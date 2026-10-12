using System.IO;
using UnityEngine;

namespace Game.Core
{
    /// <summary>Local 백엔드와 에디터 도구가 함께 사용하는 세이브 경로.</summary>
    public static class LocalSavePath
    {
        public static string Legacy => Path.Combine(Application.persistentDataPath, "save.json");

        public static string Current
        {
            get
            {
#if UNITY_EDITOR
                return ForEditorProject(Application.dataPath);
#else
                return Legacy;
#endif
            }
        }

        /// <summary>프로젝트의 Assets 경로를 기준으로 워크트리마다 저장 파일을 분리한다.</summary>
        public static string ForEditorProject(string assetsPath) =>
            Path.GetFullPath(Path.Combine(assetsPath, "..", "UserSettings", "LocalSave", "save.json"));
    }
}
