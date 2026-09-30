namespace Game.View
{
    public static class CurrencyFormat
    {
        public static string Format(int value)
        {
            if (value >= 1_000_000)
            {
                return $"{value / 1_000_000f:0.#}M";
            }

            if (value >= 10_000)
            {
                return $"{value / 1_000f:0.#}K";
            }

            return value.ToString("N0");
        }
    }
}
