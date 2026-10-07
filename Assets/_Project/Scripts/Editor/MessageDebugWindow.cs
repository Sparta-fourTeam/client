using System.Linq;
using Cysharp.Threading.Tasks;
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
        private int _realEnergy = 2;

        private int _launchStageId = 1;
        private int _failSubmitCount = 4;
        private int _resultKills = 24;
        private int _resultWave = 3;
        private float _resultPlayTime = 125f;
        private int _resultGold = 350;

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
            DrawBattleLaunch();
            DrawSubmitFault();
            DrawWaveMessages();
            DrawStageMessages();
            DrawWallMessages();
            DrawStageManagerCalls();
            DrawLobbyMessages();
            EditorGUILayout.EndScrollView();
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
                if (GUILayout.Button("WaveCompleted (중간 웨이브)"))
                {
                    Publish(new WaveCompleted(1, false));
                }

                if (GUILayout.Button("WaveCompleted (마지막 웨이브)"))
                {
                    Publish(new WaveCompleted(20, true));
                }
            }

            if (GUILayout.Button("AllEnemiesCleared (클리어 판정)"))
            {
                Publish(new AllEnemiesCleared());
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

            // 결과 팝업이 구독하는 최종 결과. 서버 제출을 거치지 않고 표시만 확인할 때 쓴다
            using (new EditorGUILayout.HorizontalScope())
            {
                _resultKills = EditorGUILayout.IntField("처치", _resultKills);
                _resultWave = EditorGUILayout.IntField("웨이브", _resultWave);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                _resultPlayTime = EditorGUILayout.FloatField("플레이 시간(초)", _resultPlayTime);
                _resultGold = EditorGUILayout.IntField("보상 골드", _resultGold);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("StageResult (Clear)"))
                {
                    Publish(new StageResult(true, _resultKills, _resultWave, _resultPlayTime, _resultGold));
                }

                if (GUILayout.Button("StageResult (Fail)"))
                {
                    Publish(new StageResult(false, _resultKills, _resultWave, _resultPlayTime, _resultGold));
                }
            }
        }

        // 로비 UI가 BattleLauncher에 연결되기 전에도 실제 전투 발급(에너지 소모 포함)으로 Stage에 들어가 결과 흐름까지 확인하는 용도
        private void DrawBattleLaunch()
        {
            Header("전투 시작 (BattleLauncher 경유, 로비에서 사용)");
            using (new EditorGUILayout.HorizontalScope())
            {
                _launchStageId = EditorGUILayout.IntField("스테이지", _launchStageId);
                if (GUILayout.Button("전투 발급 후 Stage 진입"))
                {
                    var api = Resolve<IBattleApi>();
                    var context = Resolve<StageContext>();
                    var navigator = Resolve<ISceneNavigator>();
                    var startFailed = Resolve<IPublisher<StartFailed>>();
                    if (api == null || context == null || navigator == null || startFailed == null)
                    {
                        Debug.LogWarning("[MessageDebugger] BattleLauncher에 필요한 서비스를 찾지 못했습니다 (Boot 씬에서 시작했는지 확인)");
                        return;
                    }

                    new BattleLauncher(api, context, navigator, startFailed).Launch(_launchStageId).Forget(Debug.LogException);
                }
            }
        }

        // Local 백엔드는 제출 실패를 던지지 않아서, 자동 재시도(3회)와 재시도 버튼 경로를 보려면 이 스위치로 실패를 만든다.
        // 4번 실패시키면 첫 시도와 자동 재시도 3번이 모두 실패해 재시도 버튼이 뜬다
        private void DrawSubmitFault()
        {
            Header("제출 실패 만들기 (Local 백엔드 전용)");
            var faultSwitch = Resolve<ISubmitFaultSwitch>();
            if (faultSwitch == null)
            {
                EditorGUILayout.HelpBox("재생 중에 Local 백엔드로 실행해야 쓸 수 있습니다 (Boot 씬에서 시작).", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("남은 실패 횟수", faultSwitch.FailNextSubmits.ToString());
            using (new EditorGUILayout.HorizontalScope())
            {
                _failSubmitCount = Mathf.Max(0, EditorGUILayout.IntField("실패시킬 횟수", _failSubmitCount));
                if (GUILayout.Button("다음 제출을 네트워크 오류로 실패시키기"))
                {
                    faultSwitch.FailNextSubmits = _failSubmitCount;
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

            using (new EditorGUILayout.HorizontalScope())
            {
                _realEnergy = EditorGUILayout.IntField("실제 에너지", _realEnergy);
                if (GUILayout.Button("PlayerProfile 에너지 변경 (저장 파일은 그대로)"))
                {
                    SetProfileEnergy(_realEnergy);
                }
            }

            if (GUILayout.Button("LobbyRequestFailed (회복 실패)"))
            {
                Publish(new LobbyRequestFailed("DEBUG_FAILED"));
            }
        }

        // 프로필 값만 바꾼다. EnergyClock이 다시 계산해 EnergyChanged를 발행하고, 입장 시 에너지 확인도 이 값을 쓴다.
        // Game.Editor는 Game.Network를 참조하지 않아서 로컬 저장 파일은 바꾸지 않는다
        private static void SetProfileEnergy(int energy)
        {
            var profile = Resolve<PlayerProfile>();
            if (profile == null)
            {
                Debug.LogWarning("[MessageDebugger] PlayerProfile을 찾지 못했습니다");
                return;
            }

            PlayerSnapshot current = profile.Snapshot();
            profile.Apply(new PlayerSnapshot
            {
                gold = current.gold,
                exp = current.exp,
                energyStored = energy,
                energyUpdatedAt = System.DateTime.UtcNow.ToString("O"),
                stageProgress = current.stageProgress,
                upgrades = current.upgrades,
                equipments = current.equipments,
                items = current.items,
            });
            Debug.Log($"[MessageDebugger] PlayerProfile 에너지 = {energy}");
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
