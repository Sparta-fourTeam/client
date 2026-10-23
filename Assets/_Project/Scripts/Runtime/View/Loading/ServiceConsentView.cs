using Cysharp.Threading.Tasks;
using Game.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.View
{
    /// <summary>선택 분석 안내 후 약관을 확인한다. 전체 동의와 거절, 항목별 선택을 명시적으로 구분한다.</summary>
    public sealed class ServiceConsentView : HudView
    {
        [SerializeField] private Toggle _analyticsToggle, _transferToggle;
        [SerializeField] private Button _startButton, _termsLink, _privacyLink;
        [SerializeField] private TMP_Text _startLabel;
        [SerializeField] private GameObject _analyticsPanel, _termsPanel, _detailsPanel;
        [SerializeField] private Toggle _termsToggle;
        [SerializeField] private Button _analyticsDetails, _transferDetails, _detailAccept, _detailCancel, _backButton;
        [SerializeField] private TMP_Text _title, _detailTitle, _detailAcceptLabel;
        [SerializeField] private TMP_Text _analyticsNotice, _transferNotice;
        [SerializeField] private ScrollRect _detailsScroll, _termsScroll;
        [SerializeField] private Button _declineButton, _individualButton;
        [SerializeField] private TMP_Text _individualLabel;
        private UniTaskCompletionSource<LoginConsent> _pending;
        private bool _editing, _showingTerms;
        private Toggle _requestedChoice;

        protected override void InitializeView()
        {
            gameObject.SetActive(false);
            _startButton.onClick.AddListener(AgreeAllOrConfirmTerms);
            _declineButton.onClick.AddListener(DeclineAll);
            _individualButton.onClick.AddListener(Complete);
            _termsLink.onClick.AddListener(() => Open(ExternalLinks.TermsOfUse));
            _privacyLink.onClick.AddListener(() => Open(ExternalLinks.PrivacyPolicy));
            _termsToggle.onValueChanged.AddListener(_ => RefreshStart());
            _analyticsToggle.onValueChanged.AddListener(on => RequestChoice(_analyticsToggle, on));
            _transferToggle.onValueChanged.AddListener(on => RequestChoice(_transferToggle, on));
            _analyticsDetails.onClick.AddListener(() => ShowDetails(false, false));
            _transferDetails.onClick.AddListener(() => ShowDetails(true, false));
            _detailAccept.onClick.AddListener(AcceptDetails);
            _detailCancel.onClick.AddListener(CloseDetails);
            _backButton.onClick.AddListener(() => ShowPage(false));
        }

        public UniTask<LoginConsent> ShowAsync(LoginConsent? initial = null)
        {
            Initialize();
            _pending?.TrySetCanceled();
            _pending = new UniTaskCompletionSource<LoginConsent>();
            _analyticsToggle.SetIsOnWithoutNotify(initial?.AnalyticsAccepted ?? false);
            _transferToggle.SetIsOnWithoutNotify(initial?.AnalyticsTransferAccepted ?? false);
            _termsToggle.SetIsOnWithoutNotify(false);
            _editing = initial.HasValue;
            _requestedChoice = null;
            _detailsPanel.SetActive(false);
            ShowPage(false);
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            return _pending.Task.AttachExternalCancellation(this.GetCancellationTokenOnDestroy());
        }

        private void Complete()
        {
            if (_pending == null || !_startButton.interactable) { return; }
            if (!_editing && !_showingTerms) { ShowPage(true); return; }
            var result = new LoginConsent(true, _analyticsToggle.isOn, _transferToggle.isOn);
            var pending = _pending;
            _pending = null;
            gameObject.SetActive(false);
            pending.TrySetResult(result);
        }

        private void AgreeAllOrConfirmTerms()
        {
            if (_pending == null || !_startButton.interactable) { return; }
            if (!_showingTerms) { SetAllChoices(true); }
            Complete();
        }

        private void DeclineAll()
        {
            if (_pending == null || _showingTerms || !_declineButton.interactable) { return; }
            SetAllChoices(false);
            Complete();
        }

        private void SetAllChoices(bool accepted)
        {
            // 기본 선택이나 화면 표시로 승인하지 않는다. 명시적인 전체 동의/거절 버튼에만 사용한다.
            _analyticsToggle.SetIsOnWithoutNotify(accepted);
            _transferToggle.SetIsOnWithoutNotify(accepted);
        }

        private void ShowPage(bool terms)
        {
            _showingTerms = terms;
            _analyticsPanel.SetActive(!terms);
            _termsPanel.SetActive(terms);
            _backButton.gameObject.SetActive(terms);
            _declineButton.gameObject.SetActive(!terms);
            _individualButton.gameObject.SetActive(!terms);
            _title.SetL10n(() => terms ? L10n.Get("consent.terms.title") : L10n.Get("consent.analytics.title"));
            _startLabel.SetL10n(() => terms ? L10n.Get("common.ok") : L10n.Get("consent.agreeAll"));
            bool editing = _editing;
            _individualLabel.SetL10n(() => editing ? L10n.Get("consent.saveChoice") : L10n.Get("consent.continueChoice"));
            var startRect = (RectTransform)_startButton.transform;
            startRect.anchoredPosition = new Vector2(terms ? 0 : 215, startRect.anchoredPosition.y);
            startRect.sizeDelta = new Vector2(terms ? 620 : 400, startRect.sizeDelta.y);
            if (terms) { _termsScroll.verticalNormalizedPosition = 1; }
            RefreshStart();
        }

        private void RefreshStart()
        {
            bool available = !_detailsPanel.activeSelf && Available(ExternalLinks.TermsOfUse) && Available(ExternalLinks.PrivacyPolicy);
            _startButton.interactable = available && (!_showingTerms || _termsToggle.isOn);
            _declineButton.interactable = _individualButton.interactable = available;
        }

        private void RequestChoice(Toggle choice, bool on)
        {
            if (!on) { return; }
            // 선택 자체를 동의로 확정하지 않는다. 고지 화면의 별도 동의 버튼으로만 켠다.
            choice.SetIsOnWithoutNotify(false);
            ShowDetails(choice == _transferToggle, true);
        }

        private void ShowDetails(bool transfer, bool requesting)
        {
            _requestedChoice = requesting ? transfer ? _transferToggle : _analyticsToggle : null;
            _detailTitle.SetL10n(() => transfer ? L10n.Get("consent.transfer.title") : L10n.Get("consent.analytics.title"));
            _analyticsNotice.gameObject.SetActive(true);
            _transferNotice.gameObject.SetActive(transfer);
            _detailAcceptLabel.SetL10n(() => requesting ? transfer ? L10n.Get("consent.transfer.accept") : L10n.Get("consent.analytics.accept") : L10n.Get("common.ok"));
            _detailCancel.gameObject.SetActive(requesting);
            _detailsPanel.SetActive(true);
            Canvas.ForceUpdateCanvases();
            _detailsScroll.verticalNormalizedPosition = 1;
            RefreshStart();
        }

        private void AcceptDetails()
        {
            if (_requestedChoice != null) { _requestedChoice.SetIsOnWithoutNotify(true); }
            CloseDetails();
        }

        private void CloseDetails()
        {
            _requestedChoice = null;
            _detailsPanel.SetActive(false);
            RefreshStart();
        }

        private static bool Available(string url) =>
            System.Uri.TryCreate(url, System.UriKind.Absolute, out var uri) && uri.Scheme == System.Uri.UriSchemeHttps;
        private static void Open(string url) { if (Available(url)) { Application.OpenURL(url); } }
        protected override void DisposeView() { _pending?.TrySetCanceled(); _pending = null; }
    }
}
