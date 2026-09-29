using System.Collections.Generic;
using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    public class TableTests
    {
        [Test(Description = "존재하는 키를 조회하면 값을 반환한다")]
        public void GetOrThrow_WhenKeyExists_ReturnsValue()
        {
            var table = new Table<int, string>(new Dictionary<int, string> { { 1, "a" } });

            Assert.AreEqual("a", table.GetOrThrow(1));
        }

        [Test(Description = "없는 키를 조회하면 UNKNOWN_DATA_ID로 거절한다")]
        public void GetOrThrow_WhenKeyMissing_ThrowsApiException()
        {
            var table = new Table<int, string>(new Dictionary<int, string>());

            var ex = Assert.Throws<ApiException>(() => table.GetOrThrow(999));

            Assert.AreEqual(ApiErrorKind.Rejected, ex.Kind);
            Assert.AreEqual("UNKNOWN_DATA_ID", ex.Code);
        }

        [Test(Description = "존재하는 키는 Contains가 true를 반환한다")]
        public void Contains_WhenKeyExists_ReturnsTrue()
        {
            var table = new Table<int, string>(new Dictionary<int, string> { { 1, "a" } });

            Assert.IsTrue(table.Contains(1));
        }

        [Test(Description = "없는 키는 Contains가 false를 반환한다")]
        public void Contains_WhenKeyMissing_ReturnsFalse()
        {
            var table = new Table<int, string>(new Dictionary<int, string>());

            Assert.IsFalse(table.Contains(999));
        }
    }
}
