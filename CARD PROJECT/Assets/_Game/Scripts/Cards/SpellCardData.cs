using UnityEngine;

namespace LordOfTheRealms
{
    // Definicija spella.
    // Offensive = ciljana steta ('Precise' kao Assassin, igraca stiti Taunt).
    // Defensive = heal/draw; moze se postaviti skriveno kao ZAMKA (trigger na napad).
    // Field = efekt na cijelu plocu (buff, ramp).
    [CreateAssetMenu(
        fileName = "Spell_",
        menuName = "Lord of the Realms/Spell Card",
        order = 1)]
    public class SpellCardData : CardData
    {
        [Header("Spell Behaviour")]
        [Tooltip("Offensive / Defensive / Field.")]
        public SpellType spellType = SpellType.Offensive;

        [Tooltip("Magnitude of the effect (damage dealt, health granted, etc.). Meaning depends on spellType.")]
        public int power = 1;

        [Header("Hidden Play")]
        [Tooltip("If true, this spell may be played face-down and auto-triggers later. Typically Defensive spells.")]
        public bool canBePlayedHidden = false;

        public override CardCategory Category => CardCategory.Spell;

        // offensive spellovi ciljaju kao Assassin (Precise)
        public TargetingMode CastTargeting => TargetingMode.Precise;

        // treba li igrac odabrati metu
        public bool IsTargeted => spellType == SpellType.Offensive;
    }
}
