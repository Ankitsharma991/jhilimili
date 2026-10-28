using System;

namespace UtsavNights.Gameplay
{
    /// <summary>Shows the store's rating prompt. Done(true) when the flow ran, whether or not the player rated.</summary>
    public interface IReviewBackend
    {
        string Name { get; }

        void Request(Action<bool> done);
    }

    /// <summary>Published when the review prompt is requested (playtest log; Play never says whether the player rated).</summary>
    public readonly struct ReviewPromptEvent
    {
        public ReviewPromptEvent(string backend, bool flowRan, string trigger)
        {
            Backend = backend;
            FlowRan = flowRan;
            Trigger = trigger;
        }

        public string Backend { get; }

        public bool FlowRan { get; }

        /// <summary>finale, payoffs or developer.</summary>
        public string Trigger { get; }
    }
}
