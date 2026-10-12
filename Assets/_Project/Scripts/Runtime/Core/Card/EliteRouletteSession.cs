using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    /// <summary>한 엘리트의 룰렛 보상. 다음 회차는 직전 강화가 적용된 뒤의 후보로 다시 만든다</summary>
    public sealed class EliteRouletteSession
    {
        public const int SlotCount = 8;
        public const int MaxCards = 5;
        private readonly CardDeck _deck;
        private readonly IRandomProvider _random;
        private readonly int _minCards, _maxCards;

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
            if (_maxCards == 0 || _deck.GetWeaponUpgradeCandidates().Count == 0) { return false; }
            CardCount = Math.Clamp((int)_random.Range(_minCards, _maxCards + 1), _minCards, _maxCards);
            CompletedRolls = 0;
            return true;
        }

        public EliteRouletteRound PrepareRound()
        {
            if (PendingRound != null) { return PendingRound; }
            if (IsComplete) { return null; }
            // 카드가 많은 무기가 확률까지 독점하지 않게 무기를 먼저 뽑고, 그 안에서 강화를 뽑는다.
            var groups = _deck.GetWeaponUpgradeCandidates().GroupBy(choice => choice.skill.Data.id)
                .Select(group => group.ToArray()).ToArray();
            if (groups.Length == 0) { CompletedRolls = CardCount; return null; }
            var slots = new UpgradeChoice[SlotCount];
            for (int i = 0; i < slots.Length; i++)
            {
                var group = groups[Index(groups.Length)];
                slots[i] = group[Index(group.Length)];
            }
            PendingRound = new EliteRouletteRound(Array.AsReadOnly(slots), Index(slots.Length));
            return PendingRound;
        }

        public bool TryApply(EliteRouletteRound round)
        {
            if (round == null || !ReferenceEquals(PendingRound, round)) { return false; }
            // 적용이 발행하는 메시지에서 재진입해도 같은 보상이 두 번 지급되지 않는다.
            PendingRound = null;
            CompletedRolls++;
            return _deck.TryApply(round.Winner);
        }

        public void Cancel()
        {
            PendingRound = null;
            CardCount = CompletedRolls = 0;
        }

        private int Index(int count) => Math.Clamp((int)_random.Range(0f, count), 0, count - 1);
    }

    public sealed class EliteRouletteRound
    {
        public IReadOnlyList<UpgradeChoice> Slots { get; }
        public int WinnerIndex { get; }
        public UpgradeChoice Winner => Slots[WinnerIndex];

        public EliteRouletteRound(IReadOnlyList<UpgradeChoice> slots, int winnerIndex)
        {
            Slots = slots;
            WinnerIndex = winnerIndex;
        }
    }
}
