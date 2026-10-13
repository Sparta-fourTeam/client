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
        public string Title;
        public string Message;
        /// <summary>메시지 아래 빨간 글씨 경고. 없으면 숨긴다</summary>
        public string Warning;
        public string ConfirmLabel = "확인";
        public string CancelLabel;
        public string InputPlaceholder;
        public string RequiredInput;
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

        private UniTaskCompletionSource<bool> _result;
        private ConfirmRequest _request;
        private float _confirmX;

        public bool IsOpen => _panel.activeSelf;

        protected override void InitializeView()
        {
            _panel.SetActive(false);
            _closeButton.onClick.AddListener(() => Finish(false));
            _cancelButton.onClick.AddListener(() => Finish(false));
            _confirmButton.onClick.AddListener(() => Finish(true));
            _input.onValueChanged.AddListener(_ => RefreshConfirm());
            _confirmX = ((RectTransform)_confirmButton.transform).anchoredPosition.x;
        }

        public UniTask<bool> ShowAsync(ConfirmRequest request)
        {
            // 이미 떠 있으면 앞의 요청은 취소로 끝낸다
            _result?.TrySetResult(false);
            _result = new UniTaskCompletionSource<bool>();
            _request = request;

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

            bool needsInput = !string.IsNullOrEmpty(request.RequiredInput);
            _input.gameObject.SetActive(needsInput);
            _input.SetTextWithoutNotify(string.Empty);
            ((TMP_Text)_input.placeholder).text = request.InputPlaceholder;

            // 경고·입력창이 없으면 창을 줄여 빈 공간을 없앤다
            var window = (RectTransform)_confirmButton.transform.parent;
            float height = needsInput ? InputHeight : _warning.gameObject.activeSelf ? WarningHeight : PlainHeight;
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

            _confirmButton.interactable = string.IsNullOrEmpty(_request.RequiredInput) || _input.text == _request.RequiredInput;
        }

        private void Finish(bool confirmed)
        {
            PopupPanel.Set(_panel, _transition, false);
            _result?.TrySetResult(confirmed);
            _result = null;
        }
    }
}
