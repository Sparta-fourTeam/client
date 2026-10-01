using Game.Core;
using Game.Core.Messages;
using MessagePipe;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Game.View
{
    public sealed class StageControlView : HudView
    {
        // StageManager.SetSpeed가 2배로 제한하므로 1x ↔ 2x만 돈다
        private static readonly float[] Speeds = { 1f, 2f };

        [SerializeField] private Button _speedButton;
        // [SerializeField] private TMP_Text _speedLabel;
        [SerializeField] private Button _pauseButton;
        [SerializeField] private Sprite[] _buttonimages;

        private StageManager _stageManager;
        private int _speedIndex;
        [SerializeField] private Image buttonImage;

        [Inject]
        public void Construct(
            StageManager stageManager,
            IBufferedSubscriber<StageStateChanged> stateChangedSubscriber)
        {
            _stageManager = stageManager;
            Track(stateChangedSubscriber.Subscribe(OnStateChanged));
        }

        private void Awake()
        {
            // 첫 StageStateChanged가 오기 전까지는 누를 수 없게 시작한다
            buttonImage.sprite = _buttonimages[0];
            _speedButton.interactable = false;
            _pauseButton.interactable = false;
            _speedButton.onClick.AddListener(OnSpeedClicked);
            _pauseButton.onClick.AddListener(() => _stageManager.Pause());
            // UpdateSpeedLabel();
        }

        private void OnSpeedClicked()
        {
            _speedIndex = (_speedIndex + 1) % Speeds.Length;
            _stageManager.SetSpeed(Speeds[_speedIndex]);
            buttonImage.sprite = _buttonimages[_speedIndex];
            // UpdateSpeedLabel();
        }

        // Playing에서만 누를 수 있다. 카드 선택·결과 전송 중에는 막는다
        private void OnStateChanged(StageStateChanged message)
        {
            bool playing = message.State == StageState.Playing;
            _speedButton.interactable = playing;
            _pauseButton.interactable = playing;
        }

        // private void UpdateSpeedLabel()
        // {
        //     _speedLabel.text = $"{Speeds[_speedIndex]:0.#}x";
        // }
    }
}
