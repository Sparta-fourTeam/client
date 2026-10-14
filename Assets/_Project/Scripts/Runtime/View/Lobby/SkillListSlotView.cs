using System;
using Game.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.View
{
    /// <summary>스킬 화면의 칸 하나. 열려 있으면 레벨을, 잠겨 있으면 "레벨 N에 해금"을 표시한다</summary>
    public sealed class SkillListSlotView : MonoBehaviour
    {
        [SerializeField] private Button _button;
        [SerializeField] private TMP_Text _levelText;
        [SerializeField] private GameObject _lock;
        [SerializeField] private TMP_Text _lockLabel;
        [SerializeField] private Image _icon;
        [SerializeField] private Image _levelBadge;
        [SerializeField] private Sprite _idleLevelBadge;
        [SerializeField] private Sprite _learnedLevelBadge;
        [SerializeField] private Image _elementRing;
        [Tooltip("속성별 링. 목록에 없는 속성은 속성 없음(Neutral) 링을 쓴다")]
        [SerializeField] private ElementRingSprite[] _elementRings = Array.Empty<ElementRingSprite>();
        [Tooltip("레벨 글자의 아웃라인 머티리얼. 배운 스킬(민트 배지)과 아직 못 배운 스킬(어두운 배지)이 서로 다른 색 아웃라인을 쓴다")]
        [SerializeField] private Material _learnedLevelMaterial;
        [SerializeField] private Material _idleLevelMaterial;

        [Serializable]
        private struct ElementRingSprite
        {
            public Element Element;
            public Sprite Ring;
        }

        private Action _onClick;

        private void Awake()
        {
            _button.onClick.AddListener(() => _onClick?.Invoke());
        }

        /// <summary>잠긴 칸도 눌러 강화 팝업에서 스킬 정보를 볼 수 있다 (강화는 팝업이 막는다)</summary>
        public void Bind(SkillInfo info, Action onClick, Sprite icon = null)
        {
            gameObject.SetActive(true);
            if (_icon != null)
            {
                _icon.sprite = icon;
                _icon.enabled = icon != null;
            }
            _levelText.text = $"Lv.{info.Level}";
            var levelMaterial = info.Level > 0 ? _learnedLevelMaterial : _idleLevelMaterial;
            if (levelMaterial != null) { _levelText.fontSharedMaterial = levelMaterial; }
            var levelBadgeSprite = info.Level > 0 ? _learnedLevelBadge : _idleLevelBadge;
            if (_levelBadge != null && levelBadgeSprite != null)
            {
                _levelBadge.sprite = levelBadgeSprite;
            }
            if (_levelBadge != null) { _levelBadge.gameObject.SetActive(info.IsUnlocked); }
            if (_elementRing != null)
            {
                _elementRing.gameObject.SetActive(info.IsUnlocked);
                var ring = RingFor(info.Element);
                if (ring != null) { _elementRing.sprite = ring; }
            }
            _lock.SetActive(!info.IsUnlocked);
            _lockLabel.text = $"레벨 {info.UnlockLevel}에 해금";
            _onClick = onClick;
        }

        private Sprite RingFor(Element element)
        {
            Sprite fallback = null;
            foreach (var entry in _elementRings)
            {
                if (entry.Element == element) { return entry.Ring; }
                if (entry.Element == Element.Neutral) { fallback = entry.Ring; }
            }

            return fallback;
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}
