using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace Game.Core
{
    /// <summary>인술 정의는 JSON 한 곳에서 읽는다. 프리팹과 아이콘 키는 기존 연결을 유지한다.</summary>
    public sealed class DefaultWeaponDataProvider : IWeaponDataProvider
    {
        public List<WeaponData> LoadAll()
        {
            var asset = Resources.Load<TextAsset>("MockData/Weapons");
            if (asset == null)
            {
                throw new InvalidOperationException("MockData/Weapons.json을 찾을 수 없습니다.");
            }

            return Parse(asset.text);
        }

        public static List<WeaponData> Parse(string json)
        {
            var weapons = JsonConvert.DeserializeObject<List<WeaponData>>(json);
            if (weapons == null || weapons.Count == 0)
            {
                throw new InvalidOperationException("무기 카탈로그가 비어 있습니다.");
            }

            var ids = new HashSet<int>();
            var cards = new HashSet<string>();
            foreach (var weapon in weapons)
            {
                if (weapon == null || !ids.Add(weapon.id) || weapon.baseStats == null
                    || weapon.maxLevel < 1 || weapon.upgrades == null)
                {
                    throw new InvalidOperationException("무기 정의 또는 ID가 잘못되었습니다.");
                }

                foreach (var option in weapon.upgrades)
                {
                    if (option == null || string.IsNullOrEmpty(option.id) || !cards.Add(option.id)
                        || option.maxPickCount <= 0 || option.effects == null)
                    {
                        throw new InvalidOperationException("강화 정의 또는 ID가 잘못되었습니다.");
                    }
                }
            }
            return weapons;
        }
    }
}
