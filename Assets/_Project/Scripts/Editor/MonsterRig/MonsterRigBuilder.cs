using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Animation;
using UnityEditor.U2D.PSD;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.U2D;

namespace Game.Editor.MonsterRig
{
    /// <summary>
    /// PNG → 레이어 PSD → PSD Importer(Character 모드) → 본 / 메시 / 가중치까지 자동으로 넣는다.
    /// Skinning Editor에서 손으로 하는 작업과 같은 데이터 프로바이더를 사용하므로
    /// 결과는 Skinning Editor에서 그대로 열어서 다듬을 수 있다.
    /// </summary>
    internal static class MonsterRigBuilder
    {
        public sealed class LayerSpec
        {
            public string Name;
            public string PngPath;
            public Vector2Int CanvasOffset; // PNG 좌하단이 놓일 캔버스 위치
            public string[] Bones;          // 이 레이어에 영향을 주는 본 (null = 전체)
            public Color32[] Pixels;        // PNG 대신 직접 만든 픽셀 (아래 행부터)
            public Vector2Int PixelsSize;
            public Vector2[] MeshVertices;  // 직접 만든 메시 (PNG 픽셀 좌표)
            public int[] MeshIndices;
            public string[] MeshVertexBone; // 버텍스별 본 이름 (가중치 100%)
        }

        public sealed class BoneSpec
        {
            public string Name;
            public string Parent;   // null = 루트
            public Vector2 Start;   // 캔버스 픽셀 (좌하단 원점)
            public Vector2 End;
        }

        public sealed class RigSpec
        {
            public string PsdPath;
            public Vector2Int CanvasSize;
            public float PixelsPerUnit = 100f;
            public Vector2 Pivot = new(0.5f, 0f); // 문서 기준 정규화 피벗
            public int GridStep = 10;
            public float WeightFalloff = 2.5f;
            public int MaxInfluences = 3;
            public List<LayerSpec> Layers = new();
            public List<BoneSpec> Bones = new();
        }

