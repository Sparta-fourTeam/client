using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

namespace Game.Core
{
    /// <summary>공급받은 테이블 원문을 도메인 모델로 만든다. 기본 생성자는 개발·테스트용이다</summary>
    public sealed class GameDataStore
    {
        private static readonly string[] TableNames = { "Monsters", "Stages", "Upgrades", "Energy", "Combat", "GeneralCards", "Skills", "Items", "StageRewards", "RewardRules", "PlayerLevels", "CodexRewards", "Tutorials", "Quests", "Shop" };
        private readonly Dictionary<string, string> _rawJson;
        private DisplayTexts _texts;
        private string _displayLocale;
        private readonly Dictionary<string, DisplayTexts> _textCache = new();
        private readonly Dictionary<string, List<TutorialPresentation>> _presentationCache = new();
        // LoadSkills가 내준 목록. 로케일이 바뀌면 이 객체들의 이름·설명을 제자리에서 갱신한다(해제된 것은 건너뛴다)
        private readonly List<WeakReference<List<SkillData>>> _issuedSkills = new();
        private readonly Dictionary<string, int> _skillUnlockLevels;

        public Dictionary<string, int> Revisions { get; }
        public string BattleSourceRevision { get; private set; }
        public Table<int, MonsterDefinition> Monsters { get; private set; }
        public Table<int, StageDefinition> Stages { get; private set; }
        public Table<string, UpgradeDefinition> Upgrades { get; }
        public Table<string, ItemDefinition> Items { get; }
        public Table<int, StageRewardBalance> StageRewards { get; private set; }
        public RewardRulesConfig RewardRules { get; private set; }
        public EnergyConfig Energy { get; }
        public ShopConfig Shop { get; }
        public CombatConfig Combat { get; private set; }
        public PlayerLevelTable PlayerLevels { get; private set; }
        public bool HasServerPlayerLevels { get; private set; }

        public void InvalidateServerPlayerLevels() => HasServerPlayerLevels = false;

        public void ApplyServerPlayerLevels(PlayerLevelConfig config, int version)
        {
            if (config == null || version < 1) { throw new ArgumentException("서버 PlayerLevels가 필요합니다"); }
            var table = config.ToTable();
            PlayerLevels = table;
            _rawJson["PlayerLevels"] = JsonConvert.SerializeObject(config);
            Revisions["PlayerLevels"] = version;
            HasServerPlayerLevels = true;
        }

        /// <summary>튜토리얼 정의. 표의 순서가 진행 우선순위다 (TutorialProgress)</summary>
        public IReadOnlyList<TutorialDefinition> Tutorials { get; }

        /// <summary>튜토리얼의 대사·화면 연결 정보(클라이언트 전용). 서버 테이블이 아니라서 버전·원문 조회 목록에 없다</summary>
        public TutorialPresentationTable TutorialPresentations { get; }

        /// <summary>스테이지 클리어로 열리는 로비 기능 (클라이언트 전용, ClientData/FeatureUnlocks.json)</summary>
        public FeatureUnlockTable FeatureUnlocks { get; private set; }

        /// <summary>도감 등록 보상 골드. 몬스터마다가 아니라 등급별이다</summary>
        public IReadOnlyDictionary<EnemyType, int> CodexRewardGold { get; }

        /// <summary>일일·주간 퀘스트 보드. 표의 순서가 화면의 탭 순서다</summary>
        public IReadOnlyList<QuestBoardDefinition> QuestBoards { get; }

        /// <summary>스킬이 아닌 카드(방벽 회복 등). 스킬 카드는 Skills의 upgrades에 있다</summary>
        public IReadOnlyList<GeneralCardDefinition> GeneralCards { get; }

        /// <summary>어떤 스테이지로도 시작하지 않았을 때(Stage 씬을 바로 열었을 때) 쓰는 첫 번째 스테이지의 ID</summary>
        public int FirstStageId { get; private set; }

        /// <summary>새 공급자가 전투 외 기존 테이블을 유지하며 새 저장소를 구성할 때 사용한다.</summary>
        public Dictionary<string, string> CopyTables() => new Dictionary<string, string>(_rawJson);

