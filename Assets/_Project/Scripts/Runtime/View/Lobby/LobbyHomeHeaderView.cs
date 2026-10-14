using UnityEngine;

namespace Game.View
{
    /// <summary>홈의 프로필·메뉴 표시. 다른 탭의 재화 표시는 각 화면이 소유한다.</summary>
    public sealed class LobbyHomeHeaderView : MonoBehaviour
    {
        [SerializeField] private GameObject _content;

        public void SetVisible(bool visible) => _content.SetActive(visible);
    }
}
