using Cysharp.Threading.Tasks;
using Game.Core;
using UnityEngine;

namespace Game.View
{
    /// <summary>CanvasGroup 알파로 화면을 가리는 씬 전환 커튼</summary>
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class TransitionCurtain : MonoBehaviour, ITransitionCurtain
    {
        [SerializeField] private float fadeSeconds = 0.25f;

        private CanvasGroup _group;

        /// <summary>화면을 가린 정도 (0 = 열림/꺼짐, 1 = 완전히 가림). 전환이 끝난 뒤 시작할 연출이 기다릴 때 쓴다</summary>
        public float Coverage => _group != null && gameObject.activeInHierarchy ? _group.alpha : 0f;

        private void Awake()
        {
            _group = GetComponent<CanvasGroup>();
        }

        public async UniTask Close()
        {
            if (this == null)
            {
                return;
            }

            gameObject.SetActive(true);
            if (_group == null)
            {
                _group = GetComponent<CanvasGroup>();
            }

            _group.blocksRaycasts = true;
            await Fade(0f, 1f);
        }

        public async UniTask Open()
        {
            await Fade(1f, 0f);
            if (_group == null)
            {
                return;
            }

            _group.blocksRaycasts = false;
            gameObject.SetActive(false);
        }

        private async UniTask Fade(float from, float to)
        {
            if (_group == null)
            {
                return;
            }

            _group.alpha = from;
            float elapsed = 0f;
            while (elapsed < fadeSeconds)
            {
                if (_group == null)
                {
                    return;
                }

                elapsed += Time.unscaledDeltaTime;
                _group.alpha = Mathf.Lerp(from, to, elapsed / fadeSeconds);
                await UniTask.Yield();
            }

            if (_group != null)
            {
                _group.alpha = to;
            }
        }
    }
}
