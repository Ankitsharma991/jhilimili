using System;
using System.Collections.Generic;
using UtsavNights.Core.Monetisation;

namespace UtsavNights.Gameplay
{
    /// <summary>
    /// One store (Google Play through Unity IAP from UtsavNights.Purchasing, or the development store). Callbacks run
    /// on the main thread exactly once and never throw into gameplay.
    /// </summary>
    public interface IPurchaseBackend
    {
        string Name { get; }

        bool IsReady { get; }

        void Initialize(Action<string> done);

        /// <summary>Localised price from the store, or empty before it is known.</summary>
        string PriceText(string sku);

        void Buy(string sku, Action<PurchaseResult> done);

        /// <summary>Asks the store what the player owns. <c>ok</c> is false when offline or on error.</summary>
        void QueryOwned(Action<bool, IReadOnlyCollection<string>> done);

        /// <summary>A purchase completed outside <see cref="Buy"/> (a pending payment that cleared later).</summary>
        event Action<string> PurchaseCompletedLater;
    }
}
