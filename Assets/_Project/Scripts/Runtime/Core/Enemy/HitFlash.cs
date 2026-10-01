using UnityEngine;

namespace Game.Core
{
    /// <summary>적이 데미지를 받으면 자식의 모든 SpriteRenderer를 잠깐 빨갛게 만들었다가 원래대로 돌린다.
    /// 원래 색과 상관없이 같은 색으로 보이도록, 맞는 동안 머티리얼을 Game/2D/Sprite-HitFlash로 바꿔 끼우고
    /// 스프라이트 색을 flashColor로 섞는다 (알파는 그대로라 모양이 유지된다). 섞는 정도는 MaterialPropertyBlock으로 준다.
    /// 스프라이트가 여러 조각인 리깅 몬스터도 같이 변한다. 끝나거나 비활성화되면 원래 머티리얼로 되돌린다.
    /// 애니메이션이나 다른 처리가 끝난 뒤 적용되도록 LateUpdate에서 진행한다</summary>
    public sealed class HitFlash : MonoBehaviour
    {
        private const string MaterialPath = "Materials/SpriteHitFlash";

        // 시간의 앞쪽은 빨강을 완전히 유지하고, 마지막 이 비율 동안만 원래대로 서서히 돌아온다.
        // 서서히 섞는 구간이 길면 초록처럼 원래 색이 강한 몬스터가 탁한 중간색으로 보인다
        private const float FadeRatio = 0.4f;
        private static readonly int FlashAmountId = Shader.PropertyToID("_FlashAmount");
        private static readonly int FlashColorId = Shader.PropertyToID("_FlashColor");

        private static Material s_material;
        private static bool s_warned;

        [SerializeField] private Color _flashColor = new Color(1f, 0.15f, 0.15f, 1f);
        [SerializeField, Min(0.01f)] private float _duration = 0.15f;
        [SerializeField, Range(0f, 1f)] private float _peakAmount = 1f;

        private SpriteRenderer[] _renderers;
        private Material[] _baseMaterials;
        private MaterialPropertyBlock _block;
        private float _remaining;
        private bool _applied;

        /// <summary>남은 시간 비율에 따라 섞는 정도. 앞쪽은 peak을 유지하다가 마지막 FadeRatio 동안 0으로 줄어든다.
        /// peak이 1이면 원래 색과 상관없이 flashColor로 완전히 덮는다 (1보다 작으면 원래 색이 그만큼 남는다)</summary>
        public static float AmountAt(float remaining, float duration, float peak)
        {
            if (duration <= 0f)
            {
                return 0f;
            }

            return Mathf.Clamp01(remaining / duration / FadeRatio) * peak;
        }

        private static Material FlashMaterial
        {
            get
            {
                if (s_material == null)
                {
                    s_material = Resources.Load<Material>(MaterialPath);
                    if (s_material == null && !s_warned)
                    {
                        s_warned = true;
                        Debug.LogWarning($"[HitFlash] Resources/{MaterialPath} 머티리얼을 찾지 못해 피격 플래시가 동작하지 않는다");
                    }
                }

                return s_material;
            }
        }

        /// <summary>플래시를 시작한다. 이미 진행 중이면 처음부터 다시 시작한다</summary>
        public void Flash()
        {
            if (!CacheIfNeeded())
            {
                return;
            }

            _remaining = _duration;
            Apply();
        }

        /// <summary>시간을 진행시킨다. 끝나면 원래 머티리얼로 돌린다</summary>
        public void Advance(float deltaTime)
        {
            if (_remaining <= 0f)
            {
                return;
            }

            _remaining = Mathf.Max(0f, _remaining - deltaTime);
            Apply();
        }

        /// <summary>진행 중인 플래시를 멈추고 원래 머티리얼로 돌린다</summary>
        public void Restore()
        {
            _remaining = 0f;
            Apply();
        }

        private void LateUpdate()
        {
            Advance(Time.deltaTime);
        }

        private void OnDisable()
        {
            Restore();
        }

        private bool CacheIfNeeded()
        {
            if (_renderers != null)
            {
                return true;
            }

            if (FlashMaterial == null)
            {
                return false;
            }

            _renderers = GetComponentsInChildren<SpriteRenderer>(true);
            _baseMaterials = new Material[_renderers.Length];
            for (int i = 0; i < _renderers.Length; i++)
            {
                _baseMaterials[i] = _renderers[i].sharedMaterial;
            }

            _block = new MaterialPropertyBlock();
            return true;
        }

        private void Apply()
        {
            if (_renderers == null)
            {
                return;
            }

            if (_remaining > 0f)
            {
                float amount = AmountAt(_remaining, _duration, _peakAmount);
                for (int i = 0; i < _renderers.Length; i++)
                {
                    // 리깅 몬스터의 조각이 중간에 사라질 수 있다
                    var renderer = _renderers[i];
                    if (renderer == null)
                    {
                        continue;
                    }

                    if (renderer.sharedMaterial != s_material)
                    {
                        renderer.sharedMaterial = s_material;
                    }

                    renderer.GetPropertyBlock(_block);
                    _block.SetFloat(FlashAmountId, amount);
                    _block.SetColor(FlashColorId, _flashColor);
                    renderer.SetPropertyBlock(_block);
                }

                _applied = true;
                return;
            }

            if (!_applied)
            {
                return;
            }

            for (int i = 0; i < _renderers.Length; i++)
            {
                var renderer = _renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                renderer.sharedMaterial = _baseMaterials[i];
                renderer.SetPropertyBlock(null);
            }

            _applied = false;
        }
    }
}
