using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.View
{
    /// <summary>이미 표시된 언어를 다시 고른 경우도 명시적 선택으로 저장한다. 드롭다운의 변경 이벤트는 같은 값을 생략한다.</summary>
    public sealed class LanguageOptionSelection : MonoBehaviour, IPointerClickHandler, ISubmitHandler
    {
        // TMP 드롭다운이 템플릿을 복제할 때 원래 설정 뷰 참조를 함께 보존한다.
        [SerializeField] private SettingsPopupView _view;

        internal void Initialize(SettingsPopupView view) => _view = view;

        /// <summary>왼쪽 클릭으로 고른 언어를 확정한다.</summary>
        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left) { Confirm(); }
        }

        /// <summary>키보드·게임패드로 고른 언어를 확정한다.</summary>
        public void OnSubmit(BaseEventData eventData) => Confirm();

        private void Confirm()
        {
            var toggle = GetComponent<Toggle>();
            var dropdown = GetComponentInParent<TMP_Dropdown>();
            // Content의 첫 자식은 비활성 템플릿이고, 실제 언어 항목은 그 다음부터 생성된다.
            int index = transform.GetSiblingIndex() - 1;
            if (_view != null && toggle != null && toggle.IsActive() && toggle.IsInteractable()
                && dropdown != null && index >= 0 && index < dropdown.options.Count)
            {
                _view.ConfirmLanguageSelection(index);
            }
        }
    }
}
