using Cysharp.Threading.Tasks;
using Game.View;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Tests
{
    public sealed class ClientAccessPopupTests
    {
        private GameObject _instance;
        private ConfirmPopupView _view;
        [SetUp]
        public void SetUp()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/Loading/LoadingConfirmPopup.prefab");
            _instance = Object.Instantiate(prefab);
            _view = _instance.GetComponentInChildren<ConfirmPopupView>(true);
            _view.Initialize();
        }
        [TearDown] public void TearDown() => Object.DestroyImmediate(_instance);

        [Test]
        public void BlockingAndCheckingHideXButOrdinaryConfirmationRestoresCloseBehaviour()
        {
            var close = (Button)new SerializedObject(_view).FindProperty("_closeButton").objectReferenceValue;
            var confirm = System.Array.Find(_instance.GetComponentsInChildren<Button>(true), b => b.name == "ConfirmButton");
            Assert.IsNotNull(close); Assert.IsNotNull(confirm);
            var request = new ConfirmRequest { Title = "checking", CannotDismiss = true, ConfirmEnabled = false };
            var pending = _view.ShowAsync(request).AsTask();
            Assert.IsFalse(close.gameObject.activeSelf); Assert.IsFalse(confirm.interactable);
            close.onClick.Invoke(); confirm.onClick.Invoke();
            Assert.IsFalse(pending.IsCompleted);
            _view.Cancel(request);
            var ordinary = _view.ShowAsync(new ConfirmRequest { Title = "ordinary" }).AsTask();
            Assert.IsTrue(close.gameObject.activeSelf); Assert.IsTrue(confirm.interactable);
            close.onClick.Invoke(); Assert.IsTrue(ordinary.IsCompleted); Assert.IsFalse(ordinary.Result);
        }

        [Test]
        public void MandatoryModalCannotBeSkippedByCancelAndDoesNotYieldToAnotherDialog()
        {
            var request = new ConfirmRequest { Title = "required", Message = "test", CannotDismiss = true };
            var pending = _view.ShowAsync(request).AsTask();
            foreach (var button in _instance.GetComponentsInChildren<Button>(true))
            { if (button.name != "ConfirmButton") { button.onClick.Invoke(); } }
            Assert.IsFalse(pending.IsCompleted);
            var other = _view.ShowAsync(new ConfirmRequest { Title = "other" }).AsTask();
            Assert.IsFalse(other.IsCompleted);
            Assert.IsFalse(pending.IsCompleted);
            _view.Cancel(request); Assert.IsTrue(pending.IsCompleted);
            Assert.IsFalse(other.IsCompleted);
            var close = (Button)new SerializedObject(_view).FindProperty("_closeButton").objectReferenceValue;
            close.onClick.Invoke(); Assert.IsFalse(other.Result);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void PolicyLookupPreservesResumeChoiceUntilTheUserActuallyAnswers(bool resume)
        {
            var original = _view.ShowAsync(new ConfirmRequest
            { Title = "이어하기", ConfirmLabel = "이어하기", CancelLabel = "포기" }).AsTask();
            var checking = new ConfirmRequest { Title = "checking", CannotDismiss = true, ConfirmEnabled = false };
            _view.ShowAsync(checking).Forget();
            Assert.IsFalse(original.IsCompleted, "A policy lookup must not synthesize a forfeit answer.");
            _view.Cancel(checking);
            Assert.IsFalse(original.IsCompleted);
            var field = resume ? "_confirmButton" : "_cancelButton";
            ((Button)new SerializedObject(_view).FindProperty(field).objectReferenceValue).onClick.Invoke();
            Assert.IsTrue(original.IsCompleted); Assert.AreEqual(resume, original.Result);
        }

        [Test]
        public void PolicyLookupPreservesTypedConfirmationInput()
        {
            var original = _view.ShowAsync(new ConfirmRequest { Title = "ordinary", RequiredInput = "keep" }).AsTask();
            var input = (TMPro.TMP_InputField)new SerializedObject(_view).FindProperty("_input").objectReferenceValue;
            input.text = "keep";
            var checking = new ConfirmRequest { CannotDismiss = true, ConfirmEnabled = false };
            _view.ShowAsync(checking).Forget(); _view.Cancel(checking);
            Assert.IsFalse(original.IsCompleted); Assert.AreEqual("keep", input.text);
        }

        [Test]
        public void CancellingAnOldRequestCannotDismissNewerModal()
        {
            var old = new ConfirmRequest { Title = "old" }; var original = _view.ShowAsync(old).AsTask();
            var next = new ConfirmRequest { Title = "new", CannotDismiss = true };
            var pending = _view.ShowAsync(next).AsTask();
            _view.Cancel(old); Assert.IsFalse(pending.IsCompleted);
            Assert.IsTrue(original.IsFaulted);
            Assert.That(original.Exception.InnerException, Is.InstanceOf<System.OperationCanceledException>());
            _view.Cancel(next); Assert.IsTrue(pending.IsCompleted);
        }
    }
}
