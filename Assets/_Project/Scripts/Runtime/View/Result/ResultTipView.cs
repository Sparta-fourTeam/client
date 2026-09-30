using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.View
{
    /// <summary>실패 시 보여주는 "성장을 위한 TIP!" 패널. Show로 열고 확인을 누르면 닫히면서 Confirmed가 발생한다.
    /// 언제 열지(실패 직후, 결과 앞 단계)는 연결하는 쪽이 정한다. 호출 전에는 꺼져 있다</summary>
    public sealed class ResultTipView : MonoBehaviour
    {
        [SerializeField] private TMP_Text[] _tipTexts;
        [SerializeField] private Button _confirmButton;

        public event Action Confirmed;

        private void Awake()
        {
            _confirmButton.onClick.AddListener(OnConfirmClicked);
        }

        public void Show(IReadOnlyList<string> tips)
        {
            for (int i = 0; i < _tipTexts.Length; i++)
            {
                bool has = i < tips.Count;
                _tipTexts[i].transform.parent.gameObject.SetActive(has);
                if (has)
                {
                    _tipTexts[i].text = tips[i];
                }
            }

            gameObject.SetActive(true);
        }

        private void OnConfirmClicked()
        {
            gameObject.SetActive(false);
            Confirmed?.Invoke();
        }
    }
}
