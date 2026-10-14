using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game.Core.Messages;
using MessagePipe;
using UnityEngine;
using VContainer.Unity;

namespace Game.Core
{
    /// <summary>스테이지 상태 머신. 전투 발급부터 결과 제출까지 스테이지 한 판의 생애주기를 관리한다</summary>
    public sealed class StageManager : IStartable, IDisposable
    {
        private readonly IBufferedPublisher<StageStateChanged> _stateChanged;
        private readonly ISubscriber<StageEnded> _stageEnded;
        private readonly IPublisher<StageResult> _result;
        private readonly IBattleApi _battleApi;
        private readonly StageContext _stageContext;
        private readonly BattleStats _stats;
        private readonly EncounteredMonsters _encountered;
        private readonly StageClock _clock;
        private readonly ISubscriber<WaveCompleted> _waveCompleted;
        private readonly CardDeck _deck;
        private readonly IPublisher<SubmitRejected> _submitRejected;
        private readonly IPublisher<SubmitFailed> _submitFailed;
        private readonly StageRewardTracker _rewards;
        private readonly ISubscriber<EnemyDied> _enemyDied;
        private readonly EliteRouletteSession _roulette;
        private readonly IGameplayAnalytics _analytics;
        private readonly HashSet<int> _rewardedElites = new();
        private int _pendingElites, _pendingCardSelections;

        /// <summary>일시적 실패와 네트워크 실패 때 자동으로 다시 보내는 횟수 (docs/flows.md)</summary>
        public const int AutoRetryCount = 3;

        private float _speed = 1f;
        private StageState _beforePause;
        private string _battleId;
        private IDisposable _subscription;

        // 처음 만든 요청을 재시도에도 그대로 쓴다. 결과 화면 표시용 값(kills, playTime)은 서버로 보내지 않고 종료 시점에 고정해 둔다
        private SubmitResultRequest _pendingRequest;
        private int _resultKills;
        private float _resultPlayTime;
        private bool _submitting;

        private List<UpgradeChoice> _choices = new();

        public StageState State { get; private set; } = StageState.Starting;

        /// <summary>자동 재시도 사이의 간격. 테스트에서 0으로 줄인다</summary>
        public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(1);

        /// <summary>CardSelect 상태에서 UI가 표시할 카드 후보</summary>
        public IReadOnlyList<UpgradeChoice> Choices => _choices;
        public EliteRouletteSession Roulette => _roulette;

        public StageManager(
            IBufferedPublisher<StageStateChanged> stateChanged,
            ISubscriber<StageEnded> stageEnded,
            IPublisher<StageResult> result,
            IBattleApi battleApi,
            StageContext stageContext,
            BattleStats stats,
            StageClock clock,
            ISubscriber<WaveCompleted> waveCompleted,
            CardDeck deck,
            IPublisher<SubmitRejected> submitRejected,
            IPublisher<SubmitFailed> submitFailed, StageRewardTracker rewards = null, EncounteredMonsters encountered = null,
            ISubscriber<EnemyDied> enemyDied = null, EliteRouletteSession roulette = null,
            IGameplayAnalytics analytics = null)
        {
            _stateChanged = stateChanged;
            _stageEnded = stageEnded;
            _result = result;
            _battleApi = battleApi;
            _stageContext = stageContext;
            _stats = stats;
            _clock = clock;
            _waveCompleted = waveCompleted;
            _deck = deck;
            _submitRejected = submitRejected;
            _submitFailed = submitFailed;
            _rewards = rewards;
            _encountered = encountered;
            _enemyDied = enemyDied;
            _roulette = roulette;
            _analytics = analytics;
        }

        public void Start()
        {
            var bag = DisposableBag.CreateBuilder();
            _stageEnded.Subscribe(e => OnStageEnded(e.Outcome == StageOutcome.Clear).Forget(Debug.LogException)).AddTo(bag);
            _waveCompleted.Subscribe(OnWaveCompleted).AddTo(bag);
            _enemyDied?.Subscribe(OnEnemyDied).AddTo(bag);
            _subscription = bag.Build();

            // 전투는 Lobby의 BattleLauncher가 이미 발급받아 StageContext에 채워뒀다 — 여기선 꺼내 쓰기만 한다
            _battleId = _stageContext.BattleId;
            ChangeState(StageState.Playing);
            _analytics?.BattleStarted(_stageContext.StageId);
        }

        public void Dispose()
        {
            _subscription?.Dispose();
            // Paused/CardSelect 상태로 씬을 벗어나도 timeScale이 0으로 새어나가 다음 씬까지 멈추지 않게 한다
            Time.timeScale = 1f;
        }

