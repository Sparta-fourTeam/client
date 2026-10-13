using System;
using System.IO;
using Game.Editor;
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
                Assert.That(File.ReadAllBytes(save), Is.EqualTo(contents));
                Assert.That(File.ReadAllBytes(backup), Is.EqualTo(contents));
                string nextBackup = PlaytestWindow.Backup(save);
                Assert.That(nextBackup, Is.Not.EqualTo(backup));
                Assert.That(File.ReadAllBytes(backup), Is.EqualTo(contents));
                Assert.That(File.ReadAllBytes(nextBackup), Is.EqualTo(contents));
                Assert.That(File.ReadAllBytes(save), Is.EqualTo(contents));
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

    }
}
