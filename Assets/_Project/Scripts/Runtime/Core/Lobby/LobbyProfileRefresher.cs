using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer.Unity;

namespace Game.Core
{
    /// <summary>로비에 들어올 때마다 플레이어 상태를 다시 받아 PlayerProfile에 반영한다.
    /// 전투(에너지 차감·클리어·보상)는 저장소에만 반영되므로, 돌아온 뒤 이걸 하지 않으면 이전 해금·재화가 남는다.
    /// Apply하면 Changed로 LobbyModel·EnergyClock이 알아서 다시 발행한다</summary>
    public sealed class LobbyProfileRefresher : IStartable
    {
        private readonly IPlayerApi _playerApi;
        private readonly PlayerProfile _profile;
        private readonly UniTaskCompletionSource _loaded = new UniTaskCompletionSource();

        public LobbyProfileRefresher(IPlayerApi playerApi, PlayerProfile profile)
        {
            _playerApi = playerApi;
            _profile = profile;
        }

        /// <summary>조회가 끝나면(실패 포함) 완료된다. 미션 입장처럼 최신 상태가 필요한 입력은 이걸 기다린다</summary>
        public UniTask WhenLoaded => _loaded.Task;

        public void Start()
        {
            RefreshAsync().Forget(Debug.LogException);
        }

        private async UniTask RefreshAsync()
        {
            try
            {
                PlayerSnapshot snapshot = await _playerApi.GetMe();
                _profile.Apply(snapshot);
            }
            catch (Exception ex)
            {
                // TODO(error-ui): 재시도 팝업 자리. 지금은 로그만 남기고 이전 상태로 진행한다
                Debug.LogException(ex);
            }
            finally
            {
                _loaded.TrySetResult();
            }
        }
    }
}
