using UnityEngine;

namespace Game.Core
{
    /// <summary>Per-projectile visual state, restored on each pooled spawn.</summary>
    public sealed class ProjectileVisual : System.IDisposable
    {
        private readonly GameObject owner;
        public ProjectileVisual(GameObject owner) { this.owner = owner; }

        public static Vector3 MainScale(Vector3 original, WeaponForm form, float multiplier) =>
            (form == WeaponForm.Enbakutsu ? Vector3.Scale(original, new Vector3(1.5f, .65f, 1)) : original) * multiplier;

        private LineRenderer triangleOutline;
        private Material triangleMaterial;
        private SpriteRenderer originalSprite;
        private bool originalSpriteEnabled;
        private Color originalSpriteColor;

        public void SetForm(WeaponForm form)
        {
            if (originalSprite == null)
            {
                originalSprite = owner.GetComponent<SpriteRenderer>();
                if (originalSprite != null) { originalSpriteEnabled = originalSprite.enabled; originalSpriteColor = originalSprite.color; }
            }
            bool triangle = form == WeaponForm.TriangleIce;
            if (triangle && triangleOutline == null)
            {
                triangleOutline = owner.AddComponent<LineRenderer>();
                var shader = Shader.Find("Sprites/Default");
                if (shader != null) { triangleMaterial = new Material(shader); triangleOutline.sharedMaterial = triangleMaterial; }
                triangleOutline.useWorldSpace = false; triangleOutline.loop = true; triangleOutline.positionCount = 3;
                triangleOutline.startWidth = triangleOutline.endWidth = .12f;
                triangleOutline.startColor = triangleOutline.endColor = new Color(.35f, .85f, 1);
                triangleOutline.sortingOrder = originalSprite != null ? originalSprite.sortingOrder : 10;
                triangleOutline.SetPosition(0, new Vector3(0, 1.2f, 0));
                triangleOutline.SetPosition(1, new Vector3(-.8f, -.7f, 0));
                triangleOutline.SetPosition(2, new Vector3(.8f, -.7f, 0));
            }
            if (originalSprite != null)
            {
                originalSprite.enabled = !triangle && originalSpriteEnabled;
                originalSprite.color = form == WeaponForm.FireLog ? new Color(1, .35f, .05f) : originalSpriteColor;
            }
            if (triangleOutline != null) { triangleOutline.enabled = triangle; }
        }

        public void Dispose()
        {
            if (triangleMaterial != null)
            {
                if (Application.isPlaying) { Object.Destroy(triangleMaterial); }
                else { Object.DestroyImmediate(triangleMaterial); }
            }
        }

    }
}
