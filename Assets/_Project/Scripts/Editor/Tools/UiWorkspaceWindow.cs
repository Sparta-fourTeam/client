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
        }

        private const string Layout = "Assets/_Project/Scripts/Editor/Tools/UiWorkspace.uxml";
        private static readonly List<string> Groups = new() { "로비 화면", "전투 화면", "공통 요소" };
        private static readonly Dictionary<string, string> Titles = new()
        {
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
            ["EquipSlot"] = "장비 슬롯"
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

        [MenuItem("Tools/Project Nova/UI Workspace", priority = 20)]
        public static void Open() => GetWindow<UiWorkspaceWindow>("UI Workspace");

        private void OnEnable()
        {
            minSize = new Vector2(850, 650);
            EditorApplication.projectChanged += Refresh;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private void OnDisable()
        {
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
            Refresh();
        }

        private void Button(string name, Action action) => rootVisualElement.Q<Button>(name).clicked += action;
        private void OnPlayModeChanged(PlayModeStateChange _) => Refresh();

        private void Refresh()
        {
            if (_list == null)
            {
                return;
            }

            _composition = _group < 2 ? AssetDatabase.LoadAssetAtPath<UiComposition>(
                $"Assets/_Project/Settings/UI/{(_group == 0 ? "Lobby" : "Stage")}Ui.asset") : null;
            _items = _composition != null
                ? _composition.Entries.Where(e => e != null).Select(e => new Item { Id = e.Id ?? "이름 없음", Prefab = e.Prefab, Title = Title(e.Id ?? "이름 없음") }).ToList()
                : _group == 2 ? AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project/Prefabs/UI/Common" })
                    .Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p)
                    .Select(p => new Item
                    {
                        Id = Path.GetFileNameWithoutExtension(p),
                        Title = Title(Path.GetFileNameWithoutExtension(p)),
                        Prefab = AssetDatabase.LoadAssetAtPath<GameObject>(p)
                    }).ToList() : new List<Item>();
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
            var status = rootVisualElement.Q<Label>("statusLabel");
            status.RemoveFromClassList("status-error");
            if (!canRender)
            {
                status.text = EditorApplication.isPlayingOrWillChangePlaymode
                ? "재생을 종료하면 편집 미리보기가 다시 표시됩니다." : "화면을 선택하세요."; return;
            }
            try
            {
                _shot = UiPreviewRenderer.Capture(_selected.Prefab, _composition, UiPreviewRenderer.Devices[_device],
                    rootVisualElement.Q<Toggle>("safeAreaToggle").value, _sample.value, _selected.Id);
                _image.image = _shot;
                rootVisualElement.Q<Button>("saveButton").SetEnabled(true);
                status.text = _composition != null ? "호스트의 레이어·순서와 9:16 카메라 영역을 적용했습니다." : "공통 요소 단독 미리보기";
            }
            catch (Exception error) { status.text = error.Message; status.AddToClassList("status-error"); Debug.LogException(error); }
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
