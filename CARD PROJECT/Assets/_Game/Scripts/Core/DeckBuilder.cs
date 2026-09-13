using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LordOfTheRealms
{
    // Slaze deck rase iz karata: custom deck igraca ako postoji, inace balansirani
    // default. Dodaje izabranog heroja.
    public static class DeckBuilder
    {
        // Gradi deck jedne rase. Ako je zadan 'overrideDeck' (story bitke), koristi
        // se tocno ta lista; inace se ucita igracev spremljeni deck ili zadani.
        public static Deck Build(Race race, IReadOnlyList<CardData> library, System.Random rng,
            SavedDeck overrideDeck = null)
        {
            var byName = library.Where(c => c != null && c.race == race)
                                 .GroupBy(c => c.cardName)
                                 .ToDictionary(g => g.Key, g => g.First());

            // default grana: deck iz igracevog AKTIVNOG slota te rase (ne fiksni prvi)
            var saved = overrideDeck ?? DeckStorage.LoadActive(race);
            var cards = new List<CardData>();

            // hero
            if (!string.IsNullOrEmpty(saved.heroName) && byName.TryGetValue(saved.heroName, out var hero))
                cards.Add(hero);

            // cards
            foreach (var cc in saved.cards)
            {
                if (byName.TryGetValue(cc.name, out var card))
                    for (int i = 0; i < cc.copies; i++)
                        cards.Add(card);
                else
                    Debug.LogWarning($"DeckBuilder: '{cc.name}' for {race} not in library. Run Generate + Sync.");
            }

            // dopuni do 30 najjeftinijim unitom ako fali
            if (cards.Count < 30 && cards.Count > 0)
            {
                var cheap = cards.OfType<UnitCardData>().OrderBy(c => c.cost).FirstOrDefault();
                while (cards.Count < 30 && cheap != null) cards.Add(cheap);
            }

            var deck = new Deck(cards);
            deck.Shuffle(rng);
            return deck;
        }
    }
}
