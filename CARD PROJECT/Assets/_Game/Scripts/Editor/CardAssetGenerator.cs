#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace LordOfTheRealms.EditorTools
{
    // Generira sve card ScriptableObject assete iz tuniranih podataka. Svaka rasa
    // ima prosireni pool i DVA heroja (jedan OnEnter, jedan Passive). Heroji se pri
    // smrti vracaju u deck. Menu: Lord of the Realms > Generate Card Assets
    public static class CardAssetGenerator
    {
        private const string Root = "Assets/_Game/CardData";

        private struct U { public string name; public int cost, atk, hp; public UnitType type; public CardAbility ab; public int abv; public int copies; public int mpc; }
        private struct S { public string name; public int cost, power; public SpellType type; public bool hidden; public CardAbility ab; public int abv; public int copies; }
        private struct H { public string name; public int cost, atk, hp; public CardAbility ab; public int abv; public HeroAbilityMode mode; public string desc; }

        [MenuItem("Lord of the Realms/Generate Card Assets")]
        public static void Generate()
        {
            int count = 0;
            count += BuildOrcs();
            count += BuildElves();
            count += BuildHumans();
            count += BuildDemons();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Lord of the Realms",
                $"{count} cards generated. Run 'Sync Cards To Resources' next.",
                "OK");
        }

        // ------------------------------------------------------------- ORCS
        private static int BuildOrcs()
        {
            var race = Race.Orcs;
            int n = 0;
            var units = new[]
            {
                new U{name="Goblin Cutter",cost=1,atk=2,hp=2,type=UnitType.Basic,copies=3},
                new U{name="Wolf Rider",cost=2,atk=3,hp=2,type=UnitType.Basic,copies=3},
                new U{name="Blood Stalker",cost=3,atk=3,hp=2,type=UnitType.Assassin,copies=2},
                new U{name="Orc Berserker",cost=3,atk=5,hp=3,type=UnitType.Basic,copies=2},
                new U{name="Goblin Shieldbearer",cost=2,atk=1,hp=4,type=UnitType.Taunt,copies=2},
                new U{name="Warband Scout",cost=2,atk=3,hp=2,type=UnitType.Scout,copies=2},
                new U{name="Warlord",cost=5,atk=5,hp=8,type=UnitType.Basic,copies=2},
                new U{name="Bloodrage Totem",cost=4,atk=4,hp=4,type=UnitType.Basic,ab=CardAbility.PowerRamp,abv=1,copies=2},
                // dodatni pool za slaganje deckova
                new U{name="Raider",cost=4,atk=5,hp=3,type=UnitType.Basic,copies=2},
                new U{name="Ogre Bruiser",cost=6,atk=7,hp=6,type=UnitType.Basic,copies=2},
                new U{name="Goblin Sapper",cost=1,atk=2,hp=1,type=UnitType.Assassin,copies=2},
                new U{name="War Drummer",cost=3,atk=2,hp=3,type=UnitType.Basic,ab=CardAbility.BuffAttack,abv=1,copies=2},
                // novi val (story unlockovi)
                new U{name="Axe Thrower",cost=1,atk=2,hp=2,type=UnitType.Basic,copies=2},
                new U{name="War Pup",cost=2,atk=3,hp=3,type=UnitType.Basic,copies=2},
                new U{name="Chieftain's Guard",cost=3,atk=3,hp=4,type=UnitType.Taunt,copies=2},
                new U{name="Siege Ogre",cost=5,atk=5,hp=5,type=UnitType.Basic,copies=2},
                new U{name="Gorehowl Colossus",cost=8,atk=10,hp=8,type=UnitType.Basic,copies=2,mpc=1},
                // reward val 2 (Demons 1-4 story unlockovi)
                new U{name="Bone Crusher",cost=4,atk=5,hp=4,type=UnitType.Basic,copies=2},
                new U{name="Horde Vanguard",cost=5,atk=4,hp=6,type=UnitType.Taunt,copies=2},
                // reward val 3 (popunjava zadnji prazni story level)
                new U{name="Ashmere Outrider",cost=4,atk=5,hp=3,type=UnitType.Basic,copies=2},
            };
            var spells = new[]
            {
                new S{name="Hurled Axe",cost=1,power=3,type=SpellType.Offensive,copies=2},
                new S{name="War Cry",cost=2,type=SpellType.Field,ab=CardAbility.BuffAttack,abv=2,copies=2},
                new S{name="Pillage",cost=2,power=5,type=SpellType.Offensive,copies=2},
                // novi val: Rampage i Second Wind su unique mehanike
                new S{name="Rampage",cost=3,power=4,type=SpellType.Offensive,ab=CardAbility.KillDraw,copies=2},
                new S{name="Second Wind",cost=5,type=SpellType.Field,ab=CardAbility.RefreshAttacks,copies=2},
                new S{name="Sharpen Blades",cost=1,type=SpellType.Field,ab=CardAbility.BuffAttack,abv=1,copies=2},
                new S{name="Goblin Bomb",cost=2,power=3,type=SpellType.Offensive,copies=2},
                new S{name="War Horn",cost=2,type=SpellType.Defensive,ab=CardAbility.Heal,abv=4,copies=2},
                // reward val 2: Bear Trap je hidden trigger (4 dmg napadacu)
                new S{name="Bear Trap",cost=2,power=4,type=SpellType.Defensive,hidden=true,copies=2},
                new S{name="Tripline",cost=1,power=2,type=SpellType.Defensive,hidden=true,ab=CardAbility.Draw,abv=1,copies=2},
                new S{name="Spiked Pit",cost=3,power=4,type=SpellType.Defensive,hidden=true,copies=2},
                new S{name="Earthshaker",cost=3,power=2,type=SpellType.Offensive,ab=CardAbility.Aoe,abv=2,copies=2},
            };
            var heroes = new[]
            {
                new H{name="Kragmaw, Warchief",cost=6,atk=6,hp=5,ab=CardAbility.ChargeFace,mode=HeroAbilityMode.OnEnter,
                      desc="Can attack the turn he's summoned."},
                new H{name="Dro'gan the Relentless",cost=7,atk=3,hp=7,ab=CardAbility.PassiveBuffAllies,abv=2,mode=HeroAbilityMode.Passive,
                      desc="While he lives, your other units have +2 attack."},
            };
            foreach (var u in units) { CreateUnit(race, u); n++; }
            foreach (var s in spells) { CreateSpell(race, s); n++; }
            foreach (var h in heroes) { CreateHero(race, h); n++; }
            return n;
        }

        // ------------------------------------------------------------- ELVES
        private static int BuildElves()
        {
            var race = Race.Elves;
            int n = 0;
            var units = new[]
            {
                new U{name="Elven Sentry",cost=1,atk=1,hp=3,type=UnitType.Taunt,copies=3},
                new U{name="Moonwell Keeper",cost=4,atk=2,hp=5,type=UnitType.Taunt,ab=CardAbility.PowerRamp,abv=1,copies=2},
                new U{name="Wisp Scout",cost=2,atk=3,hp=2,type=UnitType.Scout,copies=2},
                new U{name="Ancient Protector",cost=5,atk=4,hp=7,type=UnitType.Taunt,copies=2},
                new U{name="Starcaller",cost=4,atk=2,hp=4,type=UnitType.Basic,ab=CardAbility.Draw,abv=1,copies=2},
                new U{name="Sylvan Archer",cost=3,atk=4,hp=3,type=UnitType.Basic,copies=2},
                new U{name="Worldtree Colossus",cost=7,atk=7,hp=7,type=UnitType.Basic,copies=2},
                // extra pool
                new U{name="Dryad",cost=2,atk=1,hp=2,type=UnitType.Basic,ab=CardAbility.Heal,abv=2,copies=2},
                new U{name="Treant Guardian",cost=6,atk=4,hp=7,type=UnitType.Taunt,copies=2},
                new U{name="Moon Priestess",cost=5,atk=3,hp=5,type=UnitType.Basic,ab=CardAbility.Draw,abv=1,copies=2},
                new U{name="Grove Warden",cost=3,atk=2,hp=4,type=UnitType.Taunt,copies=2},
                // novi val (story unlockovi)
                new U{name="Sapling",cost=1,atk=2,hp=2,type=UnitType.Basic,copies=2},
                new U{name="Moon Owl",cost=2,atk=3,hp=2,type=UnitType.Scout,copies=2},
                new U{name="Grove Sentinel",cost=3,atk=2,hp=5,type=UnitType.Taunt,copies=2},
                new U{name="Emerald Drake",cost=5,atk=5,hp=5,type=UnitType.Basic,copies=2},
                new U{name="Forest Avatar",cost=8,atk=7,hp=10,type=UnitType.Basic,ab=CardAbility.HealAllUnits,copies=2,mpc=3},
                // reward val 2 (Demons 1-4 story unlockovi)
                new U{name="Faerie Dragon",cost=3,atk=4,hp=3,type=UnitType.Scout,copies=2},
                new U{name="Ancient of War",cost=7,atk=5,hp=8,type=UnitType.Taunt,copies=2},
                // reward val 3 (popunjava zadnji prazni story level)
                new U{name="Silverwood Ranger",cost=4,atk=3,hp=5,type=UnitType.Basic,copies=2},
            };
            var spells = new[]
            {
                new S{name="Branch of Yggdrasil",cost=2,type=SpellType.Field,ab=CardAbility.PowerRamp,abv=1,copies=2},
                new S{name="Thornweave Ward",cost=2,power=3,type=SpellType.Defensive,hidden=true,ab=CardAbility.Heal,abv=3,copies=2},
                new S{name="Nature's Bounty",cost=3,type=SpellType.Defensive,ab=CardAbility.Draw,abv=2,copies=2},
                new S{name="Entangling Roots",cost=2,power=3,type=SpellType.Offensive,copies=2},
                // novi val: Nature's Wrath i Tranquility su unique mehanike
                new S{name="Nature's Wrath",cost=5,type=SpellType.Offensive,ab=CardAbility.DamageEqualsMaxPower,copies=2},
                new S{name="Tranquility",cost=3,type=SpellType.Defensive,ab=CardAbility.HealAllUnits,copies=2},
                new S{name="Starfall",cost=3,power=5,type=SpellType.Offensive,copies=2},
                new S{name="Wisdom of Ages",cost=2,type=SpellType.Defensive,ab=CardAbility.Draw,abv=2,copies=2},
                new S{name="Wild Growth",cost=1,type=SpellType.Field,ab=CardAbility.BuffAttack,abv=1,copies=2},
                // reward val 2: Moonlit Snare je hidden trigger (3 dmg napadacu + draw 2)
                new S{name="Moonlit Snare",cost=2,power=3,type=SpellType.Defensive,hidden=true,ab=CardAbility.Draw,abv=2,copies=2},
                new S{name="Root Snare",cost=4,power=4,type=SpellType.Defensive,hidden=true,ab=CardAbility.Draw,abv=1,copies=2},
                new S{name="Rejuvenation",cost=2,type=SpellType.Defensive,ab=CardAbility.Heal,abv=4,copies=2},
            };
            var heroes = new[]
            {
                new H{name="Faelan, Archdruid",cost=6,atk=4,hp=8,ab=CardAbility.PassiveRampEachTurn,abv=1,mode=HeroAbilityMode.Passive,
                      desc="While he lives, gain +1 max POWER at the start of each of your turns."},
                new H{name="Sylvara, Moon Priestess",cost=6,atk=4,hp=6,ab=CardAbility.Draw,abv=2,mode=HeroAbilityMode.OnEnter,
                      desc="When summoned, draw 2 cards."},
            };
            foreach (var u in units) { CreateUnit(race, u); n++; }
            foreach (var s in spells) { CreateSpell(race, s); n++; }
            foreach (var h in heroes) { CreateHero(race, h); n++; }
            return n;
        }

        // ------------------------------------------------------------- HUMANS
        private static int BuildHumans()
        {
            var race = Race.Humans;
            int n = 0;
            var units = new[]
            {
                new U{name="Footman",cost=1,atk=1,hp=2,type=UnitType.Basic,copies=3},
                new U{name="Knight",cost=3,atk=3,hp=4,type=UnitType.Basic,copies=3},
                new U{name="Shield Guard",cost=2,atk=2,hp=5,type=UnitType.Taunt,copies=2},
                new U{name="Rogue Blade",cost=3,atk=3,hp=3,type=UnitType.Assassin,copies=2},
                new U{name="Ranger",cost=2,atk=2,hp=2,type=UnitType.Scout,copies=2},
                new U{name="Cavalry Captain",cost=4,atk=6,hp=4,type=UnitType.Basic,ab=CardAbility.BuffAttack,abv=1,copies=2},
                new U{name="Cleric",cost=2,atk=1,hp=3,type=UnitType.Basic,ab=CardAbility.Heal,abv=4,copies=2},
                new U{name="Catapult",cost=5,atk=6,hp=5,type=UnitType.Basic,copies=2},
                new U{name="Royal Guard",cost=6,atk=6,hp=8,type=UnitType.Taunt,copies=1},
                // extra pool
                new U{name="Squire",cost=1,atk=2,hp=2,type=UnitType.Basic,copies=2},
                new U{name="Crossbowman",cost=3,atk=3,hp=3,type=UnitType.Basic,copies=2},
                new U{name="Paladin",cost=5,atk=4,hp=6,type=UnitType.Taunt,ab=CardAbility.DivineShield,copies=2},
                new U{name="Field Medic",cost=3,atk=2,hp=3,type=UnitType.Basic,ab=CardAbility.Heal,abv=2,copies=2},
                // novi val (story unlockovi)
                new U{name="Militia",cost=1,atk=2,hp=2,type=UnitType.Basic,copies=2},
                new U{name="Chaplain",cost=2,atk=1,hp=3,type=UnitType.Basic,ab=CardAbility.Heal,abv=2,copies=2},
                new U{name="Captain of the Watch",cost=3,atk=3,hp=3,type=UnitType.Basic,ab=CardAbility.BuffAttack,abv=1,copies=2},
                new U{name="Siege Tower",cost=5,atk=3,hp=7,type=UnitType.Taunt,copies=2},
                new U{name="Grand Marshal",cost=8,atk=9,hp=9,type=UnitType.Basic,copies=2,mpc=1},
                // reward val 2 (Demons 1-4 story unlockovi)
                new U{name="Templar",cost=4,atk=3,hp=5,type=UnitType.Basic,ab=CardAbility.DivineShield,copies=2},
                new U{name="Siege Commander",cost=6,atk=5,hp=6,type=UnitType.Basic,ab=CardAbility.BuffAttack,abv=1,copies=2},
                // reward val 3 (popunjava zadnji prazni story level)
                new U{name="Highmarch Halberdier",cost=4,atk=3,hp=5,type=UnitType.Taunt,copies=2},
            };
            var spells = new[]
            {
                new S{name="Fireball",cost=4,power=7,type=SpellType.Offensive,copies=2},
                new S{name="Holy Light",cost=2,type=SpellType.Defensive,ab=CardAbility.Heal,abv=5,copies=2},
                new S{name="Rally",cost=3,type=SpellType.Field,ab=CardAbility.BuffAttack,abv=1,copies=2},
                // novi val: Call to Arms i Judgement su unique mehanike
                new S{name="Call to Arms",cost=3,type=SpellType.Field,ab=CardAbility.SummonTokens,abv=3,copies=2},
                new S{name="Judgement",cost=5,type=SpellType.Field,ab=CardAbility.ExecuteStrongest,copies=2},
                new S{name="Smite",cost=2,power=4,type=SpellType.Offensive,copies=2},
                new S{name="Blessing",cost=3,type=SpellType.Defensive,ab=CardAbility.Heal,abv=5,copies=2},
                new S{name="Battle Standard",cost=4,type=SpellType.Field,ab=CardAbility.BuffAttack,abv=2,copies=2},
                // reward val 2: Holy Ward je hidden trigger (2 dmg napadacu + heal 3)
                new S{name="Holy Ward",cost=3,power=2,type=SpellType.Defensive,hidden=true,ab=CardAbility.Heal,abv=3,copies=2},
                new S{name="Caltrops",cost=1,power=2,type=SpellType.Defensive,hidden=true,ab=CardAbility.Draw,abv=1,copies=2},
                new S{name="Sanctified Ground",cost=4,power=3,type=SpellType.Defensive,hidden=true,ab=CardAbility.Heal,abv=4,copies=2},
                new S{name="Divine Storm",cost=4,power=2,type=SpellType.Offensive,ab=CardAbility.Aoe,abv=2,copies=2},
            };
            var heroes = new[]
            {
                new H{name="King Cedric",cost=6,atk=5,hp=6,ab=CardAbility.BuffAttack,abv=2,mode=HeroAbilityMode.OnEnter,
                      desc="When summoned, give your units +2 attack."},
                new H{name="Baelric the Lightbringer",cost=7,atk=3,hp=9,ab=CardAbility.PassiveTauntShield,abv=1,mode=HeroAbilityMode.Passive,
                      desc="While he lives, your units take 1 less damage."},
            };
            foreach (var u in units) { CreateUnit(race, u); n++; }
            foreach (var s in spells) { CreateSpell(race, s); n++; }
            foreach (var h in heroes) { CreateHero(race, h); n++; }
            return n;
        }

        // ------------------------------------------------------------- DEMONS
        private static int BuildDemons()
        {
            var race = Race.Demons;
            int n = 0;
            var units = new[]
            {
                new U{name="Imp",cost=1,atk=2,hp=2,type=UnitType.Basic,copies=3},
                new U{name="Hellhound",cost=2,atk=3,hp=2,type=UnitType.Assassin,copies=2},
                new U{name="Soul Harvester",cost=3,atk=2,hp=3,type=UnitType.Basic,ab=CardAbility.Draw,abv=1,copies=2},
                new U{name="Void Scout",cost=2,atk=2,hp=2,type=UnitType.Scout,copies=2},
                new U{name="Fel Guard",cost=3,atk=1,hp=3,type=UnitType.Taunt,copies=2},
                new U{name="Dreadlord",cost=5,atk=3,hp=4,type=UnitType.Basic,ab=CardAbility.Heal,abv=2,copies=2},
                new U{name="Infernal",cost=6,atk=5,hp=6,type=UnitType.Basic,ab=CardAbility.Aoe,abv=2,copies=1},
                // extra pool
                new U{name="Succubus",cost=3,atk=3,hp=3,type=UnitType.Basic,ab=CardAbility.Lifesteal,copies=2},
                new U{name="Pit Lord",cost=6,atk=5,hp=7,type=UnitType.Taunt,copies=2},
                new U{name="Fel Imp",cost=2,atk=3,hp=2,type=UnitType.Basic,ab=CardAbility.Draw,abv=1,copies=2},
                new U{name="Void Ritualist",cost=4,atk=3,hp=3,type=UnitType.Basic,ab=CardAbility.PowerRamp,abv=1,copies=2},
                // novi val (story unlockovi)
                new U{name="Void Imp",cost=1,atk=1,hp=2,type=UnitType.Basic,ab=CardAbility.Draw,abv=1,copies=2},
                new U{name="Tormentor",cost=2,atk=3,hp=3,type=UnitType.Basic,copies=2},
                new U{name="Shadow Fiend",cost=3,atk=4,hp=2,type=UnitType.Assassin,copies=2},
                new U{name="Doom Guard",cost=5,atk=6,hp=5,type=UnitType.Basic,copies=2},
                new U{name="Abyssal Titan",cost=9,atk=10,hp=10,type=UnitType.Basic,ab=CardAbility.Aoe,abv=2,copies=2,mpc=2},
                // reward val 2 (Demons 1-4 story unlockovi)
                new U{name="Nightmare Steed",cost=4,atk=4,hp=3,type=UnitType.Assassin,copies=2},
                new U{name="Void Terror",cost=7,atk=6,hp=7,type=UnitType.Basic,copies=2},
                // reward val 3 (popunjava zadnji prazni story level)
                new U{name="Cinder Fiend",cost=4,atk=5,hp=3,type=UnitType.Basic,copies=2},
            };
            var spells = new[]
            {
                new S{name="Shadow Bolt",cost=2,power=2,type=SpellType.Offensive,copies=2},
                new S{name="Chaos Nova",cost=5,power=3,type=SpellType.Offensive,ab=CardAbility.Aoe,abv=3,copies=2},
                new S{name="Demonic Pact",cost=3,type=SpellType.Field,ab=CardAbility.PowerRamp,abv=1,copies=2},
                new S{name="Sacrifice",cost=1,type=SpellType.Defensive,ab=CardAbility.Draw,abv=2,copies=2},
                // novi val: Dark Bargain i Hellfire su unique mehanike
                new S{name="Dark Bargain",cost=2,type=SpellType.Field,ab=CardAbility.SacrificeDraw,abv=2,copies=2},
                new S{name="Hellfire",cost=4,type=SpellType.Field,ab=CardAbility.AoeAllDraw,abv=2,copies=2},
                new S{name="Immolate",cost=4,power=5,type=SpellType.Offensive,copies=2},
                new S{name="Dark Ritual",cost=2,type=SpellType.Field,ab=CardAbility.PowerRamp,abv=1,copies=2},
                new S{name="Soul Siphon",cost=3,type=SpellType.Defensive,ab=CardAbility.Heal,abv=5,copies=2},
                // reward val 2: Fel Snare je hidden trigger (3 dmg napadacu + heal 2)
                new S{name="Fel Snare",cost=3,power=3,type=SpellType.Defensive,hidden=true,ab=CardAbility.Heal,abv=2,copies=2},
                new S{name="Shadow Pit",cost=2,power=4,type=SpellType.Defensive,hidden=true,copies=2},
                new S{name="Soul Trap",cost=4,power=4,type=SpellType.Defensive,hidden=true,ab=CardAbility.Draw,abv=1,copies=2},
                new S{name="Chaos Bolt",cost=3,power=4,type=SpellType.Offensive,copies=2},
            };
            var heroes = new[]
            {
                new H{name="Vor'gathul",cost=8,atk=6,hp=7,ab=CardAbility.Aoe,abv=3,mode=HeroAbilityMode.OnEnter,
                      desc="When summoned, deal 3 damage to all enemy units."},
                new H{name="Zar'thul the Destroyer",cost=7,atk=6,hp=7,ab=CardAbility.PassiveHealEachTurn,abv=2,mode=HeroAbilityMode.Passive,
                      desc="While he lives, restore 2 health to your player at the start of each of your turns."},
            };
            foreach (var u in units) { CreateUnit(race, u); n++; }
            foreach (var s in spells) { CreateSpell(race, s); n++; }
            foreach (var h in heroes) { CreateHero(race, h); n++; }
            return n;
        }

        // ------------------------------------------------------------- helpers
        private static void EnsureFolder(Race race)
        {
            if (!AssetDatabase.IsValidFolder(Root))
            {
                Directory.CreateDirectory(Root);
                AssetDatabase.Refresh();
            }
            string sub = $"{Root}/{race}";
            if (!AssetDatabase.IsValidFolder(sub))
                AssetDatabase.CreateFolder(Root, race.ToString());
        }

        private static string PathFor(Race race, string cardName, string prefix)
        {
            EnsureFolder(race);
            string safe = cardName.Replace(",", "").Replace(" ", "_").Replace("'", "");
            return $"{Root}/{race}/{prefix}_{safe}.asset";
        }

        private static void CreateUnit(Race race, U u)
        {
            var d = ScriptableObject.CreateInstance<UnitCardData>();
            d.cardName = u.name; d.race = race; d.cost = u.cost;
            d.attack = u.atk; d.health = u.hp; d.unitType = u.type;
            d.ability = u.ab; d.abilityValue = u.abv;
            d.maxPowerCost = u.mpc; // drain karte: dodatna cijena u max poweru
            // description drzi samo flavour/lore, mehanika se crta zasebno
            // iz ability/type polja, pa bi je ovdje samo dvaput pisali
            d.description = Lore(u.name);
            WriteAsset(d, PathFor(race, u.name, "Unit"));
        }

        private static void CreateSpell(Race race, S s)
        {
            var d = ScriptableObject.CreateInstance<SpellCardData>();
            d.cardName = s.name; d.race = race; d.cost = s.cost;
            d.spellType = s.type; d.power = s.power; d.canBePlayedHidden = s.hidden;
            d.ability = s.ab; d.abilityValue = s.abv;
            d.description = Lore(s.name);
            WriteAsset(d, PathFor(race, s.name, "Spell"));
        }

        private static void CreateHero(Race race, H h)
        {
            var d = ScriptableObject.CreateInstance<HeroCardData>();
            d.cardName = h.name; d.race = race; d.cost = h.cost;
            d.attack = h.atk; d.health = h.hp;
            d.ability = h.ab; d.abilityValue = h.abv;
            d.abilityMode = h.mode;
            d.returnToDeckOnDeath = true;
            // abilityDescription = obicno napisano pravilo (koristi ga tooltip);
            // description = flavour/lore koji ide kao kurzivna linija ispod pravila
            d.abilityDescription = h.desc;
            d.description = Lore(h.name);
            WriteAsset(d, PathFor(race, h.name, "Hero"));
        }

        private static void WriteAsset(Object asset, string path)
        {
            var existing = AssetDatabase.LoadMainAssetAtPath(path);
            if (existing != null) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(asset, path);
        }

        // Flavour tekst na karti. Za sada PLACEHOLDER lore, zamijeni ih pravim
        // tekstom kasnije. Ako karta nema unos, vraca genericku liniju.
        private static string Lore(string cardName)
        {
            if (LoreTable.TryGetValue(cardName, out var line)) return line;
            return "[lore placeholder]";
        }

        private static readonly System.Collections.Generic.Dictionary<string, string> LoreTable = new()
        {
            // Orcs
            { "Goblin Cutter", "Small, and much too close to your ankles." },
            { "Wolf Rider", "They hit the line before you hear the howling." },
            { "Blood Stalker", "It picks the throat it wants and takes it." },
            { "Orc Berserker", "Pain only speeds him up." },
            { "Goblin Shieldbearer", "Holds the wall so the bigger ones can feast." },
            { "Warband Scout", "Knows every trap before the trap knows itself." },
            { "Warlord", "Where he plants his banner, the horde follows." },
            { "Bloodrage Totem", "Its drumbeat is the sound of an army waking." },
            { "Raider", "Takes what it wants and burns the rest." },
            { "Ogre Bruiser", "Two heads, one very simple plan." },
            { "Goblin Sapper", "Loves fuses a little more than is healthy." },
            { "War Drummer", "Sets the pace of the slaughter." },
            { "Hurled Axe", "Thrown with a grunt and unerring aim." },
            { "War Cry", "A roar that turns cowards into killers." },
            { "Pillage", "Everything of value, nothing left behind." },
            { "Reckless Charge", "Strategy is for those who plan to survive." },
            { "Kragmaw, Warchief", "His axe does not wait for permission." },
            { "Dro'gan the Relentless", "The horde goes where he goes." },
            // Elves
            { "Elven Sentry", "Patient as roots, sharp as first frost." },
            { "Moonwell Keeper", "Tends the waters that feed the old magic." },
            { "Wisp Scout", "A flicker of light, and your secrets are gone." },
            { "Ancient Protector", "It is older than the kingdoms it guards." },
            { "Starcaller", "Reads tomorrow in tonight's sky." },
            { "Sylvan Archer", "One arrow, one breath, one certainty." },
            { "Worldtree Colossus", "A forest that learned to walk and to war." },
            { "Dryad", "Where she steps, green things remember to grow." },
            { "Treant Guardian", "Bark as hard as iron, and just as patient." },
            { "Moon Priestess", "Her prayers are answered in silver light." },
            { "Grove Warden", "The grove's wrath given root and limb." },
            { "Branch of Yggdrasil", "A cutting from the tree that holds the world." },
            { "Thornweave Ward", "Reach for the elf, lose the hand." },
            { "Nature's Bounty", "The forest always pays back the faithful." },
            { "Entangling Roots", "The ground itself decides you've gone far enough." },
            { "Moonfire", "A whisper of starlight with a killing edge." },
            { "Faelan, Archdruid", "He sleeps for centuries and wakes in a storm." },
            { "Sylvara, Moon Priestess", "The moon listens to her." },
            // Humans
            { "Footman", "The first rank, and proud of it." },
            { "Knight", "Oathbound, armoured, unafraid." },
            { "Shield Guard", "The line holds because he does." },
            { "Rogue Blade", "Gone before the alarm is even raised." },
            { "Ranger", "Sees the ambush two ridges away." },
            { "Cavalry Captain", "His charge decides which way the field breaks." },
            { "Cleric", "Mends the wounded, steadies the frightened." },
            { "Catapult", "Rewrites the walls of any siege." },
            { "Royal Guard", "The last thing between the throne and the dark." },
            { "Squire", "Green, and quietly brave." },
            { "Crossbowman", "Punches through plate at forty paces." },
            { "Paladin", "Faith and steel, about equally." },
            { "Field Medic", "Drags the fallen back into the fight." },
            { "Fireball", "A sphere of ruin, thrown with a word." },
            { "Holy Light", "Warmth that closes wounds and doubts alike." },
            { "Rally", "One voice, and the whole line straightens." },
            { "Smite", "A verdict from above, with no argument." },
            { "King Cedric", "He carries the crown and the bill that comes with it." },
            { "Baelric the Lightbringer", "He gets up with the dawn, hammer in hand." },
            // Demons
            { "Imp", "Small trouble that laughs while it burns you." },
            { "Hellhound", "It smells fear and finds it delicious." },
            { "Soul Harvester", "Collects what the dying no longer need." },
            { "Void Scout", "Sees through shadow, and is one." },
            { "Fel Guard", "Chained to duty, hungry for release." },
            { "Dreadlord", "Feeds on the terror it so easily inspires." },
            { "Infernal", "Falls from the sky as a burning verdict." },
            { "Succubus", "A sweet whisper with a fatal ending." },
            { "Pit Lord", "The pit sent up its very worst." },
            { "Fel Imp", "Twice the spite in half the size." },
            { "Void Ritualist", "Trades pieces of itself for darker power." },
            { "Shadow Bolt", "A dart of pure malice." },
            { "Chaos Nova", "The moment everything nearby stops existing." },
            { "Demonic Pact", "Sign here. The cost comes later." },
            { "Sacrifice", "Something must be given for something gained." },
            { "Immolate", "Slow fire, certain end." },
            { "Vor'gathul", "Whole worlds have stopped at the wave of his hand." },
            { "Zar'thul the Destroyer", "His blood has doomed peoples on its own." },
            // reward val 2
            { "Bone Crusher", "Where it swings, formations stop existing." },
            { "Horde Vanguard", "First through the breach, last to fall." },
            { "Bear Trap", "The steppe keeps its own teeth hidden." },
            { "Tripline", "One length of cord, set low, where nobody looks." },
            { "Spiked Pit", "They dug it last night and covered it before dawn." },
            { "Root Snare", "The forest floor closes around the ankle." },
            { "Caltrops", "Cheap and quiet, and the horses find them first." },
            { "Sanctified Ground", "Blessed soil that punishes whoever crosses it." },
            { "Shadow Pit", "A hole that was not there a moment ago." },
            { "Soul Trap", "It takes something from whoever springs it." },
            { "Earthshaker", "The ground remembers the horde's anger." },
            { "Faerie Dragon", "A shimmer of wings, and secrets take flight." },
            { "Ancient of War", "It was old when the wars were young." },
            { "Moonlit Snare", "Step into the silver light. See what happens." },
            { "Rejuvenation", "The forest mends what battle breaks." },
            { "Templar", "His faith is armour no blade can find." },
            { "Siege Commander", "Every wall falls on his schedule." },
            { "Holy Ward", "Strike the faithful, answer to the Light." },
            { "Divine Storm", "A verdict for everyone at once." },
            { "Nightmare Steed", "It gallops through dreams and out of them." },
            { "Void Terror", "Two hungers in one body." },
            { "Fel Snare", "The pit rewards curiosity with chains." },
            { "Chaos Bolt", "Raw entropy, aimed with spite." },
            // reward val 3
            { "Ashmere Outrider", "He crosses the ford before the water settles." },
            { "Silverwood Ranger", "You are already in her sights. You have been for a while." },
            { "Highmarch Halberdier", "Paid on time, drilled to death, and he holds the line." },
            { "Cinder Fiend", "Born where the ash is still warm, and hungry for more." },
        };
    }
}
#endif
