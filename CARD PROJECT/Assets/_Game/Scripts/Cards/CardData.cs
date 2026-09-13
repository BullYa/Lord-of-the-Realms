using UnityEngine;

namespace LordOfTheRealms
{
    // Bazna klasa svih definicija karata (ScriptableObject asset, cisti podaci).
    // Runtime stanje (HP, je li na plodi, skrivena...) zivi u CardInstance, pa isti
    // asset moze koristiti vise karata bez dijeljenja promjenjivog stanja.
    // Assete radi generator: menu "Lord of the Realms/Generate Card Assets".
    public abstract class CardData : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Display name shown on the card.")]
        public string cardName = "New Card";

        [Tooltip("Which faction this card belongs to.")]
        public Race race = Race.Humans;

        [Tooltip("POWER cost to play this card from hand.")]
        [Min(0)]
        public int cost = 1;

        [Tooltip("Dodatna cijena: trajno spusti tvoj MAX power za ovoliko (oporavlja se +1/turn do capa). 0 = nema draina.")]
        [Min(0)]
        public int maxPowerCost = 0;

        [Header("Presentation")]
        [Tooltip("Card artwork. Optional while prototyping, placeholders are fine.")]
        public Sprite artwork;

        [TextArea(2, 5)]
        [Tooltip("Rules text / flavour shown to the player.")]
        public string description = "";

        [Header("Ability (tuned by balance sim)")]
        [Tooltip("Special keyword effect this card carries, if any.")]
        public CardAbility ability = CardAbility.None;

        [Tooltip("Magnitude of the ability (cards drawn, health healed, ramp amount, etc.).")]
        public int abilityValue = 0;

        // Gruba kategorija karte. Svaka podklasa vraca svoju; koristi UI, filtriranje
        // i koji slot na plodi karta smije zauzeti.
        public abstract CardCategory Category { get; }
    }
}
