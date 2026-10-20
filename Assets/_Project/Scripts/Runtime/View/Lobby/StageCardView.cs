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

            _stageText.text = $"제 {stageId} 관문";
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
            _rewardStatus.text = claiming ? "보상을 받는 중이에요..."
                : available ? "상자 영역을 눌러 보상을 모두 받으세요"
                : claimed >= 3 ? "모든 보상을 받았어요. 상자를 눌러 정보를 확인하세요"
                : !unlocked ? "이전 관문을 클리어하면 열려요"
                : "상자를 눌러 달성 조건과 보상을 확인하세요";
        }
    }
}
