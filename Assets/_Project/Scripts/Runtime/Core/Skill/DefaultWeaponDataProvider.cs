using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace Game.Core
{
    /// <summary>스킬 정의는 JSON 한 곳에서 읽는다. 프리팹과 아이콘 키는 기존 연결을 유지한다.</summary>
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
            WeaponCatalogValidator.Validate(weapons);
            return weapons;
        }
    }
}
