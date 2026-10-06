using UnityEngine;

namespace Game.Core
{
    /// <summary>Plays authored VFX prefabs while the corresponding model statuses are active.</summary>
    public sealed class EnemyStatusEffects : MonoBehaviour
    {
        private EnemyModel model;
        private GameObject visual;
        private readonly ParticleSystem[] effects = new ParticleSystem[7];
        private readonly bool[] playing = new bool[7];
        private static readonly string[] Names = { "Freeze", "Burn", "Paralysis", "Stun", "Frostbite", "Slow", "Vulnerability" };

        public void Bind(EnemyModel target)
        {
            StopAll();
            model = target;
            if (visual != null) { return; }
            var prefab = Resources.Load<GameObject>("VFX/EnemyStatuses");
            if (prefab == null) { return; }
            var renderers = GetComponentsInChildren<SpriteRenderer>();
            var bounds = new Bounds(transform.position, Vector3.one * .6f);
            if (renderers.Length > 0)
            {
                bounds = renderers[0].bounds;
                foreach (var renderer in renderers) { bounds.Encapsulate(renderer.bounds); }
            }
            visual = Instantiate(prefab, transform);
            visual.transform.position = bounds.center;
            float size = Mathf.Max(.4f, Mathf.Max(bounds.size.x, bounds.size.y)) * 1.25f;
            var scale = transform.lossyScale;
            visual.transform.localScale = new Vector3(size / Mathf.Max(.001f, Mathf.Abs(scale.x)),
                size / Mathf.Max(.001f, Mathf.Abs(scale.y)), 1 / Mathf.Max(.001f, Mathf.Abs(scale.z)));
            for (int i = 0; i < Names.Length; i++)
            {
                var child = visual.transform.Find(Names[i]);
                effects[i] = child != null ? child.GetComponent<ParticleSystem>() : null;
            }
        }

        private void LateUpdate() => Sync();

        internal void Sync()
        {
            if (model == null || model.IsDead) { StopAll(); return; }
            Set(0, model.IsFrozen);
            Set(1, model.BurnRemaining > 0);
            Set(2, model.IsParalyzed);
            Set(3, model.IsStunned);
            Set(4, model.FrostbiteStacks > 0 && !model.IsFrozen);
            Set(5, model.MovementMultiplier < 1);
            Set(6, model.VulnerabilityRemaining > 0);
        }

        private void Set(int index, bool active)
        {
            if (playing[index] == active || effects[index] == null) { return; }
            playing[index] = active;
            if (active) { effects[index].Play(true); }
            else { effects[index].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); }
        }

        private void StopAll()
        {
            for (int i = 0; i < effects.Length; i++) { Set(i, false); }
        }

        private void OnDisable() => StopAll();
    }
}
