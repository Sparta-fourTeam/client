using Game.Core;
using TMPro;
using UnityEngine;

namespace Game.View
{
    /// <summary>말풍선을 본문 길이에 맞추고 시간은 목록 오른쪽에 고정한다.</summary>
    public sealed class ChatMessageRow : MonoBehaviour, UnityEngine.UI.ILayoutElement
    {
        [SerializeField] private TMP_Text _sender, _body, _time;
        [SerializeField] private UnityEngine.UI.RawImage _portrait;
        [SerializeField] private RectTransform _bubble;
        private RectTransform Rect => (RectTransform)transform;
        private float _height = 188;
        private float _textFontSize;

        public void Bind(ChatMessage message)
        {
            _sender.richText = false;
            _sender.text = string.IsNullOrWhiteSpace(message.Nickname) ? L10n.Get("chat.defaultSender", message.SenderId) : message.Nickname;
            _body.richText = false;
            if (_textFontSize <= 0) { _textFontSize = _body.fontSize; }
            string content = message.Content.Trim();
            bool singleEmoji = System.Array.Exists(ChatEmojiPalette.Values,
                emoji => content == emoji || content == emoji + "\uFE0F");
            _body.fontSize = singleEmoji ? _textFontSize * 2.6f : _textFontSize;
            _body.text = message.Content;
            _time.text = message.SentAt.ToLocalTime().ToString("HH:mm");
            // 확장 프로필 필드가 없는 서버 메시지도 기본 초상으로 표시한다.
            SetPortrait(ProfilePortraits.IsValid(message.AvatarId) ? message.AvatarId : (int)(message.SenderId % ProfilePortraits.Count));
            UnityEngine.UI.LayoutRebuilder.MarkLayoutForRebuild(Rect);
        }

        public void SetPortrait(int index)
        {
            _portrait.uvRect = PortraitArt.Uv(index);
        }

        public void CalculateLayoutInputHorizontal() { }
        public void CalculateLayoutInputVertical()
        {
            float available = Mathf.Max(100, Rect.rect.width - 296);
            var preferred = _body.GetPreferredValues(_body.text, available - 60, float.PositiveInfinity);
            float width = Mathf.Min(available, preferred.x + 60);
            float height = Mathf.Max(84, preferred.y + 24);
            _bubble.sizeDelta = new Vector2(width, height);
            _height = Mathf.Max(188, height + 82);
        }
        public float maxWidth => -1;
        public float maxHeight => -1;
        public float minWidth => 0;
        public float preferredWidth => -1;
        public float flexibleWidth => 1;
        public float minHeight => _height;
        public float preferredHeight => _height;
        public float flexibleHeight => 0;
        public int layoutPriority => 1;
    }
}
