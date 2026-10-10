using System;
using System.Collections;
using System.Reflection;
using Game.Core;
using Game.Core.Messages;
using Game.View;
using MessagePipe;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Game.Tests
{
    public sealed class WaveHudViewTests
    {
        private IBufferedPublisher<WaveGaugeChanged> _gaugePublisher;
        private IBufferedPublisher<StageStateChanged> _statePublisher;
        private IDisposable _provider;
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
            var builder = new BuiltinContainerBuilder();
            builder.AddMessagePipe();
            builder.AddMessageBroker<WaveGaugeChanged>();
            builder.AddMessageBroker<StageStateChanged>();
            IServiceProvider provider = builder.BuildServiceProvider();
            _provider = provider as IDisposable;
            _gaugePublisher = provider.GetRequiredService<IBufferedPublisher<WaveGaugeChanged>>();
            _statePublisher = provider.GetRequiredService<IBufferedPublisher<StageStateChanged>>();
            _view.Construct(provider.GetRequiredService<IBufferedSubscriber<WaveGaugeChanged>>(),
                provider.GetRequiredService<IBufferedSubscriber<StageStateChanged>>());
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            UnityEngine.Object.Destroy(_root);
            yield return null;
            _provider?.Dispose();
            _provider = null;
        }

        private void Gauge(int index, int current, int max) => _gaugePublisher.Publish(new WaveGaugeChanged(index, current, max));
        private void State(StageState state) => _statePublisher.Publish(new StageStateChanged(state));

        private IEnumerator Fill(float expected)
        {
            float deadline = Time.realtimeSinceStartup + 3f;
            while (Mathf.Abs(_fill.fillAmount - expected) > 0.0001f && Time.realtimeSinceStartup < deadline) { yield return null; }
            Assert.AreEqual(expected, _fill.fillAmount, 0.0001f);
        }

        // 마지막 처치와 카드 선택의 같은 프레임 순서 회귀
        [UnityTest]
        public IEnumerator LastKill_DefersNextGaugeAcrossCardSelectionAndResetsOnResume()
        {
            State(StageState.Playing);
            Gauge(2, 3, 4);
            Gauge(2, 4, 4);
            yield return Fill(1f);
            Gauge(3, 0, 6);          // 다음 웨이브 신호가 먼저 온다 (같은 프레임)
            State(StageState.CardSelect);
            yield return null;
            yield return Fill(1f);

            Assert.AreEqual($"2/{StageRewardRules.WaveCount}", _text.text);
            Assert.AreEqual(1f, _fill.fillAmount, 0.0001f);
            State(StageState.Paused);
            yield return null;
            Assert.AreEqual($"2/{StageRewardRules.WaveCount}", _text.text);
            State(StageState.Playing);
            yield return Fill(0f);
            Assert.AreEqual($"3/{StageRewardRules.WaveCount}", _text.text);
            Gauge(3, 6, 6);
            yield return Fill(1f);
            Gauge(4, 0, 8);
            Assert.AreEqual($"3/{StageRewardRules.WaveCount}", _text.text, "같은 프레임에는 이전 표시를 유지한다");
            yield return null;
            yield return Fill(0f);
            Assert.AreEqual($"4/{StageRewardRules.WaveCount}", _text.text);
        }

    }
}
