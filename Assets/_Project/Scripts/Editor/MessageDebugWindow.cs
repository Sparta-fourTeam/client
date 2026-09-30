using System.Linq;
using Game.Core;
using Game.Core.Defense;
using Game.Core.Messages;
using MessagePipe;
using UnityEditor;
using UnityEngine;
using VContainer.Unity;

namespace Game.Editor
{
    /// <summary>재생 중에 메시지를 직접 발행해서 UI와 흐름을 확인하는 에디터 전용 창.
    /// MessagePipe의 Diagnostics 창은 구독 현황만 보여주고 발행은 못 하므로 따로 만들었다.
    /// 메시지를 추가하면 여기에도 버튼을 넣어둔다</summary>
    public sealed class MessageDebugWindow : EditorWindow
    {
        private int _enemyDiedCount = 5;
        private int _waveIndex = 1;
        private int _waveEnemyCount = 5;
        private bool _waveIsFinal;
        private int _wallHpCurrent = 50;
        private int _wallHpMax = 100;
        private int _wallDamage = 10;
        private Vector2 _scroll;

        private int _walletGold = 12500;
        private int _energyCurrent = 7;
        private int _energyMax = 100;

        private int _fakeSkillCount = 3;
        private readonly System.Collections.Generic.List<FakeSkillStatus> _fakeSkills = new System.Collections.Generic.List<FakeSkillStatus>();

        [MenuItem("Tools/Project Nova/Message Debugger")]
        private static void Open()
        {
            GetWindow<MessageDebugWindow>("Message Debugger");
        }

        // 재생 중에는 상태 표시가 계속 바뀌므로 주기적으로 다시 그린다
        private void OnInspectorUpdate()
        {
            if (Application.isPlaying)
            {
                Repaint();
            }
        }

        private void OnGUI()
        {
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("재생 중에만 사용할 수 있습니다. Boot 씬에서 시작해 Stage 씬까지 들어간 뒤 사용하세요.", MessageType.Info);
                return;
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            DrawStatus();
            DrawWaveMessages();
            DrawStageMessages();
            DrawWallMessages();
            DrawStageManagerCalls();
            DrawSkillMessages();
            DrawLobbyMessages();
            EditorGUILayout.EndScrollView();
        }

        // 가짜 스킬의 쿨타임을 진행시킨다. 창이 그려질 때만 갱신되지 않도록 에디터 업데이트에서 돈다
        private void OnEnable()
        {
            EditorApplication.update += TickFakeSkills;
        }

        private void OnDisable()
        {
            EditorApplication.update -= TickFakeSkills;
        }

        private void TickFakeSkills()
        {
            if (!Application.isPlaying)
            {
                _fakeSkills.Clear();
                return;
            }

            foreach (var skill in _fakeSkills)
            {
                skill.Tick(Time.deltaTime);
            }
        }

        // 실제 무기 대신 가짜 스킬로 HUD 스킬 슬롯을 확인한다. 실제 연결 전용 임시 버튼이다
        private void DrawSkillMessages()
        {
            Header("스킬 슬롯 (가짜 데이터, 실제 무기와 무관)");
            _fakeSkillCount = EditorGUILayout.IntSlider("스킬 수", _fakeSkillCount, 0, 6);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("SkillChanged 발행 (가짜)"))
                {
                    _fakeSkills.Clear();
                    string[] keys = { "fake_arrow", "fake_fireball", "fake_lightning", "fake_ice", "fake_kunai", "fake_extra" };
                    float[] cooldowns = { 1.0f, 1.8f, 2.5f, 4f, 6f, 3f };
                    for (int i = 0; i < _fakeSkillCount; i++)
                    {
                        // 칸마다 쿨타임 진행이 달라 보이도록 시작 값을 엇갈리게 준다
                        _fakeSkills.Add(new FakeSkillStatus(i + 1, i + 1, keys[i], cooldowns[i], cooldowns[i] * (i % 3) / 3f));
                    }

                    PublishBuffered(new SkillChanged(_fakeSkills.ToArray()));
                }

