using UnityEngine;

namespace LordOfTheRealms
{
    // Keyword sposobnosti karata; logika ih tumaci pri igranju / napadu / triggeru.
    // On-enter: PowerRamp, Draw, Heal, BuffAttack, Aoe, ChargeFace
    // Borbeni keywordi: Lifesteal (steta lijeci tvog playera),
    //                   DivineShield (prvi hit ne prolazi)
    // Hero passives: PassiveBuffAllies, PassiveRampEachTurn,
    //                PassiveHealEachTurn, PassiveTauntShield
    public enum CardAbility
    {
        None,
        PowerRamp,
        Draw,
        Heal,
        BuffAttack,
        Aoe,
        ChargeFace,

        // hero passives
        PassiveBuffAllies,
        PassiveRampEachTurn,
        PassiveHealEachTurn,
        PassiveTauntShield,

        // borbeni keywordi
        Lifesteal,
        DivineShield,

        // unique spell mehanike (novi val karata)
        DamageEqualsMaxPower,  // steta = tvoj max POWER (ramp payoff)
        KillDraw,              // ako meta umre od ovog spella, draw 1
        RefreshAttacks,        // sve tvoje jedinice mogu ponovno napasti
        HealAllUnits,          // izlijeci sve tvoje jedinice do punog
        ExecuteStrongest,      // unisti neprijateljsku jedinicu s najvise attacka
        SacrificeDraw,         // unisti svoju najslabiju jedinicu, draw N
        AoeAllDraw,            // N stete SVIM jedinicama (i tvojima), draw 2
        SummonTokens           // prizovi N tokena 1/1
    }
}
