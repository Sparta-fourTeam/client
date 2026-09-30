using System;
using Game.Core.Messages;
using MessagePipe;
using UnityEngine;
using VContainer.Unity;

namespace Game.Core
{
    /// <summary>Playing 상태일 때만 흐르는 플레이 시간. SubmitResultRequest.playTime으로 쓰인다.
    /// StageManager를 직접 참조하면 순환 의존(StageManager -> StageClock -> StageManager)이 생기므로
    /// StageStateChanged 메시지로만 상태를 안다</summary>
    public sealed class StageClock : IStartable, ITickable, IDisposable
    {
        private readonly ISubscriber<StageStateChanged> _stateChanged;
        private IDisposable _subscription;
        private bool _isPlaying;

        public float ElapsedSeconds { get; private set; }

        public StageClock(ISubscriber<StageStateChanged> stateChanged)
        {
            _stateChanged = stateChanged;
        }

        public void Start()
        {
            _subscription = _stateChanged.Subscribe(e => _isPlaying = e.State == StageState.Playing);
        }

        public void Tick()
        {
            Accumulate(Time.deltaTime);
        }

        /// <summary>Tick()이 실제로 하는 일. Time.deltaTime을 직접 읽지 않아 EditMode 테스트에서 결정적으로 검증할 수 있다</summary>
        public void Accumulate(float deltaTime)
        {
            if (_isPlaying)
            {
                ElapsedSeconds += deltaTime;
            }
        }

        public void Dispose()
        {
            _subscription?.Dispose();
        }
    }
}
