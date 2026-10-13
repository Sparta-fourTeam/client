using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Game.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Game.View
{
    /// <summary>로비 진입 처리와 가이드 튜토리얼 실행.
    /// 들어오면 완료 조건을 채운 튜토리얼을 완료하고 레벨업 팝업을 띄운다. 진행 중인 전투 튜토리얼이 남았으면(전투 발급 실패 등) 그 스테이지로 다시 보낸다.
    /// 그다음 진행 중인 가이드 튜토리얼(Tutorials.json)이 있으면 Script 템플릿(LobbyTutorialScripts)의 단계를 하나씩 강조하고,
    /// 완료 조건을 채우면 완료를 저장한 뒤 다음 가이드로 넘어간다.
    /// 강조하는 동안 강조한 버튼 외의 화면을 반투명 검정으로 덮고 터치를 막는다. 표시는 버튼 오른쪽 위에 놓인다</summary>
    public sealed class TutorialGuideView : HudView, IUiBindable
    {
        public void BindUi(IUiRegistry registry)
        {
            _ui = registry;
            _levelUp = registry.Get<LevelUpPopupView>();
            _dialogue = registry.Get<DialogueView>();
            _contentUnlock = registry.Get<ContentUnlockView>();
        }

        [SerializeField] private GameObject _root;
        [SerializeField] private TutorialRaycastHole _blocker;
        [Tooltip("강조 표시. 대상 오른쪽 위에서 위아래로 흔들린다 (나중에 연필 이미지로 교체)")]
        [SerializeField] private RectTransform _pointer;
        [Tooltip("화면 전체를 덮는 스포트라이트 판 (Game/UI/Spotlight 머티리얼). 강조 대상 자리만 둥글게 뚫고 둘레에 펄스 링을 그린다. 포인터와 같은 부모 안에서 화면을 꽉 채워야 한다")]
        [SerializeField] private Image _spotlight;
        [SerializeField] private RectTransform _dialog;
        [SerializeField] private TMP_Text _dialogText;
        [Tooltip("표시 중심을 대상 오른쪽 위 꼭짓점에서 표시 크기의 몇 배만큼 바깥으로 둘지")]
        [SerializeField] private float _pointerOffset = 0.3f;
        [Tooltip("손가락이 대상을 누르는 한 번의 길이(초)")]
        [SerializeField] private float _tapSeconds = 1.25f;
        [Tooltip("구멍을 대상보다 사방으로 키우는 여유(px)")]
        [SerializeField] private float _holePadding = 14f;
        [SerializeField] private float _holeRadius = 36f;
        [SerializeField] private float _holeSoftness = 10f;
        [Tooltip("펄스 링이 구멍 가장자리에서 퍼지는 최대 거리(px)")]
        [SerializeField] private float _ringSpread = 70f;
        [SerializeField] private float _ringThickness = 7f;
        [SerializeField] private Color _ringColor = new Color(1f, 0.82f, 0.35f, 1f);
        [Tooltip("강조 구멍이 새 대상으로 옮겨 가는 속도 (클수록 빠르다)")]
        [SerializeField] private float _holeFollow = 14f;
        [Tooltip("대화창을 화면 가운데에서 위/아래로 띄우는 거리. 강조 대상과 반대쪽 절반에 둔다")]
        [SerializeField] private float _dialogOffset = 520f;

        private const float PressAt = 0.5f;
        private static readonly int HoleId = Shader.PropertyToID("_Hole");
        private static readonly int ParamsId = Shader.PropertyToID("_Params");
        private static readonly int RingId = Shader.PropertyToID("_Ring");
        private static readonly int RingColorId = Shader.PropertyToID("_RingColor");
        private const string SkillTab = "skill";

        private IUiRegistry _ui;
        private LevelUpPopupView _levelUp;
        private DialogueView _dialogue;
        private ContentUnlockView _contentUnlock;
        private LobbyArrival _arrival;
        private PlayerProfile _profile;
        private GameDataStore _data;
        private BattleLauncher _launcher;
        private bool _ready;
        private bool _completing;
        private CanvasGroup _rootGroup;
        private Material _spotlightMaterial;
        private Tween _fade;
        private Vector2 _holeMin, _holeMax;
        private bool _holeShown;
        private string _shownLine;
        private TutorialDefinition _guide;
        private IReadOnlyList<TutorialStep> _steps;

        [Inject]
        public void Construct(LobbyArrival arrival, PlayerProfile profile, GameDataStore data, BattleLauncher launcher)
        {
            _arrival = arrival;
            _profile = profile;
            _data = data;
            _launcher = launcher;
            _profile.Changed += OnProfileChanged;
        }

        protected override void InitializeView()
        {
            if (_spotlight != null)
            {
                // 이 가이드만의 머티리얼 복사본 (구멍 위치는 매 프레임 바뀐다)
                _spotlightMaterial = new Material(_spotlight.material);
                _spotlight.material = _spotlightMaterial;
            }

            _rootGroup = _root.GetComponent<CanvasGroup>();
            if (_rootGroup == null)
            {
                _rootGroup = _root.AddComponent<CanvasGroup>();
            }

            _root.SetActive(false);
        }

        protected override void DisposeView()
        {
            _fade?.Kill();
            if (_spotlightMaterial != null)
            {
                Destroy(_spotlightMaterial);
            }

            if (_profile != null)
            {
                _profile.Changed -= OnProfileChanged;
            }
        }

        private void Start() => RunAsync().Forget(Debug.LogException);

        private async UniTask RunAsync()
        {
            // 해금 전이면 스킬 탭에 자물쇠를 걸어 두었다가 레벨업 연출 뒤에 푼다
            var nav = _ui.Get<LobbyNavView>();
            bool tabWasLocked = TutorialProgress.IsTabLocked(_data.Tutorials, TutorialFacts.From(_profile), SkillTab);
            nav.SetSkillLocked(tabWasLocked);
            var news = await _arrival.CollectAsync();
            if (this == null)
            {
                return;
            }

            bool tabLocked = TutorialProgress.IsTabLocked(_data.Tutorials, TutorialFacts.From(_profile), SkillTab);
            // 새 컨텐츠 해금·레벨업 연출이 끝날 때까지는 잠긴 채로 둔다
            nav.SetSkillLocked(tabLocked || tabWasLocked);

            var battle = TutorialProgress.ActiveBattle(_data.Tutorials, TutorialFacts.From(_profile));
            if (battle != null)
            {
                // 첫 시작: 로비에서 도입 대사를 보여 준 뒤 전투로 보낸다
                if (battle.Dialogue.Count > 0)
                {
                    try
                    {
                        await _dialogue.PlayAsync(battle.Dialogue);
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }

                    if (this == null)
                    {
                        return;
                    }
                }

                await _launcher.Launch(battle.StageId);
                return;
            }

            if (!news.IsEmpty)
            {
                try
                {
                    await _levelUp.ShowAsync(news);
                }
                catch (OperationCanceledException)
                {
                    // 팝업을 닫기 전에 로비가 사라지면(재생 종료·씬 전환) 기다림이 취소된다. 오류가 아니므로 조용히 끝낸다
                    return;
                }
            }

            if (this == null)
            {
                return;
            }

            // 레벨업 화면 다음에, 새 컨텐츠(스킬 탭)가 열렸으면 해금 연출을 보여 준다
            if (tabWasLocked && !tabLocked)
            {
                try
                {
                    await _contentUnlock.ShowAsync(nav.SkillTabName, nav.SkillTabIcon);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                if (this == null)
                {
                    return;
                }
            }

            if (tabWasLocked && !tabLocked)
            {
                nav.SetSkillLocked(true);
                await nav.PlaySkillUnlockAsync();
                if (this == null)
                {
                    return;
                }
            }

            // 레벨업 직후 처음 시작되는 가이드에 대사 장면이 있으면 재생한다
            var starting = TutorialProgress.ActiveGuide(_data.Tutorials, TutorialFacts.From(_profile));
            if (!news.IsEmpty && starting != null && starting.Dialogue.Count > 0)
            {
                try
                {
                    await _dialogue.PlayAsync(starting.Dialogue);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }

            _ready = this != null;
            RefreshGuide();
        }

        private void OnProfileChanged(PlayerProfile _) => RefreshGuide();

        // 지금 가이드의 완료 조건을 채웠으면 완료를 저장하고, 아니면 진행 중인 가이드를 고른다
        private void RefreshGuide()
        {
            if (!_ready || _completing)
            {
                return;
            }

            var facts = TutorialFacts.From(_profile);
            if (_guide != null && TutorialProgress.IsCompletable(_guide, facts))
            {
                CompleteGuideAsync(_guide.Id).Forget(Debug.LogException);
                return;
            }

            var next = TutorialProgress.ActiveGuide(_data.Tutorials, facts);
            if (next == _guide)
            {
                return;
            }

            _guide = next;
            _steps = next != null ? LobbyTutorialScripts.Create(next, _ui) : null;
            if (next != null && _steps == null)
            {
                Debug.LogError($"[Tutorial] {next.Id}: Script {next.Script}의 단계 템플릿이 LobbyTutorialScripts에 없습니다", this);
            }
        }

        private async UniTask CompleteGuideAsync(string tutorialId)
        {
            _completing = true;
            Point(null, null);
            await _arrival.CompleteAsync(tutorialId);
            if (this == null)
            {
                return;
            }

            _completing = false;
            _guide = null;
            _steps = null;
            RefreshGuide();
        }

        private void LateUpdate()
        {
            if (_steps == null || _completing)
            {
                if (_root.activeSelf)
                {
                    Point(null, null);
                }

                return;
            }

            // Reached가 참인 마지막 단계를 강조한다
            int index = 0;
            for (int i = _steps.Count - 1; i > 0; i--)
            {
                if (_steps[i].Reached())
                {
                    index = i;
                    break;
                }
            }

            var lines = _guide.Lines;
            Point(_steps[index].Target(), lines[Mathf.Min(index, lines.Count - 1)]);
        }

        // target 위에 표시를 띄우고 그 버튼만 누를 수 있게 한다. target이 null이면 가린 것을 모두 거둔다
        private void Point(RectTransform target, string line)
        {
            bool shown = target != null;
            if (shown != _root.activeSelf)
            {
                _fade?.Kill();
                if (shown)
                {
                    // 새로 나타날 때는 구멍을 바로 대상에 두고 판만 부드럽게 나타낸다
                    _holeShown = false;
                    _shownLine = null;
                    _root.SetActive(true);
                    _rootGroup.alpha = 0f;
                    _fade = DOTween.To(() => _rootGroup.alpha, a => _rootGroup.alpha = a, 1f, 0.25f).SetUpdate(true).SetLink(gameObject);
                }
                else
                {
                    _root.SetActive(false);
                }
            }

            _blocker.Target = target;
            if (target == null)
            {
                return;
            }

            var area = (RectTransform)_pointer.parent;
            Camera ownCamera = TutorialRaycastHole.CameraOf(area);
            Camera targetCamera = TutorialRaycastHole.CameraOf(target);
            var corners = new Vector3[4];
            target.GetWorldCorners(corners);
            Vector2 Local(Vector3 world)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(area, RectTransformUtility.WorldToScreenPoint(targetCamera, world), ownCamera, out var local);
                return local;
            }

            Vector2 min = Local(corners[0]);
            Vector2 max = Local(corners[2]);
            // 구멍과 표시는 대상이 바뀌면 새 위치로 미끄러져 간다
            if (!_holeShown)
            {
                _holeMin = min;
                _holeMax = max;
                _holeShown = true;
            }
            else
            {
                float k = 1f - Mathf.Exp(-_holeFollow * Time.unscaledDeltaTime);
                _holeMin = Vector2.Lerp(_holeMin, min, k);
                _holeMax = Vector2.Lerp(_holeMax, max, k);
            }

            min = _holeMin;
            max = _holeMax;
            Vector2 center = (min + max) * 0.5f;

            Spotlight(min, max);
            TapPointer(center, (max - min) * 0.5f);

            // 대화창은 강조 대상과 반대쪽 절반에 둔다
            _dialog.localPosition = new Vector3(0f, center.y < 0f ? _dialogOffset : -_dialogOffset, 0f);
            if (line != _shownLine)
            {
                // 대사가 바뀌면 말풍선이 톡 튄다
                _shownLine = line;
                _dialogText.text = line;
                _dialog.DOKill();
                _dialog.localScale = Vector3.one;
                _dialog.DOPunchScale(Vector3.one * 0.08f, 0.25f, 6).SetUpdate(true).SetLink(_dialog.gameObject);
            }
        }

        // 대상 사각형(min~max)을 padding만큼 키워 둥글게 뚫고, 펄스 링은 손가락이 누르는 순간에 맞춰 퍼뜨린다
        private void Spotlight(Vector2 min, Vector2 max)
        {
            if (_spotlightMaterial == null)
            {
                return;
            }

            Vector2 center = (min + max) * 0.5f;
            Vector2 half = (max - min) * 0.5f + Vector2.one * _holePadding;
            _spotlightMaterial.SetVector(HoleId, new Vector4(center.x, center.y, half.x, half.y));
            _spotlightMaterial.SetVector(ParamsId, new Vector4(_holeRadius, _holeSoftness, 0f, 0f));
            float cycle = TapCycle();
            // 누르는 순간(0.5)부터 링이 퍼진다. 그 전에는 링을 숨긴다
            float progress = Mathf.Clamp01((cycle - PressAt) / (1f - PressAt));
            _spotlightMaterial.SetVector(RingId, new Vector4(progress, _ringSpread, _ringThickness, 0f));
            _spotlightMaterial.SetColor(RingColorId, cycle < PressAt ? new Color(_ringColor.r, _ringColor.g, _ringColor.b, 0f) : _ringColor);
        }

        private float TapCycle() => Mathf.Repeat(Time.unscaledTime / Mathf.Max(0.1f, _tapSeconds), 1f);

        // 손가락이 대상으로 다가가 누르고(살짝 작아짐) 물러나는 것을 반복한다
        private void TapPointer(Vector2 center, Vector2 half)
        {
            Vector2 size = _pointer.rect.size;
            Vector2 far = new Vector2(half.x * 0.5f + size.x * 0.9f, -half.y * 0.5f - size.y * 0.9f);
            Vector2 near = new Vector2(size.x * _pointerOffset, -size.y * _pointerOffset);
            float c = TapCycle();
            float t;
            float scale = 1f;
            if (c < PressAt - 0.05f)
            {
                t = Mathf.SmoothStep(0f, 1f, c / (PressAt - 0.05f));
            }
            else if (c < PressAt + 0.05f)
            {
                t = 1f;
                scale = 0.85f;
            }
            else
            {
                t = 1f - Mathf.SmoothStep(0f, 1f, (c - PressAt - 0.05f) / (1f - PressAt - 0.05f));
            }

            Vector2 offset = Vector2.Lerp(far, near, t);
            _pointer.localPosition = new Vector3(center.x + offset.x, center.y + offset.y, 0f);
            _pointer.localScale = Vector3.one * scale;
        }
    }
}
