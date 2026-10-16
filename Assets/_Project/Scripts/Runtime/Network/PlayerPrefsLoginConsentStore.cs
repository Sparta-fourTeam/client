using System;
using Game.Core;
using UnityEngine;

namespace Game.Network
{
    /// <summary>현재 고지 버전의 기기별 선택을 보관한다. 미선택도 저장하며 계정별 동의 증명으로 간주하지 않는다.</summary>
    public sealed class PlayerPrefsLoginConsentStore : ILoginConsentStore
    {
        public const string DefaultKey = "service.consent";
        private readonly string _key;
        public PlayerPrefsLoginConsentStore(string key = DefaultKey) => _key = key;

        [Serializable]
        private sealed class Record
        {
            public string revision, acceptedAtUtc;
            public bool termsAccepted, analyticsAccepted, transferAccepted;
        }

        public bool TryLoad(out LoginConsent consent)
        {
            consent = default;
            string json = PlayerPrefs.GetString(_key, string.Empty);
            if (string.IsNullOrEmpty(json)) { return false; }
            Record record;
            try { record = JsonUtility.FromJson<Record>(json); }
            catch (ArgumentException) { return false; }
            if (record == null || record.revision != LoginConsent.Revision || !record.termsAccepted ||
                !DateTimeOffset.TryParse(record.acceptedAtUtc, out _)) { return false; }
            consent = new LoginConsent(true, record.analyticsAccepted, record.transferAccepted);
            return true;
        }

        public void Save(LoginConsent consent)
        {
            if (!consent.TermsAccepted) { throw new ArgumentException("약관 동의를 확인한 선택만 저장할 수 있습니다."); }
            var record = new Record
            {
                revision = LoginConsent.Revision,
                acceptedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
                termsAccepted = true,
                analyticsAccepted = consent.AnalyticsAccepted,
                transferAccepted = consent.AnalyticsTransferAccepted,
            };
            PlayerPrefs.SetString(_key, JsonUtility.ToJson(record));
            PlayerPrefs.Save();
        }

        public void Clear() { PlayerPrefs.DeleteKey(_key); PlayerPrefs.Save(); }
    }
}
