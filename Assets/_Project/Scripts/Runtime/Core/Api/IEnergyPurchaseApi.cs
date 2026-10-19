using System;
using System.Diagnostics;
using Cysharp.Threading.Tasks;

namespace Game.Core
{
    /// <summary>영수증과 별개로 조회한 현재 구매 기간·한도. 카운트다운은 서버 시각과 단조 시계로 계산한다.</summary>
    public sealed class EnergyPurchaseState
    {
        public DateTime PurchaseDay { get; }
        public DateTime ServerTimeUtc { get; }
        public DateTime ResetsAtUtc { get; }
        public int PurchaseCount { get; }
        public int RemainingPurchases { get; }
        public int GemCost { get; }
        public int EnergyGranted { get; }
        public int DailyLimit { get; }
        private readonly long _receivedAt = Stopwatch.GetTimestamp();
        public double RemainingSeconds => Math.Max(0, (ResetsAtUtc - ServerTimeUtc).TotalSeconds
            - (Stopwatch.GetTimestamp() - _receivedAt) / (double)Stopwatch.Frequency);

        public EnergyPurchaseState(DateTime day, DateTime serverTime, DateTime resetsAt,
            int count, int remaining, int cost, int amount, int limit)
        {
            PurchaseDay = day; ServerTimeUtc = serverTime; ResetsAtUtc = resetsAt;
            PurchaseCount = count; RemainingPurchases = remaining; GemCost = cost; EnergyGranted = amount; DailyLimit = limit;
        }
    }

    /// <summary>서버 구매 전용. 미확정 요청은 Recover(Purchase, null)로 같은 키를 복구한다. 광고는 지원하지 않는다.</summary>
    public interface IEnergyPurchaseApi : IEnergyApi
    {
        long SessionGeneration { get; }
        EnergyPurchaseState PurchaseState { get; }
        bool HasPendingPurchase { get; }
        UniTask<PlayerSnapshot> RefreshPurchaseState();
    }
}