        /// <summary>다운로드한 전투 원본이 모두 검증된 뒤 Root의 동일 인스턴스에 원자적으로 반영한다.
        /// 이미 생성된 PlayerProfile과 새 Lobby/Stage Scope가 같은 서버 원본을 보게 한다.</summary>
        public void ApplyBattleSource(GameDataStore downloaded, DataVersions versions)
        {
            if (downloaded == null || versions?.revisions == null)
            { throw new ArgumentException("검증된 전투 원본과 테이블 버전이 필요합니다"); }
            if (versions.revisions.Values.Any(revision => revision <= 0))
            { throw new InvalidOperationException("서버 테이블 버전이 잘못되었습니다"); }
            _rawJson["Stages"] = downloaded.RawJson("Stages");
            _rawJson["Monsters"] = downloaded.RawJson("Monsters");
            _rawJson["Combat"] = downloaded.RawJson("Combat");
            Monsters = downloaded.Monsters;
            Stages = downloaded.Stages;
            Combat = downloaded.Combat;
            StageRewards = downloaded.StageRewards;
            FeatureUnlocks = downloaded.FeatureUnlocks;
            FirstStageId = downloaded.FirstStageId;
            BattleSourceRevision = downloaded.BattleSourceRevision;
            foreach (var pair in versions.revisions)
            { if (Revisions.ContainsKey(pair.Key)) { Revisions[pair.Key] = pair.Value; } }
        }

        private static Dictionary<string, string> LoadLocalTables()
        {
            var result = new Dictionary<string, string>();
            foreach (var name in TableNames)
            {
                var asset = Resources.Load<TextAsset>($"MockData/{name}")
                    ?? throw new InvalidOperationException($"Resources/MockData/{name}.json을 찾을 수 없습니다");
                result[name] = asset.text;
            }
            return result;
        }

        /// <summary>개발·테스트용 로컬 테이블로 구성한다.</summary>
        [VContainer.Inject]
        public GameDataStore() : this(LoadLocalTables()) { }

        /// <summary>로컬 테이블에 표시 문구 로케일을 지정해 구성한다(Root가 시작 로케일로 만든다)</summary>
        public GameDataStore(string displayLocale) : this(LoadLocalTables(), displayLocale) { }

