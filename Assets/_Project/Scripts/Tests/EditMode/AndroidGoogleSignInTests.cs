using System;
using Game.Core;
using Game.Network;
using NUnit.Framework;

namespace Game.Tests
{
    public class AndroidGoogleSignInTests
    {
        [Test(Description = "에디터(Android 기기가 아님)에서는 구글 로그인을 쓸 수 없어 로그인창에서 준비 중으로 막히고, 불러도 네이티브를 부르지 않고 거절한다")]
        public void InEditor_IsUnavailableAndRejectsSignIn()
        {
            var google = new AndroidGoogleSignIn();

            Assert.AreEqual(LoginMethod.Google, google.Method);
            Assert.IsFalse(google.IsAvailable);
            Assert.Throws<NotSupportedException>(() => google.SignInAsync().GetAwaiter().GetResult());
        }

        [Test(Description = "에디터용 가짜 구글 로그인은 쓸 수 있고, 고정 계정 ID로 바로 성공한다")]
        public void EditorFake_IsAvailableAndReturnsFixedAccountId()
        {
            var fake = new EditorGoogleSignIn();

            Assert.AreEqual(LoginMethod.Google, fake.Method);
            Assert.IsTrue(fake.IsAvailable);
            Assert.AreEqual(EditorGoogleSignIn.FakeAccountId, fake.SignInAsync().GetAwaiter().GetResult());
        }
    }
}
