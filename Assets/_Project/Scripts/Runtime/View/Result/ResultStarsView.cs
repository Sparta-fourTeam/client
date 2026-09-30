using UnityEngine;
using UnityEngine.UI;

namespace Game.View
{
    /// <summary>클리어 결과의 별점(1~3). 별점 데이터가 생기면 SetCount를 호출한다. 호출 전에는 꺼져 있다</summary>
    public sealed class ResultStarsView : MonoBehaviour
    {
        [SerializeField] private Image[] _stars;
        [SerializeField] private Color _litColor = new Color(1f, 0.8f, 0.2f, 1f);
        [SerializeField] private Color _unlitColor = new Color(0.25f, 0.25f, 0.3f, 0.8f);

        public void SetCount(int count)
        {
            for (int i = 0; i < _stars.Length; i++)
            {
                _stars[i].color = i < count ? _litColor : _unlitColor;
            }

            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}
