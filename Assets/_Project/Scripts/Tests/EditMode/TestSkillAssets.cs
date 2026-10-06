using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Core;
using UnityEditor;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>스킬 컨트롤러 테스트가 프리팹 표(SkillAssetTable)를 붙이는 도우미</summary>
    internal static class TestSkillAssets
    {
        public const string TablePath = "Assets/_Project/Data/SkillAssetTable.asset";

        /// <summary>실제 프로젝트의 에셋 표</summary>
        public static SkillAssetTable Real() => AssetDatabase.LoadAssetAtPath<SkillAssetTable>(TablePath);

        public static SkillData Data(int id) =>
            new DefaultSkillDataProvider(new GameDataStore()).LoadAll().Find(w => w.id == id);

        /// <summary>실제 프로젝트의 에셋 표를 컨트롤러에 붙인다</summary>
        public static void AttachReal(SkillController controller) => Set(controller, Real());

        /// <summary>실제 카탈로그의 assetKey로, ids에 해당하는 빈 프리팹을 가진 표를 컨트롤러에 붙인다. 만든 프리팹은 created에 넣는다</summary>
        public static void Attach(SkillController controller, IEnumerable<int> ids, ICollection<GameObject> created)
        {
            var catalog = new DefaultSkillDataProvider(new GameDataStore()).LoadAll().ToDictionary(w => w.id);
            var entries = new List<SkillAssetEntry>();
            foreach (int id in ids)
            {
                var prefab = new GameObject("SkillPrefab" + id);
                created.Add(prefab);
                entries.Add(new SkillAssetEntry { key = catalog[id].assetKey, prefab = prefab });
            }
            Set(controller, SkillAssetTable.Create(entries));
        }

        private static void Set(SkillController controller, SkillAssetTable table) =>
            typeof(SkillController).GetField("assets", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(controller, table);
    }
}
