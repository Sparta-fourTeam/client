using System;
using Cysharp.Threading.Tasks;
using Game.Core;

namespace Game.Network
{
    /// <summary>전투 발급과 결과 제출을 로컬 저장소에 반영하는 Mock 구현</summary>
    public sealed class LocalBattleApi : IBattleApi
    {
        private readonly LocalSaveStore _store;
        private readonly GameDataStore _data;

        public LocalBattleApi(LocalSaveStore store, GameDataStore data)
        {
            _store = store;
            _data = data;
        }

        // TODO(server): dataRevision 미사용 — 서버 연동 시 최신 테이블 버전과 다르면 DATA_OUTDATED로 거절
        public UniTask<StartBattleResponse> StartBattle(int stageId, int dataRevision)
        {
            var save = _store.Load();
            _data.Stages.GetOrThrow(stageId);

            if (!save.stageProgress.Exists(p => p.stageId == stageId))
            {
                throw new ApiException(ApiErrorKind.Rejected, "STAGE_LOCKED");
            }

            var (current, _) = EnergyRule.At(save.wallet.energyStored,
                EnergyRule.ParseUpdatedAt(save.wallet.energyUpdatedAt), DateTime.UtcNow, _data.Energy);
            if (current < _data.Energy.Cost)
            {
                throw new ApiException(ApiErrorKind.Rejected, "INSUFFICIENT_ENERGY");
            }

            save.wallet.energyStored = current - _data.Energy.Cost;
            save.wallet.energyUpdatedAt = DateTime.UtcNow.ToString("O");

            var row = new BattleRow
            {
                battleKey = Guid.NewGuid().ToString(),
                stageId = stageId,
                seed = new Random().Next(),
                status = "Issued",
                rewardGold = 0
            };
            save.battles.Add(row);
            _store.Flush(save);

            return UniTask.FromResult(new StartBattleResponse { battleId = row.battleKey, seed = row.seed });
        }

        public UniTask<SubmitResultResponse> SubmitResult(SubmitResultRequest req)
        {
            var save = _store.Load();
            var battle = save.battles.Find(b => b.battleKey == req.battleId);
            if (battle == null)
            {
                throw new ApiException(ApiErrorKind.Rejected, "INVALID_ID");
            }

            if (battle.status != "Issued")
            {
                // 중복 제출 — 이전에 확정된 결과를 그대로 재반환한다
                return UniTask.FromResult(new SubmitResultResponse
                {
                    cleared = battle.status == "Cleared",
                    rewardGold = battle.rewardGold
                });
            }

            var stage = _data.Stages.GetOrThrow(battle.stageId);

            // 참가(실패) 보상은 테이블에 없다 — 클리어했을 때만 ClearGold를 지급한다
            int reward = req.cleared ? stage.ClearGold : 0;
            battle.status = req.cleared ? "Cleared" : "Failed";
            battle.rewardGold = reward;
            save.wallet.gold += reward;

            if (req.cleared)
            {
                ApplyClearRating(save, battle.stageId, req.wallHpPercent, _data.Stages);
            }

            _store.Flush(save);
            return UniTask.FromResult(new SubmitResultResponse { cleared = req.cleared, rewardGold = reward });
        }

        private static void ApplyClearRating(LocalSave save, int stageId, int wallHpPercent, Table<int, StageDefinition> stages)
        {
            int rating = wallHpPercent >= 100 ? 3 : wallHpPercent >= 50 ? 2 : 1;

            var progress = save.stageProgress.Find(p => p.stageId == stageId);
            if (progress != null && rating > progress.clearRating)
            {
                progress.clearRating = rating;
            }

            if (stages.Contains(stageId + 1) && !save.stageProgress.Exists(p => p.stageId == stageId + 1))
            {
                save.stageProgress.Add(new StageProgressRow { stageId = stageId + 1, clearRating = 0 });
            }
        }
    }
}
