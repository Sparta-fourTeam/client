using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Core;
using Game.View;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Tests
{
    /// <summary>미션 상세의 몬스터·보상 칸을 누르면 뜨는 공용 상세 창(#312)</summary>
    public sealed class MissionInfoTests
    {
        private const BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance;
        private static readonly GameDataStore Data = new();

        private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, Flags).GetValue(target);

        private static string NameOf(int id) => id == 3 ? "작은 슬라임" : $"#{id}";

        // ---------- 몬스터 특징 문장 ----------

        [Test(Description = "등급과 공격 방식이 부제가 된다")]
        public void Subtitle_GradeAndAttack()
        {
            Assert.AreEqual("일반 · 원거리 공격", MonsterTraits.Subtitle(Data.Monsters.GetOrThrow(1)));
            Assert.AreEqual("보스 · 근접 공격", MonsterTraits.Subtitle(new MonsterDefinition { IsBoss = true }));
            Assert.AreEqual("엘리트 · 근접 공격 3연타", MonsterTraits.Subtitle(new MonsterDefinition { IsElite = true, BurstCount = 3 }));
        }

        [Test(Description = "분열 패시브는 나뉘는 몬스터의 이름과 수로 적는다")]
        public void Lines_SpawnOnDeath()
        {
            CollectionAssert.Contains(MonsterTraits.Lines(Data.Monsters.GetOrThrow(1), NameOf), "쓰러지면 작은 슬라임 2마리로 나뉘어요");
        }

        [Test(Description = "속성 무효·면역·밀치기 배율·방어막을 문장으로 만든다 (갑옷 게)")]
        public void Lines_Crab()
        {
            var lines = MonsterTraits.Lines(Data.Monsters.GetOrThrow(21), NameOf);

            CollectionAssert.Contains(lines, "뇌속성 공격이 통하지 않아요");
            CollectionAssert.Contains(lines, "점화에 걸리지 않아요");
            CollectionAssert.Contains(lines, "밀려나는 거리가 50% 줄어요");
            Assert.IsTrue(lines.Any(l => l.StartsWith("처음부터 공격 6번을 막는 방어막")), string.Join(" / ", lines));
        }

        [Test(Description = "약점은 퍼센트로, 투사체 막기는 따로 적는다 (골렘)")]
        public void Lines_Golem()
        {
            var lines = MonsterTraits.Lines(Data.Monsters.GetOrThrow(25), NameOf);

            CollectionAssert.Contains(lines, "화속성 공격에 약해요 (+200%)");
            CollectionAssert.Contains(lines, "투사체의 직접 피해를 막고 관통시키지 않아요");
            CollectionAssert.Contains(lines, "점화·마비·빙결에 걸리지 않아요");
        }

        [Test(Description = "특징이 없으면 안내 한 줄, 있으면 줄마다 점을 붙인다")]
        public void Description_Bullets()
        {
            Assert.AreEqual("특별한 능력이 없어요.", MonsterTraits.Description(Data.Monsters.GetOrThrow(2), NameOf));
            StringAssert.StartsWith("· 쓰러지면", MonsterTraits.Description(Data.Monsters.GetOrThrow(1), NameOf));
        }

        [Test(Description = "받침이 있으면 '이', 없으면 '가'를 붙인다")]
        public void Lines_KoreanParticle()
        {
            var monster = new MonsterDefinition
            {
                Id = 999,
                Hp = 1,
                AttackInterval = 1,
                Passives = new List<PassiveDefinition> { new() { Kind = PassiveKind.Modifier, Target = "BurnDuration", Multiplier = 2f } },
            };

            CollectionAssert.Contains(MonsterTraits.Lines(monster, NameOf), "점화 지속시간이 100% 늘어요");
        }

        // ---------- 보상 이름·설명 ----------

        [Test(Description = "코인·경험치·랜덤 재료는 정해진 이름, 나머지는 Items 표의 이름을 쓴다")]
        public void ItemInfo_Names()
        {
            Assert.AreEqual("잉크", ItemInfo.NameOf(RewardRows.CoinId, Data));
            Assert.AreEqual("경험치", ItemInfo.NameOf(RewardRows.ExpId, Data));
            Assert.AreEqual("랜덤 마법북", ItemInfo.NameOf(ItemIds.RandomSkillMaterial, Data));
            Assert.AreEqual("랜덤 인챈트 주문서", ItemInfo.NameOf(ItemIds.RandomEquipmentMaterial, Data));
            Assert.AreEqual(Data.Items.GetOrThrow(ItemIds.ArrowBook).Name, ItemInfo.NameOf(ItemIds.ArrowBook, Data));
            StringAssert.Contains("보석", ItemInfo.DescriptionOf(ItemIds.GemChest));
            StringAssert.Contains("스테이지", ItemInfo.DescriptionOf(ShopRules.EnergyReward));
        }

        [TestCase("SkillMaterial", "랜덤 마법북", "열린 스킬")]
        [TestCase("EquipmentMaterial", "랜덤 인챈트 주문서", "열린 장비")]
        public void RandomRewardPresentationUsesTheConfiguredKind(string kind, string name, string description)
        {
            var data = new GameDataStore();
            const string randomId = "fixture.random.material";
            data.RewardRules.Items[randomId] = new RewardItemRule { Kind = kind, Random = true };
            Assert.AreEqual(name, ItemInfo.NameOf(randomId, data));
            StringAssert.Contains(description, ItemInfo.DescriptionOf(randomId, data));
        }

        [Test(Description = "보상 줄에는 코인·경험치·아이템 ID가 붙는다")]
        public void RewardRows_CarryIds()
        {
            var rows = RewardRows.Build(null, Data, 10, 5, new[] { new ItemAmount { itemId = ItemIds.GemChest, quantity = 1 }, new ItemAmount { itemId = ItemIds.ArrowBook, quantity = 2 } });

            CollectionAssert.AreEqual(new[] { RewardRows.CoinId, RewardRows.ExpId, ItemIds.ArrowBook, ItemIds.GemChest }, rows.Select(r => r.ItemId).ToArray());
        }

        [TestCase(17, 0, 17, 0)]
        [TestCase(30, 17, 13, 17)]
        [TestCase(17, 17, 0, 17)]
        public void ExperienceRowsSeparateBaseAndBonusWithoutDoubleCounting(int total, int bonus, int expectedBase, int expectedBonus)
        {
            var rows = RewardRows.Build(null, Data, 0, total, new ItemAmount[0], bonus);
            Assert.AreEqual(expectedBase, rows.Where(row => row.ItemId == RewardRows.ExpId).Sum(row => row.Count));
            Assert.AreEqual(expectedBonus, rows.Where(row => row.ItemId == RewardRows.BonusExpId).Sum(row => row.Count));
            Assert.AreEqual(total, rows.Where(row => row.ItemId != RewardRows.CoinId).Sum(row => row.Count));
            Assert.AreEqual(bonus > 0 ? 1 : 0, rows.Count(row => row.ItemId == RewardRows.BonusExpId));
            Assert.AreEqual("3성 보너스 경험치", ItemInfo.NameOf(RewardRows.BonusExpId, Data));
        }

        // ---------- 미션 상세 팝업 (실제 프리팹) ----------

        private readonly List<GameObject> _spawned = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _spawned) { Object.DestroyImmediate(go); }
            _spawned.Clear();
        }

        private (MissionPopupView mission, DetailPopupView detail) OpenMission(int stageId)
        {
            GameObject Spawn(string path)
            {
                var go = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Project/Prefabs/UI/{path}.prefab"));
                _spawned.Add(go);
                return go;
            }

            var mission = Spawn("Lobby/MissionDetailsPopup").GetComponent<MissionPopupView>();
            var detail = Spawn("Common/DetailPopup").GetComponent<DetailPopupView>();
            mission.Initialize();
            detail.Initialize();
            typeof(MissionPopupView).GetField("_detail", Flags).SetValue(mission, detail);
            mission.ShowDetails(Data, stageId);
            return (mission, detail);
        }

        private static GameObject SlotRoot(object listView, int index)
        {
            if (listView is ResultRewardListView rewards)
            {
                return rewards.Cells[index].gameObject;
            }

            var slots = (System.Array)listView.GetType().GetField("_slots", Flags).GetValue(listView);
            var slot = slots.GetValue(index);
            return (GameObject)slot.GetType().GetField("Root").GetValue(slot);
        }

        [Test(Description = "몬스터 칸을 누르면 이름·등급·특징으로 상세 창이 열린다")]
        public void Mission_MonsterClick_OpensDetail()
        {
            var (mission, detail) = OpenMission(3);
            var display = Field<MonsterDisplayTable>(mission, "_monsterDisplay");
            var rows = MonsterRows.Build(display, Data, 3);

            var slot = SlotRoot(Field<StageMonsterListView>(mission, "_monsters"), 0);
            Assert.IsTrue(slot.GetComponent<Image>().raycastTarget, "칸 이미지가 터치를 받아야 실제로 눌린다");
            Assert.IsTrue(slot.GetComponent<Button>().interactable);
            slot.GetComponent<Button>().onClick.Invoke();

            var monster = Data.Monsters.GetOrThrow(rows[0].MonsterId);
            Assert.IsTrue(detail.IsShown);
            Assert.AreEqual(rows[0].Name, Field<TMP_Text>(detail, "_titleText").text);
            Assert.AreEqual(MonsterTraits.Subtitle(monster), Field<TMP_Text>(detail, "_subtitleText").text);
            Assert.IsFalse(Field<Button>(detail, "_goButton").gameObject.activeSelf, "몬스터는 이동할 곳이 없다");
        }

        [Test(Description = "보상 칸을 누르면 이름·최대 획득 수·설명으로 상세 창이 열린다")]
        public void Mission_RewardClick_OpensDetail()
        {
            var (mission, detail) = OpenMission(3);

            var slot = SlotRoot(Field<ResultRewardListView>(mission, "_rewards"), 0);
            Assert.IsTrue(slot.GetComponent<Image>().raycastTarget, "칸 이미지가 터치를 받아야 실제로 눌린다");
            slot.GetComponent<Button>().onClick.Invoke();

            Assert.IsTrue(detail.IsShown);
            Assert.AreEqual("잉크", Field<TMP_Text>(detail, "_titleText").text);
            StringAssert.StartsWith("최대 ", Field<TMP_Text>(detail, "_subtitleText").text);
            Assert.AreEqual(ItemInfo.DescriptionOf(RewardRows.CoinId), Field<TMP_Text>(detail, "_descText").text);
        }

        [Test(Description = "누를 일을 넘기지 않는 곳(결과·일시정지 창)에서는 칸이 버튼이 되지 않는다")]
        public void ListWithoutClick_HasNoButtons()
        {
            var (mission, _) = OpenMission(3);
            var rewards = Field<ResultRewardListView>(mission, "_rewards");
            var fresh = Object.Instantiate(rewards.gameObject);
            _spawned.Add(fresh);
            var list = fresh.GetComponent<ResultRewardListView>();
            foreach (var button in fresh.GetComponentsInChildren<Button>(true)) { Object.DestroyImmediate(button); }

            list.Show(RewardRows.Build(null, Data, 10, 0, new ItemAmount[0]));

            Assert.IsEmpty(fresh.GetComponentsInChildren<Button>(true));
            Assert.IsFalse(SlotRoot(list, 0).GetComponent<Image>().raycastTarget, "누를 일이 없으면 터치를 받지 않는다");
        }
    }
}
