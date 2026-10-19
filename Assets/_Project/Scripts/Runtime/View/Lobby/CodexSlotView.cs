using System;
using UnityEngine;
using UnityEngine.UI;

namespace Game.View
{
    /// <summary>미발견 정보는 공통 물음표로 가리고, 발견한 몬스터만 상세를 열 수 있는 도감 칸.</summary>
    public sealed class CodexSlotView : MonoBehaviour
    {
        [SerializeField] private Button _button;
        [SerializeField] private Image _icon;
        [SerializeField] private GameObject _unknown;

        public int MonsterId { get; private set; }
        public bool IsDiscovered { get; private set; }

        public void Bind(int monsterId, Sprite icon, bool discovered, Action onClick)
        {
            MonsterId = monsterId;
            IsDiscovered = discovered;
            _icon.sprite = discovered ? icon : null;
            _icon.enabled = discovered && icon != null;
            _unknown.SetActive(!discovered);
            _button.interactable = discovered;
            _button.onClick.RemoveAllListeners();
            if (discovered) { _button.onClick.AddListener(() => onClick?.Invoke()); }
            gameObject.SetActive(true);
        }
    }
}
