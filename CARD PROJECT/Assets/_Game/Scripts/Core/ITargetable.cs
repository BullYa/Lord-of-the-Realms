namespace LordOfTheRealms
{
    // Sve sto moze biti meta napada ili spella: jedinica na plodi ili igrac.
    // Zato TargetingRules moze jednako tretirati i jedno i drugo.
    public interface ITargetable
    {
        Owner Owner { get; }
        bool IsAlive { get; }
        void ReceiveDamage(int amount);

        // ime za log i UI
        string TargetName { get; }
    }

    // Adapter helperi: CardInstance se preda targeting sustavu kao ITargetable, a
    // da sam CardInstance ne mora znati nista o targetingu.
    public static class TargetableExtensions
    {
        public static ITargetable AsTargetable(this CardInstance card)
            => new CardTargetable(card);
    }

    // Omotac oko CardInstance da zadovolji ITargetable.
    public sealed class CardTargetable : ITargetable
    {
        public CardInstance Card { get; }

        public CardTargetable(CardInstance card) => Card = card;

        public Owner Owner => Card.Owner;
        public bool IsAlive => Card.IsAlive;
        public void ReceiveDamage(int amount) => Card.TakeDamage(amount);
        public string TargetName => Card.Data != null ? Card.Data.cardName : "Unknown";

        // Value equality: ista karta je jednaka kroz razlicite omotace, na sto se
        // oslanja List.Contains u TargetingRules.
        public override bool Equals(object obj)
            => obj is CardTargetable other && ReferenceEquals(other.Card, Card);

        public override int GetHashCode() => Card?.GetHashCode() ?? 0;
    }
}
