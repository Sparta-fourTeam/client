using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Core;
using Game.Sandbox;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>스킬 샌드박스의 조작 로직: 구성 적용, 카드 직접 적용, 영구 레벨, 실제 3지선다, 저장</summary>
    public sealed class SkillSandboxSessionTests
    {
        private const string PlayerPrefab = "Assets/_Project/Prefabs/Stage/Player_Animated.prefab";
        private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

        private sealed class Targets : IEnemyTargetProvider
        {
            public int GetNearest(Vector2 from, int count, List<IEnemyTarget> results) { results.Clear(); return 0; }
        }

        private sealed class Publisher : MessagePipe.IBufferedPublisher<Game.Core.Messages.SkillChanged>
        {
            public void Publish(Game.Core.Messages.SkillChanged message) { }
        }

        private GameObject root;
        private WeaponController controller;
        private SandboxProgression progression;
        private SkillSandboxSession session;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("SandboxSessionTest");
            controller = root.AddComponent<WeaponController>();
            // 플레이어 프리팹의 실제 prefabEntries를 그대로 쓴다.
            var entries = new List<WeaponPrefabEntry>();
            var source = new SerializedObject(AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefab).GetComponentInChildren<WeaponController>(true)).FindProperty("prefabEntries");
            for (int i = 0; i < source.arraySize; i++)
            {
                var entry = source.GetArrayElementAtIndex(i);
                entries.Add(new WeaponPrefabEntry { id = entry.FindPropertyRelative("id").intValue, prefab = (GameObject)entry.FindPropertyRelative("prefab").objectReferenceValue });
            }
            typeof(WeaponController).GetField("prefabEntries", Private).SetValue(controller, entries);
            progression = new SandboxProgression();
            controller.Construct(new Targets(), new Publisher(), new DefaultWeaponDataProvider(new GameDataStore()), progression, startingSkills: new NoStartingSkills());
            typeof(WeaponController).GetMethod("Start", Private).Invoke(controller, null);
            session = new SkillSandboxSession(controller, new DefaultWeaponDataProvider(new GameDataStore()), progression);
        }

        [TearDown]
        public void TearDown()
        {
            controller.ClearWeapons();
            Object.DestroyImmediate(root);
        }

        // ── 목록 ──────────────────────────────────────────────────────

        [Test]
        public void Catalog_ListsEverySkillExceptChildOnlyOnes()
        {
            var all = new DefaultWeaponDataProvider(new GameDataStore()).LoadAll();
            CollectionAssert.AreEquivalent(all.Where(w => !w.childOnly).Select(w => w.id), session.Skills.Select(s => s.Id));
            Assert.IsTrue(session.Skills.All(s => s.HasPrefab), "카탈로그의 모든 스킬은 프리팹이 연결돼 있다");
            foreach (var id in new[] { 6, 7, 16, 18 }) { Assert.IsTrue(session.Skills.Any(s => s.Id == id), $"스킬 {id}가 목록에 있다"); }
        }

        [Test]
        public void SelectSkill_RejectsUnknownAndChildOnlySkills()
        {
            Assert.IsFalse(session.SelectSkill(9999));
            Assert.IsFalse(session.SelectSkill(8), "자식 전용 스킬은 고를 수 없다");
            Assert.IsNull(session.Current);
        }

        // ── 구성 적용 ─────────────────────────────────────────────────

        [Test]
        public void SelectSkill_ReplacesPreviousSkillAndStartsFromBaseStats()
        {
            Assert.IsTrue(session.SelectSkill(1));
            Assert.AreEqual(1, controller.Weapons.Count);
            Assert.IsTrue(session.TryAddCard("arrow_sharp", out _));
            Assert.IsTrue(session.SelectSkill(2));
            Assert.AreEqual(1, controller.Weapons.Count, "이전 스킬은 정리된다");
            Assert.AreEqual(2, session.Current.Data.id);
            Assert.AreEqual(0, session.Build.cards.Count, "스킬을 바꾸면 카드 목록도 새로 시작한다");
        }

        [Test]
        public void AddedCards_ProduceSameStatsAsApplyingThemDirectly()
        {
            session.SelectSkill(1);
            foreach (var id in new[] { "arrow_sharp", "kunai_spread", "kunai_barrage" }) { Assert.IsTrue(session.TryAddCard(id, out var reason), reason); }

            var data = new DefaultWeaponDataProvider(new GameDataStore()).LoadAll().Find(w => w.id == 1);
            var expected = SkillConfig.FromDefinition(data).WithChildCaster(new NoopChildCaster());
            foreach (var id in new[] { "arrow_sharp", "kunai_spread", "kunai_barrage" })
            {
                var builder = new SkillConfigBuilder(expected);
                Assert.IsTrue(builder.TryApplyCatalog(data.upgrades.Find(c => c.id == id).effects));
                expected = builder.Build();
            }
            Assert.AreEqual("(변화 없음)", WeaponStatsDump.Diff(WeaponStatsDump.Flatten(expected.Stats), WeaponStatsDump.Flatten(session.Current.Stats)));
        }

        [Test]
        public void RemoveCard_RebuildsWithoutItAndRestoresStats()
        {
            session.SelectSkill(1);
            float baseDamage = session.Current.Stats.Cast.Damage;
            session.TryAddCard("arrow_sharp", out _);
            Assert.Greater(session.Current.Stats.Cast.Damage, baseDamage);
            Assert.IsTrue(session.RemoveCard("arrow_sharp"));
            Assert.AreEqual(baseDamage, session.Current.Stats.Cast.Damage, .0001f);
            Assert.IsFalse(session.RemoveCard("arrow_sharp"), "없는 카드는 뺄 수 없다");
        }

        [Test]
        public void RemovingAMiddleCard_ReappliesTheRestInTheSameOrder()
        {
            session.SelectSkill(1);
            foreach (var id in new[] { "arrow_sharp", "kunai_spread", "kunai_aux_damage" }) { session.TryAddCard(id, out _); }
            session.RemoveCard("kunai_spread");
            CollectionAssert.AreEqual(new[] { "arrow_sharp", "kunai_aux_damage" }, session.Build.cards, "빌드에서 빠진 카드 외에는 순서가 같다");
            Assert.IsNull(session.LastError == null ? null : session.LastError, "남은 카드는 모두 다시 적용된다");
        }

        [Test]
        public void ClearCards_ReturnsToBaseStats()
        {
            session.SelectSkill(1);
            float baseDamage = session.Current.Stats.Cast.Damage;
            session.TryAddCard("arrow_sharp", out _);
            session.ClearCards();
            Assert.AreEqual(baseDamage, session.Current.Stats.Cast.Damage, .0001f);
            Assert.AreEqual(0, session.Build.cards.Count);
        }

        // ── 카드 목록과 막힌 카드 ─────────────────────────────────────

        [Test]
        public void CardList_IgnoresPrerequisitesButBlocksDisabledMaxedAndUnknownCards()
        {
            session.SelectSkill(6);
            var cards = session.Cards();
            var shatter = cards.Find(c => c.Id == "frost_prison_shatter");
            Assert.IsNull(shatter.Blocked, "선행 조건(서리 결정 보유)은 무시하고 적용할 수 있다");
            var core = cards.Find(c => c.Id == "frost_prison_core");
            Assert.IsNotNull(core.Blocked);
            StringAssert.Contains("중심부", core.Blocked, "비활성 사유가 그대로 보인다");
            Assert.IsFalse(session.TryAddCard("frost_prison_core", out var reason));
            Assert.AreEqual(core.Blocked, reason);
            Assert.IsFalse(session.TryAddCard("no_such_card", out _));

            Assert.IsTrue(session.TryAddCard("frost_prison_extreme", out _));
            Assert.IsFalse(session.TryAddCard("frost_prison_extreme", out reason), "최대 횟수를 넘지 못한다");
            StringAssert.Contains("최대", reason);
        }

        [Test]
        public void CardsThatCastChildSkills_ApplyBecauseTheSandboxWiresTheChildCaster()
        {
            session.SelectSkill(6);
            Assert.IsTrue(session.TryAddCard("frost_prison_shatter", out var reason), reason);
            Assert.IsTrue(session.Current.Config.Children.ContainsKey(4), "서리 결정을 자식으로 연결한다");
        }

        // ── 영구 레벨 ─────────────────────────────────────────────────

        [Test]
        public void PermanentLevel_FlowsToControllerAndSwitchesLevelVariants()
        {
            session.SelectSkill(1);
            session.SetPermanentLevel(0);
            var low = controller.Weapons[0].Data.upgrades.Find(c => c.id == "arrow_multishot");
            WeaponUpgradeResolver.TryResolve(low, session.Build.permanentLevel, out var before);
            session.SetPermanentLevel(9);
            Assert.AreEqual(9, controller.GetPermanentWeaponLevel(1), "슬라이더 값이 컨트롤러의 영구 레벨 표에 반영된다");
            WeaponUpgradeResolver.TryResolve(low, session.Build.permanentLevel, out var after);
            Assert.AreNotEqual(before.Name, after.Name, "영구 레벨 9에서 (+) 변형으로 바뀐다");
            Assert.AreEqual(0, new SkillSandboxSession(controller, new DefaultWeaponDataProvider(new GameDataStore()), progression).Build.permanentLevel);
        }

        [Test]
        public void PermanentLevel_IsClampedAtZero()
        {
            session.SetPermanentLevel(-5);
            Assert.AreEqual(0, session.Build.permanentLevel);
        }

        [Test]
        public void ChangingPermanentLevel_KeepsTheCardsApplied()
        {
            session.SelectSkill(1);
            session.TryAddCard("arrow_sharp", out _);
            session.SetPermanentLevel(5);
            CollectionAssert.AreEqual(new[] { "arrow_sharp" }, session.Build.cards);
            Assert.AreEqual(1, session.Current.GetAcquiredCount("arrow_sharp"));
        }

        // ── 실제 3지선다 ──────────────────────────────────────────────

        [Test]
        public void RealChoices_OnlyOfferCurrentSkillCardsThatMeetTheRealRules()
        {
            session.SelectSkill(1);
            var choices = session.RollChoices(50);
            Assert.IsNotEmpty(choices);
            foreach (var choice in choices)
            {
                Assert.IsFalse(choice.IsNewWeapon);
                Assert.AreEqual(1, choice.Weapon.Data.id);
            }
            // 선행 조건이 있는 카드는 처음에는 나오지 않는다 (kunai_barrage는 kunai_spread가 먼저 필요하다).
            Assert.IsFalse(choices.Any(c => c.Option.id == "kunai_barrage"));
            Assert.IsFalse(choices.Any(c => c.Option.id == "kunai_spread"), "kunai_spread는 arrow_sharp가 먼저 필요하다");
        }

        [Test]
        public void PickingARealChoice_AppliesItAndRecordsItInTheBuild()
        {
            session.SelectSkill(1);
            var choice = session.RollChoices(50).Find(c => c.Option.id == "arrow_sharp");
            Assert.IsNotNull(choice.Option);
            Assert.IsTrue(session.Pick(choice));
            CollectionAssert.AreEqual(new[] { "arrow_sharp" }, session.Build.cards);
            Assert.IsTrue(session.RollChoices(50).Any(c => c.Option.id == "kunai_spread"), "선행 카드를 얻으면 다음 카드가 열린다");
            session.SetPermanentLevel(1);
            CollectionAssert.AreEqual(new[] { "arrow_sharp" }, session.Build.cards, "기록된 빌드는 다시 적용해도 같다");
        }

        [Test]
        public void RealChoices_AreEmptyWithoutASkill()
        {
            Assert.IsEmpty(session.RollChoices());
        }

        // ── 저장 ──────────────────────────────────────────────────────

        [Test]
        public void Build_RoundTripsThroughJson()
        {
            var build = new SandboxBuild { skillId = 4, permanentLevel = 13, cards = { "ice_split", "ice_shard_damage" } };
            Assert.IsTrue(SandboxBuild.TryParse(build.ToJson(), out var parsed));
            Assert.AreEqual(4, parsed.skillId);
            Assert.AreEqual(13, parsed.permanentLevel);
            CollectionAssert.AreEqual(build.cards, parsed.cards);
            Assert.IsFalse(SandboxBuild.TryParse("not json", out _));
            Assert.IsFalse(SandboxBuild.TryParse("", out _));
        }

        [Test]
        public void Preset_SavesAndRestoresTheWholeBuild()
        {
            const string Slot = "unit-test-slot";
            try
            {
                session.SelectSkill(4);
                session.SetPermanentLevel(3);
                session.TryAddCard("ice_split", out _);
                session.TryAddCard("ice_shard_damage", out _);
                session.SavePreset(Slot);

                session.SelectSkill(1);
                Assert.IsTrue(SandboxPresets.Exists(Slot));
                Assert.IsTrue(session.LoadPreset(Slot));
                Assert.AreEqual(4, session.Current.Data.id);
                Assert.AreEqual(3, session.Build.permanentLevel);
                CollectionAssert.AreEqual(new[] { "ice_split", "ice_shard_damage" }, session.Build.cards);
                Assert.IsFalse(session.LoadPreset("slot-that-does-not-exist"));
            }
            finally { PlayerPrefs.DeleteKey("SkillSandbox.Preset." + Slot); }
        }

        [Test]
        public void ChangedEvent_FiresOnEveryRebuild()
        {
            int changes = 0;
            session.Changed += () => changes++;
            session.SelectSkill(1);
            session.TryAddCard("arrow_sharp", out _);
            session.SetPermanentLevel(2);
            Assert.AreEqual(3, changes);
        }

        // ── 통계 텍스트 ───────────────────────────────────────────────

        [Test]
        public void StatsText_DescribesSkillNameCastTypeAndNonZeroStats()
        {
            Assert.AreEqual("(스킬 없음)", session.StatsText());
            session.SelectSkill(7);
            var text = session.StatsText();
            StringAssert.Contains("번개 구름", text);
            StringAssert.Contains("Area", text);
            StringAssert.Contains("Radius", text);
            StringAssert.DoesNotContain("Lightning:", text, "값이 모두 0인 묶음은 숨긴다");
            StringAssert.DoesNotContain("Explosion:", text);
        }
    }
}
