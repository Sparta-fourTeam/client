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
        private readonly StageClock _clock;
        private readonly ISubscriber<WaveCompleted> _waveCompleted;
        private readonly CardDeck _deck;
        private readonly IPublisher<SubmitRejected> _submitRejected;
        private readonly IPublisher<SubmitFailed> _submitFailed;
        private readonly StageRewardTracker _rewards;

        /// <summary>일시적 실패와 네트워크 실패 때 자동으로 다시 보내는 횟수 (docs/flows.md)</summary>
        public const int AutoRetryCount = 3;

        private float _speed = 1f;
        private StageState _beforePause;
        private string _battleId;
        private IDisposable _subscription;

        // 처음 만든 요청을 재시도에도 그대로 쓴다. 다시 만들면 playTime과 createdAt이 달라 다른 요청이 된다
        private SubmitResultRequest _pendingRequest;
        private bool _submitting;

        private List<UpgradeChoice> _choices = new();

        public StageState State { get; private set; } = StageState.Starting;

        /// <summary>자동 재시도 사이의 간격. 테스트에서 0으로 줄인다</summary>
        public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(1);

        /// <summary>CardSelect 상태에서 UI가 표시할 카드 후보</summary>
        public IReadOnlyList<UpgradeChoice> Choices => _choices;

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
            IPublisher<SubmitFailed> submitFailed, StageRewardTracker rewards = null)
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
        }

        public void Start()
        {
            var bag = DisposableBag.CreateBuilder();
            _stageEnded.Subscribe(e => OnStageEnded(e.Outcome == StageOutcome.Clear).Forget(Debug.LogException)).AddTo(bag);
            _waveCompleted.Subscribe(OnWaveCompleted).AddTo(bag);
            _subscription = bag.Build();

            // 전투는 Lobby의 BattleLauncher가 이미 발급받아 StageContext에 채워뒀다 — 여기선 꺼내 쓰기만 한다
            _battleId = _stageContext.BattleId;
            ChangeState(StageState.Playing);
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
            if (_beforePause == StageState.Playing)
            {
                Time.timeScale = _speed;
            }

            ChangeState(_beforePause);
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

        /// <summary>마지막이 아닌 웨이브가 끝나면 카드 3장을 제시하고 CardSelect로 전환한다. 적 이동·스폰도 timeScale로 함께 멈춘다</summary>
        private void OnWaveCompleted(WaveCompleted e)
        {
            if (e.IsFinalWave || State != StageState.Playing)
            {
                return;
            }

            var choices = _deck.Draw(3);

            // 고를 카드가 없으면(무기 만렙, 강화 소진) 카드 선택으로 넘어가지 않는다.
            // 넘어가면 PickCard가 항상 무시되고 Pause/Resume으로도 CardSelect로 돌아와 영영 멈춘다
            if (choices.Count == 0)
            {
                return;
            }

            _choices = choices;
            Time.timeScale = 0f;
            ChangeState(StageState.CardSelect);
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

                Time.timeScale = _speed;
                ChangeState(StageState.Playing);
                return;
            }
            _stats.RecordCard(choice.IsGeneral ? choice.GeneralCard.Id
                : choice.IsNewWeapon ? $"weapon_{choice.newSkillData.id}" : choice.Option.id);
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

            OnStageEnded(false).Forget(Debug.LogException);
        }

        private async UniTask OnStageEnded(bool cleared)
        {
            if (State is StageState.Submitting or StageState.Finished) { return; }
            _rewards?.Stop();
            ChangeState(StageState.Submitting);

            _pendingRequest = new SubmitResultRequest
            {
                battleId = _battleId,
                cleared = cleared,
                reachedWave = _stats.ReachedWave,
                completedWaves = _rewards?.CompletedWaves ?? 0,
                kills = _stats.Kills,
                playTime = _clock.ElapsedSeconds,
                buildLog = _stats.BuildLog,
                wallHpPercent = cleared ? _stats.WallHpPercent : 0,
                createdAt = DateTime.UtcNow.ToString("O")
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
                _result.Publish(new StageResult(response.cleared, request.kills, request.reachedWave, request.playTime,
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
