using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Game.View
{
    public sealed class UpgradeConfirmPopupView : HudView, IUiBindable
    {
        [SerializeField] private GameObject _panel;
        [SerializeField] private PopupTransition _transition;
        [SerializeField] private TMP_Text _title, _description;
        [SerializeField] private Button _closeButton, _cancelButton, _confirmButton;
        [SerializeField] private ScrollRect _scroll;
        [SerializeField] private ResultRewardListView _rewards;
        [SerializeField] private ItemIconTable _itemIcons;
        private PlayerProfile _profile;
        private GameDataStore _data;
        private IUpgradeApi _api;
        private IUpgradeRecovery _recovery;
        private IAudioService _audio;
        private UpgradePurchasePlan _plan;
        private string _name;
        private bool _all, _busy, _visible;
        private ComingSoonToastView _toast;
        public void BindUi(IUiRegistry registry) => _toast = registry.Get<ComingSoonToastView>();
        public bool IsBusy => _busy;
        public bool IsOpen => _visible;
        public bool HasPendingRecovery => _recovery?.HasPendingPurchase == true;
        public RectTransform ConfirmButton => (RectTransform)_confirmButton.transform;

        [Inject]
        public void Construct(PlayerProfile profile, GameDataStore data, IUpgradeApi api, IAudioService audio = null)
        { _profile = profile; _data = data; _api = api; _recovery = api as IUpgradeRecovery; _audio = audio; }

        protected override void InitializeView()
        {
            _panel.SetActive(false);
            _closeButton.onClick.AddListener(Close);
            _cancelButton.onClick.AddListener(Close);
            _confirmButton.onClick.AddListener(() => ConfirmAsync().Forget(Debug.LogException));
        }

        public void Show(string name, string id, bool all)
        {
            if (_busy || (!HasPendingRecovery && string.IsNullOrEmpty(id)))
            {
                return;
            }

            if (HasPendingRecovery)
            {
                id = _recovery.PendingUpgradeId;
                name = id;
                all = false; // 미확정 구매 한 건을 먼저 복구한다. 남은 일괄 강화는 복구 뒤에 다시 시작한다.
            }

            _name = name; _all = all; _visible = true;
            Present(UpgradePurchasePlan.Create(_profile, _data, id, all),
                HasPendingRecovery ? L10n.Get("upgrade.confirm.pending") : null);
            PopupPanel.Set(_panel, _transition, true);
            PopupPanel.FitContent(_scroll, 560f, 1040f);
            _scroll.verticalNormalizedPosition = 1f;
        }

        private void Present(UpgradePurchasePlan plan, string status = null)
        {
            _plan = plan;
            _title.text = _all ? L10n.Get("skill.levelUpAll") : L10n.Get("upgrade.confirm.title");
            _description.text = status ?? (plan.Count > 0
                ? L10n.Get("upgrade.confirm.plan", _name, plan.StartLevel, plan.EndLevel, plan.Count)
                : L10n.Get("upgrade.confirm.unavailable"));
            var items = new List<ItemAmount>();
            if (plan.MaterialCost > 0)
            {
                items.Add(new ItemAmount { itemId = plan.MaterialItemId, quantity = plan.MaterialCost });
            }

            _rewards.Show(RewardRows.Build(_itemIcons, _data, plan.CoinCost, 0, items));
            SetButtons();
        }

        private void SetButtons()
        {
            _closeButton.interactable = _cancelButton.interactable = !_busy;
            _confirmButton.interactable = !_busy && (_plan.Count > 0 || HasPendingRecovery);
        }

        private void Close()
        {
            if (_busy)
            {
                return;
            }

            _visible = false;
            PopupPanel.Set(_panel, _transition, false);
        }

        private async UniTask ConfirmAsync()
        {
            if (_busy || !_visible || !_panel.activeSelf || (_plan.Count == 0 && !HasPendingRecovery))
            {
                return;
            }

            if (_recovery != null)
            {
                await ConfirmServerAsync();
                return;
            }

            var current = UpgradePurchasePlan.Create(_profile, _data, _plan.UpgradeId, _all);
            if (!_plan.SameQuote(current))
            {
                Present(current, L10n.Get("upgrade.confirm.changed", _name, current.StartLevel, current.EndLevel, current.Count));
                return;
            }
            var approved = _plan;
            _busy = true; SetButtons();
            _description.text = L10n.Get("upgrade.confirm.progress", _name, 0, approved.Count);
            int completed = 0, spentCoins = 0, spentMaterials = 0;
            try
            {
                for (int i = 0; i < approved.Count; i++)
                {
                    if (this == null)
                    {
                        return;
                    }

                    var next = UpgradePurchasePlan.Create(_profile, _data, approved.UpgradeId, false);
                    if (next.Count != 1 || next.StartLevel != approved.StartLevel + i
                        || next.CoinCost > approved.CoinCost - spentCoins || next.MaterialCost > approved.MaterialCost - spentMaterials)
                    {
                        throw new InvalidOperationException(L10n.Get("upgrade.confirm.midChanged"));
                    }

                    var snapshot = await _api.Purchase(approved.UpgradeId, Guid.NewGuid().ToString("N"));
                    _profile.Apply(snapshot);
                    completed++; spentCoins += next.CoinCost; spentMaterials += next.MaterialCost;
                    if (this == null)
                    {
                        return;
                    }

                    if (_profile.UpgradeLevel(approved.UpgradeId) != approved.StartLevel + completed)
                    {
                        throw new InvalidOperationException(L10n.Get("upgrade.confirm.levelMismatch"));
                    }

                    _description.text = L10n.Get("upgrade.confirm.progress", _name, completed, approved.Count);
                }
                if (this != null)
                {
                    _audio?.PlaySfx(SoundIds.Upgrade);
                    _visible = false;
                    PopupPanel.Set(_panel, _transition, false);
                    _toast?.Show(L10n.Get("upgrade.confirm.done", _name, _profile.UpgradeLevel(approved.UpgradeId)), 1f);
                }
            }
            catch (Exception error)
            {
                if (this != null)
                {
                    var updated = UpgradePurchasePlan.Create(_profile, _data, approved.UpgradeId, _all);
                    string reason = error is ApiException ? L10n.Get("upgrade.confirm.requestFailed") : error.Message;
                    Present(updated, L10n.Get("upgrade.confirm.partialRetry", completed, reason, updated.StartLevel, updated.EndLevel, updated.Count));
                }
            }
            finally
            {
                _busy = false;
                if (this != null)
                {
                    SetButtons();
                }
            }
        }

        private async UniTask ConfirmServerAsync()
        {
            if (!HasPendingRecovery)
            {
                var current = UpgradePurchasePlan.Create(_profile, _data, _plan.UpgradeId, _all);
                if (!_plan.SameQuote(current))
                { Present(current, L10n.Get("upgrade.confirm.stateChanged")); return; }
            }
            var approved = _plan;
            bool recovering = HasPendingRecovery;
            _busy = true; SetButtons();
            int completed = 0;
            try
            {
                if (recovering)
                {
                    _profile.Apply(await _recovery.RecoverPurchase());
                }
                else
                {
                    // 일괄 강화도 서버에는 한 건씩 보낸다. 한 건이 끝나 서버 상태를 받은 뒤에 다음 건을 새 요청 ID로 보낸다
                    for (int i = 0; i < approved.Count; i++)
                    {
                        if (this == null) { return; }
                        if (_all) { _description.text = L10n.Get("upgrade.confirm.progress", _name, completed, approved.Count); }
                        _profile.Apply(await _api.Purchase(approved.UpgradeId, Guid.NewGuid().ToString("N")));
                        completed++;
                        if (this == null) { return; }
                        if (_profile.UpgradeLevel(approved.UpgradeId) != approved.StartLevel + completed)
                        { throw new InvalidOperationException(L10n.Get("upgrade.confirm.levelMismatch")); }
                    }
                }

                if (this == null) { return; }
                _audio?.PlaySfx(SoundIds.Upgrade);
                _visible = false;
                PopupPanel.Set(_panel, _transition, false);
                _toast?.Show(_all && !recovering ? L10n.Get("upgrade.confirm.done", _name, _profile.UpgradeLevel(approved.UpgradeId))
                    : L10n.Get("upgrade.confirm.verified"), 1f);
            }
            catch (Exception error)
            {
                if (this == null) { return; }
                string status = HasPendingRecovery
                    ? L10n.Get("upgrade.confirm.unconfirmed")
                    : error is ApiException api ? L10n.Get("upgrade.confirm.requestError", completed > 0 ? L10n.Get("upgrade.confirm.partialDone", completed) : string.Empty, api.Code) : error.Message;
                // 미확정 구매는 한 건씩 복구하므로 이후 견적과 제목도 한 건 기준으로 맞춘다
                if (HasPendingRecovery) { _all = false; }
                Present(UpgradePurchasePlan.Create(_profile, _data, _plan.UpgradeId, _all), status);
            }
            finally
            {
                _busy = false;
                if (this != null) { SetButtons(); }
            }
        }

    }
}
