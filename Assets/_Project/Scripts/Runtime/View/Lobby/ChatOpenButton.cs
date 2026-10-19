using Game.Core;
using MessagePipe;
using TMPro;
using UnityEngine;
using VContainer;

namespace Game.View
{
    public sealed class ChatOpenButton : HudView, IUiBindable
    {
        [SerializeField] private UnityEngine.UI.Button _button;
        [SerializeField] private GameObject _badge;
        [SerializeField] private TMP_Text _count;
        private ChatView _chat;

        public void BindUi(IUiRegistry registry) => _chat = registry.Get<ChatView>();
        protected override void InitializeView()
        {
            _badge.SetActive(false);
            _button.onClick.AddListener(() => _chat.Open());
        }
        [Inject]
        public void Construct(ChatService chat, IBufferedSubscriber<ChatChanged> changed)
        {
            Track(changed.Subscribe(_ =>
            {
                _badge.SetActive(chat.UnreadCount > 0);
                _count.text = chat.UnreadCount > 99 ? "99+" : chat.UnreadCount.ToString();
            }));
        }
    }
}
