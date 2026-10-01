using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Game.Editor.MonsterRig
{
    /// <summary>
    /// 클립 안에서 움직이는 이펙트 스프라이트. 평소엔 투명하게 두고, 해당 클립의 커브로만 보이게 한다.
    /// 애니메이션 이벤트/파티클과 달리 Animation 창 미리보기에서도 보이고 런타임 생성 비용이 없다.
    /// </summary>
    internal static class ClipFx
    {
        public const string Ring = "Assets/_Project/Art/Fx/fx_ring.png";
        public const string Soft = "Assets/_Project/Art/Fx/fx_soft.png";

        internal sealed class Track
        {
            public string Name;
            public string Sprite;   // Ring / Soft
            public int Order = 10;
            public float Rotation;  // 고정 회전(도)
            public float Spin;      // 초당 회전(도). 0이 아니면 회전 커브를 넣는다
            public (float t, Vector2 pos, Vector2 scale, Color color)[] Keys; // 위치는 이펙트 루트 기준(유닛)
        }

        // Animator가 붙은 Body 아래에 루트 + 스프라이트들을 첫 키 상태(보통 투명)로 둔다.
        public static void CreateObjects(GameObject body, string root, Vector2 offset, IEnumerable<Track> tracks)
        {
            var parent = new GameObject(root);
            parent.transform.SetParent(body.transform, false);
            parent.transform.localPosition = offset;
            foreach (var track in tracks)
            {
                var go = new GameObject(track.Name);
                go.transform.SetParent(parent.transform, false);
                var k = track.Keys[0];
                go.transform.localPosition = k.pos;
                go.transform.localScale = new Vector3(k.scale.x, k.scale.y, 1f);
                go.transform.localRotation = Quaternion.Euler(0f, 0f, track.Rotation);
                var r = go.AddComponent<SpriteRenderer>();
                r.sprite = Sprite(track.Sprite);
                r.color = k.color;
                r.sortingOrder = track.Order;
            }
        }

        // 위치(x, y) / 크기(x, y, z) / 색(rgba)을 모두 넣는다. 일부 성분만 넣으면 나머지가 0으로 샘플링된다.
        public static void AddCurves(AnimationClip clip, string root, IEnumerable<Track> tracks)
        {
            foreach (var track in tracks)
            {
                string path = $"{root}/{track.Name}";
                var keys = track.Keys;
                Set(clip, path, typeof(Transform), "m_LocalPosition.x", keys.Select(k => (k.t, k.pos.x)));
                Set(clip, path, typeof(Transform), "m_LocalPosition.y", keys.Select(k => (k.t, k.pos.y)));
                Set(clip, path, typeof(Transform), "m_LocalScale.x", keys.Select(k => (k.t, k.scale.x)));
                Set(clip, path, typeof(Transform), "m_LocalScale.y", keys.Select(k => (k.t, k.scale.y)));
                Set(clip, path, typeof(Transform), "m_LocalScale.z", keys.Select(k => (k.t, 1f)));
                if (track.Spin != 0f)
                {
                    var spin = new AnimationCurve(keys.Select(k => new Keyframe(k.t, track.Rotation + track.Spin * k.t, track.Spin, track.Spin)).ToArray());
                    AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), "localEulerAnglesRaw.z"), spin);
                }
                for (int c = 0; c < 4; c++)
                {
                    int ch = c;
                    Set(clip, path, typeof(SpriteRenderer), "m_Color." + "rgba"[ch], keys.Select(k => (k.t, k.color[ch])));
                }
            }

            EditorUtility.SetDirty(clip);
        }

        private static void Set(AnimationClip clip, string path, System.Type type, string prop, IEnumerable<(float t, float v)> keys)
        {
            var curve = new AnimationCurve(keys.Select(k => new Keyframe(k.t, k.v)).ToArray());
            for (int i = 0; i < curve.length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
                AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
            }

            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, type, prop), curve);
        }

        // fx 텍스처(64px)를 지름 1유닛 스프라이트로 쓴다. 없으면 이펙트 텍스처부터 만든다.
        public static Sprite Sprite(string path)
        {
            if (!File.Exists(Path.GetFullPath(path)))
            {
                GroundSlamFx.Build();
            }

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer.textureType != TextureImporterType.Sprite || !Mathf.Approximately(importer.spritePixelsPerUnit, 64f))
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 64f;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        public static Color Alpha(Color c, float a) => new(c.r, c.g, c.b, a);

        /// <summary>코드로 그린 64x64 텍스처를 스프라이트로 저장 (u, v: -1..1). 이미 있으면 그대로 쓴다.</summary>
        public static string Generated(string name, System.Func<float, float, Color> paint)
        {
            string path = $"Assets/_Project/Art/Fx/{name}.png";
            if (File.Exists(Path.GetFullPath(path)))
            {
                return path;
            }

            MonsterAnimationBaker.EnsureFolder("Assets/_Project/Art/Fx");
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    tex.SetPixel(x, y, paint((x + 0.5f) / size * 2f - 1f, (y + 0.5f) / size * 2f - 1f));
                }
            }

            File.WriteAllBytes(Path.GetFullPath(path), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
            return path;
        }
    }
}
