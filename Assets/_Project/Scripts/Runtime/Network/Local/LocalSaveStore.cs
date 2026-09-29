using System;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace Game.Network
{
    /// <summary>세이브 데이터를 JSON 파일로 저장/로드하는 Local 백엔드 구현</summary>
    public sealed class LocalSaveStore
    {
        private readonly string _filePath;

        /// <summary>기본 경로(Application.persistentDataPath/save.json)를 사용한다.</summary>
        public LocalSaveStore()
            : this(Path.Combine(Application.persistentDataPath, "save.json"))
        {
        }

        /// <summary>지정한 경로를 사용한다. (테스트에서 임시 경로 주입용)</summary>
        public LocalSaveStore(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("파일 경로가 비어 있습니다.", nameof(filePath));
            }

            _filePath = filePath;
        }

        // LocalSave의 필드 기본값(예: stageProgress 시드 1행)을 Newtonsoft가 리스트에 이어붙이지 않고
        // JSON 내용으로 통째로 교체하게 한다. 없으면 Load()를 반복할 때마다 기본값 행이 계속 중복된다.
        private static readonly JsonSerializerSettings ReplaceCollections =
            new JsonSerializerSettings { ObjectCreationHandling = ObjectCreationHandling.Replace };

        /// <summary>파일이 없으면 기본값의 LocalSave를 반환한다</summary>
        public LocalSave Load()
        {
            if (!File.Exists(_filePath))
            {
                return new LocalSave();
            }

            var json = File.ReadAllText(_filePath);
            return JsonConvert.DeserializeObject<LocalSave>(json, ReplaceCollections);
        }

        /// <summary>이전 저장 내용을 덮어쓴다</summary>
        public void Flush(LocalSave save)
        {
            File.WriteAllText(_filePath, JsonConvert.SerializeObject(save));
        }
    }
}
