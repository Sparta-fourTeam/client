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
    public sealed class UpgradeConfirmPopupView : HudView
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
        private UpgradePurchasePlan _plan;
        private string _name;
        private bool _all, _busy, _visible;
        public bool IsBusy => _busy;

        [Inject]
        public void Construct(PlayerProfile profile, GameDataStore data, IUpgradeApi api)
        { _profile = profile; _data = data; _api = api; }

        protected override void InitializeView()
        {
            _panel.SetActive(false);
            _closeButton.onClick.AddListener(Close);
            _cancelButton.onClick.AddListener(Close);
            _confirmButton.onClick.AddListener(() => ConfirmAsync().Forget(Debug.LogException));
        }

        public void Show(string name, string id, bool all)
        {
            if (_busy || string.IsNullOrEmpty(id))
            {
                return;
            }

            _name = name; _all = all; _visible = true;
            Present(UpgradePurchasePlan.Create(_profile, _data, id, all));
            PopupPanel.Set(_panel, _transition, true);
            PopupPanel.FitContent(_scroll, 560f, 1040f);
            _scroll.verticalNormalizedPosition = 1f;
        }

        private void Present(UpgradePurchasePlan plan, string status = null)
        {
            _plan = plan;
            _title.text = _all ? "장비 일괄 강화" : "강화 확인";
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
            _confirmButton.interactable = !_busy && _plan.Count > 0;
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
            if (_busy || !_visible || !_panel.activeSelf || _plan.Count == 0)
            {
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
                if (this != null) { _visible = false; PopupPanel.Set(_panel, _transition, false); }
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
    }
}
