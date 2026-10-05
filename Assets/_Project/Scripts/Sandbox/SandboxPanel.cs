using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using UnityEngine;

namespace Game.Sandbox
{
    /// <summary>샌드박스의 화면 패널. IMGUI(OnGUI)로 그린다: 디버그 도구라 프리팹·EventSystem·레이아웃 그룹이 필요 없고,
    /// 씬에 컴포넌트 하나만 있으면 어디서나 같은 모양이 나온다. 화면 아래쪽 절반을 쓰고 접기 버튼으로 게임 화면을 비울 수 있다.</summary>
    public sealed class SandboxPanel
    {
        private const float DesignWidth = 560f;

        private readonly SkillSandboxSession session;
        private readonly SandboxEnemyField enemies;
        private readonly Font font;

        private GUIStyle title, header, button, buttonOn, label, muted, error, box, small;
        private bool stylesReady;
        private Vector2 scroll;
        private bool visible = true;
        private bool realMode;
        private bool clickPlace = true;
        private SandboxEnemyField.Pattern pattern = SandboxEnemyField.Pattern.Cluster;
        private int enemyHp = 1000;
        private List<UpgradeChoice> choices = new List<UpgradeChoice>();
        private List<SkillSandboxSession.CardInfo> cards = new List<SkillSandboxSession.CardInfo>();
        private float scale = 1f;
        private float nextStats;
        private string statsText = string.Empty;

        public SandboxPanel(SkillSandboxSession session, SandboxEnemyField enemies, Font font)
        {
            this.session = session;
            this.enemies = enemies;
            this.font = font;
            session.Changed += OnChanged;
            OnChanged();
        }

        public void Dispose() => session.Changed -= OnChanged;

        private void OnChanged()
        {
            cards = session.Cards();
            if (realMode) { choices = session.RollChoices(); }
            nextStats = 0;
        }

        // ── 그리기 ────────────────────────────────────────────────────

        public void OnGUI()
        {
            EnsureStyles();
            scale = Mathf.Max(1f, Screen.width / DesignWidth);
            var previous = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            float width = Screen.width / scale;
            float height = Screen.height / scale;
            var toggleRect = new Rect(width - 118, 6, 112, 38);
            var panelRect = new Rect(0, height * .5f, width, height * .5f);

            if (GUI.Button(toggleRect, visible ? "패널 접기" : "패널 펴기", button)) { visible = !visible; }
            if (visible) { DrawPanel(panelRect); }

            // 버튼이 처리한 클릭은 Used가 되므로, 남은 클릭만 게임 화면 클릭으로 본다.
            var e = Event.current;
            if (clickPlace && e.type == EventType.MouseDown && e.button == 0 && !toggleRect.Contains(e.mousePosition)
                && !(visible && panelRect.Contains(e.mousePosition)))
            {
                PlaceEnemyAt(e.mousePosition);
            }
            GUI.matrix = previous;
        }

