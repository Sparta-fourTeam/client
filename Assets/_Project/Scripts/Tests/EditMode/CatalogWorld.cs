using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Tests
{
    /// <summary>출시 카탈로그와 플레이어 프리팹의 실제 연결로 스킬을 만들어 보는 테스트 환경.
    /// 게임에서 WeaponController가 하는 일(스킬별 프리팹 연결, 자식 시전기 연결)을 같은 방식으로 한다.</summary>
    internal sealed class CatalogWorld : IDisposable
    {
        private const string PlayerPrefab = "Assets/_Project/Prefabs/Stage/Player_Animated.prefab";

        public sealed class Targets : IEnemyTargetProvider
        {
            public readonly List<IEnemyTarget> All = new List<IEnemyTarget>();
            public int GetNearest(Vector2 from, int count, List<IEnemyTarget> results)
            {
                results.Clear();
                results.AddRange(All.OrderBy(t => (t.Position - from).sqrMagnitude).Take(count));
                return results.Count;
            }
        }

        public Targets Provider { get; } = new Targets();
        public Dictionary<int, WeaponData> Data { get; }
        private readonly Dictionary<int, GameObject> prefabs = new Dictionary<int, GameObject>();
        private readonly ChildSkillCaster children;
        private readonly List<GameObject> owners = new List<GameObject>();
        private readonly List<SkillCaster> casters = new List<SkillCaster>();

        public CatalogWorld(Action<Dictionary<int, WeaponData>> tweak = null)
        {
            Data = new DefaultWeaponDataProvider().LoadAll().ToDictionary(w => w.id);
            tweak?.Invoke(Data);
            var player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefab);
            var entries = new SerializedObject(player.GetComponentInChildren<WeaponController>(true)).FindProperty("prefabEntries");
            for (int i = 0; i < entries.arraySize; i++)
            {
                var entry = entries.GetArrayElementAtIndex(i);
                prefabs[entry.FindPropertyRelative("id").intValue] = (GameObject)entry.FindPropertyRelative("prefab").objectReferenceValue;
            }
            children = new ChildSkillCaster(id => Build(id));
        }

        private SkillCaster Build(int id)
        {
            var owner = new GameObject("CatalogWorldOwner" + id);
            owners.Add(owner);
            var caster = WeaponFactory.Create(Data[id], prefabs[id], owner.transform, Provider);
            caster.UseChildCaster(children);
            casters.Add(caster);
            return caster;
        }

        /// <summary>플레이어가 얻은 스킬처럼 시전기를 만든다 (자식 시전기가 연결된다)</summary>
        public SkillCaster Create(int id) => Build(id);

        /// <summary>카드 ID로 강화한다 (조건과 무관하게 적용)</summary>
        public void Take(SkillCaster caster, params string[] cardIds)
        {
            foreach (var id in cardIds)
            {
                var option = caster.Data.upgrades.Find(c => c.id == id);
                if (option == null || !caster.LevelUp(option)) { throw new InvalidOperationException($"카드 {id}를 적용하지 못했습니다."); }
            }
        }

        public HitRecorder AddEnemy(float x, float y)
        {
            var enemy = new HitRecorder { Position = new Vector2(x, y) };
            Provider.All.Add(enemy);
            return enemy;
        }

        /// <summary>적중 반응을 발생시킨다 (투사체가 적에 맞은 것과 같다)</summary>
        public static void Hit(AttackReactions reactions, IEnemyTarget target) =>
            reactions.Raise(AttackEvent.Hit, new AttackContext(target.Position, Vector3.up, target));

        public static List<Projectile> Active() => Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None)
            .Where(p => p.gameObject.scene.IsValid() && p.gameObject.activeInHierarchy && p.name.Contains("Clone")).ToList();

        public static AttackReactions ReactionsOf(Projectile projectile) =>
            (AttackReactions)typeof(Projectile).GetField("reactions", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(projectile);

        public static Vector3 Direction(Projectile projectile) =>
            (Vector3)typeof(Projectile).GetField("direction", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(projectile);

        public void Dispose()
        {
            children.Dispose();
            foreach (var caster in casters) { caster.Dispose(); }
            foreach (var projectile in Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None)) { Object.DestroyImmediate(projectile.gameObject); }
            foreach (var owner in owners) { if (owner != null) { Object.DestroyImmediate(owner); } }
        }
    }
}
