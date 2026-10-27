using System;
using System.Collections.Generic;
using UnityEngine;
using UtsavNights.Core.Monetisation;
using UtsavNights.Core.Runtime;

namespace UtsavNights.Gameplay
{
    /// <summary>PRD 17 purchase_started / purchase_done (and failures) for the playtest log and analytics.</summary>
    public readonly struct PurchaseEvent
    {
        public PurchaseEvent(string type, string sku, PurchaseResult result)
        {
            Type = type;
            Sku = sku;
            Result = result;
        }

        /// <summary>purchase_started, purchase_done, purchase_pending, purchase_cancelled, purchase_failed or purchase_restored.</summary>
        public string Type { get; }

        public string Sku { get; }

        public PurchaseResult Result { get; }
    }

    /// <summary>
    /// The Remove-ads purchase (F11): buy, restore, and a quiet store check at launch that brings the
    /// purchase back on a new install and removes it after a refund. The cached entitlement in
    /// progress.json works offline.
    /// </summary>
    public sealed class PurchaseService
    {
        private readonly ProgressService _progress;

        public PurchaseService(IPurchaseBackend backend, ProgressService progress)
        {
            Backend = backend;
            _progress = progress;
            Backend.PurchaseCompletedLater += sku =>
            {
                _progress?.SetPurchased(sku);
                Game.Events.Publish(new PurchaseEvent("purchase_done", sku, PurchaseResult.Purchased));
                StateChanged?.Invoke();
            };
        }

        /// <summary>Raised when the price, readiness or a purchase in flight changes.</summary>
        public event Action StateChanged;

        public IPurchaseBackend Backend { get; }

        public bool Busy { get; private set; }

        public bool Owned => _progress != null && _progress.AdsRemoved;

        public string Price => Backend.PriceText(Entitlements.RemoveAdsSku);

        public void Initialize()
        {
            Backend.Initialize(error =>
            {
                if (error != null)
                {
                    Debug.LogWarning($"[Store] {error}");
                    StateChanged?.Invoke();
                    return;
                }

                Restore(null, quiet: true);
            });
        }

        /// <summary>Buys Remove ads; <c>done</c> gets the result and a message for the player.</summary>
        public void BuyRemoveAds(Action<PurchaseResult, string> done)
        {
            const string sku = Entitlements.RemoveAdsSku;
            if (Busy || !Backend.IsReady)
            {
                done?.Invoke(PurchaseResult.Failed, Backend.IsReady ? string.Empty : "The store isn't available right now.");
                return;
            }

            Busy = true;
            StateChanged?.Invoke();
            Game.Events.Publish(new PurchaseEvent("purchase_started", sku, PurchaseResult.Pending));
            Backend.Buy(sku, result =>
            {
                Busy = false;
                if (Entitlements.Grants(result))
                {
                    _progress?.SetPurchased(sku);
                }

                Game.Events.Publish(new PurchaseEvent(EventType(result), sku, result));
                StateChanged?.Invoke();
                done?.Invoke(result, Entitlements.Message(result));
            });
        }

        /// <summary>Settings "Restore purchases": asks the store and syncs the cached entitlement.</summary>
        public void Restore(Action<string> done, bool quiet = false)
        {
            if (!Backend.IsReady)
            {
                done?.Invoke("The store isn't available right now.");
                return;
            }

            Backend.QueryOwned((ok, owned) =>
            {
                EntitlementAction action = Entitlements.Sync(Owned, ok, owned);
                if (action == EntitlementAction.Grant)
                {
                    _progress?.SetPurchased(Entitlements.RemoveAdsSku);
                    Game.Events.Publish(new PurchaseEvent("purchase_restored", Entitlements.RemoveAdsSku, PurchaseResult.AlreadyOwned));
                }
                else if (action == EntitlementAction.Revoke)
                {
                    _progress?.ClearPurchase();
                    Debug.Log("[Store] purchase no longer owned (refunded); ads and locks return");
                }

                StateChanged?.Invoke();
                if (!quiet)
                {
                    done?.Invoke(!ok ? "Couldn't reach the store. Check your connection and try again."
                        : Owned ? "Purchase restored. Thank you!" : "No purchases found for this account.");
                }
            });
        }

        private static string EventType(PurchaseResult result)
        {
            switch (result)
            {
                case PurchaseResult.Purchased:
                case PurchaseResult.AlreadyOwned:
                    return "purchase_done";
                case PurchaseResult.Pending:
                    return "purchase_pending";
                case PurchaseResult.Cancelled:
                    return "purchase_cancelled";
                default:
                    return "purchase_failed";
            }
        }
    }
}