        /// <summary>공급받은 원문으로 런타임 테이블을 만든다. 전투 원본은 HTTP 공급자가 덮어쓴다.
        /// displayLocale은 표시 문구의 로케일이고 기본 ko라 기기 언어와 무관하게 동작한다</summary>
        public GameDataStore(IReadOnlyDictionary<string, string> tables, string displayLocale = DisplayTexts.BaseLocale)
        {
            if (tables == null) { throw new ArgumentNullException(nameof(tables)); }
            _rawJson = new Dictionary<string, string>();
            foreach (var name in TableNames)
            {
                if (!tables.TryGetValue(name, out var json) || string.IsNullOrWhiteSpace(json))
                { throw new InvalidOperationException($"{name} 테이블이 없습니다"); }
                _rawJson[name] = json;
            }

            BattleSourceRevision = BattleSourceFingerprint.Compute(_rawJson["Stages"], _rawJson["Monsters"], _rawJson["Combat"]);

            _displayLocale = string.IsNullOrEmpty(displayLocale) ? DisplayTexts.BaseLocale : displayLocale;
            _texts = LoadTexts(_displayLocale);

            var monsters = ParseIndexed<int, MonsterDefinition>("Monsters", d => d.Id);
            foreach (var monster in monsters.Values)
            {
                monster.Validate();
            }

            ValidatePassiveReferences(monsters);

            var stages = ParseIndexed<int, StageDefinition>("Stages", d => d.Id);
            foreach (var stage in stages.Values)
            {
                stage.Validate();
            }

            foreach (var stage in stages.Values)
            {
                foreach (var wave in stage.Waves)
                {
                    foreach (var spawn in wave.Spawns)
                    {
                        if (!monsters.TryGetValue(spawn.MonsterId, out var monster))
                        {
                            throw new InvalidOperationException($"Stages {stage.Id}: 웨이브 구성의 몬스터 {spawn.MonsterId}이(가) Monsters에 없습니다");
                        }

                        spawn.Type = monster.GetEnemyType();
                        spawn.SplitDescendants = CountSplitDescendants(monster);
                        if (spawn.Type != EnemyType.Normal && spawn.Count != 1)
                        {
                            throw new InvalidOperationException($"Stages {stage.Id}: 엘리트·보스 {spawn.MonsterId}은(는) 웨이브마다 1마리만 나옵니다 (Count 1)");
                        }
                    }
                }
            }

            Monsters = new Table<int, MonsterDefinition>(monsters);
            Stages = new Table<int, StageDefinition>(stages);
            var unlockAsset = Resources.Load<TextAsset>("ClientData/FeatureUnlocks")
                ?? throw new InvalidOperationException("Resources/ClientData/FeatureUnlocks.json을 찾을 수 없습니다");
            FeatureUnlocks = new FeatureUnlockTable(JsonConvert.DeserializeObject<List<FeatureUnlock>>(unlockAsset.text), Stages.Contains);
            var upgrades = ParseIndexed<string, UpgradeDefinition>("Upgrades", d => d.UpgradeId);
            Upgrades = new Table<string, UpgradeDefinition>(upgrades);
            var items = ParseIndexed<string, ItemDefinition>("Items", d => d.Id);
            _texts.ApplyTo(items.Values);
            ResolveEquipmentMaterialTargets(upgrades.Values, items);
            foreach (var item in items.Values) { item.Validate(); }
            Items = new Table<string, ItemDefinition>(items);
            foreach (var upgrade in upgrades.Values)
            {
                upgrade.Validate();
                if (upgrade.UsesMaterial)
                {
                    Items.GetOrThrow(upgrade.MaterialItemId);
                }
            }
            RewardRules = JsonConvert.DeserializeObject<RewardRulesConfig>(_rawJson["RewardRules"]);
            RewardRules.Validate();
            ValidateRewardItemReferences(RewardRules);
            var rewards = ParseIndexed<int, StageRewardBalance>("StageRewards", d => d.StageId);
            foreach (var balance in rewards.Values)
            {
                balance.RewardRules = RewardRules;
                balance.Validate();
                if (!Stages.Contains(balance.StageId)) { throw new InvalidOperationException("보상의 스테이지가 없습니다."); }
            }
            foreach (var stage in stages.Values)
            {
                if (!rewards.ContainsKey(stage.Id)) { throw new InvalidOperationException($"StageRewards {stage.Id}: 보상 밸런스가 없습니다."); }
            }
            Items.GetOrThrow(ItemIds.GemChest);
            StageRewards = new Table<int, StageRewardBalance>(rewards);
            Energy = JsonConvert.DeserializeObject<EnergyConfig>(_rawJson["Energy"]);
            Shop = JsonConvert.DeserializeObject<ShopConfig>(_rawJson["Shop"]);
            Combat = JsonConvert.DeserializeObject<CombatConfig>(_rawJson["Combat"]);
            Combat.Validate();
            PlayerLevels = JsonConvert.DeserializeObject<PlayerLevelConfig>(_rawJson["PlayerLevels"]).ToTable();
            var tutorials = JsonConvert.DeserializeObject<List<TutorialDefinition>>(_rawJson["Tutorials"]);
            var tutorialIds = new HashSet<string>();
            foreach (var tutorial in tutorials)
            {
                tutorial.Validate(this, tutorialIds);
                if (!tutorialIds.Add(tutorial.Id)) { throw new InvalidOperationException($"Tutorials {tutorial.Id}: Id가 중복됩니다"); }
            }

            Tutorials = tutorials;
            TutorialPresentations = new TutorialPresentationTable(LoadPresentations(_displayLocale), tutorials);

            var codexRewards = ParseIndexed<EnemyType, CodexRewardRow>("CodexRewards", d => d.GetGrade());
            foreach (var row in codexRewards.Values) { row.Validate(); }
            foreach (EnemyType grade in Enum.GetValues(typeof(EnemyType)))
            {
                if (!codexRewards.ContainsKey(grade)) { throw new InvalidOperationException($"CodexRewards {grade}: 등급별 보상이 없습니다"); }
            }
            CodexRewardGold = codexRewards.ToDictionary(pair => pair.Key, pair => pair.Value.Gold);

            var questBoards = JsonConvert.DeserializeObject<List<QuestBoardDefinition>>(_rawJson["Quests"]);
            var questIds = new HashSet<string>();
            foreach (var board in questBoards) { board.Validate(questIds); }
            QuestBoards = questBoards;

            foreach (var offer in Shop.Offers)
            {
                bool known = offer.Reward == ShopRules.CoinReward || offer.Reward == ShopRules.EnergyReward || Items.Contains(offer.Reward);
                if (!known || offer.Count <= 0 || offer.Price < 0)
                {
                    throw new InvalidOperationException($"Shop {offer.Reward}: 보상은 코인·에너지·Items의 아이템이어야 하고 수량은 1 이상, 가격은 0 이상이어야 합니다");
                }
            }

            if (Shop.GemGoldRate <= 0 || Shop.GoldPacks.Count == 0)
            {
                throw new InvalidOperationException("Shop: 잼 기준 비율(GemGoldRate)과 코인 묶음(GoldPacks)이 필요합니다");
            }

            for (int i = 0; i < Shop.GoldPacks.Count; i++)
            {
                var pack = Shop.GoldPacks[i];
                bool ordered = i == 0 || (pack.Gold > Shop.GoldPacks[i - 1].Gold && pack.DiscountPercent >= Shop.GoldPacks[i - 1].DiscountPercent);
                if (pack.Gold <= 0 || pack.DiscountPercent < 0 || pack.DiscountPercent > 99 || !ordered)
                {
                    throw new InvalidOperationException($"Shop GoldPacks[{i}]: 코인은 1 이상, 할인율은 0~99이고, 더 큰 묶음일수록 코인은 늘고 할인율은 같거나 높아야 합니다");
                }
            }

            if (Shop.GemPacks.Count == 0)
            {
                throw new InvalidOperationException("Shop: 잼 묶음(GemPacks)이 필요합니다");
            }

            for (int i = 0; i < Shop.GemPacks.Count; i++)
            {
                var pack = Shop.GemPacks[i];
                var previous = i == 0 ? null : Shop.GemPacks[i - 1];
                bool ordered = previous == null || (pack.Gems > previous.Gems && pack.PriceKrw > previous.PriceKrw && pack.BonusPercent >= previous.BonusPercent);
                if (pack.Gems <= 0 || pack.PriceKrw <= 0 || pack.BonusPercent < 0 || pack.BonusPercent > 99 || !ordered)
                {
                    throw new InvalidOperationException($"Shop GemPacks[{i}]: 잼·가격은 1 이상, 보너스는 0~99이고, 더 큰 묶음일수록 잼·가격은 늘고 보너스는 같거나 높아야 합니다");
                }
            }

            if (Shop.Slots <= 0 || Shop.Offers.Count < Shop.Slots || Shop.Discounts.Count == 0 || Shop.Discounts.Exists(d => d < 0 || d > 99))
            {
                throw new InvalidOperationException("Shop: 칸 수는 1 이상, 물품은 칸 수 이상, 할인율은 0~99 사이로 하나 이상 있어야 합니다");
            }

            var generalCards = ParseIndexed<string, GeneralCardDefinition>("GeneralCards", d => d.Id).Values.ToList();
            _texts.ApplyTo(generalCards);
            foreach (var card in generalCards)
            {
                card.Validate();
            }

            GeneralCards = generalCards;

            FirstStageId = stages.Keys.First();
            // 스킬은 정의 객체가 가변이라 호출마다 새로 읽는다. 여기서는 부팅 때 잘못된 데이터를 바로 잡는다
            var skills = LoadSkills();
            _skillUnlockLevels = UpgradeUnlockRule.SkillUnlockLevels(skills);
            foreach (var upgrade in upgrades.Values)
            {
                UpgradeUnlockRule.Validate(upgrade, _skillUnlockLevels);
                PermanentSkillEffect.Validate(upgrade, skills);
                PermanentSkillEffect.ValidateEquipment(upgrade);
            }

            // TODO(server): 빌드에 포함된 테이블 버전 메타데이터로 교체. Combat 2는 BaseTimeScale을, Quests 2는 상자 보상 Gems를 추가한 개정이다.
            Revisions = TableNames.ToDictionary(n => n, _ => 1);
            Revisions["Combat"] = 2;
            Revisions["Tutorials"] = 3; // 기능 해금 시작 조건(Start) 추가
            Revisions["Quests"] = 2;
            Revisions["Shop"] = 2; // 잼 묶음(GemPacks) 추가
        }

