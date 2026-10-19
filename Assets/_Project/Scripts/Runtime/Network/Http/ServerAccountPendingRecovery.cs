using Cysharp.Threading.Tasks;
using Game.Core;

namespace Game.Network
{
    /// <summary>로그인 직후 계정별 디스크에 남긴 거래를 같은 키로 복구한 뒤 로비에 진입한다.</summary>
    public sealed class ServerAccountPendingRecovery : IAccountPendingRecovery
    {
        private readonly IAuthTokenStore _tokens;
        private readonly PlayerProfile _profile;
        private readonly ServerClaimApi _claims;
        private readonly HttpUpgradeApi _upgrades;
        private readonly HttpEnergyApi _energy;

        public ServerAccountPendingRecovery(IAuthTokenStore tokens, PlayerProfile profile,
            ServerClaimApi claims, HttpUpgradeApi upgrades, HttpEnergyApi energy)
        { _tokens = tokens; _profile = profile; _claims = claims; _upgrades = upgrades; _energy = energy; }

        public async UniTask Recover()
        {
            long generation = _tokens.Generation;
            await _claims.RecoverPending();
            Ensure(generation);
            if (_upgrades.HasPendingPurchase)
            {
                _profile.Apply(await _upgrades.RecoverPurchase());
                Ensure(generation);
            }
            if (_energy.HasPendingPurchase)
            {
                _profile.Apply(await _energy.Recover(EnergySource.Purchase));
                Ensure(generation);
            }
        }

        private void Ensure(long generation)
        {
            if (_tokens.Generation != generation)
            { throw new ApiException(ApiErrorKind.Rejected, HttpErrorMapper.SessionExpired); }
        }
    }
}
