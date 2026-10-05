using Game.Core;
using UnityEngine;
using VContainer;

namespace Game.Sandbox
{
    /// <summary>샌드박스 씬의 진입점. 스킬 카탈로그가 준비되면(WeaponController.Start 이후) 세션과 화면 패널을 만들고, 패널은 OnGUI(IMGUI)로 그린다.</summary>
    public sealed class SandboxController : MonoBehaviour
    {
        [SerializeField] private Font font;

        private WeaponController weapons;
        private IWeaponDataProvider provider;
        private SandboxProgression progression;
        private SandboxEnemyField enemies;
        private SandboxPanel panel;

        public SkillSandboxSession Session { get; private set; }
        public SandboxEnemyField Enemies => enemies;

        [Inject]
        public void Construct(WeaponController weapons, IWeaponDataProvider provider, SandboxProgression progression, SandboxEnemyField enemies)
        {
            this.weapons = weapons;
            this.provider = provider;
            this.progression = progression;
            this.enemies = enemies;
        }

        private void Update()
        {
            if (Session != null || weapons == null || !weapons.IsReady) { return; }
            Session = new SkillSandboxSession(weapons, provider, progression);
            panel = new SandboxPanel(Session, enemies, font);
        }

        private void OnGUI() => panel?.OnGUI();

        private void OnDestroy()
        {
            panel?.Dispose();
            Time.timeScale = 1f;
        }
    }
}
