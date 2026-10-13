using Cysharp.Threading.Tasks;
using Game.Core.Messages;
using MessagePipe;

namespace Game.Core
{
    /// <summary>전투를 먼저 발급받고, 성공했을 때만 Stage로 전환한다.
    /// 로비 외에 튜토리얼에서도 쓴다(로딩·로비 → 튜토리얼 전투).
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

        /// <summary>발급에 성공해 Stage로 넘어갔으면 true, 거절돼 그대로 남았으면 false</summary>
        public async UniTask<bool> Launch(int stageId)
        {
            StartBattleResponse response;
            try
            {
                response = await _battleApi.StartBattle(stageId);
            }
            catch (ApiException e) when (e.Kind == ApiErrorKind.Rejected)
            {
                _startFailed.Publish(new StartFailed(e.Code));
                return false;
            }

            _stageContext.SetBattle(response.battleId);
            await _nav.GoToStage(stageId);
            return true;
        }
    }
}
