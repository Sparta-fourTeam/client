using Cysharp.Threading.Tasks;
using DG.Tweening;
using Game.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Game.View
{
    /// <summary>스테이지를 클리어하기 전까지 버튼 위에 자물쇠 판을 덮는다 (ClientData/FeatureUnlocks.json).
    /// 판이 터치를 가로채서 아래 버튼은 눌리지 않고, 판을 누르면 몇 스테이지를 깨야 열리는지 알려 준다. 하단 탭·사이드 메뉴 버튼에 붙인다.
    /// 로비에 돌아와 새로 열렸으면 Hold로 자물쇠를 다시 걸어 두었다가, 컨텐츠 해금 연출 뒤 PlayUnlockAsync로 푼다 (TutorialGuideView)</summary>
    public sealed class FeatureLockView : MonoBehaviour, IUiBindable, IButtonLockState
    {
        [Tooltip("FeatureUnlocks.json의 Feature 이름")]
        [SerializeField] private string _feature;
        [Tooltip("버튼을 덮는 자물쇠 판. 잠겨 있는 동안만 켜진다")]
        [SerializeField] private Button _lock;
        [Tooltip("자물쇠 그림. 해금 연출에서 흔들리다 사라진다")]
        [SerializeField] private RectTransform _lockIcon;
        [Tooltip("컨텐츠 해금 화면에 보여 줄 이 버튼의 아이콘·이름")]
        [SerializeField] private Image _icon;
        [SerializeField] private TMP_Text _label;

        private ComingSoonToastView _toast;
        private PlayerProfile _profile;
        private GameDataStore _data;
        private bool _held;
        private Sequence _unlock;

        public void BindUi(IUiRegistry registry) => _toast = registry.Get<ComingSoonToastView>();

        [Inject]
        public void Construct(PlayerProfile profile, GameDataStore data)
        {
            _profile = profile;
            _data = data;
            _profile.Changed += OnProfileChanged;
            Refresh();
        }

        public bool IsLocked => _data != null && _data.FeatureUnlocks.IsLocked(_feature, _profile.IsStageCleared);

        public bool IsLockedButton(Button button) => button == _lock && _lock.gameObject.activeSelf;

        /// <summary>붙잡아 둔 사이에 열렸다. 해금 연출을 보여 줄 차례다</summary>
        public bool IsUnlockPending => _held && !IsLocked;

        public string ContentName => _label != null ? _label.text : _feature;
        public Sprite ContentIcon => _icon != null ? _icon.sprite : null;

        private void Awake()
        {
            _lock.onClick.AddListener(() => _toast?.Show($"{_data.FeatureUnlocks.RequiredStage(_feature)}스테이지를 클리어하면 열려요"));
            Refresh();
        }

        /// <summary>wasStageCleared(이전 클리어 기록) 기준으로 잠겨 있었으면, 지금 열렸어도 PlayUnlockAsync·Release 전까지 자물쇠를 그대로 둔다</summary>
        public void Hold(System.Func<int, bool> wasStageCleared)
        {
            _held = _data != null && _data.FeatureUnlocks.IsLocked(_feature, wasStageCleared);
            Refresh();
        }

        /// <summary>붙잡은 자물쇠를 연출 없이 놓는다</summary>
        public void Release()
        {
            _held = false;
            Refresh();
        }

        /// <summary>자물쇠가 흔들리다 튀어 오르며 사라지고 버튼이 한 번 튄다. 끝나면 완료된다</summary>
        public UniTask PlayUnlockAsync()
        {
            _unlock?.Kill();
            var done = new UniTaskCompletionSource();
            _unlock = DOTween.Sequence().SetUpdate(true).SetLink(gameObject)
                .Append(_lockIcon.DOPunchRotation(new Vector3(0f, 0f, 25f), 0.5f, 14))
                .Append(_lockIcon.DOScale(1.5f, 0.12f))
                .Append(_lockIcon.DOScale(0f, 0.18f).SetEase(Ease.InBack))
                .AppendCallback(Release)
                .Append(transform.DOPunchScale(Vector3.one * 0.25f, 0.35f, 6))
                .OnComplete(() => done.TrySetResult())
                .OnKill(() =>
                {
                    _lockIcon.localScale = Vector3.one;
                    _lockIcon.localRotation = Quaternion.identity;
                    done.TrySetResult();
                });
            return done.Task;
        }

        private void OnProfileChanged(PlayerProfile _) => Refresh();

        private void Refresh() => _lock.gameObject.SetActive(_held || IsLocked);

        private void OnDestroy()
        {
            _unlock?.Kill();
            if (_profile != null)
            {
                _profile.Changed -= OnProfileChanged;
            }
        }
    }
}
