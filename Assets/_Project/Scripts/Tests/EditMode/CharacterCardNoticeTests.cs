using System.Collections.Generic;
using System.Linq;
using Game.View;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>로비 캐릭터 화면의 카드 상태와 "미완성입니다." 안내가 어긋나지 않게 지킨다.
    /// 아직 없는 기능의 버튼에는 안내를 붙이지만, 이미 배치 중인 캐릭터는 완성된 상태라 안내가 뜨면 안 된다</summary>
    public class CharacterCardNoticeTests
    {
        private const string LobbyScene = "Assets/_Project/Scenes/Lobby.unity";

        private static List<Transform> DeployedCards(UnityEngine.SceneManagement.Scene scene) =>
            scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Where(t => t.name.StartsWith("CharacterCard") && t.Find("DeployedLabel") != null)
                .Where(t => t.Find("DeployedLabel").gameObject.activeSelf)
                .ToList();

        [Test(Description = "배치중 라벨이 켜진 카드는 눌러도 미완성 안내가 뜨지 않는다")]
        public void DeployedCard_DoesNotShowComingSoonNotice()
        {
            var scene = EditorSceneManager.OpenScene(LobbyScene, OpenSceneMode.Additive);
            try
            {
                var deployed = DeployedCards(scene);
                Assert.IsNotEmpty(deployed, "배치중 카드를 찾지 못했다 (카드 이름이나 라벨 구조가 바뀌었는지 확인)");

                foreach (var card in deployed)
                {
                    Assert.IsNull(card.GetComponent<ComingSoonButton>(), $"{card.name}은 배치 중인데 미완성 안내가 붙어 있다");
                }
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
