using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LordOfTheRealms
{
    // Spremljeni deck igraca za jednu rasu: lista (ime karte, kopije) + ime heroja.
    // Sve zivi u PlayerPrefs kao obican string, pa prezivi sesiju bez asseta.
    // Format: "HeroName;Card1:2;Card2:3;..."
    [System.Serializable]
    public class SavedDeck
    {
        public string heroName;
        public List<CardCount> cards = new();

        [System.Serializable]
        public struct CardCount { public string name; public int copies; }

        public int TotalCards => cards.Sum(c => c.copies) + 1; // +1 hero

        public string Serialize()
        {
            var parts = new List<string> { heroName ?? "" };
            parts.AddRange(cards.Select(c => $"{c.name}:{c.copies}"));
            return string.Join(";", parts);
        }

        public static SavedDeck Deserialize(string data)
        {
            var deck = new SavedDeck();
            if (string.IsNullOrEmpty(data)) return deck;
            var parts = data.Split(';');
            if (parts.Length == 0) return deck;
            deck.heroName = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                var kv = parts[i].Split(':');
                if (kv.Length == 2 && int.TryParse(kv[1], out int copies))
                    deck.cards.Add(new CardCount { name = kv[0], copies = copies });
            }
            return deck;
        }
    }

    // Sprema/ucitava custom deckove po rasi. Opcionalni "slot" razdvaja story i
    // multiplayer deckove (MP ima sve karte, story samo otkljucane), ne smiju se
    // mijesati u istom spremistu).
    public static class DeckStorage
    {
        public const string MpSlot = "mp_"; // legacy alias = MpSlots[0]

        // 3 deck slota po rasi. Story/gauntlet i multiplayer imaju ODVOJENE setove
        // slotova (MP koristi sve karte, story samo otkljucane), ne smiju se mijesati.
        public static readonly string[] Slots   = { "", "s2_", "s3_" };
        public static readonly string[] MpSlots = { "mp_", "mp_s2_", "mp_s3_" };

        // koji set slotova (story ili MP) ovisno o kontekstu
        public static string[] SlotsFor(bool mp) => mp ? MpSlots : Slots;

        private static string ActiveKey(Race race, bool mp)
            => mp ? $"active_mpslot_{race}" : $"active_deckslot_{race}";

        public static int ActiveSlotIndex(Race race, bool mp)
            => Mathf.Clamp(PlayerPrefs.GetInt(ActiveKey(race, mp), 0), 0, SlotsFor(mp).Length - 1);
        public static int ActiveSlotIndex(Race race) => ActiveSlotIndex(race, false);

        public static void SetActiveSlot(Race race, int idx, bool mp)
        {
            PlayerPrefs.SetInt(ActiveKey(race, mp), Mathf.Clamp(idx, 0, SlotsFor(mp).Length - 1));
            PlayerPrefs.Save();
        }
        public static void SetActiveSlot(Race race, int idx) => SetActiveSlot(race, idx, false);

        // deck iz trenutno aktivnog slota te rase (story ili MP set)
        public static SavedDeck LoadActive(Race race, bool mp)
            => Load(race, SlotsFor(mp)[ActiveSlotIndex(race, mp)]);
        public static SavedDeck LoadActive(Race race) => LoadActive(race, false);

        private static string Key(Race race, string slot = "") => $"deck_{slot}{race}";

        public static bool HasCustom(Race race, string slot = "") => PlayerPrefs.HasKey(Key(race, slot));

        public static void Save(Race race, SavedDeck deck, string slot = "")
        {
            PlayerPrefs.SetString(Key(race, slot), deck.Serialize());
            PlayerPrefs.Save();
        }

        public static SavedDeck Load(Race race, string slot = "")
        {
            if (!HasCustom(race, slot)) return Default(race);
            var d = SavedDeck.Deserialize(PlayerPrefs.GetString(Key(race, slot)));
            d.heroName = MigrateHero(d.heroName); // stari WOW heroj -> novi (save-safe)
            return d;
        }

        public static void Reset(Race race, string slot = "")
        {
            PlayerPrefs.DeleteKey(Key(race, slot));
            PlayerPrefs.Save();
        }

        // sagradi SavedDeck iz hard-kodiranog default DeckLists popisa
        public static SavedDeck Default(Race race)
        {
            var deck = new SavedDeck();
            // zbroji kopije po imenu iz DeckLists
            var agg = new Dictionary<string, int>();
            string hero = null;
            foreach (var (name, copies) in DeckLists.For(race))
            {
                // imena heroja sadrze zarez ili su poznata; prepoznaj ih preko library u runtimeu.
                agg.TryGetValue(name, out int cur);
                agg[name] = cur + copies;
            }
            // uzmi prvog heroja rase kao zadanog
            hero = DefaultHero(race);
            // makni heroja iz brojanja cards ako je unutra
            if (hero != null && agg.ContainsKey(hero)) agg.Remove(hero);

            deck.heroName = hero;
            deck.cards = agg.Select(kv => new SavedDeck.CardCount { name = kv.Key, copies = kv.Value }).ToList();
            return deck;
        }

        public static string DefaultHero(Race race) => race switch
        {
            Race.Orcs => "Kragmaw, Warchief",
            Race.Elves => "Faelan, Archdruid",
            Race.Humans => "King Cedric",
            Race.Demons => "Vor'gathul",
            _ => "King Cedric"
        };

        // Stara imena heroja (bila su iz Warcrafta) -> nova originalna. Postojeci
        // savevi cuvaju staro ime kao string; ovo ih mapira da se ne slome.
        private static readonly Dictionary<string, string> HeroRename = new()
        {
            { "Grommash, Warchief",        "Kragmaw, Warchief" },
            { "Gul'mok the Relentless",    "Dro'gan the Relentless" },
            { "Malfurion, Archdruid",      "Faelan, Archdruid" },
            { "Tyrande, Moon Priestess",   "Sylvara, Moon Priestess" },
            { "King Arthas",               "King Cedric" },
            { "Uther the Lightbringer",    "Baelric the Lightbringer" },
            { "Archimonde",                "Vor'gathul" },
            { "Mannoroth the Destructor",  "Zar'thul the Destroyer" },
        };

        public static string MigrateHero(string name)
            => name != null && HeroRename.TryGetValue(name, out var n) ? n : name;
    }
}
