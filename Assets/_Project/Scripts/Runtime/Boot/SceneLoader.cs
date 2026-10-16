using Cysharp.Threading.Tasks;
using Game.Core;
using Game.View;
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
        private readonly IAudioService _audio;
        private bool _busy;

        public SceneId Current { get; private set; } = SceneId.Boot;

        public SceneLoader(ITransitionCurtain curtain, StageContext stageContext, LifetimeScope root, IAudioService audio = null)
        {
            _curtain = curtain;
            _stageContext = stageContext;
            _root = root;
            _audio = audio;
        }

        public UniTask GoToLoading() => Load(SceneId.Loading);

        public UniTask GoToLobby() => Load(SceneId.Lobby);

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
                // Stopping Play Mode can destroy the root while the curtain is still fading.
                if (_root == null)
                {
                    return;
                }

                try
                {
                    // 전투 음악은 Stage의 상태 메시지가 시작한다. 기존 음악은 로드 전에 줄인다.
                    if (next == SceneId.Stage) { _audio?.StopBgm(0.35f); }
                    using (LifetimeScope.EnqueueParent(_root))
                    {
                        await SceneManager.LoadSceneAsync(next.ToString(), LoadSceneMode.Single).ToUniTask();
                    }

                    foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
                    {
                        foreach (var host in root.GetComponentsInChildren<UiHost>(true))
                        {
                            await host.WhenReady;
                        }
                    }

                    Current = next;
                    if (next == SceneId.Loading) { _audio?.PlayBgm(SoundIds.IntroBgm); }
                    else if (next == SceneId.Lobby) { _audio?.PlayBgm(SoundIds.LobbyBgm); }
                }
                catch
                {
                    await _curtain.Open();
                    throw;
                }

                await _curtain.Open();
            }
            finally
            {
                _busy = false;
            }
        }
    }
}
