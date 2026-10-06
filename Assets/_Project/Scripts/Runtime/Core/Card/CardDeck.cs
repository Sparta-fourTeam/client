using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core.Defense;
using VContainer;

namespace Game.Core
{
    /// <summary>카드 3장을 뽑고 적용하는 한 판 단위 서비스. 스킬 카드는 IUpgradeChoiceSource(WeaponController)가,
    /// 일반 카드(방벽 회복 등)는 GeneralCards 테이블이 후보를 내고, 여기서 하나로 합친다</summary>
    public sealed class CardDeck
    {
        /// <summary>한 번 뽑을 때 Forced 일반 카드가 차지할 수 있는 최대 칸 수. 스킬 카드가 완전히 밀려나지 않게 한다</summary>
        public const int MaxForcedPerDraw = 1;

        private readonly IUpgradeChoiceSource _skills;
        private readonly GeneralCardContext _context;
        private readonly IReadOnlyList<GeneralCardDefinition> _general;
        private readonly IRandomProvider _random;
        private readonly Dictionary<string, int> _picks = new();

        [Inject]
        public CardDeck(IUpgradeChoiceSource skills, Wall wall, GameDataStore data, IRandomProvider random)
            : this(skills, wall, data.GeneralCards, random)
        {
        }

        public CardDeck(IUpgradeChoiceSource skills, Wall wall, IReadOnlyList<GeneralCardDefinition> general, IRandomProvider random)
        {
            _skills = skills;
            _context = new GeneralCardContext(wall);
            _general = general;
            _random = random;
        }

        public int GetPickCount(string cardId) => _picks.TryGetValue(cardId, out var count) ? count : 0;

        public List<UpgradeChoice> Draw(int count)
        {
            var result = new List<UpgradeChoice>();
            if (count <= 0)
            {
                return result;
            }

            var offered = _general.Where(CanOffer).ToList();

            // Forced 카드는 우선순위가 높은 것부터, 같으면 무작위로 정해 자리를 먼저 채운다
            var forced = offered.Where(card => card.Forced).ToList();
            Shuffle(forced);
            forced = forced.OrderByDescending(card => card.Priority).Take(Math.Min(MaxForcedPerDraw, count)).ToList();
            result.AddRange(forced.Select(ToChoice));

            // 나머지 자리는 스킬 후보와 Forced가 아닌 일반 카드를 섞어서 뽑는다
            var pool = _skills.GetUpgradeCandidates();
            pool.AddRange(offered.Where(card => !card.Forced).Select(ToChoice));
            Shuffle(pool);
            result.AddRange(pool.Take(count - result.Count));

            // 방벽 카드가 늘 맨 앞에 오지 않게 한 번 섞는다
            Shuffle(result);
            return result;
        }

        public bool TryApply(UpgradeChoice choice)
        {
            if (!choice.IsGeneral)
            {
                return _skills.ApplyUpgradeChoice(choice);
            }

            // 뽑은 뒤 상태가 바뀌었을 수 있어(이미 회복됨 등) 한 번 더 확인한다
            var card = choice.GeneralCard;
            if (!CanOffer(card))
            {
                return false;
            }

            GeneralCardRules.Apply(card.Effect, _context);
            _picks[card.Id] = GetPickCount(card.Id) + 1;
            return true;
        }

        private bool CanOffer(GeneralCardDefinition card) =>
            GetPickCount(card.Id) < card.MaxPicks && GeneralCardRules.IsMet(card.Condition, _context);

        private static UpgradeChoice ToChoice(GeneralCardDefinition card) => new UpgradeChoice
        {
            GeneralCard = card,
            DisplayName = card.Name,
            DisplayDescription = card.Desc,
        };

        private void Shuffle<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Math.Clamp((int)_random.Range(0f, i + 1), 0, i);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