        public string RawJson(string name) => _rawJson[name];

        // 로케일별 캐시에는 ko 폴백이 합쳐진 결과를 둔다. 캐시의 표는 읽기만 하고 객체에는 문자열을 복사해 넣는다
        private DisplayTexts LoadTexts(string locale)
        {
            if (!_textCache.TryGetValue(locale, out var texts)) { _textCache[locale] = texts = DisplayTexts.Load(locale); }
            return texts;
        }

        private List<TutorialPresentation> LoadPresentations(string locale)
        {
            if (!_presentationCache.TryGetValue(locale, out var rows)) { _presentationCache[locale] = rows = TutorialPresentationTable.Load(locale); }
            // 표가 행 객체를 그대로 쥐므로 호출마다 사본을 준다(캐시 객체를 갱신 대상과 공유하지 않는다)
            return JsonConvert.DeserializeObject<List<TutorialPresentation>>(JsonConvert.SerializeObject(rows));
        }

        /// <summary>표시 문구(스킬·카드·아이템 이름·설명, 튜토리얼 대사)를 로케일로 다시 적용한다. 이미 만들어진 객체를 제자리에서 갱신하고
        /// 숫자·식별자는 건드리지 않는다. 번역 파일이 없거나 항목이 비면 ko 값을 쓴다. 같은 로케일이면 아무것도 하지 않는다</summary>
        public void ApplyDisplayLocale(string localeCode)
        {
            string locale = string.IsNullOrEmpty(localeCode) ? DisplayTexts.BaseLocale : localeCode;
            if (locale == _displayLocale) { return; }

            var texts = LoadTexts(locale);
            var presentations = LoadPresentations(locale);
            _texts = texts;
            _displayLocale = locale;
            texts.ApplyTo(Items.Values);
            texts.ApplyTo(GeneralCards);
            TutorialPresentations.ApplyTexts(presentations);
            _issuedSkills.RemoveAll(r => !r.TryGetTarget(out _));
            foreach (var reference in _issuedSkills)
            {
                if (reference.TryGetTarget(out var skills)) { texts.ApplyTo(skills); }
            }
        }