                if (GUILayout.Button("레벨 +1 후 재발행"))
                {
                    foreach (var skill in _fakeSkills)
                    {
                        skill.Level++;
                    }

                    PublishBuffered(new SkillChanged(_fakeSkills.ToArray()));
                }
            }
        }

        private void DrawStatus()
        {
            var stageManager = Resolve<StageManager>();
            string state = stageManager != null ? stageManager.State.ToString() : "Stage 씬이 아님";
            EditorGUILayout.LabelField("스테이지 상태", state);
            EditorGUILayout.LabelField("Time.timeScale", Time.timeScale.ToString("0.##"));
        }

        private void DrawWaveMessages()
        {
            Header("웨이브 / 적");

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("EnemyDied"))
                {
                    Publish(new EnemyDied());
                }

                _enemyDiedCount = Mathf.Max(1, EditorGUILayout.IntField(_enemyDiedCount, GUILayout.Width(40)));
                if (GUILayout.Button($"EnemyDied x{_enemyDiedCount}"))
                {
                    for (int i = 0; i < _enemyDiedCount; i++)
                    {
                        Publish(new EnemyDied());
                    }
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("WaveGaugeFilled (중간 웨이브)"))
                {
                    Publish(new WaveGaugeFilled(false));
                }

                if (GUILayout.Button("WaveGaugeFilled (마지막 웨이브)"))
                {
                    Publish(new WaveGaugeFilled(true));
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                _waveIndex = EditorGUILayout.IntField("웨이브 번호", _waveIndex);
                _waveEnemyCount = EditorGUILayout.IntField("적 수", _waveEnemyCount);
                _waveIsFinal = EditorGUILayout.ToggleLeft("마지막", _waveIsFinal, GUILayout.Width(60));
            }

            if (GUILayout.Button("WaveStarted"))
            {
                Publish(new WaveStarted(_waveIndex, _waveEnemyCount, _waveIsFinal));
            }
        }

        private void DrawStageMessages()
        {
            Header("판정 (StageJudge를 거치지 않고 바로 발행)");

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("StageEnded (Clear)"))
                {
                    Publish(new StageEnded(StageOutcome.Clear));
                }

                if (GUILayout.Button("StageEnded (Fail)"))
                {
                    Publish(new StageEnded(StageOutcome.Fail));
                }
            }
        }

        private void DrawWallMessages()
        {
            Header("벽");

            using (new EditorGUILayout.HorizontalScope())
            {
                _wallHpCurrent = EditorGUILayout.IntField("현재 HP", _wallHpCurrent);
                _wallHpMax = EditorGUILayout.IntField("최대 HP", _wallHpMax);
            }

            if (GUILayout.Button("WallHpChanged (HUD 표시만 바뀜, 실제 벽 HP는 그대로)"))
            {
                PublishBuffered(new WallHpChanged(_wallHpCurrent, _wallHpMax));
            }

            if (GUILayout.Button("WallDestroyed (StageJudge가 실패 판정)"))
            {
                Publish(new WallDestroyed());
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                _wallDamage = EditorGUILayout.IntField("데미지", _wallDamage);
                if (GUILayout.Button("Wall.TakeDamage 호출"))
                {
                    var wall = FindAnyObjectByType<Wall>();
                    if (wall != null)
                    {
                        wall.TakeDamage(_wallDamage);
                    }
                    else
                    {
                        Debug.LogWarning("[MessageDebugger] 씬에서 Wall을 찾지 못했습니다");
                    }
                }
            }
        }

        private void DrawStageManagerCalls()
        {
            Header("StageManager 호출");
            var stageManager = Resolve<StageManager>();
            using (new EditorGUI.DisabledScope(stageManager == null))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Pause"))
                    {
                        stageManager.Pause();
                    }

                    if (GUILayout.Button("Resume"))
                    {
                        stageManager.Resume();
                    }

                    if (GUILayout.Button("Forfeit"))
                    {
                        stageManager.Forfeit();
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    for (int i = 0; i < 3; i++)
                    {
                        if (GUILayout.Button($"PickCard({i})"))
                        {
                            stageManager.PickCard(i);
                        }
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("SetSpeed 1x"))
                    {
                        stageManager.SetSpeed(1f);
                    }

                    if (GUILayout.Button("SetSpeed 2x"))
                    {
                        stageManager.SetSpeed(2f);
                    }
                }
            }
        }

        private void DrawLobbyMessages()
        {
            Header("로비");
            using (new EditorGUILayout.HorizontalScope())
            {
                _walletGold = EditorGUILayout.IntField("골드", _walletGold);

                if (GUILayout.Button("WalletChanged"))
                {
                    PublishBuffered(new WalletChanged(_walletGold));
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                _energyCurrent = EditorGUILayout.IntField("현재 에너지", _energyCurrent);
                _energyMax = EditorGUILayout.IntField("최대 에너지", _energyMax);
            }

            if (GUILayout.Button("EnergyChanged (표시만 바뀜, 실제 에너지는 그대로)"))
            {
                PublishBuffered(new EnergyChanged(_energyCurrent, _energyMax));
            }
        }

        private static void Header(string text)
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField(text, EditorStyles.boldLabel);
        }

        private static void Publish<T>(T message) where T : struct
        {
            var publisher = Resolve<IPublisher<T>>();
            if (publisher == null)
            {
                Debug.LogWarning($"[MessageDebugger] {typeof(T).Name}: 발행자를 찾지 못했습니다 (브로커가 등록된 씬인지 확인)");
                return;
            }

            publisher.Publish(message);
            Debug.Log($"[MessageDebugger] 발행: {typeof(T).Name}");
        }

        // Buffered 메시지는 Buffered 구독자에게 가도록 반드시 IBufferedPublisher로 발행한다 (docs/messages.md)
        private static void PublishBuffered<T>(T message) where T : struct
        {
            var publisher = Resolve<IBufferedPublisher<T>>();
            if (publisher == null)
            {
                Debug.LogWarning($"[MessageDebugger] {typeof(T).Name}: 발행자를 찾지 못했습니다 (브로커가 등록된 씬인지 확인)");
                return;
            }

            publisher.Publish(message);
            Debug.Log($"[MessageDebugger] 발행(Buffered): {typeof(T).Name}");
        }

        // 자식 스코프(Stage)부터 찾아서 처음 해결되는 것을 돌려준다. 못 찾으면 null
        private static T Resolve<T>() where T : class
        {
            var scopes = FindObjectsByType<LifetimeScope>(FindObjectsSortMode.None)
                .Where(s => s.Container != null)
                .OrderByDescending(Depth);

            foreach (var scope in scopes)
            {
                if (scope.Container.TryResolve(typeof(T), out object resolved))
                {
                    return resolved as T;
                }
            }

            return null;
        }

        private static int Depth(LifetimeScope scope)
        {
            int depth = 0;
            for (var parent = scope.Parent; parent != null; parent = parent.Parent)
            {
                depth++;
            }

            return depth;
        }
    }
}
