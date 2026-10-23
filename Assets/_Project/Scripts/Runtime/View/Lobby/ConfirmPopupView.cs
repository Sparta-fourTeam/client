using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.View
{
    /// <summary>확인 팝업이 보여줄 내용. 취소 문구가 없으면 확인 버튼 하나뿐인 안내창이 된다.
    /// RequiredInput이 있으면 입력창이 나타나고, 입력이 그 값과 같아야 확인을 누를 수 있다</summary>
    public sealed class ConfirmRequest
    {
        public bool CannotDismiss;
        public bool ConfirmEnabled = true;
        public string Title;
        public string Message;
        /// <summary>메시지 아래 빨간 글씨 경고. 없으면 숨긴다</summary>
        public string Warning;
        public string ConfirmLabel = "확인";
        public string CancelLabel;
        public string InputPlaceholder;
        public string RequiredInput;
        /// <summary>자유 입력의 유효성 검사. RequiredInput이 있으면 기존 삭제 확인처럼 완전 일치를 우선한다</summary>
        public System.Func<string, bool> InputValidator;
        public string InputValue;
        public string InitialInput;
    }

    /// <summary>로비의 공용 확인·안내 팝업. 로그아웃·계정 삭제 확인과 안내 문구에 쓴다.
    /// ShowAsync는 확인을 누르면 true, 취소·닫기·바깥을 누르면 false를 돌려준다</summary>
    public sealed class ConfirmPopupView : HudView
    {
        [SerializeField] private GameObject _panel;
        [SerializeField] private PopupTransition _transition;
        [SerializeField] private TMP_Text _title, _message, _warning;
        [SerializeField] private TMP_InputField _input;
        [SerializeField] private Button _closeButton, _cancelButton, _confirmButton;
        [SerializeField] private TMP_Text _cancelLabel, _confirmLabel;

        private const float PlainHeight = 420f, WarningHeight = 500f, InputHeight = 600f;
        // 메시지가 기본 칸보다 길면 그만큼 창을 키운다. 이 높이를 넘는 글은 말줄임으로 자른다
        private const float MessageMaxHeight = 900f;

        private UniTaskCompletionSource<bool> _result;
        private ConfirmRequest _request;
        private float _confirmX;
        private float _messageBaseHeight, _warningGap;

        public bool IsOpen => _panel.activeSelf;

        protected override void InitializeView()
        {
            _panel.SetActive(false);
            _closeButton.onClick.AddListener(() => Finish(false));
            _cancelButton.onClick.AddListener(() => Finish(false));
            _confirmButton.onClick.AddListener(() => Finish(true));
            _input.onValueChanged.AddListener(_ => RefreshConfirm());
            _confirmX = ((RectTransform)_confirmButton.transform).anchoredPosition.x;
            var messageRect = _message.rectTransform;
            _messageBaseHeight = messageRect.sizeDelta.y;
            _warningGap = -_warning.rectTransform.anchoredPosition.y + messageRect.anchoredPosition.y - _messageBaseHeight;
            _message.overflowMode = TextOverflowModes.Ellipsis;
        }

        public UniTask<bool> ShowAsync(ConfirmRequest request)
        {
            if (_result != null && _request?.CannotDismiss == true)
            { return UniTask.FromResult(false); }
            // 이미 떠 있으면 앞의 요청은 취소로 끝낸다
            _result?.TrySetResult(false);
            _result = new UniTaskCompletionSource<bool>();
            _request = request;
            _closeButton.gameObject.SetActive(!request.CannotDismiss);

            _title.text = request.Title;
            _message.text = request.Message;
            _warning.gameObject.SetActive(!string.IsNullOrEmpty(request.Warning));
            _warning.text = request.Warning;
            _confirmLabel.text = request.ConfirmLabel;

            bool single = string.IsNullOrEmpty(request.CancelLabel);
            _cancelButton.gameObject.SetActive(!single);
            _cancelLabel.text = request.CancelLabel;
            ((RectTransform)_confirmButton.transform).anchoredPosition =
                new Vector2(single ? 0f : _confirmX, ((RectTransform)_confirmButton.transform).anchoredPosition.y);

            bool needsInput = !string.IsNullOrEmpty(request.RequiredInput) || request.InputValidator != null;
            request.InputValue = null;
            _input.gameObject.SetActive(needsInput);
            _input.SetTextWithoutNotify(request.InitialInput ?? string.Empty);
            ((TMP_Text)_input.placeholder).text = request.InputPlaceholder;

            // 긴 메시지는 칸을 키우고 그 아래 경고를 밀어 내린다. 짧으면 기본 칸 그대로라 창 높이가 변하지 않는다
            var messageRect = _message.rectTransform;
            float messageHeight = Mathf.Clamp(_message.GetPreferredValues(request.Message ?? string.Empty, messageRect.rect.width, 0f).y,
                _messageBaseHeight, MessageMaxHeight);
            messageRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, messageHeight);
            _warning.rectTransform.anchoredPosition = new Vector2(_warning.rectTransform.anchoredPosition.x,
                messageRect.anchoredPosition.y - messageHeight - _warningGap);

            // 경고·입력창이 없으면 창을 줄여 빈 공간을 없앤다
            var window = (RectTransform)_confirmButton.transform.parent;
            float height = (needsInput ? InputHeight : _warning.gameObject.activeSelf ? WarningHeight : PlainHeight) + (messageHeight - _messageBaseHeight);
            window.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);

            RefreshConfirm();
            PopupPanel.Set(_panel, _transition, true);
            return _result.Task;
        }

        private void RefreshConfirm()
        {
            if (_request == null)
            {
                return;
            }

            _confirmButton.interactable = _request.ConfirmEnabled && (!string.IsNullOrEmpty(_request.RequiredInput)
                ? _input.text == _request.RequiredInput
                : _request.InputValidator?.Invoke(_input.text) ?? true);
        }

        /// <summary>같은 요청의 취소·foreground 재검사만 닫는다. 이미 바뀐 팝업은 건드리지 않는다.</summary>
        public void Cancel(ConfirmRequest request)
        {
            if (ReferenceEquals(_request, request)) { Finish(false, true); }
        }

        private void Finish(bool confirmed, bool force = false)
        {
            if (!confirmed && !force && _request?.CannotDismiss == true) { return; }
            if (confirmed && !_confirmButton.interactable) { return; }
            if (confirmed) { _request.InputValue = _input.text; }
            var result = _result;
            _result = null;
            PopupPanel.Set(_panel, _transition, false);
            // 대기자가 동기적으로 다음 확인창을 열어도 새 완료소스를 지우지 않는다
            result?.TrySetResult(confirmed);
        }
    }
}
