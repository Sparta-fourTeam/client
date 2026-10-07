using System;
using System.Collections.Generic;
using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    public class PlayerLevelTests
    {
        // 1→2: 100, 2→3: 150, 3→4: 200 (만렙 4)
        private static PlayerLevelTable SmallTable() => new(new List<PlayerLevelRow>
        {
            new() { Level = 1, RequiredExp = 100 },
            new() { Level = 2, RequiredExp = 150 },
            new() { Level = 3, RequiredExp = 200 },
        });

        [TestCase(0, 1, 0)]
        [TestCase(99, 1, 99)]
        [TestCase(100, 2, 0)]
        [TestCase(249, 2, 149)]
        [TestCase(250, 3, 0)]
        [TestCase(-5, 1, 0)]
        public void At_Boundaries(int exp, int level, int into)
        {
            Assert.AreEqual((level, into), SmallTable().At(exp));
        }

        [Test(Description = "한 번의 보상으로 여러 레벨이 오른다 (100 + 150 = 250, 잔여 20 → 3레벨)")]
        public void At_MultipleLevelsAtOnce()
        {
            Assert.AreEqual((3, 20), SmallTable().At(270));
        }

        [Test(Description = "만렙에서는 레벨이 오르지 않고 남는 경험치를 보관한다")]
        public void At_CapsAtMaxLevel()
        {
            var table = SmallTable();

            Assert.AreEqual(4, table.MaxLevel);
            Assert.AreEqual((4, 7), table.At(450 + 7));
            Assert.AreEqual(4, table.At(int.MaxValue).level);
            Assert.AreEqual(0, table.RequiredToNext(4));
            Assert.AreEqual(200, table.RequiredToNext(3));
        }

        [Test(Description = "잘못된 테이블(빈 목록, 레벨 누락, 0 이하 요구량)은 부팅 때 거절한다")]
        public void Constructor_RejectsInvalidRows()
        {
            Assert.Throws<InvalidOperationException>(() => new PlayerLevelTable(new List<PlayerLevelRow>()));
            Assert.Throws<InvalidOperationException>(() => new PlayerLevelTable(new List<PlayerLevelRow>
            {
                new() { Level = 1, RequiredExp = 100 }, new() { Level = 3, RequiredExp = 100 },
            }));
            Assert.Throws<InvalidOperationException>(() => new PlayerLevelTable(new List<PlayerLevelRow>
            {
                new() { Level = 1, RequiredExp = 0 },
            }));
        }

        [Test(Description = "PlayerLevels.json이 로드·검증을 통과하고 레벨이 1부터 시작한다")]
        public void Store_LoadsPlayerLevels()
        {
            var levels = new GameDataStore().PlayerLevels;

            Assert.AreEqual((1, 0), levels.At(0));
            Assert.Greater(levels.MaxLevel, 1);
        }

        [Test(Description = "Profile은 저장된 exp(재실행 후 Apply된 스냅샷)에서 같은 레벨을 복원한다")]
        public void Profile_RestoresLevelFromSnapshotExp()
        {
            var data = new GameDataStore();
            var profile = new PlayerProfile(data);
            int first = data.PlayerLevels.RequiredToNext(1);
            int second = data.PlayerLevels.RequiredToNext(2);

            profile.Apply(new PlayerSnapshot { exp = first + second + 20 });

            Assert.AreEqual(3, profile.Level);
            Assert.AreEqual(20, profile.ExpIntoLevel);
            Assert.AreEqual(data.PlayerLevels.RequiredToNext(3), profile.ExpRequiredForNext);
        }

        [Test(Description = "레벨 진행률은 현재 레벨 안의 비율이고, 만렙에서는 1이며 MAX로 판정한다")]
        public void Profile_LevelProgressAndMax()
        {
            var data = new GameDataStore();
            var profile = new PlayerProfile(data);
            int half = data.PlayerLevels.RequiredToNext(1) / 2;

            profile.Apply(new PlayerSnapshot { exp = half });
            Assert.AreEqual((float)half / data.PlayerLevels.RequiredToNext(1), profile.LevelProgress, 0.0001f);
            Assert.IsFalse(profile.IsMaxLevel);

            profile.Apply(new PlayerSnapshot { exp = int.MaxValue });
            Assert.IsTrue(profile.IsMaxLevel);
            Assert.AreEqual(1f, profile.LevelProgress);
            Assert.AreEqual(data.PlayerLevels.MaxLevel, profile.Level);
        }
    }
}
