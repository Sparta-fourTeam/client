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
        private static readonly string[] TableNames = { "Monsters", "Stages", "Upgrades", "Energy" };
        private readonly Dictionary<string, string> _rawJson;

        public Dictionary<string, int> Revisions { get; }
        public Table<int, MonsterDefinition> Monsters { get; }
        public Table<int, StageDefinition> Stages { get; }
        public Table<string, UpgradeDefinition> Upgrades { get; }
        public EnergyConfig Energy { get; }

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

            Monsters = new Table<int, MonsterDefinition>(ParseIndexed<int, MonsterDefinition>("Monsters", d => d.Id));
            Stages = new Table<int, StageDefinition>(ParseIndexed<int, StageDefinition>("Stages", d => d.Id));
            Upgrades = new Table<string, UpgradeDefinition>(ParseIndexed<string, UpgradeDefinition>("Upgrades", d => d.UpgradeId));
            Energy = JsonConvert.DeserializeObject<EnergyConfig>(_rawJson["Energy"]);
            Revisions = TableNames.ToDictionary(n => n, _ => 1);
        }

        public string RawJson(string name) => _rawJson[name];

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
