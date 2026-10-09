using System;
using System.Collections.Generic;
using Game.Core;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>자식과 손자의 시전, 형태 조건, 잘못된 자식 연결의 거절을 확인한다.</summary>
    public sealed class ChildStatReferenceTests
    {
        private const int ParentId = 1;
        private const int ChildId = 2;

        private sealed class Recorder : IChildSkillCaster
        {
            public readonly List<ChildCast> Casts = new List<ChildCast>();
            public void Cast(ChildCast cast, AttackContext context) => Casts.Add(cast);
        }

        private static SkillData Parent(float damage = 100) => new SkillData
        {
            id = ParentId,
            name = "부모",
            maxLevel = 5,
            upgrades = new List<SkillUpgradeOption>(),
            baseStats = new SkillBaseStats
            {
                cast = { baseDamage = damage, cooldown = 1, range = 10, projectileCount = 1 },
                projectile = { speed = 20, pierceCount = 1 },
                status = { freezeDuration = 2 }
            }
        };

        private static SkillData Child() => new SkillData
        {
            id = ChildId,
            name = "자식",
            maxLevel = 5,
            upgrades = new List<SkillUpgradeOption>(),
            baseStats = new SkillBaseStats
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

        // ── 손자: 보관 효과로 자식에게 반응을 붙이고, 보관 효과는 후손 전체에 적용된다 ──

        private const int GrandchildId = 3;

        [Test]
        public void AttachedReaction_MakesChildCastGrandchild()
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

            var grandchildCast = RaiseOnce(child, recorder);
            Assert.AreEqual(GrandchildId, grandchildCast.SkillId);
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

            Assert.IsTrue(builder.TryApplyCatalog(new[] { new EffectDef { kind = "form", value = (int)SkillForm.TriangleIce } }));
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

        // ── 카탈로그 검증 ─────────────────────────────────────────────

        private static List<SkillData> Catalog(Action<SkillData, SkillData> configure)
        {
            var parent = Parent();
            var child = Child();
            parent.castType = CastType.Projectile;
            child.castType = CastType.Projectile;
            parent.assetKey = "p"; child.assetKey = "c";
            configure(parent, child);
            return new List<SkillData> { parent, child };
        }

        private static SkillUpgradeOption Card(string id, params EffectDef[] effects) =>
            new SkillUpgradeOption { id = id, name = id, desc = id, maxPickCount = 1, effects = new List<EffectDef>(effects) };

        [Test]
        public void Validator_AcceptsOverlayWhenSomeCardCastsTheChildEvenWithoutPrerequisite()
        {
            var catalog = Catalog((p, c) =>
            {
                p.upgrades.Add(Card("link", Link()));
                p.upgrades.Add(Card("boost", ForChild("damage", 80)));   // 선행 조건이 없어도 된다. 보관했다가 자식이 시전될 때 적용한다
            });
            Assert.DoesNotThrow(() => SkillCatalogValidator.Validate(catalog));
        }

        [Test]
        public void Validator_AcceptsOverlayInTheSameCardAsTheLink()
        {
            var catalog = Catalog((p, c) => p.upgrades.Add(Card("both", Link(), ForChild("damage", 80))));
            Assert.DoesNotThrow(() => SkillCatalogValidator.Validate(catalog));
        }

        [Test]
        public void Validator_RejectsOverlayOnSkillThatNoCardEverCasts()
        {
            var catalog = Catalog((p, c) => p.upgrades.Add(Card("boost", ForChild("damage", 80))));
            var error = Assert.Throws<InvalidOperationException>(() => SkillCatalogValidator.Validate(catalog));
            StringAssert.Contains("boost", error.Message);
            StringAssert.Contains("시전되는 카드가 없어", error.Message);
        }

        [Test]
        public void Validator_FollowsAttachedReactionsToReachGrandchildren()
        {
            var catalog = Catalog((p, c) =>
            {
                var grand = Child(); grand.id = GrandchildId; grand.name = "손자"; grand.castType = CastType.Projectile; grand.assetKey = "g";
                p.upgrades.Add(Card("link", Link()));
                p.upgrades.Add(Card("attach", new EffectDef { kind = "onEvent", trigger = AttackEvent.Hit, skillId = GrandchildId, target = ChildId }));
                p.upgrades.Add(Card("boost", new EffectDef { kind = "damage", value = 10, target = GrandchildId }));
                catalogExtra = grand;
            });
            catalog.Add(catalogExtra);
            Assert.DoesNotThrow(() => SkillCatalogValidator.Validate(catalog));
        }

        private static SkillData catalogExtra;

        [Test]
        public void Validator_RejectsCycleCreatedByAttachedReaction()
        {
            var catalog = Catalog((p, c) =>
            {
                p.upgrades.Add(Card("link", Link()));
                // 부모 → 자식, 자식 → 부모는 자기 순환이다.
                p.upgrades.Add(Card("loop", new EffectDef { kind = "onEvent", trigger = AttackEvent.Hit, skillId = ChildId, target = ChildId }));
            });
            Assert.Throws<InvalidOperationException>(() => SkillCatalogValidator.Validate(catalog));
        }

        [Test]
        public void Validator_RejectsOverlayEffectTheChildAttackDoesNotConsume()
        {
            var catalog = Catalog((p, c) =>
            {
                c.castType = CastType.Hitscan;
                p.upgrades.Add(Card("link", Link(), ForChild("pierceCount", 1)));   // Hitscan 자식은 관통을 쓰지 않는다
            });
            var error = Assert.Throws<InvalidOperationException>(() => SkillCatalogValidator.Validate(catalog));
            StringAssert.Contains("pierceCount", error.Message);
        }

        [Test]
        public void Validator_RejectsUnknownOrSelfTargetAndUnknownInheritStat()
        {
            var unknown = Catalog((p, c) => p.upgrades.Add(Card("x", new EffectDef { kind = "damage", value = 1, target = 99 })));
            Assert.Throws<InvalidOperationException>(() => SkillCatalogValidator.Validate(unknown));
            var self = Catalog((p, c) => p.upgrades.Add(Card("x", new EffectDef { kind = "damage", value = 1, target = ParentId })));
            Assert.Throws<InvalidOperationException>(() => SkillCatalogValidator.Validate(self));
            var badStat = Catalog((p, c) => p.upgrades.Add(Card("x", Link(new Dictionary<string, float> { ["noSuchStat"] = 1 }))));
            var error = Assert.Throws<InvalidOperationException>(() => SkillCatalogValidator.Validate(badStat));
            StringAssert.Contains("inherit", error.Message);
        }
    }
}
