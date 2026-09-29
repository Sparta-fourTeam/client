using Cysharp.Threading.Tasks;
using Game.Core;
using UnityEngine;
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
            RunAsync().Forget(Debug.LogException);
        }

        private async UniTask RunAsync()
        {
            await _nav.GoToLoading();
        }
    }
}
