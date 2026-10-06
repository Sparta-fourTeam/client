using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using UnityEngine;

namespace Game.Sandbox
{
    /// <summary>
    /// 샌드박스 디버그 패널.
    /// IMGUI 기반의 드래그 가능한 플로팅 윈도우로 동작한다.
    /// 1080x1920 세로 Game View 기준으로 큼직하게 구성한다.
    /// </summary>
    public sealed class SandboxPanel
    {
        private const int WindowId = 18421;

        private const float DefaultWindowWidth = 640f;
        private const float DefaultWindowHeight = 1050f;
        private const float MinimizedHeight = 64f;

        private const float HeaderHeight = 52f;
        private const float SectionHeight = 48f;
        private const float ControlHeight = 58f;
        private const float SkillButtonHeight = 72f;
        private const float CardButtonHeight = 90f;

        private readonly SkillSandboxSession session;
        private readonly SandboxEnemyField enemies;
        private readonly Font font;

        private GUIStyle title;
        private GUIStyle section;
        private GUIStyle button;
        private GUIStyle buttonOn;
        private GUIStyle label;
        private GUIStyle muted;
        private GUIStyle error;
        private GUIStyle box;
        private GUIStyle window;
        private GUIStyle small;

        private bool stylesReady;
        private bool windowInitialized;

        private Rect windowRect;
        private Vector2 scroll;

        private bool minimized;

        private bool showSkills = true;
        private bool showCards = true;
        private bool showEnemies = true;
        private bool showEnvironment;
        private bool showStats;

        private bool realMode;
        private bool clickPlace = true;

        private SandboxEnemyField.Pattern pattern =
            SandboxEnemyField.Pattern.Cluster;

        private int enemyHp = 1000;

        private List<UpgradeChoice> choices = new();
        private List<SkillSandboxSession.CardInfo> cards = new();

        private float nextStats;
        private string statsText = string.Empty;

        public SandboxPanel(
            SkillSandboxSession session,
            SandboxEnemyField enemies,
            Font font)
        {
            this.session = session;
            this.enemies = enemies;
            this.font = font;

            session.Changed += OnChanged;

            OnChanged();
        }

        public void Dispose()
        {
            session.Changed -= OnChanged;
        }

        private void OnChanged()
        {
            cards = session.Cards();

            if (realMode)
            {
                choices = session.RollChoices();
            }

            nextStats = 0f;
        }

        // ─────────────────────────────────────────────────────────────
        // GUI
        // ─────────────────────────────────────────────────────────────

        public void OnGUI()
        {
            EnsureStyles();
            EnsureWindowRect();

            float width = Mathf.Min(
                DefaultWindowWidth,
                Mathf.Max(320f, Screen.width - 20f));

            float expandedHeight = Mathf.Min(
                DefaultWindowHeight,
                Mathf.Max(300f, Screen.height - 20f));

            windowRect.width = width;
            windowRect.height = minimized
                ? MinimizedHeight
                : expandedHeight;

            ClampWindowToScreen();

            windowRect = GUI.Window(
                WindowId,
                windowRect,
                DrawWindow,
                GUIContent.none,
                window);

            HandleWorldClick();
        }

        private void DrawWindow(int id)
        {
            DrawWindowHeader();

            if (minimized)
            {
                GUI.DragWindow(
                    new Rect(
                        0f,
                        0f,
                        Mathf.Max(0f, windowRect.width - 70f),
                        MinimizedHeight));

                return;
            }

            UpdateStatsText();

            scroll = GUILayout.BeginScrollView(
                scroll,
                GUILayout.ExpandHeight(true));

            if (!string.IsNullOrEmpty(session.LastError))
            {
                GUILayout.Label(session.LastError, error);
                GUILayout.Space(8f);
            }

            DrawSection(
                "스킬",
                ref showSkills,
                DrawSkills);

            DrawSection(
                "카드",
                ref showCards,
                DrawCards);

            DrawSection(
                "적",
                ref showEnemies,
                DrawEnemies);

            DrawSection(
                "환경",
                ref showEnvironment,
                DrawEnvironment);

            DrawSection(
                "현재 스킬 정보",
                ref showStats,
                () =>
                {
                    GUILayout.Label(
                        string.IsNullOrEmpty(statsText)
                            ? "정보 없음"
                            : statsText,
                        muted);
                });

            GUILayout.Space(30f);

            GUILayout.EndScrollView();

            GUI.DragWindow(
                new Rect(
                    0f,
                    0f,
                    Mathf.Max(0f, windowRect.width - 70f),
                    HeaderHeight));
        }

