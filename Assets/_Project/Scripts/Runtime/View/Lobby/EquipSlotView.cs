using System;
using Game.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.View
{
    /// <summary>캐릭터 화면의 장비 칸 하나. 잠긴 칸은 가리고 눌러도 반응하지 않는다</summary>
    public sealed class EquipSlotView : MonoBehaviour
    {
        [SerializeField] private Button _button;
        [SerializeField] private TMP_Text _levelText;
        [SerializeField] private GameObject _lock;

        private Action _onClick;

        private void Awake()
        {
            _button.onClick.AddListener(() => _onClick?.Invoke());
        }

        public void Bind(EquipInfo info, Action onClick)
        {
            gameObject.SetActive(true);
            _levelText.text = $"Lv.{info.Level}";
            _lock.SetActive(!info.IsUnlocked);
            _onClick = info.IsUnlocked ? onClick : null;
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}
