using UnityEngine;
using UnityEngine.UI;

namespace Game.View
{
    /// <summary>하단 메뉴. 캐릭터·스킬 탭은 해당 화면을 열고, 홈은 모두 닫고, 상점·뽑기는 미완성 안내를 띄운다</summary>
    public sealed class LobbyNavView : MonoBehaviour
    {
        [SerializeField] private Button _shopTab, _characterTab, _homeTab, _skillTab, _gachaTab;
        [SerializeField] private GameObject _characterSelected, _homeSelected, _skillSelected;
        [SerializeField] private CharacterScreenView _characterScreen;
        [SerializeField] private SkillScreenView _skillScreen;
        [SerializeField] private ComingSoonToastView _toast;

        private enum Tab
        {
            Home,
            Character,
            Skill,
        }

        private void Awake()
        {
            _shopTab.onClick.AddListener(_toast.Show);
            _gachaTab.onClick.AddListener(_toast.Show);
            _homeTab.onClick.AddListener(() => Select(Tab.Home));
            _characterTab.onClick.AddListener(() => Select(Tab.Character));
            _skillTab.onClick.AddListener(() => Select(Tab.Skill));
        }

        // 화면들의 Awake(패널 끄기)가 끝난 뒤에 처음 상태를 정한다
        private void Start()
        {
            Select(Tab.Home);
        }

        // 화면은 한 번에 하나만 연다
        private void Select(Tab tab)
        {
            _characterScreen.SetVisible(tab == Tab.Character);
            _skillScreen.SetVisible(tab == Tab.Skill);
            _homeSelected.SetActive(tab == Tab.Home);
            _characterSelected.SetActive(tab == Tab.Character);
            _skillSelected.SetActive(tab == Tab.Skill);
        }
    }
}
