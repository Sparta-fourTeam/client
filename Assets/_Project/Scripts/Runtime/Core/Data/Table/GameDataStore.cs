using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

namespace Game.Core
{
    /// <summary>Resources/MockData의 테이블 JSON을 읽어 도메인 모델 테이블로 만든다</summary>
    public sealed class GameDataStore
    {
        private static readonly string[] TableNames = { "Monsters", "Stages", "Upgrades", "Energy", "GeneralCards", "Skills" };
        private readonly Dictionary<string, string> _rawJson;

        public Dictionary<string, int> Revisions { get; }
        public Table<int, MonsterDefinition> Monsters { get; }
        public Table<int, StageDefinition> Stages { get; }
        public Table<string, UpgradeDefinition> Upgrades { get; }
        public EnergyConfig Energy { get; }

        /// <summary>스킬이 아닌 카드(방벽 회복 등). 스킬 카드는 Skills의 upgrades에 있다</summary>
        public IReadOnlyList<GeneralCardDefinition> GeneralCards { get; }

        /// <summary>어떤 스테이지로도 시작하지 않았을 때(Stage 씬을 바로 열었을 때) 쓰는 첫 번째 스테이지의 ID</summary>
        public int FirstStageId { get; }

        public GameDataStore()
        {
            _rawJson = new Dictionary<string, string>();
            foreach (var name in TableNames)
            {
                var asset = Resources.Load<TextAsset>($"MockData/{name}");
                if (asset == null)
                {
                    throw new InvalidOperationException($"Resources/MockData/{name}.json을 찾을 수 없습니다");
                }

                _rawJson[name] = asset.text;
            }

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

            Monsters = new Table<int, MonsterDefinition>(monsters);
            Stages = new Table<int, StageDefinition>(stages);
            Upgrades = new Table<string, UpgradeDefinition>(ParseIndexed<string, UpgradeDefinition>("Upgrades", d => d.UpgradeId));
            Energy = JsonConvert.DeserializeObject<EnergyConfig>(_rawJson["Energy"]);

            var generalCards = ParseIndexed<string, GeneralCardDefinition>("GeneralCards", d => d.Id).Values.ToList();
            foreach (var card in generalCards)
            {
                card.Validate();
            }

            GeneralCards = generalCards;

            FirstStageId = stages.Keys.First();
            // 스킬은 정의 객체가 가변이라 호출마다 새로 읽는다. 여기서는 부팅 때 잘못된 데이터를 바로 잡는다
            LoadSkills();

            // TODO(server): 전부 1 고정 — 예: 빌드에 포함된 테이블 버전 메타데이터로 교체
            Revisions = TableNames.ToDictionary(n => n, _ => 1);
        }

        public string RawJson(string name) => _rawJson[name];

        /// <summary>분열·소환 패시브가 가리키는 몬스터가 모두 있어야 한다</summary>
        public static void ValidatePassiveReferences(IReadOnlyDictionary<int, MonsterDefinition> monsters)
        {
            foreach (var monster in monsters.Values)
            {
                foreach (var passive in monster.Passives ?? new List<PassiveDefinition>())
                {
                    bool refersToMonster = passive.Kind == PassiveKind.Spawn;
                    if (refersToMonster && !monsters.ContainsKey(passive.MonsterId))
                    {
                        throw new InvalidOperationException($"Monsters {monster.Id}: {passive.Kind}이 없는 몬스터를 가리킵니다: {passive.MonsterId}");
                    }
                }
            }
        }

        /// <summary>스킬 정의 목록. 호출마다 새 객체를 만들고 카탈로그 검증을 통과해야 돌려준다</summary>
        public List<SkillData> LoadSkills() => ParseSkills(_rawJson["Skills"]);

        public static List<SkillData> ParseSkills(string json)
        {
            var weapons = JsonConvert.DeserializeObject<List<SkillData>>(json);
            SkillCatalogValidator.Validate(weapons);
            return weapons;
        }

        /// <summary>stageId의 스테이지. 없으면(0 포함) 첫 번째 스테이지를 돌려준다</summary>
        public StageDefinition StageOrFirst(int stageId) =>
            Stages.GetOrThrow(Stages.Contains(stageId) ? stageId : FirstStageId);

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
