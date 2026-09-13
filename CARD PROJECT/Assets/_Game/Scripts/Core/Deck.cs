using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LordOfTheRealms
{
    // Deck u mecu: stog cardova s kojeg se draw-a (vrh = kraj liste).
    // Prazan deck = fatigue steta (to rjesava GameMatch).
    public class Deck
    {
        private readonly List<CardData> _cards = new();
        public int Count => _cards.Count;
        public bool IsEmpty => _cards.Count == 0;

        public Deck(IEnumerable<CardData> cards)
        {
            _cards.AddRange(cards);
        }

        public void Shuffle(System.Random rng)
        {
            for (int i = _cards.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (_cards[i], _cards[j]) = (_cards[j], _cards[i]);
            }
        }

        // draw carda s vrha (null ako je deck prazan)
        public CardData Draw()
        {
            if (_cards.Count == 0) return null;
            var c = _cards[_cards.Count - 1];
            _cards.RemoveAt(_cards.Count - 1);
            return c;
        }

        // stavi card na vrh (sljedeci draw)
        public void PutOnTop(CardData card)
        {
            if (card != null) _cards.Add(card);
        }

        // ubaci card na nasumicno mjesto medu prvih 'depth' karata od vrha.
        // (depth 1 = uvijek vrh, offset 0 = sljedeci draw). Junak koji pogine
        // ide ovako natrag u spil da se ne izvuce uvijek odmah.
        public void PutNearTop(CardData card, int depth, System.Random rng)
        {
            if (card == null) return;
            int d = Mathf.Clamp(depth, 1, _cards.Count + 1);
            int offset = rng != null ? rng.Next(0, d) : 0;   // 0 = vrh
            _cards.Insert(_cards.Count - offset, card);
        }
    }
}
