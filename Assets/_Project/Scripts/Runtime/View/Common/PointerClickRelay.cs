using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.View
{
    /// <summary>이 오브젝트(보통 반투명 배경)를 누르면 Clicked를 알린다. 버튼이 아니라서 버튼 공통 눌림 효과가 붙지 않는다.
    /// 팝업 바깥을 눌러 닫을 때 쓴다</summary>
    public sealed class PointerClickRelay : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private Button _dismissButton;
        [SerializeField] private RectTransform[] _excludedAreas;

        public event Action Clicked;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) { return; }
            if (_excludedAreas != null)
            {
                foreach (var area in _excludedAreas)
                {
                    if (area != null && area.gameObject.activeInHierarchy
                        && RectTransformUtility.RectangleContainsScreenPoint(area, eventData.position, eventData.pressEventCamera))
                    {
                        return;
                    }
                }
            }

            if (_dismissButton != null && _dismissButton.isActiveAndEnabled && _dismissButton.IsInteractable())
            {
                _dismissButton.onClick.Invoke();
            }

            Clicked?.Invoke();
        }
    }
}
