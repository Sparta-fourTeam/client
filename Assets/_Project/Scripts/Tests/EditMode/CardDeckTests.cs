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
            public List<UpgradeChoice> Candidates;
            public List<UpgradeChoice> Applied { get; } = new List<UpgradeChoice>();

            public List<UpgradeChoice> GetUpgradeCandidates() =>
                Candidates?.ToList() ?? Enumerable.Range(0, Count).Select(i => new UpgradeChoice { DisplayName = "skill" + i }).ToList();

            public bool ApplyUpgradeChoice(UpgradeChoice choice)
            {
                Applied.Add(choice);
                return true;
            }
        }

        private sealed class TestSkill : SkillBase
        {
            public TestSkill(int id) : base(new SkillData { id = id, maxLevel = 10, baseStats = new SkillBaseStats() }, null, null) { }
            protected override bool OnFire() => false;
        }

        [Test]
        public void Roulette_UsesOwnedUpgradesOnly_AndAppliesRoundOnce()
        {
            using var weapon = new TestSkill(701);
            var upgrade = new UpgradeChoice { skill = weapon, Option = new SkillUpgradeOption { id = "boost" } };
            skills.Candidates = new List<UpgradeChoice> { upgrade,
                new UpgradeChoice { IsNewWeapon = true, newSkillData = new SkillData { id = 702 } },
                new UpgradeChoice { GeneralCard = Card("repair", false) } };
            var session = new EliteRouletteSession(Deck(), new FixedRandom(), 2, 4);
            Assert.IsTrue(session.Start());
            Assert.AreEqual(1, session.CardCount, "강화 카드가 하나면 중복 보상으로 채우지 않는다");
            var round = session.PrepareRound();
            Assert.AreEqual(8, round.Slots.Count);
            Assert.AreEqual(1, round.Slots.Count(c => c.skill == weapon && c.Option == upgrade.Option));
            Assert.IsTrue(round.Slots.Where(c => c.skill != null).All(c => c.skill == weapon));
            Assert.AreSame(round, session.PrepareRound());
            Assert.IsTrue(session.TryApply(round));
            Assert.IsFalse(session.TryApply(round));
            Assert.AreEqual(1, skills.Applied.Count);
            Assert.AreEqual(1, session.CompletedRolls);
        }

        [Test]
        public void Roulette_KeepsSlotsAndAllowsSameWeapon_WithDifferentCardsOnly()
        {
            using var weapon = new TestSkill(701);
            skills.Candidates = Enumerable.Range(0, 4).Select(i => new UpgradeChoice {
                skill = weapon, Option = new SkillUpgradeOption { id = "upgrade_" + i } }).ToList();
            skills.Candidates.Add(skills.Candidates[0]);
            var session = new EliteRouletteSession(Deck(), new FixedRandom(), 5, 5);
            Assert.IsTrue(session.Start());
            Assert.AreEqual(4, session.CardCount, "한 무기라도 서로 다른 강화 카드 네 개를 받을 수 있다");
            var slots = session.Slots;
            Assert.AreEqual(4, slots.Count(c => c.skill != null));
            for (int i = 0; i < 4; i++)
            {
                var round = session.PrepareRound();
                Assert.AreSame(slots, round.Slots);
                Assert.AreSame(weapon, round.Winner.skill);
                Assert.AreEqual(slots[round.WinnerIndex].Option.id, round.Winner.Option.id);
                Assert.IsTrue(session.TryApply(round));
                Assert.AreEqual(i + 1, Enumerable.Range(0, slots.Count).Count(session.IsSlotSelected));
                if (i == 0)
                {
                    Assert.IsTrue(Enumerable.Range(0, slots.Count).Any(index => slots[index].skill == weapon
                        && !session.IsSlotSelected(index)), "당첨된 화살 카드만 제외하고 다른 화살 카드는 남긴다");
                }
            }
            Assert.IsNull(session.PrepareRound());
            Assert.IsTrue(session.IsComplete);
            Assert.AreEqual(4, skills.Applied.Count);
            Assert.AreEqual(4, skills.Applied.Select(c => c.Option.id).Distinct().Count());
            Assert.IsTrue(session.Start());
            Assert.IsFalse(Enumerable.Range(0, session.Slots.Count).Any(session.IsSlotSelected));
        }

        [TestCase(false, 2)]
        [TestCase(true, 1)]
        public void Roulette_DistinguishesWeaponCards_AndDeduplicatesSharedCards(bool shared, int expectedCards)
        {
            using var first = new TestSkill(701);
            using var second = new TestSkill(702);
            skills.Candidates = new List<UpgradeChoice> {
                new UpgradeChoice { skill = first, Option = new SkillUpgradeOption { id = "boost", sharedId = shared ? "shared_boost" : null } },
                new UpgradeChoice { skill = second, Option = new SkillUpgradeOption { id = "boost", sharedId = shared ? "shared_boost" : null } } };
            var session = new EliteRouletteSession(Deck(), new FixedRandom(), 2, 2);
            Assert.IsTrue(session.Start());
            Assert.AreEqual(expectedCards, session.CardCount);
            for (int i = 0; i < expectedCards; i++) { Assert.IsTrue(session.TryApply(session.PrepareRound())); }
            Assert.IsNull(session.PrepareRound());
            Assert.AreEqual(expectedCards, skills.Applied.Count);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Roulette_RefreshesRemainingCandidates_WithoutChangingTheBoard(bool remainingUnavailable)
        {
            using var first = new TestSkill(701);
            using var second = new TestSkill(702);
            using var added = new TestSkill(703);
            skills.Candidates = new List<UpgradeChoice> {
                new UpgradeChoice { skill = first, Option = new SkillUpgradeOption { id = "first" } },
                new UpgradeChoice { skill = second, Option = new SkillUpgradeOption { id = "second" } } };
            var session = new EliteRouletteSession(Deck(), new FixedRandom(), 2, 2);
            Assert.IsTrue(session.Start());
            var round = session.PrepareRound();
            Assert.IsTrue(session.TryApply(round));
            var remaining = round.Winner.skill == first ? second : first;
            string remainingCardId = skills.Candidates.Single(c => c.skill == remaining).Option.id;
            var updated = new SkillUpgradeOption { id = remainingCardId, desc = "updated" };
            skills.Candidates = new List<UpgradeChoice> {
                new UpgradeChoice { skill = added, Option = new SkillUpgradeOption { id = "added" } } };
            if (!remainingUnavailable)
            {
                skills.Candidates.Add(new UpgradeChoice { skill = remaining, Option = updated });
            }
            var next = session.PrepareRound();
            if (remainingUnavailable)
            {
                Assert.IsNull(next);
                Assert.IsTrue(session.IsComplete);
                Assert.AreEqual(1, session.CompletedRolls, "후보가 없어도 실행하지 않은 회차를 완료 수에 더하지 않는다");
                Assert.AreEqual(1, skills.Applied.Count);
            }
            else
            {
                Assert.AreSame(round.Slots, next.Slots);
                Assert.AreSame(remaining, next.Winner.skill);
                Assert.AreSame(updated, next.Winner.Option);
                Assert.IsTrue(session.TryApply(next));
                Assert.IsTrue(session.IsComplete);
            }
            Assert.IsFalse(session.Slots.Any(c => c.skill == added), "새로 추가된 카드로 고정한 배치를 바꾸지 않는다");
        }

        [Test]
        public void Roulette_EmptyOrCancelledSessionCannotGrantReward()
        {
            skills.Candidates = new List<UpgradeChoice>();
            var session = new EliteRouletteSession(Deck(), new FixedRandom(), 3, 3);
            Assert.IsFalse(session.Start());
            Assert.IsNull(session.PrepareRound());
            using var weapon = new TestSkill(701);
            skills.Candidates.Add(new UpgradeChoice { skill = weapon, Option = new SkillUpgradeOption { id = "boost" } });
            Assert.IsTrue(session.Start());
            var pending = session.PrepareRound();
            session.Cancel();
            Assert.IsFalse(session.TryApply(pending));
            Assert.IsTrue(session.IsComplete);
            Assert.IsEmpty(skills.Applied);
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
