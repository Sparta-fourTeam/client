using System;
using Cysharp.Threading.Tasks;
using Game.Core;
using UnityEngine;
using VContainer.Unity;

namespace Game.Boot
{
    /// <summary>Loading 씬 진입 시 1회 실행: 로그인하고 플레이어 상태를 받아온 뒤 로비로 진입한다</summary>
    public sealed class LoadingFlow : IStartable
    {
        private readonly IAuthApi _auth;
        private readonly IPlayerApi _player;
        private readonly PlayerProfile _profile;
        private readonly ISceneNavigator _nav;

        public LoadingFlow(IAuthApi auth, IPlayerApi player, PlayerProfile profile, ISceneNavigator nav)
        {
            _auth = auth;
            _player = player;
            _profile = profile;
            _nav = nav;
        }

        public void Start()
        {
            RunAsync().Forget(Debug.LogException);
        }

        private async UniTask RunAsync()
        {
            try
            {
                await _auth.Login();
                var snapshot = await _player.GetMe();
                _profile.Apply(snapshot);
            }
            catch (Exception ex)
            {
                // TODO(error-ui): 재시도 또는 에러 팝업 훅 자리. 지금은 로그만 남기고 Loading 화면에 머문다.
                Debug.LogException(ex);
                return;
            }

            await _nav.GoToLobby();
        }
    }
}
