using Cysharp.Threading.Tasks;

namespace Game.Core
{
    public readonly struct PlayerIdentity
    {
        public string PlayerId { get; }
        public string Nickname { get; }
        public int AvatarId { get; }
        public PlayerIdentity(string playerId, string nickname, int avatarId)
        { PlayerId = playerId; Nickname = nickname; AvatarId = avatarId; }
    }

    public interface IProfileApi
    {
        UniTask<PlayerIdentity> GetProfile();
        UniTask<PlayerIdentity> ChangeAvatar(int avatarId);
    }

    public static class ProfilePortraits
    {
        public const int Count = 5;
        public static bool IsValid(int id) => id >= 0 && id < Count;
    }
}
