using System;

namespace Game.Network
{
    /// <summary>서버 주소와 요청 제한 시간. 어디서 읽어올지는 Local/Remote 선택을 연결하는 쪽이 정한다</summary>
    public sealed class HttpApiConfig
    {
        public HttpApiConfig(string baseUrl, float timeoutSeconds = 10f)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                throw new ArgumentException("서버 주소가 비어 있습니다", nameof(baseUrl));
            }

            if (timeoutSeconds <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(timeoutSeconds), "제한 시간은 0보다 커야 합니다");
            }

            BaseUrl = baseUrl.Trim().TrimEnd('/');
            TimeoutSeconds = timeoutSeconds;
        }

        /// <summary>끝의 / 를 뺀 서버 주소 (예: https://example.com)</summary>
        public string BaseUrl { get; }

        public float TimeoutSeconds { get; }
    }
}
