using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Core
{
    /// <summary>Plays authored VFX prefabs while the corresponding model statuses are active.</summary>
    public sealed class EnemyStatusEffects : MonoBehaviour
    {
        private EnemyModel model;
        private GameObject visual;
        private SortingGroup sortingGroup;
        private EnemyDepthSort depthSort;
        private int appliedOrder = int.MinValue;
        private readonly ParticleSystem[] effects = new ParticleSystem[7];
        private readonly bool[] playing = new bool[7];
        private static readonly string[] Names = { "Freeze", "Burn", "Paralysis", "Stun", "Frostbite", "Slow", "Vulnerability" };

        public void Bind(EnemyModel target)
        {
            StopAll();
            model = target;
            var prefab = Resources.Load<GameObject>("VFX/EnemyStatuses");
            if (prefab == null) { return; }
            var body = GetComponentInChildren<Animator>();
            var renderers = body != null ? body.GetComponentsInChildren<SpriteRenderer>() : GetComponentsInChildren<SpriteRenderer>();
            var bounds = new Bounds(transform.position, Vector3.one * .6f);
            bool found = false;
            foreach (var renderer in renderers)
            {
                if (renderer.sprite == null || (visual != null && renderer.transform.IsChildOf(visual.transform))) { continue; }
                if (!found) { bounds = renderer.bounds; found = true; }
                else { bounds.Encapsulate(renderer.bounds); }
            }
            if (visual == null) { visual = Instantiate(prefab, transform); }
            sortingGroup = visual.GetComponent<SortingGroup>();
            appliedOrder = int.MinValue;
            visual.transform.position = bounds.center;
            float height = Mathf.Max(.4f, bounds.size.y) * 1.2f;
            float width = Mathf.Max(height * .6f, bounds.size.x * 1.2f);
            var scale = transform.lossyScale;
            visual.transform.localScale = new Vector3(width / Mathf.Max(.001f, Mathf.Abs(scale.x)),
                height / Mathf.Max(.001f, Mathf.Abs(scale.y)), 1 / Mathf.Max(.001f, Mathf.Abs(scale.z)));
            for (int i = 0; i < Names.Length; i++)
            {
                var child = visual.transform.Find(Names[i]);
                effects[i] = child != null ? child.GetComponent<ParticleSystem>() : null;
            }
        }

        private void LateUpdate() => Sync();

        internal void Sync()
        {
            // 몸 순서를 그대로 따라 몸 바로 앞에 그린다. 바뀔 때만 쓰고, 죽는 연출 중에도 몸을 따라간다
            if (sortingGroup != null)
            {
                if (depthSort == null) { depthSort = GetComponent<EnemyDepthSort>(); }
                int order = (depthSort != null ? depthSort.Order : EnemyDepthSort.OrderAt(transform.position.y)) + 1;
                if (order != appliedOrder) { sortingGroup.sortingOrder = order; appliedOrder = order; }
            }
            if (model == null || model.IsDead) { StopAll(); return; }
            Set(0, model.IsFrozen);
            Set(1, model.BurnRemaining > 0);
            Set(2, model.IsParalyzed);
            Set(3, model.IsStunned);
            Set(4, model.FrostbiteStacks > 0 && !model.IsFrozen);
            Set(5, !model.IsFrozen && model.MovementMultiplier < 1);
            Set(6, model.VulnerabilityRemaining > 0);
        }

        private void Set(int index, bool active)
        {
            if (playing[index] == active || effects[index] == null) { return; }
            playing[index] = active;
            if (active) { effects[index].Play(true); }
            else { effects[index].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); }
        }

        public void Clear() => StopAll();

        private void StopAll()
        {
            for (int i = 0; i < effects.Length; i++) { Set(i, false); }
        }

        private void OnDisable() => StopAll();
    }
}
