using System.Collections.Generic;
using UnityEngine;

namespace LordOfTheRealms
{
    // Povijest ranked meceva (zadnjih 50), spremljena u PlayerPrefs.
    // Zapis = result|pRace|pHero|pRating|oppRace|oppHero|oppRating|delta|vsHuman
    // Zadnje polje je dodano naknadno, pa stariji zapisi bez njega vrijede kao PVE.
    public static class MatchHistory
    {
        private const string Key = "ranked_history";
        private const int MaxEntries = 50;

        public struct Entry
        {
            public bool Won;
            public Race PlayerRace; public string PlayerHero; public int PlayerRating;
            public Race OppRace; public string OppHero; public int OppRating;
            public int Delta;
            // je li protivnik bio pravi igrac; false znaci bot
            public bool VsHuman;
        }

        // dodaj mec na vrh liste (najnoviji prvi)
        public static void Add(Entry e)
        {
            var list = LoadRaw();
            list.Insert(0,
                $"{(e.Won ? 1 : 0)}|{(int)e.PlayerRace}|{e.PlayerHero}|{e.PlayerRating}|" +
                $"{(int)e.OppRace}|{e.OppHero}|{e.OppRating}|{e.Delta}|{(e.VsHuman ? 1 : 0)}");
            while (list.Count > MaxEntries) list.RemoveAt(list.Count - 1);
            PlayerPrefs.SetString(Key, string.Join("\n", list));
            PlayerPrefs.Save();
        }

        public static List<Entry> Load()
        {
            var result = new List<Entry>();
            foreach (var line in LoadRaw())
            {
                var p = line.Split('|');
                if (p.Length < 8) continue;
                result.Add(new Entry
                {
                    Won = p[0] == "1",
                    PlayerRace = (Race)int.Parse(p[1]), PlayerHero = p[2], PlayerRating = int.Parse(p[3]),
                    OppRace = (Race)int.Parse(p[4]), OppHero = p[5], OppRating = int.Parse(p[6]),
                    Delta = int.Parse(p[7]),
                    // stariji zapisi nemaju to polje, pa se broje kao mec protiv bota
                    VsHuman = p.Length >= 9 && p[8] == "1",
                });
            }
            return result;
        }

        private static List<string> LoadRaw()
        {
            var s = PlayerPrefs.GetString(Key, "");
            return string.IsNullOrEmpty(s) ? new List<string>() : new List<string>(s.Split('\n'));
        }
    }
}