        private void DrawPanel(Rect rect)
        {
            if (Time.unscaledTime >= nextStats)
            {
                nextStats = Time.unscaledTime + .25f;
                statsText = session.StatsText() + $"\n\n적 {enemies.AliveCount}마리 / 누적 피해 {enemies.TotalDamage:N0} / 최근 3초 초당 {enemies.RecentDps():N1}";
            }
            GUI.Box(rect, GUIContent.none, box);
            GUILayout.BeginArea(new Rect(rect.x + 8, rect.y + 6, rect.width - 16, rect.height - 12));
            scroll = GUILayout.BeginScrollView(scroll);

            GUILayout.Label("스킬 샌드박스", title);
            if (!string.IsNullOrEmpty(session.LastError)) { GUILayout.Label(session.LastError, error); }

            DrawSkills();
            DrawCards();
            DrawEnemies();
            DrawEnvironment();
            GUILayout.Label("현재 스킬 정보", header);
            GUILayout.Label(statsText, muted);
            GUILayout.Space(40);

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawSkills()
        {
            GUILayout.Label("스킬", header);
            int perRow = Mathf.Max(2, Mathf.FloorToInt((Screen.width / scale - 24) / 170f));
            var skills = session.Skills;
            for (int i = 0; i < skills.Count; i += perRow)
            {
                GUILayout.BeginHorizontal();
                for (int j = i; j < Mathf.Min(i + perRow, skills.Count); j++)
                {
                    var skill = skills[j];
                    string text = skill.HasPrefab ? skill.Name : skill.Name + "\n(프리팹 연결 없음)";
                    GUI.enabled = skill.HasPrefab;
                    if (GUILayout.Button(text, skill.Id == session.Build.skillId ? buttonOn : button, GUILayout.Height(44))) { session.SelectSkill(skill.Id); }
                    GUI.enabled = true;
                }
                GUILayout.EndHorizontal();
            }
        }

        private void DrawCards()
        {
            GUILayout.Label("카드", header);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("직접 선택(조건 무시)", realMode ? button : buttonOn)) { realMode = false; }
            if (GUILayout.Button("실제 3지선다", realMode ? buttonOn : button))
            {
                realMode = true;
                choices = session.RollChoices();
            }
            GUILayout.EndHorizontal();
            if (session.Build.skillId == 0)
            {
                GUILayout.Label("위에서 스킬을 고르세요", muted);
                return;
            }
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("카드 모두 비우기", button)) { session.ClearCards(); }
            if (realMode && GUILayout.Button("다시 뽑기", button)) { choices = session.RollChoices(); }
            GUILayout.EndHorizontal();
            GUILayout.Label("적용한 카드: " + (session.Build.cards.Count == 0 ? "없음" : string.Join(", ", session.Build.cards)), small);

            if (realMode) { DrawChoices(); }
            else { DrawCardList(); }
        }

        private void DrawChoices()
        {
            if (choices.Count == 0) { GUILayout.Label("지금 고를 수 있는 카드가 없습니다(조건 미충족이거나 모두 최대).", muted); }
            foreach (var choice in choices.ToList())
            {
                if (GUILayout.Button($"{choice.DisplayName}\n{choice.DisplayDescription}", button, GUILayout.MinHeight(54)))
                {
                    session.Pick(choice);
                    choices = session.RollChoices();
                }
            }
        }

        private void DrawCardList()
        {
            foreach (var card in cards.ToList())
            {
                GUILayout.BeginHorizontal();
                string text = $"{card.Name}   {card.Count}/{card.MaxPick}\n{card.Description}";
                if (card.Blocked != null) { text += $"\n⚠ {card.Blocked}"; }
                GUI.enabled = card.Blocked == null;
                if (GUILayout.Button(text, button, GUILayout.MinHeight(54))) { session.TryAddCard(card.Id, out _); }
                GUI.enabled = card.Count > 0;
                if (GUILayout.Button("−", button, GUILayout.Width(44), GUILayout.MinHeight(54))) { session.RemoveCard(card.Id); }
                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }
        }