        public void Pause()
        {
            if (State is not (StageState.Playing or StageState.CardSelect))
            {
                return;
            }

            _beforePause = State;
            Time.timeScale = 0f;
            ChangeState(StageState.Paused);
        }

        public void Resume()
        {
            if (State != StageState.Paused)
            {
                return;
            }

            // CardSelect였다면 카드를 고르기 전까지 계속 멈춰있어야 하므로 timeScale은 그대로 0으로 둔다
            if (_beforePause == StageState.Playing) { ContinueRewards(); }
            else { ChangeState(_beforePause); }
        }

        public void SetSpeed(float speed)
        {
            _speed = speed >= 2f ? 2f : 1f;
            PlayerPrefs.SetFloat("stage_speed", _speed);
            if (State == StageState.Playing)
            {
                Time.timeScale = _speed;
            }
        }

#if UNITY_EDITOR
        /// <summary>현재 전투에만 치트 배속을 적용한다. 멈춘 상태의 늦은 클릭은 무시하고 일반 배속 선호값은 저장하지 않는다</summary>
        public void SetPlaytestSpeed(float speed)
        {
            if (State != StageState.Playing || speed is not (1f or 4f or 8f)) { return; }
            _speed = speed;
            Time.timeScale = _speed;
        }
#endif

        /// <summary>마지막이 아닌 웨이브가 끝나면 카드 3장을 제시하고 CardSelect로 전환한다. 적 이동·스폰도 timeScale로 함께 멈춘다</summary>
        private void OnWaveCompleted(WaveCompleted e)
        {
            if (e.IsFinalWave || State is StageState.Starting or StageState.Submitting or StageState.Finished)
            {
                return;
            }

            _pendingCardSelections++;
            if (State == StageState.Playing) { ContinueRewards(); }
        }

        public void PickCard(int index)
        {
            if (State != StageState.CardSelect || index < 0 || index >= _choices.Count)
            {
                return;
            }

            var choice = _choices[index];
            if (!_deck.TryApply(choice))
            {
                // Refresh stale choices; if none remain, resume without recording a card.
                _choices = _deck.Draw(3);
                if (_choices.Count > 0)
                {
                    ChangeState(StageState.CardSelect);
                    return;
                }

                ContinueRewards();
                return;
            }
            _stats.RecordCard(choice.IsGeneral ? choice.GeneralCard.Id
                : choice.IsNewWeapon ? $"weapon_{choice.newSkillData.id}" : choice.Option.id);
            _analytics?.CardPicked(_stageContext.StageId, _stats.BuildLog[^1], _stats.BuildLog.Count, "wave");
            _choices = new List<UpgradeChoice>();

            ContinueRewards();
        }

        private void OnEnemyDied(EnemyDied message)
        {
            if (message.Type != EnemyType.Elite || _roulette == null
                || State is StageState.Starting or StageState.Submitting or StageState.Finished
                || !_rewardedElites.Add(message.EnemyId)) { return; }
            _pendingElites++;
            if (State == StageState.Playing) { ContinueRewards(); }
        }

        public bool ApplyRouletteReward(EliteRouletteRound round)
        {
            if (State != StageState.EliteRoulette || !_roulette.TryApply(round)) { return false; }
            _stats.RecordCard(round.Winner.Option.id);
            _analytics?.CardPicked(_stageContext.StageId, _stats.BuildLog[^1], _stats.BuildLog.Count, "elite_roulette");
            return true;
        }

        public void FinishEliteRoulette()
        {
            if (State == StageState.EliteRoulette && _roulette.IsComplete) { ContinueRewards(); }
        }

        private void ContinueRewards()
        {
            while (_pendingElites > 0)
            {
                _pendingElites--;
                if (_roulette.Start())
                {
                    Time.timeScale = 0f;
                    ChangeState(StageState.EliteRoulette);
                    return;
                }
            }
            while (_pendingCardSelections > 0)
            {
                _pendingCardSelections--;
                _choices = _deck.Draw(3);
                if (_choices.Count == 0) { continue; }
                Time.timeScale = 0f;
                ChangeState(StageState.CardSelect);
                return;
            }
            _choices = new List<UpgradeChoice>();
            Time.timeScale = _speed;
            ChangeState(StageState.Playing);
        }

        /// <summary>Paused에서만 가능 — 판정 없이 결과 전송으로 바로 간다</summary>
        public void Forfeit()
        {
            if (State != StageState.Paused)
            {
                return;
            }

            OnStageEnded(false, forfeited: true).Forget(Debug.LogException);
        }

