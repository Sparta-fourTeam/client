using Game.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.View
{
    /// <summary>레벨별 보상 한 줄. 달성한 레벨은 밝게, 아직이면 어둡게 보인다</summary>
    public sealed class LevelRewardRowView : MonoBehaviour
    {
        private static readonly Color ReachedRow = new Color(1f, 0.965f, 0.878f, 1f);
        private static readonly Color LockedRow = new Color(0.478f, 0.451f, 0.6f, 1f);
        private static readonly Color ReachedText = new Color(0.122f, 0.102f, 0.2f, 1f);
        private static readonly Color LockedText = new Color(0.769f, 0.745f, 0.890f, 1f);

        [SerializeField] private Image _background;
        [SerializeField] private Sprite _reachedBackground, _lockedBackground;
        [SerializeField] private TMP_Text _number;
        [SerializeField] private TMP_Text _text;

        public void Bind(LevelReward reward, bool reached)
        {
            gameObject.SetActive(true);
            _number.text = reward.Level.ToString();
            _text.text = reward.Text;
            Sprite stateSprite = reached ? _reachedBackground : _lockedBackground;
            if (stateSprite != null)
            {
                _background.sprite = stateSprite;
                _background.color = Color.white;
            }
            else
            {
                _background.color = reached ? ReachedRow : LockedRow;
            }
            _text.color = reached ? ReachedText : LockedText;
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}
