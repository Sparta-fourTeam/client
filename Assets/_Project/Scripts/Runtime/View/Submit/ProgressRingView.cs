using UnityEngine;
using UnityEngine.UI;

namespace Game.View
{
    /// <summary>"기다리는 중"을 보여주는 원형 진행 표시. 링이 12시에서 시계 방향으로 차오른다.
    /// 서버 응답에는 진행률이 없어서 걸린 시간으로 그린다: 처음엔 빠르게 차오르다 점점 느려지고, 끝까지 차지는 않는다(응답이 오면 화면이 닫힌다).
    /// 포기(Forfeit)는 timeScale이 0인 채로 제출하므로 unscaled time으로 센다</summary>
    public sealed class ProgressRingView : MonoBehaviour
    {
        [Tooltip("Filled(Radial360, Top, 시계 방향)로 설정된 채움 링")]
        [SerializeField] private Image _fill;
        [Tooltip("채움이 이 시간(초) 만에 약 63%까지 찬다")]
        [SerializeField, Min(0.1f)] private float _timeConstantSeconds = 1.6f;
        [SerializeField, Range(0.5f, 1f)] private float _maxFill = 0.95f;

        private float _elapsed;

        /// <summary>0~1 사이 값으로 직접 채운다 (편집 모드 미리보기와 진행률을 아는 호출자가 쓴다)</summary>
        public void SetProgress(float value)
        {
            if (_fill != null) { _fill.fillAmount = Mathf.Clamp01(value); }
        }

        private void OnEnable()
        {
            _elapsed = 0f;
            SetProgress(0f);
        }

        private void Update()
        {
            _elapsed += Time.unscaledDeltaTime;
            SetProgress(_maxFill * (1f - Mathf.Exp(-_elapsed / _timeConstantSeconds)));
        }
    }
}
