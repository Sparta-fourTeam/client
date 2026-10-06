using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Core.Defense;
using Game.Core.Messages;
using MessagePipe;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class CardDeckTests
    {
        private sealed class FakeSkills : IUpgradeChoiceSource
        {
            public int Count;
            public List<UpgradeChoice> Applied { get; } = new List<UpgradeChoice>();

            public List<UpgradeChoice> GetUpgradeCandidates() =>
                Enumerable.Range(0, Count).Select(i => new UpgradeChoice { DisplayName = "skill" + i }).ToList();

            public bool ApplyUpgradeChoice(UpgradeChoice choice)
            {
                Applied.Add(choice);
                return true;
            }
        }

        private sealed class Publisher<T> : IPublisher<T>, IBufferedPublisher<T>
        {
            public void Publish(T message) { }
        }

        // 항상 같은 값을 돌려주는 랜덤 (섞기 결과를 고정한다)
        private sealed class FixedRandom : IRandomProvider
        {
            public float Range(float min, float max) => min;
        }

        private GameObject wallObject;
        private Wall wall;
        private FakeSkills skills;

        [SetUp]
        public void SetUp()
        {
            wallObject = new GameObject("Wall");
            wall = wallObject.AddComponent<Wall>();
            wall.Construct(new Publisher<WallHpChanged>(), new Publisher<WallDestroyed>(), null);
            wall.Initialize(100);
            skills = new FakeSkills { Count = 5 };
        }

        [TearDown]
        public void TearDown() => UnityEngine.Object.DestroyImmediate(wallObject);

        private static GeneralCardDefinition Card(string id, bool forced, int priority = 0, string condition = null, float below = 50f, int maxPicks = 999) =>
            new GeneralCardDefinition
            {
                Id = id,
                Name = id,
                MaxPicks = maxPicks,
                Forced = forced,
                Priority = priority,
                Condition = condition == null ? null : new GeneralCardRule { Kind = condition, Value = below },
                Effect = new GeneralCardRule { Kind = "wallRepair", Value = 20 },
            };

        private CardDeck Deck(params GeneralCardDefinition[] cards) =>
            new CardDeck(skills, wall, cards, new FixedRandom());

        [Test(Description = "방벽 체력이 기준 이하이면 방벽 수리가 3장 중 한 칸을 차지한다")]
        public void Draw_WallLow_IncludesForcedCard()
        {
            wall.TakeDamage(50);
            var deck = Deck(Card("repair", true, condition: "wallHpBelow"));

            var choices = deck.Draw(3);

            Assert.AreEqual(3, choices.Count);
            Assert.AreEqual(1, choices.Count(c => c.IsGeneral));
            Assert.AreEqual(2, choices.Count(c => !c.IsGeneral));
        }

        [Test(Description = "방벽 체력이 기준보다 높으면 나오지 않는다")]
        public void Draw_WallHealthy_ExcludesCard()
        {
            wall.TakeDamage(49);
            var deck = Deck(Card("repair", true, condition: "wallHpBelow"));

            Assert.IsFalse(deck.Draw(3).Any(c => c.IsGeneral));
        }

        [Test(Description = "스킬 후보가 없어도 조건이 맞으면 방벽 수리만으로 선택이 열린다")]
        public void Draw_NoSkillsButWallLow_OffersOnlyRepair()
        {
            skills.Count = 0;
            wall.TakeDamage(80);

            var choices = Deck(Card("repair", true, condition: "wallHpBelow")).Draw(3);

            Assert.AreEqual(1, choices.Count);
            Assert.IsTrue(choices[0].IsGeneral);
        }

        [Test(Description = "스킬 후보도 없고 조건도 안 맞으면 빈 목록이다")]
        public void Draw_NothingOffered_IsEmpty()
        {
            skills.Count = 0;

            Assert.IsEmpty(Deck(Card("repair", true, condition: "wallHpBelow")).Draw(3));
        }

        [Test(Description = "Forced 카드가 여럿이면 한 장만, 우선순위가 높은 쪽이 나온다")]
        public void Draw_MultipleForced_OnlyHighestPriority()
        {
            wall.TakeDamage(80);
            var deck = Deck(
                Card("low", true, priority: 1, condition: "wallHpBelow"),
                Card("high", true, priority: 5, condition: "wallHpBelow"));

            var general = deck.Draw(3).Where(c => c.IsGeneral).ToList();

            Assert.AreEqual(1, general.Count);
            Assert.AreEqual("high", general[0].GeneralCard.Id);
        }

        [Test(Description = "Forced가 아닌 일반 카드는 스킬 후보와 함께 섞이는 후보가 된다")]
        public void Draw_NonForcedGeneralCard_JoinsPool()
        {
            skills.Count = 0;
            var deck = Deck(Card("bonus", false), Card("bonus2", false));

            var choices = deck.Draw(3);

            Assert.AreEqual(2, choices.Count);
            Assert.IsTrue(choices.All(c => c.IsGeneral));
        }

        [Test(Description = "MaxPicks만큼 고르면 더 나오지 않는다")]
        public void Draw_AfterMaxPicks_NotOfferedAgain()
        {
            wall.TakeDamage(80);
            var deck = Deck(Card("repair", true, condition: "wallHpBelow", maxPicks: 1));
            var choice = deck.Draw(3).Single(c => c.IsGeneral);

            Assert.IsTrue(deck.TryApply(choice));

            wall.TakeDamage(80);
            Assert.IsFalse(deck.Draw(3).Any(c => c.IsGeneral));
        }

        [Test(Description = "방벽 수리를 고르면 방벽이 회복된다")]
        public void TryApply_WallRepair_HealsWall()
        {
            wall.TakeDamage(70);
            var deck = Deck(Card("repair", true, condition: "wallHpBelow"));
            var choice = deck.Draw(3).Single(c => c.IsGeneral);

            Assert.IsTrue(deck.TryApply(choice));

            Assert.AreEqual(50, wall.CurrentHp);
            Assert.AreEqual(1, deck.GetPickCount("repair"));
        }

        [Test(Description = "뽑은 뒤 조건이 깨졌으면(이미 회복됨) 적용하지 않고 false를 돌려준다")]
        public void TryApply_ConditionNoLongerMet_ReturnsFalse()
        {
            wall.TakeDamage(70);
            var deck = Deck(Card("repair", true, condition: "wallHpBelow"));
            var choice = deck.Draw(3).Single(c => c.IsGeneral);
            wall.Repair(100);

            Assert.IsFalse(deck.TryApply(choice));
            Assert.AreEqual(0, deck.GetPickCount("repair"));
        }

        [Test(Description = "스킬 카드는 스킬 쪽으로 넘긴다")]
        public void TryApply_SkillChoice_DelegatesToSkills()
        {
            var deck = Deck();
            var choice = new UpgradeChoice { DisplayName = "skill" };

            Assert.IsTrue(deck.TryApply(choice));
            Assert.AreEqual(1, skills.Applied.Count);
        }

        [Test(Description = "방벽이 없으면(샌드박스 등) 방벽 조건은 맞지 않는다")]
        public void Draw_NoWall_ConditionNeverMet()
        {
            var deck = new CardDeck(skills, null, new[] { Card("repair", true, condition: "wallHpBelow") }, new FixedRandom());

            Assert.IsFalse(deck.Draw(3).Any(c => c.IsGeneral));
        }

        [Test]
        public void Draw_NonPositiveCount_IsEmpty()
        {
            Assert.IsEmpty(Deck().Draw(0));
        }
    }
}
