using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Game.Network
{
    /// <summary>WebSocket 조각과 무관하게 UTF-8 바이트 단위로 STOMP 프레임을 읽는다.</summary>
    public sealed class StompFrameReader
    {
        public sealed class Frame
        {
            public string Command;
            public Dictionary<string, string> Headers;
            public string Body;
        }

        private const int MaxFrameBytes = 1024 * 1024;
        private readonly List<byte> _pending = new();

        public void Append(byte[] bytes, int count)
        {
            if (_pending.Count + count > MaxFrameBytes) { throw new FormatException("STOMP frame too large."); }
            for (int i = 0; i < count; i++) { _pending.Add(bytes[i]); }
        }

        public bool TryRead(out Frame frame)
        {
            frame = null;
            int start = 0;
            while (start < _pending.Count && (_pending[start] == 10 || _pending[start] == 13)) { start++; }
            if (start > 0) { _pending.RemoveRange(0, start); }
            int headerEnd = -1, bodyStart = -1;
            for (int i = 0; i < _pending.Count - 1; i++)
            {
                if (_pending[i] != 10) { continue; }
                if (_pending[i + 1] == 10) { headerEnd = i; bodyStart = i + 2; break; }
                if (i + 2 < _pending.Count && _pending[i + 1] == 13 && _pending[i + 2] == 10)
                { headerEnd = i; bodyStart = i + 3; break; }
            }
            if (headerEnd < 0) { return false; }
            var lines = Encoding.UTF8.GetString(_pending.GetRange(0, headerEnd).ToArray()).Split('\n');
            var headers = new Dictionary<string, string>();
            string command = lines[0].TrimEnd('\r');
            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i].TrimEnd('\r');
                int colon = line.IndexOf(':');
                if (colon <= 0) { throw new FormatException("Invalid STOMP header."); }
                bool raw = command == "CONNECTED" || command == "CONNECT";
                string key = raw ? line.Substring(0, colon) : Unescape(line.Substring(0, colon));
                string value = raw ? line.Substring(colon + 1) : Unescape(line.Substring(colon + 1));
                if (!headers.ContainsKey(key)) { headers.Add(key, value); }
            }
            int end;
            if (headers.TryGetValue("content-length", out var length))
            {
                if (!int.TryParse(length, NumberStyles.None, CultureInfo.InvariantCulture, out int size) || size > MaxFrameBytes)
                { throw new FormatException("Invalid STOMP content-length."); }
                end = bodyStart + size;
                if (end >= _pending.Count) { return false; }
                if (_pending[end] != 0) { throw new FormatException("Missing STOMP NUL."); }
            }
            else
            {
                end = _pending.IndexOf(0, bodyStart);
                if (end < 0) { return false; }
            }
            frame = new Frame
            {
                Command = command,
                Headers = headers,
                Body = Encoding.UTF8.GetString(_pending.GetRange(bodyStart, end - bodyStart).ToArray())
            };
            _pending.RemoveRange(0, end + 1);
            return true;
        }

        private static string Unescape(string value)
        {
            var result = new StringBuilder();
            for (int i = 0; i < value.Length; i++)
            {
                if (value[i] != '\\') { result.Append(value[i]); continue; }
                if (++i == value.Length) { throw new FormatException("Invalid STOMP escape."); }
                result.Append(value[i] switch
                {
                    'n' => '\n',
                    'r' => '\r',
                    'c' => ':',
                    '\\' => '\\',
                    _ => throw new FormatException("Invalid STOMP escape.")
                });
            }
            return result.ToString();
        }
    }
}
