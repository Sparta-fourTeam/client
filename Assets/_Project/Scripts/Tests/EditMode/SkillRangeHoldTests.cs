using System.Collections.Generic;
using Game.Core;
using Game.View;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using Object = UnityEngine.Object;

namespace Game.Tests
{
    /// <summary>스킬 HUD를 누르고 있는 동안 사정거리를 점선으로 보여 주는 기능: 모양 결정, 점선 좌표, 슬롯 누름 이벤트</summary>
    public sealed class SkillRangeHoldTests
    {
        private sealed class Status : ISkillStatus
        {
            public int Id { get; set; } = 1;
            public int Level => 1;
            public string AssetKey => "weapon_arrow";
            public float CooldownRatio => 0;
        }

        private static SkillData Data(ProjectilePath path) => new SkillData { id = 1, projectilePath = path };

        // ── 모양 결정 ─────────────────────────────────────────────────

        [Test(Description = "플레이어에서 발사되는 스킬은 플레이어 중심 반원이다")]
        public void Resolver_AimedSkillIsAnArcAroundTheCaster()
        {
            var range = SkillRangeResolver.For(Data(ProjectilePath.Aimed), 12, new Vector2(1, -4), -2);

            Assert.AreEqual(SkillRangeShape.Arc, range.Shape);
            Assert.AreEqual(new Vector2(1, -4), range.Center);
            Assert.AreEqual(12f, range.Radius);
        }

        [Test(Description = "굴러가는 스킬(나무뿌리)은 벽 앞선에서 사정거리만큼 위의 가로선이다")]
        public void Resolver_RollingLaneSkillIsAHorizontalLineAboveTheWallLine()
        {
            var range = SkillRangeResolver.For(Data(ProjectilePath.RollingLane), 12, new Vector2(1, -4), -2);

            Assert.AreEqual(SkillRangeShape.HorizontalLine, range.Shape);
            Assert.AreEqual(10f, range.LineY, .0001f);
        }

        [Test(Description = "벽이 없으면 굴러가는 스킬도 반원으로 그린다")]
        public void Resolver_WithoutAWallFallsBackToTheArc()
        {
            Assert.AreEqual(SkillRangeShape.Arc, SkillRangeResolver.For(Data(ProjectilePath.RollingLane), 12, Vector2.zero, null).Shape);
        }

        // ── 점선 좌표 ─────────────────────────────────────────────────

        [Test(Description = "반원의 모든 점은 중심에서 반지름만큼 떨어져 있고 중심보다 위(또는 같은 높이)다. 양 끝은 좌우 가장자리다")]
        public void ArcPoints_AreOnTheUpperHalfOfTheCircle()
        {
            var center = new Vector2(1, -4);
            var points = SkillRangeIndicator.ArcPoints(center, 5, 32);

            Assert.AreEqual(33, points.Length);
            foreach (var point in points)
            {
                Assert.AreEqual(5f, Vector2.Distance(center, point), .001f);
                Assert.GreaterOrEqual(point.y, center.y - .0001f);
            }
            Assert.AreEqual(6f, points[0].x, .001f);
            Assert.AreEqual(-4f, points[32].x, .001f);
        }

        [Test(Description = "가로선은 같은 높이의 두 점이고 가운데 x를 기준으로 양쪽 반폭만큼 뻗는다")]
        public void LinePoints_AreAHorizontalSegmentCenteredOnTheCamera()
        {
            var points = SkillRangeIndicator.LinePoints(3.5f, 1f, 4f);

            Assert.AreEqual(2, points.Length);
            Assert.AreEqual(3.5f, points[0].y);
            Assert.AreEqual(3.5f, points[1].y);
            Assert.AreEqual(-3f, points[0].x);
            Assert.AreEqual(5f, points[1].x);
        }

        // ── 슬롯 누름 ─────────────────────────────────────────────────

        private GameObject hud;

        [TearDown]
        public void TearDown()
        {
            if (hud != null) { Object.DestroyImmediate(hud); }
        }

        private SkillSlotView Slot()
        {
            hud = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/SkillHud.prefab"));
            return hud.GetComponentsInChildren<SkillSlotView>(true)[0];
        }

        [Test(Description = "스킬이 든 슬롯을 누르면 시작, 떼면 끝 이벤트가 한 번씩 난다")]
        public void Slot_PressAndReleaseRaiseHoldEventsOnce()
        {
            var slot = Slot();
            var status = new Status { Id = 7 };
            slot.Bind(status, null);
            var started = new List<int>();
            int ended = 0;
            slot.HoldStarted += s => started.Add(s.Id);
            slot.HoldEnded += () => ended++;
            var pointer = new PointerEventData(null);

            slot.OnPointerDown(pointer);
            slot.OnPointerDown(pointer);
            slot.OnPointerUp(pointer);
            slot.OnPointerUp(pointer);

            CollectionAssert.AreEqual(new[] { 7 }, started);
            Assert.AreEqual(1, ended);
        }

        [Test(Description = "빈 슬롯은 눌러도 아무 일이 없다")]
        public void Slot_EmptySlotRaisesNothing()
        {
            var slot = Slot();
            slot.Clear();
            int events = 0;
            slot.HoldStarted += _ => events++;
            slot.HoldEnded += () => events++;

            slot.OnPointerDown(new PointerEventData(null));
            slot.OnPointerUp(new PointerEventData(null));

            Assert.AreEqual(0, events);
        }

        [Test(Description = "누르는 중에 스킬이 사라져 슬롯이 비면 표시가 끝난다")]
        public void Slot_ClearingWhileHeldEndsTheHold()
        {
            var slot = Slot();
            slot.Bind(new Status(), null);
            int ended = 0;
            slot.HoldEnded += () => ended++;
            slot.OnPointerDown(new PointerEventData(null));

            slot.Clear();

            Assert.AreEqual(1, ended);
        }
    }
}
