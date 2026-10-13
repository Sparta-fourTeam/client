using Game.Core;
using Game.Core.Messages;
using MessagePipe;
using UnityEngine;
using VContainer;

namespace Game.View
{
    /// <summary>결과를 서버에 제출하는 동안(Submitting) 화면을 덮는 "전송 중" 표시.
    /// 제출이 거절되거나 실패하면 상태는 Submitting 그대로라서 SubmitRejected, SubmitFailed도 같이 받아 숨긴다.
    /// 응답이 빨라도 잠깐 떴다 사라지는 깜빡임이 생기지 않게, 일정 시간 이상 걸릴 때만 보여준다</summary>
    public sealed class SubmittingView : HudView
    {
        [SerializeField] private GameObject _panel;
        [SerializeField, Min(0f)] private float _showDelaySeconds = 0.3f;

        private bool _waiting;
        private float _waitingSince;

        [Inject]
        public void Construct(
            IBufferedSubscriber<StageStateChanged> stateChanged,
            ISubscriber<SubmitRejected> submitRejected,
            ISubscriber<SubmitFailed> submitFailed)
        {
            Track(stateChanged.Subscribe(OnStateChanged));
            Track(submitRejected.Subscribe(_ => Hide()));
            Track(submitFailed.Subscribe(_ => Hide()));
        }

        protected override void InitializeView()
        {
            _panel.SetActive(false);
        }

        private void OnStateChanged(StageStateChanged message)
        {
            if (message.State == StageState.Submitting)
            {
                _waiting = true;
                _waitingSince = Time.unscaledTime;
            }
            else
            {
                Hide();
            }
        }

        // 포기에서는 timeScale이 0이라 unscaled time으로 센다
        private void Update()
        {
            if (_waiting && !_panel.activeSelf && Time.unscaledTime - _waitingSince >= _showDelaySeconds)
            {
                _panel.SetActive(true);
            }
        }

        private void Hide()
        {
            _waiting = false;
            _panel.SetActive(false);
        }
    }
}
