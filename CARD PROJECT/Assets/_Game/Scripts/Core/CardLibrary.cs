using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LordOfTheRealms
{
    // Ucitava sve generirane CardData assete iz Resources/CardData.
    // Generator ih pise u Assets/_Game/CardData, a "Sync Cards To Resources" ih
    // kopira ovamo da ih runtime moze naci. Prazno = zaboravljen sync (warning).
    public static class CardLibrary
    {
        private static List<CardData> _cache;

        public static IReadOnlyList<CardData> All
        {
            get
            {
                if (_cache != null) return _cache;
                _cache = Resources.LoadAll<CardData>("CardData").ToList();
                if (_cache.Count == 0)
                    Debug.LogWarning("CardLibrary: no CardData found in a Resources/CardData folder. " +
                                     "Run 'Lord of the Realms > Generate Card Assets' then " +
                                     "'Lord of the Realms > Sync Cards To Resources'.");
                return _cache;
            }
        }

        public static void Invalidate() => _cache = null;
    }

    // Nosi izabrane rase i tezinu bota sa setup ekrana u scenu meca. Za story
    // bitke nosi i id nodea te fiksni protivnicki deck (zadani deck te rase s
    // herojem nodea), da bot slucajno ne uzme igracev prilagodeni deck.
    public static class MatchConfig
    {
        public static Race PlayerRace = Race.Humans;
        public static Race OpponentRace = Race.Orcs;
        public static BotDifficulty Difficulty = BotDifficulty.Medium;
        public static int Seed = 0; // 0 = random

        // dodaci za story bitku (null ili prazno kad je obican mec)
        public static bool IsStory = false;
        public static string StoryNodeId = null;
        public static SavedDeck OpponentDeckOverride = null;

        // ranked 1v1 (vs bot dok nema pravog matchmakinga)
        public static bool IsRanked = false;
        public static int RankedOpponentRating = 0;
        public static SavedDeck PlayerDeckOverride = null; // MP deck (odvojen slot)

        // infinite gauntlet run
        public static bool IsGauntlet = false;

        // skriptirani tutorial mec (fiksne karte, protivnik bez AI-ja)
        public static bool IsTutorial = false;

        // online mec protiv pravog igraca; poteze razmjenjuje NetMatch
        public static bool IsOnline = false;
        // u online mecu host igra prvi, gost drugi
        public static bool LocalGoesFirst = true;

        // prijateljski mec preko koda sobe: igra se normalno, ali ne dira ELO
        public static bool IsFriendly = false;

        public static void ClearStory()
        {
            IsStory = false;
            StoryNodeId = null;
            OpponentDeckOverride = null;
            IsRanked = false;
            RankedOpponentRating = 0;
            PlayerDeckOverride = null;
            IsGauntlet = false;
            IsTutorial = false;
            IsOnline = false;
            LocalGoesFirst = true;
            IsFriendly = false;
        }
    }
}
