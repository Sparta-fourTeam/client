using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    // Editor-only adapter: reuse installed PSD/Sprite Editor providers without changing assembly references.
    internal static class EliteRigSkin
    {
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        internal static object Get(object value, string name) => value.GetType().GetField(name, Flags)?.GetValue(value) ?? value.GetType().GetProperty(name, Flags)?.GetValue(value);
        private static void Set(object value, string name, object data)
        {
            var field = value.GetType().GetField(name, Flags);
            if (field != null) { field.SetValue(value, data); } else { value.GetType().GetProperty(name, Flags).SetValue(value, data); }
        }
        private static object Call(object target, string name, params object[] args) => target.GetType().GetMethods(Flags)
            .First(m => (m.Name == name || m.Name.EndsWith("." + name)) && m.GetParameters().Length == args.Length).Invoke(target, args);
        private static Type TypeOf(string name) => UnityEngine.Assemblies.CurrentAssemblies.GetLoadedAssemblies().Select(a => a.GetType(name)).FirstOrDefault(t => t != null)
            ?? throw new InvalidOperationException("Missing 2D package API: " + name);
        private static object Provider(object importer, string name)
        {
            var contract = TypeOf("UnityEditor.U2D.Sprites.ISpriteEditorDataProvider");
            return contract.GetMethod("GetDataProvider").MakeGenericMethod(TypeOf(name)).Invoke(importer, null);
        }

        internal sealed class Source
        {
            public object Character, Bones, Mesh;
            public object[] Rects;
            public Dictionary<string, object> Parts;
            public Dictionary<string, Vector2> Origins;
            public float Ppu;
        }

        internal static Source Read(string path)
        {
            var importer = AssetImporter.GetAtPath(path); Call(importer, "InitSpriteEditorDataProvider");
            var characterProvider = Provider(importer, "UnityEditor.U2D.Animation.ICharacterDataProvider");
            var character = Call(characterProvider, "GetCharacterData");
            var rects = ((Array)Call(importer, "GetSpriteRects")).Cast<object>().ToArray();
            var parts = ((Array)Get(character, "parts")).Cast<object>().ToArray();
            var source = new Source { Character = character, Rects = rects, Bones = Provider(importer, "UnityEditor.U2D.Sprites.ISpriteBoneDataProvider"), Mesh = Provider(importer, "UnityEditor.U2D.Sprites.ISpriteMeshDataProvider"), Parts = new(), Origins = new(), Ppu = (float)Get(importer, "spritePixelsPerUnit") };
            foreach (var rect in rects)
            {
                string id = Get(rect, "spriteID").ToString(), name = (string)Get(rect, "name");
                var part = parts.First(p => (string)Get(p, "spriteId") == id); source.Parts[name] = part; source.Origins[name] = ((RectInt)Get(part, "spritePosition")).position;
            }
            return source;
        }

        internal static Dictionary<string, Vector2> Apply(string path, Source source, EliteRigArt.Document art)
        {
            var psdType = TypeOf("UnityEditor.U2D.PSD.PSDImporter");
            if (AssetImporter.GetAtPath(path).GetType() != psdType)
            {
                typeof(AssetDatabase).GetMethods(BindingFlags.Static | BindingFlags.Public)
                    .First(m => m.Name == "SetImporterOverride" && m.IsGenericMethodDefinition)
                    .MakeGenericMethod(psdType).Invoke(null, new object[] { path });
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            }
            var importer = AssetImporter.GetAtPath(path);
            Set(importer, "textureType", TextureImporterType.Sprite); Set(importer, "spriteImportMode", SpriteImportMode.Multiple);
            Set(importer, "useMosaicMode", true); Set(importer, "useCharacterMode", true); Set(importer, "spritePixelsPerUnit", source.Ppu);
            Set(importer, "filterMode", FilterMode.Bilinear); Set(importer, "mipmapEnabled", false);
            importer.SaveAndReimport(); importer = AssetImporter.GetAtPath(path); Call(importer, "InitSpriteEditorDataProvider");
            var characterProvider = Provider(importer, "UnityEditor.U2D.Animation.ICharacterDataProvider");
            var bones = Provider(importer, "UnityEditor.U2D.Sprites.ISpriteBoneDataProvider"); var mesh = Provider(importer, "UnityEditor.U2D.Sprites.ISpriteMeshDataProvider");
            var character = Call(characterProvider, "GetCharacterData"); var parts = (Array)Get(character, "parts");
            var rects = ((Array)Call(importer, "GetSpriteRects")).Cast<object>().ToArray(); var origins = new Dictionary<string, Vector2>();
            Set(character, "bones", Get(source.Character, "bones")); Set(character, "pivot", Get(source.Character, "pivot")); Set(character, "dimension", Get(source.Character, "dimension"));
            for (int p = 0; p < parts.Length; p++)
            {
                var part = parts.GetValue(p); var rect = rects.First(r => Get(r, "spriteID").ToString() == (string)Get(part, "spriteId"));
                string name = (string)Get(rect, "name"); var oldRect = source.Rects.First(r => (string)Get(r, "name") == name);
                var oldId = Get(oldRect, "spriteID"); var id = Get(rect, "spriteID");
                Vector2 origin = ((RectInt)Get(part, "spritePosition")).position; origins[name] = origin;
                Set(part, "bones", Get(source.Parts[name], "bones")); Set(part, "order", Get(source.Parts[name], "order")); parts.SetValue(part, p);
                var oldBones = (System.Collections.IList)Call(source.Bones, "GetBones", oldId); var newBones = (System.Collections.IList)Activator.CreateInstance(oldBones.GetType());
                foreach (var oldBone in oldBones)
                {
                    // Sprite-local root positions change when the new layer is trimmed to a different rectangle.
                    object bone = oldBone;
                    if ((int)Get(bone, "parentId") == -1) { Vector3 position = (Vector3)Get(bone, "position"); position += (Vector3)(source.Origins[name] - origin); Set(bone, "position", position); }
                    newBones.Add(bone);
                }
                Call(bones, "SetBones", id, newBones);
                var oldVertices = ((Array)Call(source.Mesh, "GetVertices", oldId)).Cast<object>().ToArray();
                var layer = art.Layers.First(l => l.Name == name);
                var regions = name == "golem" ? GolemRegions(art, layer) : null;
                int RigidBone(Vector2 point) => regions[Mathf.Clamp((int)point.y, 0, layer.Rect.height - 1) * layer.Rect.width + Mathf.Clamp((int)point.x, 0, layer.Rect.width - 1)];
                Grid(layer, name == "golem" ? RigidBone : null, out var positions, out var rigidWeights, out var indices, out var edges);
                var vertexType = TypeOf("UnityEditor.U2D.Sprites.Vertex2DMetaData"); var vertices = Array.CreateInstance(vertexType, positions.Count);
                for (int v = 0; v < positions.Count; v++)
                {
                    var vertex = Activator.CreateInstance(vertexType); Set(vertex, "position", positions[v]);
                    Set(vertex, "boneWeight", rigidWeights[v] >= 0 ? new BoneWeight { boneIndex0 = rigidWeights[v], weight0 = 1 } : SampleWeights(oldVertices, origin + positions[v] - source.Origins[name])); vertices.SetValue(vertex, v);
                }
                Call(mesh, "SetVertices", id, vertices); Call(mesh, "SetIndices", id, indices); Call(mesh, "SetEdges", id, edges);
            }
            Set(character, "parts", parts); Call(characterProvider, "SetCharacterData", character); Call(importer, "Apply");
            importer = AssetImporter.GetAtPath(path); importer.SaveAndReimport();
            return origins;
        }

        private static BoneWeight SampleWeights(object[] vertices, Vector2 point)
        {
            var nearest = vertices.Select(v => (vertex: v, distance: Vector2.SqrMagnitude((Vector2)Get(v, "position") - point))).OrderBy(v => v.distance).Take(3).ToArray();
            var weights = new Dictionary<int, float>();
            foreach (var sample in nearest)
            {
                var bw = (BoneWeight)Get(sample.vertex, "boneWeight"); float factor = 1f / Math.Max(.01f, sample.distance);
                void Add(int bone, float weight) { if (weight > 0) { weights[bone] = weights.GetValueOrDefault(bone) + weight * factor; } }
                Add(bw.boneIndex0, bw.weight0); Add(bw.boneIndex1, bw.weight1); Add(bw.boneIndex2, bw.weight2); Add(bw.boneIndex3, bw.weight3);
            }
            var kept = weights.OrderByDescending(w => w.Value).Take(4).ToArray(); float total = kept.Sum(w => w.Value);
            if (total <= 0) { throw new InvalidOperationException("Source layer has no valid bone weights"); }
            var result = new BoneWeight();
            if (kept.Length > 0) { result.boneIndex0 = kept[0].Key; result.weight0 = kept[0].Value / total; }
            if (kept.Length > 1) { result.boneIndex1 = kept[1].Key; result.weight1 = kept[1].Value / total; }
            if (kept.Length > 2) { result.boneIndex2 = kept[2].Key; result.weight2 = kept[2].Value / total; }
            if (kept.Length > 3) { result.boneIndex3 = kept[3].Key; result.weight3 = kept[3].Value / total; }
            return result;
        }

        private static int[] GolemRegions(EliteRigArt.Document art, PsdWriter.Layer layer)
        {
            // The existing 15 bone indices, with centers taken from the new stone shapes.
            var landmarks = new[] { new Vector2(475,830), new Vector2(429,1060), new Vector2(716,854), new Vector2(785,1095),
                new Vector2(625,743), new Vector2(612,620), new Vector2(490,488), new Vector2(713,460), new Vector2(520,249),
                new Vector2(287,418), new Vector2(214,732), new Vector2(208,908), new Vector2(962,403), new Vector2(1011,751), new Vector2(1070,991) };
            int width = layer.Rect.width, height = layer.Rect.height;
            var labels = Enumerable.Repeat(-1, width * height).ToArray(); var distances = Enumerable.Repeat(float.PositiveInfinity, labels.Length).ToArray();
            var queue = new SortedSet<(float cost, int at, int owner)>();
            for (int bone = 0; bone < landmarks.Length; bone++)
            {
                Vector2 center = art.MapPoint(landmarks[bone]) - (Vector2)layer.Rect.position;
                int seed = Enumerable.Range(0, labels.Length).Where(at => layer.Pixels[at].a > 100)
                    .OrderBy(at => Vector2.SqrMagnitude(center - new Vector2(at % width, at / width))).First();
                distances[seed] = 0; labels[seed] = bone; queue.Add((0, seed, bone));
            }
            while (queue.Count > 0)
            {
                var current = queue.Min; queue.Remove(current); if (current.cost > distances[current.at]) { continue; }
                int x = current.at % width, y = current.at / width;
                for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) { continue; }
                    int nx = x + dx, ny = y + dy;
                    if (nx < 0 || nx >= width || ny < 0 || ny >= height) { continue; }
                    int next = ny * width + nx; var color = layer.Pixels[next]; if (color.a <= 8) { continue; }
                    float luma = .299f * color.r + .587f * color.g + .114f * color.b;
                    float cost = current.cost + (dx != 0 && dy != 0 ? 1.414f : 1) * (luma < 70 ? 14 : luma < 105 ? 4 : 1);
                    if (cost >= distances[next]) { continue; }
                    distances[next] = cost; labels[next] = current.owner; queue.Add((cost, next, current.owner));
                }
            }
            // Propagate to transparent mesh padding and disconnected spikes, without changing opaque ownership.
            var flood = new Queue<int>(Enumerable.Range(0, labels.Length).Where(at => labels[at] >= 0));
            while (flood.Count > 0)
            {
                int at = flood.Dequeue(), x = at % width, y = at / width;
                foreach (var offset in new[] { Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down })
                {
                    int nx = x + offset.x, ny = y + offset.y; if (nx < 0 || nx >= width || ny < 0 || ny >= height) { continue; }
                    int next = ny * width + nx; if (labels[next] >= 0) { continue; }
                    labels[next] = labels[at]; flood.Enqueue(next);
                }
            }
            return labels;
        }

        private static void Grid(PsdWriter.Layer layer, Func<Vector2, int> rigidBone, out List<Vector2> vertices, out List<int> rigidWeights, out int[] indices, out Vector2Int[] edges)
        {
            int step = rigidBone == null ? 8 : 12; int width = layer.Rect.width, height = layer.Rect.height;
            var points = new List<Vector2>(); var weights = new List<int>(); var triangles = new List<int>(); var lookup = new Dictionary<Vector3Int, int>();
            int Vertex(int x, int y, int bone) { var key = new Vector3Int(x, y, bone); if (!lookup.TryGetValue(key, out int at)) { at = points.Count; lookup.Add(key, at); points.Add(new Vector2(x, y)); weights.Add(bone); } return at; }
            void Cell(int x, int y, int x1, int y1)
            {
                if (rigidBone != null && (x1 - x > 6 || y1 - y > 6))
                {
                    int owner = rigidBone(new Vector2(x, y));
                    if (rigidBone(new Vector2(x1 - 1, y)) != owner || rigidBone(new Vector2(x, y1 - 1)) != owner || rigidBone(new Vector2(x1 - 1, y1 - 1)) != owner)
                    {
                        int mx = (x + x1) / 2, my = (y + y1) / 2; Cell(x, y, mx, my); Cell(mx, y, x1, my); Cell(x, my, mx, y1); Cell(mx, my, x1, y1); return;
                    }
                }
                bool filled = false;
                for (int sy = y; sy < y1 && !filled; sy++) for (int sx = x; sx < x1; sx++) if (layer.Pixels[sy * width + sx].a > 8) { filled = true; break; }
                if (!filled) { return; }
                int bone = rigidBone?.Invoke(new Vector2((x + x1) * .5f, (y + y1) * .5f)) ?? -1;
                int a = Vertex(x, y, bone), b = Vertex(x1, y, bone), c = Vertex(x1, y1, bone), d = Vertex(x, y1, bone);
                triangles.AddRange(new[] { a, c, b, a, d, c });
            }
            for (int y = 0; y < height; y += step) for (int x = 0; x < width; x += step) { Cell(x, y, Math.Min(x + step, width), Math.Min(y + step, height)); }
            var counts = new Dictionary<Vector2Int, int>();
            for (int i = 0; i < triangles.Count; i += 3) for (int e = 0; e < 3; e++)
            { int a = triangles[i + e], b = triangles[i + (e + 1) % 3]; var key = new Vector2Int(Math.Min(a, b), Math.Max(a, b)); counts[key] = counts.GetValueOrDefault(key) + 1; }
            vertices = points; rigidWeights = weights; indices = triangles.ToArray(); edges = counts.Where(e => e.Value == 1).Select(e => e.Key).ToArray();
        }
    }
}
