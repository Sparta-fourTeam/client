using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Core;
using Game.Sandbox;
using Game.View;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>스킬은 슬롯 수(SkillSlotLimit.Max)까지만 가질 수 있다: 획득, 새 스킬 카드 후보, HUD 슬롯 수가 같은 값을 쓴다</summary>
    public sealed class SkillSlotLimitTests
    {
        private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

        private sealed class Targets : IEnemyTargetProvider
        {
            public int GetNearest(Vector2 from, int count, List<IEnemyTarget> results) { results.Clear(); return 0; }
        }

        private sealed class Publisher : MessagePipe.IBufferedPublisher<Game.Core.Messages.SkillChanged>
        {
            public void Publish(Game.Core.Messages.SkillChanged message) { }
        }

        private sealed class State : IUpgradeState
        {
            private readonly IReadOnlyList<SkillBase> owned;
            public State(IReadOnlyList<SkillBase> owned) => this.owned = owned;
            public int GetWeaponLevel(int id) => owned.Any(s => s.Data.id == id) ? 1 : 0;
            public int GetPermanentWeaponLevel(int id) => 0;
            public int GetAcquiredCount(int id, string card) => 0;
        }

        private GameObject root;

        [TearDown]
        public void TearDown()
        {
            if (root != null) { Object.DestroyImmediate(root); }
        }

        private SkillController Controller()
        {
            root = new GameObject("SkillSlotLimitTest");
            var controller = root.AddComponent<SkillController>();
            TestSkillAssets.AttachReal(controller);
            var progression = new SandboxProgression();
            controller.Construct(new Targets(), new Publisher(), new DefaultSkillDataProvider(new GameDataStore()), progression,
                startingSkills: new NoStartingSkills(), permanentEffects: new SandboxPermanentSkillEffects(new GameDataStore(), progression));
            typeof(SkillController).GetMethod("Start", Private).Invoke(controller, null);
            return controller;
        }

        [Test(Description = "스킬 HUD 프리팹의 슬롯 수는 스킬 슬롯 수의 출처(SkillSlotLimit.Max)와 같다")]
        public void HudPrefab_HasExactlyAsManySlotsAsTheLimit()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/SkillHud.prefab");
            var slots = (SkillSlotView[])typeof(SkillHudView).GetField("_slots", Private).GetValue(prefab.GetComponent<SkillHudView>());

            Assert.AreEqual(SkillSlotLimit.Max, slots.Length);
        }

        [Test(Description = "슬롯 수만큼 스킬을 얻은 뒤에는 더 얻지 못한다")]
        public void AddWeapon_RefusesTheSkillBeyondTheLimit()
        {
            var controller = Controller();
            var ids = new DefaultSkillDataProvider(new GameDataStore()).LoadAll().Where(w => !w.childOnly && controller.HasPrefab(w.id)).Select(w => w.id).Take(SkillSlotLimit.Max + 1).ToList();
            Assert.AreEqual(SkillSlotLimit.Max + 1, ids.Count, "테스트에 쓸 스킬이 모자란다");

            for (int i = 0; i < SkillSlotLimit.Max; i++) { Assert.IsTrue(controller.AddWeapon(ids[i]), $"{i + 1}번째 스킬"); }
            UnityEngine.TestTools.LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("최대"));

            Assert.IsFalse(controller.AddWeapon(ids[SkillSlotLimit.Max]));
            Assert.AreEqual(SkillSlotLimit.Max, controller.Skills.Count);
        }

        [Test(Description = "슬롯이 남아 있으면 새 스킬 카드가 나오고, 다 찼으면 새 스킬 카드는 나오지 않고 강화 카드만 나온다")]
        public void NewSkillCards_AreOfferedOnlyWhileASlotIsFree()
        {
            var controller = Controller();
            var catalog = new DefaultSkillDataProvider(new GameDataStore()).LoadAll();
            var ids = catalog.Where(w => !w.childOnly && controller.HasPrefab(w.id)).Select(w => w.id).ToList();
            for (int i = 0; i < SkillSlotLimit.Max - 1; i++) { controller.AddWeapon(ids[i]); }

            var withFreeSlot = new SkillUpgradeChoices(controller.Skills, catalog, new State(controller.Skills), controller.HasPrefab).Candidates();
            controller.AddWeapon(ids[SkillSlotLimit.Max - 1]);
            var full = new SkillUpgradeChoices(controller.Skills, catalog, new State(controller.Skills), controller.HasPrefab).Candidates();

            Assert.IsTrue(withFreeSlot.Any(c => c.IsNewWeapon), "슬롯이 남았으면 새 스킬 카드가 나온다");
            Assert.IsFalse(full.Any(c => c.IsNewWeapon), "슬롯이 다 찼으면 새 스킬 카드가 나오지 않는다");
            Assert.IsTrue(full.Any(c => !c.IsNewWeapon), "강화 카드는 계속 나온다");
        }

        [Test(Description = "샌드박스의 스킬 최대 수도 같은 값이다")]
        public void Sandbox_UsesTheSameLimit()
        {
            Assert.AreEqual(SkillSlotLimit.Max, SkillSandboxSession.MaxSkills);
        }
    }
}
