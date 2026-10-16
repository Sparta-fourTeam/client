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
        private readonly PlayerApiMode _mode;
        private readonly LoginFlow _login;
        private readonly TapToStartView _tapToStart;
        private readonly ISceneNavigator _nav;
        private readonly PlayerProfile _profile;
        private readonly BattleLauncher _launcher;
        private readonly GameDataStore _data;
        private readonly IBattleCheckpointStore _checkpoints;
        private readonly IAppUpdateChecker _updates;
        private readonly ConfirmPopupView _confirm;

        public LoadingFlow(LoginFlow login, TapToStartView tapToStart, ISceneNavigator nav, PlayerProfile profile, BattleLauncher launcher,
            GameDataStore data, IAppUpdateChecker updates, ConfirmPopupView confirm, IBattleCheckpointStore checkpoints = null, PlayerApiMode mode = null)
        {
            _mode = mode;
            _updates = updates;
            _confirm = confirm;
            _checkpoints = checkpoints;
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
                await CheckForUpdateAsync();
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
            // 도입 대사가 있는 전투 튜토리얼은 로비에서 대사를 본 뒤 전투로 간다 (TutorialGuideView)
            var battle = _mode?.ServerUpgradeTest == true ? null : TutorialProgress.ActiveBattle(_data.Tutorials, TutorialFacts.From(_profile));
            if (_checkpoints?.Load() == null && battle != null && _data.TutorialPresentations.For(battle.Id).Dialogue.Count == 0 && await _launcher.Launch(battle.StageId))
            {
                return;
            }

            await _nav.GoToLobby();
        }

        private async UniTask CheckForUpdateAsync()
        {
            var cancellationToken = _confirm.GetCancellationTokenOnDestroy();
            AppUpdateInfo update;
            try
            {
                update = await _updates.CheckAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception error)
            {
                // 스토어 장애·오프라인·미출시 상태로 게임 진입을 막지 않는다.
                Debug.LogWarning($"스토어 업데이트 확인 실패: {error.Message}");
                return;
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (update == null)
            {
                return;
            }

            bool confirmed = await _confirm.ShowAsync(new ConfirmRequest
            {
                Title = "새 버전 안내",
                Message = "새로운 버전이 출시되었습니다.\n스토어에서 업데이트할까요?",
                ConfirmLabel = "업데이트",
                CancelLabel = "나중에",
            }).AttachExternalCancellation(cancellationToken);
            if (confirmed)
            {
                Application.OpenURL(update.StoreUrl);
            }
        }
    }
}
