using System.Collections.Generic;

namespace UtsavNights.Core.Monetisation
{
    public enum PurchaseResult
    {
        Purchased,

        /// <summary>Paid by a slow method (cash, bank); granted later when the store confirms.</summary>
        Pending,

        Cancelled,
        Failed,

        /// <summary>The store says the player already owns it (bought on another device): treat as bought.</summary>
        AlreadyOwned,
    }

    public enum EntitlementAction
    {
        Keep,
        Grant,
        Revoke,
    }

    /// <summary>
    /// F11 entitlement rules: one non-consumable, "Remove ads and unlock all". The cached flag in
    /// progress.json is trusted offline; only a successful store query that no longer lists the product
    /// (a refund) takes it away. A failed or offline query never revokes.
    /// </summary>
    public static class Entitlements
    {
        public const string RemoveAdsSku = "remove_ads";

        public static bool Grants(PurchaseResult result) =>
            result == PurchaseResult.Purchased || result == PurchaseResult.AlreadyOwned;

        public static EntitlementAction Sync(bool cachedOwned, bool queryOk, IReadOnlyCollection<string> ownedSkus)
        {
            if (!queryOk)
            {
                return EntitlementAction.Keep;
            }

            bool owned = false;
            if (ownedSkus != null)
            {
                foreach (string sku in ownedSkus)
                {
                    if (sku == RemoveAdsSku)
                    {
                        owned = true;
                        break;
                    }
                }
            }

            if (owned == cachedOwned)
            {
                return EntitlementAction.Keep;
            }

            return owned ? EntitlementAction.Grant : EntitlementAction.Revoke;
        }

        /// <summary>Short text for the player after a purchase attempt.</summary>
        public static string Message(PurchaseResult result)
        {
            switch (result)
            {
                case PurchaseResult.Purchased:
                case PurchaseResult.AlreadyOwned:
                    return "Thank you! Ads are gone and every cracker is unlocked.";
                case PurchaseResult.Pending:
                    return "Payment pending. It unlocks as soon as it completes.";
                case PurchaseResult.Cancelled:
                    return string.Empty;
                default:
                    return "The purchase didn't go through. Please try again.";
            }
        }
    }
}
