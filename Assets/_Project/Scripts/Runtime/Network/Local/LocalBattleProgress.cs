using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Game.Core;
using Newtonsoft.Json;

namespace Game.Network
{
    public sealed partial class LocalBattleApi
    {
        public async UniTask<BattleProgressAck> AbandonProgress(BattleProgressRequest request)
        {
            var ack = await ReportProgress(request);
            var save = _store.Load();
            var battle = save.battles.Find(b => b.battleKey == request.battleId);
            if (battle.status == "Issued") { battle.status = "Abandoned"; _store.Flush(save); }
            return ack;
        }

        /// <summary>최초 접수한 누적 차이만 저장한다. 최종 결과는 아직 지급하지 않는다.</summary>
        public UniTask<BattleProgressAck> ReportProgress(BattleProgressRequest request)
        {
            var save = _store.Load();
            var battle = save.battles.Find(b => b.battleKey == request?.battleId)
                ?? throw new ApiException(ApiErrorKind.Rejected, "INVALID_ID");
            if (request.encounteredMonsterIds == null || request.expectedRevision < 0 || !Guid.TryParse(request.reportId, out _))
            { throw new ApiException(ApiErrorKind.Rejected, "INVALID_PROGRESS"); }
            request = new BattleProgressRequest
            {
                battleId = request.battleId,
                reportId = request.reportId,
                expectedRevision = request.expectedRevision,
                killedMonsters = request.killedMonsters,
                completedWaves = request.completedWaves,
                reachedWave = request.reachedWave,
                encounteredMonsterIds = request.encounteredMonsterIds.Distinct().OrderBy(id => id).ToList(),
            };
            var prior = battle.progressReceipts.Find(p => p.request.reportId == request.reportId);
            if (prior != null)
            {
                if (JsonConvert.SerializeObject(prior.request) != JsonConvert.SerializeObject(request))
                { throw new ApiException(ApiErrorKind.Rejected, "BATTLE_PROGRESS_CONFLICT"); }
                return UniTask.FromResult(prior.ack);
            }
            var stage = _data.Stages.GetOrThrow(battle.stageId);
            if (battle.status != "Issued" || request.expectedRevision != battle.progressRevision
                || request.killedMonsters < battle.reportedKills || request.completedWaves < battle.reportedCompletedWaves
                || request.reachedWave < battle.reportedReachedWave || request.completedWaves > request.reachedWave
                || request.reachedWave > stage.Waves.Count || request.reachedWave > request.completedWaves + 1
                || !battle.reportedEncounters.All(request.encounteredMonsterIds.Contains))
            { throw new ApiException(ApiErrorKind.Rejected, "BATTLE_PROGRESS_CONFLICT"); }
            var allowed = new HashSet<int>();
            void Add(int id)
            {
                if (!allowed.Add(id)) { return; }
                foreach (var passive in _data.Monsters.GetOrThrow(id).Passives.Where(p => p.Kind == PassiveKind.Spawn))
                { Add(passive.MonsterId); }
            }
            foreach (var wave in stage.Waves.Take(request.reachedWave))
            { foreach (var spawn in wave.Spawns) { Add(spawn.MonsterId); } }
            if (request.killedMonsters < 0 || !request.encounteredMonsterIds.All(allowed.Contains)
                || (request.killedMonsters > 0 && request.encounteredMonsterIds.Count == 0))
            { throw new ApiException(ApiErrorKind.Rejected, "INVALID_PROGRESS"); }
            var now = DateTime.UtcNow;
            LocalQuests.Roll(save, _data, now);
            int delta = request.killedMonsters - battle.reportedKills;
            LocalQuests.Add(save, _data, QuestKind.KillMonsters, delta);
            RegisterEncounters(save, request.encounteredMonsterIds);
            battle.progressRevision++;
            battle.reportedKills = request.killedMonsters;
            battle.reportedCompletedWaves = request.completedWaves;
            battle.reportedReachedWave = request.reachedWave;
            battle.reportedEncounters = new List<int>(request.encounteredMonsterIds);
            var ack = new BattleProgressAck
            {
                battleId = battle.battleKey,
                stageId = battle.stageId,
                policyVersion = 1,
                revision = battle.progressRevision,
                killedMonsters = request.killedMonsters,
                completedWaves = request.completedWaves,
                reachedWave = request.reachedWave,
                encounteredMonsterIds = new List<int>(request.encounteredMonsterIds),
                acceptedDelta = delta,
                acceptedAt = now.ToString("O"),
                dailyKey = QuestRules.PeriodKey(QuestPeriod.Daily, now),
                weeklyKey = QuestRules.PeriodKey(QuestPeriod.Weekly, now),
            };
            battle.progressReceipts.Add(new LocalProgressReceipt { request = request, ack = ack });
            _store.Flush(save);
            return UniTask.FromResult(ack);
        }
    }
}