        private void DrawWindowHeader()
        {
            GUILayout.BeginHorizontal(
                GUILayout.Height(HeaderHeight));

            GUILayout.Label(
                "Skill Sandbox",
                title,
                GUILayout.ExpandWidth(true));

            if (GUILayout.Button(
                    minimized ? "+" : "−",
                    button,
                    GUILayout.Width(56f),
                    GUILayout.Height(48f)))
            {
                minimized = !minimized;
            }

            GUILayout.EndHorizontal();

            if (!minimized)
            {
                GUILayout.Space(8f);
            }
        }

        private void DrawSection(
            string name,
            ref bool expanded,
            Action drawer)
        {
            if (GUILayout.Button(
                    expanded
                        ? $"▼  {name}"
                        : $"▶  {name}",
                    section,
                    GUILayout.Height(SectionHeight)))
            {
                expanded = !expanded;
            }

            if (!expanded)
            {
                GUILayout.Space(4f);
                return;
            }

            GUILayout.BeginVertical(box);

            drawer();

            GUILayout.EndVertical();

            GUILayout.Space(10f);
        }

        private void UpdateStatsText()
        {
            if (Time.unscaledTime < nextStats)
            {
                return;
            }

            nextStats = Time.unscaledTime + .25f;

            statsText =
                session.StatsText()
                + $"\n\n적 {enemies.AliveCount}마리"
                + $" / 누적 피해 {enemies.TotalDamage:N0}"
                + $" / 최근 3초 초당 {enemies.RecentDps():N1}";
        }

        private void EnsureWindowRect()
        {
            if (windowInitialized)
            {
                return;
            }

            windowInitialized = true;

            float width = Mathf.Min(
                DefaultWindowWidth,
                Mathf.Max(320f, Screen.width - 20f));

            float height = Mathf.Min(
                DefaultWindowHeight,
                Mathf.Max(300f, Screen.height - 20f));

            windowRect = new Rect(
                20f,
                20f,
                width,
                height);
        }

        private void ClampWindowToScreen()
        {
            windowRect.x = Mathf.Clamp(
                windowRect.x,
                0f,
                Mathf.Max(
                    0f,
                    Screen.width - windowRect.width));

            windowRect.y = Mathf.Clamp(
                windowRect.y,
                0f,
                Mathf.Max(
                    0f,
                    Screen.height - windowRect.height));
        }

        private void HandleWorldClick()
        {
            if (!clickPlace)
            {
                return;
            }

            var e = Event.current;

            if (e.type != EventType.MouseDown ||
                e.button != 0)
            {
                return;
            }

            if (windowRect.Contains(e.mousePosition))
            {
                return;
            }

            PlaceEnemyAt(e.mousePosition);
        }

        // ─────────────────────────────────────────────────────────────
        // Skills
        // ─────────────────────────────────────────────────────────────

        private void DrawSkills()
        {
            const int perRow = 2;

            var skills = session.Skills;

            for (int i = 0; i < skills.Count; i += perRow)
            {
                GUILayout.BeginHorizontal();

                for (int j = i;
                     j < Mathf.Min(i + perRow, skills.Count);
                     j++)
                {
                    var skill = skills[j];

                    string text = skill.HasPrefab
                        ? skill.Name
                        : skill.Name + "\n(프리팹 연결 없음)";

                    GUI.enabled = skill.HasPrefab;

                    if (GUILayout.Button(
                            text,
                            skill.Id == session.Build.skillId
                                ? buttonOn
                                : button,
                            GUILayout.Height(SkillButtonHeight)))
                    {
                        session.SelectSkill(skill.Id);
                    }

                    GUI.enabled = true;
                }

                GUILayout.EndHorizontal();

                GUILayout.Space(6f);
            }
        }

        // ─────────────────────────────────────────────────────────────
        // Cards
        // ─────────────────────────────────────────────────────────────