        /// <summary>장비 재료의 대상은 Upgrades.MaterialItemId에서 역으로 찾는다. 한 재료가 여러 장비에 연결되면 로드를 거절한다.</summary>
        public static void ResolveEquipmentMaterialTargets(IEnumerable<UpgradeDefinition> upgrades, IReadOnlyDictionary<string, ItemDefinition> items)
        {
            var targets = new Dictionary<string, string>();
            foreach (var upgrade in upgrades)
            {
                if (!upgrade.UpgradeId.StartsWith("equipment.", StringComparison.Ordinal) || !upgrade.UsesMaterial) { continue; }
                if (!items.TryGetValue(upgrade.MaterialItemId, out var material)
                    || targets.ContainsKey(upgrade.MaterialItemId)
                    || (!string.IsNullOrEmpty(material.TargetId) && material.TargetId != upgrade.UpgradeId))
                {
                    throw new InvalidOperationException($"Upgrades {upgrade.UpgradeId}: 장비 재료 {upgrade.MaterialItemId}의 연결이 없거나 중복·불일치합니다.");
                }
                targets.Add(upgrade.MaterialItemId, upgrade.UpgradeId);
            }
            // 모든 연결을 확인한 뒤에만 적용하므로 실패 시 일부 대상이 바뀌지 않는다.
            foreach (var target in targets) { items[target.Key].ResolveEquipmentTarget(target.Value); }
        }

        /// <summary>분열·소환 패시브가 가리키는 몬스터가 있어야 하고, 자기 자신이거나 다시 소환하는 몬스터면 안 된다</summary>
        public static void ValidatePassiveReferences(IReadOnlyDictionary<int, MonsterDefinition> monsters)
        {
            foreach (var monster in monsters.Values)
            {
                foreach (var passive in monster.Passives ?? new List<PassiveDefinition>())
                {
                    bool refersToMonster = passive.Kind == PassiveKind.Spawn;
                    if (!refersToMonster)
                    {
                        continue;
                    }

                    if (!monsters.TryGetValue(passive.MonsterId, out var target))
                    {
                        throw new InvalidOperationException($"Monsters {monster.Id}: {passive.Kind}이 없는 몬스터를 가리킵니다: {passive.MonsterId}");
                    }

                    if (target.Id == monster.Id)
                    {
                        throw new InvalidOperationException($"Monsters {monster.Id}: {passive.Kind}이 자기 자신을 가리킵니다");
                    }

                    // 연쇄 소환 금지: 소환·분열로 나오는 몬스터는 다시 소환·분열하지 않는다
                    if ((target.Passives ?? new List<PassiveDefinition>()).Exists(p => p != null && p.Kind == PassiveKind.Spawn))
                    {
                        throw new InvalidOperationException(
                            $"Monsters {monster.Id}: 소환 대상 {target.Id}이(가) 다시 {PassiveKind.Spawn} 패시브를 가집니다 (연쇄 소환 금지)");
                    }
                }
            }
        }

