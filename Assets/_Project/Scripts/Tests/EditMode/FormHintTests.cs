using System.Collections.Generic;
using System.Linq;
using Game.Core;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class FormHintTests
    {
        // Skills.json: 화염구 2(불사조 불꽃), 낙뢰 3(뇌신의 심판), 통나무 5(거대 통나무·불타는 뿌리)
        private const int Fireball = 2;
        private const int Lightning = 3;
        private const int Log = 5;

        private sealed class State : IUpgradeState
        {
            public readonly Dictionary<int, int> Levels = new();
            public readonly Dictionary<int, int> PermanentLevels = new();
            public readonly Dictionary<(int, string), int> Counts = new();
            public int GetWeaponLevel(int id) => Levels.TryGetValue(id, out var level) ? level : 0;
            public int GetPermanentWeaponLevel(int id) => PermanentLevels.TryGetValue(id, out var level) ? level : 0;
            public int GetAcquiredCount(int id, string card) => Counts.TryGetValue((id, card), out var count) ? count : 0;
        }

        private static List<SkillData> Catalog() => new DefaultSkillDataProvider(new GameDataStore()).LoadAll();

        private static SkillUpgradeOption Card(int skillId, string cardId) =>
            Catalog().Find(s => s.id == skillId).upgrades.Find(c => c.id == cardId);

        private static string[] Describe(IReadOnlyList<FormHint> hints) =>
            hints.Select(h => $"{h.Card.id}:{h.Kind}").OrderBy(s => s).ToArray();

        [Test(Description = "스킬을 새로 얻는 카드에는 그 스킬의 형태 변환이 조건으로 붙는다")]
        public void NewSkillCard_ShowsItsOwnForms()
        {
            var state = new State();
            state.PermanentLevels[Log] = 9;

            var hints = FormHintFinder.For(Log, null, Catalog(), state);

            CollectionAssert.AreEqual(new[] { "log_fire:Enables", "log_large:Enables" }, Describe(hints));
            Assert.AreEqual(SkillForm.LargeLog, hints.Single(h => h.Card.id == "log_large").Form);
            Assert.AreEqual(SkillForm.FireLog, hints.Single(h => h.Card.id == "log_fire").Form);
        }

        [Test(Description = "다른 스킬이 필수인 변환은 그 다른 스킬의 새 스킬 카드에도 붙는다 (불타는 뿌리는 화염구가 필요하다)")]
        public void NewSkillCard_ShowsFormsThatRequireIt()
        {
            var state = new State();
            state.PermanentLevels[Log] = 9;

            var hints = FormHintFinder.For(Fireball, null, Catalog(), state);

            CollectionAssert.AreEqual(new[] { "log_fire:Enables" }, Describe(hints), "불사조 불꽃은 영구 레벨 13 미만이라 빠진다");
        }

        [Test(Description = "변환의 필수 카드는 조건으로 붙고, 이미 가진 필수 카드는 붙지 않는다")]
        public void RequiredCard_ShowsUntilAcquired()
        {
            var state = new State();
            state.Levels[Log] = 1;
            state.PermanentLevels[Log] = 9;

            CollectionAssert.AreEqual(new[] { "log_fire:Enables", "log_large:Enables" },
                Describe(FormHintFinder.For(Log, "log_size", Catalog(), state)));

            state.Counts[(Log, "log_size")] = 1;
            CollectionAssert.IsEmpty(FormHintFinder.For(Log, "log_size", Catalog(), state));
        }

        [Test(Description = "고르면 변환을 더는 얻을 수 없게 되는 카드에는 그 변환이 막힘으로 붙고, 변환 카드 자신은 붙지 않는다")]
        public void ExclusiveCard_ShowsBlocked()
        {
            var state = new State();
            state.Levels[Log] = 1;
            state.PermanentLevels[Log] = 9;

            CollectionAssert.AreEqual(new[] { "log_fire:Blocks" }, Describe(FormHintFinder.For(Log, "log_large", Catalog(), state)));
            CollectionAssert.AreEqual(new[] { "log_large:Blocks" }, Describe(FormHintFinder.For(Log, "log_fire", Catalog(), state)));
        }

        [Test(Description = "영구 레벨 조건이 있는 배타는 그 레벨 미만일 때만 막힘으로 붙는다 (화염구 연발은 영구 25 미만에서 불사조 불꽃을 막는다)")]
        public void ExclusionBelowPermanentLevel_BlocksOnlyBelowLevel()
        {
            var state = new State();
            state.Levels[Fireball] = 1;
            state.PermanentLevels[Fireball] = 13;
            CollectionAssert.AreEqual(new[] { "fireball_phoenix:Blocks" },
                Describe(FormHintFinder.For(Fireball, "fireball_burst", Catalog(), state)));

            state.PermanentLevels[Fireball] = 25;
            CollectionAssert.IsEmpty(FormHintFinder.For(Fireball, "fireball_burst", Catalog(), state));
        }

        [Test(Description = "이미 얻은 변환과 이미 막힌 변환은 보이지 않는다")]
        public void AcquiredOrAlreadyBlockedForms_AreHidden()
        {
            var state = new State();
            state.Levels[Log] = 1;
            state.PermanentLevels[Log] = 9;
            state.Counts[(Log, "log_large")] = 1;   // 거대 통나무를 얻었고, 그래서 불타는 뿌리는 막혔다

            CollectionAssert.IsEmpty(FormHintFinder.For(Log, "log_size", Catalog(), state));
        }

        [Test(Description = "영구 레벨이 모자라 이번 판에서 얻을 수 없는 변환은 보이지 않는다")]
        public void FormsBelowPermanentLevel_AreHidden()
        {
            var state = new State();
            state.Levels[Log] = 1;

            CollectionAssert.AreEqual(new[] { "log_large:Enables" }, Describe(FormHintFinder.For(Log, "log_size", Catalog(), state)));
        }

        [Test(Description = "형태 변환과 관계없는 카드에는 아무것도 붙지 않는다")]
        public void UnrelatedCard_ShowsNothing()
        {
            var state = new State();
            state.Levels[Lightning] = 1;
            state.PermanentLevels[Lightning] = 13;

            CollectionAssert.IsEmpty(FormHintFinder.For(Lightning, "lightning_damage", Catalog(), state));
        }

        [Test(Description = "표시는 실제 획득 판정과 맞는다: 조건 카드를 다 채우면 변환 카드를 얻을 수 있고, 막힘 카드를 고르면 얻을 수 없다")]
        public void Hints_MatchActualEligibility()
        {
            var state = new State();
            state.Levels[Fireball] = 1;
            state.PermanentLevels[Fireball] = 13;
            state.Counts[(Fireball, "fireball_impact_damage")] = 1;
            var phoenix = Card(Fireball, "fireball_phoenix");
            Assert.IsFalse(UpgradeEligibility.CanAcquire(phoenix, Fireball, state));

            var enabling = FormHintFinder.For(Fireball, "fireball_explosion_radius", Catalog(), state);
            Assert.AreEqual(FormHintKind.Enables, enabling.Single(h => h.Card.id == "fireball_phoenix").Kind);
            state.Counts[(Fireball, "fireball_explosion_radius")] = 1;
            Assert.IsTrue(UpgradeEligibility.CanAcquire(phoenix, Fireball, state), "조건 카드를 고르면 실제로 얻을 수 있다");

            var blocking = FormHintFinder.For(Fireball, "fireball_burst", Catalog(), state);
            Assert.AreEqual(FormHintKind.Blocks, blocking.Single(h => h.Card.id == "fireball_phoenix").Kind);
            state.Counts[(Fireball, "fireball_burst")] = 1;
            Assert.IsFalse(UpgradeEligibility.CanAcquire(phoenix, Fireball, state), "막힘 카드를 고르면 실제로 얻을 수 없다");
        }

        [Test(Description = "카드 후보를 만들 때 각 카드에 형태 변환 표시가 채워진다")]
        public void Candidates_FillFormHints()
        {
            var state = new State();
            state.PermanentLevels[Log] = 9;
            var choices = new SkillUpgradeChoices(new List<SkillBase>(), Catalog(), state, _ => true).Candidates();

            var log = choices.Single(c => c.IsNewWeapon && c.newSkillData.id == Log);
            CollectionAssert.AreEqual(new[] { "log_fire:Enables", "log_large:Enables" }, Describe(log.FormHints));
        }

        [Test(Description = "일반 카드에는 형태 변환 표시가 없다")]
        public void GeneralCard_ShowsNothing()
        {
            var general = new UpgradeChoice { GeneralCard = new GeneralCardDefinition() };

            CollectionAssert.IsEmpty(FormHintFinder.For(general, Catalog(), new State()));
        }

        [Test(Description = "형태 아이콘이 있으면 그 아이콘을, 없으면 변환 카드를 가진 스킬의 HUD 아이콘을 쓴다")]
        public void FormIcon_FallsBackToSkillHudIcon()
        {
            var texture = new Texture2D(2, 2);
            var hud = Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.zero);
            var large = Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.zero);
            var table = SkillAssetTable.Create(new[] { new SkillAssetEntry { key = "weapon_log", hudIcon = hud } },
                formIcons: new[] { new SkillFormIconEntry { form = SkillForm.LargeLog, icon = large } });

            Assert.AreSame(large, table.GetFormIcon(SkillForm.LargeLog, "weapon_log"));
            Assert.AreSame(hud, table.GetFormIcon(SkillForm.FireLog, "weapon_log"));

            Object.DestroyImmediate(table);
            Object.DestroyImmediate(large);
            Object.DestroyImmediate(hud);
            Object.DestroyImmediate(texture);
        }
    }
}
