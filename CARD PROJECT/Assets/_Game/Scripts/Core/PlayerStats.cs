using UnityEngine;

namespace LordOfTheRealms
{
    // Statistika igraca: ukupni W/L, po rasi, win streak. Sve u PlayerPrefs.
    public static class PlayerStats
    {
        private static int GetI(string k) => PlayerPrefs.GetInt("stat_" + k, 0);
        private static void SetI(string k, int v) => PlayerPrefs.SetInt("stat_" + k, v);

        public static int TotalGames => GetI("games");
        public static int TotalWins => GetI("wins");
        public static int BestStreak => GetI("best_streak");
        public static int CurrentStreak => GetI("cur_streak");
        public static int WinsFor(Race r) => GetI($"w_{r}");
        public static int LossesFor(Race r) => GetI($"l_{r}");

        // zove se jednom na kraju svakog meca
        public static void Record(bool won, Race playerRace)
        {
            SetI("games", TotalGames + 1);
            if (won)
            {
                SetI("wins", TotalWins + 1);
                SetI($"w_{playerRace}", WinsFor(playerRace) + 1);
                int streak = CurrentStreak + 1;
                SetI("cur_streak", streak);
                if (streak > BestStreak) SetI("best_streak", streak);
            }
            else
            {
                SetI($"l_{playerRace}", LossesFor(playerRace) + 1);
                SetI("cur_streak", 0);
            }
            PlayerPrefs.Save();
        }

        // tekst za prikaz u multiplayer/profil ekranu
        public static string Summary()
        {
            int g = TotalGames, w = TotalWins, l = g - w;
            if (g == 0) return Localization.T("stats.career") + "\n" + Localization.T("stats.nomatches");
            int pct = Mathf.RoundToInt(100f * w / g);
            var sb = new System.Text.StringBuilder();
            sb.AppendLine(string.Format(Localization.T("stats.career_line"), w, l, pct, BestStreak));
            foreach (Race r in System.Enum.GetValues(typeof(Race)))
            {
                int rw = WinsFor(r), rl = LossesFor(r);
                if (rw + rl > 0) sb.AppendLine($"{r}: {rw}-{rl}");
            }
            return sb.ToString().TrimEnd();
        }
    }
}