        private async UniTask OnStageEnded(bool cleared, bool forfeited = false)
        {
            if (State is StageState.Submitting or StageState.Finished) { return; }
            _rewards?.Stop();
            _pendingElites = _pendingCardSelections = 0;
            _roulette?.Cancel();
            // 클리어·실패는 Playing 중에 끝나 timeScale이 배속 그대로라 적·투사체가 결과 화면 뒤에서 계속 움직인다.
            // 제출 대기·재시도 연출은 unscaled time을 쓰므로 멈춰도 된다. 다음 전투는 Dispose가 1로 되돌린다
            Time.timeScale = 0f;
            ChangeState(StageState.Submitting);

            _resultKills = _stats.Kills;
            _resultPlayTime = _clock.ElapsedSeconds;
            _analytics?.BattleEnded(_stageContext.StageId, cleared, forfeited, _stats.ReachedWave,
                _resultKills, _resultPlayTime);
            _pendingRequest = new SubmitResultRequest
            {
                battleId = _battleId,
                cleared = cleared,
                reachedWave = _stats.ReachedWave,
                completedWaves = _rewards?.CompletedWaves ?? 0,
                wallHpPercent = cleared ? _stats.WallHpPercent : 0,
                encounteredMonsterIds = _encountered == null ? new List<int>() : new List<int>(_encountered.Ids)
            };

            await Submit(AutoRetryCount);
        }

        /// <summary>SubmitFailed(Retryable) 뒤에 플레이어가 재시도를 눌렀을 때. 처음 만든 요청을 그대로 다시 보낸다.
        /// 제출 중이거나 보낼 요청이 없으면 무시한다(연타 방지)</summary>
        public void RetrySubmit()
        {
            if (State != StageState.Submitting || _pendingRequest == null || _submitting)
            {
                return;
            }

            // 같은 상태를 다시 발행해 "전송 중" 표시가 다시 뜨고 실패 안내가 닫히게 한다
            ChangeState(StageState.Submitting);
            Submit(0).Forget(Debug.LogException);
        }

        /// <summary>요청을 보낸다. 일시적 실패와 네트워크 실패는 autoRetries번까지 자동으로 다시 보낸다</summary>
        private async UniTask Submit(int autoRetries)
        {
            var request = _pendingRequest;
            _submitting = true;
            try
            {
                SubmitResultResponse response = null;
                var sent = false;
                for (var attempt = 0; !sent; attempt++)
                {
                    try
                    {
                        response = await _battleApi.SubmitResult(request);
                        sent = true;
                    }
                    catch (ApiException e) when (e.Kind == ApiErrorKind.Rejected)
                    {
                        // 여기서 로비로 이동하지 않는다: 이동하면 Stage 씬이 사라져 거절 안내가 보이지 않는다.
                        // 안내를 보여주고 로비로 보내는 것은 SubmitRejected를 구독하는 뷰의 몫이다 (docs/flows.md: 거절 안내 후 Lobby)
                        _pendingRequest = null;
                        _submitRejected.Publish(new SubmitRejected(e.Code));
                        return;
                    }
                    catch (ApiException e)
                    {
                        // Transient, Network
                        if (attempt < autoRetries)
                        {
                            if (RetryDelay > TimeSpan.Zero)
                            {
                                // 연출과 같은 이유로 unscaled time. 포기에서는 timeScale이 0이다
                                await UniTask.Delay(RetryDelay, ignoreTimeScale: true);
                            }

                            continue;
                        }

                        _submitFailed.Publish(new SubmitFailed(e.Code, retryable: true));
                        return;
                    }
                    catch (Exception e)
                    {
                        // 예상 밖 예외는 다시 보내도 같은 결과일 가능성이 커서 재시도하지 않는다. 원인은 로그로 남긴다
                        Debug.LogException(e);
                        _submitFailed.Publish(new SubmitFailed("UNEXPECTED"));
                        return;
                    }
                }

                _pendingRequest = null;
                _result.Publish(new StageResult(request.cleared, _resultKills, request.reachedWave, _resultPlayTime,
                    response.rewardGold, response.rewardItems, response.rewardExp, response.clearRating,
                    _stats.SkillDamageRanking()));
                ChangeState(StageState.Finished);
            }
            finally
            {
                _submitting = false;
            }
        }

        private void ChangeState(StageState next)
        {
            State = next;
            _stateChanged.Publish(new StageStateChanged(next));
        }
    }
}
