using Game.Network;
using NUnit.Framework;

namespace Game.Tests
{
    public class AppStoreUpdateTests
    {
        private const string BundleId = "com.example.nova";
        private const string StoreUrl = "https://apps.apple.com/kr/app/id123456789";

        [TestCase("1.10", "1.9", true)]
        [TestCase("2.0.0", "1.99.99", true)]
        [TestCase("1.0.1", "1.0", true)]
        [TestCase("1.0.0", "1.0", false)]
        [TestCase("1.0", "1.0.0", false)]
        [TestCase("1.9", "1.10", false)]
        [TestCase("2.invalid", "1.0", false)]
        [TestCase("2.0", "invalid", false)]
        public void OnlyNewerNumericVersionsOfferAnUpdate(string available, string installed, bool expected)
        {
            var update = AppStoreUpdateParser.Parse(Response(available), BundleId, installed);

            Assert.That(update != null, Is.EqualTo(expected));
            if (expected)
            {
                Assert.That(update.StoreUrl, Is.EqualTo(StoreUrl));
            }
        }

        [TestCase("com.other.app", StoreUrl)]
        [TestCase(BundleId, "https://example.test/app")]
        [TestCase(BundleId, "https://apps.apple.com.example.test/app")]
        [TestCase(BundleId, "http://apps.apple.com/app")]
        public void MismatchedAppOrNonStoreLinkDoesNotOfferAnUpdate(string bundleId, string url)
        {
            Assert.That(AppStoreUpdateParser.Parse(Response("2.0", bundleId, url), BundleId, "1.0"), Is.Null);
        }

        [TestCase("{\"resultCount\":0,\"results\":[]}")]
        [TestCase("{\"results\":[null]}")]
        [TestCase("{}")]
        [TestCase("invalid json")]
        [TestCase("")]
        public void UnpublishedAppOrInvalidResponseDoesNotOfferAnUpdate(string body)
        {
            Assert.That(AppStoreUpdateParser.Parse(new HttpResponseData(200, body), BundleId, "1.0"), Is.Null);
            Assert.That(AppStoreUpdateParser.Parse(new HttpResponseData(503, body), BundleId, "1.0"), Is.Null);
        }

        private static HttpResponseData Response(string version, string bundleId = BundleId, string url = StoreUrl) =>
            new HttpResponseData(200, $"{{\"results\":[{{\"bundleId\":\"{bundleId}\",\"version\":\"{version}\",\"trackViewUrl\":\"{url}\"}}]}}");
    }
}
