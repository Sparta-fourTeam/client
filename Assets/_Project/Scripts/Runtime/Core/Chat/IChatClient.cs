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
        UniTask RunAsync(Action connected, Action<ChatMessage> received, Action<int> presence, CancellationToken cancellationToken);
        UniTask SendAsync(string content, CancellationToken cancellationToken);
    }
}
