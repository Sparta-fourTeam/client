using System;
using System.IO;
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

    }
}
