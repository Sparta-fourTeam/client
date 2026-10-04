using System.Collections.Generic;
using System.Reflection;
using Game.Core;
using Game.Core.Messages;
using MessagePipe;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>WeaponController가 무기를 얻거나 강화할 때 보유 목록 스냅샷을 SkillChanged로 발행하는지 본다.
    /// MonoBehaviour라 EditMode에서는 Start가 호출되지 않고 프리팹 목록은 직렬화 필드라서, 리플렉션으로 같은 상태를 만든다</summary>
    public sealed class WeaponControllerSkillPublishTests
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
        private WeaponController _controller;

        [SetUp]
        public void SetUp()
        {
            _publisher = new FakeSkillPublisher();

            var player = new GameObject("Player");
            _created.Add(player);
            _controller = player.AddComponent<WeaponController>();

            var entries = new List<WeaponPrefabEntry>();
            for (int id = 1; id <= 3; id++)
            {
                var prefab = new GameObject($"WeaponPrefab{id}");
                _created.Add(prefab);
                entries.Add(new WeaponPrefabEntry { id = id, prefab = prefab });
            }

            const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
            typeof(WeaponController).GetField("prefabEntries", Private).SetValue(_controller, entries);

            _controller.Construct(new NullEnemyTargetProvider(), _publisher, new DefaultWeaponDataProvider());
            typeof(WeaponController).GetMethod("Start", Private).Invoke(_controller, null);
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

        [Test(Description = "시작 무기가 생길 때 보유 목록을 한 번 발행한다 (HUD가 처음부터 채워진다). 어떤 무기로 시작하는지는 정하지 않는다")]
        public void Start_PublishesStartingWeapon()
        {
            Assert.AreEqual(1, _publisher.Published.Count);
            var skills = _publisher.Published[0].Skills;
            Assert.AreEqual(1, skills.Count);
            Assert.AreEqual(1, skills[0].Level);
            Assert.IsNotEmpty(skills[0].IconKey);
        }

        [Test(Description = "새 무기를 얻으면 보유 목록 전체를 다시 발행한다")]
        public void AcquireWeapon_PublishesFullSnapshot()
        {
            var newWeapon = _controller.GetRandomUpgradeChoices(50).Find(c => c.IsNewWeapon);

            _controller.ApplyUpgradeChoice(newWeapon);

            Assert.AreEqual(2, _publisher.Published.Count);
            var skills = _publisher.Published[1].Skills;
            Assert.AreEqual(2, skills.Count);
            Assert.AreEqual(_publisher.Published[0].Skills[0].Id, skills[0].Id, "시작 무기는 그대로 앞에 있다");
            Assert.AreEqual(newWeapon.NewWeaponData.id, skills[1].Id);
        }

        [Test(Description = "기존 무기를 강화하면 올라간 레벨로 다시 발행한다")]
        public void LevelUp_PublishesRaisedLevel()
        {
            var upgrade = _controller.GetRandomUpgradeChoices(50).Find(c => !c.IsNewWeapon);

            _controller.ApplyUpgradeChoice(upgrade);

            Assert.AreEqual(2, _publisher.Published.Count);
            var skills = _publisher.Published[1].Skills;
            Assert.AreEqual(1, skills.Count);
            Assert.AreEqual(2, skills[0].Level);
        }

        [Test(Description = "발행된 목록은 그 시점의 사본이라 나중에 무기를 얻어도 이전 발행의 개수가 바뀌지 않는다")]
        public void Snapshot_IsACopy()
        {
            var first = _publisher.Published[0].Skills;
            var newWeapon = _controller.GetRandomUpgradeChoices(50).Find(c => c.IsNewWeapon);

            _controller.ApplyUpgradeChoice(newWeapon);

            Assert.AreEqual(1, first.Count);
        }

        [Test]
        public void StaleChoice_DoesNotPublishOrIncreaseLevelAgain()
        {
            var choice = _controller.GetRandomUpgradeChoices(50).Find(c => !c.IsNewWeapon);
            for (int i = 0; i < choice.Option.maxPickCount; i++)
            {
                Assert.IsTrue(_controller.ApplyUpgradeChoice(choice));
            }
            int level = choice.Weapon.Level;
            int published = _publisher.Published.Count;
            Assert.IsFalse(_controller.ApplyUpgradeChoice(choice));
            Assert.AreEqual(level, choice.Weapon.Level);
            Assert.AreEqual(published, _publisher.Published.Count);
        }

        [Test]
        public void ForgedChoice_IsRejected()
        {
            var choice = _controller.GetRandomUpgradeChoices(50).Find(c => !c.IsNewWeapon);
            choice.Option = new WeaponUpgradeOption { id = "forged", effects = new List<StatEffect>() };
            Assert.IsFalse(_controller.ApplyUpgradeChoice(choice));
            Assert.AreEqual(1, choice.Weapon.Level);
            Assert.AreEqual(1, _publisher.Published.Count);
        }

        [Test]
        public void PermanentVariant_ChoiceUsesSnapshotForPresentationAndApplication()
        {
            const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
            var levels = (Dictionary<int, int>)typeof(WeaponController).GetField("permanentLevels", Private).GetValue(_controller);
            levels[3] = 9;
            var choice = _controller.GetRandomUpgradeChoices(50).Find(c => c.Option?.id == "lightning_burst");
            Assert.AreEqual("연속 벼락(+)", choice.DisplayName);
            Assert.AreEqual("시전 수 +1", choice.DisplayDescription);
            Assert.IsTrue(_controller.ApplyUpgradeChoice(choice));
            var stats = (IWeaponStats)typeof(WeaponBase).GetField("stats", Private).GetValue(choice.Weapon);
            Assert.AreEqual(choice.Weapon.Data.baseStats.baseDamage, stats.Damage);
            Assert.AreEqual(2, stats.CastCount);
            Assert.AreEqual(1, choice.Weapon.GetAcquiredCount("lightning_burst"));
        }

        [Test]
        public void SharedChoices_AreOfferedOnceAndPublishBothUpdatedWeapons()
        {
            Assert.IsTrue(_controller.AddWeapon(1));
            const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
            var weapons = (List<WeaponBase>)typeof(WeaponController).GetField("weapons", Private).GetValue(_controller);
            foreach (var weapon in weapons)
            {
                weapon.Data.upgrades.Add(new WeaponUpgradeOption
                {
                    id = "synthetic_pair_" + weapon.Data.id,
                    name = "테스트 공유 강화",
                    sharedId = "synthetic_pair",
                    affectedWeaponIds = new[] { 1, 3 },
                    maxPickCount = 1,
                    effects = new List<StatEffect> { new StatEffect { type = UpgradeType.Damage, value = 80 } }
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

        [Test]
        public void NonPositiveChoiceCount_ReturnsEmpty()
        {
            Assert.IsEmpty(_controller.GetRandomUpgradeChoices(-1));
            Assert.IsEmpty(_controller.GetRandomUpgradeChoices(0));
        }
    }
}
