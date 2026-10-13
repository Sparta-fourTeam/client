using System;
using System.IO;
using Game.Core;
using Game.Editor;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class EditorToolsTests
    {

        [Test]
        public void SaveBackupPreservesOriginalBytesAndCreatesSeparateCopies()
        {
            string folder = Path.Combine(Path.GetTempPath(), "nova-tools-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                string save = Path.Combine(folder, "save.json");
                byte[] contents = System.Text.Encoding.UTF8.GetBytes("{\"gold\":123,\"name\":\"검증\"}");
                File.WriteAllBytes(save, contents);
                string backup = PlaytestWindow.Backup(save);
                Assert.That(Path.GetFileName(backup), Does.Match(@"^save\.json\.\d{8}-\d{6}\.\d{7}\.bak$"));
                Assert.That(File.ReadAllBytes(save), Is.EqualTo(contents));
                Assert.That(File.ReadAllBytes(backup), Is.EqualTo(contents));
                string nextBackup = PlaytestWindow.Backup(save);
                Assert.That(nextBackup, Is.Not.EqualTo(backup));
                Assert.That(PlaytestWindow.LatestBackup(save), Is.EqualTo(nextBackup));
                Assert.That(File.ReadAllBytes(backup), Is.EqualTo(contents));
                Assert.That(File.ReadAllBytes(nextBackup), Is.EqualTo(contents));
                Assert.That(File.ReadAllBytes(save), Is.EqualTo(contents));
            }
            finally { Directory.Delete(folder, true); }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SaveRestoreUsesLatestSameSecondBackupAndPreservesCurrentSave(bool legacy)
        {
            string folder = Path.Combine(Path.GetTempPath(), "nova-tools-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                string save = Path.Combine(folder, "save.json");
                string older = save + (legacy ? ".20991013-120000-00000000.bak" : ".20991013-120000.0000001.bak");
                string newer = save + (legacy ? ".20991013-120000-ffffffff.bak" : ".20991013-120000.0000002.bak");
                File.WriteAllText(older, "older");
                File.WriteAllText(newer, "newer");
                File.SetLastWriteTimeUtc(older, new DateTime(2026, 10, 14, 0, 0, 0, DateTimeKind.Utc));
                File.SetLastWriteTimeUtc(newer, new DateTime(2026, 10, 13, 0, 0, 0, DateTimeKind.Utc));
                File.WriteAllText(save, "current");

                string latest = PlaytestWindow.LatestBackup(save);
                Assert.That(latest, Is.EqualTo(newer));
                string preserved = PlaytestWindow.Restore(save, latest);
                Assert.That(preserved, Is.EqualTo(save + (legacy ? ".20991013-120000.0000001.bak" : ".20991013-120000.0000003.bak")),
                    "시계가 기존 백업보다 앞서 있어도 생성 순서가 유지되어야 한다");
                Assert.That(PlaytestWindow.LatestBackup(save), Is.EqualTo(preserved));
                Assert.That(File.ReadAllText(save), Is.EqualTo("newer"));
                Assert.That(File.ReadAllText(preserved), Is.EqualTo("current"));
                Assert.That(File.ReadAllText(older), Is.EqualTo("older"));
                Assert.That(File.ReadAllText(newer), Is.EqualTo("newer"));
            }
            finally { Directory.Delete(folder, true); }
        }

        [Test(Description = "세이브 삭제는 원본을 지우되 지우기 전 내용을 백업으로 남긴다")]
        public void SaveDeleteRemovesOriginalAndKeepsBackup()
        {
            string folder = Path.Combine(Path.GetTempPath(), "nova-tools-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                string save = Path.Combine(folder, "save.json");
                byte[] contents = System.Text.Encoding.UTF8.GetBytes("{\"gold\":123}");
                File.WriteAllBytes(save, contents);
                string backup = PlaytestWindow.Delete(save);
                Assert.That(File.Exists(save), Is.False);
                Assert.That(File.ReadAllBytes(backup), Is.EqualTo(contents));
            }
            finally { Directory.Delete(folder, true); }
        }

        // 레벨 치트가 레벨업 팝업 재현에 쓰이므로, 설정한 exp가 실제 레벨 계산에서 의도한 레벨·직전 상태가 되는지 본다
        [Test]
        public void LevelCheatExpLandsOnRequestedLevelOrJustBeforeNext()
        {
            var table = new PlayerLevelTable(new[]
            {
                new PlayerLevelRow { Level = 1, RequiredExp = 10 },
                new PlayerLevelRow { Level = 2, RequiredExp = 25 },
                new PlayerLevelRow { Level = 3, RequiredExp = 40 },
            });

            Assert.That(table.At(PlaytestCheats.ExpFor(table, 3, false)), Is.EqualTo((3, 0)));
            Assert.That(table.At(PlaytestCheats.ExpFor(table, 3, true)), Is.EqualTo((3, 39)));
            Assert.That(table.At(PlaytestCheats.ExpFor(table, 99, true)).level, Is.EqualTo(table.MaxLevel));
        }

    }
}
