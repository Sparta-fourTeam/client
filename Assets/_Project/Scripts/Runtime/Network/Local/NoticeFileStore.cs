using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Game.Core;
using UnityEngine;

namespace Game.Network
{
    /// <summary>받은 공지를 기기의 JSON 파일에 저장한다. 파일이 없거나 깨졌으면 빈 목록으로 시작한다</summary>
    public sealed class NoticeFileStore : INoticeStore
    {
        private readonly string _path;

        public NoticeFileStore(string path) => _path = path;

        public IReadOnlyList<Notice> Load()
        {
            var notices = new List<Notice>();
            try
            {
                if (!File.Exists(_path))
                {
                    return notices;
                }

                var file = JsonUtility.FromJson<NoticeFile>(File.ReadAllText(_path));
                foreach (var row in file?.notices ?? new List<Row>())
                {
                    if (row.id > 0 && DateTimeOffset.TryParse(row.createdAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var createdAt))
                    {
                        notices.Add(new Notice(row.id, row.title, row.content, createdAt, row.isRead));
                    }
                }
            }
            catch (Exception e) when (e is IOException || e is ArgumentException || e is UnauthorizedAccessException)
            {
                Debug.LogWarning($"[Notice] 저장된 공지를 읽지 못해 비웁니다: {e.Message}");
                notices.Clear();
            }

            return notices;
        }

        public void Save(IReadOnlyList<Notice> notices)
        {
            var file = new NoticeFile { notices = new List<Row>(notices.Count) };
            foreach (var notice in notices)
            {
                file.notices.Add(new Row
                {
                    id = notice.Id,
                    title = notice.Title,
                    content = notice.Content,
                    createdAt = notice.CreatedAt.ToString("o", CultureInfo.InvariantCulture),
                    isRead = notice.IsRead,
                });
            }

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_path));
                // 쓰다 끊겨도 옛 파일이 남도록 임시 파일에 쓴 뒤 바꾼다
                string temp = _path + ".tmp";
                File.WriteAllText(temp, JsonUtility.ToJson(file));
                if (File.Exists(_path))
                {
                    File.Delete(_path);
                }

                File.Move(temp, _path);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Debug.LogWarning($"[Notice] 공지를 저장하지 못했습니다: {e.Message}");
            }
        }

        [Serializable]
        private sealed class NoticeFile
        {
            public List<Row> notices;
        }

        [Serializable]
        private sealed class Row
        {
            public long id;
            public string title;
            public string content;
            public string createdAt;
            public bool isRead;
        }
    }
}
