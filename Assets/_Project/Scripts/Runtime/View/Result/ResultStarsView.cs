using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Game.View
{
    /// <summary>클리어 결과의 별점(1~3). 클리어하면 켜 주고, 실패하면 보이지 않는다. 호출 전에는 꺼져 있다</summary>
    public sealed class ResultStarsView : MonoBehaviour
    {
        [SerializeField] private Image[] _stars;
        [SerializeField] private Color _litColor = new Color(1f, 0.8f, 0.2f, 1f);
        [SerializeField] private Color _unlitColor = new Color(0.25f, 0.25f, 0.3f, 0.8f);
        private Coroutine _reveal;
        private int _pendingCount = -1;

        /// <summary>클리어면 별점만큼 별을 켜서 보여주고, 실패면 별 영역을 숨긴다 (별점은 클리어에만 있다)</summary>
        public void Show(bool cleared, int rating)
        {
            if (cleared)
            {
                SetCount(rating);
                if (Application.isPlaying)
                {
                    _pendingCount = Mathf.Clamp(rating, 0, _stars.Length);
                    foreach (var star in _stars) { star.color = _unlitColor; }
                    if (isActiveAndEnabled) { BeginReveal(); }
                }
            }
            else
            {
                Hide();
            }
        }

        public void SetCount(int count)
        {
            CancelReveal();
            for (int i = 0; i < _stars.Length; i++)
            {
                _stars[i].color = i < count ? _litColor : _unlitColor;
            }

            gameObject.SetActive(true);
        }

        public void Hide()
        {
            CancelReveal();
            gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            if (_pendingCount >= 0) { BeginReveal(); }
        }

        private void OnDisable() => CancelReveal();

        private void BeginReveal()
        {
            int count = _pendingCount;
            _pendingCount = -1;
            _reveal = StartCoroutine(Reveal(count));
        }

        private IEnumerator Reveal(int count)
        {
            // 결과 화면은 게임 시간이 멈춰 있으므로 실제 시간으로 차례대로 켠다.
            yield return new WaitForSecondsRealtime(0.18f);
            for (int i = 0; i < count; i++)
            {
                float elapsed = 0f;
                while (elapsed < 0.16f)
                {
                    elapsed += Time.unscaledDeltaTime;
                    _stars[i].color = Color.Lerp(_unlitColor, _litColor, elapsed / 0.16f);
                    yield return null;
                }
                _stars[i].color = _litColor;
                yield return new WaitForSecondsRealtime(0.1f);
            }
            _reveal = null;
        }

        private void CancelReveal()
        {
            if (_reveal != null) { StopCoroutine(_reveal); _reveal = null; }
            _pendingCount = -1;
        }
    }
}
