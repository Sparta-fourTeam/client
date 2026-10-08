using System.Reflection;
using DG.Tweening;
using Game.View;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class PopupTransitionTests
    {
        private const BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance;
        private GameObject _go;
        private PopupTransition _popup;
        private CanvasGroup _group;
        private RectTransform _window;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("Panel", typeof(RectTransform));
            _go.SetActive(false);
            _popup = _go.AddComponent<PopupTransition>();
            _group = _go.GetComponent<CanvasGroup>();
            _window = new GameObject("Window", typeof(RectTransform)).GetComponent<RectTransform>();
            _window.SetParent(_go.transform);
            typeof(PopupTransition).GetField("_window", Flags).SetValue(_popup, _window);
        }

        [TearDown]
        public void TearDown()
        {
            DOTween.KillAll();
            Object.DestroyImmediate(_go);
        }

        // 편집 모드에서는 DOTween이 매 프레임 돌지 않으므로, 진행 중인 전환의 시간을 직접 앞당긴다
        private void Tick(float seconds)
        {
            var sequence = (Sequence)typeof(PopupTransition).GetField("_sequence", Flags).GetValue(_popup);
            if (sequence != null && sequence.active)
            {
                sequence.Goto(sequence.Elapsed() + seconds);
            }
        }

        // 편집 모드에서는 SetActive가 OnEnable/OnDisable을 부르지 않으므로 직접 부른다
        private void CallMessage(string name) => typeof(PopupTransition).GetMethod(name, Flags).Invoke(_popup, null);

        [Test(Description = "열면 켜지고 시간이 흐르면서 완전히 보인다 (페이드와 창 크기)")]
        public void Show_FadesAndScalesInToFullyShown()
        {
            _popup.Show();

            Assert.IsTrue(_go.activeSelf);
            Assert.AreEqual(0f, _group.alpha);
            Assert.AreEqual(0.92f, _window.localScale.x, 0.0001f);

            Tick(0.05f);
            Assert.That(_group.alpha, Is.GreaterThan(0f).And.LessThan(1f));

            Tick(1f);
            Assert.AreEqual(1f, _group.alpha);
            Assert.AreEqual(1f, _window.localScale.x, 0.0001f);
            Assert.IsTrue(_group.interactable);
            Assert.IsTrue(_group.blocksRaycasts);
        }

        [Test(Description = "닫는 동안에는 눌리지 않고, 닫기가 끝나야 패널이 꺼지며 콜백은 한 번만 불린다")]
        public void Hide_BlocksInputWhileClosing_ThenDeactivatesAndCallsBackOnce()
        {
            _popup.Show();
            Tick(1f);
            int calls = 0;

            _popup.Hide(() => calls++);

            Assert.IsFalse(_group.interactable);
            Assert.IsFalse(_group.blocksRaycasts);
            Tick(0.05f);
            Assert.IsTrue(_go.activeSelf);
            Assert.AreEqual(0, calls);

            Tick(1f);
            Assert.IsFalse(_go.activeSelf);
            Assert.AreEqual(1, calls);
            Tick(1f);
            Assert.AreEqual(1, calls);
        }

        [Test(Description = "열기·닫기를 번갈아 연타해도 마지막 동작대로 끝나고, 취소된 닫기의 콜백은 불리지 않는다")]
        public void Spam_EndsInTheLastRequestedState()
        {
            int hiddenCalls = 0;
            _popup.Show();
            Tick(0.05f);
            _popup.Hide(() => hiddenCalls++);
            Tick(0.03f);
            _popup.Show();
            Tick(0.02f);
            _popup.Hide(() => hiddenCalls++);
            Tick(0.01f);   // 닫기가 끝나기 전에 다시 연다 (끝나면 콜백이 정상적으로 불리므로 취소 상황이 아니다)
            _popup.Show();
            Tick(1f);

            Assert.IsTrue(_go.activeSelf);
            Assert.AreEqual(1f, _popup.Progress);
            Assert.AreEqual(1f, _group.alpha);
            Assert.IsTrue(_group.interactable);
            Assert.IsTrue(_group.blocksRaycasts);
            Assert.AreEqual(0, hiddenCalls);
        }

        [Test(Description = "닫다가 다시 열면 처음부터가 아니라 지금 진행도에서 이어서 열린다 (튀지 않는다)")]
        public void Reopen_WhileClosing_ContinuesFromCurrentProgress()
        {
            _popup.Show();
            Tick(1f);
            _popup.Hide();
            Tick(0.06f);   // 닫기 0.12초의 절반
            float half = _popup.Progress;

            _popup.Show();

            Assert.AreEqual(0.5f, half, 0.01f);
            Assert.AreEqual(half, _popup.Progress, 0.0001f);
            Assert.AreEqual(half, _group.alpha, 0.0001f);
        }

        [Test(Description = "이미 닫혀 있으면 닫기 콜백을 바로 부르고 켜지지 않는다")]
        public void Hide_WhenAlreadyHidden_CallsBackImmediately()
        {
            int calls = 0;

            _popup.Hide(() => calls++);

            Assert.AreEqual(1, calls);
            Assert.IsFalse(_go.activeSelf);
        }

        [Test(Description = "닫는 중에 닫기를 또 눌러도 전환이 다시 시작되지 않고 콜백은 모두 한 번씩 불린다")]
        public void Hide_WhileClosing_KeepsOneTransitionAndCallsAllCallbacks()
        {
            _popup.Show();
            Tick(1f);
            int first = 0, second = 0;

            _popup.Hide(() => first++);
            Tick(0.06f);
            _popup.Hide(() => second++);
            Tick(0.06f);

            Assert.IsFalse(_go.activeSelf);
            Assert.AreEqual((1, 1), (first, second));
        }

        [Test(Description = "Show를 거치지 않고 다른 코드가 SetActive(true)로 켜면 전환 없이 바로 완전히 보인다")]
        public void ExternalActivation_SnapsToFullyShown()
        {
            _go.SetActive(true);
            CallMessage("OnEnable");

            Assert.AreEqual(1f, _popup.Progress);
            Assert.AreEqual(1f, _group.alpha);
            Assert.IsTrue(_group.interactable);
            Assert.IsTrue(_popup.IsShown);
        }

        [Test(Description = "꺼졌다가 다시 열면 처음 상태(투명)에서 시작한다")]
        public void Disabled_ResetsSoNextShowStartsFromHidden()
        {
            _popup.Show();
            Tick(1f);
            _go.SetActive(false);
            CallMessage("OnDisable");

            _popup.Show();

            Assert.AreEqual(0f, _popup.Progress);
            Assert.AreEqual(0f, _group.alpha);
        }

        [Test(Description = "HideImmediate는 전환 없이 바로 끄고 처음 상태로 되돌린다")]
        public void HideImmediate_TurnsOffAndResets()
        {
            _popup.Show();
            Tick(1f);

            _popup.HideImmediate();

            Assert.IsFalse(_go.activeSelf);
            Assert.AreEqual(0f, _popup.Progress);
            Assert.IsFalse(_popup.IsShown);
        }
    }
}