        public static GameObject Build(RigSpec spec)
        {
            var layers = spec.Layers.Select(l => LoadLayer(l)).ToList();
            PsdWriter.Write(Path.GetFullPath(spec.PsdPath), spec.CanvasSize.x, spec.CanvasSize.y,
                layers.Select(l => l.psd).ToList());

            AssetDatabase.ImportAsset(spec.PsdPath, ImportAssetOptions.ForceUpdate);
            if (AssetImporter.GetAtPath(spec.PsdPath) is not PSDImporter)
            {
                AssetDatabase.SetImporterOverride<PSDImporter>(spec.PsdPath);
                AssetDatabase.ImportAsset(spec.PsdPath, ImportAssetOptions.ForceUpdate);
            }

            var importer = (PSDImporter)AssetImporter.GetAtPath(spec.PsdPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.useMosaicMode = true;
            importer.useCharacterMode = true;
            importer.spritePixelsPerUnit = spec.PixelsPerUnit;
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();

            // 재임포트하면 importer 인스턴스가 교체되므로 다시 가져온다.
            importer = (PSDImporter)AssetImporter.GetAtPath(spec.PsdPath);
            ApplySkeleton(importer, spec, layers);
            importer = (PSDImporter)AssetImporter.GetAtPath(spec.PsdPath);
            importer.SaveAndReimport();

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(spec.PsdPath);
            if (prefab == null)
            {
                throw new InvalidOperationException($"PSD 임포트 결과 프리팹이 없음: {spec.PsdPath}");
            }

            return prefab;
        }

        private static void ApplySkeleton(PSDImporter importer, RigSpec spec,
            List<(LayerSpec spec, PsdWriter.Layer psd)> layers)
        {
            var dp = (ISpriteEditorDataProvider)importer;
            dp.InitSpriteEditorDataProvider();

            var characterProvider = dp.GetDataProvider<ICharacterDataProvider>();
            var boneProvider = dp.GetDataProvider<ISpriteBoneDataProvider>();
            var meshProvider = dp.GetDataProvider<ISpriteMeshDataProvider>();
            var rects = dp.GetSpriteRects();

            var world = ComputeWorldBones(spec.Bones);
            var characterBones = ToSpriteBones(spec.Bones, world, Vector2.zero);

            var character = characterProvider.GetCharacterData();
            character.bones = characterBones;
            character.pivot = spec.Pivot;

            for (int p = 0; p < character.parts.Length; p++)
            {
                var part = character.parts[p];
                var rect = rects.First(r => r.spriteID.ToString() == part.spriteId);
                var layer = layers.FirstOrDefault(l => l.spec.Name == rect.name);
                if (layer.spec == null)
                {
                    throw new InvalidOperationException($"레이어 스펙 없음: {rect.name}");
                }

                var boneIndices = (layer.spec.Bones ?? spec.Bones.Select(b => b.Name).ToArray())
                    .Select(n => spec.Bones.FindIndex(b => b.Name == n))
                    .Where(i => i >= 0).OrderBy(i => i).ToArray();
                part.bones = boneIndices;
                character.parts[p] = part;

                Vector2 origin = part.spritePosition.position;
                var subset = boneIndices.Select(i => spec.Bones[i]).ToList();
                var subsetWorld = boneIndices.Select(i => world[i]).ToArray();
                boneProvider.SetBones(rect.spriteID, ToSpriteBones(subset, subsetWorld, origin).ToList());

                Vertex2DMetaData[] meta;
                int[] indices;
                Vector2Int[] edges;
                if (layer.spec.MeshVertices != null)
                {
                    // 직접 만든 메시: PNG 좌표 → 스프라이트(잘린 레이어) 로컬, 버텍스마다 지정한 본 하나에 100%
                    Vector2 trim = layer.psd.Rect.position - layer.spec.CanvasOffset;
                    var size = new Vector2(rect.rect.width, rect.rect.height);
                    meta = layer.spec.MeshVertices.Select((v, i) =>
                    {
                        int bone = Array.IndexOf(boneIndices, spec.Bones.FindIndex(b => b.Name == layer.spec.MeshVertexBone[i]));
                        if (bone < 0)
                        {
                            throw new InvalidOperationException($"메시 본이 레이어 본 목록에 없음: {layer.spec.MeshVertexBone[i]}");
                        }

                        return new Vertex2DMetaData
                        {
                            position = Vector2.Min(Vector2.Max(v - trim, Vector2.zero), size),
                            boneWeight = new BoneWeight { boneIndex0 = bone, weight0 = 1f },
                        };
                    }).ToArray();
                    indices = layer.spec.MeshIndices;
                    edges = BoundaryEdges(indices);
                }
                else
                {
                    BuildMesh(layer.psd, (int)rect.rect.width, (int)rect.rect.height, spec.GridStep,
                        out var vertices, out indices, out edges);
                    meta = new Vertex2DMetaData[vertices.Count];
                    for (int v = 0; v < vertices.Count; v++)
                    {
                        meta[v].position = vertices[v];
                        meta[v].boneWeight = ComputeWeight(origin + vertices[v], subsetWorld, spec.WeightFalloff, spec.MaxInfluences);
                    }
                }

                meshProvider.SetVertices(rect.spriteID, meta);
                meshProvider.SetIndices(rect.spriteID, indices);
                meshProvider.SetEdges(rect.spriteID, edges);
                Debug.Log($"[MonsterRigBuilder] {rect.name}: verts={meta.Length} tris={indices.Length / 3} bones={boneIndices.Length}");
            }

            characterProvider.SetCharacterData(character);
            dp.Apply();
        }

        private static (LayerSpec spec, PsdWriter.Layer psd) LoadLayer(LayerSpec spec)
        {
            Color32[] pixels;
            int w, h;
            if (spec.Pixels != null)
            {
                pixels = spec.Pixels;
                w = spec.PixelsSize.x;
                h = spec.PixelsSize.y;
            }
            else
            {
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                tex.LoadImage(File.ReadAllBytes(Path.GetFullPath(spec.PngPath)));
                pixels = tex.GetPixels32();
                w = tex.width;
                h = tex.height;
                UnityEngine.Object.DestroyImmediate(tex);
            }

            // 투명 여백을 잘라 레이어 경계를 그림에 딱 맞춘다.
            int minX = w, minY = h, maxX = -1, maxY = -1;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (pixels[y * w + x].a <= 8)
                    {
                        continue;
                    }

                    minX = Math.Min(minX, x); maxX = Math.Max(maxX, x);
                    minY = Math.Min(minY, y); maxY = Math.Max(maxY, y);
                }
            }

