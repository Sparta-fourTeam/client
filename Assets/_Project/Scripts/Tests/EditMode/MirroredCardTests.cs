using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>다른 스킬의 카드 효과를 가져오는 자식 스킬(집중 화살이 화살의 일제 사격을 쓴다)</summary>
    public sealed class MirroredCardTests
    {
        private const int ArrowId = 1, FocusBeamId = 16, FocusArrowId = 17;

        private sealed class State : IUpgradeState
        {
            public readonly Dictionary<(int, string), int> Counts = new();
            public int Permanent;
            public int GetWeaponLevel(int id) => 1;
            public int GetPermanentWeaponLevel(int id) => Permanent;
            public int GetAcquiredCount(int id, string card) => Counts.TryGetValue((id, card), out var count) ? count : 0;
        }

        private static Dictionary<int, SkillData> Catalog() => new DefaultSkillDataProvider(new GameDataStore()).LoadAll().ToDictionary(w => w.id);

        private static string Describe(IEnumerable<EffectDef> effects) => string.Join(",", effects.Select(e => $"{e.kind}:{e.value}"));

        [Test(Description = "화살 일제 사격을 얻은 횟수만큼 선언된 효과가 쌓인다")]
        public void For_RepeatsTheMirroredCardEffectsByAcquiredCount()
        {
            var catalog = Catalog();
            var state = new State();
            state.Counts[(ArrowId, "1")] = 2;

            var effects = MirroredEffects.For(catalog[FocusArrowId], catalog, state);

            Assert.AreEqual(Describe(Enumerable.Repeat(catalog[ArrowId].upgrades.Find(c => c.id == "1").effects, 2).SelectMany(e => e)), Describe(effects));
        }

        [Test(Description = "영구 레벨 변형의 해금 경계에 도달하면 변형 효과를 복제한다")]
        public void For_FollowsThePermanentLevelVariant()
        {
            var catalog = Catalog();
            var variant = catalog[ArrowId].upgrades.Find(c => c.id == "1").variants[0];
            var state = new State { Permanent = variant.minPermanentLevel };
            state.Counts[(ArrowId, "1")] = 1;

            Assert.AreEqual(Describe(variant.effects), Describe(MirroredEffects.For(catalog[FocusArrowId], catalog, state)));
        }

        [Test(Description = "카드를 얻지 않았거나 가져올 카드가 없는 스킬은 효과가 없다")]
        public void For_NothingAcquiredOrNothingMirrored_IsEmpty()
        {
            var catalog = Catalog();

            Assert.IsEmpty(MirroredEffects.For(catalog[FocusArrowId], catalog, new State()));
            Assert.IsEmpty(MirroredEffects.For(catalog[ArrowId], catalog, new State()));
        }

        [Test(Description = "mirrorCards가 없는 스킬·카드, 자기 자신, 가져올 수 없는 효과는 카탈로그 검증이 막는다")]
        public void Validator_RejectsBrokenMirrorCards()
        {
            foreach (var cardId in new[] { "없는 카드", "self", "123" })
            {
                var list = new DefaultSkillDataProvider(new GameDataStore()).LoadAll();
                var child = list.Find(w => w.id == FocusArrowId);
                child.upgrades.Add(new SkillUpgradeOption
                {
                    id = "self",
                    name = "자기 카드",
                    effects = new List<EffectDef> { new() { kind = "damage", value = 10 } }
                });
                child.mirrorCards = new List<MirroredCard> { new() { cardId = cardId } };

                Assert.Throws<InvalidOperationException>(() => SkillCatalogValidator.Validate(list), cardId);
            }
        }

        [Test(Description = "집중 화살: 집중 광선이 시전되면 화살 한 발에 화살 일제 사격을 얻은 횟수만큼 더해 쏜다")]
        public void FocusArrow_FiresWithTheArrowSkillsMultishotCount()
        {
            using var world = new CatalogWorld(data => data[ArrowId].upgrades.Find(c => c.id == "1").maxPickCount = 2);
            var arrow = world.Create(ArrowId);
            world.Take(arrow, "1", "1");
            var beam = world.Create(FocusBeamId);
            world.Take(beam, "117", "123");
            world.AddEnemy(0, 4);

            typeof(SkillCaster).GetMethod("OnFire", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(beam, null);

            var extra = world.Data[ArrowId].upgrades.Find(c => c.id == "1").effects.First(e => e.kind == "projectileCount").value;
            Assert.AreEqual(world.Data[FocusArrowId].baseStats.cast.projectileCount + 2 * (int)extra, CatalogWorld.Active().Count);
        }

        [Test(Description = "화살 일제 사격이 없으면 집중 화살은 기본 1발이다")]
        public void FocusArrow_WithoutMultishotFiresOne()
        {
            using var world = new CatalogWorld();
            world.Create(ArrowId);
            var beam = world.Create(FocusBeamId);
            world.Take(beam, "117", "123");
            world.AddEnemy(0, 4);

            typeof(SkillCaster).GetMethod("OnFire", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(beam, null);

            Assert.AreEqual(world.Data[FocusArrowId].baseStats.cast.projectileCount, CatalogWorld.Active().Count);
        }
    }
}
