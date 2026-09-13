using UnityEngine;

namespace LordOfTheRealms
{
    // Ranked 1v1 elo sustav.
    // Rank ide 0 - 3000+, start 1500. Svakih 500 ela = jaci bot protivnik,
    // iznad 3000 je leaderboard teritorij. Dobitak/gubitak ela ovisi o razlici
    // ratinga (standardna Elo formula, K=32).
    public static class RankedSystem
    {
        private const string EloKey = "ranked_elo";
        private const float K = 32f;

        public static int Elo
        {
            get => PlayerPrefs.GetInt(EloKey, 1500);
            private set { PlayerPrefs.SetInt(EloKey, Mathf.Max(0, value)); PlayerPrefs.Save(); }
        }

        // ime ranga po bracketu od 500
        public static string RankName(int elo)
        {
            if (elo >= 3000) return "LEADERBOARD";
            return (elo / 500) switch
            {
                0 => "Bronze",
                1 => "Silver",
                2 => "Gold",
                3 => "Platinum",
                4 => "Diamond",
                _ => "Master",
            };
        }

        // bot difficulty po bracketu: 0-499 najlaksi ... 2500+ najtezi (1..6)
        public static int DifficultyFor(int elo)
            => Mathf.Clamp(elo / 500 + 1, 1, 6);

        // rating "protivnika" za elo racun: sredina bracketa + sum, PLUS bonus po
        // win streaku; na streaku dobivas jace protivnike, pa Elo formula sama da
        // vise ela za win i manje oduzme za loss (dobri igraci brze dodu na svoj rank)
        public static int OpponentRating(int elo)
        {
            int bracket = Mathf.Clamp(elo / 500, 0, 5);
            int streakBonus = Mathf.Min(PlayerStats.CurrentStreak, 6) * 70; // max +420
            return bracket * 500 + 250 + streakBonus + Random.Range(-120, 121);
        }

        // primijeni rezultat: vraca elo deltu (moze biti negativna)
        public static int ApplyResult(bool won, int opponentRating)
        {
            float expected = 1f / (1f + Mathf.Pow(10f, (opponentRating - Elo) / 400f));
            int delta = Mathf.RoundToInt(K * ((won ? 1f : 0f) - expected));
            // pobjeda uvijek daje bar +1, poraz bar -1, da nema mrtvih meceva
            if (won && delta < 1) delta = 1;
            if (!won && delta > -1) delta = -1;
            Elo += delta;
            return delta;
        }
    }
}
