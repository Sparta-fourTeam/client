using Cysharp.Threading.Tasks;
using Game.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer.Unity;

namespace Game.Boot
{
    /// <summary>앱 시작 시 1회 실행: DI 초기화만 마치고 Loading 씬으로 넘어간다</summary>
    public sealed class BootFlow : IStartable
    {
        private readonly ISceneNavigator _nav;

        public BootFlow(ISceneNavigator nav)
        {
            _nav = nav;
        }

        public void Start()
        {
            // RootLifetimeScope는 autoRun으로 항상 생성되므로, Boot 씬이 아닌 씬(Stage 단독 실행,
            // 샌드박스 씬 등)에서 Play했을 때는 로비로 강제 이동시키지 않는다.
            if (SceneManager.GetActiveScene().name != nameof(SceneId.Boot))
            {
                return;
            }

            RunAsync().Forget(Debug.LogException);
        }

        private async UniTask RunAsync()
        {
            await _nav.GoToLoading();
        }
    }
}
