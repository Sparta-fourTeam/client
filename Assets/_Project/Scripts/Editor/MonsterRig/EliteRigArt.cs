using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Game.Editor
{
    internal static class EliteRigArt
    {
        internal sealed class Document
        {
            public int Width, Height;
            public List<PsdWriter.Layer> Layers = new();
            public Color32[] Image;
            public Func<Vector2, Vector2> MapPoint;
        }

        internal static Document Read(string path)
        {
            using var stream = File.OpenRead(path);
            using var reader = new BinaryReader(stream);
            int U16() => (reader.ReadByte() << 8) | reader.ReadByte();
            int I16() => (short)U16();
            int I32() => (reader.ReadByte() << 24) | (reader.ReadByte() << 16) | (reader.ReadByte() << 8) | reader.ReadByte();
            void Skip(int length) => stream.Position += length;
            if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != "8BPS" || U16() != 1) { throw new InvalidDataException(path); }
            Skip(6); U16();
            int height = I32(), width = I32();
            if (U16() != 8 || U16() != 3) { throw new InvalidDataException("Expected 8-bit RGB PSD"); }
            Skip(I32()); Skip(I32()); I32(); I32();
            int count = Math.Abs(I16());
            var channels = new List<(short id, int bytes)>[count];
            var document = new Document { Width = width, Height = height };
            for (int i = 0; i < count; i++)
            {
                int top = I32(), left = I32(), bottom = I32(), right = I32();
                int channelCount = U16(); channels[i] = new();
                for (int c = 0; c < channelCount; c++) { channels[i].Add(((short)I16(), I32())); }
                Skip(12); int extra = I32(); long end = stream.Position + extra;
                Skip(I32()); Skip(I32());
                int nameLength = reader.ReadByte(); string name = Encoding.ASCII.GetString(reader.ReadBytes(nameLength));
                stream.Position = end;
                document.Layers.Add(new PsdWriter.Layer { Name = name, Rect = new RectInt(left, height - bottom, right - left, bottom - top), Pixels = new Color32[(right - left) * (bottom - top)] });
            }
            for (int i = 0; i < count; i++)
            {
                var layer = document.Layers[i];
                foreach (var channel in channels[i])
                {
                    long end = stream.Position + channel.bytes;
                    int compression = U16(); byte[] data;
                    if (compression == 0) { data = reader.ReadBytes(layer.Pixels.Length); }
                    else if (compression == 1)
                    {
                        var lengths = new int[layer.Rect.height]; for (int y = 0; y < lengths.Length; y++) { lengths[y] = U16(); }
                        var result = new List<byte>(layer.Pixels.Length);
                        foreach (int length in lengths)
                        {
                            long rowEnd = stream.Position + length;
                            while (stream.Position < rowEnd)
                            {
                                int run = reader.ReadSByte();
                                if (run >= 0) { result.AddRange(reader.ReadBytes(run + 1)); }
                                else if (run != -128) { byte value = reader.ReadByte(); for (int n = 0; n < 1 - run; n++) { result.Add(value); } }
                            }
                        }
                        data = result.ToArray();
                    }
                    else { throw new InvalidDataException("Unsupported PSD channel compression"); }
                    for (int y = 0; y < layer.Rect.height; y++) for (int x = 0; x < layer.Rect.width; x++)
                    {
                        int pixel = (layer.Rect.height - 1 - y) * layer.Rect.width + x;
                        var color = layer.Pixels[pixel]; byte value = data[y * layer.Rect.width + x];
                        if (channel.id == -1) { color.a = value; } else if (channel.id == 0) { color.r = value; } else if (channel.id == 1) { color.g = value; } else if (channel.id == 2) { color.b = value; }
                        layer.Pixels[pixel] = color;
                    }
                    stream.Position = end;
                }
            }
            document.Image = Composite(document);
            return document;
        }

        private static Color32[] Composite(Document document)
        {
            var pixels = new Color32[document.Width * document.Height];
            foreach (var layer in document.Layers)
            {
                for (int y = 0; y < layer.Rect.height; y++) for (int x = 0; x < layer.Rect.width; x++)
                {
                    int at = (y + layer.Rect.y) * document.Width + x + layer.Rect.x;
                    Color src = layer.Pixels[y * layer.Rect.width + x], dst = pixels[at];
                    float alpha = src.a + dst.a * (1f - src.a);
                    if (alpha <= 0) { continue; }
                    Color result = (src * src.a + dst * dst.a * (1f - src.a)) / alpha; result.a = alpha; pixels[at] = result;
                }
            }
            return pixels;
        }

        internal static RectInt Bounds(Color32[] pixels, int width, int height)
        {
            int x0 = width, y0 = height, x1 = -1, y1 = -1;
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++) if (pixels[y * width + x].a > 24)
            { x0 = Math.Min(x0, x); y0 = Math.Min(y0, y); x1 = Math.Max(x1, x); y1 = Math.Max(y1, y); }
            return x1 < 0 ? new RectInt(0, 0, 1, 1) : new RectInt(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
        }

        internal static Document Prepare(string name)
        {
            string sourceName = name == "Oni" ? "Horned" : name;
            var reference = Read($"Assets/_Project/PSD/{sourceName}.psd");
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Color32[] incoming; int iw, ih;
            try { texture.LoadImage(File.ReadAllBytes($"Assets/_Project/Sprites/{name}Elite.png")); iw = texture.width; ih = texture.height; incoming = texture.GetPixels32(); }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
            var from = Bounds(incoming, iw, ih); var to = Bounds(reference.Image, reference.Width, reference.Height);
            int width = reference.Width, height = reference.Height;
            var image = new Color32[width * height];
            // Place the new drawing in the existing bind-pose canvas so original clip positions stay valid.
            for (int y = to.yMin; y < to.yMax; y++) for (int x = to.xMin; x < to.xMax; x++)
            {
                float r = 0, g = 0, b = 0, alpha = 0;
                for (int sy = 0; sy < 4; sy++) for (int sx = 0; sx < 4; sx++)
                {
                    int px = Mathf.Clamp((int)(from.x + (x - to.x + (sx + .5f) / 4) * from.width / to.width), 0, iw - 1);
                    int py = Mathf.Clamp((int)(from.y + (y - to.y + (sy + .5f) / 4) * from.height / to.height), 0, ih - 1);
                    var c = incoming[py * iw + px]; r += c.r * c.a; g += c.g * c.a; b += c.b * c.a; alpha += c.a;
                }
                if (alpha > 0) { image[y * width + x] = new Color32((byte)(r / alpha), (byte)(g / alpha), (byte)(b / alpha), (byte)(alpha / 16)); }
            }
            bool[] markers = reference.Layers.Select(l => l.Name.Contains("_cross")).ToArray();
            var normalOwners = NearestOwners(reference, false);
            var markerOwners = markers.Any(m => m) ? NearestOwners(reference, true) : null;
            var owner = (int[])normalOwners.Clone();
            Vector2 MapPoint(Vector2 point) => new Vector2(to.x + (point.x - from.x) * to.width / from.width,
                to.y + (ih - point.y - from.y) * to.height / from.height);
            if (name == "EyeJelly")
            {
                var center = MapPoint(new Vector2(627, 505));
                float rx = 270f * to.width / from.width, ry = 275f * to.height / from.height;
                int body = reference.Layers.FindIndex(l => l.Name == "eyejelly_body");
                var outside = new bool[image.Length];
                for (int at = 0; at < image.Length; at++)
                {
                    float dx = (at % width - center.x) / rx, dy = (at / width - center.y) / ry;
                    if (dx * dx + dy * dy <= 1) { owner[at] = body; }
                    else { outside[at] = image[at].a > 24; }
                }
                foreach (var component in Components(outside, width, height))
                {
                    var votes = component.Select(at => normalOwners[at]).Where(i => i >= 0 && reference.Layers[i].Name.Contains("_t"))
                        .GroupBy(i => i).OrderByDescending(g => g.Count()).FirstOrDefault();
                    int part = votes?.Key ?? body;
                    foreach (int at in component) { owner[at] = part; }
                }
            }
            if (name == "BrainSpider")
            {
                var low = MapPoint(new Vector2(440, 750)); var high = MapPoint(new Vector2(880, 320));
                int brain = reference.Layers.FindIndex(l => l.Name.EndsWith("_brain"));
                for (int y = Mathf.CeilToInt(low.y); y <= high.y; y++)
                {
                    int left = width, right = -1;
                    for (int x = Mathf.CeilToInt(low.x); x <= high.x; x++)
                    {
                        var color = image[y * width + x];
                        if (color.a > 100 && color.b > color.g + 5 && color.r > color.g + 5) { left = Math.Min(left, x); right = Math.Max(right, x); }
                    }
                    if (right >= left) for (int x = Math.Max(0, left - 1); x <= Math.Min(width - 1, right + 1); x++) { owner[y * width + x] = brain; }
                }
            }
            if (name == "Oni")
            {
                var polygon = new[] { new Vector2(715, 365), new Vector2(805, 485), new Vector2(940, 470), new Vector2(1040, 150), new Vector2(978, 0), new Vector2(934, 139), new Vector2(851, 265) }.Select(MapPoint).ToArray();
                int horn = reference.Layers.FindIndex(l => l.Name.EndsWith("_horn"));
                for (int at = 0; at < image.Length; at++) if (Inside(new Vector2(at % width, at / width), polygon)) { owner[at] = horn; }
            }
            var markerPixels = new bool[image.Length];
            if (markerOwners != null)
            {
                // Facial landmarks belong to the elite drawing; the ordinary face can be in a different place.
                Rect face = name == "Ghost" ? new Rect(450, 245, 250, 300)
                    : name == "Oni" ? new Rect(630, 460, 280, 200) : new Rect(460, 390, 290, 315);
                Vector2 Map(float x, float y) => new Vector2(to.x + (x - from.x) * to.width / from.width,
                    to.y + (ih - y - from.y) * to.height / from.height);
                var low = Map(face.xMin, face.yMax); var high = Map(face.xMax, face.yMin);
                var box = new RectInt(Mathf.FloorToInt(low.x), Mathf.FloorToInt(low.y), Mathf.CeilToInt(high.x - low.x), Mathf.CeilToInt(high.y - low.y));
                int margin = 1;
                for (int y = Math.Max(0, box.yMin - margin); y < Math.Min(height, box.yMax + margin); y++) for (int x = Math.Max(0, box.xMin - margin); x < Math.Min(width, box.xMax + margin); x++)
                {
                    int at = y * width + x; var c = image[at];
                    if (c.a > 32 && .299f * c.r + .587f * c.g + .114f * c.b < 115) { markerPixels[at] = true; }
                }
                // Keep the central facial stroke, excluding unrelated dark outline components.
                var components = Components(markerPixels, width, height);
                var center = box.center;
                var stroke = components.OrderByDescending(c => c.Count / (1f + Vector2.SqrMagnitude(new Vector2((float)c.Average(p => p % width), (float)c.Average(p => p / width)) - center) / Math.Max(1, box.width * box.height))).FirstOrDefault();
                Array.Clear(markerPixels, 0, markerPixels.Length);
                if (stroke != null) foreach (int at in stroke)
                {
                    markerPixels[at] = true;
                    if (name == "Oni") { owner[at] = Array.FindIndex(markers, m => m); }
                    else
                    {
                        Vector2 offset = new Vector2(at % width, at / width) - center;
                        string suffix = Mathf.Abs(offset.x) > Mathf.Abs(offset.y) ? (offset.x < 0 ? "_crossl" : "_crossr") : (offset.y < 0 ? "_crossd" : "_crossu");
                        owner[at] = reference.Layers.FindIndex(l => l.Name.EndsWith(suffix));
                    }
                }
            }
            var layerPixels = reference.Layers.Select(_ => new Color32[image.Length]).ToArray();
            for (int at = 0; at < image.Length; at++) if (image[at].a > 8 && owner[at] >= 0) { layerPixels[owner[at]][at] = image[at]; }
            if (name == "Oni")
            {
                int head = reference.Layers.FindIndex(l => l.Name.EndsWith("_head")), horn = reference.Layers.FindIndex(l => l.Name.EndsWith("_horn"));
                int body = reference.Layers.FindIndex(l => l.Name.EndsWith("_body"));
                var headCenter = MapPoint(new Vector2(690, 495)); var bodyCenter = MapPoint(new Vector2(650, 946));
                float hx = 210f * to.width / from.width, hy = 205f * to.height / from.height;
                float bx = 250f * to.width / from.width, by = 330f * to.height / from.height;
                for (int at = 0; at < image.Length; at++)
                {
                    if (image[at].a <= 24) { continue; }
                    var point = new Vector2(at % width, at / width);
                    var h = point - headCenter; var b = point - bodyCenter;
                    if (owner[at] == horn && h.x * h.x / (hx * hx) + h.y * h.y / (hy * hy) < 1) { layerPixels[head][at] = new Color32(239, 238, 241, 255); }
                    if (reference.Layers[owner[at]].Name.Contains("_arm") && b.x * b.x / (bx * bx) + b.y * b.y / (by * by) < 1) { layerPixels[body][at] = new Color32(239, 238, 241, 255); }
                }
            }
            // A clean body remains behind the independently animated face marks.
            for (int at = 0; at < image.Length; at++) if (markerPixels[at])
            {
                int x = at % width, y = at / width; Color32 replacement = default; bool found = false;
                for (int radius = 1; radius < 35 && !found; radius++)
                {
                    foreach (var offset in new[] { new Vector2Int(radius, 0), new Vector2Int(-radius, 0), new Vector2Int(0, radius), new Vector2Int(0, -radius) })
                    {
                        int nx = x + offset.x, ny = y + offset.y; if (nx < 0 || nx >= width || ny < 0 || ny >= height) { continue; }
                        int neighbor = ny * width + nx; var c = image[neighbor];
                        if (!markerPixels[neighbor] && c.a > 100 && c.r + c.g + c.b > 400) { replacement = c; found = true; break; }
                    }
                }
                if (found && normalOwners[at] >= 0) { layerPixels[normalOwners[at]][at] = replacement; }
            }
            // Backfill hidden joint pixels underneath higher layers so moving an arm or horn exposes a filled surface.
            var visible = layerPixels.Select(p => (Color32[])p.Clone()).ToArray();
            for (int layer = 0; layer < visible.Length; layer++) for (int at = 0; at < image.Length; at++)
            {
                if (image[at].a <= 24 || owner[at] <= layer || layerPixels[layer][at].a > 24) { continue; }
                int x = at % width, y = at / width;
                bool filled = false;
                for (int dy = -2; dy <= 2 && !filled; dy++) for (int dx = -2; dx <= 2; dx++)
                {
                    int nx = x + dx, ny = y + dy; if (nx < 0 || nx >= width || ny < 0 || ny >= height) { continue; }
                    var color = visible[layer][ny * width + nx]; if (color.a > 100) { layerPixels[layer][at] = color; filled = true; break; }
                }
            }
            var result = new Document { Width = width, Height = height, Image = image, MapPoint = MapPoint };
            for (int i = 0; i < layerPixels.Length; i++)
            {
                var pixels = layerPixels[i]; var bounds = Bounds(pixels, width, height);
                if (!pixels.Any(c => c.a > 24))
                {
                    // Retain unused original layer paths without showing an extra part.
                    var point = reference.Layers[i].Rect.center; int at = Mathf.Clamp(Mathf.RoundToInt(point.y), 0, height - 1) * width + Mathf.Clamp(Mathf.RoundToInt(point.x), 0, width - 1);
                    pixels[at] = new Color32(255, 255, 255, 9); bounds = new RectInt(at % width, at / width, 1, 1);
                }
                var trimmed = new Color32[bounds.width * bounds.height];
                for (int y = 0; y < bounds.height; y++) Array.Copy(pixels, (bounds.y + y) * width + bounds.x, trimmed, y * bounds.width, bounds.width);
                result.Layers.Add(new PsdWriter.Layer { Name = reference.Layers[i].Name, Rect = bounds, Pixels = trimmed });
            }
            return result;
        }

        private static bool Inside(Vector2 point, Vector2[] polygon)
        {
            bool inside = false;
            for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
                if ((polygon[i].y > point.y) != (polygon[j].y > point.y) && point.x < (polygon[j].x - polygon[i].x) * (point.y - polygon[i].y) / (polygon[j].y - polygon[i].y) + polygon[i].x) { inside = !inside; }
            return inside;
        }

        private static int[] NearestOwners(Document document, bool onlyMarkers)
        {
            int width = document.Width, height = document.Height; var labels = Enumerable.Repeat(-1, width * height).ToArray(); var queue = new Queue<int>();
            for (int i = 0; i < document.Layers.Count; i++)
            {
                var layer = document.Layers[i]; if (layer.Name.Contains("_cross") != onlyMarkers) { continue; }
                for (int y = 0; y < layer.Rect.height; y++) for (int x = 0; x < layer.Rect.width; x++) if (layer.Pixels[y * layer.Rect.width + x].a > 64) { labels[(layer.Rect.y + y) * width + layer.Rect.x + x] = i; }
            }
            for (int at = 0; at < labels.Length; at++) if (labels[at] >= 0) { queue.Enqueue(at); }
            while (queue.Count > 0)
            {
                int at = queue.Dequeue(), x = at % width, y = at / width;
                foreach (var d in new[] { -1, 1, -width, width })
                {
                    if (d == -1 && x == 0 || d == 1 && x == width - 1 || d == -width && y == 0 || d == width && y == height - 1) { continue; }
                    int next = at + d; if (labels[next] != -1) { continue; }
                    labels[next] = labels[at]; queue.Enqueue(next);
                }
            }
            return labels;
        }

        private static List<List<int>> Components(bool[] mask, int width, int height)
        {
            var visited = new bool[mask.Length]; var result = new List<List<int>>();
            for (int start = 0; start < mask.Length; start++) if (mask[start] && !visited[start])
            {
                var part = new List<int>(); var queue = new Queue<int>(); queue.Enqueue(start); visited[start] = true;
                while (queue.Count > 0)
                {
                    int at = queue.Dequeue(); part.Add(at); int x = at % width, y = at / width;
                    foreach (var d in new[] { -1, 1, -width, width })
                    {
                        if (d == -1 && x == 0 || d == 1 && x == width - 1 || d == -width && y == 0 || d == width && y == height - 1) { continue; }
                        int next = at + d; if (!mask[next] || visited[next]) { continue; }
                        visited[next] = true; queue.Enqueue(next);
                    }
                }
                result.Add(part);
            }
            return result;
        }

        internal static void WritePreview(string name, string path)
        {
            var document = Prepare(name); var colors = new Color32[document.Width * document.Height];
            for (int i = 0; i < document.Layers.Count; i++)
            {
                var layer = document.Layers[i]; Color32 tint = Color.HSVToRGB(i / (float)Math.Max(1, document.Layers.Count), .65f, 1);
                for (int y = 0; y < layer.Rect.height; y++) for (int x = 0; x < layer.Rect.width; x++)
                { var c = layer.Pixels[y * layer.Rect.width + x]; if (c.a <= 24) { continue; } tint.a = c.a; colors[(layer.Rect.y + y) * document.Width + layer.Rect.x + x] = tint; }
            }
            var source = EliteRigSkin.Read($"Assets/_Project/PSD/{(name == "Oni" ? "Horned" : name)}.psd");
            var bones = (Array)EliteRigSkin.Get(source.Character, "bones"); var matrices = new Matrix4x4[bones.Length];
            for (int i = 0; i < bones.Length; i++)
            {
                object bone = bones.GetValue(i); int parent = (int)EliteRigSkin.Get(bone, "parentId");
                var local = Matrix4x4.TRS((Vector3)EliteRigSkin.Get(bone, "position"), (Quaternion)EliteRigSkin.Get(bone, "rotation"), Vector3.one);
                matrices[i] = parent < 0 ? local : matrices[parent] * local;
                var start = matrices[i].MultiplyPoint3x4(Vector3.zero); int bx = Mathf.RoundToInt(start.x), by = Mathf.RoundToInt(start.y);
                for (int dy = -2; dy <= 2; dy++) for (int dx = -2; dx <= 2; dx++)
                {
                    int x = bx + dx, y = by + dy; if (x < 0 || x >= document.Width || y < 0 || y >= document.Height) { continue; }
                    colors[y * document.Width + x] = Mathf.Abs(dx) == 2 || Mathf.Abs(dy) == 2 ? new Color32(0, 0, 0, 255) : new Color32(255, 255, 255, 255);
                }
            }
            var texture = new Texture2D(document.Width * 3, document.Height * 3, TextureFormat.RGBA32, false); var enlarged = new Color32[texture.width * texture.height];
            for (int y = 0; y < texture.height; y++) for (int x = 0; x < texture.width; x++) { enlarged[y * texture.width + x] = colors[(y / 3) * document.Width + x / 3]; }
            texture.SetPixels32(enlarged); texture.Apply(); Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllBytes(path, texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture);
        }
    }
}
