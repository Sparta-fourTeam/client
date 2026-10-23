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
            Assert.IsFalse(_view.ShowAsync(new ConfirmRequest { Title = "other" }).GetAwaiter().GetResult());
            Assert.IsFalse(pending.IsCompleted);
            _view.Cancel(request); Assert.IsTrue(pending.IsCompleted);
        }

        [Test]
        public void CancellingAnOldRequestCannotDismissNewerModal()
        {
            var old = new ConfirmRequest { Title = "old" }; _view.ShowAsync(old).Forget();
            var next = new ConfirmRequest { Title = "new", CannotDismiss = true };
            var pending = _view.ShowAsync(next).AsTask();
            _view.Cancel(old); Assert.IsFalse(pending.IsCompleted);
            _view.Cancel(next); Assert.IsTrue(pending.IsCompleted);
        }
    }
}
