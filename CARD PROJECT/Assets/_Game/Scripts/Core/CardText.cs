using System.Collections.Generic;

namespace LordOfTheRealms
{
    // Hrvatski prijevod flavour teksta karata i herojevih opisa pravila.
    // Kljuc je IME KARTE (ostaje englesko), pa ovo NE trazi regeneraciju assetsa:
    // engleski tekst i dalje zivi u generiranom assetu i sluzi kao fallback.
    public static class CardText
    {
        // flavour/lore linija ispod pravila
        public static string Lore(CardData data)
        {
            if (data == null) return "";
            if (Localization.Current == Language.Croatian
                && LoreHr.TryGetValue(data.cardName, out var hr)) return hr;
            return data.description;
        }

        // herojev opis pravila (heroji imaju zaseban abilityDescription)
        public static string HeroRule(HeroCardData hero)
        {
            if (hero == null) return "";
            if (Localization.Current == Language.Croatian
                && HeroRuleHr.TryGetValue(hero.cardName, out var hr)) return hr;
            return hero.abilityDescription;
        }

        private static readonly Dictionary<string, string> HeroRuleHr = new()
        {
            { "Kragmaw, Warchief", "Može napasti u potezu u kojem je prizvan." },
            { "Dro'gan the Relentless", "Dok je živ, ostale tvoje jedinice imaju +2 napada." },
            { "Faelan, Archdruid", "Dok je živ, dobivaš +1 max POWER na početku svakog svog poteza." },
            { "Sylvara, Moon Priestess", "Kad je prizvana, vuci 2 karte." },
            { "King Cedric", "Kad je prizvan, daj svojim jedinicama +2 napada." },
            { "Baelric the Lightbringer", "Dok je živ, tvoje jedinice primaju 1 manje štete." },
            { "Vor'gathul", "Kad je prizvan, nanesi 3 štete svim neprijateljskim jedinicama." },
            { "Zar'thul the Destroyer", "Dok je živ, vraća 2 zdravlja tvom igraču na početku svakog tvog poteza." },
        };

