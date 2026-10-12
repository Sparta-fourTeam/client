using DG.Tweening;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Core
{
    /// <summary>적의 발밑에 단순한 타원 그림자를 깐다. 비주얼 SortingGroup 안(애니메이션되는 Body의 형제)에 두어
    /// 적과 함께 이동·크기 변경·앞뒤 정렬을 따르고, Body가 흔들려도 바닥에 그대로 있다. 적과 함께 파괴되므로 따로 정리할 필요가 없다.
    /// 피격 플래시·상태이상 색은 Body 아래만 바꾸므로 그림자에는 닿지 않는다</summary>
    public sealed class EnemyShadow : MonoBehaviour
    {
        public const string ShadowName = "Shadow";
        private const int SortingOrder = -1000;        // 그룹 안에서 모든 파츠(0 이상)보다 뒤
        private const float WidthRatio = 0.8f;         // 스프라이트 폭 대비 그림자 폭
        private const float HeightRatio = 0.28f;       // 그림자 폭 대비 높이
        private const float MinWidth = 0.15f;
        private static readonly Color ShadowColor = new Color(0f, 0f, 0f, 0.45f);

        private static Sprite s_sprite;

        private SpriteRenderer _renderer;
        private Tween _fade;

        /// <summary>그림자 오브젝트. 비주얼이 없는 적이면 null</summary>
        public Transform Shadow => _renderer != null ? _renderer.transform : null;

        /// <summary>그림자를 만들거나(이미 있으면 다시 쓴다) 지금 비주얼 크기에 맞춘다</summary>
        public void Bind()
        {
            _fade?.Kill();
            var group = GetComponentInChildren<SortingGroup>(true);
            var parent = group != null ? group.transform : transform;
            var body = GetComponentInChildren<Animator>(true);
            var bodyRoot = body != null ? body.transform : parent;

            if (_renderer == null)
            {
                var existing = parent.Find(ShadowName);
                _renderer = existing != null ? existing.GetComponent<SpriteRenderer>() : null;
                if (_renderer == null)
                {
                    var go = new GameObject(ShadowName, typeof(SpriteRenderer));
                    go.transform.SetParent(parent, false);
                    _renderer = go.GetComponent<SpriteRenderer>();
                }
            }

            _renderer.sprite = SharedSprite;
            _renderer.color = ShadowColor;
            _renderer.sortingOrder = SortingOrder;
            Fit(parent, bodyRoot);
        }

        /// <summary>사망 연출 동안 그림자를 서서히 지운다</summary>
        public void FadeOut(float seconds)
        {
            if (_renderer == null)
            {
                return;
            }

            _fade?.Kill();
            _fade = DOTween.ToAlpha(() => _renderer.color, c => _renderer.color = c, 0f, Mathf.Max(0.01f, seconds))
                .SetLink(gameObject);
        }

        private void OnDestroy() => _fade?.Kill();

        // 발끝(Body 스프라이트의 아래쪽 끝) 가운데에, 몸 폭에 맞춘 타원을 놓는다. 크기는 월드 기준으로 맞추고 부모 스케일을 나눈다
        private void Fit(Transform parent, Transform bodyRoot)
        {
            bool found = false;
            var bounds = new Bounds(transform.position, Vector3.zero);
            foreach (var renderer in bodyRoot.GetComponentsInChildren<SpriteRenderer>())
            {
                if (renderer == _renderer || renderer.sprite == null)
                {
                    continue;
                }

                if (!found) { bounds = renderer.bounds; found = true; }
                else { bounds.Encapsulate(renderer.bounds); }
            }

            float width = Mathf.Max(MinWidth, (found ? bounds.size.x : 0.5f) * WidthRatio);
            var world = new Vector3(bounds.center.x, found ? bounds.min.y : transform.position.y, transform.position.z);
            var shadow = _renderer.transform;
            shadow.position = world;
            shadow.rotation = Quaternion.identity;
            var lossy = parent.lossyScale;
            shadow.localScale = new Vector3(width / Mathf.Max(0.0001f, Mathf.Abs(lossy.x)),
                width * HeightRatio / Mathf.Max(0.0001f, Mathf.Abs(lossy.y)), 1f);
        }

        // 가장자리로 갈수록 흐려지는 1×1 유닛 원. 타원 모양은 스케일로 만든다. 모든 적이 한 장을 같이 쓴다
        private static Sprite SharedSprite
        {
            get
            {
                if (s_sprite != null)
                {
                    return s_sprite;
                }

                const int size = 64;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    name = "EnemyShadow",
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                    hideFlags = HideFlags.DontSave,
                };
                var pixels = new Color32[size * size];
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float dx = (x + 0.5f) / size * 2f - 1f;
                        float dy = (y + 0.5f) / size * 2f - 1f;
                        float r = Mathf.Sqrt(dx * dx + dy * dy);
                        float alpha = Mathf.Clamp01((1f - r) * 2.5f);
                        pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                    }
                }

                texture.SetPixels32(pixels);
                texture.Apply(false, true);
                s_sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
                s_sprite.name = "EnemyShadow";
                s_sprite.hideFlags = HideFlags.DontSave;
                return s_sprite;
            }
        }
    }
}
