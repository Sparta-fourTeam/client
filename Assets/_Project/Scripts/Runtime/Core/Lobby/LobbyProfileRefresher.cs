using System;
using System.Collections.Generic;
using System.Linq;
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
        private readonly AccountService _account;
        private readonly bool _refreshPublicProfile;
        private readonly PlayerProfile _profile;
        private readonly StageContext _stageContext;
        private readonly UniTaskCompletionSource _loaded = new UniTaskCompletionSource();

        public LobbyProfileRefresher(IPlayerApi playerApi, PlayerProfile profile, AccountService account = null,
            bool refreshPublicProfile = true, StageContext stageContext = null)
        {
            _playerApi = playerApi;
            _account = account;
            _refreshPublicProfile = refreshPublicProfile;
            _profile = profile;
            _stageContext = stageContext;
        }

        /// <summary>조회가 끝나면(실패 포함) 완료된다. 미션 입장처럼 최신 상태가 필요한 입력은 이걸 기다린다</summary>
        public UniTask WhenLoaded => _loaded.Task;

        /// <summary>다시 받기 전의 플레이어 레벨. 전투에서 돌아왔으면 전투 전 레벨이라 레벨업 팝업이 이것과 비교한다.
        /// 받기 전 프로필이 비어 있었으면(로그인 없이 로비부터 재생 등) 비교할 이전 레벨이 없으므로 받은 뒤 레벨과 같다</summary>
        public int LevelBefore { get; private set; }

        private bool _hadProfile;
        private HashSet<int> _clearedBefore = new();

        /// <summary>다시 받기 전에 깬 스테이지였는지. 전투에서 돌아왔으면 전투 전 기록이라 스테이지 클리어 해금 연출이 이것과 비교한다.
        /// 받기 전 프로필이 비어 있었으면 받은 뒤 기록과 같다</summary>
        public bool WasStageCleared(int stageId) => _clearedBefore.Contains(stageId);

        private HashSet<int> ClearedNow() =>
            _profile.Snapshot().stageProgress.Where(row => row.IsCleared).Select(row => row.stageId).ToHashSet();

        public void Start()
        {
            _hadProfile = _profile.IsLoaded;
            LevelBefore = _profile.Level;
            _clearedBefore = ClearedNow();
            // 전투 결과가 프로필에 먼저 반영됐을 수 있으므로(서버 전투) 전투를 시작할 때 남긴 값이 있으면 그것을 전투 전 기록으로 쓴다
            var preBattle = _stageContext?.TakePreBattle();
            if (preBattle != null)
            {
                LevelBefore = preBattle.Level;
                _clearedBefore = preBattle.ClearedStages.ToHashSet();
            }

            RefreshAsync().Forget(Debug.LogException);
        }

        private async UniTask RefreshAsync()
        {
            try
            {
                PlayerSnapshot snapshot = await _playerApi.GetMe();
                _profile.Apply(snapshot);
                if (_refreshPublicProfile && _account != null) { await _account.RefreshProfileAsync(); }
                if (!_hadProfile)
                {
                    LevelBefore = _profile.Level;
                    _clearedBefore = ClearedNow();
                }
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
