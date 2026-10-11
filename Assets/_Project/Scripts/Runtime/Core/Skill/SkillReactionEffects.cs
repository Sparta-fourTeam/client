using UnityEngine;

namespace Game.Core
{
    public static class SkillReactionEffects
    {
        public static int Explode(IEnemyTargetProvider provider, Vector2 position, float radius, float damage, string visualEffect = null)
        {
            int hits = AreaDamage.Apply(provider, position, radius, damage);
            if (Application.isPlaying && provider != null && radius > 0 && damage > 0
                && !float.IsNaN(radius) && !float.IsInfinity(radius)
                && !float.IsNaN(damage) && !float.IsInfinity(damage))
            {
                Spawn(visualEffect ?? ExplosionVisual(Game.Core.Combat.DamageAttribution.CurrentSource.Element), position, radius);
            }
            return hits;
        }

        internal static string ExplosionVisual(Element element) => element switch
        {
            Element.Fire => "Explosion",
            Element.Ice => "IceExplosion",
            Element.Lightning => "LightningExplosion",
            Element.Earth => "EarthExplosion",
            Element.Wind => "WindExplosion",
            _ => "NeutralExplosion"
        };

        public static void Lightning(Vector2 position)
        {
            if (Application.isPlaying) { Spawn("AdditionalLightning", position, 1); }
        }

        internal static GameObject Spawn(string effect, Vector2 position, float scale)
        {
            var prefab = Resources.Load<GameObject>("VFX/" + effect);
            if (prefab == null) { return null; }
            var visual = Object.Instantiate(prefab, position, Quaternion.identity);
            visual.transform.localScale = Vector3.one * scale;
            return visual;
        }
    }
}
