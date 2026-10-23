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

namespace Game.Tests
{
    public sealed class ClientAccessModalPlayModeTests
    {
        private GameObject _instance;
        [UnityTearDown]
        public IEnumerator TearDown() { if (_instance != null) { UnityEngine.Object.Destroy(_instance); } yield return null; }

#if UNITY_EDITOR
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
#endif

        private static IEnumerator Until(Func<bool> condition)
        {
            float deadline = Time.realtimeSinceStartup + 4f;
            while (!condition() && Time.realtimeSinceStartup < deadline) { yield return null; }
            Assert.IsTrue(condition(), "Policy flow did not reach its completion condition.");
        }

        private sealed class Client : IClientPolicyClient
        {
            public ClientBuild Build => new ClientBuild("android", "10");
            public UniTask<ClientAccessPolicy> Fetch(CancellationToken cancellationToken)
            {
                return UniTask.FromResult(new ClientAccessPolicy
                {
                    schemaVersion = 1,
                    revision = "test",
                    enforcementEnabled = true,
                    mode = "MAINTENANCE",
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
