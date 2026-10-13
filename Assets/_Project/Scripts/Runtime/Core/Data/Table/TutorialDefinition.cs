using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    /// <summary>튜토리얼 한 단계 (Tutorials.json 한 행). 표의 순서가 진행 우선순위다.
    /// Kind battle: 시작 조건을 채웠고 완료 조건(Complete)을 못 채웠으면 로비 대신 StageId 전투로 보내고, 그 전투는 에너지를 쓰지 않는다.
    /// Kind guide: 로비에서 Script 템플릿의 단계를 Lines 대사로 하나씩 강조해 완료 조건까지 이끈다.
    /// 시작 조건은 앞 튜토리얼(After) 완료와 플레이어 레벨(MinLevel)이다. 완료 조건을 채우면 서버에 완료로 저장하며 Rewards를 받는다</summary>
    [Serializable]
    public sealed class TutorialDefinition
    {
        public const string Battle = "battle";
        public const string Guide = "guide";

        public string Id;
        public string Kind;
        public string After;
        public int MinLevel;
        public int StageId;
        public string Script;
        /// <summary>이 튜토리얼이 시작되기 전까지 잠가 둘 하단 탭 (예: skill). 시작되면 해금 연출과 함께 열린다. 비우면 없음</summary>
        public string UnlocksTab;
        public List<string> Lines = new();
        /// <summary>가이드 시작 전에 재생하는 대사 장면 (레벨업 연출 직후 한 번). 비우면 바로 가이드로 간다</summary>
        public List<DialogueLine> Dialogue = new();
        public TutorialCondition Complete;
        public List<TutorialReward> Rewards = new();

        public bool IsBattle => Kind == Battle;
        public bool IsGuide => Kind == Guide;

        public void Validate(GameDataStore data, ICollection<string> earlierIds)
        {
            string name = $"Tutorials {Id}";
            if (string.IsNullOrWhiteSpace(Id) || (Kind != Battle && Kind != Guide) || Complete == null)
            {
                throw new InvalidOperationException($"{name}: Id, Kind(battle/guide), Complete가 필요합니다");
            }

            if (!string.IsNullOrEmpty(After) && !earlierIds.Contains(After))
            {
                throw new InvalidOperationException($"{name}: After {After}은(는) 앞 행의 튜토리얼이어야 합니다");
            }

            if (IsBattle && !data.Stages.Contains(StageId))
            {
                throw new InvalidOperationException($"{name}: 전투 튜토리얼의 StageId {StageId}가 Stages에 없습니다");
            }

            if (IsGuide && (string.IsNullOrWhiteSpace(Script) || Lines == null || Lines.Count == 0))
            {
                throw new InvalidOperationException($"{name}: 가이드 튜토리얼은 Script와 Lines가 필요합니다");
            }

            if (Dialogue != null && Dialogue.Any(line => line == null || string.IsNullOrWhiteSpace(line.Speaker) || string.IsNullOrWhiteSpace(line.Text)))
            {
                throw new InvalidOperationException($"{name}: Dialogue의 Speaker와 Text는 비울 수 없습니다");
            }

            Complete.Validate(data, name);
            foreach (var reward in Rewards ?? new List<TutorialReward>())
            {
                reward.Validate(data, name);
            }
        }
    }

    /// <summary>대사 한 줄. Side는 화자 초상이 서는 쪽 (left 기본, right)</summary>
    [Serializable]
    public sealed class DialogueLine
    {
        public string Speaker;
        public string Side;
        public string Text;

        public bool IsRight => Side == "right";
    }

    /// <summary>튜토리얼 완료 조건. stageCleared(StageId), upgradeLevel(UpgradeId를 Level 이상), playerLevel(Level 이상)</summary>
    [Serializable]
    public sealed class TutorialCondition
    {
        public const string StageCleared = "stageCleared";
        public const string UpgradeLevel = "upgradeLevel";
        public const string PlayerLevel = "playerLevel";

        public string Kind;
        public int StageId;
        public string UpgradeId;
        public int Level;

        public bool IsMet(TutorialFacts facts) => Kind switch
        {
            StageCleared => facts.IsStageCleared(StageId),
            UpgradeLevel => facts.UpgradeLevel(UpgradeId) >= Level,
            PlayerLevel => facts.Level >= Level,
            _ => false,
        };

        public void Validate(GameDataStore data, string name)
        {
            bool valid = Kind switch
            {
                StageCleared => data.Stages.Contains(StageId),
                UpgradeLevel => data.Upgrades.Contains(UpgradeId ?? "") && Level >= 1,
                PlayerLevel => Level >= 1,
                _ => false,
            };
            if (!valid)
            {
                throw new InvalidOperationException($"{name}: 완료 조건 {Kind}의 값이 잘못됐습니다");
            }
        }
    }

    /// <summary>튜토리얼 완료 보상.
    /// setLevel: 경험치를 Level 레벨 시작점(진행 0)으로 맞춘다. upgradeMaterial: UpgradeId를 Level로 올리는 데 드는 재료를 준다.
    /// item: ItemId를 Quantity개 준다</summary>
    [Serializable]
    public sealed class TutorialReward
    {
        public const string SetLevel = "setLevel";
        public const string UpgradeMaterial = "upgradeMaterial";
        public const string Item = "item";

        public string Kind;
        public int Level;
        public string UpgradeId;
        public string ItemId;
        public int Quantity;

        public void Validate(GameDataStore data, string name)
        {
            bool valid = Kind switch
            {
                SetLevel => Level >= 1 && Level <= data.PlayerLevels.MaxLevel,
                UpgradeMaterial => data.Upgrades.Contains(UpgradeId ?? "") && data.Upgrades.GetOrThrow(UpgradeId).UsesMaterial
                    && Level >= 1 && Level <= data.Upgrades.GetOrThrow(UpgradeId).MaxLevel,
                Item => data.Items.Contains(ItemId ?? "") && Quantity > 0,
                _ => false,
            };
            if (!valid)
            {
                throw new InvalidOperationException($"{name}: 보상 {Kind}의 값이 잘못됐습니다");
            }
        }
    }

    /// <summary>튜토리얼 조건을 판정하는 데 필요한 플레이어 상태. 클라이언트는 PlayerProfile, 서버는 저장 데이터로 만든다</summary>
    public sealed class TutorialFacts
    {
        public int Level;
        public Func<int, bool> IsStageCleared;
        public Func<string, int> UpgradeLevel;
        public Func<string, bool> IsDone;

        public static TutorialFacts From(PlayerProfile profile) => new()
        {
            Level = profile.Level,
            IsStageCleared = profile.IsStageCleared,
            UpgradeLevel = profile.UpgradeLevel,
            IsDone = profile.IsTutorialDone,
        };
    }
}
