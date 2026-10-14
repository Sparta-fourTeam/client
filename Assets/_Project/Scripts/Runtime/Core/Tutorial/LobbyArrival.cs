using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Game.Core
{
    /// <summary>로비에 들어올 때 보여줄 레벨업 소식. 레벨이 오르지 않았고 받은 것도 없으면 비어 있다</summary>
    public sealed class LevelUpNews
    {
        public int FromLevel;
        public int ToLevel;
        /// <summary>이번에 오른 레벨 구간에서 열린 것 (등록된 ILevelUnlockSource 순서)</summary>
        public IReadOnlyList<UnlockNotice> Unlocks = Array.Empty<UnlockNotice>();
        /// <summary>튜토리얼 완료 등으로 함께 받은 아이템</summary>
        public IReadOnlyList<ItemAmount> RewardItems = Array.Empty<ItemAmount>();
        /// <summary>튜토리얼 완료 등으로 함께 받은 코인</summary>
        public int RewardGold;

        public bool LeveledUp => ToLevel > FromLevel;
        public bool IsEmpty => !LeveledUp && RewardItems.Count == 0 && RewardGold <= 0;
    }

    /// <summary>로비 진입 처리. 프로필을 다시 받은 뒤 완료 조건을 채운 튜토리얼(전투 튜토리얼 클리어 등)을 서버에 완료로 알려 보상을 받고,
    /// 전투 전 레벨과 비교해 레벨업 소식을 만든다. 보여주는 것은 View가 한다</summary>
    public sealed class LobbyArrival
    {
        private readonly LobbyProfileRefresher _refresher;
        private readonly PlayerProfile _profile;
        private readonly ITutorialApi _tutorialApi;
        private readonly GameDataStore _data;
        private readonly IReadOnlyList<ILevelUnlockSource> _unlockSources;

        public LobbyArrival(LobbyProfileRefresher refresher, PlayerProfile profile, ITutorialApi tutorialApi, GameDataStore data,
            IReadOnlyList<ILevelUnlockSource> unlockSources)
        {
            _refresher = refresher;
            _profile = profile;
            _tutorialApi = tutorialApi;
            _data = data;
            _unlockSources = unlockSources;
        }

        /// <summary>로비에 오기 전(전투 전)에 깬 스테이지였는지. CollectAsync가 끝난 뒤에 쓴다</summary>
        public bool WasStageCleared(int stageId) => _refresher.WasStageCleared(stageId);

        public async UniTask<LevelUpNews> CollectAsync()
        {
            await _refresher.WhenLoaded;
            var rewards = new List<ItemAmount>();
            int gold = 0;
            // 완료하면 다음 튜토리얼이 시작될 수 있어서(After) 더 완료할 게 없을 때까지 반복한다
            for (var ready = Completable(); ready != null; ready = Completable())
            {
                var response = await CompleteAsync(ready.Id);
                if (response == null)
                {
                    break;
                }

                rewards.AddRange(response.rewardItems ?? new List<ItemAmount>());
                gold += response.rewardGold;
            }

            int from = _refresher.LevelBefore;
            int to = _profile.Level;
            return new LevelUpNews
            {
                FromLevel = from,
                ToLevel = to,
                Unlocks = to > from ? _unlockSources.SelectMany(source => source.Unlocked(from, to)).ToList() : Array.Empty<UnlockNotice>(),
                RewardItems = rewards,
                RewardGold = gold,
            };
        }

        private TutorialDefinition Completable() =>
            TutorialProgress.Completable(_data.Tutorials, TutorialFacts.From(_profile)).FirstOrDefault();

        /// <summary>튜토리얼을 완료로 저장하고 프로필에 반영한다. 받은 보상이 담긴 응답을 돌려주고,
        /// 실패하면 로그만 남기고 null을 돌려준다 (다음 로비 진입 때 다시 시도된다)</summary>
        public async UniTask<TutorialCompleteResponse> CompleteAsync(string tutorialId)
        {
            try
            {
                var response = await _tutorialApi.Complete(tutorialId);
                _profile.Apply(response.snapshot);
                return response;
            }
            catch (Exception ex)
            {
                // TODO(error-ui): 재시도 팝업 자리. 지금은 로그만 남긴다
                Debug.LogException(ex);
                return null;
            }
        }
    }
}
