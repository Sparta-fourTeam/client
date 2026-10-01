using System;
using Game.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.View
{
    /// <summary>인술 화면의 칸 하나. 열려 있으면 레벨을, 잠겨 있으면 "레벨 N에 해금"을 표시한다</summary>
    public sealed class NinpoSlotView : MonoBehaviour
    {
        [SerializeField] private Button _button;
        [SerializeField] private TMP_Text _levelText;
        [SerializeField] private GameObject _lock;
        [SerializeField] private TMP_Text _lockLabel;

        private Action _onClick;

        private void Awake()
        {
            _button.onClick.AddListener(() => _onClick?.Invoke());
        }

        /// <summary>잠긴 칸은 눌러도 아무것도 하지 않는다</summary>
        public void Bind(NinpoInfo info, Action onClick)
        {
            gameObject.SetActive(true);
            _levelText.text = $"Lv.{info.Level}";
            _lock.SetActive(!info.IsUnlocked);
            _lockLabel.text = $"레벨 {info.UnlockLevel}에 해금";
            _onClick = info.IsUnlocked ? onClick : null;
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}