        private static readonly Dictionary<string, string> LoreHr = new()
        {
            // Orcs
            { "Goblin Cutter", "Malen i preblizu tvojim gležnjevima." },
            { "Wolf Rider", "Udare u liniju prije nego što čuješ zavijanje." },
            { "Blood Stalker", "Odabere grlo koje želi i uzme ga." },
            { "Orc Berserker", "Bol ga samo ubrza." },
            { "Goblin Shieldbearer", "Drži zid da se veći mogu najesti." },
            { "Warband Scout", "Zna svaku zamku prije nego što zamka zna samu sebe." },
            { "Warlord", "Gdje zabode svoj barjak, horda ga slijedi." },
            { "Bloodrage Totem", "Njegov bubanj je zvuk vojske koja se budi." },
            { "Raider", "Uzme što želi i spali ostalo." },
            { "Ogre Bruiser", "Dvije glave, jedan vrlo jednostavan plan." },
            { "Goblin Sapper", "Voli fitilje malo više nego što je zdravo." },
            { "War Drummer", "Određuje ritam pokolja." },
            { "Hurled Axe", "Bačena uz gunđanje i s nepogrešivim ciljem." },
            { "War Cry", "Urlik koji kukavice pretvara u ubojice." },
            { "Pillage", "Sve što vrijedi, ništa ostavljeno." },
            { "Reckless Charge", "Strategija je za one koji planiraju preživjeti." },
            { "Kragmaw, Warchief", "Njegova sjekira ne čeka dopuštenje." },
            { "Dro'gan the Relentless", "Horda ide kamo on ide." },

            // Elves
            { "Elven Sentry", "Strpljiv kao korijenje, oštar kao prvi mraz." },
            { "Moonwell Keeper", "Brine se o vodama koje hrane staru magiju." },
            { "Wisp Scout", "Treptaj svjetla, i tvoje su tajne nestale." },
            { "Ancient Protector", "Stariji je od kraljevstava koja čuva." },
            { "Starcaller", "Čita sutra u večerašnjem nebu." },
            { "Sylvan Archer", "Jedna strijela, jedan dah, jedna sigurnost." },
            { "Worldtree Colossus", "Šuma koja je naučila hodati i ratovati." },
            { "Dryad", "Gdje ona kroči, zelene se stvari sjete rasti." },
            { "Treant Guardian", "Kora tvrda kao željezo, i jednako strpljiva." },
            { "Moon Priestess", "Njezine se molitve uslišuju u srebrnom svjetlu." },
            { "Grove Warden", "Gnjev gaja pretočen u korijen i granu." },
            { "Branch of Yggdrasil", "Odrezak sa stabla koje drži svijet." },
            { "Thornweave Ward", "Posegni za vilenjakom, izgubi ruku." },
            { "Nature's Bounty", "Šuma uvijek uzvraća vjernima." },
            { "Entangling Roots", "Samo tlo odlučuje da si otišao dovoljno daleko." },
            { "Moonfire", "Šapat zvjezdane svjetlosti s ubojitim rubom." },
            { "Faelan, Archdruid", "Spava stoljećima, budi se u oluji." },
            { "Sylvara, Moon Priestess", "Mjesec je sluša." },

            // Humans
            { "Footman", "Prvi red, i ponosan na to." },
            { "Knight", "Zakletvom vezan, oklopljen, neustrašiv." },
            { "Shield Guard", "Linija drži jer on drži." },
            { "Rogue Blade", "Nestane prije nego što se uzbuna uopće digne." },
            { "Ranger", "Vidi zasjedu dva grebena dalje." },
            { "Cavalry Captain", "Njegov juriš odlučuje na koju stranu bojište pukne." },
            { "Cleric", "Liječi ranjene, smiruje uplašene." },
            { "Catapult", "Iznova ispisuje zidine svake opsade." },
            { "Royal Guard", "Posljednje što stoji između prijestolja i tame." },
            { "Squire", "Zelen i tiho hrabar." },
            { "Crossbowman", "Probija oklop s četrdeset koraka." },
            { "Paladin", "Vjera i čelik, podjednako." },
            { "Field Medic", "Vuče pale natrag u borbu." },
            { "Fireball", "Kugla propasti, bačena jednom riječju." },
            { "Holy Light", "Toplina koja jednako zatvara rane i sumnje." },
            { "Rally", "Jedan glas, i cijela se linija ispravi." },
            { "Smite", "Presuda odozgo, bez rasprave." },
            { "King Cedric", "Nosi krunu i račun koji ide uz nju." },
            { "Baelric the Lightbringer", "Ustaje sa zorom, s bojnim maljem u ruci." },

            // Demons
            { "Imp", "Mala nevolja koja se smije dok te pali." },
            { "Hellhound", "Njuši strah i nalazi ga slasnim." },
            { "Soul Harvester", "Skuplja ono što umirućima više ne treba." },
            { "Void Scout", "Vidi kroz sjenu, i sam je sjena." },
            { "Fel Guard", "Okovan dužnošću, gladan oslobođenja." },
            { "Dreadlord", "Hrani se užasom koji tako lako izaziva." },
            { "Infernal", "Pada s neba kao goruća presuda." },
            { "Succubus", "Sladak šapat s kobnim završetkom." },
            { "Pit Lord", "Jama je poslala ono najgore što ima." },
            { "Fel Imp", "Dvostruko zlobe u upola manjem tijelu." },
            { "Void Ritualist", "Trguje dijelovima sebe za mračniju moć." },
            { "Shadow Bolt", "Strelica čiste pakosti." },
            { "Chaos Nova", "Trenutak u kojem sve u blizini prestane postojati." },
            { "Demonic Pact", "Potpiši ovdje. Cijena dolazi kasnije." },
            { "Sacrifice", "Nešto se mora dati za nešto dobiveno." },
            { "Immolate", "Spora vatra, siguran kraj." },
            { "Vor'gathul", "Cijeli su svjetovi stali na mah njegove ruke." },
            { "Zar'thul the Destroyer", "Njegova je krv osudila narode." },

            // reward val 2
            { "Bone Crusher", "Gdje zamahne, formacije prestaju postojati." },
            { "Horde Vanguard", "Prvi kroz proboj, zadnji koji padne." },
            { "Bear Trap", "Stepa drži vlastite zube skrivene." },
            { "Tripline", "Jedan komad užeta, nisko postavljen, ondje gdje nitko ne gleda." },
            { "Spiked Pit", "Iskopali su je noćas i prekrili prije zore." },
            { "Root Snare", "Šumsko se tlo sklopi oko gležnja." },
            { "Caltrops", "Jeftini, tihi, i konji ih prvi nađu." },
            { "Sanctified Ground", "Blagoslovljena zemlja koja kažnjava onoga tko je prijeđe." },
            { "Shadow Pit", "Rupa koje malo prije nije bilo." },
            { "Soul Trap", "Uzme nešto od onoga tko je aktivira." },
            { "Earthshaker", "Tlo pamti gnjev horde." },
            { "Faerie Dragon", "Blijesak krila, i tajne polete." },
            { "Ancient of War", "Bio je star kad su ratovi bili mladi." },
            { "Moonlit Snare", "Zakorači u srebrnu svjetlost. Vidi što će biti." },
            { "Rejuvenation", "Šuma krpa ono što bitka slomi." },
            { "Templar", "Njegova je vjera oklop koji nijedna oštrica ne nalazi." },
            { "Siege Commander", "Svaki zid pada po njegovom rasporedu." },
            { "Holy Ward", "Udari vjernika, odgovaraj Svjetlu." },
            { "Divine Storm", "Presuda za sve odjednom." },
            { "Nightmare Steed", "Galopira kroz snove i iz njih van." },
            { "Void Terror", "Dvije gladi u jednom tijelu." },
            { "Fel Snare", "Jama nagrađuje znatiželju lancima." },
            { "Chaos Bolt", "Sirova entropija, naciljana pakošću." },

            // reward val 3
            { "Ashmere Outrider", "Prijeđe gaz prije nego što se voda smiri." },
            { "Silverwood Ranger", "Već si joj na nišanu. I to već neko vrijeme." },
            { "Highmarch Halberdier", "Plaćen na vrijeme, izmuštran do besvijesti, i drži liniju." },
            { "Cinder Fiend", "Rođen ondje gdje je pepeo još topao, i gladan još." },
        };
    }
}
