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

        private IDisposable _subscriptions;
        private int _lastWallHp = -1;

        public BattleSoundPlayer(
            IAudioService audio,
            ISubscriber<EnemyDied> enemyDied,
            ISubscriber<WaveStarted> waveStarted,
            ISubscriber<SpecialEnemyWarning> warning,
            ISubscriber<WallHpChanged> wallHp,
            ISubscriber<WallDestroyed> wallDestroyed,
            ISubscriber<StageEnded> stageEnded)
        {
            _audio = audio;
            _enemyDied = enemyDied;
            _waveStarted = waveStarted;
            _warning = warning;
            _wallHp = wallHp;
            _wallDestroyed = wallDestroyed;
            _stageEnded = stageEnded;
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
            _subscriptions = bag.Build();

            _audio.PlayBgm(SoundIds.BattleBgm, BgmFadeIn);
        }

        public static string DeathSound(EnemyType type) => type switch
        {
            EnemyType.Elite => SoundIds.EliteDie,
            EnemyType.Boss => SoundIds.BossDie,
            _ => SoundIds.EnemyDie,
        };

        // 방벽 체력은 Buffered라 구독 직후 현재 값이 먼저 온다. 그 값은 기준으로만 삼고 줄어들 때만 소리를 낸다
        private void OnWallHpChanged(WallHpChanged e)
        {
            if (_lastWallHp >= 0 && e.Current < _lastWallHp && e.Current > 0)
            {
                _audio.PlaySfx(SoundIds.WallHit);
            }

            _lastWallHp = e.Current;
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
