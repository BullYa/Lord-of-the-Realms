using UnityEngine;

namespace LordOfTheRealms
{
    // Definicija jedinice: stvorenje koje se stavlja na plocu.
    // Sve jedinice imaju summoning sickness (ne napadaju potez kad su odigrane).
    [CreateAssetMenu(
        fileName = "Unit_",
        menuName = "Lord of the Realms/Unit Card",
        order = 0)]
    public class UnitCardData : CardData
    {
        [Header("Unit Stats")]
        [Min(0)]
        public int attack = 1;

        [Min(1)]
        public int health = 1;

        [Header("Unit Behaviour")]
        [Tooltip("Basic / Taunt / Assassin / Scout. Determines special rules.")]
        public UnitType unitType = UnitType.Basic;

        public override CardCategory Category => CardCategory.Unit;

        // Assassini napadaju 'Precise' (ignoriraju Taunt vs jedinice), ostali 'Normal'
        public TargetingMode AttackTargeting =>
            unitType == UnitType.Assassin ? TargetingMode.Precise : TargetingMode.Normal;

        // Taunt = neprijatelji ga moraju prvo napasti
        public bool IsTaunt => unitType == UnitType.Taunt;

        // Scout otkriva skrivene spellove
        public bool RevealsHiddenSpells => unitType == UnitType.Scout;
    }
}
