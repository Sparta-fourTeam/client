using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Game.Core
{
    public sealed partial class StageManager
    {
        private BattleProgressRequest _pendingProgress;
        private long _progressRevision;
        private bool _progressRequested;
        private UniTaskCompletionSource _progressFlight;
        private bool _progressDisposed;
        /// <summary>진행 보고와 같은 실행 상태를 디스크에 확정한 뒤에만 전송한다.</summary>
        public Func<bool> PersistProgress { get; set; }

        private void QueueProgress()
        {
            if (_battleApi is not IBattleProgressApi) { return; }
            _progressRequested = true;
            SaveWaveProgress();
        }

        private async UniTaskVoid SaveWaveProgress()
        {
            // EnemyDied의 나머지 구독자와 웨이브 전환이 끝난 상태로 누적 수를 캡처한다.
            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);
            if (_progressDisposed) { return; }
            try { await FlushProgress(); }
            catch (Exception error) { Debug.LogWarning($"[BattleProgress] 진행 보고 보류: {error.Message}"); }
        }

        private UniTask FlushProgress()
        {
            if (_battleApi is not IBattleProgressApi) { return UniTask.CompletedTask; }
            if (_progressFlight != null) { return _progressFlight.Task; }
            var flight = new UniTaskCompletionSource();
            _progressFlight = flight;
            SendProgress(flight);
            return flight.Task;
        }

        private async UniTaskVoid SendProgress(UniTaskCompletionSource flight)
        {
            try
            {
                while (_progressRequested || _pendingProgress != null)
                {
                    if (_progressDisposed) { throw new OperationCanceledException(); }
                    if (_pendingProgress == null)
                    {
                        _pendingProgress = new BattleProgressRequest
                        {
                            battleId = _battleId,
                            reportId = Guid.NewGuid().ToString("D"),
                            expectedRevision = _progressRevision,
                            killedMonsters = _stats.Kills,
                            completedWaves = _rewards?.CompletedWaves ?? 0,
                            reachedWave = _stats.ReachedWave,
                            encounteredMonsterIds = _encountered?.Ids.OrderBy(id => id).ToList() ?? new List<int>(),
                        };
                        _progressRequested = false;
                    }
                    if (PersistProgress == null || !PersistProgress())
                    { throw new ApiException(ApiErrorKind.Transient, "PROGRESS_STORAGE_UNAVAILABLE"); }
                    var request = _pendingProgress;
                    var ack = await ((IBattleProgressApi)_battleApi).ReportProgress(request);
                    if (_progressDisposed) { throw new OperationCanceledException(); }
                    if (ack == null || ack.battleId != request.battleId || ack.revision != request.expectedRevision + 1
                        || ack.killedMonsters != request.killedMonsters)
                    { throw new ApiException(ApiErrorKind.Rejected, "INVALID_PROGRESS_ACK"); }
                    _progressRevision = ack.revision;
                    _pendingProgress = null;
                    if (!PersistProgress()) { throw new ApiException(ApiErrorKind.Transient, "PROGRESS_STORAGE_UNAVAILABLE"); }
                }
                _progressFlight = null;
                flight.TrySetResult();
            }
            catch (Exception error)
            {
                _progressFlight = null;
                flight.TrySetException(error);
            }
        }
    }
}
