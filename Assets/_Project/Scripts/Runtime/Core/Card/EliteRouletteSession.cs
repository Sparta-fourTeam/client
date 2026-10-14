using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    /// <summary>한 엘리트의 룰렛 보상. 강화 카드 배치는 고정하고, 이미 당첨된 카드는 다시 뽑지 않는다</summary>
    public sealed class EliteRouletteSession
    {
        public const int SlotCount = 8;
        public const int MaxCards = 5;
        private readonly CardDeck _deck;
        private readonly IRandomProvider _random;
        private readonly int _minCards, _maxCards;
        private readonly HashSet<(int? WeaponId, string CardId)> _selectedCards = new();

        public IReadOnlyList<UpgradeChoice> Slots { get; private set; } = Array.Empty<UpgradeChoice>();
        public int CardCount { get; private set; }
        public int CompletedRolls { get; private set; }
        public bool IsComplete => CompletedRolls >= CardCount && PendingRound == null;
        public EliteRouletteRound PendingRound { get; private set; }

        public EliteRouletteSession(CardDeck deck, IRandomProvider random, int minCards, int maxCards)
        {
            if (minCards < 0 || maxCards < minCards || maxCards > MaxCards || minCards == 0 && maxCards != 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxCards));
            }

            _deck = deck;
            _random = random;
            _minCards = minCards;
            _maxCards = maxCards;
        }

        public bool Start()
        {
            Cancel();
            if (_maxCards == 0) { return false; }
            var candidates = _deck.GetWeaponUpgradeCandidates().GroupBy(CardKey)
                .Select(group => group.First()).ToArray();
            if (candidates.Length == 0) { return false; }
            int maximum = Math.Min(_maxCards, Math.Min(candidates.Length, SlotCount));
            int minimum = Math.Min(_minCards, maximum);
            CardCount = Math.Clamp((int)_random.Range(minimum, maximum + 1), minimum, maximum);
            for (int i = candidates.Length - 1; i > 0; i--)
            {
                int j = Index(i + 1);
                (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
            }
            // 같은 무기의 다른 강화는 별개의 카드다. 후보가 부족하면 반복 배치해 여덟 칸을 채운다.
            var slots = new UpgradeChoice[SlotCount];
            for (int i = 0; i < slots.Length; i++) { slots[i] = candidates[i % candidates.Length]; }
            for (int i = slots.Length - 1; i > 0; i--)
            {
                int j = Index(i + 1);
                (slots[i], slots[j]) = (slots[j], slots[i]);
            }
            Slots = Array.AsReadOnly(slots);
            return true;
        }

        public EliteRouletteRound PrepareRound()
        {
            if (PendingRound != null) { return PendingRound; }
            if (IsComplete) { return null; }
            // 배치는 유지하되 직전 보상으로 조건이 바뀐 강화 후보는 현재 상태에서 다시 확인한다.
            var candidates = _deck.GetWeaponUpgradeCandidates().GroupBy(CardKey)
                .ToDictionary(group => group.Key, group => group.First());
            var groups = Enumerable.Range(0, Slots.Count)
                .Where(i => Slots[i].skill != null && !IsSlotSelected(i)
                    && candidates.TryGetValue(CardKey(Slots[i]), out var choice)
                    && choice.skill.Data.id == Slots[i].skill.Data.id)
                .GroupBy(i => Slots[i].skill.Data.id).Select(group => group.ToArray()).ToArray();
            if (groups.Length == 0) { CardCount = CompletedRolls; return null; }
            // 칸 수와 강화 카드 수에 관계없이 남은 무기 중 하나를 먼저 뽑는다.
            var group = groups[Index(groups.Length)];
            int winnerIndex = group[Index(group.Length)];
            PendingRound = new EliteRouletteRound(Slots, winnerIndex, candidates[CardKey(Slots[winnerIndex])]);
            return PendingRound;
        }

        public bool TryApply(EliteRouletteRound round)
        {
            if (round == null || !ReferenceEquals(PendingRound, round)) { return false; }
            // 적용이 발행하는 메시지에서 재진입해도 같은 보상이 두 번 지급되지 않는다.
            PendingRound = null;
            CompletedRolls++;
            _selectedCards.Add(CardKey(round.Winner));
            return _deck.TryApply(round.Winner);
        }

        public void Cancel()
        {
            PendingRound = null;
            CardCount = CompletedRolls = 0;
            Slots = Array.Empty<UpgradeChoice>();
            _selectedCards.Clear();
        }

        public bool IsSlotSelected(int index) => index >= 0 && index < Slots.Count && Slots[index].skill != null
            && _selectedCards.Contains(CardKey(Slots[index]));

        // 공유 강화는 무기별 별칭도 같은 카드이고, 일반 강화는 무기 ID와 카드 ID를 함께 식별한다.
        private static (int? WeaponId, string CardId) CardKey(UpgradeChoice choice) =>
            string.IsNullOrEmpty(choice.Option.sharedId) ? ((int?)choice.skill.Data.id, choice.Option.id)
                : (null, choice.Option.sharedId);

        private int Index(int count) => Math.Clamp((int)_random.Range(0f, count), 0, count - 1);
    }

    public sealed class EliteRouletteRound
    {
        public IReadOnlyList<UpgradeChoice> Slots { get; }
        public int WinnerIndex { get; }
        public UpgradeChoice Winner { get; }

        public EliteRouletteRound(IReadOnlyList<UpgradeChoice> slots, int winnerIndex, UpgradeChoice winner)
        {
            Slots = slots;
            WinnerIndex = winnerIndex;
            Winner = winner;
        }
    }
}
