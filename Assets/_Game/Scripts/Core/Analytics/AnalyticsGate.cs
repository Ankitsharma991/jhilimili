using System.Collections.Generic;

namespace UtsavNights.Core.Analytics
{
    /// <summary>
    /// Holds events until consent is known (app_first_open and scene_ready fire before the consent
    /// check finishes), sends them once allowed, and drops them when not. Never sends while denied.
    /// </summary>
    public sealed class AnalyticsGate
    {
        public const int MaxHeld = 50;

        private readonly Queue<KeyValuePair<string, EventParams>> _held = new Queue<KeyValuePair<string, EventParams>>();

        public enum State
        {
            Undecided,
            Allowed,
            Denied,
        }

        public State Current { get; private set; } = State.Undecided;

        public int HeldCount => _held.Count;

        /// <summary>Returns true when the event should be sent now; holds it while undecided.</summary>
        public bool Offer(string eventName, EventParams parameters)
        {
            switch (Current)
            {
                case State.Allowed:
                    return true;
                case State.Denied:
                    return false;
                default:
                    if (_held.Count >= MaxHeld)
                    {
                        _held.Dequeue();
                    }

                    _held.Enqueue(new KeyValuePair<string, EventParams>(eventName, parameters));
                    return false;
            }
        }

        /// <summary>Back to holding (consent asked again); nothing is sent until <see cref="Decide"/>.</summary>
        public void Hold() => Current = State.Undecided;

        /// <summary>Sets the consent decision. When it becomes allowed, returns the held events to send now.</summary>
        public List<KeyValuePair<string, EventParams>> Decide(bool allowed)
        {
            var release = new List<KeyValuePair<string, EventParams>>();
            Current = allowed ? State.Allowed : State.Denied;
            if (allowed)
            {
                release.AddRange(_held);
            }

            _held.Clear();
            return release;
        }
    }
}
