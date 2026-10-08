using DG.Tweening;
using Game.View;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class PressScaleAnimatorTests
    {
        private GameObject _a, _b;
        private PressScaleAnimator _anim;

        [SetUp]
        public void SetUp()
        {
            _a = new GameObject("A", typeof(RectTransform));
            _b = new GameObject("B", typeof(RectTransform));
            _anim = new PressScaleAnimator(pressedScale: 0.9f, pressSeconds: 0.1f, releaseSeconds: 0.2f);
        }

        // 편집 모드에서는 DOTween이 매 프레임 돌지 않으므로, 진행 중인 트윈의 시간을 직접 앞당긴다
        private void Tick(float seconds)
        {
            var tween = _anim.Current;
            if (tween != null && tween.active)
            {
                tween.Goto(tween.Elapsed() + seconds);
            }
        }

        [TearDown]
        public void TearDown()
        {
            DOTween.KillAll();
            Object.DestroyImmediate(_a);
            Object.DestroyImmediate(_b);
        }

        [Test(Description = "누르면 시간이 흐르며 줄어들어 완전히 눌리면 눌림 비율이 된다")]
        public void Press_ShrinksToPressedScale()
        {
            _anim.Press(_a.transform);

            Tick(0.03f);
            Assert.That(_a.transform.localScale.x, Is.LessThan(1f).And.GreaterThan(0.9f));

            Tick(1f);
            Assert.AreEqual(0.9f, _a.transform.localScale.x, 0.0001f);
        }

        [Test(Description = "놓으면 원래 크기로 돌아오고 상태가 정리된다")]
        public void Release_RestoresExactBaseScale()
        {
            _anim.Press(_a.transform);
            Tick(1f);

            _anim.Release();
            Tick(0.1f);
            Assert.That(_a.transform.localScale.x, Is.GreaterThan(0.9f).And.LessThan(1f));

            Tick(1f);
            Assert.AreEqual(Vector3.one, _a.transform.localScale);
            Assert.IsFalse(_anim.IsActive);
        }

        [Test(Description = "눌림이 끝나기 전에 놓아도(짧게 탭) 현재 크기에서 되돌아와 줄어든 채 남지 않는다")]
        public void QuickTap_ReturnsToBase()
        {
            _anim.Press(_a.transform);
            Tick(0.02f);
            _anim.Release();
            Tick(5f);

            Assert.AreEqual(Vector3.one, _a.transform.localScale);
            Assert.IsFalse(_anim.IsActive);
        }

        [Test(Description = "원래 크기가 1이 아니어도(비균등 포함) 그 크기를 기준으로 줄이고 정확히 되돌린다")]
        public void NonUniformBaseScale_IsRespected()
        {
            _a.transform.localScale = new Vector3(2f, 1f, 1f);

            _anim.Press(_a.transform);
            Tick(1f);
            Assert.AreEqual(1.8f, _a.transform.localScale.x, 0.0001f);
            Assert.AreEqual(0.9f, _a.transform.localScale.y, 0.0001f);

            _anim.Release();
            Tick(5f);
            Assert.AreEqual(new Vector3(2f, 1f, 1f), _a.transform.localScale);
        }

        [Test(Description = "놓는 중에 다른 버튼을 누르면 앞 버튼은 바로 원래 크기로 돌아간다")]
        public void NewPress_WhileReleasing_RestoresPreviousImmediately()
        {
            _anim.Press(_a.transform);
            Tick(1f);
            _anim.Release();
            Tick(0.05f);

            _anim.Press(_b.transform);

            Assert.AreEqual(Vector3.one, _a.transform.localScale);
            Tick(1f);
            Assert.AreEqual(0.9f, _b.transform.localScale.x, 0.0001f);
        }

        [Test(Description = "누르는 중에 대상이 파괴돼도 예외 없이 정리된다")]
        public void DestroyedTarget_IsHandled()
        {
            _anim.Press(_a.transform);
            Object.DestroyImmediate(_a);

            Assert.DoesNotThrow(() => _anim.CheckTarget());
            Assert.IsFalse(_anim.IsActive);
        }

        [Test(Description = "누른 채로 대상이 꺼지면(눌러서 팝업이 닫히는 경우) 바로 원래 크기로 돌아와, 다음에 켜졌을 때 줄어 있지 않다")]
        public void DeactivatedTarget_RestoresScale()
        {
            _anim.Press(_a.transform);
            Tick(1f);
            _a.SetActive(false);

            _anim.CheckTarget();
            _a.SetActive(true);

            Assert.AreEqual(Vector3.one, _a.transform.localScale);
            Assert.IsFalse(_anim.IsActive);
        }
    }
}