        /// <summary>호출부가 전투 시작 전에 갱신한다. 검증 완료 전에는 어떤 기존 설정도 바꾸지 않는다.</summary>
        public void ApplyRewardRules(string json, int version)
        {
            var rules = JsonConvert.DeserializeObject<RewardRulesConfig>(json);
            if (rules == null || rules.Version != version || version <= 0)
            { throw new InvalidOperationException("RewardRules 버전이 잘못되었습니다."); }
            if (RewardRules.Version.HasValue && version < RewardRules.Version.Value)
            { throw new InvalidOperationException("이전 RewardRules 버전은 적용할 수 없습니다."); }
            if (RewardRules.Version == version && !Newtonsoft.Json.Linq.JToken.DeepEquals(
                    Newtonsoft.Json.Linq.JToken.Parse(_rawJson["RewardRules"]), Newtonsoft.Json.Linq.JToken.Parse(json)))
            { throw new InvalidOperationException("동일한 RewardRules 버전의 내용이 다릅니다."); }
            rules.Validate();
            ValidateRewardItemReferences(rules);
            foreach (var balance in StageRewards.Values) { balance.Validate(rules); }
            RewardRules = rules;
            foreach (var balance in StageRewards.Values) { balance.RewardRules = rules; }
            Revisions["RewardRules"] = version;
            _rawJson["RewardRules"] = json;
        }

        private void ValidateRewardItemReferences(RewardRulesConfig rules)
        {
            foreach (var rule in rules.Items)
            {
                if (!rule.Value.Random && !Items.Contains(rule.Key))
                { throw new InvalidOperationException($"RewardRules: 없는 아이템 {rule.Key}"); }
            }
        }

        /// <summary>강화의 해금 플레이어 레벨. 스킬 강화는 스킬이 열리는 레벨(SkillData.unlockLevel)을, 그 외는 행의 UnlockLevel을 쓴다</summary>
        public int UnlockLevelOf(UpgradeDefinition def) => UpgradeUnlockRule.LevelOf(def, _skillUnlockLevels);

        /// <summary>스킬 정의 목록. 호출마다 새 객체를 만들고 카탈로그 검증을 통과해야 돌려준다</summary>
        public List<SkillData> LoadSkills()
        {
            var skills = ParseSkills(_rawJson["Skills"], _texts);
            _issuedSkills.RemoveAll(r => !r.TryGetTarget(out _));
            _issuedSkills.Add(new WeakReference<List<SkillData>>(skills));
            return skills;
        }

        /// <summary>texts가 있으면 검증 전에 이름·설명을 채운다(검증이 카드·변형 이름을 본다)</summary>
        public static List<SkillData> ParseSkills(string json, DisplayTexts texts = null)
        {
            var weapons = JsonConvert.DeserializeObject<List<SkillData>>(json);
            texts?.ApplyTo(weapons);
            SkillCatalogValidator.Validate(weapons);
            return weapons;
        }

        /// <summary>stageId 스테이지에 등장하는 몬스터. 스테이지 정의의 MonsterIds 순서를 따르고, 없는 stageId는 첫 스테이지로 본다</summary>
        public IReadOnlyList<MonsterDefinition> StageMonsters(int stageId) =>
            StageOrFirst(stageId).MonsterIds.Select(id => Monsters.GetOrThrow(id)).ToList();

        /// <summary>stageId의 스테이지. 없으면(0 포함) 첫 번째 스테이지를 돌려준다</summary>
        public StageDefinition StageOrFirst(int stageId) =>
            Stages.GetOrThrow(Stages.Contains(stageId) ? stageId : FirstStageId);

        /// <summary>몬스터 한 마리가 죽을 때 분열(OnDeath Spawn)로 나오는 몬스터 수. 분열체는 다시 분열하지 않으므로(연쇄 소환 금지) 한 단계만 센다</summary>
        private static int CountSplitDescendants(MonsterDefinition monster)
        {
            int total = 0;
            foreach (var passive in monster.Passives ?? new List<PassiveDefinition>())
            {
                if (passive != null && passive.Kind == PassiveKind.Spawn && passive.ToSpawnTrigger() == SpawnTrigger.OnDeath)
                {
                    total += passive.Count;
                }
            }

            return total;
        }

        private Dictionary<TKey, TValue> ParseIndexed<TKey, TValue>(string name, Func<TValue, TKey> keyOf)
        {
            var list = JsonConvert.DeserializeObject<List<TValue>>(_rawJson[name]);
            var map = new Dictionary<TKey, TValue>();
            foreach (var item in list)
            {
                var key = keyOf(item);
                if (map.ContainsKey(key))
                {
                    throw new InvalidOperationException($"{name} 테이블에 중복된 키가 있습니다: {key}");
                }

                map.Add(key, item);
            }

            return map;
        }
    }
}
