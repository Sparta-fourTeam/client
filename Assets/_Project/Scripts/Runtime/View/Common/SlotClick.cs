using System;
using UnityEngine;
using UnityEngine.UI;

namespace Game.View
{
    /// <summary>목록 칸을 누를 수 있게 하거나 못 하게 한다. 결과·일시정지 창처럼 보기만 하는 곳과
    /// 미션 상세처럼 눌러서 상세를 여는 곳이 같은 목록 프리팹을 쓰므로, 버튼은 누를 때 할 일이 있을 때만 붙인다.
    /// 목록 프리팹의 칸 이미지는 터치를 받지 않게(raycastTarget 꺼짐) 되어 있으므로, 누를 수 있을 때만 칸 이미지가 터치를 받게 한다</summary>
    public static class SlotClick
    {
        public static void Set(GameObject slot, Action onClick)
        {
            slot.TryGetComponent<Graphic>(out var graphic);
            if (graphic != null)
            {
                graphic.raycastTarget = onClick != null;
            }

            if (!slot.TryGetComponent<Button>(out var button))
            {
                if (onClick == null)
                {
                    return;
                }

                button = slot.AddComponent<Button>();
                button.transition = Selectable.Transition.None;
                button.targetGraphic = graphic;
                var navigation = button.navigation;
                navigation.mode = Navigation.Mode.None;
                button.navigation = navigation;
            }

            button.onClick.RemoveAllListeners();
            button.interactable = onClick != null;
            if (onClick != null)
            {
                button.onClick.AddListener(() => onClick());
            }
        }
    }
}
