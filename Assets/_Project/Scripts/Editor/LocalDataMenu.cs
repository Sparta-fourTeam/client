using System.IO;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>Local 백엔드의 저장 데이터(save.json)를 찾아 열고 지우는 메뉴</summary>
    public static class LocalDataMenu
    {
        private const string OpenMenuPath = "Tools/Project Nova/Local Data/Open Save Folder";
        private const string DeleteMenuPath = "Tools/Project Nova/Local Data/Delete Save (save.json)";

        // LocalSaveStore의 기본 파일 이름과 같아야 한다
        private const string SaveFileName = "save.json";

        private static string SavePath => Path.Combine(Application.persistentDataPath, SaveFileName);

        [MenuItem(OpenMenuPath, priority = 100)]
        private static void OpenFolder()
        {
            Debug.Log($"[LocalData] {Application.persistentDataPath}");
            EditorUtility.RevealInFinder(File.Exists(SavePath) ? SavePath : Application.persistentDataPath);
        }

        [MenuItem(DeleteMenuPath, priority = 101)]
        private static void DeleteSave()
        {
            string path = SavePath;
            if (!File.Exists(path))
            {
                EditorUtility.DisplayDialog("Local Data", $"지울 save.json이 없습니다.\n{path}", "확인");
                return;
            }

            if (!EditorUtility.DisplayDialog("Local Data",
                    $"로컬 저장 데이터를 지웁니다. 다음 재생은 초기 데이터로 시작합니다.\n{path}", "삭제", "취소"))
            {
                return;
            }

            File.Delete(path);
            Debug.Log($"[LocalData] 삭제함: {path}");
        }

        // 재생 중에는 종료 때 파일이 다시 써져 지운 의미가 없어진다
        [MenuItem(DeleteMenuPath, validate = true)]
        private static bool ValidateDeleteSave() => !EditorApplication.isPlaying;
    }
}
