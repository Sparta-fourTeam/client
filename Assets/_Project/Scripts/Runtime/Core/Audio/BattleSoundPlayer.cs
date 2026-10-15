using System;
using Game.Core.Messages;
using MessagePipe;
using VContainer.Unity;

namespace Game.Core
{
    /// <summary>전투 메시지를 받아 소리를 낸다. 게임플레이 코드는 오디오를 알 필요가 없도록 이 클래스가 메시지와 소리 ID를 잇는다.
    /// 스테이지 스코프에서만 살고, 스테이지를 나가면 배경음을 줄여 멈춘다</summary>
    public sealed class BattleSoundPlayer : IInitializable, IDisposable
    {
        private const float BgmFadeIn = 1f;
        private const float BgmFadeOut = 0.5f;

        private readonly IAudioService _audio;
        private readonly ISubscriber<EnemyDied> _enemyDied;
        private readonly ISubscriber<WaveStarted> _waveStarted;
        private readonly ISubscriber<SpecialEnemyWarning> _warning;
        private readonly ISubscriber<WallHpChanged> _wallHp;
        private readonly ISubscriber<WallDestroyed> _wallDestroyed;
        private readonly ISubscriber<StageEnded> _stageEnded;
        private readonly IBufferedSubscriber<StageStateChanged> _stageState;

        private IDisposable _subscriptions;
        private int _lastWallHp = -1;
        private StageState _state = StageState.Starting;
        private bool _bgmStarted;

        public BattleSoundPlayer(
            IAudioService audio,
            ISubscriber<EnemyDied> enemyDied,
            ISubscriber<WaveStarted> waveStarted,
            ISubscriber<SpecialEnemyWarning> warning,
            ISubscriber<WallHpChanged> wallHp,
            ISubscriber<WallDestroyed> wallDestroyed,
            ISubscriber<StageEnded> stageEnded,
            IBufferedSubscriber<StageStateChanged> stageState)
        {
            _audio = audio;
            _enemyDied = enemyDied;
            _waveStarted = waveStarted;
            _warning = warning;
            _wallHp = wallHp;
            _wallDestroyed = wallDestroyed;
            _stageEnded = stageEnded;
            _stageState = stageState;
        }

        public void Initialize()
        {
            var bag = DisposableBag.CreateBuilder();
            _enemyDied.Subscribe(e => _audio.PlaySfx(DeathSound(e.Type))).AddTo(bag);
            _waveStarted.Subscribe(_ => _audio.PlaySfx(SoundIds.WaveStart)).AddTo(bag);
            _warning.Subscribe(_ => _audio.PlaySfx(SoundIds.SpecialWarning)).AddTo(bag);
            _wallHp.Subscribe(OnWallHpChanged).AddTo(bag);
            _wallDestroyed.Subscribe(_ => _audio.PlaySfx(SoundIds.WallDestroyed)).AddTo(bag);
            _stageEnded.Subscribe(OnStageEnded).AddTo(bag);
            _stageState.Subscribe(OnStageState).AddTo(bag);
            _subscriptions = bag.Build();

        }

        public static string DeathSound(EnemyType type) => type switch
        {
            EnemyType.Elite => SoundIds.EliteDie,
            EnemyType.Boss => SoundIds.BossDie,
            _ => SoundIds.EnemyDie,
        };

        // 방벽 체력은 Buffered라 구독 직후 현재 값이 먼저 온다. 체크포인트 복원도 같은 메시지로 낮은 값을 발행하므로,
        // 복원이 끝나 Playing이 된 뒤에 줄어든 경우만 피격으로 본다
        private void OnWallHpChanged(WallHpChanged e)
        {
            if (_state == StageState.Playing && _lastWallHp >= 0 && e.Current < _lastWallHp && e.Current > 0)
            {
                _audio.PlaySfx(SoundIds.WallHit);
            }

            _lastWallHp = e.Current;
        }

        // 포기는 StageEnded 없이 결과 제출로 바로 가므로, 제출 단계에 들어가면 배경음을 멈춘다
        private void OnStageState(StageStateChanged e)
        {
            _state = e.State;
            if (e.State is StageState.Submitting or StageState.Finished)
            {
                _audio.StopBgm(BgmFadeOut);
                return;
            }

            // 이어하기는 Start 뒤에 체크포인트 상태가 복원되므로 Initialize에서 바로 틀지 않는다.
            // Starting을 벗어난 첫 진행 상태에서 한 번만 시작해, 이미 끝난 전투를 다시 열 때 순간적으로 울리지 않게 한다
            if (!_bgmStarted && e.State != StageState.Starting)
            {
                _bgmStarted = true;
                _audio.PlayBgm(SoundIds.BattleBgm, BgmFadeIn);
            }
        }

        private void OnStageEnded(StageEnded e)
        {
            _audio.StopBgm(BgmFadeOut);
            _audio.PlaySfx(e.Outcome == StageOutcome.Clear ? SoundIds.StageClear : SoundIds.StageFail);
        }

        public void Dispose()
        {
            _subscriptions?.Dispose();
            _audio.StopBgm(BgmFadeOut);
        }
    }
}
