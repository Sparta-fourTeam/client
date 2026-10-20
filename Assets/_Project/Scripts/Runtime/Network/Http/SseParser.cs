using System.Text;

namespace Game.Network
{
    public readonly struct SseEvent
    {
        public SseEvent(string name, string data)
        {
            Name = name;
            Data = data;
        }

        /// <summary>event: 줄의 이름. 없으면 SSE 규칙대로 message</summary>
        public string Name { get; }

        /// <summary>data: 줄을 줄바꿈으로 이은 값</summary>
        public string Data { get; }
    }

    /// <summary>text/event-stream을 한 줄씩 받아 이벤트로 묶는다. 빈 줄이 이벤트의 끝이다.
    /// :로 시작하는 줄(주석·하트비트)과 id·retry 같은 알 수 없는 필드는 무시한다</summary>
    public sealed class SseParser
    {
        private readonly StringBuilder _data = new();
        private string _name;
        private bool _hasData;

        /// <summary>한 줄을 먹인다. 이벤트가 완성되면 true. data가 없는 이벤트는 SSE 규칙대로 내보내지 않는다</summary>
        public bool TryFeed(string line, out SseEvent evt)
        {
            evt = default;
            if (line.Length == 0)
            {
                bool ready = _hasData;
                if (ready)
                {
                    evt = new SseEvent(_name ?? "message", _data.ToString());
                }

                _name = null;
                _data.Clear();
                _hasData = false;
                return ready;
            }

            if (line[0] == ':')
            {
                return false;
            }

            int colon = line.IndexOf(':');
            string field = colon < 0 ? line : line.Substring(0, colon);
            string value = colon < 0 ? string.Empty : line.Substring(colon + 1);
            if (value.Length > 0 && value[0] == ' ')
            {
                value = value.Substring(1);
            }

            switch (field)
            {
                case "event":
                    _name = value;
                    break;
                case "data":
                    if (_hasData)
                    {
                        _data.Append('\n');
                    }

                    _data.Append(value);
                    _hasData = true;
                    break;
            }

            return false;
        }
    }
}
