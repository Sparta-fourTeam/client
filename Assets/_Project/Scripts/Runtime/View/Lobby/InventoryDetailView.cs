using System;
using Game.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.View
{
    /// <summary>인벤토리 아이템 상세. 아이콘·이름·보유 개수·설명을 보여주고, 강화 재료면 "강화하러 가기"로 해당 화면을 연다.
    /// 어두운 배경(이 오브젝트)을 누르거나 닫기를 누르면 닫힌다. 카드는 배경의 형제라서 카드를 눌러도 닫히지 않는다</summary>
    public sealed class InventoryDetailView : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private Image _icon;
        [SerializeField] private TMP_Text _nameText, _countText, _descText;
        [SerializeField] private Button _goButton, _closeButton;
        [SerializeField] private TMP_Text _goLabel;

        private Action _onGo;
        private bool _listening;

        public bool IsShown => _root.activeSelf;

        // 꺼진 채로 시작하는 팝업이라 Awake가 늦게 돈다. 처음 열 때 한 번만 연결한다
        private void Listen()
        {
            if (_listening)
            {
                return;
            }

            _listening = true;
            _goButton.onClick.AddListener(() =>
            {
                var go = _onGo;
                Hide();
                go?.Invoke();
            });
            _closeButton.onClick.AddListener(Hide);
        }

        /// <summary>설명 문구. 마법북은 스킬 화면에서, 주문서는 캐릭터 화면에서 쓴다</summary>
        public static string DescriptionOf(InventoryCategory category) => category switch
        {
            InventoryCategory.SkillMaterial => "스킬을 강화할 때 쓰는 마법북이에요.\n스킬 화면에서 사용해요.",
            InventoryCategory.EquipmentMaterial => "장비를 강화할 때 쓰는 주문서예요.\n캐릭터 화면에서 사용해요.",
            _ => "보관 중인 아이템이에요.",
        };

        /// <param name="onGo">"강화하러 가기"를 누르면 부른다. null이면 버튼을 숨긴다</param>
        public void Show(InventoryItem item, Sprite icon, Action onGo, string goLabel = "강화하러 가기")
        {
            Listen();
            if (icon != null)
            {
                _icon.sprite = icon;
            }

            _nameText.text = item.Name;
            _countText.text = $"보유 {CurrencyFormat.Format(item.Quantity)}개";
            _descText.text = DescriptionOf(item.Category);
            _onGo = onGo;
            _goButton.gameObject.SetActive(onGo != null);
            _goLabel.text = goLabel;
            _root.SetActive(true);
        }

        public void Hide()
        {
            _onGo = null;
            _root.SetActive(false);
        }

        public void OnPointerClick(PointerEventData eventData) => Hide();
    }
}
