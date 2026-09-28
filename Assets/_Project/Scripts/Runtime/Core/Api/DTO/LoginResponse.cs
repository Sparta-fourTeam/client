using System;

namespace Game.Core
{
    [Serializable]
    public class LoginResponse
    {
        public string accessToken;
        public int dataRevision;
    }
}
