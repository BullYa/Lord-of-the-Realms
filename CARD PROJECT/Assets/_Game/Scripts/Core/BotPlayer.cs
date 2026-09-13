using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LordOfTheRealms
{
    // 6 stupnjeva tezine za PVE bota. Vise = bolje igra i ima male prednosti.
    public enum BotDifficulty
    {
        VeryEasy = 1,
        Easy = 2,
        Medium = 3,
        Hard = 4,
        VeryHard = 5,
        Nightmare = 6
    }

    // Protivnik. Nije "treniran", samo slijedi pravila, a tezina odreduje
    // koliko su ta pravila pametna. Na niskim razinama igra nasumicno i
    // namjerno grijesi; na visokima dobro trguje i trazi lethal.
    //
    // TakeTurn() je coroutine da ekran meca moze razmaknuti poteze i da se vidi
    // sto bot radi. Omotan je u try/catch da bug nikad ne zamrzne igru usred
    // poteza; u najgorem slucaju stane ranije i preda potez.
    public class BotPlayer
    {
        private readonly GameMatch _match;
        private readonly int _side;
        private readonly BotDifficulty _difficulty;
        private readonly System.Random _rng;

        public BotPlayer(GameMatch match, int side, BotDifficulty difficulty, int seed = 0)
        {
            _match = match;
            _side = side;
            _difficulty = difficulty;
            _rng = seed == 0 ? new System.Random() : new System.Random(seed);
        }

        private int Level => (int)_difficulty;

        public IEnumerator TakeTurn(float actionDelay = 0.9f)
        {
            // kratka pauza prije nego bot krene, da primijetis da mu je turn poceo
            yield return new WaitForSeconds(actionDelay * 0.5f);

            // igraj cards dok moze (ili dok ne odluci stati)
            int safety = 0;
            while (safety++ < 40)
            {
                bool played = SafeStep(TryPlayOneCard);
                if (!played) break;
                if (_match.IsOver) yield break;
                yield return new WaitForSeconds(actionDelay);
            }

            yield return new WaitForSeconds(actionDelay * 0.6f);

            // zatim napadni svime sto moze
            safety = 0;
            while (safety++ < 40)
            {
                bool acted = SafeStep(TryOneAttack);
                if (!acted) break;
                if (_match.IsOver) yield break;
                yield return new WaitForSeconds(actionDelay);
            }

            yield return new WaitForSeconds(actionDelay * 0.5f);
        }

        // odradi jednu odluku; ako baci iznimku, logiraj je i prekini ovu fazu
        // umjesto da se zaglavi
        private bool SafeStep(System.Func<bool> step)
        {
            try { return step(); }
            catch (System.Exception e)
            {
                Debug.LogError($"Bot step error: {e}");
                return false;
            }
        }

        // ---- playing cards ----

        private bool TryPlayOneCard()
        {
            var me = _match.Sides[_side];
            var affordable = me.Hand.Where(c => _match.CanPlay(_side, c)).ToList();
            if (affordable.Count == 0) return false;

            // najlaksi bot ponekad samo baci POWER i preskoci potez
            if (_difficulty == BotDifficulty.VeryEasy && _rng.NextDouble() < 0.35)
                return false;

            // niski leveli biraju nasumicno pa im ScoreCard veto ne pomaze, makni
            // drain karte iz izbora kad bi ih zakopale (isti prag kao u ScoreCard)
            if (Level <= 2)
                affordable = affordable
                    .Where(c => c.maxPowerCost == 0 ||
                                me.Hero.Power.MaxPower - c.maxPowerCost >= 7)
                    .ToList();
            if (affordable.Count == 0) return false;

            CardData choice = ChooseCardToPlay(affordable);
            if (choice == null) return false;

            // offensive spell treba metu
            if (choice is SpellCardData sp && sp.IsTargeted)
            {
                var target = ChooseSpellTarget(sp);
                if (target == null && Level >= 3)
                    return TryPlayNonSpell(affordable); // no good target -> play something else
                return _match.CastSpell(_side, sp, target ?? FallbackSpellTarget());
            }

            // od tezine 2 navise bot postavlja obrambene spellove licem prema dolje
            bool hidden = choice is SpellCardData ds && ds.canBePlayedHidden && Level >= 2
                          && _rng.NextDouble() < 0.6;

            return _match.PlayCard(_side, choice, hidden);
        }

        private bool TryPlayNonSpell(List<CardData> affordable)
        {
            var nonSpell = affordable.Where(c => !(c is SpellCardData s && s.IsTargeted)).ToList();
            if (nonSpell.Count == 0) return false;
            var choice = ChooseCardToPlay(nonSpell);
            return choice != null && _match.PlayCard(_side, choice);
        }

        private CardData ChooseCardToPlay(List<CardData> affordable)
        {
            // niske razine: samo zgrabi nasumicnu card koju si moze priustiti
            if (Level <= 2)
                return affordable[_rng.Next(affordable.Count)];

            // vise razine: ocijeni svaku card i uzmi najbolju (na Medium malo suma)
            CardData best = null;
            float bestScore = float.MinValue;
            foreach (var c in affordable)
            {
                float score = ScoreCard(c);
                if (score == float.MinValue) continue; // veto (npr. preduboki drain)
                if (Level == 3) score += (float)_rng.NextDouble() * 2f;
                if (score > bestScore) { bestScore = score; best = c; }
            }
            return best;
        }

        // gruba ocjena "koliko je ova card dobra bas sad"
        private float ScoreCard(CardData c)
        {
            var me = _match.Sides[_side];
            // ramp se isplati uzeti rano dok jos ima meca u kojem ce se iskoristiti
            if (c.ability == CardAbility.PowerRamp && me.Hero.Power.MaxPower < 8)
                return 100 + c.cost;

            // drain karte (maxPowerCost): tempo za snagu, igraj ih samo sa zdravim
            // maxom; na niskom maxu bi si bot sam iskopao rupu iz koje ne izlazi
            float drainPenalty = 0f;
            if (c.maxPowerCost > 0)
            {
                int maxAfter = me.Hero.Power.MaxPower - c.maxPowerCost;
                if (maxAfter < 7) return float.MinValue;      // preskoci: preduboka rupa
                if (me.Hero.Power.MaxPower >= 10) drainPenalty = -1f;  // pun max: skoro besplatno
                else drainPenalty = -3f * c.maxPowerCost;     // ispod capa: oprez
            }

            switch (c)
            {
                case UnitCardData u:
                    return 10 + u.attack + u.health + AbilityBonus(c) + drainPenalty;
                case HeroCardData h:
                    return 12 + h.attack + h.health + AbilityBonus(c) + drainPenalty;
                case SpellCardData s:
                    if (s.spellType == SpellType.Offensive)
                        return 8 + s.power + AbilityBonus(c) + drainPenalty;
                    if (s.spellType == SpellType.Defensive)
                        return 6 + AbilityBonus(c) + drainPenalty;
                    return 5 + AbilityBonus(c) + drainPenalty;
            }
            return 5;
        }

        // dodatni bodovi za sposobnosti koje su dobre u trenutnom stanju boarda
        private float AbilityBonus(CardData c) => c.ability switch
        {
            CardAbility.Draw => 6,
            CardAbility.Aoe => _match.Sides[1 - _side].Board.Count(u => u.IsAlive) * 3,
            CardAbility.Heal => _match.Sides[_side].Hero.CurrentHealth < 15 ? 8 : 2,
            CardAbility.BuffAttack => _match.Sides[_side].Board.Count(u => u.IsAlive) * 2,
            _ => 0
        };

        private ITargetable ChooseSpellTarget(SpellCardData spell)
        {
            var enemy = _match.Sides[1 - _side];
            var units = enemy.Board.Where(u => u.IsAlive && !u.IsHidden).ToList();

            // radije ubij najopasnije sto mozes ubiti
            var killable = units.Where(u => u.CurrentHealth <= spell.power)
                                .OrderByDescending(u => u.Attack).ToList();
            if (killable.Count > 0) return killable[0].AsTargetable();

            // inace grickaj heroja ako ga nitko ne cuva
            if (Level >= 3 && !enemy.HasTaunt)
                return enemy.Hero;

            return null;
        }

        private ITargetable FallbackSpellTarget()
        {
            var enemy = _match.Sides[1 - _side];
            var units = enemy.Board.Where(u => u.IsAlive && !u.IsHidden).ToList();
            if (units.Count > 0) return units[0].AsTargetable();
            return enemy.Hero;
        }

        // ---- attacking ----

        // Scout trosi napad da otkrije i makne protivnikovu skrivenu zamku.
        // Vrijedi mu jer razoruzavanje sad vuce kartu.
        private bool TryScoutReveal()
        {
            var me = _match.Sides[_side];
            var enemy = _match.Sides[1 - _side];

            var trap = enemy.Board.FirstOrDefault(c => c.IsAlive && c.IsHiddenSpell);
            if (trap == null) return false;

            var scout = me.Board.FirstOrDefault(u => u.IsAlive && u.IsScout && u.CanAttack);
            if (scout == null) return false;

            return _match.ScoutReveal(_side, scout, trap);
        }

        private bool TryOneAttack()
        {
            // od tezine 2 navise bot prvo razoruza tvoju skrivenu zamku Scoutom
            if (Level >= 2 && TryScoutReveal()) return true;

            var me = _match.Sides[_side];
            var enemy = _match.Sides[1 - _side];

            var attackers = me.Board.Where(u => u.CanAttack && u.IsAlive && u.Attack > 0).ToList();
            if (attackers.Count == 0) return false;

            // Hard i vise traze lethal: ako heroja mozemo ubiti ovaj turn, ubij ga
            if (Level >= 4 && CanReachFace(attackers, enemy))
            {
                int faceDmg = attackers.Where(a => CanHitFace(a, enemy)).Sum(a => a.Attack);
                if (faceDmg >= enemy.Hero.CurrentHealth)
                {
                    var faceAttacker = attackers.First(a => CanHitFace(a, enemy));
                    _match.Attack(_side, faceAttacker, enemy.Hero);
                    return true;
                }
            }

            var attacker = attackers[0];
            var legal = _match.LegalTargets(_side, attacker);
            if (legal.Count == 0) { attacker.CanAttack = false; return true; }

            // niske razine: udri po nasumicnoj legalnoj meti
            if (Level <= 2)
            {
                var pick = legal[_rng.Next(legal.Count)];
                _match.Attack(_side, attacker, pick);
                return true;
            }

            // vise razine: trazi dobar trade, inace idi na heroja
            var unitTargets = legal.OfType<CardTargetable>().ToList();
            CardTargetable bestTrade = null;
            float bestVal = 0;
            foreach (var t in unitTargets)
            {
                var d = t.Card;
                bool weKill = attacker.Attack >= d.CurrentHealth;
                bool weSurvive = d.Attack < attacker.CurrentHealth;
                float val = 0;
                if (weKill) val += d.Attack + d.CurrentHealth;
                if (weKill && weSurvive) val += 3;   // killing it and living is ideal
                if (!weKill) val -= 2;
                if (val > bestVal) { bestVal = val; bestTrade = t; }
            }

            bool faceOpen = legal.Contains(enemy.Hero);
            if (bestTrade != null && (bestVal >= 4 || !faceOpen))
                _match.Attack(_side, attacker, bestTrade);
            else if (faceOpen)
                _match.Attack(_side, attacker, enemy.Hero);
            else if (unitTargets.Count > 0)
                _match.Attack(_side, attacker, unitTargets[0]);
            else
                attacker.CanAttack = false;

            return true;
        }

        private bool CanReachFace(List<CardInstance> attackers, SideState enemy)
            => attackers.Any(a => CanHitFace(a, enemy));

        private bool CanHitFace(CardInstance attacker, SideState enemy)
            => _match.LegalTargets(_side, attacker).Contains(enemy.Hero);
    }
}
