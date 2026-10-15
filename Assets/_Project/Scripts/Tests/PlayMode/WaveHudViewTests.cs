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
        private IServiceProvider _services;
        private GameObject _root;
        private WaveHudView _view;
        private TMP_Text _text;
        private Image _fill;
        private float _timeScale;
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [SetUp]
        public void SetUp()
        {
            _timeScale = Time.timeScale;
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
            _services = provider;
            _provider = provider as IDisposable;
            _gaugePublisher = provider.GetRequiredService<IBufferedPublisher<WaveGaugeChanged>>();
            _statePublisher = provider.GetRequiredService<IBufferedPublisher<StageStateChanged>>();
            _view.Construct(provider.GetRequiredService<IBufferedSubscriber<WaveGaugeChanged>>(),
                provider.GetRequiredService<IBufferedSubscriber<StageStateChanged>>());
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = _timeScale;
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

#if UNITY_EDITOR
        private Image UseAuthoredWave()
        {
            _view.Dispose();
            UnityEngine.Object.Destroy(_root);
            _root = new GameObject("WaveCanvas", typeof(RectTransform), typeof(Canvas));
            _root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/Stage/HudCanvas.prefab");
            _view = UnityEngine.Object.Instantiate(prefab.GetComponentInChildren<WaveHudView>(true), _root.transform);
            _text = Field<TMP_Text>("_waveValueText");
            _fill = Field<Image>("_gaugeFill");
            _view.Construct(_services.GetRequiredService<IBufferedSubscriber<WaveGaugeChanged>>(),
                _services.GetRequiredService<IBufferedSubscriber<StageStateChanged>>());
            return Field<Image>("_gaugeGlow");
        }

        private T Field<T>(string name) => (T)typeof(WaveHudView).GetField(name, Private).GetValue(_view);

        private static IEnumerator Until(Func<bool> condition)
        {
            float deadline = Time.realtimeSinceStartup + 3f;
            while (!condition() && Time.realtimeSinceStartup < deadline) { yield return null; }
            Assert.That(condition(), Is.True, "실제 프레임에서 HUD 전환이 완료되어야 한다");
        }

        [UnityTest]
        public IEnumerator Glow_RepeatedFullAndDecreaseFollowAuthoredFillWhilePaused()
        {
            var glow = UseAuthoredWave();
            Time.timeScale = 0f;
            State(StageState.Playing);
            Gauge(2, 2, 4);
            yield return Fill(0.5f);
            Assert.That(glow.enabled, Is.False);
            Gauge(2, 4, 4);
            Gauge(3, 0, 6);
            State(StageState.CardSelect);
            yield return Until(() => glow.enabled && glow.color.a > 0.3f);
            // Repeated buffered state notifications must not restart the longer glow fade.
            float deadline = Time.realtimeSinceStartup + 3f;
            while (Field<float>("_glowOn") < 0.999f && Time.realtimeSinceStartup < deadline)
            {
                State(StageState.CardSelect);
                yield return null;
            }
            Assert.That(Field<float>("_glowOn"), Is.EqualTo(1f).Within(0.001f));
            Assert.That(glow.rectTransform.anchorMax.x, Is.EqualTo(_fill.fillAmount).Within(0.001f));
            State(StageState.Playing);
            yield return Fill(0f);
            yield return Until(() => !glow.enabled && glow.color.a < 0.001f);
            Gauge(3, 3, 6);
            yield return Fill(0.5f);
            Assert.That(glow.enabled, Is.False);
            Assert.That(glow.rectTransform.anchorMax.x, Is.EqualTo(0.5f).Within(0.001f));
        }

        [UnityTest]
        public IEnumerator Glow_DisableStopsAnimationAndReenableResumesCurrentState()
        {
            var glow = UseAuthoredWave();
            Time.timeScale = 0f;
            Gauge(2, 4, 4);
            Gauge(3, 0, 6);
            State(StageState.CardSelect);
            yield return Until(() => Field<float>("_glowOn") >= 0.999f);
            _view.gameObject.SetActive(false);
            var disabledColor = glow.color;
            for (int frame = 0; frame < 12; frame++)
            {
                yield return null;
                Assert.That(glow.color, Is.EqualTo(disabledColor), "숨긴 HUD의 트윈은 계속 갱신되면 안 된다");
            }
            _view.gameObject.SetActive(true);
            yield return Until(() => glow.enabled && glow.color != disabledColor);
            // A state change delivered while disabled must be shown when the HUD returns.
            _view.gameObject.SetActive(false);
            State(StageState.Playing);
            Gauge(3, 0, 6);
            _view.gameObject.SetActive(true);
            yield return Fill(0f);
            Assert.That(glow.enabled, Is.False);
            State(StageState.CardSelect);
            yield return Until(() => glow.enabled);
            object breathing = Field<object>("_glowTween");
            UnityEngine.Object.Destroy(_view.gameObject);
            yield return null;
            Assert.That((bool)breathing.GetType().GetProperty("active").GetValue(breathing), Is.False,
                "삭제한 HUD의 반복 글로우 트윈이 남으면 안 된다");
        }
#endif

    }
}
