using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace Game.Network
{
    public sealed class LocalSaveStore
    {
        private readonly string _filePath;

        public LocalSaveStore(string filePath = null)
        {
            _filePath = filePath ?? Path.Combine(Application.persistentDataPath, "save.json");
        }

        public LocalSave Load()
        {
            if (!File.Exists(_filePath))
            {
                return new LocalSave();
            }

            var json = File.ReadAllText(_filePath);
            return JsonConvert.DeserializeObject<LocalSave>(json);
        }

        public void Flush(LocalSave save)
        {
            File.WriteAllText(_filePath, JsonConvert.SerializeObject(save));
        }
    }
}