        private void DrawEnemies()
        {
            GUILayout.Label("적", header);
            GUILayout.BeginHorizontal();
            foreach (int count in new[] { 1, 3, 5, 10 })
            {
                if (GUILayout.Button($"샌드백 {count}", button, GUILayout.Height(40))) { enemies.SpawnPattern(pattern, count, enemyHp, new Vector2(0, 2.2f)); }
            }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            foreach (SandboxEnemyField.Pattern value in Enum.GetValues(typeof(SandboxEnemyField.Pattern)))
            {
                string name = value == SandboxEnemyField.Pattern.Line ? "직선" : value == SandboxEnemyField.Pattern.Cluster ? "군집" : "흩뿌림";
                if (GUILayout.Button("배치: " + name, pattern == value ? buttonOn : button)) { pattern = value; }
            }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            foreach (int hp in new[] { 100, 1000, 10000 })
            {
                if (GUILayout.Button($"HP {hp:N0}", enemyHp == hp ? buttonOn : button)) { enemyHp = hp; }
            }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button($"이동 {(enemies.Moving ? "켬" : "끔")}", enemies.Moving ? buttonOn : button)) { enemies.Moving = !enemies.Moving; }
            if (GUILayout.Button($"되살림 {(enemies.Respawn ? "켬" : "끔")}", enemies.Respawn ? buttonOn : button)) { enemies.Respawn = !enemies.Respawn; }
            if (GUILayout.Button($"클릭 배치 {(clickPlace ? "켬" : "끔")}", clickPlace ? buttonOn : button)) { clickPlace = !clickPlace; }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("모두 제거", button)) { enemies.Clear(); }
            if (GUILayout.Button("피해 집계 초기화", button)) { enemies.ResetDamage(); }
            GUILayout.EndHorizontal();
        }

        private void DrawEnvironment()
        {
            GUILayout.Label("환경", header);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("−", button, GUILayout.Width(48))) { session.SetPermanentLevel(session.Build.permanentLevel - 1); }
            GUILayout.Label($"영구 레벨 {session.Build.permanentLevel}", label, GUILayout.ExpandWidth(true));
            if (GUILayout.Button("+", button, GUILayout.Width(48))) { session.SetPermanentLevel(session.Build.permanentLevel + 1); }
            if (GUILayout.Button("+5", button, GUILayout.Width(48))) { session.SetPermanentLevel(session.Build.permanentLevel + 5); }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            foreach (float speed in new[] { .25f, .5f, 1f, 2f })
            {
                if (GUILayout.Button($"{speed}x", Mathf.Approximately(Time.timeScale, speed) ? buttonOn : button)) { Time.timeScale = speed; }
            }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            for (int i = 1; i <= 3; i++)
            {
                string slot = i.ToString();
                if (GUILayout.Button($"저장 {slot}", button)) { session.SavePreset(slot); }
                GUI.enabled = SandboxPresets.Exists(slot);
                if (GUILayout.Button($"불러오기 {slot}", button)) { session.LoadPreset(slot); }
                GUI.enabled = true;
            }
            GUILayout.EndHorizontal();
        }

        private void PlaceEnemyAt(Vector2 guiPosition)
        {
            var camera = Camera.main;
            if (camera == null) { return; }
            // GUI 좌표는 배율을 곱하고 위아래를 뒤집어야 화면 좌표가 된다.
            var screen = new Vector3(guiPosition.x * scale, Screen.height - guiPosition.y * scale, 0);
            Vector3 world = camera.ScreenToWorldPoint(screen);
            enemies.Spawn(new Vector2(world.x, world.y), enemyHp);
        }

        // ── 스타일 (한 번만 만든다. OnGUI마다 만들면 매 프레임 할당이 생긴다) ──────

        private void EnsureStyles()
        {
            if (stylesReady) { return; }
            stylesReady = true;
            GUIStyle Make(int size, Color color, FontStyle fontStyle = FontStyle.Normal, TextAnchor anchor = TextAnchor.MiddleLeft)
            {
                var style = new GUIStyle(GUI.skin.label) { fontSize = size, fontStyle = fontStyle, alignment = anchor, wordWrap = true };
                style.normal.textColor = color;
                if (font != null) { style.font = font; }
                return style;
            }
            title = Make(24, Color.white, FontStyle.Bold);
            header = Make(18, new Color(.55f, .8f, 1f), FontStyle.Bold);
            label = Make(16, Color.white);
            muted = Make(14, new Color(.72f, .74f, .8f), anchor: TextAnchor.UpperLeft);
            small = Make(13, new Color(.72f, .74f, .8f));
            error = Make(15, new Color(1f, .55f, .5f));

            GUIStyle Button(Color color)
            {
                var style = new GUIStyle(GUI.skin.button) { fontSize = 15, wordWrap = true, alignment = TextAnchor.MiddleCenter };
                style.normal.textColor = Color.white;
                style.hover.textColor = Color.white;
                style.active.textColor = Color.white;
                style.normal.background = style.hover.background = style.active.background = Texture(color);
                if (font != null) { style.font = font; }
                return style;
            }
            button = Button(new Color(.22f, .24f, .3f, 1));
            buttonOn = Button(new Color(.2f, .55f, .85f, 1));
            box = new GUIStyle(GUI.skin.box);
            box.normal.background = Texture(new Color(0, 0, 0, .82f));
        }

        private static Texture2D Texture(Color color)
        {
            var texture = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }
    }
}
