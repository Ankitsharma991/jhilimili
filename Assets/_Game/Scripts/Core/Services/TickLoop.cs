using System;
using System.Collections.Generic;

namespace UtsavNights.Core.Services
{
    public interface ITickable
    {
        void Tick(float deltaSeconds);
    }

    /// <summary>
    /// Central ticking (PRD 13.3): one Unity Update drives every active cracker and effect instead of
    /// hundreds of Update methods. Add and remove are safe during a tick (applied after it).
    /// A throwing tickable is reported and skipped for that frame, never stopping the rest.
    /// </summary>
    public sealed class TickLoop
    {
        private readonly List<ITickable> _items = new List<ITickable>(64);
        private readonly List<ITickable> _pendingAdd = new List<ITickable>(16);
        private readonly List<ITickable> _pendingRemove = new List<ITickable>(16);
        private bool _ticking;

        /// <summary>Raised when a tickable throws.</summary>
        public event Action<ITickable, Exception> TickFailed;

        public bool IsPaused { get; set; }

        public int Count => _items.Count + _pendingAdd.Count - _pendingRemove.Count;

        public void Add(ITickable item)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            if (_ticking)
            {
                _pendingRemove.Remove(item);
                if (!_items.Contains(item) && !_pendingAdd.Contains(item))
                {
                    _pendingAdd.Add(item);
                }

                return;
            }

            if (!_items.Contains(item))
            {
                _items.Add(item);
            }
        }

        public void Remove(ITickable item)
        {
            if (item == null)
            {
                return;
            }

            if (_ticking)
            {
                _pendingAdd.Remove(item);
                if (_items.Contains(item) && !_pendingRemove.Contains(item))
                {
                    _pendingRemove.Add(item);
                }

                return;
            }

            _items.Remove(item);
        }

        public bool Contains(ITickable item)
        {
            return (_items.Contains(item) && !_pendingRemove.Contains(item)) || _pendingAdd.Contains(item);
        }

        public void Tick(float deltaSeconds)
        {
            if (IsPaused)
            {
                return;
            }

            _ticking = true;
            try
            {
                for (int i = 0; i < _items.Count; i++)
                {
                    ITickable item = _items[i];
                    if (_pendingRemove.Count > 0 && _pendingRemove.Contains(item))
                    {
                        continue;
                    }

                    try
                    {
                        item.Tick(deltaSeconds);
                    }
                    catch (Exception e)
                    {
                        TickFailed?.Invoke(item, e);
                    }
                }
            }
            finally
            {
                _ticking = false;
                ApplyPending();
            }
        }

        public void Clear()
        {
            _items.Clear();
            _pendingAdd.Clear();
            _pendingRemove.Clear();
        }

        private void ApplyPending()
        {
            for (int i = 0; i < _pendingRemove.Count; i++)
            {
                _items.Remove(_pendingRemove[i]);
            }

            _pendingRemove.Clear();
            for (int i = 0; i < _pendingAdd.Count; i++)
            {
                if (!_items.Contains(_pendingAdd[i]))
                {
                    _items.Add(_pendingAdd[i]);
                }
            }

            _pendingAdd.Clear();
        }
    }
}
