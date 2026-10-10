using System.Collections;
using Cysharp.Threading.Tasks;
using Game.Boot;
using Game.Core;
using Game.View;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VContainer.Unity;

namespace Game.Tests
{
    public sealed class SceneTransitionLifecycleTests
    {
        [UnityTest]
        public IEnumerator DestroyingDuringTransitionCompletesWithoutLoadingOrTouchingDestroyedObjects()
        {
            var originalScene = SceneManager.GetActiveScene();
            var root = new GameObject("TransitionRootTest");
            root.SetActive(false);
            var scope = root.AddComponent<LifetimeScope>();
            var curtainRoot = new GameObject("TransitionCurtainTest", typeof(CanvasGroup));
            try
            {
                var curtain = curtainRoot.AddComponent<TransitionCurtain>();
                var navigator = new SceneLoader(curtain, new StageContext(), scope);
                var pending = navigator.GoToLobby();
                Assert.That(pending.Status, Is.EqualTo(UniTaskStatus.Pending));
                Assert.That(curtainRoot.GetComponent<CanvasGroup>().blocksRaycasts, Is.True);
                Object.Destroy(root);
                Object.Destroy(curtainRoot);
                yield return null;
                yield return Complete(pending);
                Assert.That(root == null && curtainRoot == null, Is.True);
                Assert.That(navigator.Current, Is.EqualTo(SceneId.Boot));
                Assert.That(SceneManager.GetActiveScene().handle, Is.EqualTo(originalScene.handle), "파괴된 루트로 다음 씬을 로드하지 않는다");
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                if (root != null) { Object.Destroy(root); }
                if (curtainRoot != null) { Object.Destroy(curtainRoot); }
            }

            // Opening also resumes after a yield and must tolerate destruction.
            curtainRoot = new GameObject("OpeningCurtainTest", typeof(CanvasGroup));
            try
            {
                var curtain = curtainRoot.AddComponent<TransitionCurtain>();
                var pending = curtain.Open();
                Assert.That(pending.Status, Is.EqualTo(UniTaskStatus.Pending));
                Object.Destroy(curtainRoot);
                yield return null;
                yield return Complete(pending);
                Assert.That(curtainRoot == null, Is.True);
                LogAssert.NoUnexpectedReceived();
            }
            finally { if (curtainRoot != null) { Object.Destroy(curtainRoot); } }
            yield return null;
        }

        private static IEnumerator Complete(UniTask task)
        {
            float deadline = Time.realtimeSinceStartup + 3f;
            while (task.Status == UniTaskStatus.Pending)
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), "전환 작업이 객체 파괴 후 종료되지 않았다");
                yield return null;
            }
            task.GetAwaiter().GetResult();
        }
    }
}
