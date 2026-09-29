using Cysharp.Threading.Tasks;
using Game.Core;
using UnityEngine.SceneManagement;
using VContainer.Unity;

namespace Game.Boot
{
    /// <summary>커튼을 닫고 씬을 로드한 뒤 다시 여는 씬 전환 구현체. 전환 중 호출은 현재 전환이 끝날 때까지 대기했다가 실행된다</summary>
    public sealed class SceneLoader : ISceneNavigator
    {
        private readonly ITransitionCurtain _curtain;
        private readonly StageContext _stageContext;
        private readonly LifetimeScope _root;
        private bool _busy;

        public SceneId Current { get; private set; } = SceneId.Boot;

        public SceneLoader(ITransitionCurtain curtain, StageContext stageContext, LifetimeScope root)
        {
            _curtain = curtain;
            _stageContext = stageContext;
            _root = root;
        }

        public UniTask GoToLoading() => Load(SceneId.Loading);

        public UniTask GoToLobby() => Load(SceneId.Lobby);

        public UniTask GoToBoot() => Load(SceneId.Boot);

        public UniTask GoToStage(int stageId)
        {
            _stageContext.Set(stageId);
            return Load(SceneId.Stage);
        }

        public UniTask RestartStage() => Load(SceneId.Stage);

        private async UniTask Load(SceneId next)
        {
            // 방금 로드된 씬 자신의 Awake 단계(예: 진입 즉시 다음 씬으로 넘어가는 엔트리 포인트)에서
            // 걸어온 호출은, 그 씬을 로드한 바깥쪽 Load()가 아직 커튼을 여는 중이라 _busy가 true일 수 있다.
            // 무시하지 않고 그 전환이 끝날 때까지 대기했다가 이어서 실행한다.
            while (_busy)
            {
                await UniTask.Yield();
            }

            _busy = true;
            try
            {
                await _curtain.Close();
                using (LifetimeScope.EnqueueParent(_root))
                {
                    await SceneManager.LoadSceneAsync(next.ToString(), LoadSceneMode.Single).ToUniTask();
                }

                Current = next;
                await _curtain.Open();
            }
            finally
            {
                _busy = false;
            }
        }
    }
}
