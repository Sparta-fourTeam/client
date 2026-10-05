using UnityEngine;

namespace Game.Core
{
    /// <summary>Displays authored form artwork and restores the base sprite on pooled reuse.</summary>
    public sealed class ProjectileVisual : System.IDisposable
    {
        [System.Serializable]
        public struct FormSprite
        {
            public WeaponForm form;
            public Sprite sprite;
            public GameObject visualRoot;
            public bool hideBaseSprite;
        }

        private readonly SpriteRenderer renderer;
        private readonly Sprite original;
        private readonly FormSprite[] forms;
        private readonly GameObject baseVisual;
        private readonly bool originalEnabled;
        private readonly bool originalActive;

        public ProjectileVisual(GameObject owner, FormSprite[] forms = null, GameObject baseVisual = null)
        {
            renderer = owner.GetComponentInChildren<SpriteRenderer>(true);
            original = renderer != null ? renderer.sprite : null;
            this.forms = forms;
            this.baseVisual = baseVisual;
            originalEnabled = renderer != null && renderer.enabled;
            originalActive = baseVisual != null && baseVisual.activeSelf;
        }

        public static Vector3 MainScale(Vector3 original, WeaponForm form, float multiplier) =>
            (form == WeaponForm.Enbakutsu ? Vector3.Scale(original, new Vector3(1.5f, .65f, 1)) : original) * multiplier;

        public void SetForm(WeaponForm form)
        {
            if (renderer != null) { renderer.sprite = original; renderer.enabled = originalEnabled; }
            if (baseVisual != null) { baseVisual.SetActive(originalActive); }
            if (forms == null) { return; }
            foreach (var entry in forms)
            {
                bool selected = entry.form == form;
                if (entry.visualRoot != null) { entry.visualRoot.SetActive(selected); }
                if (!selected) { continue; }
                if (entry.sprite != null && renderer != null) { renderer.sprite = entry.sprite; }
                if (entry.hideBaseSprite && (entry.sprite != null || entry.visualRoot != null))
                {
                    if (renderer != null) { renderer.enabled = false; }
                    if (baseVisual != null) { baseVisual.SetActive(false); }
                }
            }
        }

        public void Dispose() { }
    }
}
