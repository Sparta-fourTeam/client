using UnityEngine;

namespace Game.View
{
    /// <summary>"기다리는 중"을 보여주는 회전 표시. 포기(Forfeit)는 timeScale이 0인 채로 제출하므로 unscaled time으로 돈다</summary>
    public sealed class SpinnerView : MonoBehaviour
    {
        [SerializeField] private float _degreesPerSecond = 360f;

        private void Update()
        {
            transform.Rotate(0f, 0f, -_degreesPerSecond * Time.unscaledDeltaTime);
        }
    }
}
