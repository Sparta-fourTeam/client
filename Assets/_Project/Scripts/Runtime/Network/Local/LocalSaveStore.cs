using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Core;
using Newtonsoft.Json;
using VContainer;

namespace Game.Network
{
    /// <summary>세이브 데이터를 JSON 파일로 저장/로드하는 Local 백엔드 구현</summary>
    public sealed class LocalSaveStore
    {
        private readonly string _filePath;
        private readonly int _startEnergy;
        private readonly List<string> _legacyTutorials;

        /// <summary>에디터는 프로젝트별 경로, 플레이어 빌드는 persistentDataPath/save.json을 사용한다. 새 세이브는 시작 에너지를 가지고 시작한다</summary>
        [Inject]
        public LocalSaveStore(GameDataStore data)
            : this(LocalSavePath.Current, LocalSavePath.Legacy, data)
        {
        }

        /// <summary>지정한 경로를 사용한다. (테스트에서 임시 경로 주입용)
        /// data: 새 세이브의 시작 에너지와, 튜토리얼이 생기기 전 저장 파일을 마친 것으로 볼 튜토리얼 목록. 없으면 에너지 0, 목록은 비어 있다.
        /// 시작 에너지는 로그인(LocalAuthApi)을 거치지 않고 로비부터 재생해도 새 계정이 에너지를 갖게 한다</summary>
        public LocalSaveStore(string filePath, GameDataStore data = null)
            : this(filePath, null, data)
        {
        }

        /// <summary>지정한 이전 경로의 세이브를 처음 한 번만 가져온다. 기존 프로젝트 세이브와 원본은 보존한다.</summary>
        public LocalSaveStore(string filePath, string legacyFilePath, GameDataStore data = null)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("파일 경로가 비어 있습니다.", nameof(filePath));
            }

            _filePath = filePath;
            _startEnergy = data?.Energy.Start ?? 0;
            _legacyTutorials = data?.Tutorials.Select(t => t.Id).ToList() ?? new List<string>();
            if (!string.IsNullOrEmpty(legacyFilePath)
                && !string.Equals(Path.GetFullPath(filePath), Path.GetFullPath(legacyFilePath), StringComparison.Ordinal))
            {
                ImportLegacySave(legacyFilePath);
            }
        }

        private void ImportLegacySave(string legacyFilePath)
        {
            string marker = _filePath + ".migrated";
            if (File.Exists(marker)) { return; }
            EnsureDirectory();
            if (!File.Exists(_filePath) && File.Exists(legacyFilePath))
            {
                File.Copy(legacyFilePath, _filePath, overwrite: false);
            }
            // 삭제 후 재생할 때 공유 세이브가 다시 복원되지 않게 이전 여부를 별도로 남긴다.
            File.WriteAllText(marker, string.Empty);
        }

        private void EnsureDirectory()
        {
            string directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory)) { Directory.CreateDirectory(directory); }
        }

        // LocalSave의 필드 기본값(예: stageProgress 시드 1행)을 Newtonsoft가 리스트에 이어붙이지 않고
        // JSON 내용으로 통째로 교체하게 한다. 없으면 Load()를 반복할 때마다 기본값 행이 계속 중복된다.
        private static readonly JsonSerializerSettings ReplaceCollections =
            new JsonSerializerSettings { ObjectCreationHandling = ObjectCreationHandling.Replace };

        /// <summary>저장 파일이 이미 있는지(기존 계정인지) 확인한다</summary>
        public bool Exists() => File.Exists(_filePath);

        /// <summary>파일이 없으면 기본값의 LocalSave를 반환한다</summary>
        public LocalSave Load()
        {
            var save = File.Exists(_filePath)
                ? JsonConvert.DeserializeObject<LocalSave>(File.ReadAllText(_filePath), ReplaceCollections)
                : NewSave();
            // 튜토리얼이 생기기 전 저장 파일은 이미 플레이한 계정이므로 튜토리얼을 마친 것으로 본다
            save.tutorials ??= save.exp > 0 ? new List<string>(_legacyTutorials) : new List<string>();
            return save;
        }

        private LocalSave NewSave()
        {
            var save = new LocalSave();
            save.wallet.energyStored = _startEnergy;
            save.wallet.energyUpdatedAt = DateTime.UtcNow.ToString("O");
            return save;
        }

        /// <summary>이전 저장 내용을 덮어쓴다</summary>
        public void Flush(LocalSave save)
        {
            EnsureDirectory();
            File.WriteAllText(_filePath, JsonConvert.SerializeObject(save));
        }
    }
}
