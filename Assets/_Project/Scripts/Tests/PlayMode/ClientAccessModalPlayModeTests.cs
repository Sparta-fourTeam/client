using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core;
using Game.View;
using NUnit.Framework;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Game.Tests
{
    public sealed class ClientAccessModalPlayModeTests
    {
        private GameObject _instance;
        [UnityTearDown]
        public IEnumerator TearDown() { if (_instance != null) { UnityEngine.Object.Destroy(_instance); } yield return null; }

#if UNITY_EDITOR
        [UnityTest]
        public IEnumerator PendingLookupBlocksAllButtonsAndSupersededReplyAcrossRealFrames()
        {
            _instance = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/Prefabs/UI/Loading/LoadingConfirmPopup.prefab"));
            var popup = _instance.GetComponent<ConfirmPopupView>(); popup.Initialize();
            var first = new UniTaskCompletionSource<ClientAccessPolicy>();
            var second = new UniTaskCompletionSource<ClientAccessPolicy>();
            var client = new Client { Pending = first };
            var guard = new ClientAccessGuard(client, new ClientAccessPrompt(), new NoAdvisory());
            var task = guard.EnsureAllowed(ClientAccessPurpose.Login).AsTask();
            yield return Until(() => popup.IsOpen);
            var close = (Button)new SerializedObject(popup).FindProperty("_closeButton").objectReferenceValue;
            var confirm = (Button)new SerializedObject(popup).FindProperty("_confirmButton").objectReferenceValue;
            Assert.IsFalse(close.gameObject.activeSelf); Assert.IsFalse(confirm.interactable);
            close.onClick.Invoke(); confirm.onClick.Invoke();
            yield return null;
            Assert.IsTrue(popup.IsOpen); Assert.IsFalse(task.IsCompleted);
            client.Pending = second; guard.Recheck();
            var allowed = new Client { Normal = true }.Fetch(default).GetAwaiter().GetResult();
            first.TrySetResult(allowed);
            yield return null;
            Assert.IsTrue(popup.IsOpen); Assert.IsFalse(task.IsCompleted); Assert.IsFalse(confirm.interactable);
            second.TrySetResult(allowed);
            yield return Until(() => task.IsCompleted);
            Assert.IsFalse(task.IsFaulted); Assert.AreEqual(2, client.Reads);
        }

        [UnityTest]
        public IEnumerator MandatoryModalStaysThroughFramesAndForegroundRecheckReleasesOnlyOnNormalPolicy()
        {
            _instance = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/Prefabs/UI/Loading/LoadingConfirmPopup.prefab"));
            var popup = _instance.GetComponent<ConfirmPopupView>();
            popup.Initialize();
            var client = new Client();
            var guard = new ClientAccessGuard(client, new ClientAccessPrompt(), new NoAdvisory());
            var task = guard.EnsureAllowed(ClientAccessPurpose.Login).AsTask();
            yield return Until(() => popup.IsOpen);
            foreach (var button in _instance.GetComponentsInChildren<Button>(true))
            { if (button.name != "ConfirmButton") { button.onClick.Invoke(); } }
            yield return null;
            Assert.IsFalse(task.IsCompleted);
            Assert.IsTrue(popup.IsOpen);
            client.Normal = true;
            guard.Recheck();
            yield return Until(() => task.IsCompleted);
            Assert.IsFalse(task.IsFaulted);
            Assert.AreEqual(2, client.Reads);
        }

        [UnityTest]
        public IEnumerator DestroyedScenePopupCancelsGuardInsteadOfLeavingAStuckGate()
        {
            _instance = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/Prefabs/UI/Loading/LoadingConfirmPopup.prefab"));
            var popup = _instance.GetComponent<ConfirmPopupView>(); popup.Initialize();
            var guard = new ClientAccessGuard(new Client(), new ClientAccessPrompt(), new NoAdvisory());
            var task = guard.EnsureAllowed(ClientAccessPurpose.Login).AsTask();
            yield return Until(() => popup.IsOpen);
            UnityEngine.Object.Destroy(_instance);
            yield return Until(() => task.IsCompleted);
            Assert.IsTrue(task.IsCanceled || task.IsFaulted);
            Assert.IsFalse(guard.IsChecking);
            if (task.IsFaulted) { Assert.IsNotNull(task.Exception); }
        }

        [UnityTest]
        public IEnumerator SceneLifetimeCancellationClosesOnlyOwnedModalAndNeverGrantsEntry()
        {
            _instance = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/Prefabs/UI/Loading/LoadingConfirmPopup.prefab"));
            var popup = _instance.GetComponent<ConfirmPopupView>(); popup.Initialize();
            using var cancellation = new CancellationTokenSource();
            var guard = new ClientAccessGuard(new Client(), new ClientAccessPrompt(), new NoAdvisory());
            var task = guard.EnsureAllowed(ClientAccessPurpose.Login, cancellation.Token).AsTask();
            yield return Until(() => popup.IsOpen);
            cancellation.Cancel();
            yield return Until(() => task.IsCompleted);
            Assert.IsTrue(task.IsCanceled || task.IsFaulted);
            Assert.IsFalse(guard.IsChecking);
            // Observe the exception without leaving an unobserved Task error behind.
            if (task.IsFaulted) { Assert.IsNotNull(task.Exception); }
        }
#endif

        private static IEnumerator Until(Func<bool> condition)
        {
            float deadline = Time.realtimeSinceStartup + 4f;
            while (!condition() && Time.realtimeSinceStartup < deadline) { yield return null; }
            Assert.IsTrue(condition(), "Policy flow did not reach its completion condition.");
        }

        private sealed class Client : IClientPolicyClient
        {
            public bool Normal; public int Reads;
            public UniTaskCompletionSource<ClientAccessPolicy> Pending;
            public ClientBuild Build => new ClientBuild("android", "10");
            public UniTask<ClientAccessPolicy> Fetch(CancellationToken cancellationToken)
            {
                Reads++;
                if (Pending != null) { return Pending.Task; }
                return UniTask.FromResult(new ClientAccessPolicy
                {
                    schemaVersion = 1,
                    revision = "test",
                    enforcementEnabled = true,
                    mode = Normal ? "NORMAL" : "MAINTENANCE",
                    message = "test",
                    retryAfterSeconds = 30,
                    platforms = new Dictionary<string, ClientPlatformPolicy>
                    { ["android"] = new ClientPlatformPolicy { minimumBuild = "0", recommendedBuild = "0", storeUrl = "https://play.google.com/store/apps/details?id=test" } },
                });
            }
        }

        private sealed class NoAdvisory : IAppUpdateChecker
        {
            public UniTask<AppUpdateInfo> CheckAsync(CancellationToken cancellationToken) => UniTask.FromResult<AppUpdateInfo>(null);
        }
    }
}
