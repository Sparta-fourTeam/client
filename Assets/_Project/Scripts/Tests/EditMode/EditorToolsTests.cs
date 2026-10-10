using System;
using System.IO;
using Game.Editor;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class EditorToolsTests
    {

        [Test]
        public void SaveResetKeepsAnExactRecoverableBackup()
        {
            string folder = Path.Combine(Path.GetTempPath(), "nova-tools-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                string save = Path.Combine(folder, "save.json");
                byte[] contents = System.Text.Encoding.UTF8.GetBytes("{\"gold\":123,\"name\":\"검증\"}");
                File.WriteAllBytes(save, contents);
                string backup = PlaytestWindow.BackupAndReset(save);
                Assert.That(File.Exists(save), Is.False);
                Assert.That(File.ReadAllBytes(backup), Is.EqualTo(contents));
                File.Copy(backup, save);
                string nextBackup = PlaytestWindow.BackupAndReset(save);
                Assert.That(nextBackup, Is.Not.EqualTo(backup));
                Assert.That(File.Exists(backup), Is.True);
            }
            finally { Directory.Delete(folder, true); }
        }

    }
}
