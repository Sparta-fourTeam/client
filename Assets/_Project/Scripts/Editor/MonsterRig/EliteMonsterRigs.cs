using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>Existing monster skeletons and clips, with independently layered elite artwork.</summary>
    public static class EliteMonsterRigs
    {
        public static readonly string[] Names = { "Slime", "Golem", "BrainSpider", "ArmoredCrab", "Bat", "EyeJelly", "Ghost", "Oni", "Skeleton", "Spider" };

        public static string WritePreview(string name, string path)
        {
            EliteRigArt.WritePreview(name, Path.GetFullPath(path)); return path;
        }

        public static string RefreshSkin(string name)
        {
            if (!Names.Contains(name) || EditorApplication.isPlayingOrWillChangePlaymode) { throw new InvalidOperationException("Choose an elite rig in Edit mode."); }
            EliteRigSkin.Apply($"Assets/_Project/PSD/{name}Elite.psd", EliteRigSkin.Read($"Assets/_Project/PSD/{(name == "Oni" ? "Horned" : name)}.psd"), EliteRigArt.Prepare(name));
            return name + " skin refreshed";
        }

        public static string Build(string name)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { throw new InvalidOperationException("Stop Play mode before saving PSD skinning data."); }
            if (!Names.Contains(name)) { throw new ArgumentException(name); }
            string baseName = name == "Oni" ? "Horned" : name;
            string basePsd = $"Assets/_Project/PSD/{baseName}.psd", psd = $"Assets/_Project/PSD/{name}Elite.psd";
            string prefab = $"Assets/_Project/Prefabs/Monsters/{name}Elite_Animated.prefab";
            if (File.Exists(psd) || File.Exists(prefab)) { throw new InvalidOperationException("Elite assets already exist; remove only the intended generated assets through Unity before rebuilding"); }
            var source = EliteRigSkin.Read(basePsd); var art = EliteRigArt.Prepare(name);
            PsdWriter.Write(Path.GetFullPath(psd), art.Width, art.Height, art.Layers);
            AssetDatabase.ImportAsset(psd, ImportAssetOptions.ForceSynchronousImport);
            var origins = EliteRigSkin.Apply(psd, source, art);
            var sprites = AssetDatabase.LoadAllAssetsAtPath(psd).OfType<Sprite>().ToDictionary(s => s.name);
            string template = name == "Slime" ? "SlimeVariant" : name == "Bat" ? "BatVariant" : name;
            var root = PrefabUtility.LoadPrefabContents($"Assets/_Project/Prefabs/Stage/{template}.prefab");
            try
            {
                // Unpack only this editable clone so the ordinary monster prefab is never changed.
                foreach (var instance in root.GetComponentsInChildren<Transform>(true).Where(t => PrefabUtility.IsOutermostPrefabInstanceRoot(t.gameObject)).ToArray())
                { PrefabUtility.UnpackPrefabInstance(instance.gameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction); }
                int changed = 0;
                foreach (var renderer in root.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    if (renderer.sprite == null || AssetDatabase.GetAssetPath(renderer.sprite) != basePsd) { continue; }
                    var old = renderer.sprite; var replacement = sprites[old.name];
                    Vector2 delta = (origins[old.name] + replacement.pivot - source.Origins[old.name] - old.pivot) / source.Ppu;
                    renderer.transform.localPosition += (Vector3)delta; renderer.sprite = replacement;
                    renderer.gameObject.name = old.name; changed++;
                }
                if (changed != art.Layers.Count) { throw new InvalidOperationException($"Layer replacement mismatch: {changed}/{art.Layers.Count}"); }
                root.name = name + "Elite_Animated";
                PrefabUtility.SaveAsPrefabAsset(root, prefab);
                AssetDatabase.SaveAssets();
                return $"{name}: PSD {art.Width}x{art.Height}, {changed} layers; original skeleton, Move/Attack/Die controller and effects retained.";
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