            int tw = maxX - minX + 1, th = maxY - minY + 1;
            var trimmed = new Color32[tw * th];
            for (int y = 0; y < th; y++)
            {
                Array.Copy(pixels, (minY + y) * w + minX, trimmed, y * tw, tw);
            }

            return (spec, new PsdWriter.Layer
            {
                Name = spec.Name,
                Rect = new RectInt(spec.CanvasOffset.x + minX, spec.CanvasOffset.y + minY, tw, th),
                Pixels = trimmed,
            });
        }

        // ---------- bones ----------

        private struct WorldBone
        {
            public Vector2 Start, End;
            public float Angle; // deg
            public float Length;
        }

        private static WorldBone[] ComputeWorldBones(List<BoneSpec> bones)
        {
            return bones.Select(b =>
            {
                var d = b.End - b.Start;
                return new WorldBone
                {
                    Start = b.Start,
                    End = b.End,
                    Angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg,
                    Length = d.magnitude,
                };
            }).ToArray();
        }

        // 루트 본은 origin 기준 절대 위치/회전, 자식 본은 부모 로컬 공간 (Skinning Editor 저장 형식과 동일)
        private static SpriteBone[] ToSpriteBones(List<BoneSpec> bones, WorldBone[] world, Vector2 origin)
        {
            var result = new SpriteBone[bones.Count];
            for (int i = 0; i < bones.Count; i++)
            {
                int parent = bones[i].Parent == null ? -1 : bones.FindIndex(b => b.Name == bones[i].Parent);
                Vector2 position;
                float angle;
                if (parent < 0)
                {
                    position = world[i].Start - origin;
                    angle = world[i].Angle;
                }
                else
                {
                    var inv = Quaternion.Euler(0f, 0f, -world[parent].Angle);
                    position = inv * (world[i].Start - world[parent].Start);
                    angle = world[i].Angle - world[parent].Angle;
                }

                result[i] = new SpriteBone
                {
                    name = bones[i].Name,
                    guid = GUID.Generate().ToString(),
                    position = new Vector3(position.x, position.y, 0f),
                    rotation = Quaternion.Euler(0f, 0f, angle),
                    length = world[i].Length,
                    parentId = parent,
                    color = Color.HSVToRGB(i / (float)Mathf.Max(1, bones.Count), 0.7f, 1f),
                };
            }

            return result;
        }

        // 본 선분까지의 거리 역수로 가중치. 가장 가까운 MaxInfluences개만 남기고 정규화한다.
        private static BoneWeight ComputeWeight(Vector2 point, WorldBone[] bones, float falloff, int maxInfluences)
        {
            var scored = new List<(int index, float w)>();
            for (int i = 0; i < bones.Length; i++)
            {
                float d = DistanceToSegment(point, bones[i].Start, bones[i].End);
                scored.Add((i, 1f / Mathf.Pow(d + 1f, falloff)));
            }

            var top = scored.OrderByDescending(s => s.w).Take(Mathf.Clamp(maxInfluences, 1, 4)).ToArray();
            float sum = top.Sum(s => s.w);
            var bw = new BoneWeight();
            for (int k = 0; k < top.Length; k++)
            {
                float w = top[k].w / sum;
                switch (k)
                {
                    case 0: bw.boneIndex0 = top[k].index; bw.weight0 = w; break;
                    case 1: bw.boneIndex1 = top[k].index; bw.weight1 = w; break;
                    case 2: bw.boneIndex2 = top[k].index; bw.weight2 = w; break;
                    case 3: bw.boneIndex3 = top[k].index; bw.weight3 = w; break;
                }
            }

            return bw;
        }

