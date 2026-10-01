using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace Game.Editor.MonsterRig
{
    /// <summary>
    /// 8bit RGB 레이어 PSD를 무압축으로 쓴다. PSD Importer가 읽을 수 있는 최소 구성만 기록한다.
    /// 좌표와 픽셀은 Unity 기준(좌하단 원점, 아래 행부터)으로 받는다.
    /// </summary>
    internal static class PsdWriter
    {
        public struct Layer
        {
            public string Name;
            public RectInt Rect;      // 캔버스 내 위치 (좌하단 원점)
            public Color32[] Pixels;  // Rect.width * Rect.height, 아래 행부터
        }

        public static void Write(string path, int width, int height, IList<Layer> layers)
        {
            using var stream = new MemoryStream();
            var w = new BigEndianWriter(stream);

            // File header
            w.Bytes(Encoding.ASCII.GetBytes("8BPS"));
            w.U16(1);
            w.Bytes(new byte[6]);
            w.U16(4);         // RGBA
            w.U32((uint)height);
            w.U32((uint)width);
            w.U16(8);         // depth
            w.U16(3);         // RGB

            w.U32(0);         // color mode data
            w.U32(0);         // image resources

            // Layer and mask information
            var layerInfo = BuildLayerInfo(height, layers);
            w.U32((uint)(4 + layerInfo.Length + 4));
            w.U32((uint)layerInfo.Length);
            w.Bytes(layerInfo);
            w.U32(0);         // global layer mask info

            // Merged image (planar, raw)
            var merged = Composite(width, height, layers);
            w.U16(0);
            for (int c = 0; c < 4; c++)
            {
                for (int row = height - 1; row >= 0; row--)
                {
                    for (int x = 0; x < width; x++)
                    {
                        w.Byte(Channel(merged[row * width + x], c));
                    }
                }
            }

            File.WriteAllBytes(path, stream.ToArray());
        }

        private static byte[] BuildLayerInfo(int height, IList<Layer> layers)
        {
            using var stream = new MemoryStream();
            var w = new BigEndianWriter(stream);

            // 음수 = 병합 이미지의 첫 알파 채널이 투명도
            w.I16((short)-layers.Count);

            // PSD는 첫 레코드가 가장 아래 레이어
            foreach (var layer in layers)
            {
                var r = layer.Rect;
                int top = height - (r.y + r.height);
                w.I32(top);
                w.I32(r.x);
                w.I32(top + r.height);
                w.I32(r.x + r.width);

                w.U16(4);
                uint channelLength = (uint)(2 + r.width * r.height);
                foreach (short id in new short[] { -1, 0, 1, 2 })
                {
                    w.I16(id);
                    w.U32(channelLength);
                }

                w.Bytes(Encoding.ASCII.GetBytes("8BIMnorm"));
                w.Byte(255); // opacity
                w.Byte(0);   // clipping
                w.Byte(0);   // flags
                w.Byte(0);   // filler

                var name = Encoding.ASCII.GetBytes(layer.Name);
                int nameLength = 1 + name.Length;
                int namePadded = (nameLength + 3) & ~3;
                w.U32((uint)(4 + 4 + namePadded));
                w.U32(0);    // layer mask data
                w.U32(0);    // blending ranges
                w.Byte((byte)name.Length);
                w.Bytes(name);
                w.Bytes(new byte[namePadded - nameLength]);
            }

            foreach (var layer in layers)
            {
                var r = layer.Rect;
                foreach (int c in new[] { 3, 0, 1, 2 })
                {
                    w.U16(0);
                    for (int row = r.height - 1; row >= 0; row--)
                    {
                        for (int x = 0; x < r.width; x++)
                        {
                            w.Byte(Channel(layer.Pixels[row * r.width + x], c));
                        }
                    }
                }
            }

            if (stream.Length % 2 != 0)
            {
                w.Byte(0);
            }

            return stream.ToArray();
        }

        private static Color32[] Composite(int width, int height, IList<Layer> layers)
        {
            var result = new Color32[width * height];
            foreach (var layer in layers)
            {
                var r = layer.Rect;
                for (int y = 0; y < r.height; y++)
                {
                    for (int x = 0; x < r.width; x++)
                    {
                        int cx = r.x + x, cy = r.y + y;
                        if (cx < 0 || cy < 0 || cx >= width || cy >= height)
                        {
                            continue;
                        }

                        Color src = layer.Pixels[y * r.width + x];
                        Color dst = result[cy * width + cx];
                        float a = src.a + dst.a * (1f - src.a);
                        Color rgb = a > 0f ? (src * src.a + dst * dst.a * (1f - src.a)) / a : Color.clear;
                        rgb.a = a;
                        result[cy * width + cx] = rgb;
                    }
                }
            }

            return result;
        }

        private static byte Channel(Color32 c, int index) => index switch
        {
            0 => c.r,
            1 => c.g,
            2 => c.b,
            _ => c.a,
        };

        private sealed class BigEndianWriter
        {
            private readonly Stream _s;
            public BigEndianWriter(Stream s) => _s = s;
            public void Byte(byte v) => _s.WriteByte(v);
            public void Bytes(byte[] v) => _s.Write(v, 0, v.Length);
            public void U16(ushort v) { Byte((byte)(v >> 8)); Byte((byte)v); }
            public void I16(short v) => U16((ushort)v);
            public void U32(uint v) { U16((ushort)(v >> 16)); U16((ushort)v); }
            public void I32(int v) => U32((uint)v);
        }
    }
}
