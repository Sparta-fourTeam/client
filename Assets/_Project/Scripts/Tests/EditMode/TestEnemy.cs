using System.Collections.Generic;
using Game.Core;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using UnityEngine;

[assembly: Game.Tests.CleanUpTestEnemies]

namespace Game.Tests
{
    // 테스트용 Enemy. 위치는 Enemy(Transform)가 갖고 규칙은 EnemyModel이 가진다.
    // 만든 GameObject는 각 테스트가 끝날 때 CleanUpTestEnemies가 지운다
    internal static class TestEnemy
    {
        private static readonly List<GameObject> Created = new();

        public static Enemy Create(EnemyModel model, Vector2 position = default)
        {
            var go = new GameObject("TestEnemy");
            go.transform.position = position;
            var enemy = go.AddComponent<Enemy>();
            enemy.Attach(model, null);
            Created.Add(go);
            return enemy;
        }

        public static void DestroyAll()
        {
            foreach (var go in Created)
            {
                if (go != null)
                {
                    Object.DestroyImmediate(go);
                }
            }

            Created.Clear();
        }
    }

    [System.AttributeUsage(System.AttributeTargets.Assembly)]
    public sealed class CleanUpTestEnemiesAttribute : System.Attribute, ITestAction
    {
        public ActionTargets Targets => ActionTargets.Test;
        public void BeforeTest(ITest test) { }
        public void AfterTest(ITest test) => TestEnemy.DestroyAll();
    }
}
