using Cysharp.Threading.Tasks;
using MessagePipe;

namespace Game.Core
{
    /// <summary>Lobby에서 전투를 먼저 발급받고, 성공했을 때만 Stage로 전환한다.
    /// 거절되면 씬 전환 없이 StartFailed만 발행한다 — 이전엔 반대로 Stage로 넘어갔다가 거절돼서 되돌아왔다</summary>
    public sealed class BattleLauncher
    {
        private readonly IBattleApi _battleApi;
        private readonly StageContext _stageContext;
        private readonly ISceneNavigator _nav;
        private readonly IPublisher<StartFailed> _startFailed;

        public BattleLauncher(IBattleApi battleApi, StageContext stageContext, ISceneNavigator nav, IPublisher<StartFailed> startFailed)
        {
            _battleApi = battleApi;
            _stageContext = stageContext;
            _nav = nav;
            _startFailed = startFailed;
        }

        public async UniTask Launch(int stageId)
        {
            StartBattleResponse response;
            try
            {
                // TODO(server): dataRevision 하드코딩(client#25) — 예: await _dataApi.GetVersions() 결과로 교체
                response = await _battleApi.StartBattle(stageId, dataRevision: 1);
            }
            catch (ApiException e) when (e.Kind == ApiErrorKind.Rejected)
            {
                _startFailed.Publish(new StartFailed(e.Code));
                return;
            }

            _stageContext.SetBattle(response.battleId, response.seed);
            await _nav.GoToStage(stageId);
        }
    }
}
