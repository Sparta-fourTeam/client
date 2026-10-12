using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.View
{
    /// <summary>공용 아이템 칸(Prefabs/UI/Common/ItemCell). 배경·테두리·아이콘·개수로 이루어지고, 누를 일이 있으면 누를 수 있다.
    /// 보상 목록(RewardList)과 인벤토리가 같이 쓴다. 칸의 색·크기는 쓰는 화면이 프리팹 인스턴스에서 덮어쓴다</summary>
    public sealed class ItemCellView : MonoBehaviour
    {
        // 빈칸은 배경을 이만큼 어둡게 그린다
        private const float EmptyDim = 0.6f;

        [SerializeField] private Image _background;
        [SerializeField] private Image _icon;
        [SerializeField] private TMP_Text _count;

        private Color _baseColor;
        private bool _hasBaseColor;

        public bool IsEmpty { get; private set; }

        public Image Icon => _icon;
        public string CountText => _count != null ? _count.text : string.Empty;

        /// <param name="icon">null이면 지금 아이콘(프리팹의 자리표시)을 그대로 둔다</param>
        /// <param name="onClick">누르면 부른다. null이면 눌리지 않는다</param>
        public void Show(Sprite icon, string count, Action onClick = null)
        {
            IsEmpty = false;
            if (icon != null)
            {
                _icon.sprite = icon;
            }

            _icon.enabled = true;
            _count.text = count;
            SetBackground(1f);
            SlotClick.Set(gameObject, onClick);
            gameObject.SetActive(true);
        }

        /// <summary>아이템이 없는 칸. 아이콘과 개수를 숨기고 배경을 어둡게 하며 눌리지 않는다</summary>
        public void ShowEmpty()
        {
            IsEmpty = true;
            _icon.enabled = false;
            _count.text = string.Empty;
            SetBackground(EmptyDim);
            SlotClick.Set(gameObject, null);
            gameObject.SetActive(true);
        }

        public void Hide() => gameObject.SetActive(false);

        private void SetBackground(float brightness)
        {
            if (_background == null)
            {
                return;
            }

            // 처음 칠할 때의 색을 원래 색으로 기억한다 (Awake가 불리지 않는 편집 모드에서도 같게 동작한다)
            if (!_hasBaseColor)
            {
                _baseColor = _background.color;
                _hasBaseColor = true;
            }

            _background.color = new Color(_baseColor.r * brightness, _baseColor.g * brightness, _baseColor.b * brightness, _baseColor.a);
        }
    }
}
