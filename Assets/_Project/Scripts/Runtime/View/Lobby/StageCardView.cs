using Game.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.View
{
    /// <summary>스테이지 선택창의 관문 카드 한 장: 관문 이름, 잠김, 별 보상 상자, 안내 문구.
    /// 가운데 카드는 MissionScreenView가 버튼을 연결해 쓰고, 양옆 미리보기 카드(StageCarouselView)는 같은 모양으로 그리기만 한다</summary>
    public sealed class StageCardView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _stageText;
        [SerializeField] private GameObject _lockOverlay;
        [SerializeField] private Image _rewardFill;             // RewardTrack/Bar/Fill
        [SerializeField] private MissionRewardNodeView[] _rewardNodes;
        [SerializeField] private Button _rewardAreaButton;
        [SerializeField] private Button _infoButton;
        [SerializeField] private GameObject _rewardBadge;
        [SerializeField] private TMP_Text _rewardStatus;

        public Button InfoButton => _infoButton;
        public Button RewardAreaButton => _rewardAreaButton;
        public MissionRewardNodeView[] RewardNodes => _rewardNodes;

        /// <summary>안내 문구. 보상 수령 실패 등은 Show 뒤에 덮어쓴다</summary>
        public string Status
        {
            get => _rewardStatus.text;
            set => _rewardStatus.text = value;
        }

        public void Show(int stageId, PlayerProfile profile, bool busy, bool claiming)
        {
            bool unlocked = profile.IsStageUnlocked(stageId);
            int rating = profile.ClearRating(stageId);
            int claimed = profile.ClaimedRating(stageId);

            _stageText.text = L10n.Get("stage.title", stageId);
            _lockOverlay.SetActive(!unlocked);
            _infoButton.interactable = !busy;
            for (int i = 0; i < _rewardNodes.Length; i++)
            {
                _rewardNodes[i].Show(rating > i, claimed > i, busy);
            }

            _rewardFill.fillAmount = rating <= 1 ? 0f : (rating - 1) / 2f;
            bool available = profile.HasUnclaimedReward(stageId);
            _rewardAreaButton.gameObject.SetActive(available);
            _rewardAreaButton.interactable = !busy;
            _rewardBadge.SetActive(available);
            _rewardStatus.text = claiming ? L10n.Get("stage.reward.claiming")
                : available ? L10n.Get("stage.reward.available")
                : claimed >= 3 ? L10n.Get("stage.reward.allClaimed")
                : !unlocked ? L10n.Get("stage.reward.locked")
                : L10n.Get("stage.reward.default");
        }
    }
}
