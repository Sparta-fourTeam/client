using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Game.Core
{
    public sealed class ChatMessage
    {
        public long SenderId { get; }
        public string Content { get; }
        public string Nickname { get; }
        public int AvatarId { get; }
        public DateTimeOffset SentAt { get; }

        public ChatMessage(long senderId, string content, DateTimeOffset sentAt, string nickname = null, int avatarId = -1)
        {
            Nickname = nickname;
            AvatarId = avatarId;
            SenderId = senderId;
            Content = content;
            SentAt = sentAt;
        }
    }

    public interface IChatClient
    {
        UniTask<IReadOnlyList<ChatMessage>> GetRecentAsync(CancellationToken cancellationToken);
        /// <summary>구독이 서버에서 확인되면 connected를 호출하고, 연결 종료까지 실행한다.
        /// connected 전 수신한 방송도 received로 전달하며, 취소 시 연결을 종료한다.</summary>
        UniTask RunAsync(Action connected, Action<ChatMessage> received, Action<int> presence, CancellationToken cancellationToken);
        UniTask SendAsync(string content, CancellationToken cancellationToken);
    }
}
