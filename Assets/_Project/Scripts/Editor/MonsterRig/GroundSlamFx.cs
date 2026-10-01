using System.IO;
using UnityEditor;
using UnityEngine;

namespace Game.Editor.MonsterRig
{
    /// <summary>
    /// 땅 내려치기 이펙트 프리팹 생성: 바닥에 퍼지는 충격파 링 + 흙먼지 + 튀는 돌 부스러기.
    /// 텍스처(부드러운 원, 링)도 코드로 만든다. 한 번 재생 후 스스로 파괴된다.
    /// </summary>
    public static class GroundSlamFx
    {
        public const string PrefabPath = "Assets/_Project/Prefabs/Fx/Fx_GroundSlam.prefab";
        private const string ArtFolder = "Assets/_Project/Art/Fx";
        private const string Shader2D = "Universal Render Pipeline/2D/Sprite-Unlit-Default";
        private const int SortingOrder = 20;

        public static GameObject Build()
        {
            MonsterAnimationBaker.EnsureFolder(ArtFolder);
            MonsterAnimationBaker.EnsureFolder(Path.GetDirectoryName(PrefabPath)!.Replace('\\', '/'));
            var soft = Material("fx_soft", r => Mathf.Pow(Mathf.Clamp01(1f - r), 1.6f));
            var ring = Material("fx_ring", r => Mathf.Clamp01(1f - Mathf.Abs(r - 0.8f) / 0.14f) * (r < 1f ? 1f : 0f));

            var root = new GameObject("Fx_GroundSlam");
            try
            {
                // 흙먼지: 바닥에서 옆·위로 퍼지며 커지고 옅어짐
                var dust = root.AddComponent<ParticleSystem>();
                Setup(dust, soft, count: 18, life: (0.6f, 0.9f), speed: (0.3f, 0.9f), size: (0.3f, 0.55f),
                    color: new Color(0.55f, 0.48f, 0.4f, 0.9f), gravity: -0.15f);
                var shape = dust.shape;
                shape.shapeType = ParticleSystemShapeType.Cone;
                shape.angle = 75f;
                shape.radius = 0.25f;
                shape.rotation = new Vector3(-90f, 0f, 0f);
                var sizeOverLife = dust.sizeOverLifetime;
                sizeOverLife.enabled = true;
                sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.4f));
                var main = dust.main;
                main.stopAction = ParticleSystemStopAction.Destroy;

                // 충격파 링: 납작하게(원근) 빠르게 퍼지며 사라짐
                var ringGo = new GameObject("Ring");
                ringGo.transform.SetParent(root.transform, false);
                ringGo.transform.localScale = new Vector3(1f, 0.35f, 1f);
                var ringPs = ringGo.AddComponent<ParticleSystem>();
                Setup(ringPs, ring, count: 1, life: (0.35f, 0.35f), speed: (0f, 0f), size: (1f, 1f),
                    color: new Color(0.85f, 0.8f, 0.7f, 0.9f), gravity: 0f);
                var ringShape = ringPs.shape;
                ringShape.enabled = false;
                var ringSize = ringPs.sizeOverLifetime;
                ringSize.enabled = true;
                ringSize.size = new ParticleSystem.MinMaxCurve(1.6f, AnimationCurve.EaseInOut(0f, 0.2f, 1f, 1f));
                var ringMain = ringPs.main;
                ringMain.scalingMode = ParticleSystemScalingMode.Hierarchy;

                // 돌 부스러기: 위로 튀었다가 떨어짐
                var debrisGo = new GameObject("Debris");
                debrisGo.transform.SetParent(root.transform, false);
                var debris = debrisGo.AddComponent<ParticleSystem>();
                Setup(debris, soft, count: 10, life: (0.5f, 0.8f), speed: (1.2f, 2.2f), size: (0.04f, 0.08f),
                    color: new Color(0.42f, 0.38f, 0.33f, 1f), gravity: 5f);
                var debrisShape = debris.shape;
                debrisShape.shapeType = ParticleSystemShapeType.Cone;
                debrisShape.angle = 55f;
                debrisShape.radius = 0.15f;
                debrisShape.rotation = new Vector3(-90f, 0f, 0f);

                var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                return prefab;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        public const string SonicPrefabPath = "Assets/_Project/Prefabs/Fx/Fx_SonicWave.prefab";

        /// <summary>초음파: 입에서 아래(목표 쪽)로 납작한 파동 링 3개가 연달아 커지며 날아간다.</summary>
        public static GameObject BuildSonic()
        {
            MonsterAnimationBaker.EnsureFolder(ArtFolder);
            MonsterAnimationBaker.EnsureFolder(Path.GetDirectoryName(SonicPrefabPath)!.Replace(Path.DirectorySeparatorChar, '/'));
            var ring = Material("fx_ring", r => Mathf.Clamp01(1f - Mathf.Abs(r - 0.8f) / 0.14f) * (r < 1f ? 1f : 0f));

            var root = new GameObject("Fx_SonicWave");
            try
            {
                root.transform.localScale = new Vector3(1f, 0.5f, 1f); // 납작한 파동
                var ps = root.AddComponent<ParticleSystem>();
                Setup(ps, ring, count: 1, life: (0.45f, 0.45f), speed: (1.6f, 1.6f), size: (1f, 1f),
                    color: new Color(0.8f, 0.95f, 1f, 0.95f), gravity: 0f);
                var main = ps.main;
                main.startRotation = 0f;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                main.stopAction = ParticleSystemStopAction.Destroy;
                var emission = ps.emission;
                emission.SetBursts(new[]
                {
                    new ParticleSystem.Burst(0f, 1), new ParticleSystem.Burst(0.09f, 1), new ParticleSystem.Burst(0.18f, 1),
                });
                var shape = ps.shape;
                shape.shapeType = ParticleSystemShapeType.Cone;
                shape.angle = 0f;
                shape.radius = 0.01f;
                shape.rotation = new Vector3(90f, 0f, 0f); // 아래로
                var size = ps.sizeOverLifetime;
                size.enabled = true;
                size.size = new ParticleSystem.MinMaxCurve(0.9f, AnimationCurve.EaseInOut(0f, 0.25f, 1f, 1f));
                return PrefabUtility.SaveAsPrefabAsset(root, SonicPrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void Setup(ParticleSystem ps, Material material, int count, (float, float) life,
            (float, float) speed, (float, float) size, Color color, float gravity)
        {
            var main = ps.main;
            main.duration = 1f;
            main.loop = false;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life.Item1, life.Item2);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed.Item1, speed.Item2);
            main.startSize = new ParticleSystem.MinMaxCurve(size.Item1, size.Item2);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = color;
            main.gravityModifier = gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.4f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.sortingOrder = SortingOrder;
        }

        // 반지름(0..1) → 알파 함수로 64x64 텍스처를 만들고 그 텍스처를 쓰는 머티리얼을 만든다.
        private static Material Material(string name, System.Func<float, float> alpha)
        {
            string texPath = $"{ArtFolder}/{name}.png";
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float r = new Vector2(x + 0.5f - size / 2f, y + 0.5f - size / 2f).magnitude / (size / 2f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha(r)));
                }
            }

            File.WriteAllBytes(Path.GetFullPath(texPath), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(texPath, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(texPath);
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();

            string matPath = $"{ArtFolder}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find(Shader2D));
                AssetDatabase.CreateAsset(mat, matPath);
            }

            mat.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            EditorUtility.SetDirty(mat);
            return mat;
        }
    }
}
