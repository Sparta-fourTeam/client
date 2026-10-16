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
        public bool SinglePurchaseOnly => _recovery != null;
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

            if (_recovery != null && all && !_recovery.HasPendingPurchase)
            {
                _toast?.Show("서버 강화 테스트는 단건 구매만 지원합니다.", 1f);
                return;
            }
            if (_recovery?.HasPendingPurchase == true)
            {
                id = _recovery.PendingUpgradeId;
                name = id;
                all = false;
            }
            _name = name; _all = all; _visible = true;
            Present(UpgradePurchasePlan.Create(_profile, _data, id, all),
                _recovery?.HasPendingPurchase == true ? "미확정 서버 강화가 있습니다. 확인을 누르면 같은 구매를 복구합니다." : null);
            PopupPanel.Set(_panel, _transition, true);
            PopupPanel.FitContent(_scroll, 560f, 1040f);
            _scroll.verticalNormalizedPosition = 1f;
        }

        private void Present(UpgradePurchasePlan plan, string status = null)
        {
            _plan = plan;
            _title.text = _all ? "일괄 레벨업" : "강화 확인";
            _description.text = status ?? (plan.Count > 0
                ? $"{_name}\nLv.{plan.StartLevel} → Lv.{plan.EndLevel} · {plan.Count}회 강화\n아래 재화를 소모해 강화할까요?"
                : "지금은 강화할 수 없어요. 재화와 최대 레벨을 확인해 주세요.");
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
            _confirmButton.interactable = !_busy && (_plan.Count > 0 || _recovery?.HasPendingPurchase == true);
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
            if (_busy || !_visible || !_panel.activeSelf || (_plan.Count == 0 && _recovery?.HasPendingPurchase != true))
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
                Present(current, "재화 또는 레벨이 변경됐어요. 변경된 비용을 확인한 뒤 다시 확인을 눌러 주세요.\n"
                    + $"{_name} · Lv.{current.StartLevel} → Lv.{current.EndLevel} · {current.Count}회");
                return;
            }
            var approved = _plan;
            _busy = true; SetButtons();
            _description.text = $"{_name} 강화 중 · 0/{approved.Count}";
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
                        throw new InvalidOperationException("강화 도중 재화 또는 레벨이 변경됐어요.");
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
                        throw new InvalidOperationException("강화 결과가 예상 레벨과 달라요. 최신 상태를 확인해 주세요.");
                    }

                    _description.text = $"{_name} 강화 중 · {completed}/{approved.Count}";
                }
                if (this != null)
                {
                    _audio?.PlaySfx(SoundIds.Upgrade);
                    _visible = false;
                    PopupPanel.Set(_panel, _transition, false);
                    _toast?.Show($"<color=#FFCF5B>{_name} Lv.{_profile.UpgradeLevel(approved.UpgradeId)} 강화 완료!</color>", 1f);
                }
            }
            catch (Exception error)
            {
                if (this != null)
                {
                    var updated = UpgradePurchasePlan.Create(_profile, _data, approved.UpgradeId, _all);
                    string reason = error is ApiException ? "강화 요청을 완료하지 못했어요." : error.Message;
                    Present(updated, $"{completed}회 강화 완료. {reason}\n남은 강화와 비용을 확인한 뒤 다시 시도해 주세요.\n"
                        + $"Lv.{updated.StartLevel} → Lv.{updated.EndLevel} · {updated.Count}회");
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
            if (!_recovery.HasPendingPurchase)
            {
                var current = UpgradePurchasePlan.Create(_profile, _data, _plan.UpgradeId, false);
                if (!_plan.SameQuote(current)) { Present(current, "상태가 변경됐습니다. 단건 비용을 다시 확인해 주세요."); return; }
            }
            _busy = true; SetButtons();
            _description.text = "서버 강화 상태를 확인하고 있습니다.";
            try
            {
                var snapshot = _recovery.HasPendingPurchase ? await _recovery.RecoverPurchase()
                    : await _api.Purchase(_plan.UpgradeId, Guid.NewGuid().ToString("N"));
                _profile.Apply(snapshot);
                if (this == null) { return; }
                _visible = false;
                PopupPanel.Set(_panel, _transition, false);
                _toast?.Show("서버 강화 결과와 현재 상태를 확인했습니다.", 1f);
            }
            catch (Exception error)
            {
                if (this == null) { return; }
                string status = _recovery.HasPendingPurchase
                    ? "구매 결과 또는 상태 조회가 미확정입니다. 확인을 눌러 같은 구매를 복구해 주세요. 새 구매는 잠겨 있습니다."
                    : "구매하지 못했습니다. 서버와 표시된 비용·해금 조건이 같은지 확인해 주세요.";
                if (error is ApiException sessionError && sessionError.Code == "SESSION_EXPIRED")
                { status = "로그인 상태가 바뀌었습니다. 다시 로그인한 뒤 서버의 강화 상태를 확인해 주세요."; }
                Present(UpgradePurchasePlan.Create(_profile, _data, _plan.UpgradeId, false), status);
                Debug.LogWarning($"서버 강화 요청: {(error is ApiException api ? api.Code : error.GetType().Name)}");
            }
            finally
            {
                _busy = false;
                if (this != null) { SetButtons(); }
            }
        }

    }
}