        private void DrawCards()
        {
            GUILayout.BeginHorizontal();

            if (GUILayout.Button(
                    "직접 선택",
                    realMode ? button : buttonOn,
                    GUILayout.Height(ControlHeight)))
            {
                realMode = false;
            }

            if (GUILayout.Button(
                    "실제 3지선다",
                    realMode ? buttonOn : button,
                    GUILayout.Height(ControlHeight)))
            {
                realMode = true;
                choices = session.RollChoices();
            }

            GUILayout.EndHorizontal();

            GUILayout.Space(8f);

            if (session.Build.skillId == 0)
            {
                GUILayout.Label(
                    "위에서 스킬을 고르세요",
                    muted);

                return;
            }

            GUILayout.BeginHorizontal();

            if (GUILayout.Button(
                    "카드 모두 비우기",
                    button,
                    GUILayout.Height(ControlHeight)))
            {
                session.ClearCards();
            }

            if (realMode &&
                GUILayout.Button(
                    "다시 뽑기",
                    button,
                    GUILayout.Height(ControlHeight)))
            {
                choices = session.RollChoices();
            }

            GUILayout.EndHorizontal();

            GUILayout.Space(8f);

            GUILayout.Label(
                "적용한 카드: "
                + (session.Build.cards.Count == 0
                    ? "없음"
                    : string.Join(
                        ", ",
                        session.Build.cards)),
                small);

            GUILayout.Space(10f);

            if (realMode)
            {
                DrawChoices();
            }
            else
            {
                DrawCardList();
            }
        }

        private void DrawChoices()
        {
            if (choices.Count == 0)
            {
                GUILayout.Label(
                    "지금 고를 수 있는 카드가 없습니다.\n"
                    + "(조건 미충족 또는 모두 최대)",
                    muted);

                return;
            }

            foreach (var choice in choices.ToList())
            {
                if (GUILayout.Button(
                        $"{choice.DisplayName}\n"
                        + choice.DisplayDescription,
                        button,
                        GUILayout.MinHeight(CardButtonHeight)))
                {
                    session.Pick(choice);
                    choices = session.RollChoices();
                }

                GUILayout.Space(6f);
            }
        }

        private void DrawCardList()
        {
            foreach (var card in cards.ToList())
            {
                GUILayout.BeginHorizontal();

                string text =
                    $"{card.Name}   "
                    + $"{card.Count}/{card.MaxPick}\n"
                    + card.Description;

                if (card.Blocked != null)
                {
                    text += $"\n⚠ {card.Blocked}";
                }

                GUI.enabled = card.Blocked == null;

                if (GUILayout.Button(
                        text,
                        button,
                        GUILayout.MinHeight(CardButtonHeight)))
                {
                    session.TryAddCard(
                        card.Id,
                        out _);
                }

                GUI.enabled = card.Count > 0;

                if (GUILayout.Button(
                        "−",
                        button,
                        GUILayout.Width(64f),
                        GUILayout.MinHeight(CardButtonHeight)))
                {
                    session.RemoveCard(card.Id);
                }

                GUI.enabled = true;

                GUILayout.EndHorizontal();

                GUILayout.Space(6f);
            }
        }

        // ─────────────────────────────────────────────────────────────
        // Enemies
        // ─────────────────────────────────────────────────────────────

