using System.Collections.Generic;

namespace LordOfTheRealms
{
    // Tocan sastav default 30-card decka po rasi, uskladen s balans simom.
    // Pravila sastava: max 4 kopije unita, max 2 kopije spella.
    public static class DeckLists
    {
        public const int MaxUnitCopies = 4;
        public const int MaxSpellCopies = 2;

        public static IEnumerable<(string name, int copies)> For(Race race) => race switch
        {
            Race.Orcs => Orcs,
            Race.Elves => Elves,
            Race.Humans => Humans,
            Race.Demons => Demons,
            _ => Humans
        };

        private static readonly (string, int)[] Orcs =
        {
            ("Goblin Cutter", 4), ("Wolf Rider", 4), ("Blood Stalker", 3),
            ("Orc Berserker", 3), ("Goblin Shieldbearer", 3), ("Warband Scout", 2),
            ("Warlord", 2), ("Bloodrage Totem", 2),
            ("Hurled Axe", 2), ("War Cry", 2), ("Pillage", 2),
            ("Kragmaw, Warchief", 1),
        };

        private static readonly (string, int)[] Elves =
        {
            ("Elven Sentry", 4), ("Moonwell Keeper", 3), ("Wisp Scout", 3),
            ("Ancient Protector", 3), ("Starcaller", 3), ("Sylvan Archer", 3),
            ("Worldtree Colossus", 2),
            ("Branch of Yggdrasil", 2), ("Thornweave Ward", 2),
            ("Nature's Bounty", 2), ("Entangling Roots", 2),
            ("Faelan, Archdruid", 1),
        };

        private static readonly (string, int)[] Humans =
        {
            ("Footman", 4), ("Knight", 4), ("Shield Guard", 3), ("Rogue Blade", 3),
            ("Ranger", 2), ("Cavalry Captain", 2), ("Cleric", 2), ("Catapult", 2),
            ("Royal Guard", 1),
            ("Fireball", 2), ("Holy Light", 2), ("Rally", 2),
            ("King Cedric", 1),
        };

        private static readonly (string, int)[] Demons =
        {
            ("Imp", 4), ("Hellhound", 4), ("Soul Harvester", 3), ("Void Scout", 3),
            ("Fel Guard", 3), ("Dreadlord", 3), ("Infernal", 1),
            ("Shadow Bolt", 2), ("Chaos Nova", 2), ("Demonic Pact", 2),
            ("Sacrifice", 2),
            ("Vor'gathul", 1),
        };
    }
}
