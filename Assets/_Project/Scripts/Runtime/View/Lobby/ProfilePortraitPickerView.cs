using System;
using Cysharp.Threading.Tasks;
using Game.Core;
using TMPro;
using UnityEngine;
using VContainer;

namespace Game.View
{
    public sealed class ProfilePortraitPickerView : HudView
    {
        [SerializeField] private GameObject _panel;
        [SerializeField] private UnityEngine.UI.Button _closeButton;
        [SerializeField] private UnityEngine.UI.Button[] _choices;
        [SerializeField] private GameObject[] _selected;
        [SerializeField] private TMP_Text _status;
        private AccountService _account;
        private PlayerProfile _profile;
        private bool _busy;

        [Inject]
        public void Construct(AccountService account, PlayerProfile profile)
        { _account = account; _profile = profile; }
        protected override void InitializeView()
        {
            _panel.SetActive(false);
            _closeButton.onClick.AddListener(() => _panel.SetActive(false));
            for (int i = 0; i < _choices.Length; i++)
            {
                int id = i;
                _choices[i].onClick.AddListener(() => ChooseAsync(id).Forget());
            }
        }
        public void Open()
        {
            _status.text = "사용할 초상을 선택해 주세요.";
            _panel.SetActive(true);
            Refresh();
        }
        private void Refresh()
        {
            for (int i = 0; i < _choices.Length; i++)
            {
                _choices[i].interactable = !_busy && _account != null && _profile?.IsLoaded == true;
                _selected[i].SetActive(_profile != null && _profile.AvatarId == i);
            }
        }
        private async UniTask ChooseAsync(int id)
        {
            if (_busy || _account == null || _profile?.IsLoaded != true) { return; }
            _busy = true;
            _status.text = "저장 중…";
            Refresh();
            try
            {
                await _account.ChangeAvatarAsync(id);
                if (this != null) { _panel.SetActive(false); }
            }
            catch (Exception)
            {
                if (this != null) { _status.text = "저장하지 못했습니다. 다시 선택해 주세요."; }
            }
            finally
            {
                _busy = false;
                if (this != null) { Refresh(); }
            }
        }
    }
}
