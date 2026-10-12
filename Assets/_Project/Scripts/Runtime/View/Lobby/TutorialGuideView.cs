using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game.Core;
using TMPro;
using UnityEngine;
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
        }

        [SerializeField] private GameObject _root;
        [SerializeField] private TutorialRaycastHole _blocker;
        [Tooltip("강조 표시. 대상 오른쪽 위에서 위아래로 흔들린다 (나중에 연필 이미지로 교체)")]
        [SerializeField] private RectTransform _pointer;
        [Tooltip("강조 대상 둘레를 덮는 반투명 검정 판 4장 (위, 아래, 왼쪽, 오른쪽). 대상 자리만 비워 둔다")]
        [SerializeField] private RectTransform[] _dim;
        [SerializeField] private RectTransform _dialog;
        [SerializeField] private TMP_Text _dialogText;
        [Tooltip("표시 중심을 대상 오른쪽 위 꼭짓점에서 표시 크기의 몇 배만큼 바깥으로 둘지")]
        [SerializeField] private float _pointerOffset = 0.3f;
        [SerializeField] private float _bobHeight = 12f;
        [SerializeField] private float _bobSpeed = 4f;
        [Tooltip("대화창을 화면 가운데에서 위/아래로 띄우는 거리. 강조 대상과 반대쪽 절반에 둔다")]
        [SerializeField] private float _dialogOffset = 520f;

        private IUiRegistry _ui;
        private LevelUpPopupView _levelUp;
        private LobbyArrival _arrival;
        private PlayerProfile _profile;
        private GameDataStore _data;
        private BattleLauncher _launcher;
        private bool _ready;
        private bool _completing;
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

        protected override void InitializeView() => _root.SetActive(false);

        protected override void DisposeView()
        {
            if (_profile != null)
            {
                _profile.Changed -= OnProfileChanged;
            }
        }

        private void Start() => RunAsync().Forget(Debug.LogException);

        private async UniTask RunAsync()
        {
            var news = await _arrival.CollectAsync();
            if (this == null)
            {
                return;
            }

            int battleStage = TutorialProgress.BattleStage(_data.Tutorials, TutorialFacts.From(_profile));
            if (battleStage > 0)
            {
                await _launcher.Launch(battleStage);
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
            _root.SetActive(target != null);
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
            Vector2 center = (min + max) * 0.5f;

            float bob = (Mathf.Sin(Time.unscaledTime * _bobSpeed) + 1f) * 0.5f * _bobHeight;
            Vector2 size = _pointer.rect.size;
            _pointer.localPosition = new Vector3(max.x + size.x * _pointerOffset, max.y + size.y * _pointerOffset + bob, 0f);
            Dim(area.rect, min, max);

            // 대화창은 강조 대상과 반대쪽 절반에 둔다
            _dialog.localPosition = new Vector3(0f, center.y < 0f ? _dialogOffset : -_dialogOffset, 0f);
            _dialogText.text = line;
        }

        // 대상 사각형(min~max)만 비우고 둘레를 판 4장으로 덮는다. 판은 왼쪽 아래 기준(pivot 0,0)이다
        private void Dim(Rect area, Vector2 min, Vector2 max)
        {
            if (_dim == null || _dim.Length < 4)
            {
                return;
            }

            min = Vector2.Max(min, area.min);
            max = Vector2.Min(max, area.max);
            Place(_dim[0], new Vector2(area.xMin, max.y), new Vector2(area.xMax, area.yMax));
            Place(_dim[1], new Vector2(area.xMin, area.yMin), new Vector2(area.xMax, min.y));
            Place(_dim[2], new Vector2(area.xMin, min.y), new Vector2(min.x, max.y));
            Place(_dim[3], new Vector2(max.x, min.y), new Vector2(area.xMax, max.y));
        }

        private static void Place(RectTransform panel, Vector2 from, Vector2 to)
        {
            panel.localPosition = new Vector3(from.x, from.y, 0f);
            panel.sizeDelta = Vector2.Max(Vector2.zero, to - from);
        }
    }
}
