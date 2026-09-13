using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LordOfTheRealms
{
    // sve sto jedan igrac ima tijekom meca: heroja, deck, hand i board
    public class SideState
    {
        public PlayerEntity Hero;
        public Deck Deck;
        public readonly List<CardData> Hand = new();
        public readonly List<CardInstance> Board = new();
        // odigrane carolije, poginule jedinice i spaljene karte; samo za prikaz,
        // pravila ih ne koriste. Junak koji se vraca u deck NE ide ovdje.
        public readonly List<CardData> Discard = new();
        public int Fatigue;
        // vlastiti generator za mijesanje ovog decka; dijeljeni bi ovisio o tome
        // koja se strana obraduje prva, a to je obrnuto kod hosta i kod gosta
        public System.Random Rng;

        public bool HasTaunt => Board.Any(u => u.IsAlive && u.IsTaunt);

        // heroji koji su jos zivi na mom boardu (za passive efekte)
        public IEnumerable<CardInstance> LivingHeroes =>
            Board.Where(c => c.IsAlive && c.Data is HeroCardData);
    }

    // Sama igra. Namjerno bez Unityja i UI-ja da je mogu voziti i bot i battle
    // screen, i da se pravila mogu testirati odvojeno.
    //
    // Kako ide turn: BeginTurn() sprema trenutnu stranu (ramp POWER, draw, budi
    // unite), zatim igrac ili bot igra cards i napada, pa PassTurn() preda
    // drugoj strani i pokrene njezin turn.
    //
    // Heroji su isto ovdje: passive efekti tikaju dok je heroj ziv, a kad heroj
    // pogine vraca se u deck umjesto da bude izgubljen.
    public class GameMatch
    {
        public const int MaxHandSize = 10;

        public SideState[] Sides = new SideState[2];
        public int Current { get; private set; }
        public int TurnNumber { get; private set; }
        public bool IsOver { get; private set; }
        public Owner? Winner { get; private set; }

        private readonly System.Random _rng;

        public event Action<int> OnTurnStarted;
        public event Action OnStateChanged;
        public event Action<string> OnLog;
        public event Action<Owner> OnGameOver;
        // javi se tik prije nego steta padne da je UI moze animirati: napadacki unit
        // (null za spellove), meta i koliko. Zato battle screen moze pokazati lunge i
        // damage brojke i za BOTOVE poteze, ne samo za igraceve.
        public event Action<CardInstance, ITargetable, int> OnAttackResolved;
        public event Action<ITargetable, int> OnDamageDealt;
        // javi se kad bilo tko baci spell, da ga UI moze pokazati veliko u sredini
        // prije nego efekt padne
        public event Action<SpellCardData, int> OnSpellCast;
        // javi se na pocetku turna s brojem izvucenih cards, da UI moze poslati
        // toliko card backova s deck pilea
        public event Action<int, int> OnCardsDrawn;
        // koja je karta odigrana (za "last played" povijest u UI-ju);
        // skriveni spellovi se NE javljaju da se ne otkriju
        public event Action<int, CardData> OnCardPlayed;

        // mulligan: vrati odabrane cards u deck, shufflaj, drawaj isti broj.
        // Dozvoljen samo prije prvog poteza.
        public void Mulligan(int side, List<CardData> toReplace)
        {
            if (TurnNumber > 0 || toReplace == null || toReplace.Count == 0) return;
            var s = Sides[side];
            int n = 0;
            foreach (var c in toReplace)
                if (s.Hand.Remove(c)) { s.Deck.PutOnTop(c); n++; }
            if (n == 0) return;
            // mijesanje ide generatorom TE strane, da online obje masine dobiju
            // isti redoslijed bez obzira tko je prvi mulliganirao
            s.Deck.Shuffle(s.Rng ?? _rng);
            for (int i = 0; i < n; i++)
            {
                var c = s.Deck.Draw();
                if (c != null) s.Hand.Add(c);
            }
            Log($"{s.Hero.Race} mulligans {n} card(s).");
            OnStateChanged?.Invoke();
        }

        public GameMatch(Race playerRace, Race opponentRace, IReadOnlyList<CardData> library,
                         int seed = 0, int playerHealth = 40, int powerCap = 10,
                         SavedDeck opponentDeckOverride = null, SavedDeck playerDeckOverride = null,
                         int opponentHealth = -1, bool localGoesFirst = true)
        {
            _rng = seed == 0 ? new System.Random() : new System.Random(seed);

            // Sjeme se vezuje uz ULOGU (tko igra prvi), ne uz stranu 0/1, jer je
            // strana 0 kod svakog igraca on sam. Tako obje masine promijesaju isti
            // deck na isti nacin.
            int baseSeed = seed == 0 ? Environment.TickCount : seed;
            int firstSeed = baseSeed;
            int secondSeed = unchecked(baseSeed + 7919);
            int mySeed = localGoesFirst ? firstSeed : secondSeed;
            int theirSeed = localGoesFirst ? secondSeed : firstSeed;

            Sides[0] = BuildSide(Owner.Player, playerRace, library, playerHealth, powerCap, playerDeckOverride, mySeed);
            Sides[1] = BuildSide(Owner.Opponent, opponentRace, library,
                opponentHealth > 0 ? opponentHealth : playerHealth, powerCap, opponentDeckOverride, theirSeed);

            // pocetne ruke: onaj tko igra prvi dobiva 3, drugi 4 kao naknadu.
            // U online mecu gost je taj koji igra drugi, pa se strane obrnu.
            DrawCards(0, localGoesFirst ? 3 : 4);
            DrawCards(1, localGoesFirst ? 4 : 3);

            Current = localGoesFirst ? 0 : 1;
            TurnNumber = 0;
        }

        private SideState BuildSide(Owner owner, Race race, IReadOnlyList<CardData> library,
                                    int hp, int cap, SavedDeck deckOverride, int deckSeed)
        {
            var s = new SideState
            {
                Hero = new PlayerEntity(owner, race, hp, cap),
                Rng = new System.Random(deckSeed),
                // VAZNO za online: svaka strana ima SVOJ generator za mijesanje.
                // Sa zajednickim _rng redoslijed ovisi o tome koja se strana gradi
                // prva, a to je obrnuto kod hosta i kod gosta, pa bi isti deck dobio
                // dva razlicita redoslijeda i mecevi bi se razisli.
                Deck = DeckBuilder.Build(race, library, new System.Random(deckSeed), deckOverride)
            };
            s.Hero.OnDefeated += _ => HandleDefeat(owner);
            return s;
        }

        // ---- turn flow ----

        // finalni boss: Vor'gathul ima vise HP-a i baca AOE svakih par poteza
        public bool FinalBossMode;
        private int _bossTurns;

        // Zastita od dvostrukog pocetka poteza. BeginTurn vuce kartu i daje POWER,
        // pa bi drugi poziv za istu stranu dao kartu i POWER viska. Online se to
        // lako dogodi jer potez pokrecu i mulligan handshake i mrezni tok.
        private bool _turnActive;

        public void BeginTurn()
        {
            if (IsOver) return;
            if (_turnActive) return;
            _turnActive = true;
            var s = Sides[Current];
            TurnNumber++;

            s.Hero.Power.StartTurn();

            // ako bi turn poceo s praznim handom, vuci 2 umjesto 1 da kasno u igri
            // nikad ne ostanes bez icega. Provjera ide PRIJE vucenja, jer nakon
            // njega uvijek vidi svjeze izvucenu card.
            int toDraw = s.Hand.Count == 0 ? 2 : 1;
            int handBefore = s.Hand.Count;
            DrawCards(Current, toDraw);
            int actuallyDrawn = s.Hand.Count - handBefore; // fatigue i burn ne vuku nista
            if (actuallyDrawn > 0) OnCardsDrawn?.Invoke(Current, actuallyDrawn);

            // uniti opet mogu napasti ovaj turn
            foreach (var u in s.Board)
                u.CanAttack = true;

            ApplyStartOfTurnPassives(Current);

            // boss mehanika: svaki 3. potez finalnog bossa pada Rain of Chaos
            if (FinalBossMode && Current == 1)
            {
                _bossTurns++;
                if (_bossTurns % 3 == 0)
                {
                    Log("Vor'gathul unleashes RAIN OF CHAOS! 2 damage to all your units!");
                    foreach (var u in Sides[0].Board.Where(c => c.IsAlive && !c.IsHidden).ToList())
                    {
                        u.TakeDamage(2);
                        OnDamageDealt?.Invoke(u.AsTargetable(), 2);
                    }
                    CheckDeaths();
                }
            }

            Log($"-- {s.Hero.Race} turn {(TurnNumber + 1) / 2} " +
                $"(POWER {s.Hero.Power.CurrentPower}/{s.Hero.Power.MaxPower}) --");

            OnTurnStarted?.Invoke(Current);
            OnStateChanged?.Invoke();
        }

        public void PassTurn()
        {
            if (IsOver) return;
            _turnActive = false;
            Current = 1 - Current;
            BeginTurn();
        }

        public bool IsPlayersTurn => Current == 0;

        // ---- vucenje (prazan deck = fatigue steta da mec ne traje vjecno) ----

        private void DrawCards(int side, int n)
        {
            var s = Sides[side];
            for (int i = 0; i < n; i++)
            {
                var card = s.Deck.Draw();
                if (card == null)
                {
                    s.Fatigue++;
                    s.Hero.ReceiveDamage(s.Fatigue);
                    OnDamageDealt?.Invoke(s.Hero, s.Fatigue);
                    Log($"{s.Hero.Race} is out of cards! Fatigue {s.Fatigue} damage.");
                }
                else if (s.Hand.Count < MaxHandSize)
                {
                    s.Hand.Add(card);
                }
                else
                {
                    // pun hand -> card se spali i zavrsi na odbacenima
                    s.Discard.Add(card);
                }
            }
        }

        // ---- playing cards ----

        public bool CanPlay(int side, CardData card)
        {
            var s = Sides[side];
            return !IsOver && s.Hand.Contains(card) && s.Hero.Power.CanAfford(card.cost);
        }

        public bool PlayCard(int side, CardData card, bool playHidden = false)
        {
            if (!CanPlay(side, card)) return false;
            var s = Sides[side];

            // offensive spellovi trebaju metu, pa idu kroz CastSpell
            if (card is SpellCardData sp && sp.IsTargeted)
            {
                Log("Offensive spell needs a target, use CastSpell.");
                return false;
            }

            s.Hero.Power.TrySpend(card.cost);
            s.Hand.Remove(card);

            // drain karte: uz normalni cost trajno spuste tvoj MAX power (oporavak
            // je prirodni +1/turn do capa), tempo za snagu
            if (card.maxPowerCost > 0)
            {
                s.Hero.Power.LowerMax(card.maxPowerCost);
                Log($"{card.cardName} drains {card.maxPowerCost} max POWER.");
            }

            switch (card)
            {
                case UnitCardData _:
                    var unit = new CardInstance(card, s.Hero.Owner);
                    if (card.ability == CardAbility.ChargeFace) unit.CanAttack = true;
                    s.Board.Add(unit);
                    ApplyOnEnterAbility(side, card);
                    ApplyPassiveAuras(side); // a hero on the board might buff this new unit
                    Log($"{s.Hero.Race} plays {card.cardName}.");
                    OnCardPlayed?.Invoke(side, card);
                    break;

                case HeroCardData hero:
                    var hInst = new CardInstance(card, s.Hero.Owner);
                    if (card.ability == CardAbility.ChargeFace) hInst.CanAttack = true;
                    s.Board.Add(hInst);
                    if (hero.abilityMode == HeroAbilityMode.OnEnter)
                        ApplyOnEnterAbility(side, card);
                    ApplyPassiveAuras(side);
                    Log($"{s.Hero.Race} summons hero {card.cardName}.");
                    OnCardPlayed?.Invoke(side, card);
                    break;

                case SpellCardData spell:
                    if (spell.canBePlayedHidden && playHidden)
                    {
                        var hidden = new CardInstance(card, s.Hero.Owner) { IsHidden = true };
                        s.Board.Add(hidden);
                        Log($"{s.Hero.Race} sets a hidden spell.");
                        // namjerno bez OnCardPlayed, skrivena karta ostaje tajna
                    }
                    else
                    {
                        OnSpellCast?.Invoke(spell, side);
                        ApplyOnEnterAbility(side, card);
                        Log($"{s.Hero.Race} casts {card.cardName}.");
                        OnCardPlayed?.Invoke(side, card);
                        s.Discard.Add(card); // odigrana carolija ide na odbacene
                    }
                    break;
            }

            CheckDeaths();
            OnStateChanged?.Invoke();
            return true;
        }

        // ciljani offensive spell: precizno gadanje, taunt i dalje cuva heroja
        public bool CastSpell(int side, SpellCardData spell, ITargetable target)
        {
            if (!CanPlay(side, spell)) return false;
            if (!spell.IsTargeted) return PlayCard(side, spell);

            var enemy = Sides[1 - side];
            var enemyUnits = enemy.Board.Where(c => !c.IsHidden).ToList();

            if (!TargetingRules.IsLegalTarget(TargetingMode.Precise, target, enemyUnits, enemy.Hero))
            {
                Log("Illegal spell target (taunt protects the hero from precise damage).");
                return false;
            }

            Sides[side].Hero.Power.TrySpend(spell.cost);
            Sides[side].Hand.Remove(spell);
            Sides[side].Discard.Add(spell); // ciljana carolija isto ide na odbacene

            // drain vrijedi i za ciljane spellove (za buduce drain spellove)
            if (spell.maxPowerCost > 0)
            {
                Sides[side].Hero.Power.LowerMax(spell.maxPowerCost);
                Log($"{spell.cardName} drains {spell.maxPowerCost} max POWER.");
            }

            OnSpellCast?.Invoke(spell, side);
            // ramp payoff: steta ovog spella = tvoj trenutni max POWER
            int dmg = spell.ability == CardAbility.DamageEqualsMaxPower
                ? Sides[side].Hero.Power.MaxPower
                : spell.power;
            target.ReceiveDamage(dmg);
            Log($"{Sides[side].Hero.Race} casts {spell.cardName} for {dmg}.");
            OnDamageDealt?.Invoke(target, dmg);
            OnCardPlayed?.Invoke(side, spell);

            // kill-reward: ako je meta pala od ovog spella, draw 1
            if (spell.ability == CardAbility.KillDraw &&
                target is CardTargetable killed && !killed.Card.IsAlive)
            {
                int b = Sides[side].Hand.Count;
                DrawCards(side, 1);
                if (Sides[side].Hand.Count > b) OnCardsDrawn?.Invoke(side, 1);
                Log("The kill fuels another draw!");
            }

            // neki offensive spellovi usto poprskaju cijeli protivnicki board.
            // Meta je svoju stetu vec primila gore, pa je ovdje preskacemo, inace
            // bi kao jedina dobila dvostruko.
            if (spell.ability == CardAbility.Aoe)
            {
                var primary = (target as CardTargetable)?.Card;
                foreach (var u in enemy.Board.Where(c => c.IsAlive && !c.IsHidden && c != primary).ToList())
                {
                    u.TakeDamage(spell.abilityValue);
                    OnDamageDealt?.Invoke(u.AsTargetable(), spell.abilityValue);
                }
            }

            CheckDeaths();
            OnStateChanged?.Invoke();
            return true;
        }

        // ---- attacking ----

        public List<ITargetable> LegalTargets(int side, CardInstance attacker)
        {
            var enemy = Sides[1 - side];
            var enemyUnits = enemy.Board.Where(c => !c.IsHidden).ToList();
            return TargetingRules.GetLegalTargets(attacker.AttackTargeting, enemyUnits, enemy.Hero);
        }

        public List<CardInstance> RevealableSpells(int side)
            => Sides[1 - side].Board.Where(c => c.IsHiddenSpell).ToList();

        // scout trosi napad da otkrije i RAZORUZA skriveni spell (mice ga s ploce)
        public bool ScoutReveal(int side, CardInstance scout, CardInstance hiddenSpell)
        {
            if (!scout.IsScout || !scout.CanAttack) return false;
            if (!hiddenSpell.IsHiddenSpell) return false;

            hiddenSpell.Reveal();
            Sides[1 - side].Board.Remove(hiddenSpell);
            scout.CanAttack = false;
            // razoruzavanje se nagraduje kartom, inace je Scout precesto mrtva karta
            DrawCards(side, 1);
            Log($"{Sides[side].Hero.Race}'s Scout reveals and disarms {hiddenSpell.Data.cardName}, and draws a card!");
            OnStateChanged?.Invoke();
            return true;
        }

        public bool Attack(int side, CardInstance attacker, ITargetable target)
        {
            if (IsOver || !attacker.CanAttack || !attacker.IsAlive || attacker.Attack <= 0)
                return false;

            if (!LegalTargets(side, attacker).Contains(target))
            {
                Log("Illegal attack (taunt rules).");
                return false;
            }

            // zamka: napad na stranu s postavljenim skrivenim spellom ga aktivira,
            // spell se otkrije, odigra svoj efekt, a power udari napadaca
            var defSide = Sides[1 - side];
            var trap = defSide.Board.FirstOrDefault(c => c.IsHiddenSpell);
            if (trap != null)
            {
                trap.Reveal();
                defSide.Board.Remove(trap);
                var tSpell = (SpellCardData)trap.Data;
                Log($"Hidden spell triggers: {tSpell.cardName}!");
                OnSpellCast?.Invoke(tSpell, 1 - side);
                OnCardPlayed?.Invoke(1 - side, tSpell);
                ApplyOnEnterAbility(1 - side, tSpell); // heal/draw/buff dio efekta
                if (tSpell.power > 0)
                {
                    attacker.TakeDamage(tSpell.power);
                    OnDamageDealt?.Invoke(attacker.AsTargetable(), tSpell.power);
                }
                if (!attacker.IsAlive)
                {
                    Log($"{attacker.Data.cardName} is slain by the trap!");
                    CheckDeaths();
                    OnStateChanged?.Invoke();
                    return true;
                }
            }

            if (target is CardTargetable ct)
            {
                // sudar s drugom jedinicom, obje primaju stetu
                var defender = ct.Card;
                int toDefender = MitigatedDamage(1 - side, attacker.Attack);
                int toAttacker = MitigatedDamage(side, defender.Attack);
                defender.TakeDamage(toDefender);
                attacker.TakeDamage(toAttacker);
                OnDamageDealt?.Invoke(target, toDefender);
                // uzvratna steta napadacu isto dobiva svoju brojku
                if (toAttacker > 0)
                    OnDamageDealt?.Invoke(attacker.AsTargetable(), toAttacker);
                // lifesteal: steta koju napadac nanese lijeci njegovog playera
                if (attacker.Data.ability == CardAbility.Lifesteal && toDefender > 0)
                {
                    Sides[side].Hero.Heal(toDefender);
                    Log($"{attacker.Data.cardName} drains {toDefender} health.");
                }
            }
            else
            {
                // udarac u protivnickog heroja, nema uzvrata
                target.ReceiveDamage(attacker.Attack);
                OnDamageDealt?.Invoke(target, attacker.Attack);
                if (attacker.Data.ability == CardAbility.Lifesteal)
                {
                    Sides[side].Hero.Heal(attacker.Attack);
                    Log($"{attacker.Data.cardName} drains {attacker.Attack} health.");
                }
            }

            attacker.CanAttack = false;
            Log($"{attacker.Data.cardName} attacks {target.TargetName} for {attacker.Attack}.");
            OnAttackResolved?.Invoke(attacker, target, attacker.Attack);

            CheckDeaths();
            OnStateChanged?.Invoke();
            return true;
        }

        // a defending side's shield-hero soaks some damage off its units (never below 1)
        private int MitigatedDamage(int defendingSide, int rawDamage)
        {
            if (rawDamage <= 0) return 0;
            int shield = Sides[defendingSide].LivingHeroes
                .Where(h => h.Data.ability == CardAbility.PassiveTauntShield
                            && ((HeroCardData)h.Data).abilityMode == HeroAbilityMode.Passive)
                .Sum(h => h.Data.abilityValue);
            if (shield <= 0) return rawDamage;
            return Mathf.Max(1, rawDamage - shield);
        }

        // ---- abilities ----

        // javi se jednom, kad se card odigra (ili kad OnEnter heroj sleti)
        private void ApplyOnEnterAbility(int side, CardData card)
        {
            var me = Sides[side];
            var enemy = Sides[1 - side];
            switch (card.ability)
            {
                case CardAbility.PowerRamp:
                    me.Hero.Power.RaiseMax(card.abilityValue);
                    me.Hero.Power.AddTemporary(card.abilityValue);
                    break;
                case CardAbility.Draw:
                    // event da UI pusti draw let i za karte koje vuku karte
                    int before = me.Hand.Count;
                    DrawCards(side, card.abilityValue);
                    int got = me.Hand.Count - before;
                    if (got > 0) OnCardsDrawn?.Invoke(side, got);
                    break;
                case CardAbility.Heal:
                    me.Hero.Heal(card.abilityValue);
                    break;
                case CardAbility.BuffAttack:
                    foreach (var u in me.Board.Where(c => c.IsAlive && !c.IsHidden))
                        u.Buff(card.abilityValue, 0);
                    break;
                case CardAbility.Aoe:
                    foreach (var u in enemy.Board.Where(c => c.IsAlive && !c.IsHidden).ToList())
                    {
                        u.TakeDamage(card.abilityValue);
                        OnDamageDealt?.Invoke(u.AsTargetable(), card.abilityValue);
                    }
                    break;

                // ---- unique mehanike novog vala karata ----

                case CardAbility.RefreshAttacks:
                    // sve tvoje jedinice mogu ponovno napasti ovaj potez
                    foreach (var u in me.Board.Where(c => c.IsAlive && !c.IsHidden))
                        u.CanAttack = true;
                    Log("Second wind! Your units can attack again.");
                    break;

                case CardAbility.HealAllUnits:
                    foreach (var u in me.Board.Where(c => c.IsAlive && !c.IsHidden))
                        u.HealFull();
                    Log("Your units are restored to full health.");
                    break;

                case CardAbility.ExecuteStrongest:
                    // pogubi neprijateljsku jedinicu s najvise attacka
                    var strongest = enemy.Board
                        .Where(c => c.IsAlive && !c.IsHidden && c.Data.Category != CardCategory.Spell)
                        .OrderByDescending(c => c.Attack).FirstOrDefault();
                    if (strongest != null)
                    {
                        Log($"{strongest.Data.cardName} is executed!");
                        strongest.TakeDamage(9999);
                        OnDamageDealt?.Invoke(strongest.AsTargetable(), strongest.MaxHealth);
                    }
                    break;

                case CardAbility.SacrificeDraw:
                    // zrtvuj svoju jedinicu s najmanje HP (ne heroja), drawaj N
                    var weakest = me.Board
                        .Where(c => c.IsAlive && !c.IsHidden && c.Data is UnitCardData)
                        .OrderBy(c => c.CurrentHealth).FirstOrDefault();
                    if (weakest == null) { Log("No unit to sacrifice, the pact fizzles."); break; }
                    Log($"{weakest.Data.cardName} is sacrificed to the dark bargain.");
                    weakest.TakeDamage(9999);
                    int hb = me.Hand.Count;
                    DrawCards(side, card.abilityValue);
                    if (me.Hand.Count > hb) OnCardsDrawn?.Invoke(side, me.Hand.Count - hb);
                    break;

                case CardAbility.AoeAllDraw:
                    // steta SVIM jedinicama (i tvojima!), pa draw 2
                    foreach (var s2 in Sides)
                        foreach (var u in s2.Board.Where(c => c.IsAlive && !c.IsHidden).ToList())
                        {
                            u.TakeDamage(card.abilityValue);
                            OnDamageDealt?.Invoke(u.AsTargetable(), card.abilityValue);
                        }
                    int hb2 = me.Hand.Count;
                    DrawCards(side, 2);
                    if (me.Hand.Count > hb2) OnCardsDrawn?.Invoke(side, me.Hand.Count - hb2);
                    break;

                case CardAbility.SummonTokens:
                    // prizovi N 1/1 tokena (runtime karta, ne postoji u decku)
                    for (int i = 0; i < card.abilityValue; i++)
                    {
                        var tok = ScriptableObject.CreateInstance<UnitCardData>();
                        tok.cardName = "Recruit";
                        tok.cost = 1; tok.attack = 1; tok.health = 1;
                        tok.race = card.race; tok.unitType = UnitType.Basic;
                        me.Board.Add(new CardInstance(tok, me.Hero.Owner));
                    }
                    ApplyPassiveAuras(side); // aura heroji buffaju i tokene
                    Log($"{card.abilityValue} Recruits answer the call!");
                    break;
            }
        }

        // passive heroji koji nesto rade na pocetku svakog mog turna
        private void ApplyStartOfTurnPassives(int side)
        {
            var me = Sides[side];
            foreach (var hero in me.LivingHeroes.ToList())
            {
                var data = (HeroCardData)hero.Data;
                if (data.abilityMode != HeroAbilityMode.Passive) continue;
                switch (data.ability)
                {
                    case CardAbility.PassiveRampEachTurn:
                        me.Hero.Power.RaiseMax(data.abilityValue);
                        me.Hero.Power.AddTemporary(data.abilityValue);
                        Log($"{data.cardName}: +{data.abilityValue} POWER.");
                        break;
                    case CardAbility.PassiveHealEachTurn:
                        me.Hero.Heal(data.abilityValue);
                        Log($"{data.cardName}: heals {data.abilityValue}.");
                        break;
                }
            }
        }

        // "aura" heroji koji daju saveznicima +attack dok su zivi. Svaki unit koji je
        // heroj vec buffao oznacim, da se bonus ne zbraja svaki put kad ovo prode.
        private void ApplyPassiveAuras(int side)
        {
            var me = Sides[side];
            foreach (var hero in me.LivingHeroes)
            {
                var data = (HeroCardData)hero.Data;
                if (data.abilityMode == HeroAbilityMode.Passive &&
                    data.ability == CardAbility.PassiveBuffAllies)
                {
                    foreach (var u in me.Board.Where(c => c.IsAlive && !c.IsHidden && c != hero))
                    {
                        if (!u.HasAura(hero))
                        {
                            u.Buff(data.abilityValue, 0);
                            u.MarkAura(hero);
                        }
                    }
                }
            }
        }

        // ---- deaths / winning ----

        private void CheckDeaths()
        {
            foreach (var s in Sides)
            {
                // poginuli junak se vraca u spil, na nasumicno mjesto medu sljedecih
                // 5 karata, da se ne izvuce uvijek odmah sljedeci potez
                var dead = s.Board.Where(u => !u.IsAlive && !u.IsHidden).ToList();
                foreach (var d in dead)
                {
                    if (d.Data is HeroCardData hd && hd.returnToDeckOnDeath)
                    {
                        s.Deck.PutNearTop(d.Data, 5, _rng);
                        Log($"{d.Data.cardName} falls and returns near the top of the deck.");
                    }
                    else
                    {
                        s.Discard.Add(d.Data);
                    }
                }
                s.Board.RemoveAll(u => !u.IsAlive && !u.IsHidden);
            }
        }

        private void HandleDefeat(Owner loser)
        {
            if (IsOver) return;
            IsOver = true;
            Winner = loser == Owner.Player ? Owner.Opponent : Owner.Player;
            Log($"{Winner} wins!");
            OnGameOver?.Invoke(Winner.Value);
        }

        // Predaja: strana gubi mec bez da joj je zdravlje palo na nulu. Koristi se
        // u online mecu kad protivnik izade iz igre ili mu pukne veza, i kad igrac
        // sam napusti mec preko izbornika. Kraj meca ide istim putem kao i obican
        // poraz, pa ekran rezultata, ELO i statistika rade bez izmjena.
        public void Concede(int side)
        {
            if (IsOver || side < 0 || side > 1) return;
            Log($"{Sides[side].Hero.Race} forfeits the match.");
            HandleDefeat(Sides[side].Hero.Owner);
        }

        private void Log(string msg) => OnLog?.Invoke(msg);

        public SideState PlayerSide => Sides[0];
        public SideState OpponentSide => Sides[1];
    }
}
