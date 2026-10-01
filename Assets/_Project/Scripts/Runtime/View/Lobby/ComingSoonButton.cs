using UnityEngine;
using UnityEngine.UI;

namespace Game.View
{
    /// <summary>누르면 미완성 안내를 띄운다. 공방·결계·냥냥닌자 탭처럼 아직 없는 기능의 버튼에 붙인다</summary>
    [RequireComponent(typeof(Button))]
    public sealed class ComingSoonButton : MonoBehaviour
    {
        [SerializeField] private ComingSoonToastView _toast;

        private void Awake()
        {
            GetComponent<Button>().onClick.AddListener(_toast.Show);
        }
    }
}
