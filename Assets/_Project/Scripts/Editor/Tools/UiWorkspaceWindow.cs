using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.View;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.Editor
{
    public sealed class UiWorkspaceWindow : EditorWindow
    {
        private sealed class Item
        {
            public string Id;
            public string Title;
            public GameObject Prefab;
            public bool Standalone;
        }

        private const string Layout = "Assets/_Project/Scripts/Editor/Tools/UiWorkspace.uxml";
        private static readonly List<string> Groups = new() { "로딩 화면", "로비 화면", "전투 화면", "공통 요소" };
        private static readonly Dictionary<string, string> Titles = new()
        {
            ["LoadingScreen"] = "로딩 화면",
            ["TapToStart"] = "터치 시작",
            ["LoginView"] = "로그인 창",
            ["LobbyBackground"] = "홈 배경",
            ["LobbyTopBar"] = "상단 프로필 / 재화",
            ["LobbyMissionButton"] = "임무 입장 버튼",
            ["LobbyBottomNav"] = "하단 메뉴",
            ["LobbySideMenu"] = "홈 사이드 메뉴",
            ["LobbyEventBanner"] = "이벤트 배너",
            ["CharacterScreen"] = "캐릭터 화면",
            ["SkillScreen"] = "스킬 화면",
            ["MissionScreen"] = "임무 화면",
            ["EnergyRecoverPopup"] = "에너지 회복",
            ["SkillUpgradePopup"] = "스킬 강화",
            ["ProfilePopup"] = "프로필",
            ["EquipUpgradePopup"] = "장비 강화",
            ["RewardPopup"] = "보상",
            ["ComingSoonToast"] = "미완성 안내",
            ["HudCanvas"] = "전투 HUD",
            ["CardSelect"] = "카드 선택",
            ["PauseMenu"] = "일시정지",
            ["ResultPopup"] = "전투 결과",
            ["SubmittingOverlay"] = "결과 제출 중",
            ["SubmitRejectedPopup"] = "결과 제출 오류",
            ["CloseButton"] = "닫기 버튼",
            ["StatRow"] = "능력치 행",
            ["StatCell"] = "능력치 칸",
            ["LevelRewardRow"] = "레벨 보상 행",
            ["CardSlot"] = "카드 슬롯",
            ["SkillSlot"] = "전투 스킬 슬롯",
            ["SkillListSlot"] = "스킬 목록 슬롯",
            ["EquipSlot"] = "장비 슬롯",
            ["InventoryScreen"] = "인벤토리 화면",
            ["ShopScreen"] = "상점 화면",
            ["MissionDetailsPopup"] = "임무 상세",
            ["MissionRewardInfoPopup"] = "임무 보상 정보",
            ["LobbyMenuPopup"] = "로비 메뉴",
            ["SettingsPopup"] = "설정",
            ["UpgradeConfirmPopup"] = "강화 확인",
            ["DetailPopup"] = "상세 정보",
            ["FriendPopup"] = "친구",
            ["QuestPopup"] = "퀘스트",
            ["MailPopup"] = "우편함",
            ["RewardOddsPopup"] = "보상 확률 정보",
            ["ConfirmPopup"] = "확인 팝업",
            ["ContentUnlock"] = "콘텐츠 해금",
            ["DialogueScene"] = "대화 장면",
            ["LanguagePopup"] = "언어 선택",
            ["LevelUpBurst"] = "레벨 업 연출",
            ["LevelUpPopup"] = "레벨 업",
            ["TutorialGuide"] = "튜토리얼 안내",
            ["SkillHud"] = "스킬 HUD",
            ["StageControls"] = "전투 조작",
            ["EliteRoulette"] = "엘리트 룰렛",
            ["SkillRangeIndicator"] = "스킬 범위 표시",
            ["StarSparksFx"] = "별 반짝임 효과",
            ["CharacterCard"] = "캐릭터 카드",
            ["CurrencySlot"] = "재화 슬롯",
            ["ItemCell"] = "아이템 칸",
            ["MissionTab"] = "임무 탭",
            ["MonsterList"] = "몬스터 목록",
            ["NavTab"] = "하단 탭",
            ["NotificationBadge"] = "알림 배지",
            ["RewardList"] = "보상 목록",
            ["RewardNode"] = "보상 노드",
            ["SideMenuButton"] = "사이드 메뉴 버튼",
            ["SubTab"] = "보조 탭",
            ["TransitionCurtain"] = "화면 전환 막"
        };

        [SerializeField] private int _group;
        [SerializeField] private string _selectedId;
        [SerializeField] private int _device;
        private UiComposition _composition;
        private List<Item> _items = new();
        private List<Item> _filtered = new();
        private ListView _list;
        private DropdownField _sample;
        private ToolbarSearchField _search;
        private Image _image;
        private Texture2D _shot;
        private Item _selected;
        private UiPreviewRenderer.Session _preview;
        private DropdownField _motionButton;
        private bool _motionPlaying;
        private double _lastFrame;

        [MenuItem("Tools/Project Nova/UI 작업실", priority = 20)]
        public static void Open() => GetWindow<UiWorkspaceWindow>("UI 작업실");

        private void OnEnable()
        {
            minSize = new Vector2(850, 650);
            EditorApplication.update += UpdateMotion;
            EditorApplication.projectChanged += Refresh;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private void OnDisable()
        {
            EditorApplication.update -= UpdateMotion;
            EditorApplication.projectChanged -= Refresh;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            ClearShot();
        }

        public void CreateGUI()
        {
            rootVisualElement.Clear();
            AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(Layout).CloneTree(rootVisualElement);
            var group = rootVisualElement.Q<DropdownField>("groupField");
            group.choices = Groups;
            group.SetValueWithoutNotify(Groups[Mathf.Clamp(_group, 0, Groups.Count - 1)]);
            group.RegisterValueChangedCallback(e => { _group = Groups.IndexOf(e.newValue); _selectedId = null; Refresh(); });
            _search = rootVisualElement.Q<ToolbarSearchField>("searchField");
            _search.RegisterValueChangedCallback(_ => Filter());
            _list = rootVisualElement.Q<ListView>("assetList");
            _list.fixedItemHeight = 30;
            _list.makeItem = () => { var label = new Label(); label.AddToClassList("asset-name"); return label; };
            _list.bindItem = (element, i) => ((Label)element).text = _filtered[i].Title;
            _list.selectionChanged += items => Select(items.OfType<Item>().FirstOrDefault());
            _list.itemsChosen += _ => Edit();
            _image = rootVisualElement.Q<Image>("previewImage");
            _image.scaleMode = ScaleMode.ScaleToFit;
            rootVisualElement.RegisterCallback<GeometryChangedEvent>(_ => FitPreview());
            var device = rootVisualElement.Q<DropdownField>("deviceField");
            device.choices = UiPreviewRenderer.Devices.Select(d => d.Name).ToList();
            device.index = Mathf.Clamp(_device, 0, device.choices.Count - 1);
            device.RegisterValueChangedCallback(_ => { _device = device.index; Render(); });
            _sample = rootVisualElement.Q<DropdownField>("sampleField");
            _sample.RegisterValueChangedCallback(_ => Render());
            rootVisualElement.Q<Toggle>("safeAreaToggle").RegisterValueChangedCallback(_ => Render());
            Button("editButton", Edit);
            Button("locateButton", () => { if (_selected?.Prefab != null) { EditorGUIUtility.PingObject(_selected.Prefab); } });
            Button("compositionButton", () => Selection.activeObject = _composition);
            Button("refreshButton", Refresh);
            Button("previewButton", Render);
            Button("saveButton", Save);
            Button("auditButton", Audit);
            _motionButton = rootVisualElement.Q<DropdownField>("motionButtonField");
            Button("showMotionButton", () => PlayMotion(() => _preview.Transition(true)));
            Button("hideMotionButton", () => PlayMotion(() => _preview.Transition(false)));
            Button("pressMotionButton", () => PlayMotion(() => _preview.Press(_motionButton.value)));
            Button("particleMotionButton", () => PlayMotion(() => _preview.PlayParticles()));
            Button("resetMotionButton", Render);
            Refresh();
        }

        // 미리보기가 원본 해상도(1080x1920)로 커지지 않도록, 창 높이에 맞춰 한눈에 보이는 크기로 고정한다
        private void FitPreview()
        {
            if (_image == null) { return; }
            float height = rootVisualElement.resolvedStyle.height;
            if (float.IsNaN(height) || height <= 0f) { return; }
            _image.style.flexGrow = 0f;
            _image.style.height = Mathf.Clamp(height - 330f, 360f, 1400f);
        }

        private void Button(string name, Action action) => rootVisualElement.Q<Button>(name).clicked += action;
        private void OnPlayModeChanged(PlayModeStateChange _) => Refresh();

        private void Refresh()
        {
            if (_list == null)
            {
                return;
            }

            string folder = _group switch { 0 => "Loading", 1 => "Lobby", 2 => "Stage", _ => "Common" };
            _composition = _group < 3 ? AssetDatabase.LoadAssetAtPath<UiComposition>(
                $"Assets/_Project/Settings/UI/{folder}Ui.asset") : null;
            var registered = _composition != null
                ? _composition.Entries.Where(e => e != null).ToList() : new List<UiComposition.Entry>();
            _items = registered.Select(e => new Item { Id = e.Id ?? "이름 없음", Prefab = e.Prefab, Title = Title(e.Id ?? "이름 없음") }).ToList();
            // 화면 구성에 등록되지 않은 프리팹도 목록에 보여 준다 (단독 미리보기)
            foreach (var path in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project/Prefabs/UI/" + folder })
                .Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                string id = Path.GetFileNameWithoutExtension(path);
                if (prefab == null || registered.Any(e => e.Prefab == prefab || e.Id == id)) { continue; }
                _items.Add(new Item { Id = id, Title = Title(id), Prefab = prefab, Standalone = _composition != null });
            }
            rootVisualElement.Q<Button>("compositionButton").SetEnabled(_composition != null);
            rootVisualElement.Q<Label>("auditSummary").text = "변경 후에는 다시 검사하세요.";
            rootVisualElement.Q<ScrollView>("issueList").Clear();
            Filter();
        }

        private static string Title(string id) => id != null && Titles.TryGetValue(id, out var title) ? title : id;

        private void Filter()
        {
            string query = _search.value ?? "";
            _filtered = _items.Where(i => i.Title.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
                || i.Id.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            _list.itemsSource = _filtered;
            _list.Rebuild();
            if (_filtered.Count == 0) { _list.ClearSelection(); Select(null); return; }
            int index = _filtered.FindIndex(i => i.Id == _selectedId);
            index = index < 0 ? 0 : index;
            _list.SetSelectionWithoutNotify(new[] { index });
            Select(_filtered[index]);
        }

        private void Select(Item item)
        {
            _selected = item;
            _selectedId = item?.Id;
            rootVisualElement.Q<Label>("assetTitle").text = item?.Title ?? "일치하는 화면이 없습니다.";
            rootVisualElement.Q<Label>("assetPath").text = item?.Prefab != null ? AssetDatabase.GetAssetPath(item.Prefab) : "";
            rootVisualElement.Q<Button>("editButton").SetEnabled(item?.Prefab != null);
            rootVisualElement.Q<Button>("locateButton").SetEnabled(item?.Prefab != null);
            _sample.choices = UiPreviewRenderer.Samples(item?.Id);
            _sample.SetValueWithoutNotify(_sample.choices[0]);
            Render();
        }

        private void Edit()
        {
            if (_selected?.Prefab != null)
            {
                AssetDatabase.OpenAsset(_selected.Prefab);
            }
        }

        private void ClearShot()
        {
            _motionPlaying = false;
            _preview?.Dispose();
            _preview = null;
            if (_image != null)
            {
                _image.image = null;
            }

            if (_shot != null)
            {
                DestroyImmediate(_shot);
            }

            _shot = null;
        }

        private void Render()
        {
            ClearShot();
            bool canRender = _selected?.Prefab != null && !EditorApplication.isPlayingOrWillChangePlaymode;
            rootVisualElement.Q<Button>("previewButton").SetEnabled(canRender);
            rootVisualElement.Q<Button>("saveButton").SetEnabled(false);
            SetMotionControls(false);
            var status = rootVisualElement.Q<Label>("statusLabel");
            status.RemoveFromClassList("status-error");
            if (!canRender)
            {
                status.text = EditorApplication.isPlayingOrWillChangePlaymode
                ? "재생을 종료하면 편집 미리보기가 다시 표시됩니다." : "화면을 선택하세요."; return;
            }
            try
            {
                _preview = UiPreviewRenderer.CreateSession(_selected.Prefab, _selected.Standalone ? null : _composition, UiPreviewRenderer.Devices[_device],
                    rootVisualElement.Q<Toggle>("safeAreaToggle").value, _sample.value, _selected.Id);
                _shot = _preview.Capture();
                _image.image = _shot;
                _motionButton.choices = _preview.Buttons.ToList();
                _motionButton.SetValueWithoutNotify(_motionButton.choices.FirstOrDefault() ?? "");
                SetMotionControls(true);
                rootVisualElement.Q<Button>("saveButton").SetEnabled(true);
                status.text = _composition != null && !_selected.Standalone ? "호스트의 레이어·순서와 9:16 카메라 영역을 적용했습니다." : "단독 미리보기 (화면 구성에 등록되지 않은 프리팹)";
            }
            catch (Exception error) { ClearShot(); SetMotionControls(false); status.text = error.Message; status.AddToClassList("status-error"); Debug.LogException(error); }
        }

        private void SetMotionControls(bool available)
        {
            rootVisualElement.Q<Button>("showMotionButton").SetEnabled(available && _preview.HasTransition);
            rootVisualElement.Q<Button>("hideMotionButton").SetEnabled(available && _preview.HasTransition);
            rootVisualElement.Q<Button>("pressMotionButton").SetEnabled(available && _preview.Buttons.Length > 0);
            rootVisualElement.Q<Button>("particleMotionButton").SetEnabled(available && _preview.HasParticles);
            rootVisualElement.Q<Button>("resetMotionButton").SetEnabled(available);
            _motionButton?.SetEnabled(available && _preview.Buttons.Length > 0);
        }

        private void PlayMotion(Action action)
        {
            if (_preview == null || EditorApplication.isPlayingOrWillChangePlaymode) { return; }
            try
            {
                action();
                _motionPlaying = true;
                _lastFrame = EditorApplication.timeSinceStartup;
                _preview.Capture(_shot);
                _image.MarkDirtyRepaint();
                rootVisualElement.Q<Label>("statusLabel").text = "편집 연출 미리보기 재생 중 · 초기화로 중단 / 원상 복귀";
            }
            catch (Exception error)
            {
                Render();
                Debug.LogException(error);
            }
        }

        private void UpdateMotion()
        {
            if (!_motionPlaying || _preview == null) { return; }
            if (EditorApplication.isPlayingOrWillChangePlaymode) { ClearShot(); return; }
            double now = EditorApplication.timeSinceStartup;
            if (now - _lastFrame < 1.0 / 24.0) { return; }
            float elapsed = (float)(now - _lastFrame);
            _lastFrame = now;
            try
            {
                _preview.Advance(Mathf.Min(elapsed, 0.1f));
                _preview.Capture(_shot);
                _image.MarkDirtyRepaint();
                Repaint();
                if (!_preview.IsAnimating)
                {
                    _motionPlaying = false;
                    rootVisualElement.Q<Label>("statusLabel").text = "편집 연출 미리보기 완료 · 열기로 다시 재생 / 초기화로 원상 복귀";
                }
            }
            catch (Exception error)
            {
                Render();
                Debug.LogException(error);
            }
        }

        private void Save()
        {
            if (_shot == null)
            {
                return;
            }

            var path = EditorUtility.SaveFilePanel("UI 미리보기 저장", "", _selected.Id + ".png", "png");
            if (!string.IsNullOrEmpty(path))
            {
                File.WriteAllBytes(path, _shot.EncodeToPNG());
            }
        }

        private void Audit()
        {
            var issues = UiAssetAudit.InspectAll();
            var list = rootVisualElement.Q<ScrollView>("issueList");
            list.Clear();
            int errors = issues.Count(i => i.IsError);
            rootVisualElement.Q<Label>("auditSummary").text = $"오류 {errors}개 · 검토 {issues.Count - errors}개";
            foreach (var issue in issues)
            {
                var button = new Button(() => UiAssetAudit.Open(issue))
                {
                    text = $"{(issue.IsError ? "오류" : "검토")} · {Path.GetFileNameWithoutExtension(issue.AssetPath)} / {issue.ObjectPath}\n{issue.Message}",
                    tooltip = issue.AssetPath
                };
                button.AddToClassList("issue-button");
                list.Add(button);
            }
        }
    }
}
