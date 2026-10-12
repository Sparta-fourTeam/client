using System.Collections.Generic;
using System.Reflection;
using Game.Core;
using Game.Core.Messages;
using MessagePipe;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>스킬 획득·강화·거절 뒤 실제 보유 상태와 메시지 스냅샷을 검증한다. 런타임 시작 검사는 SkillLifecycleTests에서 실행한다.</summary>
    public sealed class SkillControllerSkillPublishTests
    {
        private sealed class FakeSkillPublisher : IPublisher<SkillChanged>, IBufferedPublisher<SkillChanged>
        {
            public readonly List<SkillChanged> Published = new();

            public void Publish(SkillChanged message)
            {
                Published.Add(message);
            }
        }

        private readonly List<GameObject> _created = new();
        private FakeSkillPublisher _publisher;
        private SkillController _controller;

        [SetUp]
        public void SetUp()
        {
            _publisher = new FakeSkillPublisher();

            var player = new GameObject("Player");
            _created.Add(player);
            _controller = player.AddComponent<SkillController>();

            TestSkillAssets.Attach(_controller, new[] { 1, 2, 3, 4 }, _created);
            _controller.Construct(new NullEnemyTargetProvider(), _publisher, new DefaultSkillDataProvider(new GameDataStore()));
            TestSkillAssets.PrepareForRules(_controller, 3);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _created)
            {
                if (go != null)
                {
                    Object.DestroyImmediate(go);
                }
            }

            _created.Clear();
        }

        [Test(Description = "ClearWeapons는 보유 스킬을 모두 정리하고 빈 목록을 발행한다")]
        public void ClearWeapons_DisposesAllAndPublishesEmptySnapshot()
        {
            Assert.IsTrue(_controller.AddWeapon(1));
            Assert.AreEqual(2, _controller.Skills.Count, "시작 스킬과 추가한 스킬");
            _controller.ClearWeapons();
            Assert.AreEqual(0, _controller.Skills.Count);
            Assert.AreEqual(0, _publisher.Published[_publisher.Published.Count - 1].Skills.Count);
            Assert.IsTrue(_controller.AddWeapon(1), "정리 후에는 같은 스킬을 다시 얻을 수 있다");
        }

        [Test]
        public void AcquisitionAndUpgradePublishOwnedSkillsWithoutChangingEarlierMembership()
        {
            var initial = _publisher.Published[0].Skills;
            int initialId = initial[0].Id;
            var acquisition = _controller.GetRandomUpgradeChoices(50).Find(c => c.IsNewWeapon);
            Assert.IsTrue(_controller.ApplyUpgradeChoice(acquisition));
            Assert.AreEqual(2, _controller.Skills.Count);
            var acquired = _publisher.Published[_publisher.Published.Count - 1].Skills;
            Assert.AreEqual(2, acquired.Count);
            Assert.AreEqual(initialId, acquired[0].Id);
            Assert.AreEqual(acquisition.newSkillData.id, acquired[1].Id);
            var upgrade = _controller.GetRandomUpgradeChoices(50).Find(c => !c.IsNewWeapon && c.skill.Data.id == initialId);
            Assert.IsTrue(_controller.ApplyUpgradeChoice(upgrade));
            Assert.AreEqual(2, _controller.GetWeaponLevel(initialId));
            var upgraded = _publisher.Published[_publisher.Published.Count - 1].Skills;
            Assert.AreEqual(2, upgraded.Count);
            Assert.AreEqual(2, upgraded[0].Level);
            Assert.AreEqual(acquisition.newSkillData.id, upgraded[1].Id);
            Assert.AreEqual(1, initial.Count, "획득 후에도 초기 목록은 변하지 않는다");
            Assert.AreEqual(2, acquired.Count, "강화는 이전 목록 구성을 변경하지 않는다");
        }

        [Test]
        public void StaleChoice_DoesNotPublishOrIncreaseLevelAgain()
        {
            var choice = _controller.GetRandomUpgradeChoices(50).Find(c => !c.IsNewWeapon);
            for (int i = 0; i < choice.Option.maxPickCount; i++)
            {
                Assert.IsTrue(_controller.ApplyUpgradeChoice(choice));
            }
            int level = choice.skill.Level;
            int published = _publisher.Published.Count;
            Assert.IsFalse(_controller.ApplyUpgradeChoice(choice));
            Assert.AreEqual(level, choice.skill.Level);
            Assert.AreEqual(published, _publisher.Published.Count);
        }

        [Test]
        public void ForgedChoice_IsRejected()
        {
            var choice = _controller.GetRandomUpgradeChoices(50).Find(c => !c.IsNewWeapon);
            choice.Option = new SkillUpgradeOption { id = "forged", effects = new List<EffectDef>() };
            Assert.IsFalse(_controller.ApplyUpgradeChoice(choice));
            Assert.AreEqual(1, choice.skill.Level);
            Assert.AreEqual(1, _publisher.Published.Count);
        }

        [Test]
        public void SharedChoices_AreOfferedOnceAndPublishBothUpdatedWeapons()
        {
            Assert.IsTrue(_controller.AddWeapon(1));
            const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
            var weapons = (List<SkillBase>)typeof(SkillController).GetField("skills", Private).GetValue(_controller);
            foreach (var weapon in weapons)
            {
                weapon.Data.upgrades.Add(new SkillUpgradeOption
                {
                    id = "synthetic_pair_" + weapon.Data.id,
                    name = "테스트 공유 강화",
                    sharedId = "synthetic_pair",
                    affectedWeaponIds = new[] { 1, 3 },
                    maxPickCount = 1,
                    effects = new List<EffectDef> { new EffectDef { kind = "damage", value = 80 } }
                });
            }
            var choices = _controller.GetRandomUpgradeChoices(100).FindAll(c => c.Option?.sharedId == "synthetic_pair");
            Assert.AreEqual(1, choices.Count);
            int published = _publisher.Published.Count;
            Assert.IsTrue(_controller.ApplyUpgradeChoice(choices[0]));
            Assert.AreEqual(published + 1, _publisher.Published.Count);
            Assert.AreEqual(2, _controller.GetWeaponLevel(1));
            Assert.AreEqual(2, _controller.GetWeaponLevel(3));
            Assert.AreEqual(1, _controller.GetAcquiredCount(1, "synthetic_pair_1"));
            Assert.AreEqual(1, _controller.GetAcquiredCount(3, "synthetic_pair_3"));
            Assert.IsEmpty(_controller.GetRandomUpgradeChoices(100).FindAll(c => c.Option?.sharedId == "synthetic_pair"));
        }

        /// <summary>테스트가 정한 영구 강화 효과를 한 progressionId에만 돌려준다 (출시 수치와 무관하게 전투 반영 경로만 본다)</summary>
        private sealed class FixedPermanentEffects : IPermanentSkillEffects
        {
            private readonly string _progressionId;
            private readonly EffectDef[] _effects;

            public FixedPermanentEffects(string progressionId, params EffectDef[] effects)
            {
                _progressionId = progressionId;
                _effects = effects;
            }

            public IReadOnlyList<EffectDef> For(string progressionId) =>
                progressionId == _progressionId ? _effects : System.Array.Empty<EffectDef>();
        }

        private SkillController CreateController(IPermanentSkillEffects permanentEffects)
        {
            var player = new GameObject("PermanentPlayer");
            _created.Add(player);
            var controller = player.AddComponent<SkillController>();
            TestSkillAssets.Attach(controller, new[] { 1, 2 }, _created);
            controller.Construct(new NullEnemyTargetProvider(), new FakeSkillPublisher(), new DefaultSkillDataProvider(new GameDataStore()),
                permanentEffects: permanentEffects);
            TestSkillAssets.PrepareForRules(controller, 1, 2);
            return controller;
        }

        [Test(Description = "로비 영구 강화 효과가 전투 스킬의 시작 설정에 들어가고, 판 안 카드 강화는 그 설정 위에 쌓인다 (C-13)")]
        public void PermanentEffects_ApplyToBattleSkill_AndCardsStackOnTop()
        {
            var progressionId = TestSkillAssets.Data(1).progressionId;
            Assert.IsNotEmpty(progressionId, "영구 강화가 있는 스킬로 검사한다");
            var plain = CreateController(null);
            var upgraded = CreateController(new FixedPermanentEffects(progressionId,
                new EffectDef { kind = "damage", value = 50 }, new EffectDef { kind = "attackSpeed", value = 20 }));

            SkillBase Skill(SkillController c, int id)
            {
                foreach (var skill in c.Skills)
                {
                    if (skill.Data.id == id) { return skill; }
                }
                return null;
            }
            void AssertPermanentApplied(string when)
            {
                var basic = Skill(plain, 1).Stats.Cast;
                var boosted = Skill(upgraded, 1).Stats.Cast;
                Assert.AreEqual(basic.Damage * 1.5f, boosted.Damage, 1e-3f, $"{when}: 피해 +50%가 실제 시전 설정에 남는다");
                Assert.AreEqual(basic.Cooldown * 0.8f, boosted.Cooldown, 1e-4f, $"{when}: 공격 속도 +20%가 쿨타임에 남는다");
            }

            AssertPermanentApplied("획득 직후");
            Assert.AreEqual(Skill(plain, 2).Stats.Cast.Damage, Skill(upgraded, 2).Stats.Cast.Damage,
                "다른 progressionId의 스킬에는 적용하지 않는다");

            var card = upgraded.GetUpgradeCandidates().Find(c => !c.IsNewWeapon && c.skill.Data.id == 1 && string.IsNullOrEmpty(c.Option.sharedId));
            Assert.IsNotNull(card.Option, "강화할 수 있는 카드가 있어야 한다");
            var sameCard = plain.GetUpgradeCandidates().Find(c => !c.IsNewWeapon && c.skill.Data.id == 1 && c.Option.id == card.Option.id);
            Assert.IsTrue(upgraded.ApplyUpgradeChoice(card));
            Assert.IsTrue(plain.ApplyUpgradeChoice(sameCard));
            AssertPermanentApplied($"카드 {card.Option.id} 적용 뒤");
        }

    }
}
