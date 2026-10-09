using System.Reflection;
using DG.Tweening;
using Game.Core;
using Game.Core.Messages;
using Game.View;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Tests
{
    public sealed class WaveHudViewTests
    {
        private GameObject _root;
        private WaveHudView _view;
        private TMP_Text _text;
        private Image _fill;
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("WaveHud");
            _view = _root.AddComponent<WaveHudView>();
            _text = new GameObject("Number", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
            _fill = new GameObject("Fill", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            _text.transform.SetParent(_root.transform);
            _fill.transform.SetParent(_root.transform);
            typeof(WaveHudView).GetField("_waveValueText", Private).SetValue(_view, _text);
            typeof(WaveHudView).GetField("_gaugeFill", Private).SetValue(_view, _fill);
        }

        [TearDown]
        public void TearDown()
        {
            DOTween.KillAll();
            Object.DestroyImmediate(_root);
        }

        // 게이지는 부드럽게 차오르므로, 값을 확인하기 전에 진행 중인 트윈을 끝까지 보낸다
        private void Settle() => DOTween.CompleteAll();

        private void Gauge(int index, int current, int max) =>
            typeof(WaveHudView).GetMethod("OnGaugeChanged", Private).Invoke(_view,
                new object[] { new WaveGaugeChanged(index, current, max) });

        private void State(StageState state) =>
            typeof(WaveHudView).GetMethod("OnStateChanged", Private).Invoke(_view,
                new object[] { new StageStateChanged(state) });

        [Test]
        public void NextWaveBeforeCardSelection_ShowsCompletedWaveUntilPlaying()
        {
            State(StageState.Playing);
            Gauge(2, 0, 4);
            State(StageState.CardSelect);
            Settle();
            Assert.AreEqual("1/20", _text.text);
            Assert.AreEqual(1f, _fill.fillAmount);

            State(StageState.Paused);
            Assert.AreEqual("1/20", _text.text);
            State(StageState.CardSelect);
            State(StageState.Playing);
            Settle();
            Assert.AreEqual("2/20", _text.text);
            Assert.AreEqual(0f, _fill.fillAmount);
            Gauge(2, 2, 4);
            Settle();
            Assert.AreEqual(0.5f, _fill.fillAmount);
        }

        [Test]
        public void BufferedDefault_DoesNotDisplayACompletedWave()
        {
            Gauge(0, 0, 0);
            State(StageState.Starting);
            Assert.AreEqual(string.Empty, _text.text);
            Assert.AreEqual(0f, _fill.fillAmount);
        }
    }
}
