using Cysharp.Threading.Tasks;
using Game.Core;
using MessagePipe;
using TMPro;
using UnityEngine;
using VContainer;

namespace Game.View
{
    public sealed class ChatView : HudView
    {
        [SerializeField] private GameObject _panel;
        [SerializeField] private RectTransform _window;
        [SerializeField] private UnityEngine.UI.Button _closeButton, _sendButton, _retryButton, _emojiButton;
        [SerializeField] private TMP_Text _status, _presence;
        [SerializeField] private TMP_InputField _input;
        [SerializeField] private UnityEngine.UI.ScrollRect _scroll;
        [SerializeField] private RectTransform _content;
        [SerializeField] private ChatMessageRow _rowTemplate;
        private ChatService _chat;
        private bool _sending;
        private ChatMessage _lastMessage;
        private int _shownCount = -1;
        private Vector2 _windowSize;
        private float _keyboardHeight;
        private Canvas _canvas;
        [SerializeField] private GameObject _emojiPanel;
        [SerializeField] private UnityEngine.UI.Button[] _emojiChoices;
        private Vector2 _inputPosition, _sendPosition;
        private Vector2 _retryPosition, _statusPosition;
        private Vector2 _scrollOffset;

        [Inject]
        public void Construct(ChatService chat, IBufferedSubscriber<ChatChanged> changed)
        {
            _chat = chat;
            Track(changed.Subscribe(_ => Refresh()));
            Refresh();
        }

        protected override void InitializeView()
        {
            _windowSize = _window.sizeDelta;
            _canvas = _window.GetComponentInParent<Canvas>()?.rootCanvas;
            _inputPosition = _input.GetComponent<RectTransform>().anchoredPosition;
            _sendPosition = _sendButton.GetComponent<RectTransform>().anchoredPosition;
            _retryPosition = _retryButton.GetComponent<RectTransform>().anchoredPosition;
            _statusPosition = _status.rectTransform.anchoredPosition;
            _scrollOffset = ((RectTransform)_scroll.transform).offsetMin;
            _emojiPanel.SetActive(false);
            _panel.SetActive(false);
            _rowTemplate.gameObject.SetActive(false);
            _closeButton.onClick.AddListener(Close);
            _emojiButton.onClick.AddListener(() => ToggleEmoji(!_emojiPanel.activeSelf));
            for (int i = 0; i < _emojiChoices.Length; i++)
            {
                int index = i;
                _emojiChoices[i].onClick.AddListener(() =>
                {
                    _input.text = _input.text.Insert(UnityEngine.Mathf.Clamp(_input.stringPosition, 0, _input.text.Length), ChatEmojiPalette.Values[index]);
                    _input.stringPosition = _input.text.Length;
                    RefreshSend();
                });
            }
            _retryButton.onClick.AddListener(() => _chat?.Retry());
            _sendButton.onClick.AddListener(() => SendAsync().Forget());
            _input.onValueChanged.AddListener(_ => RefreshSend());
            _input.onSubmit.AddListener(_ => SendAsync().Forget());
        }

        private void Update()
        {
            float height = 0;
            if (_panel.activeSelf && TouchScreenKeyboard.visible)
            {
                float screenHeight = TouchScreenKeyboard.area.height;
                if (screenHeight <= 0) { screenHeight = Screen.height * 0.4f; }
                if (_canvas == null) { _canvas = _window.GetComponentInParent<Canvas>()?.rootCanvas; }
                if (_canvas != null) { height = screenHeight / _canvas.scaleFactor; }
            }
            if (Mathf.Approximately(height, _keyboardHeight)) { return; }
            float distance = DistanceFromBottom();
            _keyboardHeight = height;
            float available = ((RectTransform)_panel.transform).rect.height - height - 80;
            _window.sizeDelta = new Vector2(_windowSize.x, Mathf.Min(_windowSize.y, Mathf.Max(650, available)));
            _window.anchoredPosition = new Vector2(0, height * 0.5f);
            RestoreDistanceFromBottom(distance);
        }

        public void Open()
        {
            _shownCount = -1;
            _panel.SetActive(true);
            _chat?.SetOpen(true);
            Refresh();
        }

        public void Close()
        {
            ToggleEmoji(false);
            _panel.SetActive(false);
            _chat?.SetOpen(false);
        }

        public void ToggleEmoji(bool open)
        {
            float distance = DistanceFromBottom();
            _emojiPanel.SetActive(open);
            float lift = open ? 330 : 0;
            _input.GetComponent<RectTransform>().anchoredPosition = _inputPosition + new Vector2(0, lift);
            _sendButton.GetComponent<RectTransform>().anchoredPosition = _sendPosition + new Vector2(0, lift);
            _retryButton.GetComponent<RectTransform>().anchoredPosition = _retryPosition + new Vector2(0, lift);
            _status.rectTransform.anchoredPosition = _statusPosition + new Vector2(0, lift);
            ((RectTransform)_scroll.transform).offsetMin = _scrollOffset + new Vector2(0, lift);
            RestoreDistanceFromBottom(distance);
        }

        private float DistanceFromBottom()
        {
            Canvas.ForceUpdateCanvases();
            return Mathf.Max(0, _content.rect.height - _scroll.viewport.rect.height - _content.anchoredPosition.y);
        }

        private void RestoreDistanceFromBottom(float distance)
        {
            Canvas.ForceUpdateCanvases();
            _scroll.StopMovement();
            float overflow = Mathf.Max(0, _content.rect.height - _scroll.viewport.rect.height);
            var position = _content.anchoredPosition;
            position.y = Mathf.Clamp(overflow - distance, 0, overflow);
            _content.anchoredPosition = position;
        }

        private async UniTask SendAsync()
        {
            if (_chat == null || _sending) { return; }
            string draft = _input.text;
            _sending = true;
            RefreshSend();
            try
            {
                bool sent = await _chat.SendAsync(draft);
                if (this == null) { return; }
                if (sent && _input.text == draft) { _input.text = ""; }
            }
            finally
            {
                _sending = false;
                if (this != null) { RefreshSend(); }
            }
        }

        private void RefreshSend() => _sendButton.interactable = !_sending && _chat != null
            && _chat.IsConnected && !string.IsNullOrWhiteSpace(_input.text);

        private void Refresh()
        {
            if (_chat == null) { return; }
            _status.text = _chat.Status;
            _status.gameObject.SetActive(!_chat.IsConnected);
            _presence.text = _chat.OnlineCount.HasValue ? $"접속자 {_chat.OnlineCount.Value:N0}명"
                : _chat.IsConnected ? "접속자 확인 중" : "";
            _retryButton.gameObject.SetActive(!_chat.IsConnected);
            RefreshSend();
            if (!_panel.activeSelf) { return; }
            var messages = _chat.Messages;
            var last = messages.Count == 0 ? null : messages[messages.Count - 1];
            if (_shownCount == messages.Count && ReferenceEquals(_lastMessage, last)) { return; }
            bool follow = _shownCount < 0 || _scroll.verticalNormalizedPosition < 0.05f;
            for (int i = _content.childCount - 1; i >= 0; i--)
            {
                var child = _content.GetChild(i).gameObject;
                if (child != _rowTemplate.gameObject) { child.SetActive(false); Destroy(child); }
            }
            foreach (var message in messages)
            {
                var row = Instantiate(_rowTemplate, _content);
                row.Bind(message);
                row.gameObject.SetActive(true);
            }
            _shownCount = messages.Count;
            _lastMessage = last;
            Canvas.ForceUpdateCanvases();
            if (follow) { _scroll.verticalNormalizedPosition = 0; }
        }
    }
}
