using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Purchasing;
using UtsavNights.Core.Monetisation;
using UtsavNights.Gameplay;
using PurchaseResult = UtsavNights.Core.Monetisation.PurchaseResult;

namespace UtsavNights.Purchasing
{
    /// <summary>
    /// Google Play Billing through Unity IAP 5 for <see cref="PurchaseService"/>. One non-consumable,
    /// "remove_ads". Orders are confirmed on the device (no game server; PRD 16 keeps no accounts), which
    /// is acceptable for a one-time unlock. Pending payments that clear later raise
    /// <see cref="PurchaseCompletedLater"/>.
    /// </summary>
    public sealed class UnityIapBackend : IPurchaseBackend
    {
        private StoreController _store;
        private Action<string> _initDone;
        private Action<PurchaseResult> _buyDone;
        private Action<bool, IReadOnlyCollection<string>> _queryDone;

        public event Action<string> PurchaseCompletedLater;

        public string Name => "google_play";

        public bool IsReady { get; private set; }

        public void Initialize(Action<string> done)
        {
            _initDone = done;
            _store = UnityIAPServices.StoreController();
            _store.OnProductsFetched += OnProductsFetched;
            _store.OnProductsFetchFailed += failed => FinishInit("products not available: " + failed.FailureReason);
            _store.OnPurchasePending += order => _store.ConfirmPurchase(order);
            _store.OnPurchaseConfirmed += OnPurchaseConfirmed;
            _store.OnPurchaseFailed += OnPurchaseFailed;
            _store.OnPurchaseDeferred += order => FinishBuy(PurchaseResult.Pending);
            _store.OnPurchasesFetched += OnPurchasesFetched;
            _store.OnPurchasesFetchFailed += failure => FinishQuery(false, null);
            Connect();
        }

        public string PriceText(string sku)
        {
            Product product = _store?.GetProductById(sku);
            return product != null && product.availableToPurchase ? product.metadata.localizedPriceString : string.Empty;
        }

        public void Buy(string sku, Action<PurchaseResult> done)
        {
            Product product = _store?.GetProductById(sku);
            if (product == null || !product.availableToPurchase || _buyDone != null)
            {
                done?.Invoke(PurchaseResult.Failed);
                return;
            }

            _buyDone = done;
            _store.PurchaseProduct(product);
        }

        public void QueryOwned(Action<bool, IReadOnlyCollection<string>> done)
        {
            if (_store == null || !IsReady)
            {
                done?.Invoke(false, null);
                return;
            }

            _queryDone = done;
            _store.FetchPurchases();
        }

        private async void Connect()
        {
            try
            {
                await _store.Connect();
            }
            catch (Exception e)
            {
                FinishInit("store not connected: " + e.Message);
                return;
            }

            _store.FetchProducts(new List<ProductDefinition> { new ProductDefinition(Entitlements.RemoveAdsSku, ProductType.NonConsumable) });
        }

        private void OnProductsFetched(List<Product> products)
        {
            IsReady = true;
            FinishInit(null);
        }

        private void OnPurchaseConfirmed(Order order)
        {
            if (order is FailedOrder failed)
            {
                Debug.LogWarning($"[Store] confirmation failed: {failed.FailureReason}");
                FinishBuy(PurchaseResult.Failed);
                return;
            }

            if (!Contains(order, Entitlements.RemoveAdsSku))
            {
                return;
            }

            if (_buyDone != null)
            {
                FinishBuy(PurchaseResult.Purchased);
            }
            else
            {
                PurchaseCompletedLater?.Invoke(Entitlements.RemoveAdsSku);
            }
        }

        private void OnPurchaseFailed(FailedOrder order)
        {
            switch (order.FailureReason)
            {
                case PurchaseFailureReason.UserCancelled:
                    FinishBuy(PurchaseResult.Cancelled);
                    break;
                case PurchaseFailureReason.DuplicateTransaction:
                    FinishBuy(PurchaseResult.AlreadyOwned);
                    break;
                case PurchaseFailureReason.ExistingPurchasePending:
                    FinishBuy(PurchaseResult.Pending);
                    break;
                default:
                    Debug.LogWarning($"[Store] purchase failed: {order.FailureReason} {order.Details}");
                    FinishBuy(PurchaseResult.Failed);
                    break;
            }
        }

        private void OnPurchasesFetched(Orders orders)
        {
            var owned = new List<string>();
            foreach (ConfirmedOrder order in orders.ConfirmedOrders)
            {
                foreach (CartItem item in order.CartOrdered.Items())
                {
                    owned.Add(item.Product.definition.id);
                }
            }

            FinishQuery(true, owned);
        }

        private static bool Contains(Order order, string sku)
        {
            foreach (CartItem item in order.CartOrdered.Items())
            {
                if (item.Product.definition.id == sku)
                {
                    return true;
                }
            }

            return false;
        }

        private void FinishInit(string error)
        {
            Action<string> done = _initDone;
            _initDone = null;
            done?.Invoke(error);
        }

        private void FinishBuy(PurchaseResult result)
        {
            Action<PurchaseResult> done = _buyDone;
            _buyDone = null;
            done?.Invoke(result);
        }

        private void FinishQuery(bool ok, IReadOnlyCollection<string> owned)
        {
            Action<bool, IReadOnlyCollection<string>> done = _queryDone;
            _queryDone = null;
            done?.Invoke(ok, owned);
        }
    }
}
