using System;
using System.Collections.Generic;
using UnityEngine;

namespace LordOfTheRealms
{
    // Achievementi: definicija + uvjet, otkljucano stanje u PlayerPrefs.
    // CheckAll() se zove nakon svakog meca; bez rewarda, samo bragging rights.
    public static class Achievements
    {
        public struct Def
        {
            public string Id, Title, Desc;
            public Func<bool> Check;
        }

        public static readonly Def[] All =
        {
            new Def{ Id="first_win", Title="First Blood",
                Desc="Win your first match.",
                Check=() => PlayerStats.TotalWins >= 1 },
            new Def{ Id="games10", Title="Veteran",
                Desc="Play 10 matches.",
                Check=() => PlayerStats.TotalGames >= 10 },
            new Def{ Id="streak5", Title="Unstoppable",
                Desc="Win 5 matches in a row.",
                Check=() => PlayerStats.BestStreak >= 5 },
            new Def{ Id="elo2000", Title="Climber",
                Desc="Reach 2000 ELO in ranked.",
                Check=() => RankedSystem.Elo >= 2000 },
            new Def{ Id="story_final", Title="Realm Breaker",
                Desc="Defeat the Hollow King in the final battle.",
                Check=() => StoryProgress.IsDone("Final") },
            new Def{ Id="golden", Title="Gilded",
                Desc="Unlock a golden card frame (finish a campaign).",
                Check=() => GoldenFrames.Has(Race.Orcs) || GoldenFrames.Has(Race.Elves)
                         || GoldenFrames.Has(Race.Humans) || GoldenFrames.Has(Race.Demons) },
            // gauntlet: bez rewarda, samo slava
            new Def{ Id="gauntlet3", Title="Gauntlet Initiate",
                Desc="Win 3 battles in a single gauntlet run.",
                Check=() => BestGauntlet >= 3 },
            new Def{ Id="gauntlet6", Title="Gauntlet Veteran",
                Desc="Win 6 battles in a single gauntlet run.",
                Check=() => BestGauntlet >= 6 },
            new Def{ Id="gauntlet10", Title="Gauntlet Legend",
                Desc="Win 10 battles in a single gauntlet run.",
                Check=() => BestGauntlet >= 10 },

            // ---- card collecting ----
            new Def{ Id="collect_first", Title="Collector",
                Desc="Unlock your first story reward card.",
                Check=() => UnlockedCardCount() >= 1 },
            new Def{ Id="collect_race", Title="Race Complete",
                Desc="Unlock every reward card of a single race.",
                Check=() => RaceFullyCollected() },
            new Def{ Id="collect_50", Title="Hoarder",
                Desc="Unlock 50 reward cards across all races.",
                Check=() => UnlockedCardCount() >= 50 },
            new Def{ Id="collect_all", Title="Master Collector",
                Desc="Unlock every reward card in the game.",
                Check=() => UnlockedCardCount() >= TotalRewardCards() },
            new Def{ Id="second_hero", Title="Twin Champions",
                Desc="Unlock a second hero for any race.",
                Check=() => AnySecondHeroUnlocked() },
            new Def{ Id="all_heroes", Title="Hall of Heroes",
                Desc="Unlock the second hero of all four races.",
                Check=() => AllSecondHeroesUnlocked() },

            // ---- misc cool ----
            new Def{ Id="all_golden", Title="All That Glitters",
                Desc="Unlock the golden frame for all four races.",
                Check=() => GoldenFrames.Has(Race.Orcs) && GoldenFrames.Has(Race.Elves)
                         && GoldenFrames.Has(Race.Humans) && GoldenFrames.Has(Race.Demons) },
            new Def{ Id="elo2500", Title="Grandmaster",
                Desc="Reach 2500 ELO in ranked.",
                Check=() => RankedSystem.Elo >= 2500 },
            new Def{ Id="games50", Title="Battle-Hardened",
                Desc="Play 50 matches.",
                Check=() => PlayerStats.TotalGames >= 50 },
        };

        // ---- helperi za collecting achievemente ----

        private static readonly Race[] Races = { Race.Orcs, Race.Elves, Race.Humans, Race.Demons };

        // sve otkljucive (reward) karte rase: 18 iz RewardOrder + drugi heroj
        private static IEnumerable<string> RewardCardsOf(Race race)
        {
            foreach (var c in StoryData.RewardCards(race)) yield return c;
            yield return StoryData.SecondHero(race);
        }

        private static int TotalRewardCards()
        {
            int t = 0;
            foreach (var r in Races) foreach (var _ in RewardCardsOf(r)) t++;
            return t;
        }

        private static int UnlockedCardCount()
        {
            int t = 0;
            foreach (var r in Races)
                foreach (var c in RewardCardsOf(r))
                    if (StoryProgress.IsCardUnlocked(r, c)) t++;
            return t;
        }

        private static bool RaceFullyCollected()
        {
            foreach (var r in Races)
            {
                bool all = true;
                foreach (var c in RewardCardsOf(r))
                    if (!StoryProgress.IsCardUnlocked(r, c)) { all = false; break; }
                if (all) return true;
            }
            return false;
        }

        private static bool AnySecondHeroUnlocked()
        {
            foreach (var r in Races)
                if (StoryProgress.IsCardUnlocked(r, StoryData.SecondHero(r))) return true;
            return false;
        }

        private static bool AllSecondHeroesUnlocked()
        {
            foreach (var r in Races)
                if (!StoryProgress.IsCardUnlocked(r, StoryData.SecondHero(r))) return false;
            return true;
        }

        public static int BestGauntlet => PlayerPrefs.GetInt("gauntlet_best", 0);

        public static bool IsUnlocked(string id) => PlayerPrefs.GetInt("ach_" + id, 0) == 1;

        // lokalizirani naslov/opis (EN tekst u All[] sluzi kao fallback izvor)
        public static string TitleOf(Def d) => Localization.T("ach." + d.Id + ".title");
        public static string DescOf(Def d) => Localization.T("ach." + d.Id + ".desc");

        // Napredak za achievemente koji nesto broje, npr. "7 / 50". Vraca prazno
        // za one koji se ili imaju ili nemaju, jer im brojka ne znaci nista.
        public static string ProgressOf(Def d)
        {
            int cur, target;
            switch (d.Id)
            {
                case "games10":    cur = PlayerStats.TotalGames; target = 10; break;
                case "games50":    cur = PlayerStats.TotalGames; target = 50; break;
                case "streak5":    cur = PlayerStats.BestStreak; target = 5; break;
                case "elo2000":    cur = RankedSystem.Elo; target = 2000; break;
                case "elo2500":    cur = RankedSystem.Elo; target = 2500; break;
                case "gauntlet3":  cur = BestGauntlet; target = 3; break;
                case "gauntlet6":  cur = BestGauntlet; target = 6; break;
                case "gauntlet10": cur = BestGauntlet; target = 10; break;
                default: return "";
            }
            if (cur > target) cur = target;
            return $"{cur} / {target}";
        }

        // provjeri sve uvjete i trajno otkljucaj ispunjene; vraca novo-otkljucane
        public static List<Def> CheckAll()
        {
            var fresh = new List<Def>();
            foreach (var d in All)
            {
                if (IsUnlocked(d.Id)) continue;
                bool ok;
                try { ok = d.Check(); } catch { ok = false; }
                if (!ok) continue;
                PlayerPrefs.SetInt("ach_" + d.Id, 1);
                fresh.Add(d);
            }
            if (fresh.Count > 0) PlayerPrefs.Save();
            return fresh;
        }
    }
}
