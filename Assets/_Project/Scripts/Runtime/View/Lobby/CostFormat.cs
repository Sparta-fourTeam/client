namespace Game.View
{
    public static class CostFormat
    {
        private const string Enough = "#7BE05A";
        private const string Short = "#FF5A5A";

        /// <summary>"보유/필요". 보유량이 충분하면 초록, 모자라면 빨강으로 칠한다</summary>
        public static string HaveNeed(int have, int need)
        {
            string color = have >= need ? Enough : Short;
            return $"<color={color}>{CurrencyFormat.Format(have)}</color>/{CurrencyFormat.Format(need)}";
        }
    }
}
