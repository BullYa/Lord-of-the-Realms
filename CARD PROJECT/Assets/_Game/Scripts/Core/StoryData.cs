using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LordOfTheRealms
{
    // Jedan battle node na story mapi.
    public class StoryNode
    {
        public string Id;            // e.g. "Orcs_3"
        public Race EnemyRace;
        public BotDifficulty Difficulty;
        public string EnemyHeroName; // which hero the enemy deck runs
        public int IndexInRegion;    // 1..5, or 0 for the final battle
        public bool IsFinal;
    }

    // Raspored cijele kampanje: 4 regije (jedna po rasi) x 5 bitaka, tezina raste
    // 1..5 unutar regije, plus jedna zavrsna bitka (tezina 6) u sredini koja se
    // otvori kad su sve regije ociscene.
    //
    // Rewardi: svaki level daje FIKSNU kartu igraceve rase (RewardOrder dolje),
    // Demons 5 daje drugog heroja, finale daje golden okvir. Drugi heroj je zakljucan
    // dok se ne osvoji (ili izabere u New Game).
    public static class StoryData
    {
        public static readonly Race[] RegionOrder =
            { Race.Orcs, Race.Elves, Race.Humans, Race.Demons };

        // dva heroja koja svaka rasa moze izvesti (prvi = zadani)
        private static readonly Dictionary<Race, string[]> Heroes = new()
        {
            { Race.Orcs,   new[]{ "Kragmaw, Warchief", "Dro'gan the Relentless" } },
            { Race.Elves,  new[]{ "Faelan, Archdruid", "Sylvara, Moon Priestess" } },
            { Race.Humans, new[]{ "King Cedric", "Baelric the Lightbringer" } },
            { Race.Demons, new[]{ "Vor'gathul", "Zar'thul the Destroyer" } },
        };

        private static List<StoryNode> _nodes;

        public static IReadOnlyList<StoryNode> AllNodes
        {
            get
            {
                if (_nodes != null) return _nodes;
                _nodes = new List<StoryNode>();
                foreach (var race in RegionOrder)
                {
                    for (int i = 1; i <= 5; i++)
                    {
                        _nodes.Add(new StoryNode
                        {
                            Id = $"{race}_{i}",
                            EnemyRace = race,
                            Difficulty = (BotDifficulty)i,
                            // neparne bitke koriste prvog heroja rase, parne drugog
                            EnemyHeroName = Heroes[race][(i % 2 == 1) ? 0 : 1],
                            IndexInRegion = i,
                            IsFinal = false
                        });
                    }
                }
                // zavrsna bitka u sredini mape
                _nodes.Add(new StoryNode
                {
                    Id = "Final",
                    EnemyRace = Race.Demons,
                    Difficulty = BotDifficulty.Nightmare,
                    EnemyHeroName = "Vor'gathul",
                    IndexInRegion = 0,
                    IsFinal = true
                });
                return _nodes;
            }
        }

        public static StoryNode Get(string id) => AllNodes.FirstOrDefault(n => n.Id == id);

        public static IEnumerable<StoryNode> RegionNodes(Race race)
            => AllNodes.Where(n => !n.IsFinal && n.EnemyRace == race).OrderBy(n => n.IndexInRegion);

        // ---- story rewardi: FIKSNO po levelu ----
        // Svih 20 regijskih borbi daje reward: borbe 1-19 karte tvoje rase redom iz
        // liste (19 karata), Demons 5 (zadnja) daje DRUGOG HEROJA, finale golden okvir.
        private static readonly Dictionary<Race, string[]> RewardOrder = new()
        {
            { Race.Orcs, new[]{ "Axe Thrower", "War Pup", "Goblin Sapper", "Raider",
                "Chieftain's Guard", "War Drummer", "Sharpen Blades", "Goblin Bomb",
                "War Horn", "Siege Ogre", "Rampage", "Ogre Bruiser", "Second Wind",
                "Gorehowl Colossus", "Bone Crusher", "Bear Trap", "Horde Vanguard",
                "Earthshaker", "Ashmere Outrider" } },
            { Race.Elves, new[]{ "Sapling", "Moon Owl", "Dryad", "Grove Warden",
                "Grove Sentinel", "Wild Growth", "Wisdom of Ages", "Moon Priestess",
                "Starfall", "Emerald Drake", "Tranquility", "Treant Guardian",
                "Nature's Wrath", "Forest Avatar", "Faerie Dragon", "Moonlit Snare",
                "Ancient of War", "Rejuvenation", "Silverwood Ranger" } },
            { Race.Humans, new[]{ "Militia", "Squire", "Chaplain", "Crossbowman",
                "Captain of the Watch", "Smite", "Field Medic", "Blessing",
                "Call to Arms", "Siege Tower", "Battle Standard", "Paladin",
                "Judgement", "Grand Marshal", "Templar", "Holy Ward",
                "Siege Commander", "Divine Storm", "Highmarch Halberdier" } },
            { Race.Demons, new[]{ "Void Imp", "Tormentor", "Fel Imp", "Shadow Fiend",
                "Dark Ritual", "Soul Siphon", "Succubus", "Dark Bargain",
                "Void Ritualist", "Immolate", "Doom Guard", "Hellfire", "Pit Lord",
                "Abyssal Titan", "Nightmare Steed", "Fel Snare", "Void Terror",
                "Chaos Bolt", "Cinder Fiend" } },
        };

        // drugi (ne-default) heroj rase, story reward na zadnjoj regijskoj borbi
        public static string SecondHero(Race race) => Heroes[race][1];

        // javni popis reward karata rase (za achievemente / statistiku collectiona)
        public static IReadOnlyList<string> RewardCards(Race race) => RewardOrder[race];

        // globalni indeks borbe na mapi: pozicija regije u RegionOrder x 5 + indeks
        // u regiji (0-based); finale nema indeks (vraca -1)
        private static int BattleIndex(StoryNode node)
        {
            if (node == null || node.IsFinal) return -1;
            int region = System.Array.IndexOf(RegionOrder, node.EnemyRace);
            if (region < 0) return -1;
            return region * 5 + (node.IndexInRegion - 1);
        }

        // reward ovog levela za IGRACEVU rasu: karta (borbe 1-19), drugi heroj
        // (Demons 5) ili null (finale, ono daje golden okvir)
        public static string RewardFor(Race playerRace, StoryNode node)
        {
            int i = BattleIndex(node);
            if (i < 0) return null;                       // finale ili nepoznat node
            if (i == 19) return SecondHero(playerRace);   // Demons 5 → drugi heroj
            var cards = RewardOrder[playerRace];
            return i < cards.Length ? cards[i] : null;
        }
    }

    // Golden okvir, cosmetic po rasi, otkljucava ga finale storyja s tom rasom
    public static class GoldenFrames
    {
        public static bool Has(Race race) => PlayerPrefs.GetInt($"golden_{race}", 0) == 1;
        public static void Unlock(Race race)
        {
            PlayerPrefs.SetInt($"golden_{race}", 1);
            PlayerPrefs.Save();
        }
    }

    // Poruka koju story mapa pokaze kao animirani toast kad se vrati iz meca
    // (npr. "Card unlocked: Void Imp"). BattleController je postavi, mapa je potrosi.
    public static class MapToast
    {
        public static string Pending;
        public static string Consume()
        {
            var m = Pending; Pending = null; return m;
        }
    }

    // 4 save slota za story kampanje; svaki slot je svoj playthrough (rasa, heroj,
    // napredak mape). Otkljucane karte su GLOBALNE (dijele ih svi slotovi, collection
    // i multiplayer) da novi playthrough ne unisti stare rewarde.
    public static class StorySlots
    {
        public static readonly string[] Suffix = { "", "_s2", "_s3", "_s4" }; // "" = legacy save
        private const string ActiveKey = "story_active_slot";

        public static int Count => Suffix.Length;

        public static int Active
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt(ActiveKey, 0), 0, Count - 1);
            set { PlayerPrefs.SetInt(ActiveKey, Mathf.Clamp(value, 0, Count - 1)); PlayerPrefs.Save(); }
        }

        // sufiks kljuceva trenutno aktivnog slota
        public static string Sfx => Suffix[Active];
    }

    // Pamti koje su story bitke odradene i koje su cards time otkljucane.
    // Osnovne cards (sve iz zadanih deck lista plus zadani heroji) uvijek su
    // otkljucane; nagradne se otvaraju kako se bitke pobjeduju.
    public static class StoryProgress
    {
        private const string DoneKey = "story_done";       // csv of node ids (po slotu)
        private const string CardsKey = "unlocked_cards";  // csv of card names (GLOBALNO)

        // jednokratno: preimenuj stara imena heroja u otkljucanim kartama (WOW->novi)
        // da igrac koji je vec osvojio 2. heroja ne izgubi unlock nakon rename-a
        static StoryProgress()
        {
            if (PlayerPrefs.GetInt("hero_rename_v1", 0) == 1) return;
            var set = new HashSet<string>(
                PlayerPrefs.GetString(CardsKey, "").Split('|')
                    .Where(s => s.Length > 0).Select(DeckStorage.MigrateHero));
            PlayerPrefs.SetString(CardsKey, string.Join("|", set));
            PlayerPrefs.SetInt("hero_rename_v1", 1);
            PlayerPrefs.Save();
        }

        // done lista je per-slot; unlockovi su globalni
        private static string SlotDoneKey => DoneKey + StorySlots.Sfx;

        private static HashSet<string> LoadSet(string key)
            => new(PlayerPrefs.GetString(key, "").Split('|').Where(s => s.Length > 0));

        private static void SaveSet(string key, HashSet<string> set)
        {
            PlayerPrefs.SetString(key, string.Join("|", set));
            PlayerPrefs.Save();
        }

        // koliko je story battleova zavrseno u AKTIVNOM slotu (za save prikaz)
        public static int CompletedCount() => LoadSet(SlotDoneKey).Count;

        // isto, ali za odredeni slot (za slot picker)
        public static int CompletedCountFor(int slot)
            => LoadSet(DoneKey + StorySlots.Suffix[Mathf.Clamp(slot, 0, StorySlots.Count - 1)]).Count;

        public static bool IsDone(string nodeId) => LoadSet(SlotDoneKey).Contains(nodeId);

        public static void Complete(StoryNode node)
        {
            var done = LoadSet(SlotDoneKey);
            done.Add(node.Id);
            SaveSet(SlotDoneKey, done);
            // reward se veze uz NODE, BattleController otkljucava kartu tog levela
            // preko StoryData.RewardFor
        }

        // Prisilno otkljucaj card po imenu (npr. heroja kojeg je igrac izabrao na
        // New Game, a koji bi inace bio zakljucan kao nagradni drugi heroj rase).
        public static void Unlock(string cardName)
        {
            if (string.IsNullOrEmpty(cardName)) return;
            var cards = LoadSet(CardsKey);
            cards.Add(cardName);
            SaveSet(CardsKey, cards);
        }

        // node 1 svake regije je uvijek otvoren, ostali traze pobjedu na prethodnom;
        // zavrsni trazi svih 20 regijskih bitaka
        public static bool IsAvailable(StoryNode node)
        {
            if (node.IsFinal)
                return StoryData.AllNodes.Where(n => !n.IsFinal).All(n => IsDone(n.Id));
            if (node.IndexInRegion <= 1) return true;
            var prev = StoryData.Get($"{node.EnemyRace}_{node.IndexInRegion - 1}");
            return prev != null && IsDone(prev.Id);
        }

        // ---- otkljucavanje cards (koristi collection) ----

        // nove zamke su base content, dostupne od pocetka (nisu story nagrade)
        private static readonly HashSet<string> AlwaysUnlocked = new()
        {
            "Tripline", "Spiked Pit", "Root Snare", "Caltrops",
            "Sanctified Ground", "Shadow Pit", "Soul Trap",
        };

        public static bool IsCardUnlocked(Race race, string cardName)
        {
            // sve iz zadane deck liste plus zadani heroj je osnovni sadrzaj;
            // DRUGI heroj je zakljucan dok se ne osvoji na Demons 5 (ili izabere u
            // New Game, sto ga force-unlocka)
            if (AlwaysUnlocked.Contains(cardName)) return true;
            if (DeckStorage.DefaultHero(race) == cardName) return true;
            if (DeckLists.For(race).Any(e => e.name == cardName)) return true;
            return LoadSet(CardsKey).Contains(cardName);
        }

        // reset SAMO aktivnog slota (napredak mape); globalni unlockovi ostaju,
        // inace bi New Game u jednom slotu unistio rewarde iz drugih playthrougha
        public static void ResetActiveSlot()
        {
            PlayerPrefs.DeleteKey(SlotDoneKey);
            PlayerPrefs.Save();
        }

        // obrisi napredak mape odredenog slota (za DELETE u slot pickeru)
        public static void ResetSlot(int slot)
        {
            PlayerPrefs.DeleteKey(DoneKey + StorySlots.Suffix[Mathf.Clamp(slot, 0, StorySlots.Count - 1)]);
            PlayerPrefs.Save();
        }

        public static void ResetAll()
        {
            foreach (var sfx in StorySlots.Suffix)
                PlayerPrefs.DeleteKey(DoneKey + sfx);
            PlayerPrefs.DeleteKey(CardsKey);
            PlayerPrefs.Save();
        }
    }

    // Story save slot: koju je rasu igrac izabrao za ovaj prolaz kampanje i kojeg
    // heroja. Cita/pise AKTIVNI slot (StorySlots); New Game brise
    // samo napredak tog slota, globalni card unlockovi ostaju.
    public static class StorySave
    {
        private const string RaceKey = "story_race";
        private const string HeroKey = "story_hero";

        public static bool Exists => PlayerPrefs.HasKey(RaceKey + StorySlots.Sfx);

        // postoji li save u odredenom slotu (za slot picker)
        public static bool ExistsIn(int slot)
            => PlayerPrefs.HasKey(RaceKey + StorySlots.Suffix[Mathf.Clamp(slot, 0, StorySlots.Count - 1)]);

        public static Race RaceIn(int slot)
            => (Race)PlayerPrefs.GetInt(RaceKey + StorySlots.Suffix[Mathf.Clamp(slot, 0, StorySlots.Count - 1)], (int)Race.Humans);

        public static string HeroIn(int slot)
            => DeckStorage.MigrateHero(PlayerPrefs.GetString(HeroKey + StorySlots.Suffix[Mathf.Clamp(slot, 0, StorySlots.Count - 1)], ""));

        public static Race PlayerRace =>
            (Race)PlayerPrefs.GetInt(RaceKey + StorySlots.Sfx, (int)Race.Humans);

        public static string HeroName =>
            DeckStorage.MigrateHero(PlayerPrefs.GetString(HeroKey + StorySlots.Sfx, DeckStorage.DefaultHero(PlayerRace)));

        // potpuno obrisi save slot (rasa/heroj/napredak); unlockovi su globalni i ostaju
        public static void DeleteSlot(int slot)
        {
            slot = Mathf.Clamp(slot, 0, StorySlots.Count - 1);
            StoryProgress.ResetSlot(slot);
            PlayerPrefs.DeleteKey(RaceKey + StorySlots.Suffix[slot]);
            PlayerPrefs.DeleteKey(HeroKey + StorySlots.Suffix[slot]);
            PlayerPrefs.Save();
        }

        public static void NewGame(Race race, string heroName)
        {
            StoryProgress.ResetActiveSlot(); // samo ovaj slot; unlockovi su globalni
            PlayerPrefs.SetInt(RaceKey + StorySlots.Sfx, (int)race);
            PlayerPrefs.SetString(HeroKey + StorySlots.Sfx, heroName);
            PlayerPrefs.Save();

            // izabrani pocetni heroj je uvijek upotrebljiv, cak i ako je rasin
            // drugi heroj koji je inace zakljucan kao nagrada
            StoryProgress.Unlock(heroName);

            // stavi izabranog heroja u spremljeni deck te rase da ga bitke koriste
            var deck = DeckStorage.Load(race);
            deck.heroName = heroName;
            DeckStorage.Save(race, deck);
        }
    }

    // Narativ story moda: ime protivnika i tekst prije bitke, po nodeu.
    // Svijet i puna razrada price su u STORY.txt u korijenu projekta.
    public static class StoryLore
    {
        // ime + titula protivnika (prikazuje se na mapi i u lore prozoru)
        public static string CommanderName(StoryNode node)
        {
            if (Localization.Current == Language.Croatian && CommandersHr.TryGetValue(node.Id, out var hr)) return hr;
            return Commanders.TryGetValue(node.Id, out var n) ? n : node.EnemyRace.ToString();
        }

        // tekst prije bitke: put do protivnika + tko je on
        public static string For(StoryNode node)
        {
            if (Localization.Current == Language.Croatian && TextsHr.TryGetValue(node.Id, out var hr)) return hr;
            return Texts.TryGetValue(node.Id, out var t) ? t : "";
        }

        // sto protivnik kaze kad ga porazis (prikazuje se na victory ekranu)
        public static string DefeatLine(StoryNode node)
        {
            if (node == null) return "";
            if (Localization.Current == Language.Croatian && DefeatHr.TryGetValue(node.Id, out var hr)) return hr;
            return Defeat.TryGetValue(node.Id, out var t) ? t : "";
        }

        // 21 protivnik: 4 regije x 5 + finale
        private static readonly Dictionary<string, string> Commanders = new()
        {
            { "Orcs_1",   "Skarn Ashtooth, Raid-Leader" },
            { "Orcs_2",   "Gora Two-Axes, Warden of the Ford" },
            { "Orcs_3",   "Vhek the Drumbound" },
            { "Orcs_4",   "Morg Ironjaw, Banner-Captain" },
            { "Orcs_5",   "Ur'gath, Khan of the Bloodmoot" },
            { "Elves_1",  "Sentinel Aelira of the Old Road" },
            { "Elves_2",  "Keeper Lysvane of the Moonwell" },
            { "Elves_3",  "Thornmother Ysseldra" },
            { "Elves_4",  "Starcaller Neriel" },
            { "Elves_5",  "Elderroot Cael'dorin" },
            { "Humans_1", "Captain Ordwin Vale" },
            { "Humans_2", "Ser Katherin Marlow" },
            { "Humans_3", "Master Engineer Hollis Ferrin" },
            { "Humans_4", "Lord Commander Bertran Kaul" },
            { "Humans_5", "Regent Aldric Vayne" },
            { "Demons_1", "Grishk Gigglespine" },
            { "Demons_2", "Vareth the Houndmaster" },
            { "Demons_3", "Xarveth, Harvester of Souls" },
            { "Demons_4", "Dread Marshal Nekh'zul" },
            { "Demons_5", "Ghar'moth the Unmaker" },
            { "Final",    "The Hollow King" },
        };

        private static readonly Dictionary<string, string> Texts = new()
        {
            // ---- I. CRVENA STEPA: pohod pocinje na istoku ----
            { "Orcs_1",
              "Skarn Ashtooth rides at the head of a raiding band, nine days east of the last " +
              "standing gate. He is young and loud. He tests your borders the way a wolf tests " +
              "a fence, and he believes that whoever bleeds first loses the whole war." },
            { "Orcs_2",
              "The raiders were only the first thing you met. At the ford of the Ashmere, " +
              "Gora Two-Axes has planted her banner in the mud and told her warband that the river " +
              "is where your campaign ends. She has held this crossing through three winters and " +
              "buried every lord who argued with her." },
            { "Orcs_3",
              "Deeper into the steppe the drums never stop. Vhek the Drumbound cut out his own " +
              "tongue so that nothing would compete with the beat, and his riders move to it as one " +
              "animal. Break the rhythm and the band breaks with it, if you can survive the first " +
              "charge." },
            { "Orcs_4",
              "Morg Ironjaw brings the Khan's own banner and the veterans who follow it. There is " +
              "no boasting here and no drums, only a line of shields that has never been asked to " +
              "break. He fights you so he can report your measure to his Khan." },
            { "Orcs_5",
              "At the great war camp the clans have gathered to watch. Ur'gath, Khan of the Bloodmoot, " +
              "keeps the Ember Shard bound into the haft of his axe and has not drawn it in eleven " +
              "years. He offers you the old courtesy of the steppe. Win and the shard and the clans " +
              "are yours. Lose and you stay here." },

            // ---- II. SREBROSUMA: sume koje pamte ----
            { "Elves_1",
              "The steppe ends where the Silverwood begins, and the forest notices you at once. " +
              "Sentinel Aelira bars the old road with a courtesy cold enough to cut. She has turned " +
              "back kings and merchants with the same words and does not much care which you are." },
            { "Elves_2",
              "Keeper Lysvane tends the wells of moonlight past the old road, and has done since " +
              "before your grandfather was born. He will not ask you to leave twice." },
            { "Elves_3",
              "The deep wood stirs. Roots shift where no wind blows, and Thornmother Ysseldra walks " +
              "ahead of them wearing bark like mail. She remembers the Sundering, and she remembers " +
              "what lords carrying shards tend to promise beforehand." },
            { "Elves_4",
              "Starcaller Neriel reads the sky and does not like the way your name is written in it. " +
              "Starlight falls through the canopy in thin bright lines, and every one of them is " +
              "aimed." },
            { "Elves_5",
              "At the roots of the Worldtree the eldest of the elves is waiting, and has been for some " +
              "time. Elderroot Cael'dorin keeps the Verdant Shard inside his own heartwood. To take it " +
              "you have to convince something older than the war that the war is worth fighting." },

            // ---- III. HIGHMARCH: kraljevstvo koje se pretvara da je mirno ----
            { "Humans_1",
              "Beyond the treeline the fields are ploughed, the roads are paved, and the war is " +
              "politely denied. Captain Ordwin Vale holds the border gate with forty men and a " +
              "standing order from a Regent who says Highmarch is neutral. Ordwin knows better than " +
              "that. He intends to obey anyway." },
            { "Humans_2",
              "Ser Katherin Marlow leads the column out in perfect order with the lances bright. She " +
              "means to end this in a single charge, because one charge means fewer funerals." },
            { "Humans_3",
              "Highmarch stops pretending. Master Engineer Hollis Ferrin arrives with the walls in " +
              "tow, towers on wheels and counterweights and ranging tables. He has never held a sword " +
              "and has killed more soldiers than anyone else in this campaign." },
            { "Humans_4",
              "The inner city closes like a fist. Lord Commander Bertran Kaul draws the royal guard " +
              "up before the gates and prays briefly for both armies. Then he does his duty, which he " +
              "has never once confused with mercy." },
            { "Humans_5",
              "The throne room doors stand open, which is the first honest thing Highmarch has done. " +
              "Regent Aldric Vayne rules for a king who has not woken in nine years and keeps the " +
              "Sunlit Shard under that sleeping king's pillow. He bargained with the Maw to keep his " +
              "borders quiet, and he would like you to understand that he had no choice." },

            // ---- IV. PEPELJASTA PUSTOS: cijena pakta ----
            { "Demons_1",
              "Past the border of the wastes the ground blackens and stays warm. Grishk Gigglespine " +
              "and his imps come out to meet you laughing, and they are the smallest thing the Maw " +
              "will send." },
            { "Demons_2",
              "Something drives the herds of ash ahead of it. Vareth the Houndmaster runs the ridge " +
              "lines with a pack that hunts by fear alone, and armies are bad at hiding fear." },
            { "Demons_3",
              "Xarveth harvests souls the way Highmarch harvests grain. Ritual fires burn green " +
              "across the plain and each one is a ledger. He has already counted your army twice, " +
              "once as enemies and once as yield." },
            { "Demons_4",
              "Dread Marshal Nekh'zul was a mortal general before the Sundering and is still a good " +
              "one. Terror marches in ranks ahead of him and keeps step." },
            { "Demons_5",
              "At the mouth of the Maw stands Ghar'moth the Unmaker, wreathed in fire, holding the " +
              "Ashen Shard that was never his. Kill him and the fourth shard is yours. So is the " +
              "champion he chained here, who has waited a long time for someone to open the door." },

            // ---- FINALE: Ashmount ----
            { "Final",
              "Four shards, and one road left. It goes up. On the summit of Ashmount the Hollow King " +
              "waits beside the Sundering, the crack in the world that his crown made when it broke. " +
              "He was the last lord to unite the realms, and what the crown left behind is patient " +
              "and wants only to be whole again, whatever that costs. He has been waiting for someone " +
              "to carry all four shards to him.\n\n" +
              "He is glad it is you." },
        };

        // ---- hrvatski prijevod (osobna imena ostaju, titule/narativ prevedeni) ----
        private static readonly Dictionary<string, string> CommandersHr = new()
        {
            { "Orcs_1",   "Skarn Ashtooth, vođa pohoda" },
            { "Orcs_2",   "Gora Two-Axes, čuvarica gaza" },
            { "Orcs_3",   "Vhek the Drumbound" },
            { "Orcs_4",   "Morg Ironjaw, barjaktar-kapetan" },
            { "Orcs_5",   "Ur'gath, Khan Bloodmoota" },
            { "Elves_1",  "Stražarica Aelira sa Starog puta" },
            { "Elves_2",  "Čuvar Lysvane s Mjesečeva zdenca" },
            { "Elves_3",  "Trnomajka Ysseldra" },
            { "Elves_4",  "Zvjezdozovka Neriel" },
            { "Elves_5",  "Starokorijen Cael'dorin" },
            { "Humans_1", "Kapetan Ordwin Vale" },
            { "Humans_2", "Ser Katherin Marlow" },
            { "Humans_3", "Glavni inženjer Hollis Ferrin" },
            { "Humans_4", "Lord zapovjednik Bertran Kaul" },
            { "Humans_5", "Namjesnik Aldric Vayne" },
            { "Demons_1", "Grishk Gigglespine" },
            { "Demons_2", "Vareth, gospodar pasa" },
            { "Demons_3", "Xarveth, Žetelac duša" },
            { "Demons_4", "Strahomaršal Nekh'zul" },
            { "Demons_5", "Ghar'moth the Unmaker" },
            { "Final",    "Šuplji Kralj" },
        };

        private static readonly Dictionary<string, string> TextsHr = new()
        {
            // ---- I. CRVENA STEPA: pohod pocinje na istoku ----
            { "Orcs_1",
              "Skarn Ashtooth jaše na čelu pljačkaške družine devet dana istočno od posljednjih " +
              "vrata koja još stoje. Mlad je i glasan. Iskušava tvoje granice kao što vuk " +
              "iskušava ogradu, i vjeruje da onaj tko prvi prokrvari gubi cijeli rat." },
            { "Orcs_2",
              "Pljačkaši su bili tek prvo na što si naletio. Na gazu Ashmere, Gora Two-Axes zabola je " +
              "svoj barjak u blato i rekla svojoj četi da je rijeka mjesto gdje tvoj pohod završava. " +
              "Držala je ovaj prijelaz kroz tri zime i pokopala svakog gospodara koji joj se suprotstavio." },
            { "Orcs_3",
              "Dublje u stepi bubnjevi nikad ne prestaju. Vhek the Drumbound iščupao je vlastiti jezik " +
              "da se ništa ne bi natjecalo s ritmom, a njegovi se jahači kreću uz njega kao jedna " +
              "životinja. Slomiš li ritam, s njim se lomi i družina, ako preživiš prvi juriš." },
            { "Orcs_4",
              "Morg Ironjaw donosi Khanov barjak i veterane koji ga slijede. Nema hvalisanja ni " +
              "bubnjeva, samo red štitova koji nikad nije zamoljen da se slomi. Bori se protiv " +
              "tebe da svom Khanu javi tvoju mjeru." },
            { "Orcs_5",
              "U velikom ratnom taboru klanovi su se okupili da gledaju. Ur'gath, Khan Bloodmoota, drži " +
              "Žeravnu krhotinu utkanu u dršku svoje sjekire i nije je izvukao već jedanaest godina. Nudi " +
              "ti staru pristojnost stepe. Pobijediš li, krhotina i klanovi su tvoji. Izgubiš li, ostaješ " +
              "ovdje." },

            // ---- II. SREBROSUMA: sume koje pamte ----
            { "Elves_1",
              "Stepa završava gdje počinje Srebrošuma, i šuma te odmah primijeti. Stražarica Aelira " +
              "zatvara stari put pristojnošću dovoljno hladnom da reže. Istim je riječima vraćala i " +
              "kraljeve i trgovce i nije joj osobito važno koje si od toga." },
            { "Elves_2",
              "Čuvar Lysvane brine se o zdencima mjesečine iza starog puta, i čini to još otkad " +
              "tvoj djed nije bio rođen. Neće te dvaput zamoliti da odeš." },
            { "Elves_3",
              "Duboka šuma se komeša. Korijenje se pomiče gdje vjetar ne puše, a Trnomajka Ysseldra " +
              "korača pred njim, noseći koru poput oklopa. Pamti Raskol, i pamti što gospodari s " +
              "krhotinama običaju obećati unaprijed." },
            { "Elves_4",
              "Zvjezdozovka Neriel čita nebo i ne sviđa joj se kako je ondje zapisano tvoje ime. " +
              "Svjetlost zvijezda pada kroz krošnje u tankim svijetlim crtama, i svaka je od njih " +
              "naciljana." },
            { "Elves_5",
              "Kod korijena Svjetskog Stabla najstariji od vilenjaka čeka, i čeka već neko vrijeme. " +
              "Starokorijen Cael'dorin drži Zelenu krhotinu unutar vlastite srčike. Da je uzmeš, moraš " +
              "uvjeriti nešto starije od rata da se rat isplati voditi." },

            // ---- III. HIGHMARCH: kraljevstvo koje se pretvara da je mirno ----
            { "Humans_1",
              "Iza ruba šume polja su preorana, ceste popločane, a rat se pristojno niječe. Kapetan " +
              "Ordwin Vale drži granična vrata s četrdeset ljudi i stalnom zapovijedi Namjesnika koji " +
              "tvrdi da je Highmarch neutralan. Ordwin zna da to nije točno. Svejedno kani poslušati." },
            { "Humans_2",
              "Ser Katherin Marlow izvodi kolonu u savršenom redu, s blistećim kopljima. Kani ovo " +
              "okončati jednim jedinim jurišem, jer jedan juriš znači manje sprovoda." },
            { "Humans_3",
              "Highmarch se prestaje pretvarati. Glavni inženjer Hollis Ferrin dovlači zidine sa sobom, " +
              "kule na kotačima i protuutege i tablice za gađanje. Nikad nije držao mač, a ubio je više " +
              "vojnika od bilo koga drugog u ovom pohodu." },
            { "Humans_4",
              "Unutarnji grad se zatvara poput šake. Lord zapovjednik Bertran Kaul postrojava kraljevsku " +
              "gardu pred vratima i kratko se moli za obje vojske. Zatim čini svoju dužnost, koju nijednom " +
              "nije pomiješao s milošću." },
            { "Humans_5",
              "Vrata prijestolne dvorane stoje otvorena, što je prva iskrena stvar koju je Highmarch " +
              "učinio. Namjesnik Aldric Vayne vlada umjesto kralja koji se nije probudio već devet godina " +
              "i drži Sunčevu krhotinu pod jastukom tog usnulog kralja. Cjenkao se sa Ždrijelom da mu " +
              "granice ostanu mirne, i želio bi da shvatiš da nije imao izbora." },

            // ---- IV. PEPELJASTA PUSTOS: cijena pakta ----
            { "Demons_1",
              "Tlo iza granice pustoši pocrni i ostaje toplo. Grishk Gigglespine i njegovi impovi " +
              "izlaze ti u susret smijući se, i najmanje su što će Ždrijelo poslati." },
            { "Demons_2",
              "Nešto tjera krda pepela pred sobom. Vareth the Houndmaster juri grebenima s čoporom koji " +
              "lovi samim strahom, a vojske strah loše skrivaju." },
            { "Demons_3",
              "Xarveth žanje duše kao što Highmarch žanje žito. Ritualne vatre gore zeleno diljem " +
              "ravnice i svaka je od njih knjiga računa. Tvoju je vojsku već dvaput izbrojao, jednom " +
              "kao neprijatelje i jednom kao urod." },
            { "Demons_4",
              "Strahomaršal Nekh'zul bio je smrtni general prije Raskola i još uvijek je dobar. " +
              "Užas maršira u redovima pred njim i drži korak." },
            { "Demons_5",
              "Na ušću Ždrijela stoji Ghar'moth the Unmaker, ovjenčan vatrom, držeći Pepeljastu krhotinu " +
              "koja nikad nije bila njegova. Ubij ga i četvrta je krhotina tvoja. Tvoj je i prvak kojeg " +
              "je ovdje okovao, koji dugo čeka da netko otvori vrata." },

            // ---- FINALE: Ashmount ----
            { "Final",
              "Četiri krhotine, i jedan preostao put. Vodi uzbrdo. Na vrhu Ashmounta Šuplji Kralj čeka " +
              "pokraj Raskola, pukotine u svijetu koju je stvorila njegova kruna kad se slomila. Bio je " +
              "posljednji gospodar koji je ujedinio kraljevstva, a ono što je kruna ostavila za sobom " +
              "strpljivo je i želi samo opet biti cijelo, koliko god to stajalo. Čekao je da mu netko " +
              "donese sve četiri krhotine.\n\n" +
              "Drago mu je što si to ti." },
        };

        // ---- sto protivnik kaze kad ga porazis ----
        private static readonly Dictionary<string, string> Defeat = new()
        {
            { "Orcs_1",
              "You held. I did not think you would." },
            { "Orcs_2",
              "Three winters I kept this crossing. You took it in an afternoon. Cross, then. " +
              "The water is cold, mind your horses." },
            { "Orcs_3",
              "He does not speak, and does not try to. He picks up the broken drumstick, puts " +
              "it in your hand, and lowers his head." },
            { "Orcs_4",
              "Good enough. The Khan wanted your measure and I have it. Come to the Bloodmoot. " +
              "Bring that arm with you." },
            { "Orcs_5",
              "The shard is yours. So are the clans, if you want them. Carry it better than the " +
              "last lord who did. He is still sitting up on Ashmount." },
            { "Elves_1",
              "You did not burn the road behind you. Most who come this way do. Pass." },
            { "Elves_2",
              "The wells are clear. You struck me and left them alone. Go deeper if you must, " +
              "but Ysseldra has less patience than I do." },
            { "Elves_3",
              "Bark breaks, and that is all you broke. Take your shard. I have seen this " +
              "spring before." },
            { "Elves_4",
              "I read your name wrong. That does not happen to me often." },
            { "Elves_5",
              "Take it. The Verdant Shard leaves my heartwood freely, remember that. " +
              "It was not torn from me." },
            { "Humans_1",
              "Orders discharged. Forty men, one gate, and no lie left standing behind it. " +
              "The road in is open." },
            { "Humans_2",
              "One charge, one answer. I wanted fewer funerals and I have bought more. " +
              "Ride on, before I have to write the letters." },
            { "Humans_3",
              "The numbers were right. The assumption was wrong. I did not account for a lord " +
              "who walks toward the counterweights. Take the city." },
            { "Humans_4",
              "I prayed for both armies and one prayer was answered. My duty is done and I will " +
              "not call it mercy. The Regent is behind those doors." },
            { "Humans_5",
              "Take the Sunlit Shard out from under a sleeping king's head and tell me it feels " +
              "like a choice. I bargained with the Maw to keep my borders quiet." },
            { "Demons_1",
              "Ha. Ow. Still funny. You know what gets me, big lord? You are walking in. " +
              "Everything else in this waste is trying to get out." },
            { "Demons_2",
              "The pack will not come at you again. They have your scent and they do not like it." },
            { "Demons_3",
              "I counted you twice. Once as enemies, once as yield. I should have counted you as " +
              "a cost." },
            { "Demons_4",
              "Ranks broken, line held to the last. You would have made a good general before the " +
              "Sundering." },
            { "Demons_5",
              "The Ashen Shard was never mine, and it will sit no easier with you. Unchain my " +
              "prisoner. He has waited longer than you have been alive and he will follow you " +
              "up the mountain." },
            { "Final",
              "You carried all four of them up here. Tell me how they feel in your hands.\n\n" +
              "I carried them once, and I could not put them down again. See if you do better." },
        };

        private static readonly Dictionary<string, string> DefeatHr = new()
        {
            { "Orcs_1",
              "Izdržao si. Nisam mislio da hoćeš." },
            { "Orcs_2",
              "Tri zime sam čuvala ovaj prijelaz. Ti si ga uzeo u jedno popodne. Prijeđi. " +
              "Voda je hladna, pazi na konje." },
            { "Orcs_3",
              "Ne govori i ne pokušava. Podiže slomljenu palicu, stavlja ti je u ruku i " +
              "spušta glavu." },
            { "Orcs_4",
              "Dovoljno. Khan je htio tvoju mjeru i sad je imam. Dođi na Bloodmoot. " +
              "I ponesi tu ruku sa sobom." },
            { "Orcs_5",
              "Krhotina je tvoja. I klanovi, ako ih hoćeš. Nosi je bolje od posljednjeg " +
              "gospodara koji je to činio. On još sjedi gore na Ashmountu." },
            { "Elves_1",
              "Nisi spalio put za sobom. Većina onih koji dođu ovuda spali. Prođi." },
            { "Elves_2",
              "Zdenci su čisti. Mene si pogodio, a njih ostavio. Idi dublje ako moraš, " +
              "ali Ysseldra ima manje strpljenja od mene." },
            { "Elves_3",
              "Kora puca, i to je sve što si slomio. Uzmi svoju krhotinu. Vidjela sam " +
              "ovo proljeće i prije." },
            { "Elves_4",
              "Krivo sam pročitala tvoje ime. To mi se rijetko događa." },
            { "Elves_5",
              "Uzmi je. Zelena krhotina napušta moju srčiku svojevoljno, zapamti to. " +
              "Nije ti otrgnuta." },
            { "Humans_1",
              "Zapovijed izvršena. Četrdeset ljudi, jedna vrata i nijedna laž više iza njih. " +
              "Put unutra je otvoren." },
            { "Humans_2",
              "Jedan juriš, jedan odgovor. Htjela sam manje sprovoda, a kupila sam više. " +
              "Jaši dalje, prije nego što moram pisati pisma." },
            { "Humans_3",
              "Brojke su bile točne. Pretpostavka je bila kriva. Nisam računao na gospodara " +
              "koji korača prema protuutezima. Uzmi grad." },
            { "Humans_4",
              "Molio sam za obje vojske i uslišana je jedna molitva. Dužnost je izvršena i " +
              "neću je zvati milošću. Namjesnik je iza tih vrata." },
            { "Humans_5",
              "Uzmi Sunčevu krhotinu ispod glave usnulog kralja pa mi reci da ti to djeluje " +
              "kao izbor. Ja sam se cjenkao sa Ždrijelom da mi granice budu mirne." },
            { "Demons_1",
              "Ha. Joj. Još je smiješno. Znaš što me drži, veliki gospodaru? Ti ulaziš unutra. " +
              "Sve ostalo u ovoj pustoši pokušava van." },
            { "Demons_2",
              "Čopor više neće na tebe. Imaju tvoj miris i ne sviđa im se." },
            { "Demons_3",
              "Dvaput sam te izbrojao. Jednom kao neprijatelje, jednom kao urod. Trebao sam te " +
              "izbrojati kao trošak." },
            { "Demons_4",
              "Redovi slomljeni, crta držana do zadnjega. Bio bi dobar general prije Raskola." },
            { "Demons_5",
              "Pepeljasta krhotina nikad nije bila moja i neće ti sjesti ništa lakše. Oslobodi " +
              "mog zarobljenika. Čekao je dulje nego što si ti živ i slijedit će te uz planinu." },
            { "Final",
              "Donio si sve četiri gore. Reci mi kako ti stoje u rukama.\n\n" +
              "Ja sam ih jednom nosio i nisam ih uspio spustiti. Vidi hoćeš li ti bolje." },
        };
    }
}
