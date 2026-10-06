using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game.Core;

namespace Game.Network
{
    /// <summary>전투 발급과 결과 제출을 로컬 저장소에 반영하는 Mock 구현</summary>
    public sealed class LocalBattleApi : IBattleApi, ISubmitFaultSwitch
    {
        private readonly LocalSaveStore _store;
        private readonly GameDataStore _data;
        /// <summary>해금 담당 코드가 최신 후보 목록 조회를 연결한다. 미연결 상태에서 랜덤 재료는 지급하지 않는다.</summary>
        public Func<IEnumerable<int>> UnlockedSkillIds { get; set; }
        public Func<int, int, int[]> MaterialDistribution { get; set; }

        public int FailNextSubmits { get; set; }

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
            if (_data.StageRewards.GetOrThrow(stageId).SkillMaterial == ItemIds.RandomSkillMaterial && UnlockedSkillIds == null)
            { throw new ApiException(ApiErrorKind.Rejected, "SKILL_UNLOCK_SOURCE_NOT_READY"); }

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
                rewardGold = 0,
                rewardRuleVersion = 1
            };
            save.battles.Add(row);
            _store.Flush(save);

            return UniTask.FromResult(new StartBattleResponse { battleId = row.battleKey, seed = row.seed });
        }

        public UniTask<SubmitResultResponse> SubmitResult(SubmitResultRequest req)
        {
            // 개발용 스위치: 저장소를 건드리기 전에 실패시켜서, 재시도로 같은 요청이 다시 오는 상황을 만든다
            if (FailNextSubmits > 0)
            {
                FailNextSubmits--;
                throw new ApiException(ApiErrorKind.Network, "NETWORK");
            }

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
                    rewardGold = battle.rewardGold,
                    rewardExp = battle.rewardExp,
                    clearRating = battle.clearRating,
                    rewardItems = battle.rewardItems ?? new()
                });
            }

            var stage = _data.Stages.GetOrThrow(battle.stageId);

            int rating = req.cleared ? StageRewardRules.ClearRating(req.wallHpPercent) : 0;
            int reward, rewardExp = 0;
            IEnumerable<ItemAmount> rewards;
            if (battle.rewardRuleVersion == 0)
            {
                // 업데이트 전에 발급한 전투만 기존 지급 규칙으로 마무리한다.
                reward = req.cleared ? stage.ClearGold : 0;
                rewards = req.cleared ? stage.ClearItems : null;
            }
            else
            {
                if (req.completedWaves < 0 || req.completedWaves > StageRewardRules.WaveCount
                    || req.reachedWave < req.completedWaves || req.reachedWave > StageRewardRules.WaveCount
                    || (req.cleared && req.completedWaves != StageRewardRules.WaveCount))
                { throw new ApiException(ApiErrorKind.Rejected, "INVALID_COMPLETED_WAVES"); }
                var calculated = StageRewardRules.Calculate(_data.StageRewards.GetOrThrow(battle.stageId),
                    req.completedWaves, req.cleared, rating);
                reward = calculated.Coin;
                rewardExp = calculated.Exp;
                rewards = SkillMaterialResolver.Resolve(calculated.Items, UnlockedSkillIds?.Invoke(), _data,
                    battle.seed, MaterialDistribution);
            }
            int totalGold, totalExp;
            try { totalGold = checked(save.wallet.gold + reward); totalExp = checked(save.exp + rewardExp); }
            catch (OverflowException) { throw new ApiException(ApiErrorKind.Rejected, "REWARD_QUANTITY_OVERFLOW"); }
            var itemRewards = LocalItemRewards.Apply(save, rewards, _data);
            battle.status = req.cleared ? "Cleared" : "Failed";
            battle.rewardGold = reward;
            battle.rewardExp = rewardExp;
            battle.clearRating = rating;
            battle.rewardItems = itemRewards;
            save.wallet.gold = totalGold;
            save.exp = totalExp;

            if (req.cleared)
            {
                ApplyClearRating(save, battle.stageId, req.wallHpPercent, _data.Stages);
            }

            _store.Flush(save);
            return UniTask.FromResult(new SubmitResultResponse
            { cleared = req.cleared, rewardGold = reward, rewardExp = rewardExp, clearRating = rating, rewardItems = itemRewards });
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
