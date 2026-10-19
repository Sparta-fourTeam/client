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
        private readonly IBattleCheckpointStore _checkpoints;
        private readonly StageContext _stageContext;
        private readonly ISceneNavigator _nav;
        private readonly IPublisher<StartFailed> _startFailed;

        public BattleLauncher(IBattleApi battleApi, StageContext stageContext, ISceneNavigator nav, IPublisher<StartFailed> startFailed, IBattleCheckpointStore checkpoints = null)
        {
            _checkpoints = checkpoints;
            _battleApi = battleApi;
            _stageContext = stageContext;
            _nav = nav;
            _startFailed = startFailed;
        }

        /// <summary>저장된 마지막 누적 진행을 접수한 뒤 이어하기를 포기한다. 재시도할 요청은 먼저 로컬에 확정한다.</summary>
        public async UniTask Discard(BattleCheckpoint checkpoint)
        {
            if (_battleApi is not IBattleProgressApi api) { return; }
            if (_battleApi is IBattleCheckpointProgressApi pinned) { api = pinned.ForCheckpoint(checkpoint); }
            var state = checkpoint.Stage;
            if (state.PendingProgress != null && state.PendingProgressAbandon)
            {
                await api.AbandonProgress(state.PendingProgress);
                return;
            }
            if (state.PendingProgress != null)
            {
                var prior = await api.ReportProgress(state.PendingProgress);
                state.ProgressRevision = prior.revision;
                state.ProgressSourceRevision = state.PendingProgress.sourceRevision;
                state.PendingProgress = null;
                SaveProgress(checkpoint);
            }
            state.PendingProgress = new BattleProgressRequest
            {
                battleId = checkpoint.BattleId,
                reportId = System.Guid.NewGuid().ToString("D"),
                sourceRevision = state.ProgressSourceRevision ?? api.SourceRevision,
                expectedRevision = state.ProgressRevision,
                killedMonsters = state.Kills,
                completedWaves = state.CompletedWaves,
                reachedWave = state.ReachedWave,
                encounteredMonsterIds = new System.Collections.Generic.List<int>(state.Encountered),
            };
            state.PendingProgressAbandon = true;
            SaveProgress(checkpoint);
            await api.AbandonProgress(state.PendingProgress);
        }

        private void SaveProgress(BattleCheckpoint checkpoint)
        {
            if (_checkpoints == null) { throw new ApiException(ApiErrorKind.Transient, "PROGRESS_STORAGE_UNAVAILABLE"); }
            try { _checkpoints.Save(checkpoint); }
            catch (System.Exception error) when (error is System.IO.IOException or System.InvalidOperationException or System.UnauthorizedAccessException)
            { throw new ApiException(ApiErrorKind.Transient, "PROGRESS_STORAGE_UNAVAILABLE"); }
        }

        /// <summary>기기에 저장된 같은 전투를 재개한다. 새 전투 발급이나 에너지 차감은 하지 않는다.</summary>
        public UniTask Resume(BattleCheckpoint checkpoint)
        {
            if (checkpoint == null) { throw new System.ArgumentNullException(nameof(checkpoint)); }
            _stageContext.SetBattle(checkpoint.BattleId);
            _stageContext.Checkpoint = checkpoint;
            return _nav.GoToStage(checkpoint.StageId);
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

            _checkpoints?.Clear();
            if (_battleApi is IBattleLaunchCheckpointSession session)
            { session.OnBattleLaunched(response.battleId, stageId); }
            _stageContext.SetBattle(response.battleId);
            await _nav.GoToStage(stageId);
            return true;
        }
    }
}
