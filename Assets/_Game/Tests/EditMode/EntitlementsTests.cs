using NUnit.Framework;
using UtsavNights.Core.Monetisation;

namespace UtsavNights.Tests
{
    /// <summary>F11: the purchase persists, Restore brings it back, only a confirmed refund removes it.</summary>
    public class EntitlementsTests
    {
        private const string Sku = Entitlements.RemoveAdsSku;

        [Test]
        public void Restore_GrantsWhenTheStoreListsIt()
        {
            Assert.AreEqual(EntitlementAction.Grant, Entitlements.Sync(false, true, new[] { Sku }));
            Assert.AreEqual(EntitlementAction.Keep, Entitlements.Sync(true, true, new[] { "other", Sku }));
        }

        [Test]
        public void Refund_RevokesOnlyOnASuccessfulQuery()
        {
            Assert.AreEqual(EntitlementAction.Revoke, Entitlements.Sync(true, true, new string[0]));
            Assert.AreEqual(EntitlementAction.Keep, Entitlements.Sync(true, false, null), "offline or store error never revokes");
            Assert.AreEqual(EntitlementAction.Keep, Entitlements.Sync(false, true, null));
        }

        [TestCase(PurchaseResult.Purchased, true)]
        [TestCase(PurchaseResult.AlreadyOwned, true)]
        [TestCase(PurchaseResult.Pending, false)]
        [TestCase(PurchaseResult.Cancelled, false)]
        [TestCase(PurchaseResult.Failed, false)]
        public void OnlyCompletedOrOwnedPurchasesGrant(PurchaseResult result, bool grants)
        {
            Assert.AreEqual(grants, Entitlements.Grants(result));
        }

        [Test]
        public void Messages()
        {
            StringAssert.Contains("Thank you", Entitlements.Message(PurchaseResult.AlreadyOwned));
            StringAssert.Contains("pending", Entitlements.Message(PurchaseResult.Pending));
            Assert.IsEmpty(Entitlements.Message(PurchaseResult.Cancelled), "cancelling needs no message");
        }
    }
}
