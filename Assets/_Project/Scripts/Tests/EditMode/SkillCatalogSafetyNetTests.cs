using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>
    /// 공격 종류가 소비할 수 없는 카드 효과를 검증기가 거부하는지 확인한다.
    /// </summary>
    public sealed class SkillCatalogSafetyNetTests
    {
        private static List<SkillData> LoadUnvalidated() =>
            JsonConvert.DeserializeObject<List<SkillData>>(Resources.Load<TextAsset>("MockData/Skills").text);

        [Test]
        public void Validator_RejectsProjectileOnlyEffectOnHitscanWeapon()
        {
            var catalog = LoadUnvalidated();
            var weapon = catalog.Find(w => w.castType == CastType.Hitscan);
            var card = weapon.upgrades[0];
            card.effects.Add(new EffectDef { kind = "pierceCount", value = 1 });

            var error = Assert.Throws<InvalidOperationException>(() => SkillCatalogValidator.Validate(catalog));
            StringAssert.Contains(card.id, error.Message);
            StringAssert.Contains("pierceCount", error.Message);
        }

        [Test]
        public void Validator_RejectsHitscanOnlyEffectOnProjectileWeapon()
        {
            var catalog = LoadUnvalidated();
            var weapon = catalog.Find(w => w.castType == CastType.Projectile);
            var card = weapon.upgrades[0];
            card.effects.Add(new EffectDef { kind = "fieldDuration", value = 1 });

            var error = Assert.Throws<InvalidOperationException>(() => SkillCatalogValidator.Validate(catalog));
            StringAssert.Contains(card.id, error.Message);
            StringAssert.Contains("fieldDuration", error.Message);
        }

        [Test]
        public void Validator_ChecksEffectsInsideVariants()
        {
            var catalog = LoadUnvalidated();
            var weapon = catalog.Find(w => w.castType == CastType.Hitscan && w.upgrades.Any(u => u.variants != null && u.variants.Length > 0));
            var card = weapon.upgrades.First(u => u.variants != null && u.variants.Length > 0);
            card.variants[0].effects.Add(new EffectDef { kind = "projectileSpeed", value = 1 });

            var error = Assert.Throws<InvalidOperationException>(() => SkillCatalogValidator.Validate(catalog));
            StringAssert.Contains(card.id, error.Message);
        }

    }
}
