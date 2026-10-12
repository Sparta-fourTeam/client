using Game.Core;

namespace Game.View
{
    /// <summary>목록 칸(등장 요마, 보상)을 눌렀을 때 공용 상세 창에 넣을 내용. 미션 상세, 일시정지 창, 결과 창이 같이 쓴다</summary>
    public static class DetailContent
    {
        /// <summary>몬스터의 이름·등급·특징을 띄우고, 도감 등록 보상을 받을 수 있으면 수령 아이콘도 띄운다. 상세 창이나 데이터가 없으면 아무것도 하지 않는다</summary>
        public static void ShowMonster(DetailPopupView detail, GameDataStore data, MonsterDisplayTable display, StageMonsterItem item)
        {
            if (detail == null || data == null || !data.Monsters.Contains(item.MonsterId))
            {
                return;
            }

            var monster = data.Monsters.GetOrThrow(item.MonsterId);
            detail.Show(item.Icon, item.Name, MonsterTraits.Subtitle(monster),
                MonsterTraits.Description(monster, id => display != null && display.TryGetMonster(id, out var entry) ? entry.displayName : $"#{id}"));
            detail.OfferMonsterReward(item.MonsterId);
        }

        /// <param name="countFormat">부제. {0}에 수량이 들어간다 (예: "최대 {0}개 획득")</param>
        public static void ShowReward(DetailPopupView detail, GameDataStore data, ResultRewardItem item, string countFormat)
        {
            if (detail == null || string.IsNullOrEmpty(item.ItemId))
            {
                return;
            }

            detail.Show(item.Icon, ItemInfo.NameOf(item.ItemId, data), string.Format(countFormat, CurrencyFormat.Format(item.Count)),
                ItemInfo.DescriptionOf(item.ItemId));
        }
    }
}
