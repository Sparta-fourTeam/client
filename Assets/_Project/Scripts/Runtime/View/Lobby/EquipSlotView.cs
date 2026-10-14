using System;
using Game.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.View
{
    /// <summary>캐릭터 화면·프로필 팝업의 장비 칸 하나. 잠긴 칸은 자물쇠로 가리고, 누르면 열리는 플레이어 레벨을 안내한다</summary>
    public sealed class EquipSlotView : MonoBehaviour
    {
        [SerializeField] private Button _button;
        [SerializeField] private TMP_Text _levelText;
        [SerializeField] private TMP_Text _lockedLevelText;
        [SerializeField] private GameObject _lock;

        private Action _onClick;

        private void Awake()
        {
            _button.onClick.AddListener(() => _onClick?.Invoke());
        }

        public void Bind(EquipInfo info, Action onClick, ComingSoonToastView toast)
        {
            gameObject.SetActive(true);
            _levelText.text = $"Lv.{info.Level}";
            if (_lockedLevelText != null)
            {
                _lockedLevelText.text = _levelText.text;
            }
            _lock.SetActive(!info.IsUnlocked);
            int unlockLevel = info.UnlockLevel;
            _onClick = info.IsUnlocked ? onClick : () => toast?.Show($"플레이어 레벨 {unlockLevel}이 되면 열려요");
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}
