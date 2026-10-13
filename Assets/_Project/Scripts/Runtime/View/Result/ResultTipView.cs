using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.View
{
    /// <summary>실패하면 결과 화면 위에 겹쳐 띄우는 "성장을 위한 TIP!" 패널. 카드 문구와 아이콘은 프리팹에 고정되어 있고 기능은 없다.
    /// 패널 아무 곳이나 누르면 닫히면서 Closed가 발생한다. 호출 전에는 꺼져 있다.
    /// 버튼이 아니라 클릭을 직접 받으므로 버튼 공통 눌림 효과(ButtonPressFeedback)가 적용되지 않는다.
    /// 클릭은 패널 배경 이미지(raycastTarget)가 받고, 카드 위를 눌러도 부모인 이 패널로 올라온다</summary>
    public sealed class ResultTipView : MonoBehaviour, IPointerClickHandler
    {
        public event Action Closed;

        public bool IsShown => gameObject.activeSelf;

        public void Show() => gameObject.SetActive(true);

        public void Hide() => gameObject.SetActive(false);

        public void OnPointerClick(PointerEventData eventData) => Close();

        /// <summary>닫고 Closed를 알린다. 이미 닫혀 있으면 아무것도 하지 않는다</summary>
        public void Close()
        {
            if (!IsShown)
            {
                return;
            }

            Hide();
            Closed?.Invoke();
        }
    }
}
