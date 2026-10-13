using UnityEngine;

namespace Game.View
{
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class NotificationBadgeView : HudView
    {
        public bool IsVisible => GetComponent<CanvasGroup>().alpha > 0;
        protected override void InitializeView() => SetVisible(false);
        public void SetVisible(bool visible)
        {
            var group = GetComponent<CanvasGroup>();
            group.alpha = visible ? 1f : 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
        }
    }
}
