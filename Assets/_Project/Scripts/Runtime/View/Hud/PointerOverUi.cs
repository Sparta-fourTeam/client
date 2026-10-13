using UnityEngine.EventSystems;

namespace Game.View
{
    /// <summary>포인터가 지금 UI(HUD 등) 위에 있는지. IMGUI나 월드 클릭 도구가 UI를 누른 클릭을 가로채지 않게 할 때 쓴다.</summary>
    public static class PointerOverUi
    {
        public static bool IsOver => EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }
}
