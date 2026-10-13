using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    /// <summary>한 전투의 아이템 조회·집계. 영구 소유량이나 지급은 변경하지 않는다.</summary>
    public sealed class StageItemCache : IStageItemInfo
    {
        private IReadOnlyList<string> _obtainableItemIds = Array.Empty<string>();
        private readonly Dictionary<string, int> _acquired = new(StringComparer.Ordinal);

        public int StageId { get; private set; }
        public IReadOnlyList<string> ObtainableItemIds => _obtainableItemIds;

        /// <summary>가변 DTO를 수정해도 내부 집계에 영향이 없는 시점별 복사본.</summary>
        public IReadOnlyList<ItemAmount> AcquiredItems => _acquired
            .Select(item => new ItemAmount { itemId = item.Key, quantity = item.Value })
            .ToList().AsReadOnly();

        public int AcquiredQuantity(string itemId) =>
            itemId != null && _acquired.TryGetValue(itemId, out var quantity) ? quantity : 0;

        /// <summary>전투 시작·재시작·전환 시 호출한다. 같은 스테이지여도 획득 내역을 초기화한다.</summary>
        public void Begin(int stageId, IEnumerable<string> obtainableItemIds)
        {
            if (stageId <= 0) { throw new ArgumentOutOfRangeException(nameof(stageId)); }
            if (obtainableItemIds == null) { throw new ArgumentNullException(nameof(obtainableItemIds)); }

            var ids = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var id in obtainableItemIds)
            {
                if (string.IsNullOrWhiteSpace(id))
                {
                    throw new ArgumentException("획득 가능 아이템 ID는 비어 있을 수 없습니다.", nameof(obtainableItemIds));
                }
                if (seen.Add(id)) { ids.Add(id); }
            }

            StageId = stageId;
            _obtainableItemIds = ids.AsReadOnly();
            _acquired.Clear();
        }

        /// <summary>누적 확보량 전체로 획득 내역을 교체한다. 웨이브마다 누적 합계가 다시 계산되는 집계용이다.</summary>
        public void SetAcquired(IEnumerable<ItemAmount> items)
        {
            if (StageId == 0) { throw new InvalidOperationException("전투를 시작한 뒤 획득량을 기록해야 합니다."); }
            if (items == null) { throw new ArgumentNullException(nameof(items)); }

            // 검증이 끝난 뒤에만 교체해 실패 시 기존 집계를 유지한다.
            var next = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var item in items)
            {
                if (item == null || string.IsNullOrWhiteSpace(item.itemId)) { throw new ArgumentException("아이템 ID가 필요합니다.", nameof(items)); }
                if (item.quantity <= 0) { throw new ArgumentOutOfRangeException(nameof(items)); }
                next[item.itemId] = checked(next.TryGetValue(item.itemId, out var current) ? current + item.quantity : item.quantity);
            }

            _acquired.Clear();
            foreach (var item in next) { _acquired[item.Key] = item.Value; }
        }

        /// <summary>지급 모듈이 획득을 확정한 뒤 호출한다. 중복 지급 판단은 지급 모듈에서 한다.</summary>
        public void RecordAcquired(string itemId, int quantity)
        {
            if (StageId == 0) { throw new InvalidOperationException("전투를 시작한 뒤 획득량을 기록해야 합니다."); }
            if (string.IsNullOrWhiteSpace(itemId)) { throw new ArgumentException("아이템 ID가 필요합니다.", nameof(itemId)); }
            if (quantity <= 0) { throw new ArgumentOutOfRangeException(nameof(quantity)); }

            // 덧셈 실패 시 기존 집계는 유지한다.
            _acquired[itemId] = checked(AcquiredQuantity(itemId) + quantity);
        }
    }
}
