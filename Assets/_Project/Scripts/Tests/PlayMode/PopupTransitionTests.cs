using System;
using System.Collections;
using System.Reflection;
using Game.View;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

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
