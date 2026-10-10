using System.Collections.Generic;
using System.Linq;
using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>카드 아래 형태 변환 힌트: 조건이면 아이콘, 고르면 막히면 X. 막힘은 카드에 직접 적힌 배타뿐 아니라 선행 체인으로도 따라간다</summary>
    public sealed class FormHintFinderTests
    {
        private sealed class State : IUpgradeState
        {
            public readonly Dictionary<(int, string), int> Counts = new();
            public int GetWeaponLevel(int id) => 1;
            public int GetPermanentWeaponLevel(int id) => 13;
            public int GetAcquiredCount(int id, string card) => Counts.TryGetValue((id, card), out var count) ? count : 0;
        }

        private static List<SkillData> Catalog() => new DefaultSkillDataProvider(new GameDataStore()).LoadAll();

        private static FormHintKind? KindOf(IReadOnlyList<FormHint> hints, string cardId)
        {
            var hint = hints.Where(h => h.Card.id == cardId).Cast<FormHint?>().FirstOrDefault();
            return hint?.Kind;
        }

        [Test(Description = "폭발 화살을 고르면 화염 화살은 조건 아이콘, 뇌전 화살은 (충격 화살이 막혀서) X")]
        public void ChoosingExplosionArrow_BlocksThunderArrowThroughItsPrerequisite()
        {
            var hints = FormHintFinder.For(1, "arrow_explosion", Catalog(), new State());

            Assert.AreEqual(FormHintKind.Enables, KindOf(hints, "arrow_flame"));
            Assert.AreEqual(FormHintKind.Blocks, KindOf(hints, "arrow_thunder"));
        }

        [Test(Description = "충격 화살을 고르면 반대로 뇌전 화살은 조건 아이콘, 화염 화살은 X")]
        public void ChoosingShockArrow_BlocksFlameArrowThroughItsPrerequisite()
        {
            var hints = FormHintFinder.For(1, "arrow_shock", Catalog(), new State());

            Assert.AreEqual(FormHintKind.Enables, KindOf(hints, "arrow_thunder"));
            Assert.AreEqual(FormHintKind.Blocks, KindOf(hints, "arrow_flame"));
        }

        [Test(Description = "이미 폭발 화살이 있으면 뇌전 화살은 더 얻을 수 없으므로 그 선행 카드에도 힌트를 보이지 않는다")]
        public void AlreadyUnreachableForm_IsNotShown()
        {
            var state = new State();
            state.Counts[(1, "arrow_explosion")] = 1;

            var hints = FormHintFinder.For(1, "arrow_light", Catalog(), state);

            Assert.IsNull(KindOf(hints, "arrow_thunder"));
        }

        [Test(Description = "카드에 직접 적힌 배타는 그대로 X (나무뿌리 대형 ↔ 불타는 뿌리)")]
        public void DirectExclusion_StillBlocks()
        {
            var hints = FormHintFinder.For(5, "log_large", Catalog(), new State());

            Assert.AreEqual(FormHintKind.Blocks, KindOf(hints, "log_fire"));
        }
    }
}
