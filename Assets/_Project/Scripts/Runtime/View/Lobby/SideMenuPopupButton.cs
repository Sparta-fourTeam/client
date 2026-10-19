using UnityEngine;
using UnityEngine.UI;

namespace Game.View
{
    /// <summary>로비 오른쪽 메뉴의 친구·퀘스트·우편함·도감 버튼. 누르면 해당 목록 팝업을 연다</summary>
    [RequireComponent(typeof(Button))]
    public sealed class SideMenuPopupButton : MonoBehaviour, IUiBindable
    {
        public enum Target
        {
            Friend,
            Quest,
            Mail,
            Codex,
        }

        [SerializeField] private Target _target;

        private LobbyListPopupView _popup;

        public void BindUi(IUiRegistry registry)
        {
            _popup = _target switch
            {
                Target.Friend => registry.Get<FriendPopupView>(),
                Target.Quest => registry.Get<QuestPopupView>(),
                Target.Codex => registry.Get<CodexPopupView>(),
                _ => registry.Get<MailPopupView>(),
            };
        }

        private void Awake() => GetComponent<Button>().onClick.AddListener(() => _popup.Open());
    }
}
