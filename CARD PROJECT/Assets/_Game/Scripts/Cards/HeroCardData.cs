using UnityEngine;

namespace LordOfTheRealms
{
    // OnEnter = efekt se okine jednom pri igranju; Passive = traje dok heroj zivi
    public enum HeroAbilityMode
    {
        OnEnter,
        Passive
    }

    // Definicija heroja: 1 po decku, statovi kao jedinica + ability (on-enter ili
    // passive). Kad umre vraca se u deck, blizu vrha (returnToDeckOnDeath).
    [CreateAssetMenu(
        fileName = "Hero_",
        menuName = "Lord of the Realms/Hero Card",
        order = 2)]
    public class HeroCardData : CardData
    {
        [Header("Hero Stats")]
        [Min(0)]
        public int attack = 2;

        [Min(1)]
        public int health = 5;

        [Header("Hero Ability")]
        [Tooltip("OnEnter fires once when played; Passive lasts while the hero is alive.")]
        public HeroAbilityMode abilityMode = HeroAbilityMode.OnEnter;

        [TextArea(2, 4)]
        [Tooltip("Human-readable description of the hero's ability.")]
        public string abilityDescription = "";

        [Header("Death")]
        [Tooltip("If true, when this hero dies it returns to the top of the owner's deck.")]
        public bool returnToDeckOnDeath = true;

        public override CardCategory Category => CardCategory.Hero;

        public TargetingMode AttackTargeting => TargetingMode.Normal;
    }
}
