using System;
using System.Collections.Generic;

namespace UtsavNights.Core.Services
{
    /// <summary>
    /// Typed publish/subscribe for decoupled systems (audio, haptics, analytics react to gameplay).
    /// Use small structs as events. Publishing allocates nothing; handlers may subscribe or unsubscribe
    /// while an event is being delivered (changes apply to the next publish).
    /// A throwing handler is logged through <see cref="HandlerFailed"/> and does not stop the others
    /// (PRD 13.3: a failed effect or sound must not crash the scene).
    /// </summary>
    public sealed class EventBus
    {
        private readonly Dictionary<Type, IHandlerList> _lists = new Dictionary<Type, IHandlerList>();

        /// <summary>Raised with the event type and exception when a handler throws.</summary>
        public event Action<Type, Exception> HandlerFailed;

        private interface IHandlerList
        {
            int Count { get; }

            void Clear();
        }

        public void Subscribe<T>(Action<T> handler) where T : struct
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            GetList<T>(true).Add(handler);
        }

        public void Unsubscribe<T>(Action<T> handler) where T : struct
        {
            if (handler != null)
            {
                GetList<T>(false)?.Remove(handler);
            }
        }

        public void Publish<T>(T evt) where T : struct
        {
            HandlerList<T> list = GetList<T>(false);
            if (list != null)
            {
                list.Invoke(evt, this);
            }
        }

        public int SubscriberCount<T>() where T : struct => GetList<T>(false)?.Count ?? 0;

        public void Clear()
        {
            foreach (IHandlerList list in _lists.Values)
            {
                list.Clear();
            }
        }

        private void ReportFailure(Type type, Exception e) => HandlerFailed?.Invoke(type, e);

        private HandlerList<T> GetList<T>(bool create) where T : struct
        {
            if (_lists.TryGetValue(typeof(T), out IHandlerList existing))
            {
                return (HandlerList<T>)existing;
            }

            if (!create)
            {
                return null;
            }

            var list = new HandlerList<T>();
            _lists.Add(typeof(T), list);
            return list;
        }

        private sealed class HandlerList<T> : IHandlerList where T : struct
        {
            private readonly List<Action<T>> _handlers = new List<Action<T>>();
            private int _depth;
            private bool _dirty;

            public int Count
            {
                get
                {
                    int n = 0;
                    foreach (Action<T> h in _handlers)
                    {
                        if (h != null)
                        {
                            n++;
                        }
                    }

                    return n;
                }
            }

            public void Add(Action<T> handler)
            {
                if (!_handlers.Contains(handler))
                {
                    _handlers.Add(handler);
                }
            }

            public void Remove(Action<T> handler)
            {
                int index = _handlers.IndexOf(handler);
                if (index < 0)
                {
                    return;
                }

                if (_depth > 0)
                {
                    // Defer compaction so in-flight iteration indices stay valid.
                    _handlers[index] = null;
                    _dirty = true;
                }
                else
                {
                    _handlers.RemoveAt(index);
                }
            }

            public void Clear()
            {
                if (_depth > 0)
                {
                    for (int i = 0; i < _handlers.Count; i++)
                    {
                        _handlers[i] = null;
                    }

                    _dirty = true;
                }
                else
                {
                    _handlers.Clear();
                }
            }

            public void Invoke(T evt, EventBus bus)
            {
                _depth++;
                try
                {
                    // Handlers added during delivery are skipped until the next publish.
                    int count = _handlers.Count;
                    for (int i = 0; i < count; i++)
                    {
                        Action<T> handler = _handlers[i];
                        if (handler == null)
                        {
                            continue;
                        }

                        try
                        {
                            handler(evt);
                        }
                        catch (Exception e)
                        {
                            bus.ReportFailure(typeof(T), e);
                        }
                    }
                }
                finally
                {
                    _depth--;
                    if (_depth == 0 && _dirty)
                    {
                        _handlers.RemoveAll(h => h == null);
                        _dirty = false;
                    }
                }
            }
        }
    }
}
