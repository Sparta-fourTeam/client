using System;
using System.IO;
using System.Linq;
using Game.Core;
using Game.Editor;
using Game.Network;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class EditorToolsTests
    {
        [Test]
        public void AndroidExportArgumentOverridesProfileBundleFormatAndRejectsMismatch()
        {
            bool original = UnityEditor.EditorUserBuildSettings.buildAppBundle;
            try
            {
                var args = CiBuildArgs.Parse(new[] { "-androidExportType", "androidPackage" });
                UnityEditor.EditorUserBuildSettings.buildAppBundle = true;
                CiBuild.ConfigureAndroidExport(args["androidExportType"], "build/Android.apk");
                Assert.That(UnityEditor.EditorUserBuildSettings.buildAppBundle, Is.False);
                CiBuild.ConfigureAndroidExport("androidAppBundle", "build/Android.aab");
                Assert.That(UnityEditor.EditorUserBuildSettings.buildAppBundle, Is.True);
                Assert.Throws<ArgumentException>(() => CiBuild.ConfigureAndroidExport("androidPackage", "build/Android.aab"));
                Assert.Throws<ArgumentException>(() => CiBuild.ConfigureAndroidExport("unknown", "build/Android.apk"));
            }
            finally { UnityEditor.EditorUserBuildSettings.buildAppBundle = original; }
        }

        [TestCase("Common", "CloseButton")]
        [TestCase("Lobby", "MissionDetailsPopup")]
        public void WorkspacePreviewDoesNotRegisterButtonsAndDisposesItsScene(string group, string id)
        {
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(
                $"Assets/_Project/Prefabs/UI/{group}/{id}.prefab");
            Assert.That(prefab, Is.Not.Null);
            int selectables = UnityEngine.UI.Selectable.allSelectableCount;
            int previewScenes = UnityEditor.SceneManagement.EditorSceneManager.previewSceneCount;
            for (int attempt = 0; attempt < 2; attempt++)
            {
                using (var preview = UiPreviewRenderer.CreateSession(prefab, null, UiPreviewRenderer.Devices[0], true, "기본", id))
                {
                    Assert.That(preview.ReviewRoot.GetComponentsInChildren<UnityEngine.UI.Selectable>(true), Is.Not.Empty);
                    Assert.That(UnityEngine.UI.Selectable.allSelectableCount, Is.EqualTo(selectables),
                        "작업실의 시각 미리보기 버튼은 실제 UI의 탐색 목록에 들어가면 안 된다");
                }
                Assert.That(UnityEngine.UI.Selectable.allSelectableCount, Is.EqualTo(selectables));
                Assert.That(UnityEditor.SceneManagement.EditorSceneManager.previewSceneCount, Is.EqualTo(previewScenes));
            }
        }

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

        [Test]
        public void SaveRestoreUsesLatestSameSecondBackupAndPreservesCurrentSave()
        {
            string folder = Path.Combine(Path.GetTempPath(), "nova-tools-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                string save = Path.Combine(folder, "save.json");
                string older = save + ".20991013-120000.0000001.bak";
                string newer = save + ".20991013-120000.0000002.bak";
                File.WriteAllText(older, "older");
                File.WriteAllText(newer, "newer");
                File.SetLastWriteTimeUtc(older, new DateTime(2026, 10, 14, 0, 0, 0, DateTimeKind.Utc));
                File.SetLastWriteTimeUtc(newer, new DateTime(2026, 10, 13, 0, 0, 0, DateTimeKind.Utc));
                File.WriteAllText(save, "current");

                string latest = PlaytestWindow.LatestBackup(save);
                Assert.That(latest, Is.EqualTo(newer));
                string preserved = PlaytestWindow.Restore(save, latest);
                Assert.That(preserved, Is.EqualTo(save + ".20991013-120000.0000003.bak"),
                    "시계가 기존 백업보다 앞서 있어도 생성 순서가 유지되어야 한다");
                Assert.That(PlaytestWindow.LatestBackup(save), Is.EqualTo(preserved));
                Assert.That(File.ReadAllText(save), Is.EqualTo("newer"));
                Assert.That(File.ReadAllText(preserved), Is.EqualTo("current"));
                Assert.That(File.ReadAllText(older), Is.EqualTo("older"));
                Assert.That(File.ReadAllText(newer), Is.EqualTo("newer"));
            }
            finally { Directory.Delete(folder, true); }
        }

        [Test]
        public void SaveLegacySameSecondBackupsDoNotGuessLatestAndNewBackupFollowsBoth()
        {
            string folder = Path.Combine(Path.GetTempPath(), "nova-tools-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                string save = Path.Combine(folder, "save.json");
                string older = save + ".20991013-120000-ffffffff.bak";
                string newer = save + ".20991013-120000-00000000.bak";
                File.WriteAllText(save, "older");
                File.Copy(save, older);
                File.WriteAllText(save, "newer");
                File.Copy(save, newer);
                File.SetLastWriteTimeUtc(newer, File.GetLastWriteTimeUtc(older));
                Assert.IsNull(PlaytestWindow.LatestBackup(save), "구형 GUID/복사된 메타데이터로 최신 스냅샷을 추측하면 안 된다");

                string next = PlaytestWindow.Backup(save);
                Assert.That(next, Is.EqualTo(save + ".20991013-120000.0000001.bak"));
                Assert.That(PlaytestWindow.LatestBackup(save), Is.EqualTo(next), "신형/구형이 섞여도 확정된 새 백업을 선택한다");
                File.WriteAllText(save, "current");
                string preserved = PlaytestWindow.Restore(save, next);
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

        [Test]
        public void ResourceCheatSetsEveryDeclaredItemWithoutDuplicatingOrChangingLevel()
        {
            var data = new GameDataStore();
            var first = data.Items.Values.First();
            var save = new LocalSave { exp = 123 };
            save.wallet.gold = 2000;
            save.items.Add(new ItemAmount { itemId = first.Id, quantity = 2000 });

            PlaytestCheats.SetAllResources(save, data);
            PlaytestCheats.SetAllResources(save, data);

            Assert.That(save.wallet.gold, Is.EqualTo(999));
            Assert.That(save.wallet.energyStored, Is.EqualTo(999));
            foreach (var definition in data.Items.Values)
            {
                var rows = save.items.Where(item => item.itemId == definition.Id).ToList();
                Assert.That(rows.Count, Is.EqualTo(1), definition.Id);
                Assert.That(rows[0].quantity, Is.EqualTo(999), definition.Id);
            }
            Assert.That(save.exp, Is.EqualTo(123));
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