        private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            float t = ab.sqrMagnitude > 0f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
            return Vector2.Distance(p, a + ab * t);
        }

        // ---------- mesh ----------

        // 불투명 픽셀이 있는 격자 칸만 남겨 삼각형 2개씩 만든다. 경계 변이 edges가 된다.
        private static void BuildMesh(PsdWriter.Layer layer, int width, int height, int step,
            out List<Vector2> vertices, out int[] indices, out Vector2Int[] edges)
        {
            int cols = Mathf.CeilToInt(width / (float)step);
            int rows = Mathf.CeilToInt(height / (float)step);
            var vertexIndex = new Dictionary<Vector2Int, int>();
            var verts = new List<Vector2>();
            var tris = new List<int>();

            int Vertex(int gx, int gy)
            {
                var key = new Vector2Int(gx, gy);
                if (vertexIndex.TryGetValue(key, out int idx))
                {
                    return idx;
                }

                idx = verts.Count;
                verts.Add(new Vector2(Mathf.Min(gx * step, width), Mathf.Min(gy * step, height)));
                vertexIndex[key] = idx;
                return idx;
            }

            for (int gy = 0; gy < rows; gy++)
            {
                for (int gx = 0; gx < cols; gx++)
                {
                    if (!CellHasPixels(layer, gx * step, gy * step, step))
                    {
                        continue;
                    }

                    int a = Vertex(gx, gy), b = Vertex(gx + 1, gy), c = Vertex(gx + 1, gy + 1), d = Vertex(gx, gy + 1);
                    tris.AddRange(new[] { a, b, c, a, c, d });
                }
            }

            var edgeCount = new Dictionary<(int, int), int>();
            for (int t = 0; t < tris.Count; t += 3)
            {
                for (int e = 0; e < 3; e++)
                {
                    int i0 = tris[t + e], i1 = tris[t + (e + 1) % 3];
                    var key = i0 < i1 ? (i0, i1) : (i1, i0);
                    edgeCount[key] = edgeCount.TryGetValue(key, out int n) ? n + 1 : 1;
                }
            }

            vertices = verts;
            indices = tris.ToArray();
            edges = edgeCount.Where(kv => kv.Value == 1).Select(kv => new Vector2Int(kv.Key.Item1, kv.Key.Item2)).ToArray();
        }

        private static Vector2Int[] BoundaryEdges(int[] tris)
        {
            var edgeCount = new Dictionary<(int, int), int>();
            for (int t = 0; t < tris.Length; t += 3)
            {
                for (int e = 0; e < 3; e++)
                {
                    int i0 = tris[t + e], i1 = tris[t + (e + 1) % 3];
                    var key = i0 < i1 ? (i0, i1) : (i1, i0);
                    edgeCount[key] = edgeCount.TryGetValue(key, out int n) ? n + 1 : 1;
                }
            }

            return edgeCount.Where(kv => kv.Value == 1).Select(kv => new Vector2Int(kv.Key.Item1, kv.Key.Item2)).ToArray();
        }

        private static bool CellHasPixels(PsdWriter.Layer layer, int x0, int y0, int step)
        {
            int w = layer.Rect.width, h = layer.Rect.height;
            for (int y = y0; y < Mathf.Min(y0 + step, h); y++)
            {
                for (int x = x0; x < Mathf.Min(x0 + step, w); x++)
                {
                    if (layer.Pixels[y * w + x].a > 8)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
