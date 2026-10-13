using System;
using System.Collections;
using System.Reflection;
using Cysharp.Threading.Tasks;
using Game.Core;
using Game.View;
using NUnit.Framework;
using TMPro;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Game.Tests
{
    public sealed class PopupTransitionTests
    {
        private GameObject _go;
        private PopupTransition _popup;
        private CanvasGroup _group;
        private float _timeScale;

        [SetUp]
        public void SetUp()
        {
            _timeScale = Time.timeScale;
            _go = new GameObject("Panel", typeof(RectTransform));
            _go.SetActive(false);
            _popup = _go.AddComponent<PopupTransition>();
            _group = _go.GetComponent<CanvasGroup>();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = _timeScale;
            UnityEngine.Object.Destroy(_go);
            yield return null;
        }

        private static IEnumerator Until(Func<bool> finished)
        {
            float deadline = Time.realtimeSinceStartup + 3f;
            while (!finished() && Time.realtimeSinceStartup < deadline) { yield return null; }
            Assert.IsTrue(finished(), "Popup transition did not finish within three seconds.");
        }

#if UNITY_EDITOR
        private sealed class MemorySettingsStore : ISettingsStore
        {
            public bool TryGet(string key, out int value) { value = 0; return false; }
            public void Set(string key, int value) { }
        }

        [UnityTest]
        public IEnumerator SettingsDiagnosticCopy_RemainsClickableAfterRepeatedTransitions()
        {
            string clipboard = GUIUtility.systemCopyBuffer;
            var canvas = new GameObject("Settings test canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.GetComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvas.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1080, 1920);
            var events = new GameObject("Settings test events", typeof(EventSystem));
            try
            {
                UnityEngine.Object.Destroy(_go);
                _go = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/_Project/Prefabs/UI/Lobby/SettingsPopup.prefab"), canvas.transform, false);
                var settings = _go.GetComponent<SettingsPopupView>();
                var game = new GameSettings(new MemorySettingsStore());
                settings.Construct(null, game);
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                _popup = (PopupTransition)typeof(SettingsPopupView).GetField("_transition", flags).GetValue(settings);
                var copy = (Button)typeof(SettingsPopupView).GetField("_diagnosticCopyButton", flags).GetValue(settings);
                var label = (TMP_Text)typeof(SettingsPopupView).GetField("_diagnosticCopyLabel", flags).GetValue(settings);
                var scroll = _go.GetComponentInChildren<ScrollRect>(true);
                int frame = Time.frameCount;
                for (int i = 0; i < 3; i++)
                {
                    settings.Open();
                    yield return Until(() => _popup.Progress == 1f);
                    typeof(SettingsPopupView).GetField("_diagnosticId", flags).SetValue(settings, "synthetic-runtime-installation-id");
                    game.SetLanguage(GameLanguage.English);
                    Canvas.ForceUpdateCanvases();
                    scroll.verticalNormalizedPosition = 0;
                    yield return null;
                    var pointer = new PointerEventData(events.GetComponent<EventSystem>())
                    {
                        position = RectTransformUtility.WorldToScreenPoint(null, ((RectTransform)copy.transform).TransformPoint(((RectTransform)copy.transform).rect.center)),
                        button = PointerEventData.InputButton.Left
                    };
                    var hits = new System.Collections.Generic.List<RaycastResult>();
                    events.GetComponent<EventSystem>().RaycastAll(pointer, hits);
                    Assert.IsTrue(hits.Count > 0 && ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject) == copy.gameObject, "스크롤 하단의 복사 버튼에 실제 UI raycast가 도달한다");
                    ExecuteEvents.Execute(copy.gameObject, pointer, ExecuteEvents.pointerClickHandler);
                    Assert.IsTrue(GUIUtility.systemCopyBuffer == "synthetic-runtime-installation-id");
                    Assert.AreEqual("Copied", label.text);
                    game.SetLanguage(GameLanguage.Korean);
                    Assert.AreEqual("복사 완료", label.text);
                    settings.Close();
                    yield return Until(() => !settings.IsOpen);
                }
                Assert.Greater(Time.frameCount, frame, "실제 프레임이 진행해야 전환 검증으로 인정한다");
                settings.Open(); settings.Close(); settings.Open();
                yield return Until(() => _popup.Progress == 1f);
                Assert.IsTrue(settings.IsOpen);
                Assert.AreEqual("ID 복사", label.text);
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                GUIUtility.systemCopyBuffer = clipboard;
                UnityEngine.Object.Destroy(canvas);
                UnityEngine.Object.Destroy(events);
            }
        }

        [UnityTest]
        public IEnumerator ConfirmPopup_SecondPromptSurvivesClosingTransitionAndCanReopen()
        {
            UnityEngine.Object.Destroy(_go);
            _go = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/Prefabs/UI/Lobby/ConfirmPopup.prefab"));
            var confirm = _go.GetComponent<ConfirmPopupView>();
            confirm.Initialize();
            var flags = BindingFlags.NonPublic | BindingFlags.Instance;
            _popup = (PopupTransition)typeof(ConfirmPopupView).GetField("_transition", flags).GetValue(confirm);
            var accept = (Button)typeof(ConfirmPopupView).GetField("_confirmButton", flags).GetValue(confirm);
            var cancel = (Button)typeof(ConfirmPopupView).GetField("_cancelButton", flags).GetValue(confirm);
            var input = (TMP_InputField)typeof(ConfirmPopupView).GetField("_input", flags).GetValue(confirm);
            var flow = ConfirmTwice(confirm);
            yield return Until(() => _popup.Progress == 1f);
            accept.onClick.Invoke();
            Assert.IsTrue(input.gameObject.activeSelf);
            yield return Until(() => _popup.Progress == 1f);
            Assert.IsFalse(accept.interactable);
            input.text = "Review_Ninja";
            Assert.IsTrue(accept.interactable);
            accept.onClick.Invoke();
            yield return Until(() => flow.Status != UniTaskStatus.Pending);
            Assert.IsTrue(flow.GetAwaiter().GetResult());
            yield return Until(() => !_popup.gameObject.activeSelf);

            var retry = confirm.ShowAsync(new ConfirmRequest { Title = "Retry", CancelLabel = "Cancel" });
            yield return Until(() => _popup.Progress == 1f);
            cancel.onClick.Invoke();
            yield return Until(() => retry.Status != UniTaskStatus.Pending);
            Assert.IsFalse(retry.GetAwaiter().GetResult());
            yield return Until(() => !_popup.gameObject.activeSelf);
        }

        private static async UniTask<bool> ConfirmTwice(ConfirmPopupView confirm)
        {
            if (!await confirm.ShowAsync(new ConfirmRequest { Title = "First", CancelLabel = "Cancel" })) { return false; }
            return await confirm.ShowAsync(new ConfirmRequest
            {
                Title = "Second",
                RequiredInput = "Review_Ninja",
                CancelLabel = "Cancel",
            });
        }
#endif

        [UnityTest]
        public IEnumerator Hide_BlocksInputWhileClosing_ThenDeactivatesAndCallsBackOnce()
        {
            Time.timeScale = 0f;
            _popup.Show();
            Assert.IsTrue(_go.activeSelf);
            yield return Until(() => _popup.Progress == 1f);
            Assert.IsTrue(_group.interactable);
            Assert.IsTrue(_group.blocksRaycasts);
            int calls = 0, second = 0;
            _popup.Hide(() => calls++);
            _popup.Hide(() => second++);
            Assert.IsFalse(_group.interactable);
            Assert.IsFalse(_group.blocksRaycasts);
            Assert.AreEqual(0, calls);
            yield return Until(() => !_go.activeSelf);
            Assert.AreEqual((1, 1), (calls, second));
            yield return null;
            Assert.AreEqual((1, 1), (calls, second));
            _popup.Hide(() => calls++);
            Assert.AreEqual(2, calls, "Already hidden callbacks run immediately.");
            Assert.IsFalse(_go.activeSelf);
        }

        [UnityTest]
        public IEnumerator Spam_EndsInTheLastRequestedState()
        {
            // A long close leaves time to observe and reverse an actual in-flight transition.
            typeof(PopupTransition).GetField("_hideSeconds", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(_popup, 1f);
            _popup.Show();
            yield return Until(() => _popup.Progress == 1f);
            int hiddenCalls = 0;
            _popup.Hide(() => hiddenCalls++);
            yield return Until(() => _popup.Progress < 1f);
            float progress = _popup.Progress;
            Assert.Greater(progress, 0f);
            _popup.Show();
            Assert.AreEqual(progress, _popup.Progress, "Reopening must continue from the current progress.");
            _popup.Hide(() => hiddenCalls++);
            _popup.Show();
            yield return Until(() => _popup.Progress == 1f);
            Assert.IsTrue(_go.activeSelf);
            Assert.IsTrue(_group.interactable);
            Assert.IsTrue(_group.blocksRaycasts);
            Assert.AreEqual(0, hiddenCalls);
        }

    }
}
