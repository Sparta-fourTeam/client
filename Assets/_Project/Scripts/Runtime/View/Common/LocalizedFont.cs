using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;

namespace Game.View
{
    /// <summary>
    /// 로케일별로 TMP 라벨의 폰트를 Asset Table의 값으로 바꾼다.
    /// 일본어·중국어처럼 정적 폰트에 글리프가 없는 로케일에서 쓰고, 항목이 비어 있으면(ko/en) 라벨의 원래 폰트와 머티리얼을 그대로 둔다.
    /// 폰트를 바꾸면 TMP가 새 폰트의 기본 머티리얼을 쓰므로, 라벨의 원래 머티리얼 설정(외곽선 등)을 복사한 머티리얼을 대신 연결한다.
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    [AddComponentMenu("Localization/Asset/Localized Font")]
    public sealed class LocalizedFont : LocalizedAssetBehaviour<TMP_FontAsset, LocalizedTmpFont>
    {
        /// <summary>기본으로 참조하는 Asset Table 이름.</summary>
        public const string DefaultTable = "GameAssets";

        /// <summary>기본으로 참조하는 Asset Table 키.</summary>
        public const string DefaultEntry = "UIFont";

        // 라벨별 설정을 옮길 때 이름이 같으면 복사하는 머티리얼 프로퍼티
        private static readonly string[] ColorProperties =
        {
            "_FaceColor", "_OutlineColor", "_UnderlayColor",
        };

        private static readonly string[] FloatProperties =
        {
            "_FaceDilate",
            "_OutlineWidth", "_OutlineSoftness",
            "_UnderlayOffsetX", "_UnderlayOffsetY", "_UnderlayDilate", "_UnderlaySoftness",
        };

        // (폰트, 원래 머티리얼) 쌍마다 한 번만 만든 머티리얼
        private static readonly Dictionary<(TMP_FontAsset, Material), Material> MaterialCache = new();

        // 자동 축소 하한. 원래 글자 크기에 대한 비율
        private const float MinSizeRatio = 0.55f;

        private TMP_Text _label;
        private TMP_FontAsset _originalFont;
        private Material _originalMaterial;
        private FontStyles _originalStyle;
        private bool _originalAutoSize;
        private float _originalSize, _originalSizeMin, _originalSizeMax;
        private bool _captured;

        /// <summary>항목이 비어 있으면 기본 테이블/키로 채운다. 에디터에서 컴포넌트를 붙일 때 호출된다.</summary>
        private void Reset()
        {
            ApplyDefaultReference();
        }

        private void Awake()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            ApplyDefaultReference();
            CaptureOriginal();
            // 문구가 먼저 바뀐 뒤 글리프 없는 폰트로 한 번 그려지지 않도록 폰트는 로케일 변경 시점에 동기로 불러온다(WebGL 미지원)
#if !UNITY_WEBGL
            AssetReference.WaitForCompletion = true;
#endif
        }

        // 기반 클래스의 OnEnable이 변경 핸들러를 등록하면서 곧바로 UpdateAsset을 부를 수 있으므로 그 전에 원본을 저장한다
        protected override void OnEnable()
        {
            if (Application.isPlaying)
            {
                CaptureOriginal();
            }

            base.OnEnable();
        }

        /// <summary>로케일의 폰트로 라벨을 바꾼다. font가 null이면 원래 폰트와 머티리얼로 복원한다.</summary>
        /// <param name="font">로케일의 폰트. 비어 있으면 null.</param>
        protected override void UpdateAsset(TMP_FontAsset font)
        {
            // 에디터 편집 중(ExecuteAlways, OnValidate)에는 라벨을 건드리지 않아 프리팹·씬에 섞이지 않게 한다
            if (!Application.isPlaying)
            {
                return;
            }

            CaptureOriginal();
            if (_label == null)
            {
                return;
            }

            if (font == null)
            {
                Restore();
                return;
            }

            _label.font = font;
            _label.fontSharedMaterial = GetMaterial(font, _originalMaterial);
            // 언어마다 글자 폭이 달라 버튼 밖으로 넘치기 쉬우므로 원래 크기를 최대로 자동 축소한다
            if (!_originalAutoSize)
            {
                _label.fontSize = _originalSize;
                _label.fontSizeMax = _originalSize;
                _label.fontSizeMin = _originalSize * MinSizeRatio;
                _label.enableAutoSizing = true;
            }
            _label.SetAllDirty();
        }

        private void ApplyDefaultReference()
        {
            var reference = AssetReference;
            if (reference == null || !reference.IsEmpty)
            {
                return;
            }

            reference.SetReference(DefaultTable, DefaultEntry);
        }

        private void CaptureOriginal()
        {
            if (_captured)
            {
                return;
            }

            _label = GetComponent<TMP_Text>();
            if (_label == null)
            {
                return;
            }

            _originalFont = _label.font;
            _originalMaterial = _label.fontSharedMaterial;
            _originalStyle = _label.fontStyle;
            _originalAutoSize = _label.enableAutoSizing;
            _originalSize = _label.fontSize;
            _originalSizeMin = _label.fontSizeMin;
            _originalSizeMax = _label.fontSizeMax;
            _captured = true;
        }

        private void Restore()
        {
            if (_originalFont != null)
            {
                _label.font = _originalFont;
            }

            if (_originalMaterial != null)
            {
                _label.fontSharedMaterial = _originalMaterial;
            }

            _label.fontStyle = _originalStyle;
            _label.enableAutoSizing = _originalAutoSize;
            _label.fontSize = _originalSize;
            _label.fontSizeMin = _originalSizeMin;
            _label.fontSizeMax = _originalSizeMax;
            _label.SetAllDirty();
        }

        private static Material GetMaterial(TMP_FontAsset font, Material original)
        {
            // 원래 머티리얼이 없거나 이미 이 폰트의 기본 머티리얼이면 복사할 설정이 없다
            if (original == null || original == font.material)
            {
                return font.material;
            }

            var key = (font, original);
            if (MaterialCache.TryGetValue(key, out var cached) && cached != null)
            {
                return cached;
            }

            var material = new Material(font.material)
            {
                name = $"{font.name} ({original.name})",
                hideFlags = HideFlags.DontSave,
            };
            CopyLabelSettings(original, material);
            MaterialCache[key] = material;
            return material;
        }

        private static void CopyLabelSettings(Material source, Material target)
        {
            foreach (var property in ColorProperties)
            {
                if (source.HasProperty(property) && target.HasProperty(property))
                {
                    target.SetColor(property, source.GetColor(property));
                }
            }

            foreach (var property in FloatProperties)
            {
                if (source.HasProperty(property) && target.HasProperty(property))
                {
                    target.SetFloat(property, source.GetFloat(property));
                }
            }

            // OUTLINE_ON, UNDERLAY_ON 같은 셰이더 키워드
            target.shaderKeywords = source.shaderKeywords;
        }
    }
}
