using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Tests
{
    /// <summary>로비 캐릭터 화면의 카드 목록이 지금 구현된 캐릭터와 일치하는지 지킨다.
    /// 캐릭터를 추가하면 ImplementedCharacterCount를 함께 올려야 하고, 그때 카드도 같이 늘려야 한다는 신호가 된다</summary>
    public class CharacterCardListTests
    {
        private const string LobbyScene = "Assets/_Project/Scenes/Lobby.unity";

        /// <summary>지금 구현된 캐릭터 수. 플레이어 프리팹이 하나뿐이고 IGrowthCatalog.Character도 한 명이다</summary>
        private const int ImplementedCharacterCount = 1;

        private static Transform[] Cards(Scene scene) =>
            scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Where(t => t.name.StartsWith("CharacterCard") && t.parent != null && t.parent.name == "Content")
                .ToArray();

        [Test(Description = "캐릭터 카드 수가 구현된 캐릭터 수와 같다 (구현되지 않은 캐릭터의 카드를 보여 주지 않는다)")]
        public void CardCount_MatchesImplementedCharacters()
        {
            var scene = EditorSceneManager.OpenScene(LobbyScene, OpenSceneMode.Additive);
            try
            {
                var cards = Cards(scene);

                Assert.AreEqual(ImplementedCharacterCount, cards.Length,
                    $"카드: {string.Join(", ", cards.Select(c => c.name))}");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test(Description = "구현된 캐릭터의 카드는 배치 중으로 표시된다")]
        public void ImplementedCharacterCard_IsShownAsDeployed()
        {
            var scene = EditorSceneManager.OpenScene(LobbyScene, OpenSceneMode.Additive);
            try
            {
                var card = Cards(scene).Single();

                Assert.IsTrue(card.Find("DeployedLabel").gameObject.activeSelf);
                Assert.IsFalse(card.Find("Unowned").gameObject.activeSelf);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
