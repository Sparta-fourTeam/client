using Game.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.View
{
    /// <summary>미달성, 수령 가능, 수령 완료를 구분하는 관문 보상 상자.</summary>
    public sealed class MissionRewardNodeView : MonoBehaviour
    {
        [SerializeField] private Image _chest, _dot, _glow;
        [SerializeField] private Button _button;
        [SerializeField] private GameObject _check;
        [SerializeField] private TMP_Text _stateText;
        [SerializeField] private Sprite _closedChest, _openedChest;

        [Header("수령 가능 광채")]
        [Tooltip("상자 뒤의 광채 색상. 알파는 아래 밝기에 곱해집니다.")]
        [SerializeField] private Color _glowColor = new Color32(255, 210, 31, 255);
        [Tooltip("광채의 기본 가로·세로 크기")]
        [SerializeField] private Vector2 _glowSize = new(320f, 280f);
        [Tooltip("광채의 고정 밝기 (0~1)")]
        [SerializeField, Range(0f, 1f)] private float _glowAlpha = 0.85f;

        private bool _glowSettingsDirty;

        public Button Button => _button;

        public void Show(bool achieved, bool claimed, bool busy)
        {
            bool available = achieved && !claimed;
            _chest.sprite = claimed ? _openedChest : _closedChest;
            _chest.color = Color.white;
            _dot.color = Color.white;
            _check.SetActive(claimed);
            _glow.gameObject.SetActive(available);
            ApplyGlowSettings();
            _button.interactable = !busy;
            _stateText.SetL10n(() => claimed ? L10n.Get("missionNode.claimed") : available ? L10n.Get("missionNode.claim") : L10n.Get("missionNode.locked"));
            _stateText.color = claimed ? new Color32(23, 180, 136, 255)
                : available ? new Color32(31, 26, 51, 255) : new Color32(122, 115, 153, 255);
            _chest.rectTransform.localScale = Vector3.one;
        }

        private void OnEnable() => ApplyGlowSettings();

        private void OnValidate()
        {
            _glowSize = new Vector2(Mathf.Max(1f, _glowSize.x), Mathf.Max(1f, _glowSize.y));
            _glowAlpha = Mathf.Clamp01(_glowAlpha);
            _glowSettingsDirty = true;
        }

        private void LateUpdate()
        {
            // Inspector validation can run during loading; apply UI changes on the next game frame.
            if (!_glowSettingsDirty || _glow == null) { return; }
            _glowSettingsDirty = false;
            ApplyGlowSettings();
        }

        private void ApplyGlowSettings()
        {
            if (_glow == null) { return; }
            _glowSettingsDirty = false;
            _glow.rectTransform.sizeDelta = _glowSize;
            var color = _glowColor;
            color.a *= _glowAlpha;
            _glow.color = color;
            _glow.rectTransform.localScale = Vector3.one;
        }

    }
}