        private void DrawEnemies()
        {
            GUILayout.BeginHorizontal();

            foreach (int count in new[]
                     {
                         1,
                         3,
                         5,
                         10
                     })
            {
                if (GUILayout.Button(
                        $"샌드백 {count}",
                        button,
                        GUILayout.Height(ControlHeight)))
                {
                    enemies.SpawnPattern(
                        pattern,
                        count,
                        enemyHp,
                        new Vector2(0f, 2.2f));
                }
            }

            GUILayout.EndHorizontal();

            GUILayout.Space(8f);

            GUILayout.BeginHorizontal();

            foreach (SandboxEnemyField.Pattern value
                     in Enum.GetValues(
                         typeof(SandboxEnemyField.Pattern)))
            {
                string name =
                    value == SandboxEnemyField.Pattern.Line
                        ? "직선"
                        : value ==
                          SandboxEnemyField.Pattern.Cluster
                            ? "군집"
                            : "흩뿌림";

                if (GUILayout.Button(
                        "배치: " + name,
                        pattern == value
                            ? buttonOn
                            : button,
                        GUILayout.Height(ControlHeight)))
                {
                    pattern = value;
                }
            }

            GUILayout.EndHorizontal();

            GUILayout.Space(8f);

            GUILayout.BeginHorizontal();

            foreach (int hp in new[]
                     {
                         100,
                         1000,
                         10000
                     })
            {
                if (GUILayout.Button(
                        $"HP {hp:N0}",
                        enemyHp == hp
                            ? buttonOn
                            : button,
                        GUILayout.Height(ControlHeight)))
                {
                    enemyHp = hp;
                }
            }

            GUILayout.EndHorizontal();

            GUILayout.Space(8f);

            GUILayout.BeginHorizontal();

            if (GUILayout.Button(
                    $"이동 {(enemies.Moving ? "켬" : "끔")}",
                    enemies.Moving
                        ? buttonOn
                        : button,
                    GUILayout.Height(ControlHeight)))
            {
                enemies.Moving = !enemies.Moving;
            }

            if (GUILayout.Button(
                    $"되살림 {(enemies.Respawn ? "켬" : "끔")}",
                    enemies.Respawn
                        ? buttonOn
                        : button,
                    GUILayout.Height(ControlHeight)))
            {
                enemies.Respawn = !enemies.Respawn;
            }

            GUILayout.EndHorizontal();

            GUILayout.Space(8f);

            if (GUILayout.Button(
                    $"클릭 배치 {(clickPlace ? "켬" : "끔")}",
                    clickPlace
                        ? buttonOn
                        : button,
                    GUILayout.Height(ControlHeight)))
            {
                clickPlace = !clickPlace;
            }

            GUILayout.Space(8f);

            GUILayout.BeginHorizontal();

            if (GUILayout.Button(
                    "모두 제거",
                    button,
                    GUILayout.Height(ControlHeight)))
            {
                enemies.Clear();
            }

            if (GUILayout.Button(
                    "피해 집계 초기화",
                    button,
                    GUILayout.Height(ControlHeight)))
            {
                enemies.ResetDamage();
            }

            GUILayout.EndHorizontal();
        }

        // ─────────────────────────────────────────────────────────────
        // Environment
        // ─────────────────────────────────────────────────────────────

        private void DrawEnvironment()
        {
            GUILayout.BeginHorizontal();

            if (GUILayout.Button(
                    "−",
                    button,
                    GUILayout.Width(64f),
                    GUILayout.Height(ControlHeight)))
            {
                session.SetPermanentLevel(
                    session.Build.permanentLevel - 1);
            }

            GUILayout.Label(
                $"영구 레벨 {session.Build.permanentLevel}",
                label,
                GUILayout.ExpandWidth(true),
                GUILayout.Height(ControlHeight));

            if (GUILayout.Button(
                    "+",
                    button,
                    GUILayout.Width(64f),
                    GUILayout.Height(ControlHeight)))
            {
                session.SetPermanentLevel(
                    session.Build.permanentLevel + 1);
            }

            if (GUILayout.Button(
                    "+5",
                    button,
                    GUILayout.Width(72f),
                    GUILayout.Height(ControlHeight)))
            {
                session.SetPermanentLevel(
                    session.Build.permanentLevel + 5);
            }

            GUILayout.EndHorizontal();

            GUILayout.Space(8f);

            GUILayout.BeginHorizontal();

            foreach (float speed in new[]
                     {
                         .25f,
                         .5f,
                         1f,
                         2f
                     })
            {
                if (GUILayout.Button(
                        $"{speed}x",
                        Mathf.Approximately(
                            Time.timeScale,
                            speed)
                            ? buttonOn
                            : button,
                        GUILayout.Height(ControlHeight)))
                {
                    Time.timeScale = speed;
                }
            }

            GUILayout.EndHorizontal();

            GUILayout.Space(8f);

            GUILayout.BeginHorizontal();

            for (int i = 1; i <= 3; i++)
            {
                string slot = i.ToString();

                if (GUILayout.Button(
                        $"저장 {slot}",
                        button,
                        GUILayout.Height(ControlHeight)))
                {
                    session.SavePreset(slot);
                }

                GUI.enabled =
                    SandboxPresets.Exists(slot);

                if (GUILayout.Button(
                        $"불러오기 {slot}",
                        button,
                        GUILayout.Height(ControlHeight)))
                {
                    session.LoadPreset(slot);
                }

                GUI.enabled = true;
            }

            GUILayout.EndHorizontal();
        }

