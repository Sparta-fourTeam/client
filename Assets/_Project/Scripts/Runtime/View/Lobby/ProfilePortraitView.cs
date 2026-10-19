using Game.Core;
using UnityEngine;
using VContainer;

namespace Game.View
{
    public static class PortraitArt
    {
        public static Rect Uv(int id)
        {
            id = ProfilePortraits.IsValid(id) ? id : 0;
            return new Rect((id % 3 + .0625f) / 3f, (1 - id / 3 + .0625f) / 2f, .875f / 3f, .875f / 2f);
        }
    }

    public sealed class ProfilePortraitView : HudView, IUiBindable
    {
        [SerializeField] private UnityEngine.UI.RawImage _portrait;
        [SerializeField] private UnityEngine.UI.Button _button;
        private PlayerProfile _profile;
        private ProfilePortraitPickerView _picker;

        public void BindUi(IUiRegistry registry) => _picker = registry.Get<ProfilePortraitPickerView>();
        protected override void InitializeView()
        {
            if (_button != null) { _button.onClick.AddListener(() => _picker.Open()); }
        }
        [Inject]
        public void Construct(PlayerProfile profile)
        {
            _profile = profile;
            _profile.Changed += Refresh;
            Refresh(profile);
        }
        private void Refresh(PlayerProfile profile) => _portrait.uvRect = PortraitArt.Uv(profile.AvatarId);
        protected override void DisposeView()
        {
            if (_profile != null) { _profile.Changed -= Refresh; }
        }
    }
}
