using System;
using Cysharp.Threading.Tasks;
using Game.Core;
using Game.View;
using UnityEngine;
using VContainer.Unity;

namespace Game.Boot
{
    /// <summary>Loading 씬 진입 시 1회 실행: 로그인을 확인하고(최초 진입이면 로그인창, 실패하면 재시도) 플레이어 상태를 받아온 뒤 로비로 진입한다.
    /// 전투 튜토리얼 중이면(처음 시작 등) 로비 대신 그 스테이지로 바로 들어간다.
    /// 저장된 로그인 정보로 자동 로그인했으면 "화면을 터치하세요"에서 터치를 기다린 뒤 넘어간다.
    /// 로그인창에서 직접 로그인했으면 이미 버튼을 눌렀으므로 바로 넘어간다</summary>
    public sealed class LoadingFlow : IStartable
    {
        private readonly LoginFlow _login;
        private readonly TapToStartView _tapToStart;
        private readonly ISceneNavigator _nav;
        private readonly PlayerProfile _profile;
        private readonly BattleLauncher _launcher;
        private readonly GameDataStore _data;

        public LoadingFlow(LoginFlow login, TapToStartView tapToStart, ISceneNavigator nav, PlayerProfile profile, BattleLauncher launcher,
            GameDataStore data)
        {
            _data = data;
            _login = login;
            _tapToStart = tapToStart;
            _nav = nav;
            _profile = profile;
            _launcher = launcher;
        }

        public void Start()
        {
            RunAsync().Forget(Debug.LogException);
        }

        private async UniTask RunAsync()
        {
            try
            {
                bool prompted = await _login.RunAsync();
                if (!prompted)
                {
                    await _tapToStart.WaitForTapAsync();
                }
            }
            catch (OperationCanceledException)
            {
                // 기다리는 중에 Loading 화면이 사라지면(재생 종료 등) 대기가 취소된다. 오류가 아니므로 조용히 끝낸다
                return;
            }

            // 전투 발급이 거절되면(에너지 부족 등) 로비로 간다. 로비에서 다시 1스테이지로 보낸다
            int tutorialStage = TutorialProgress.BattleStage(_data.Tutorials, TutorialFacts.From(_profile));
            if (tutorialStage > 0 && await _launcher.Launch(tutorialStage))
            {
                return;
            }

            await _nav.GoToLobby();
        }
    }
}
