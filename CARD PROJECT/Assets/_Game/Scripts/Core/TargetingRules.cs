using System.Collections.Generic;
using System.Linq;

namespace LordOfTheRealms
{
    // Cista pravila "tko koga smije napasti/gadati". Bez Unity tipova i vizuala,
    // samo dogovorena targeting tablica, pa se moze testirati zasebno i dijeliti
    // izmedu lokalne i (kasnije) mrezne igre.
    //
    //  Napadac                      | Nema taunta        | Ima taunt na plodi
    //  -----------------------------|--------------------|--------------------
    //  Basic/Scout/Taunt/Hero       | bilo koja + igrac  | samo taunt jedinice
    //  Assassin / offensive spell   | bilo koja + igrac  | bilo koja, igrac NE
    //
    // "Precise" napadaci (Assassin, offensive spell) ignoriraju taunt na jedinicama,
    // ali taunt i dalje stiti IGRACA od njih.
    public static class TargetingRules
    {
        // svi legalni mete za napadaca, s obzirom na plocu obrambene strane
        public static List<ITargetable> GetLegalTargets(
            TargetingMode attackerMode,
            IReadOnlyList<CardInstance> enemyUnits,
            ITargetable enemyPlayer)
        {
            var results = new List<ITargetable>();
            bool tauntPresent = enemyUnits.Any(u => u != null && u.IsAlive && u.IsTaunt);

            if (attackerMode == TargetingMode.Normal)
            {
                if (tauntPresent)
                {
                    // samo taunt jedinice su legalne
                    foreach (var u in enemyUnits)
                        if (u != null && u.IsAlive && u.IsTaunt)
                            results.Add(u.AsTargetable());
                }
                else
                {
                    // bilo koja jedinica + igrac
                    foreach (var u in enemyUnits)
                        if (u != null && u.IsAlive)
                            results.Add(u.AsTargetable());
                    if (enemyPlayer != null)
                        results.Add(enemyPlayer);
                }
            }
            else // Precise
            {
                // sve jedinice su uvijek legalne, bez obzira na taunt
                foreach (var u in enemyUnits)
                    if (u != null && u.IsAlive)
                        results.Add(u.AsTargetable());

                // igrac je legalan SAMO ako nema taunta
                if (!tauntPresent && enemyPlayer != null)
                    results.Add(enemyPlayer);
            }

            return results;
        }

        // je li konkretna meta legalna za ovog napadaca
        public static bool IsLegalTarget(
            TargetingMode attackerMode,
            ITargetable target,
            IReadOnlyList<CardInstance> enemyUnits,
            ITargetable enemyPlayer)
        {
            return GetLegalTargets(attackerMode, enemyUnits, enemyPlayer).Contains(target);
        }
    }
}
