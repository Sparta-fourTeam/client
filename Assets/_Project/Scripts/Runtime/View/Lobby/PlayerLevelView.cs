using Game.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Game.View
{
    /// <summary>로비 상단 바의 플레이어 레벨 칸. 레벨, 경험치 게이지, 현재/필요 경험치를 PlayerProfile에서 읽어 보여준다.
    /// 프로필이 갱신될 때마다(로비 진입 시 GetMe 적용, 전투 보상 EXP 반영 후 등) 다시 그린다</summary>
    public sealed class PlayerLevelView : HudView
    {
        [SerializeField] private TMP_Text _levelText, _expText;
        [SerializeField] private Image _expFill;

        private PlayerProfile _profile;

        [Inject]
        public void Construct(PlayerProfile profile)
        {
            _profile = profile;
            _profile.Changed += OnProfileChanged;
            Refresh();
        }

        protected override void OnDestroy()
        {
            if (_profile != null)
            {
                _profile.Changed -= OnProfileChanged;
            }

            base.OnDestroy();
        }

        private void OnProfileChanged(PlayerProfile _) => Refresh();

        // 만렙에서는 요구 경험치가 0이라 게이지를 가득 채우고 "MAX"를 보여준다
        private void Refresh()
        {
            _levelText.text = $"Lv.{_profile.Level}";
            _expFill.fillAmount = _profile.LevelProgress;
            _expText.text = _profile.IsMaxLevel
                ? "MAX"
                : $"{CurrencyFormat.Format(_profile.ExpIntoLevel)} / {CurrencyFormat.Format(_profile.ExpRequiredForNext)}";
        }
    }
}
