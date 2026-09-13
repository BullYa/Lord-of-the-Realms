using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LordOfTheRealms
{
    // Jezik UI-ja: EN ili HR. Loc kljuc -> string za aktivni jezik.
    public enum Language { English, Croatian }

    // Sredisnja lokalizacija. Loc.T(key) vraca string na aktivnom jeziku; ako
    // kljuca nema ili je HR vrijednost prazna, pada na engleski, pa na sam kljuc.
    // Stringovi zive u kodu (cijeli UI se gradi iz koda), pa ovo pokriva UI.
    // Imena cards i lore su u generiranim assetima i rjesavaju se odvojeno.
    public static class Localization
    {
        private const string PrefKey = "ui_language";

        private static Language _current = (Language)PlayerPrefs.GetInt(PrefKey, 0);
        public static Language Current => _current;

        // promijeni jezik i ponovno izgradi UI: reload aktivne scene je najjednostavniji
        // siguran nacin, jer svaki ekran svoj tekst gradi jednom u Start()
        public static void SetLanguage(Language lang)
        {
            if (lang == _current) return;
            _current = lang;
            PlayerPrefs.SetInt(PrefKey, (int)lang);
            PlayerPrefs.Save();
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        // dohvat s engleskim fallbackom
        public static string T(string key)
        {
            if (Table.TryGetValue(key, out var p))
                return _current == Language.Croatian && !string.IsNullOrEmpty(p.hr) ? p.hr : p.en;
            return key;
        }

        // lokalizirani naziv tezine bota (dijeljeno: match setup + story map)
        public static string DiffName(BotDifficulty d) => d switch
        {
            BotDifficulty.VeryEasy => T("diff.veryeasy"),
            BotDifficulty.Easy => T("diff.easy"),
            BotDifficulty.Medium => T("diff.medium"),
            BotDifficulty.Hard => T("diff.hard"),
            BotDifficulty.VeryHard => T("diff.veryhard"),
            BotDifficulty.Nightmare => T("diff.nightmare"),
            _ => d.ToString(),
        };

        // lokalizirani naziv rase (za prikaz); enum vrijednost ostaje ista u logici
        public static string RaceName(Race r) => r switch
        {
            Race.Orcs => T("race.orcs"),
            Race.Elves => T("race.elves"),
            Race.Humans => T("race.humans"),
            Race.Demons => T("race.demons"),
            _ => r.ToString(),
        };

        private struct Pair
        {
            public string en, hr;
            public Pair(string e, string h) { en = e; hr = h; }
        }

        // kljuc -> { EN, HR }. Grupirano po ekranu; raste kako se ekrani prevode.
        private static readonly Dictionary<string, Pair> Table = new()
        {
            // main menu
            { "menu.freeplay",     new Pair("FREE PLAY",       "SLOBODNA IGRA") },
            { "menu.story",        new Pair("STORY",           "PRIČA") },
            { "menu.collection",   new Pair("CARD COLLECTION", "ZBIRKA KARATA") },
            { "menu.achievements", new Pair("ACHIEVEMENTS",    "POSTIGNUĆA") },
            { "menu.quit",         new Pair("QUIT",            "IZLAZ") },

            // settings (top-right box)
            { "settings.sound",    new Pair("SOUND",           "ZVUK") },
            { "settings.language", new Pair("LANGUAGE",        "JEZIK") },

            // shared
            { "common.close",      new Pair("CLOSE",           "ZATVORI") },
            { "common.menu",       new Pair("MENU",            "IZBORNIK") },
            { "common.win",        new Pair("WIN",             "POB.") },
            { "common.loss",       new Pair("LOSS",            "POR.") },

            // free play / ranked
            { "mp.ranked",         new Pair("RANKED 1v1",        "RANGIRANO 1v1") },
            { "mp.gauntlet",       new Pair("INFINITE GAUNTLET", "BESKONAČNI GAUNTLET") },
            { "mp.editdeck",       new Pair("EDIT DECK",         "UREDI ŠPIL") },
            { "mp.invite",         new Pair("INVITE PLAYER",     "POZOVI IGRAČA") },
            { "mp.yourrace",       new Pair("Your race:",        "Tvoja rasa:") },
            { "mp.deck_ok",        new Pair("Active deck: Slot {0}  ·  {1}/30  ✓", "Aktivni špil: Slot {0}  ·  {1}/30  ✓") },
            { "mp.deck_edit",      new Pair("Active deck: Slot {0}  ·  {1}/30  ·  edit before playing", "Aktivni špil: Slot {0}  ·  {1}/30  ·  uredi prije igranja") },
            { "mp.history",        new Pair("MATCH HISTORY",     "POVIJEST MEČEVA") },
            { "mp.history_none",   new Pair("No ranked matches played yet.", "Još nema odigranih rangiranih mečeva.") },
            { "mp.history_summary",new Pair("{0}W - {1}L   ·   {2}% winrate   ·   avg {3} ELO   ·   last {4}", "{0} pob. - {1} por.   ·   {2}% pobjeda   ·   prosj. {3} ELO   ·   zadnjih {4}") },
            { "mp.searching",      new Pair("Searching for opponent", "Tražim protivnika") },
            { "mp.allowbots",      new Pair("Play against bots if no player is found", "Igraj protiv bota ako nema igrača") },
            { "mp.nobody_found",   new Pair("No player found. Turn on bot matches, or try again later.", "Nema igrača. Uključi mečeve protiv bota ili pokušaj kasnije.") },
            { "mp.connecting",     new Pair("Connecting to the server", "Spajanje na poslužitelj") },
            { "mp.opponent_found", new Pair("Opponent found! Starting the match.", "Pronađen protivnik! Meč počinje.") },
            { "mp.online_failed",  new Pair("Could not reach the server.", "Nije moguće doći do poslužitelja.") },

            // lobby za izazivanje prijatelja
            { "lb.title",      new Pair("PLAY WITH A FRIEND", "IGRAJ S PRIJATELJEM") },
            { "lb.yourname",   new Pair("YOUR NAME", "TVOJE IME") },
            { "lb.name_ph",    new Pair("enter a name", "upiši ime") },
            { "lb.create",     new Pair("CREATE ROOM", "OTVORI SOBU") },
            { "lb.join",       new Pair("JOIN", "PRIDRUŽI SE") },
            { "lb.code_ph",    new Pair("friend's code", "prijateljev kod") },
            { "lb.your_code",  new Pair("ROOM CODE: {0}", "KOD SOBE: {0}") },
            { "lb.share",      new Pair("Send this code to your friend.", "Pošalji ovaj kod prijatelju.") },
            { "lb.waiting",    new Pair("Waiting for your friend", "Čekam prijatelja") },
            { "lb.joined",     new Pair("{0} joined!", "{0} se pridružio!") },
            { "lb.start",      new Pair("START MATCH", "POKRENI MEČ") },
            { "lb.host_starts",new Pair("Waiting for the host to start.", "Čekam da domaćin pokrene meč.") },
            { "lb.leave",      new Pair("LEAVE ROOM", "NAPUSTI SOBU") },
            { "lb.unranked",   new Pair("Friendly match. ELO does not change.", "Prijateljski meč. ELO se ne mijenja.") },
            { "lb.bad_code",   new Pair("No room with that code.", "Nema sobe s tim kodom.") },
            { "mp.no_player",      new Pair("No player found. Matched against a bot ({0} rating).", "Nema igrača. Uparen s botom (rating {0}).") },
            { "mp.streak_note",    new Pair("  (win streak {0}, tougher match!)", "  (niz pobjeda {0}, teži meč!)") },

            // career stats (shown in free play)
            { "stats.career",      new Pair("CAREER",          "KARIJERA") },
            { "stats.nomatches",   new Pair("No matches played yet.", "Još nema odigranih mečeva.") },
            { "stats.career_line", new Pair("CAREER   W {0} - L {1}  ({2}%)   Best streak: {3}", "KARIJERA   P {0} - I {1}  ({2}%)   Najbolji niz: {3}") },

            // card tooltip: type labels + descriptive type names (keywords Taunt/Assassin/Scout stay EN)
            { "tt.type_unit",   new Pair("{0} Unit",  "{0} jedinica") },
            { "tt.type_spell",  new Pair("{0} Spell", "{0} čarolija") },
            { "tt.type_hero",   new Pair("Hero",      "Junak") },
            { "ut.basic",       new Pair("Basic",     "Osnovna") },
            { "st.offensive",   new Pair("Offensive", "Napadačka") },
            { "st.defensive",   new Pair("Defensive", "Obrambena") },
            { "st.field",       new Pair("Field",     "Terenska") },
            { "tt.stats_unit",  new Pair("Attack {0} / Health {1}", "Napad {0} / Zdravlje {1}") },
            { "tt.stats_spell", new Pair("Power {0}", "Snaga {0}") },

            // card tooltip: rule lines
            { "tt.drain",       new Pair("Drains {0} max POWER when played (recovers +1 per turn, up to 10).", "Troši {0} max POWER pri igranju (vraća +1 po potezu, do 10).") },
            { "tt.taunt",       new Pair("Taunt: enemies must attack this first.", "Taunt: neprijatelji prvo moraju napasti ovu jedinicu.") },
            { "tt.assassin",    new Pair("Assassin: ignores Taunt when attacking units; can't hit the enemy hero while a Taunt stands.", "Assassin: ignorira Taunt pri napadu na jedinice; ne može pogoditi neprijateljskog junaka dok Taunt stoji.") },
            { "tt.scout",       new Pair("Scout: can reveal an enemy hidden spell instead of attacking.", "Scout: umjesto napada može otkriti neprijateljsku skrivenu čaroliju.") },
            { "tt.offensive",   new Pair("Deal {0} damage to a chosen target (units always; enemy player only if no Taunt).", "Nanesi {0} štete odabranom cilju (jedinicama uvijek; neprijateljskom igraču samo ako nema Taunta).") },
            { "tt.hidden",      new Pair("Hidden trap: set face-down. When the enemy attacks it triggers, its effect fires and it deals {0} damage to the attacker. An enemy Scout can disarm it.", "Skrivena zamka: postavi licem prema dolje. Kad neprijatelj napadne aktivira se, efekt opali i nanese {0} štete napadaču. Neprijateljski Scout je može razoružati.") },
            { "tt.field_effect",new Pair("Field effect.", "Terenski efekt.") },

            // card tooltip: ability lines
            { "ab.powerramp",   new Pair("Gain +{0} max POWER.", "Dobij +{0} max POWER.") },
            { "ab.draw",        new Pair("Draw {0} card(s).", "Vuci {0} kartu/karte.") },
            { "ab.heal",        new Pair("Heal your player {0}.", "Izliječi svog igrača za {0}.") },
            { "ab.buffattack",  new Pair("Give your units +{0} attack.", "Daj svojim jedinicama +{0} napada.") },
            { "ab.aoe",         new Pair("Deal {0} damage to all enemy units.", "Nanesi {0} štete svim neprijateljskim jedinicama.") },
            { "ab.chargeface",  new Pair("Can attack the turn it's played.", "Može napasti u potezu u kojem je odigrana.") },
            { "ab.passivebuff", new Pair("While alive: your other units have +{0} attack.", "Dok je živa: ostale tvoje jedinice imaju +{0} napada.") },
            { "ab.passiveramp", new Pair("While alive: +{0} max POWER each turn.", "Dok je živa: +{0} max POWER svaki potez.") },
            { "ab.passiveheal", new Pair("While alive: heal your player {0} each turn.", "Dok je živa: liječi tvog igrača za {0} svaki potez.") },
            { "ab.passiveshield",new Pair("While alive: your units take {0} less damage.", "Dok je živa: tvoje jedinice primaju {0} manje štete.") },
            { "ab.lifesteal",   new Pair("Lifesteal: combat damage this unit deals also heals your player.", "Lifesteal: borbena šteta koju ova jedinica nanese ujedno liječi tvog igrača.") },
            { "ab.divineshield",new Pair("Shield: the first damage this unit would take is prevented.", "Shield: prva šteta koju bi ova jedinica primila je spriječena.") },
            { "ab.dmgmaxpower", new Pair("Deal damage equal to your max POWER to a chosen target.", "Nanesi štetu jednaku tvom max POWER-u odabranom cilju.") },
            { "ab.killdraw",    new Pair("If this kills the target, draw a card.", "Ako ovo ubije cilj, vuci kartu.") },
            { "ab.refresh",     new Pair("All your units can attack again this turn.", "Sve tvoje jedinice mogu ponovno napasti ovaj potez.") },
            { "ab.healall",     new Pair("Restore all your units to full health.", "Vrati sve tvoje jedinice na puno zdravlje.") },
            { "ab.execute",     new Pair("Destroy the enemy unit with the highest attack.", "Uništi neprijateljsku jedinicu s najvećim napadom.") },
            { "ab.sacrifice",   new Pair("Destroy your unit with the least health, then draw {0} cards.", "Uništi svoju jedinicu s najmanje zdravlja, zatim vuci {0} karte.") },
            { "ab.aoealldraw",  new Pair("Deal {0} damage to ALL units (yours too), then draw 2 cards.", "Nanesi {0} štete SVIM jedinicama (i tvojima), zatim vuci 2 karte.") },
            { "ab.summon",      new Pair("Summon {0} 1/1 Recruits.", "Prizovi {0} 1/1 Recruita.") },

            // battle screen (BattleController)
            { "bt.endturn",     new Pair("END TURN", "ZAVRŠI POTEZ") },
            { "bt.enemyplayed", new Pair("ENEMY PLAYED", "PROTIVNIK ODIGRAO") },
            { "bt.yourturn",    new Pair("YOUR TURN", "TVOJ POTEZ") },
            { "bt.opponentsturn",new Pair("OPPONENT'S TURN", "POTEZ PROTIVNIKA") },
            { "bt.enemyturn",   new Pair("ENEMY TURN", "POTEZ PROTIVNIKA") },
            { "bt.hand",        new Pair("Hand: {0}", "Ruka: {0}") },
            { "bt.log_nopower", new Pair("Not enough POWER for that card.", "Nema dovoljno POWER-a za tu kartu.") },
            { "bt.log_selecttarget", new Pair("Select a target for {0}.", "Odaberi cilj za {0}.") },
            { "bt.log_cantattack",   new Pair("{0} can't attack yet.", "{0} još ne može napasti.") },
            { "bt.log_dead",    new Pair("That unit is dead.", "Ta jedinica je mrtva.") },
            { "bt.log_stillmoves",   new Pair("You still have moves. Click {0} again to pass.", "Još imaš poteza. Klikni {0} ponovno za predaju.") },
            { "bt.startinghand",new Pair("STARTING HAND", "POČETNA RUKA") },
            { "bt.mull_hint",   new Pair("Click the cards you want to replace, then confirm.", "Klikni karte koje želiš zamijeniti, zatim potvrdi.") },
            { "bt.replace",     new Pair("REPLACE", "ZAMIJENI") },
            { "bt.mull_replace",new Pair("REPLACE {0} & START", "ZAMIJENI {0} I POČNI") },
            { "bt.mull_keep",   new Pair("KEEP ALL & START", "ZADRŽI SVE I POČNI") },
            { "bt.settings",    new Pair("SETTINGS", "POSTAVKE") },
            { "bt.resume",      new Pair("RESUME", "NASTAVI") },
            { "bt.gamespeed",   new Pair("GAME SPEED", "BRZINA IGRE") },
            { "bt.volume",      new Pair("VOLUME {0}%", "GLASNOĆA {0}%") },
            { "bt.quittomenu",  new Pair("QUIT TO MENU", "IZLAZ U IZBORNIK") },
            { "bt.victory",     new Pair("VICTORY", "POBJEDA") },
            { "bt.defeat",      new Pair("DEFEAT", "PORAZ") },
            { "bt.continue",    new Pair("CONTINUE", "NASTAVI") },
            { "bt.backtomap",   new Pair("BACK TO MAP", "NATRAG NA MAPU") },
            { "bt.retry",       new Pair("RETRY", "PONOVI") },
            { "bt.nextbattle",  new Pair("NEXT BATTLE", "SLJEDEĆA BITKA") },
            { "bt.backgauntlet",new Pair("BACK TO GAUNTLET", "NATRAG NA GAUNTLET") },
            { "bt.backranked",  new Pair("BACK TO RANKED", "NATRAG NA RANGIRANO") },
            { "bt.newmatch",    new Pair("NEW MATCH", "NOVI MEČ") },
            { "bt.mainmenu",    new Pair("MAIN MENU", "GLAVNI IZBORNIK") },
            { "bt.ach_unlocked",new Pair("ACHIEVEMENT UNLOCKED", "POSTIGNUĆE OTKLJUČANO") },
            { "bt.blocked",     new Pair("BLOCKED", "BLOKIRANO") },
            { "bt.reward_campaign",new Pair("Campaign complete! GOLDEN FRAME unlocked for {0}!", "Kampanja završena! GOLDEN FRAME otključan za {0}!") },
            { "bt.reward_unlocked",new Pair("Unlocked: {0}", "Otključano: {0}") },
            { "bt.reward_gauntlet",new Pair("Gauntlet streak: {0}", "Gauntlet niz: {0}") },
            { "bt.reward_runover",new Pair("Run over. Streak {0}  (best {1})", "Kraj. Niz {0}  (najbolji {1})") },

            // online: protivnik je izasao ili mu je pukla veza
            { "bt.opp_left",    new Pair("Your opponent left the match.", "Protivnik je napustio meč.") },
            { "bt.opp_lost",    new Pair("Lost connection to your opponent. Waiting a few seconds.", "Izgubljena veza s protivnikom. Čekam nekoliko sekundi.") },
            { "bt.opp_timeout", new Pair("Your opponent stopped responding.", "Protivnik se prestao javljati.") },
            { "bt.you_left",    new Pair("You left the match.", "Napustio si meč.") },
            { "bt.quit_online", new Pair("QUIT (FORFEIT)", "IZLAZ (PREDAJA)") },

            // collection / deck editor (CollectionController)
            { "col.title",       new Pair("COLLECTION", "ZBIRKA") },
            { "col.deck_title",  new Pair("DECK: {0}", "ŠPIL: {0}") },
            { "col.gauntlet",    new Pair("GAUNTLET", "GAUNTLET") },
            { "col.storymap",    new Pair("STORY MAP", "MAPA PRIČE") },
            { "col.allcards",    new Pair("ALL CARDS  (click to add)", "SVE KARTE  (klikni za dodati)") },
            { "col.savedeck",    new Pair("SAVE DECK", "SPREMI ŠPIL") },
            { "col.reset",       new Pair("RESET", "RESETIRAJ") },
            { "col.hide_locked", new Pair("HIDE LOCKED", "SAKRIJ ZAKLJUČANE") },
            { "col.show_locked", new Pair("SHOW LOCKED", "PRIKAŽI ZAKLJUČANE") },
            { "col.sortby",      new Pair("SORT BY", "SORTIRAJ") },
            { "col.sort_cost",   new Pair("COST", "CIJENA") },
            { "col.sort_name",   new Pair("NAME", "IME") },
            { "col.sort_type",   new Pair("TYPE", "TIP") },
            { "col.progress",    new Pair("{0} COLLECTION: {1} / {2} UNLOCKED  ({3}%)", "{0} ZBIRKA: {1} / {2} OTKLJUČANO  ({3}%)") },
            { "col.locked",      new Pair("LOCKED", "ZAKLJUČANO") },
            { "col.indeck",      new Pair("in deck {0}", "u špilu {0}") },
            { "col.yourdeck",    new Pair("YOUR DECK: {0}/{1}   (+1 hero)", "TVOJ ŠPIL: {0}/{1}   (+1 junak)") },
            { "col.deckslot",    new Pair("DECK SLOT", "SLOT ŠPILA") },
            { "col.setactive",   new Pair("SET ACTIVE", "POSTAVI AKTIVNIM") },
            { "col.flash_locked",new Pair("Win the story battle that rewards this card to unlock it.", "Pobijedi story bitku koja nagrađuje ovu kartu da je otključaš.") },
            { "col.flash_full",  new Pair("Deck is full (30).", "Špil je pun (30).") },
            { "col.flash_maxcopies",new Pair("Max {0} copies of this card.", "Najviše {0} kopije ove karte.") },
            { "col.flash_exact", new Pair("Deck must be exactly {0} cards (now {1}).", "Špil mora imati točno {0} karata (sada {1}).") },
            { "col.flash_pickhero",new Pair("Pick a hero.", "Odaberi junaka.") },
            { "col.flash_lockedcards",new Pair("Deck contains locked cards: {0}", "Špil sadrži zaključane karte: {0}") },
            { "col.flash_saved", new Pair("Deck saved!", "Špil spremljen!") },
            { "col.flash_reset", new Pair("Reset to default deck.", "Vraćeno na zadani špil.") },
            { "col.reset_confirm", new Pair("This clears the whole deck. Click again to confirm.", "Ovo briše cijeli špil. Klikni ponovno za potvrdu.") },
            { "col.flash_slotactive",new Pair("Slot {0} is now active for battles.", "Slot {0} je sada aktivan za bitke.") },

            // gauntlet lobby (GauntletController)
            { "g.sub",           new Pair("Climb as far as you can. Every 2 wins the bots get tougher. One loss ends the run.", "Penji se koliko možeš. Svake 2 pobjede botovi jačaju. Jedan poraz završava run.") },
            { "g.beststreak",    new Pair("BEST STREAK:  {0}", "NAJBOLJI NIZ:  {0}") },
            { "g.recentruns",    new Pair("RECENT RUNS", "NEDAVNI RUNOVI") },
            { "g.run_row",       new Pair("{0} wins  ·  {1}", "{0} pobjeda  ·  {1}") },
            { "g.startrun",      new Pair("START RUN", "POKRENI RUN") },

            // match setup (MatchSetupController)
            { "ms.title",        new Pair("MATCH SETUP", "POSTAVKE MEČA") },
            { "ms.yourrace",     new Pair("YOUR RACE", "TVOJA RASA") },
            { "ms.opprace",      new Pair("OPPONENT RACE", "PROTIVNIČKA RASA") },
            { "ms.difficulty",   new Pair("BOT DIFFICULTY", "TEŽINA BOTA") },
            { "ms.startmatch",   new Pair("START MATCH", "POKRENI MEČ") },
            { "ms.deck_hint",    new Pair("from Free Play, where you change race and deck", "iz Slobodne igre, ondje mijenjaš rasu i špil") },
            { "common.back",     new Pair("BACK", "NATRAG") },

            // bot difficulty names (shown in match setup)
            { "diff.veryeasy",   new Pair("VeryEasy", "Vrlo lako") },
            { "diff.easy",       new Pair("Easy", "Lako") },
            { "diff.medium",     new Pair("Medium", "Srednje") },
            { "diff.hard",       new Pair("Hard", "Teško") },
            { "diff.veryhard",   new Pair("VeryHard", "Vrlo teško") },
            { "diff.nightmare",  new Pair("Nightmare", "Noćna mora") },

            // story map (StoryMapController), samo chrome; lore tekst je u StoryLore
            { "sm.title",         new Pair("STORY: THE WAR OF THE REALMS", "PRIČA: RAT KRALJEVSTAVA") },
            { "sm.reward",        new Pair("REWARD", "NAGRADA") },
            { "sm.choose_campaign",new Pair("CHOOSE YOUR CAMPAIGN", "ODABERI KAMPANJU") },
            { "sm.slot_info",     new Pair("{0} · {1} · {2}/21 battles", "{0} · {1} · {2}/21 bitaka") },
            { "sm.slot_empty",    new Pair("empty, start a new campaign", "prazno, pokreni novu kampanju") },
            { "sm.newgame",       new Pair("NEW GAME", "NOVA IGRA") },
            { "sm.delete",        new Pair("DELETE", "OBRIŠI") },
            { "sm.sure",          new Pair("SURE?", "SIGURNO?") },
            { "sm.choose_realm",  new Pair("CHOOSE YOUR REALM", "ODABERI SVOJE KRALJEVSTVO") },
            { "sm.new_campaign",  new Pair("NEW CAMPAIGN", "NOVA KAMPANJA") },
            { "sm.warn",          new Pair("Starting a new game resets THIS slot's map progress.\nCard unlocks are shared across all saves and stay unlocked.", "Nova igra resetira napredak mape OVOG slota.\nOtključane karte dijele se kroz sve saveove i ostaju otključane.") },
            { "sm.race_pick",     new Pair("RACE:  {0}", "RASA:  {0}") },
            { "sm.begin",         new Pair("BEGIN", "POČNI") },
            { "sm.cancel",        new Pair("CANCEL", "ODUSTANI") },
            { "sm.hero_tag",      new Pair("HERO", "JUNAK") },
            { "sm.final_battle",  new Pair("FINAL BATTLE", "ZAVRŠNA BITKA") },
            { "sm.battle_n",      new Pair("BATTLE {0}", "BITKA {0}") },
            { "sm.enemy_line",    new Pair("Enemy: {0}   ·   Difficulty {1} ({2})", "Protivnik: {0}   ·   Težina {1} ({2})") },
            { "sm.enemy_hero",    new Pair("ENEMY HERO", "PROTIVNIČKI JUNAK") },
            { "sm.reward_golden", new Pair("REWARD: GOLDEN CARD FRAME", "NAGRADA: ZLATNI OKVIR KARTE") },
            { "sm.reward_none",   new Pair("No card reward for this battle", "Nema nagrade u kartama za ovu bitku") },
            { "sm.reward_claimed",new Pair("REWARD: already claimed", "NAGRADA: već preuzeta") },
            { "sm.reward_onwin",  new Pair("REWARD ON VICTORY", "NAGRADA NA POBJEDU") },
            { "sm.replay",        new Pair("REPLAY", "PONOVI") },
            { "sm.fight",         new Pair("FIGHT", "BORI SE") },
            { "sm.lore_final",    new Pair("THE FINAL BATTLE · ASHMOUNT", "ZAVRŠNA BITKA · ASHMOUNT") },
            { "sm.lore_sub",      new Pair("{0} · BATTLE {1}", "{0} · BITKA {1}") },

            // race names (shown across screens; enum stays English in logic)
            { "race.orcs",   new Pair("Orcs",   "Orci") },
            { "race.elves",  new Pair("Elves",  "Vilenjaci") },
            { "race.humans", new Pair("Humans", "Ljudi") },
            { "race.demons", new Pair("Demons", "Demoni") },

            // achievements (title + desc, keyed by id)
            { "ach.first_win.title", new Pair("First Blood", "Prva krv") },
            { "ach.first_win.desc",  new Pair("Win your first match.", "Pobijedi svoj prvi meč.") },
            { "ach.games10.title",   new Pair("Veteran", "Veteran") },
            { "ach.games10.desc",    new Pair("Play 10 matches.", "Odigraj 10 mečeva.") },
            { "ach.streak5.title",   new Pair("Unstoppable", "Nezaustavljiv") },
            { "ach.streak5.desc",    new Pair("Win 5 matches in a row.", "Pobijedi 5 mečeva zaredom.") },
            { "ach.elo2000.title",   new Pair("Climber", "Penjač") },
            { "ach.elo2000.desc",    new Pair("Reach 2000 ELO in ranked.", "Dosegni 2000 ELO u rangiranom.") },
            { "ach.story_final.title",new Pair("Realm Breaker", "Razbijač kraljevstava") },
            { "ach.story_final.desc", new Pair("Defeat the Hollow King in the final battle.", "Porazi Šupljeg Kralja u završnoj bitci.") },
            { "ach.golden.title",    new Pair("Gilded", "Pozlaćen") },
            { "ach.golden.desc",     new Pair("Unlock a golden card frame (finish a campaign).", "Otključaj zlatni okvir karte (završi kampanju).") },
            { "ach.gauntlet3.title", new Pair("Gauntlet Initiate", "Gauntlet početnik") },
            { "ach.gauntlet3.desc",  new Pair("Win 3 battles in a single gauntlet run.", "Pobijedi 3 bitke u jednom gauntlet runu.") },
            { "ach.gauntlet6.title", new Pair("Gauntlet Veteran", "Gauntlet veteran") },
            { "ach.gauntlet6.desc",  new Pair("Win 6 battles in a single gauntlet run.", "Pobijedi 6 bitaka u jednom gauntlet runu.") },
            { "ach.gauntlet10.title",new Pair("Gauntlet Legend", "Gauntlet legenda") },
            { "ach.gauntlet10.desc", new Pair("Win 10 battles in a single gauntlet run.", "Pobijedi 10 bitaka u jednom gauntlet runu.") },
            { "ach.collect_first.title",new Pair("Collector", "Sakupljač") },
            { "ach.collect_first.desc", new Pair("Unlock your first story reward card.", "Otključaj svoju prvu story nagradnu kartu.") },
            { "ach.collect_race.title", new Pair("Race Complete", "Rasa kompletirana") },
            { "ach.collect_race.desc",  new Pair("Unlock every reward card of a single race.", "Otključaj sve nagradne karte jedne rase.") },
            { "ach.collect_50.title", new Pair("Hoarder", "Skupljač blaga") },
            { "ach.collect_50.desc",  new Pair("Unlock 50 reward cards across all races.", "Otključaj 50 nagradnih karata kroz sve rase.") },
            { "ach.collect_all.title", new Pair("Master Collector", "Majstor sakupljač") },
            { "ach.collect_all.desc",  new Pair("Unlock every reward card in the game.", "Otključaj sve nagradne karte u igri.") },
            { "ach.second_hero.title", new Pair("Twin Champions", "Dvojni prvaci") },
            { "ach.second_hero.desc",  new Pair("Unlock a second hero for any race.", "Otključaj drugog junaka za bilo koju rasu.") },
            { "ach.all_heroes.title", new Pair("Hall of Heroes", "Dvorana junaka") },
            { "ach.all_heroes.desc",  new Pair("Unlock the second hero of all four races.", "Otključaj drugog junaka svih četiriju rasa.") },
            { "ach.all_golden.title", new Pair("All That Glitters", "Sve što sja") },
            { "ach.all_golden.desc",  new Pair("Unlock the golden frame for all four races.", "Otključaj zlatni okvir za sve četiri rase.") },
            { "ach.elo2500.title",   new Pair("Grandmaster", "Velemajstor") },
            { "ach.elo2500.desc",    new Pair("Reach 2500 ELO in ranked.", "Dosegni 2500 ELO u rangiranom.") },
            { "ach.games50.title",   new Pair("Battle-Hardened", "Prekaljen u borbi") },
            { "ach.games50.desc",    new Pair("Play 50 matches.", "Odigraj 50 mečeva.") },

            // main menu tutorial button + tutorial nav
            { "menu.title",    new Pair("LORD OF THE REALMS", "VLADAR SVJETOVA") },
            { "mp.custom",     new Pair("CUSTOM MATCH", "PRILAGOĐENI MEČ") },
            { "menu.howtoplay", new Pair("HOW TO PLAY", "KAKO IGRATI") },
            { "tut.next",     new Pair("NEXT", "DALJE") },
            { "tut.practice", new Pair("PRACTICE MATCH", "PROBNI MEČ") },

            // interaktivni tutorial (skriptirani mec)
            { "tut.blocked", new Pair("Follow the current step.", "Prati trenutni korak.") },
            { "tut.done",    new Pair("That's the core of the game. Keep playing this match, or exit.", "To je srce igre. Nastavi igrati ovaj meč, ili izadi.") },
            { "tut.exit",    new Pair("EXIT TUTORIAL", "IZAĐI IZ TUTORIALA") },
            { "tut.rules",   new Pair("RULES", "PRAVILA") },
            { "tut.start",   new Pair("START TUTORIAL", "POKRENI TUTORIAL") },

            // koraci skriptiranog tutoriala (Demoni vs Ljudi)
            { "tut.s1", new Pair("Both players start at 40 HP. You win by getting the enemy hero to 0. Your health is on the left, the enemy's above it.", "Oba igrača počinju sa 40 HP. Pobjeđuješ kad protivničkog junaka spustiš na 0. Tvoje je zdravlje lijevo, protivnikovo iznad njega.") },
            { "tut.s2", new Pair("POWER is your resource, shown next to your health. You gain +1 max POWER every turn (up to 10), and it refills at the start of your turn.", "POWER je tvoj resurs, prikazan uz zdravlje. Svaki potez dobiješ +1 maksimalnog POWER-a (do 10), i puni se na početku tvog poteza.") },
            { "tut.s3", new Pair("You draw a card every turn, and because your hand was empty you drew 2 instead of 1. You are never left with nothing.", "Svaki potez izvučeš kartu, a pošto ti je ruka bila prazna izvukao si 2 umjesto 1. Nikad ne ostaneš bez ičega.") },
            { "tut.s4", new Pair("Play Imp. It costs 1 POWER, exactly what you have this turn. Click it in your hand.", "Odigraj Impa. Košta 1 POWER, točno koliko imaš ovaj potez. Klikni ga u ruci.") },
            { "tut.s5", new Pair("Imp is on the board, but it can't attack yet: most units can only attack the turn AFTER they are played.", "Imp je na ploči, ali još ne može napasti: većina jedinica može napasti tek u potezu NAKON što su odigrane.") },
            { "tut.s6", new Pair("That's everything for this turn. Click END TURN.", "To je sve za ovaj potez. Klikni ZAVRŠI POTEZ.") },
            { "tut.s7", new Pair("Play Void Imp. Some cards do something the moment they land, and this one draws you a card straight away.", "Odigraj Void Impa. Neke karte nešto naprave čim slete, a ova ti odmah izvuče jednu kartu.") },
            { "tut.s8", new Pair("Now attack. Click your Imp, then the enemy Footman. Both units deal their attack to each other, so both take damage.", "Sad napadni. Klikni svog Impa, pa protivničkog Footmana. Obje jedinice nanesu svoj napad jedna drugoj, pa obje prime štetu.") },
            { "tut.s9", new Pair("Click END TURN.", "Klikni ZAVRŠI POTEZ.") },
            { "tut.s10", new Pair("Play Soul Siphon to heal back the 5 the enemy spell took off you. Healing never takes you above 40.", "Odigraj Soul Siphon i vrati 5 zdravlja koje ti je protivnička čarolija oduzela. Liječenje te nikad ne diže preko 40.") },
            { "tut.s10b", new Pair("The enemy board is empty, so your Imp can reach their hero. Click Imp, then click the enemy hero. That is how you win.", "Protivnička je ploča prazna, pa tvoj Imp može do njihovog junaka. Klikni Impa, zatim protivničkog junaka. Tako se pobjeđuje.") },
            { "tut.s11", new Pair("Click END TURN.", "Klikni ZAVRŠI POTEZ.") },
            { "tut.s12", new Pair("The enemy played Shield Guard, which has Taunt: while it stands, your normal units must attack IT and can't reach the enemy hero.", "Protivnik je odigrao Shield Guard, koji ima Taunt: dok stoji, tvoje obične jedinice moraju napasti NJEGA i ne mogu do protivničkog junaka.") },
            { "tut.s13", new Pair("Play Hellhound. It is an Assassin, so it ignores Taunt when attacking units and can strike past that wall.", "Odigraj Hellhounda. On je Assassin, pa ignorira Taunt pri napadu na jedinice i može udariti iza tog zida.") },
            { "tut.s14", new Pair("Now play Dark Ritual. It raises your MAX POWER by 1 permanently. That is called ramp, and it means bigger cards sooner.", "Sad odigraj Dark Ritual. Trajno ti diže MAKSIMALNI POWER za 1. To se zove ramp, i znači skuplje karte ranije.") },
            { "tut.s15", new Pair("Click END TURN.", "Klikni ZAVRŠI POTEZ.") },
            { "tut.s16", new Pair("The enemy played Templar, which has Shield. Attack it with your Hellhound. The Assassin walks past the Taunt to reach it.", "Protivnik je odigrao Templara, koji ima Shield. Napadni ga svojim Hellhoundom. Assassin prođe pokraj Taunta da dođe do njega.") },
            { "tut.s17", new Pair("See BLOCKED? Shield stops the FIRST damage a unit would take and is then used up. Templar hit back, and your Hellhound fell.", "Vidiš BLOCKED? Shield zaustavi PRVU štetu koju bi jedinica primila i time se potroši. Templar je uzvratio i tvoj Hellhound je pao.") },
            { "tut.s18", new Pair("Play Succubus. It has Lifesteal: the damage it deals in combat also heals your hero.", "Odigraj Succubus. Ima Lifesteal: šteta koju nanese u borbi ujedno liječi tvog junaka.") },
            { "tut.s19", new Pair("Click END TURN.", "Klikni ZAVRŠI POTEZ.") },
            { "tut.s20", new Pair("Attack the Shield Guard with your Succubus and watch your own health go UP while you deal damage. That is Lifesteal.", "Napadni Shield Guard svojom Succubus i gledaj kako ti zdravlje RASTE dok nanosiš štetu. To je Lifesteal.") },
            { "tut.s21", new Pair("Taunt works for you too. Play Fel Guard. While it lives, the enemy must attack it instead of your hero.", "Taunt radi i za tebe. Odigraj Fel Guard. Dok je živ, protivnik mora napadati njega umjesto tvog junaka.") },
            { "tut.s22", new Pair("Now play Fel Snare. It's a hidden trap: it goes face-down and springs when the enemy attacks.", "Sad odigraj Fel Snare. To je skrivena zamka: postavlja se licem prema dolje i opali kad protivnik napadne.") },
            { "tut.s23", new Pair("Click END TURN and watch the trap.", "Klikni ZAVRŠI POTEZ i gledaj zamku.") },
            { "tut.s24", new Pair("The enemy attacked, so your trap sprang: it hit the attacker and healed you. A trap only works once.", "Protivnik je napao, pa je tvoja zamka opalila: pogodila je napadača i izliječila tebe. Zamka radi samo jednom.") },
            { "tut.s25", new Pair("Play Void Scout. A Scout can reveal and disarm an enemy hidden trap instead of attacking.", "Odigraj Void Scout. Scout umjesto napada može otkriti i razoružati protivničku skrivenu zamku.") },
            { "tut.s26", new Pair("Click END TURN.", "Klikni ZAVRŠI POTEZ.") },
            { "tut.s27", new Pair("The enemy set a face-down card of their own. Click your Void Scout, then click that face-down card to disarm it.", "Protivnik je i sam postavio kartu licem prema dolje. Klikni svog Void Scouta, zatim tu okrenutu kartu da je razoružaš.") },
            { "tut.s28", new Pair("Play your hero, Vor'gathul. A hero fights like a unit, and if it dies it is not lost. It goes back into your deck, somewhere in the next few cards.", "Odigraj svog junaka, Vor'gathula. Junak se bori kao jedinica, a ako pogine nije izgubljen. Vraća se u špil, među sljedećih nekoliko karata.") },
            { "tut.s29", new Pair("Click END TURN.", "Klikni ZAVRŠI POTEZ.") },
            { "tut.s30", new Pair("Look at Abyssal Titan in your hand: the red -2 badge means playing it permanently LOWERS your max POWER by 2. Huge cards cost more than POWER.", "Pogledaj Abyssal Titana u ruci: crveni -2 znači da ti igranje trajno SMANJUJE maksimalni POWER za 2. Ogromne karte koštaju više od POWER-a.") },
            { "tut.s31", new Pair("Play it and watch your max POWER drop. A 10/10 is worth the cost.", "Odigraj ga i gledaj kako ti maksimalni POWER pada. Za 10/10 se isplati.") },
            { "tut.s32", new Pair("That's every mechanic in the game. Finish this match however you like, or exit.", "To su sve mehanike u igri. Dovrši ovaj meč kako želiš, ili izađi.") },

            // tutorial pages
            { "tut.p1.h", new Pair("THE GOAL", "CILJ IGRE") },
            { "tut.p1.b", new Pair("You and your opponent each start with 40 HP. Win by reducing the enemy hero's HP to 0. You take turns playing cards and attacking.", "Ti i protivnik počinjete sa 40 HP. Pobjeđuješ tako da protivnikovom junaku spustiš HP na 0. Igrate naizmjence, igrajući karte i napadajući.") },
            { "tut.p2.h", new Pair("POWER", "POWER") },
            { "tut.p2.b", new Pair("POWER is your resource. You gain +1 max POWER each turn (up to 10) and refill at the start of your turn. Cards cost POWER to play. Some cards ramp your POWER faster; a few costly cards permanently drain your max POWER.", "POWER je tvoj resurs. Svaki potez dobiješ +1 maksimalnog POWER-a (do 10) i napuniš ga na početku poteza. Karte se igraju za POWER. Neke karte brže dižu POWER; nekoliko skupih karata trajno smanjuje tvoj maksimalni POWER.") },
            { "tut.p3.h", new Pair("YOUR TURN", "TVOJ POTEZ") },
            { "tut.p3.b", new Pair("On your turn you draw a card, then spend POWER to play units and spells from your hand. Units can attack the turn after they're played. Click a unit, then a target, to attack. Press END TURN when you're done.", "Na tvom potezu izvučeš kartu, zatim trošiš POWER da odigraš jedinice i čarolije iz ruke. Jedinice mogu napasti tek sljedeći potez nakon što su odigrane. Klikni jedinicu pa cilj da napadneš. Klikni ZAVRŠI POTEZ kad završiš.") },
            { "tut.p4.h", new Pair("UNITS", "JEDINICE") },
            { "tut.p4.b", new Pair("Units fight on the board. Basic units have no special rule. Taunt: enemies must attack it first. Assassin: ignores Taunt and can't hit the enemy hero while a Taunt stands. Scout: can reveal an enemy hidden spell instead of attacking.", "Jedinice se bore na ploči. Osnovne jedinice nemaju posebno pravilo. Taunt: neprijatelji je prvo moraju napasti. Assassin: ignorira Taunt i ne može pogoditi neprijateljskog junaka dok Taunt stoji. Scout: umjesto napada može otkriti neprijateljsku skrivenu čaroliju.") },
            { "tut.p5.h", new Pair("SPELLS & TRAPS", "ČAROLIJE I ZAMKE") },
            { "tut.p5.b", new Pair("Spells have one-time effects. Offensive spells deal damage to a target. Some spells can be set face-down as a hidden trap: when the enemy attacks, it triggers and hits the attacker. An enemy Scout can disarm it.", "Čarolije imaju jednokratne efekte. Napadačke čarolije nanose štetu cilju. Neke se čarolije mogu postaviti licem prema dolje kao skrivena zamka: kad neprijatelj napadne, aktivira se i pogodi napadača. Neprijateljski Scout je može razoružati.") },
            { "tut.p6.h", new Pair("KEYWORDS", "KEYWORDOVI") },
            { "tut.p6.b", new Pair("Watch for keywords: Lifesteal heals you for the damage that unit deals. Shield prevents the first hit a unit would take. Other cards heal, buff your units, or hit all enemies at once. Hover any card to read exactly what it does.", "Pazi na keywordove: Lifesteal te liječi za štetu koju ta jedinica nanese. Shield spriječi prvi udarac koji bi jedinica primila. Druge karte liječe, jačaju tvoje jedinice ili pogode sve neprijatelje odjednom. Prijeđi mišem preko karte da pročitaš točno što radi.") },
            { "tut.p7.h", new Pair("HEROES & DECK", "JUNACI I ŠPIL") },
            { "tut.p7.b", new Pair("Each deck has 29 cards plus one hero, drawn from 4 races (Orcs, Elves, Humans, Demons). Your hero fights like a unit; if it dies it is not lost, it goes back into your deck among the next few cards.", "Svaki špil ima 29 karata plus jednog junaka, iz 4 rase (Orci, Vilenjaci, Ljudi, Demoni). Tvoj se junak bori kao jedinica; ako pogine nije izgubljen, vraća se u špil među sljedećih nekoliko karata.") },
            { "tut.p8.h", new Pair("READY?", "SPREMAN?") },
            { "tut.p8.b", new Pair("That's the basics! The best way to learn is to play. Try a practice match against an easy bot, or head back to the menu.", "To su osnove! Najbolji način da naučiš je da igraš. Probaj probni meč protiv laganog bota, ili se vrati na izbornik.") },
        };
    }
}
