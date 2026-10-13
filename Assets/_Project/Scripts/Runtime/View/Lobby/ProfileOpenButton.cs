using UnityEngine;
using UnityEngine.UI;

namespace Game.View
{
    [RequireComponent(typeof(Button))]
    public sealed class ProfileOpenButton : MonoBehaviour, IUiBindable
    {
        private ProfilePopupView _popup;
        public void BindUi(IUiRegistry registry) => _popup = registry.Get<ProfilePopupView>();
        private void Awake() => GetComponent<Button>().onClick.AddListener(() => _popup.Open());
    }
}
