using System;
using Game.Core;
using UnityEngine;

namespace Game.Network
{
    /// <summary>로그인 정보(방식과 계정 ID)를 PlayerPrefs에 저장한다. 로컬·서버 백엔드와 상관없이 기기에 남는다.
    /// TODO(server): 서버 자격 증명(비밀값 포함)을 보관하게 되면 보안 저장소(Keychain/Keystore)로 바꾼다</summary>
    public sealed class PlayerPrefsAuthCredentialStore : IAuthCredentialStore
    {
        private const string MethodKey = "auth.method";
        private const string AccountIdKey = "auth.account_id";

        public bool HasCredential => AccountId != null;

        public LoginMethod Method =>
            Enum.TryParse(PlayerPrefs.GetString(MethodKey, string.Empty), out LoginMethod method) ? method : LoginMethod.Guest;

        public string AccountId
        {
            get
            {
                string value = PlayerPrefs.GetString(AccountIdKey, string.Empty);
                return string.IsNullOrEmpty(value) ? null : value;
            }
        }

        public void Save(LoginMethod method, string accountId)
        {
            PlayerPrefs.SetString(MethodKey, method.ToString());
            PlayerPrefs.SetString(AccountIdKey, accountId);
            PlayerPrefs.Save();
        }

        public void Clear()
        {
            PlayerPrefs.DeleteKey(MethodKey);
            PlayerPrefs.DeleteKey(AccountIdKey);
            PlayerPrefs.Save();
        }
    }
}
