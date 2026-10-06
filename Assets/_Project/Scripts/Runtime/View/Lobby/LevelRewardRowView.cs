using Game.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.View
{
    /// <summary>레벨별 보상 한 줄. 달성한 레벨은 밝게, 아직이면 어둡게 보인다</summary>
    public sealed class LevelRewardRowView : MonoBehaviour
    {
        private static readonly Color ReachedRow = new Color(0.25f, 0.30f, 0.55f, 1f);
        private static readonly Color LockedRow = new Color(0.08f, 0.10f, 0.20f, 1f);
        private static readonly Color ReachedText = Color.white;
        private static readonly Color LockedText = new Color(1f, 1f, 1f, 0.45f);

        [SerializeField] private Image _background;
        [SerializeField] private TMP_Text _number;
        [SerializeField] private TMP_Text _text;

        public void Bind(LevelReward reward, bool reached)
        {
            gameObject.SetActive(true);
            _number.text = reward.Level.ToString();
            _text.text = reward.Text;
            _background.color = reached ? ReachedRow : LockedRow;
            _text.color = reached ? ReachedText : LockedText;
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}
