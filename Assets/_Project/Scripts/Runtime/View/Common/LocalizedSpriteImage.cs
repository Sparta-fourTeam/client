using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;
using UnityEngine.UI;

namespace Game.View
{
    /// <summary>
    /// 로케일별로 UI Image의 스프라이트를 Asset Table의 값으로 바꾼다.
    /// 글자가 그려진 로고처럼 언어마다 이미지가 달라야 하는 곳에 쓰고, 항목이 비어 있으면 Image의 원래 스프라이트를 그대로 둔다.
    /// </summary>
    [RequireComponent(typeof(Image))]
    [AddComponentMenu("Localization/Asset/Localized Sprite Image")]
    public sealed class LocalizedSpriteImage : LocalizedAssetBehaviour<Sprite, LocalizedSprite>
    {
        /// <summary>기본으로 참조하는 Asset Table 이름.</summary>
        public const string DefaultTable = "GameAssets";

        [SerializeField] private string _entry = "TitleLogo";

        private Image _image;
        private Sprite _originalSprite;
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
            // 로고가 먼저 원래 언어로 한 번 그려지지 않도록 스프라이트는 로케일 변경 시점에 동기로 불러온다(WebGL 미지원)
            AssetReference.WaitForCompletion = true;
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

        /// <summary>로케일의 스프라이트로 Image를 바꾼다. sprite가 null이면 원래 스프라이트로 복원한다.</summary>
        /// <param name="sprite">로케일의 스프라이트. 비어 있으면 null.</param>
        protected override void UpdateAsset(Sprite sprite)
        {
            // 에디터 편집 중(ExecuteAlways, OnValidate)에는 Image를 건드리지 않아 프리팹·씬에 섞이지 않게 한다
            if (!Application.isPlaying)
            {
                return;
            }

            CaptureOriginal();
            if (_image == null)
            {
                return;
            }

            _image.sprite = sprite != null ? sprite : _originalSprite;
        }

        private void ApplyDefaultReference()
        {
            var reference = AssetReference;
            if (reference == null || !reference.IsEmpty)
            {
                return;
            }

            reference.SetReference(DefaultTable, _entry);
        }

        private void CaptureOriginal()
        {
            if (_captured)
            {
                return;
            }

            _image = GetComponent<Image>();
            if (_image == null)
            {
                return;
            }

            _originalSprite = _image.sprite;
            _captured = true;
        }
    }
}