        // ─────────────────────────────────────────────────────────────
        // World click
        // ─────────────────────────────────────────────────────────────

        private void PlaceEnemyAt(
            Vector2 guiPosition)
        {
            var camera = Camera.main;

            if (camera == null)
            {
                return;
            }

            var screen = new Vector3(
                guiPosition.x,
                Screen.height - guiPosition.y,
                0f);

            Vector3 world =
                camera.ScreenToWorldPoint(screen);

            enemies.Spawn(
                new Vector2(
                    world.x,
                    world.y),
                enemyHp);
        }

        // ─────────────────────────────────────────────────────────────
        // Styles
        // ─────────────────────────────────────────────────────────────

        private void EnsureStyles()
        {
            if (stylesReady)
            {
                return;
            }

            stylesReady = true;

            GUIStyle Make(
                int size,
                Color color,
                FontStyle fontStyle = FontStyle.Normal,
                TextAnchor anchor = TextAnchor.MiddleLeft)
            {
                var style =
                    new GUIStyle(GUI.skin.label)
                    {
                        fontSize = size,
                        fontStyle = fontStyle,
                        alignment = anchor,
                        wordWrap = true
                    };

                style.normal.textColor = color;

                if (font != null)
                {
                    style.font = font;
                }

                return style;
            }

            title = Make(
                28,
                Color.white,
                FontStyle.Bold);

            label = Make(
                22,
                Color.white);

            muted = Make(
                19,
                new Color(.72f, .74f, .8f),
                anchor: TextAnchor.UpperLeft);

            small = Make(
                18,
                new Color(.72f, .74f, .8f));

            error = Make(
                21,
                new Color(1f, .55f, .5f));

            GUIStyle Button(Color color)
            {
                var style =
                    new GUIStyle(GUI.skin.button)
                    {
                        fontSize = 21,
                        wordWrap = true,
                        alignment =
                            TextAnchor.MiddleCenter,

                        padding =
                            new RectOffset(
                                10,
                                10,
                                8,
                                8)
                    };

                style.normal.textColor =
                    Color.white;

                style.hover.textColor =
                    Color.white;

                style.active.textColor =
                    Color.white;

                style.normal.background =
                    style.hover.background =
                    style.active.background =
                        Texture(color);

                if (font != null)
                {
                    style.font = font;
                }

                return style;
            }

            button = Button(
                new Color(
                    .22f,
                    .24f,
                    .3f,
                    1f));

            buttonOn = Button(
                new Color(
                    .2f,
                    .55f,
                    .85f,
                    1f));

            section =
                new GUIStyle(button)
                {
                    fontSize = 22,

                    alignment =
                        TextAnchor.MiddleLeft,

                    fontStyle =
                        FontStyle.Bold,

                    padding =
                        new RectOffset(
                            16,
                            12,
                            10,
                            10)
                };

            box =
                new GUIStyle(GUI.skin.box);

            box.padding =
                new RectOffset(
                    12,
                    12,
                    12,
                    12);

            box.margin =
                new RectOffset(
                    0,
                    0,
                    4,
                    4);

            box.normal.background =
                Texture(
                    new Color(
                        .08f,
                        .085f,
                        .1f,
                        .88f));

            window =
                new GUIStyle(GUI.skin.window)
                {
                    padding =
                        new RectOffset(
                            14,
                            14,
                            10,
                            14)
                };

            window.normal.background =
                Texture(
                    new Color(
                        .06f,
                        .065f,
                        .08f,
                        .95f));
        }

        private static Texture2D Texture(
            Color color)
        {
            var texture =
                new Texture2D(1, 1)
                {
                    hideFlags =
                        HideFlags.HideAndDontSave
                };

            texture.SetPixel(
                0,
                0,
                color);

            texture.Apply();

            return texture;
        }
    }
}
