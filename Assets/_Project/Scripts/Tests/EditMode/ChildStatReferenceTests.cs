using System;
using System.Collections.Generic;
using Game.Core;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>자식 스킬이 부모의 스탯을 참조하는 방식: 상속(inherit), 자식 전용 강화(target), 시전 시점 스냅샷</summary>
    public sealed class ChildStatReferenceTests
    {
        private const int ParentId = 1;
        private const int ChildId = 2;

        private sealed class Recorder : IChildSkillCaster
        {
            public readonly List<ChildCast> Casts = new List<ChildCast>();
            public void Cast(ChildCast cast, AttackContext context) => Casts.Add(cast);
        }

        private static WeaponData Parent(float damage = 100) => new WeaponData
        {
            id = ParentId,
            name = "부모",
            maxLevel = 5,
            upgrades = new List<WeaponUpgradeOption>(),
            baseStats = new WeaponBaseStats
            {
                cast = { baseDamage = damage, cooldown = 1, range = 10, projectileCount = 1 },
                projectile = { speed = 20, pierceCount = 1 },
                status = { freezeDuration = 2 }
            }
        };

        private static WeaponData Child() => new WeaponData
        {
            id = ChildId,
            name = "자식",
            maxLevel = 5,
            upgrades = new List<WeaponUpgradeOption>(),
            baseStats = new WeaponBaseStats
            {
                cast = { baseDamage = 3, cooldown = 1, range = 10, projectileCount = 1 },
                projectile = { speed = 5 }
            }
        };

        private static EffectDef Link(Dictionary<string, float> inherit = null, AttackEvent trigger = AttackEvent.Hit) =>
            new EffectDef { kind = "onEvent", trigger = trigger, skillId = ChildId, inherit = inherit };

        private static EffectDef ForChild(string kind, float value) => new EffectDef { kind = kind, value = value, target = ChildId };

        private static SkillConfigBuilder NewBuilder(Recorder recorder, float damage = 100) =>
            new SkillConfigBuilder(SkillConfig.FromDefinition(Parent(damage)).WithChildCaster(recorder));

        private static ChildCast RaiseOnce(SkillConfig config, Recorder recorder, AttackEvent trigger = AttackEvent.Hit)
        {
            recorder.Casts.Clear();
            config.Reactions.Raise(trigger, new AttackContext(Vector2.zero, Vector3.up));
            Assert.AreEqual(1, recorder.Casts.Count);
            return recorder.Casts[0];
        }

        private static SkillConfig ChildBase() => SkillConfig.FromDefinition(Child());

        // ── 상속 ──────────────────────────────────────────────────────

        [Test]
        public void WithoutInherit_ChildKeepsItsOwnBaseStats()
        {
            var recorder = new Recorder();
            var builder = NewBuilder(recorder);
            Assert.IsTrue(builder.TryApplyCatalog(new[] { Link() }));
            var resolved = RaiseOnce(builder.Build(), recorder).Resolve(ChildBase());
            Assert.AreEqual(3, resolved.Stats.Cast.Damage);
            Assert.AreEqual(5, resolved.Stats.Projectile.Speed);
        }

        [Test]
        public void Inherit_TakesParentStatsTimesScale()
        {
            var recorder = new Recorder();
            var builder = NewBuilder(recorder);
            var inherit = new Dictionary<string, float> { ["damage"] = .5f, ["projectileSpeed"] = 1, ["pierceCount"] = 1, ["freezeDuration"] = 1 };
            Assert.IsTrue(builder.TryApplyCatalog(new[] { Link(inherit) }));
            var resolved = RaiseOnce(builder.Build(), recorder).Resolve(ChildBase());
            Assert.AreEqual(50, resolved.Stats.Cast.Damage, .001f);
            Assert.AreEqual(20, resolved.Stats.Projectile.Speed);
            Assert.AreEqual(1, resolved.Stats.Projectile.PierceCount);
            Assert.AreEqual(2, resolved.Stats.Status.FreezeDuration);
            Assert.AreEqual(1, resolved.Stats.Cast.Cooldown, "상속하지 않은 값은 자식의 것이다");
        }

        [Test]
        public void Inherit_FollowsParentUpgradesMadeAfterTheLinkWasCreated()
        {
            var recorder = new Recorder();
            var builder = NewBuilder(recorder);
            Assert.IsTrue(builder.TryApplyCatalog(new[] { Link(new Dictionary<string, float> { ["damage"] = .5f }) }));
            Assert.IsTrue(builder.TryApplyCatalog(new[] { new EffectDef { kind = "damage", value = 100 } }));
            var resolved = RaiseOnce(builder.Build(), recorder).Resolve(ChildBase());
            Assert.AreEqual(100, resolved.Stats.Cast.Damage, .001f, "부모 피해 200의 절반");
        }

        [Test]
        public void AlreadyLaunchedAttack_KeepsParentStatsFromWhenItWasLaunched()
        {
            var recorder = new Recorder();
            var builder = NewBuilder(recorder);
            Assert.IsTrue(builder.TryApplyCatalog(new[] { Link(new Dictionary<string, float> { ["damage"] = .5f }) }));
            var launched = builder.Build();           // 이 설정으로 발사한 공격
            Assert.IsTrue(builder.TryApplyCatalog(new[] { new EffectDef { kind = "damage", value = 100 } }));
            var later = builder.Build();              // 이후 강화한 설정

            Assert.AreEqual(50, RaiseOnce(launched, recorder).Resolve(ChildBase()).Stats.Cast.Damage, .001f);
            Assert.AreEqual(100, RaiseOnce(later, recorder).Resolve(ChildBase()).Stats.Cast.Damage, .001f);
        }

        // ── 자식 전용 강화 ────────────────────────────────────────────

        [Test]
        public void ChildOnlyUpgrade_AppliesAfterInheritAndNeverTouchesParent()
        {
            var recorder = new Recorder();
            var builder = NewBuilder(recorder);
            Assert.IsTrue(builder.TryApplyCatalog(new[] { Link(new Dictionary<string, float> { ["damage"] = .5f }) }));
            Assert.IsTrue(builder.TryApplyCatalog(new[] { ForChild("damage", 80) }));
            var config = builder.Build();
            Assert.AreEqual(100, config.Stats.Cast.Damage, "부모는 바뀌지 않는다");
            Assert.AreEqual(90, RaiseOnce(config, recorder).Resolve(ChildBase()).Stats.Cast.Damage, .001f, "100 × 0.5 × 1.8");
        }

        [Test]
        public void ChildOnlyUpgrade_TakenBeforeTheLinkIsKeptAndAppliedOnceChildIsCast()
        {
            var recorder = new Recorder();
            var builder = NewBuilder(recorder);
            Assert.IsTrue(builder.TryApplyCatalog(new[] { ForChild("damage", 100) }), "연결이 아직 없어도 보관한다");
            Assert.IsTrue(builder.TryApplyCatalog(new[] { Link() }));
            Assert.AreEqual(6, RaiseOnce(builder.Build(), recorder).Resolve(ChildBase()).Stats.Cast.Damage, .001f, "자식 기본 3 × 2");
        }

        [Test]
        public void ChildOnlyUpgrades_StackInTheOrderTheyWereTaken()
        {
            var recorder = new Recorder();
            var builder = NewBuilder(recorder);
            Assert.IsTrue(builder.TryApplyCatalog(new[] { Link(), ForChild("damage", 100), ForChild("projectileSpeed", 50) }));
            var resolved = RaiseOnce(builder.Build(), recorder).Resolve(ChildBase());
            Assert.AreEqual(6, resolved.Stats.Cast.Damage, .001f);
            Assert.AreEqual(7.5f, resolved.Stats.Projectile.Speed, .001f);
        }

        [Test]
        public void DamageScale_ComesAfterInheritAndOverlay()
        {
            var recorder = new Recorder();
            var builder = NewBuilder(recorder);
            var link = Link(new Dictionary<string, float> { ["damage"] = 1 });
            link.damageScale = .5f;
            Assert.IsTrue(builder.TryApplyCatalog(new[] { link, ForChild("damage", 100) }));
            Assert.AreEqual(100, RaiseOnce(builder.Build(), recorder).Resolve(ChildBase()).Stats.Cast.Damage, .001f, "100 × 2 × 0.5");
        }

        // ── 연결 규칙 ─────────────────────────────────────────────────

        [Test]
        public void InheritRules_MergeAcrossCastSitesAndLaterValueWinsPerStat()
        {
            var recorder = new Recorder();
            var builder = NewBuilder(recorder);
            Assert.IsTrue(builder.TryApplyCatalog(new[] { Link(new Dictionary<string, float> { ["damage"] = .5f }) }));
            Assert.IsTrue(builder.TryApplyCatalog(new[] { Link(new Dictionary<string, float> { ["projectileSpeed"] = 1 }, AttackEvent.Kill) }), "규칙이 합쳐진다");
            var both = RaiseOnce(builder.Build(), recorder).Resolve(ChildBase());
            Assert.AreEqual(50, both.Stats.Cast.Damage, .001f);
            Assert.AreEqual(20, both.Stats.Projectile.Speed, "다른 시전 지점에서 더한 규칙도 같은 연결에 쌓인다");

            Assert.IsTrue(builder.TryApplyCatalog(new[] { Link(new Dictionary<string, float> { ["damage"] = .7f }, AttackEvent.Start) }));
            Assert.AreEqual(70, RaiseOnce(builder.Build(), recorder).Resolve(ChildBase()).Stats.Cast.Damage, .001f, "같은 스탯은 나중 값이 이긴다");
        }

        [Test]
        public void InheritEffect_AddsRulesWithoutCasting()
        {
            var recorder = new Recorder();
            var builder = NewBuilder(recorder);
            Assert.IsTrue(builder.TryApplyCatalog(new[]
            {
                Link(),
                new EffectDef { kind = "inherit", skillId = ChildId, inherit = new Dictionary<string, float> { ["damage"] = .25f } }
            }));
            Assert.AreEqual(25, RaiseOnce(builder.Build(), recorder).Resolve(ChildBase()).Stats.Cast.Damage, .001f);
        }

        // ── 손자: 보관 효과로 자식에게 반응을 붙이고, 보관 효과는 후손 전체에 적용된다 ──

        private const int GrandchildId = 3;

        private static SkillConfig GrandchildBase() => SkillConfig.FromDefinition(new WeaponData
        {
            id = GrandchildId,
            name = "손자",
            maxLevel = 5,
            upgrades = new List<WeaponUpgradeOption>(),
            baseStats = new WeaponBaseStats { cast = { baseDamage = 1, cooldown = 1, range = 10, projectileCount = 1 } }
        });

        [Test]
        public void AttachedReaction_MakesChildCastGrandchildThatInheritsFromChildNotRoot()
        {
            var recorder = new Recorder();
            var builder = NewBuilder(recorder);
            Assert.IsTrue(builder.TryApplyCatalog(new[]
            {
                Link(new Dictionary<string, float> { ["damage"] = .5f }),
                // 자식에게 "맞으면 손자를 시전한다"를 붙인다. 손자는 자식 피해의 절반을 가져온다.
                new EffectDef { kind = "onEvent", trigger = AttackEvent.Hit, skillId = GrandchildId, target = ChildId,
                    inherit = new Dictionary<string, float> { ["damage"] = .5f } },
                // 손자 피해 +100%는 어느 단계에서 시전되든 손자에게 적용된다.
                new EffectDef { kind = "damage", value = 100, target = GrandchildId }
            }));

            var childCast = RaiseOnce(builder.Build(), recorder);
            var child = childCast.Resolve(ChildBase(), recorder);
            Assert.AreEqual(50, child.Stats.Cast.Damage, .001f);

            var grandchildCast = RaiseOnce(child, recorder);
            Assert.AreEqual(GrandchildId, grandchildCast.SkillId);
            var grandchild = grandchildCast.Resolve(GrandchildBase(), recorder);
            Assert.AreEqual(50, grandchild.Stats.Cast.Damage, .001f, "자식 피해 50 × 0.5 × 2(보관 효과)");
        }

        [Test]
        public void AttachedReaction_TakenBeforeParentLinksTheChild_StillAttachesWhenChildIsCast()
        {
            var recorder = new Recorder();
            var builder = NewBuilder(recorder);
            Assert.IsTrue(builder.TryApplyCatalog(new[]
            {
                new EffectDef { kind = "onEvent", trigger = AttackEvent.Hit, skillId = GrandchildId, target = ChildId }
            }));
            Assert.IsTrue(builder.TryApplyCatalog(new[] { Link() }), "자식은 나중에 연결돼도 된다");
            var child = RaiseOnce(builder.Build(), recorder).Resolve(ChildBase(), recorder);
            Assert.AreEqual(GrandchildId, RaiseOnce(child, recorder).SkillId);
        }

        [Test]
        public void OnlyForm_AttachesReactionOnlyWhileTheFormMatches()
        {
            var recorder = new Recorder();
            var builder = NewBuilder(recorder);
            var guarded = Link();
            guarded.onlyForm = "Default";
            Assert.IsTrue(builder.TryApplyCatalog(new[] { guarded }));
            Assert.AreEqual(1, RaiseCount(builder.Build(), recorder), "기본 형태에서는 시전한다");

            Assert.IsTrue(builder.TryApplyCatalog(new[] { new EffectDef { kind = "form", value = (int)WeaponForm.TriangleIce } }));
            Assert.AreEqual(0, RaiseCount(builder.Build(), recorder), "형태가 바뀌면 이 반응은 빠진다");
        }

        private static int RaiseCount(SkillConfig config, Recorder recorder)
        {
            recorder.Casts.Clear();
            config.Reactions.Raise(AttackEvent.Hit, new AttackContext(Vector2.zero, Vector3.up));
            return recorder.Casts.Count;
        }

        [Test]
        public void InvalidOnlyForm_IsRejected()
        {
            var effect = Link();
            effect.onlyForm = "NoSuchForm";
            Assert.IsFalse(NewBuilder(new Recorder()).TryApplyCatalog(new[] { effect }));
        }

        [Test]
        public void InvalidInherit_IsRejected()
        {
            var recorder = new Recorder();
            Assert.IsFalse(NewBuilder(recorder).TryApplyCatalog(new[] { Link(new Dictionary<string, float> { ["noSuchStat"] = 1 }) }));
            Assert.IsFalse(NewBuilder(recorder).TryApplyCatalog(new[] { Link(new Dictionary<string, float> { ["damage"] = 0 }) }));
            Assert.IsFalse(NewBuilder(recorder).TryApplyCatalog(new[] { Link(new Dictionary<string, float> { ["damage"] = -1 }) }));
        }

        [Test]
        public void Json_DeserializesInheritAndTarget()
        {
            var link = JsonConvert.DeserializeObject<EffectDef>("{\"kind\":\"onEvent\",\"trigger\":\"Hit\",\"skillId\":2,\"inherit\":{\"damage\":0.5,\"projectileSpeed\":1}}");
            Assert.AreEqual(.5f, link.inherit["damage"]);
            var overlay = JsonConvert.DeserializeObject<EffectDef>("{\"kind\":\"damage\",\"value\":80,\"target\":2}");
            Assert.AreEqual(2, overlay.target);
        }

        // ── 기존 하드코딩 분열 공식과의 일치 ───────────────────────────

        [Test]
        public void ParityWithHardCodedSplitShardDamage()
        {
            // 기존: 조각 피해 = 부모 피해 × 0.5 × 분열 피해 배율(+80% → 1.8). 부모를 강화하면 따라 오른다.
            var recorder = new Recorder();
            var builder = NewBuilder(recorder);
            Assert.IsTrue(builder.TryApplyCatalog(new[] { Link(new Dictionary<string, float> { ["damage"] = .5f }), ForChild("damage", 80) }));
            Assert.AreEqual(90f, RaiseOnce(builder.Build(), recorder).Resolve(ChildBase()).Stats.Cast.Damage, .001f);

            Assert.AreEqual(100f * .5f * 1.8f, 90f, .001f, "옛 하드코딩 공식(부모 피해 × 0.5 × 분열 배율)과 같은 값");

            Assert.IsTrue(builder.TryApplyCatalog(new[] { new EffectDef { kind = "damage", value = 100 } }));
            Assert.AreEqual(180f, RaiseOnce(builder.Build(), recorder).Resolve(ChildBase()).Stats.Cast.Damage, .001f, "부모 피해 200 × 0.5 × 1.8");
        }

        [Test]
        public void ParityWithHardCodedKillLightningRatio()
        {
            // 기존: 처치 번개 피해 = 부모 피해 × 처치 번개 비율(25%)
            var recorder = new Recorder();
            var builder = NewBuilder(recorder);
            var link = Link(new Dictionary<string, float> { ["damage"] = .25f }, AttackEvent.Kill);
            Assert.IsTrue(builder.TryApplyCatalog(new[] { link }));
            Assert.AreEqual(25f, RaiseOnce(builder.Build(), recorder, AttackEvent.Kill).Resolve(ChildBase()).Stats.Cast.Damage, .001f);
        }

        // ── 카탈로그 검증 ─────────────────────────────────────────────

        private static List<WeaponData> Catalog(Action<WeaponData, WeaponData> configure)
        {
            var parent = Parent();
            var child = Child();
            parent.castType = CastType.Projectile;
            child.castType = CastType.Projectile;
            parent.iconKey = "p"; child.iconKey = "c";
            configure(parent, child);
            return new List<WeaponData> { parent, child };
        }

        private static WeaponUpgradeOption Card(string id, params EffectDef[] effects) =>
            new WeaponUpgradeOption { id = id, name = id, desc = id, maxPickCount = 1, effects = new List<EffectDef>(effects) };

        [Test]
        public void Validator_AcceptsOverlayWhenSomeCardCastsTheChildEvenWithoutPrerequisite()
        {
            var catalog = Catalog((p, c) =>
            {
                p.upgrades.Add(Card("link", Link()));
                p.upgrades.Add(Card("boost", ForChild("damage", 80)));   // 선행 조건이 없어도 된다. 보관했다가 자식이 시전될 때 적용한다
            });
            Assert.DoesNotThrow(() => WeaponCatalogValidator.Validate(catalog));
        }

        [Test]
        public void Validator_AcceptsOverlayInTheSameCardAsTheLink()
        {
            var catalog = Catalog((p, c) => p.upgrades.Add(Card("both", Link(), ForChild("damage", 80))));
            Assert.DoesNotThrow(() => WeaponCatalogValidator.Validate(catalog));
        }

        [Test]
        public void Validator_RejectsOverlayOnSkillThatNoCardEverCasts()
        {
            var catalog = Catalog((p, c) => p.upgrades.Add(Card("boost", ForChild("damage", 80))));
            var error = Assert.Throws<InvalidOperationException>(() => WeaponCatalogValidator.Validate(catalog));
            StringAssert.Contains("boost", error.Message);
            StringAssert.Contains("시전되는 카드가 없어", error.Message);
        }

        [Test]
        public void Validator_FollowsAttachedReactionsToReachGrandchildren()
        {
            var catalog = Catalog((p, c) =>
            {
                var grand = Child(); grand.id = GrandchildId; grand.name = "손자"; grand.castType = CastType.Projectile; grand.iconKey = "g";
                p.upgrades.Add(Card("link", Link()));
                p.upgrades.Add(Card("attach", new EffectDef { kind = "onEvent", trigger = AttackEvent.Hit, skillId = GrandchildId, target = ChildId }));
                p.upgrades.Add(Card("boost", new EffectDef { kind = "damage", value = 10, target = GrandchildId }));
                catalogExtra = grand;
            });
            catalog.Add(catalogExtra);
            Assert.DoesNotThrow(() => WeaponCatalogValidator.Validate(catalog));
        }

        private static WeaponData catalogExtra;

        [Test]
        public void Validator_RejectsCycleCreatedByAttachedReaction()
        {
            var catalog = Catalog((p, c) =>
            {
                p.upgrades.Add(Card("link", Link()));
                // 부모 → 자식, 자식 → 부모는 자기 순환이다.
                p.upgrades.Add(Card("loop", new EffectDef { kind = "onEvent", trigger = AttackEvent.Hit, skillId = ChildId, target = ChildId }));
            });
            Assert.Throws<InvalidOperationException>(() => WeaponCatalogValidator.Validate(catalog));
        }

        [Test]
        public void Validator_RejectsOverlayEffectTheChildAttackDoesNotConsume()
        {
            var catalog = Catalog((p, c) =>
            {
                c.castType = CastType.Hitscan;
                p.upgrades.Add(Card("link", Link(), ForChild("pierceCount", 1)));   // Hitscan 자식은 관통을 쓰지 않는다
            });
            var error = Assert.Throws<InvalidOperationException>(() => WeaponCatalogValidator.Validate(catalog));
            StringAssert.Contains("pierceCount", error.Message);
        }

        [Test]
        public void Validator_RejectsUnknownOrSelfTargetAndUnknownInheritStat()
        {
            var unknown = Catalog((p, c) => p.upgrades.Add(Card("x", new EffectDef { kind = "damage", value = 1, target = 99 })));
            Assert.Throws<InvalidOperationException>(() => WeaponCatalogValidator.Validate(unknown));
            var self = Catalog((p, c) => p.upgrades.Add(Card("x", new EffectDef { kind = "damage", value = 1, target = ParentId })));
            Assert.Throws<InvalidOperationException>(() => WeaponCatalogValidator.Validate(self));
            var badStat = Catalog((p, c) => p.upgrades.Add(Card("x", Link(new Dictionary<string, float> { ["noSuchStat"] = 1 }))));
            var error = Assert.Throws<InvalidOperationException>(() => WeaponCatalogValidator.Validate(badStat));
            StringAssert.Contains("inherit", error.Message);
        }
    }
}
