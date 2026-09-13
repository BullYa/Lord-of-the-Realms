using System.Collections.Generic;
using System.Linq;

namespace LordOfTheRealms
{
    // sto igrac mora napraviti da korak prode
    public enum TutGate { Text, PlayCard, AttackFace, AttackUnit, ScoutReveal, EndTurn }

    // sto se istice dok korak traje
    public enum TutHi { None, BothFaces, PlayerFace, OppFace, Hand, EndTurn, OppBoard, PlayerBoard }

    public struct TutStep
    {
        public string Key;       // loc kljuc teksta koraka
        public TutGate Gate;
        public string CardName;  // za PlayCard gate
        public TutHi Hi;
    }

    // Skriptirani tutorial mec: igrac su Demoni (imaju draw, ramp i lifesteal),
    // protivnik su Ljudi (imaju Shield, koji se uci napadom na njihovog Templara).
    // Fiksne ruke, fiksni redoslijed izvlacenja i protivnik bez AI-ja.
    // Aktivan samo kad je MatchConfig.IsTutorial true.
    public static class TutorialFlow
    {
        public static int Index;
        public static bool Finished;

        public static readonly TutStep[] Steps =
        {
            // --- potez 1: osnove ---
            new TutStep{ Key="tut.s1",  Gate=TutGate.Text, Hi=TutHi.BothFaces },
            new TutStep{ Key="tut.s2",  Gate=TutGate.Text, Hi=TutHi.PlayerFace },
            new TutStep{ Key="tut.s3",  Gate=TutGate.Text, Hi=TutHi.Hand },
            new TutStep{ Key="tut.s4",  Gate=TutGate.PlayCard, CardName="Imp", Hi=TutHi.Hand },
            new TutStep{ Key="tut.s5",  Gate=TutGate.Text, Hi=TutHi.PlayerBoard },
            new TutStep{ Key="tut.s6",  Gate=TutGate.EndTurn, Hi=TutHi.EndTurn },
            // --- potez 2: draw efekt + napad na jedinicu ---
            new TutStep{ Key="tut.s7",  Gate=TutGate.PlayCard, CardName="Void Imp", Hi=TutHi.Hand },
            new TutStep{ Key="tut.s8",  Gate=TutGate.AttackUnit, Hi=TutHi.OppBoard },
            new TutStep{ Key="tut.s9",  Gate=TutGate.EndTurn, Hi=TutHi.EndTurn },
            // --- potez 3: heal + prvi napad na junaka ---
            new TutStep{ Key="tut.s10", Gate=TutGate.PlayCard, CardName="Soul Siphon", Hi=TutHi.Hand },
            // kljuc nije u nizu jer je korak dodan naknadno; redoslijed odreduje ovaj array
            new TutStep{ Key="tut.s10b", Gate=TutGate.AttackFace, Hi=TutHi.OppFace },
            new TutStep{ Key="tut.s11", Gate=TutGate.EndTurn, Hi=TutHi.EndTurn },
            // --- potez 4: Taunt, Assassin, ramp ---
            new TutStep{ Key="tut.s12", Gate=TutGate.Text, Hi=TutHi.OppBoard },
            new TutStep{ Key="tut.s13", Gate=TutGate.PlayCard, CardName="Hellhound", Hi=TutHi.Hand },
            new TutStep{ Key="tut.s14", Gate=TutGate.PlayCard, CardName="Dark Ritual", Hi=TutHi.Hand },
            new TutStep{ Key="tut.s15", Gate=TutGate.EndTurn, Hi=TutHi.EndTurn },
            // --- potez 5: Assassin probija Taunt + Shield/BLOCKED + lifesteal na plocu ---
            new TutStep{ Key="tut.s16", Gate=TutGate.AttackUnit, Hi=TutHi.OppBoard },
            new TutStep{ Key="tut.s17", Gate=TutGate.Text, Hi=TutHi.OppBoard },
            new TutStep{ Key="tut.s18", Gate=TutGate.PlayCard, CardName="Succubus", Hi=TutHi.Hand },
            new TutStep{ Key="tut.s19", Gate=TutGate.EndTurn, Hi=TutHi.EndTurn },
            // --- potez 6: lifesteal, tvoj Taunt, skrivena zamka ---
            new TutStep{ Key="tut.s20", Gate=TutGate.AttackUnit, Hi=TutHi.OppBoard },
            new TutStep{ Key="tut.s21", Gate=TutGate.PlayCard, CardName="Fel Guard", Hi=TutHi.Hand },
            new TutStep{ Key="tut.s22", Gate=TutGate.PlayCard, CardName="Fel Snare", Hi=TutHi.Hand },
            new TutStep{ Key="tut.s23", Gate=TutGate.EndTurn, Hi=TutHi.EndTurn },
            // --- potez 7: zamka opalila + Scout na plocu ---
            new TutStep{ Key="tut.s24", Gate=TutGate.Text, Hi=TutHi.PlayerBoard },
            new TutStep{ Key="tut.s25", Gate=TutGate.PlayCard, CardName="Void Scout", Hi=TutHi.Hand },
            new TutStep{ Key="tut.s26", Gate=TutGate.EndTurn, Hi=TutHi.EndTurn },
            // --- potez 8: Scout razoruzava + junak ---
            new TutStep{ Key="tut.s27", Gate=TutGate.ScoutReveal, Hi=TutHi.OppBoard },
            new TutStep{ Key="tut.s28", Gate=TutGate.PlayCard, CardName="Vor'gathul", Hi=TutHi.Hand },
            new TutStep{ Key="tut.s29", Gate=TutGate.EndTurn, Hi=TutHi.EndTurn },
            // --- potez 9: drain ---
            new TutStep{ Key="tut.s30", Gate=TutGate.Text, Hi=TutHi.Hand },
            new TutStep{ Key="tut.s31", Gate=TutGate.PlayCard, CardName="Abyssal Titan", Hi=TutHi.Hand },
            new TutStep{ Key="tut.s32", Gate=TutGate.Text, Hi=TutHi.None },
        };

        public static bool Active => MatchConfig.IsTutorial && !Finished;
        public static TutStep Current => Steps[UnityEngine.Mathf.Clamp(Index, 0, Steps.Length - 1)];

        public static void Begin() { Index = 0; Finished = false; _oppTurn = 0; }

        public static void Advance()
        {
            Index++;
            if (Index >= Steps.Length) { Finished = true; Index = Steps.Length - 1; }
        }

        // ---- gating: prolazi samo potez koji korak trazi ----

        // svi gateovi osim PlayCard provjeravaju samo vrstu poteza
        public static bool Allows(TutGate gate)
            => !Active || Current.Gate == gate;

        // PlayCard usto trazi tocno odredenu kartu
        public static bool AllowPlay(CardData card)
            => !Active || (Current.Gate == TutGate.PlayCard && card != null && card.cardName == Current.CardName);

        // ---- fiksni spilovi i ruke ----

        // Ruke krecu PRAZNE: prvi BeginTurn tada izvuce 2 karte umjesto 1,
        // sto je bas pravilo koje korak 3 objasnjava.
        public static void SetupDecks(GameMatch m, IReadOnlyList<CardData> lib)
        {
            CardData F(string n) => lib.FirstOrDefault(c => c.cardName == n);

            m.PlayerSide.Hand.Clear();
            m.OpponentSide.Hand.Clear();

            // vrh spila = KRAJ liste, pa je redoslijed izvlacenja obrnut od popisa
            var player = new List<CardData>();
            for (int i = 0; i < 6; i++) player.Add(F("Tormentor"));  // punjenje protiv fatigue
            player.Add(F("Abyssal Titan"));  // potez 9 (drain -2)
            player.Add(F("Vor'gathul"));     // potez 8 (junak)
            player.Add(F("Void Scout"));     // potez 7 (Scout)
            player.Add(F("Fel Snare"));      // potez 6 (skrivena zamka)
            player.Add(F("Fel Guard"));      // potez 5 (tvoj Taunt)
            player.Add(F("Succubus"));       // potez 4 (lifesteal)
            player.Add(F("Hellhound"));      // potez 3 (Assassin)
            player.Add(F("Soul Siphon"));    // izvuce je Void Imp u 2. potezu
            player.Add(F("Dark Ritual"));    // potez 2 (ramp)
            player.Add(F("Void Imp"));       // dio pocetnog drawa
            player.Add(F("Imp"));            // prva izvucena
            m.PlayerSide.Deck = new Deck(player.Where(c => c != null));

            var opp = new List<CardData>();
            for (int i = 0; i < 6; i++) opp.Add(F("Militia"));
            opp.Add(F("Holy Ward"));    // potez 7 (skrivena zamka za Scout lekciju)
            opp.Add(F("Fireball"));     // potez 5
            opp.Add(F("Templar"));      // potez 4 (Shield)
            opp.Add(F("Shield Guard")); // potez 3 (Taunt)
            opp.Add(F("Smite"));        // dio pocetnog drawa (potez 2)
            opp.Add(F("Footman"));      // prva izvucena (potez 1)
            m.OpponentSide.Deck = new Deck(opp.Where(c => c != null));
        }

        // ---- skriptirani protivnik ----

        private static int _oppTurn;

        // odigra tocno ono sto scenarij trazi za taj protivnikov potez
        public static void RunOpponentTurn(GameMatch m)
        {
            _oppTurn++;
            var hand = m.OpponentSide.Hand;
            CardData InHand(string n) => hand.FirstOrDefault(c => c.cardName == n);

            switch (_oppTurn)
            {
                case 1:
                    Play(m, InHand("Footman"));
                    break;
                case 2:
                    // ceoni pogodak carolijom (postavlja lekciju o healu)
                    Cast(m, InHand("Smite") as SpellCardData, m.PlayerSide.Hero);
                    break;
                case 3:
                    Play(m, InHand("Shield Guard"));   // Taunt
                    break;
                case 4:
                    Play(m, InHand("Templar"));        // Shield (za BLOCKED lekciju)
                    break;
                case 5:
                    Cast(m, InHand("Fireball") as SpellCardData, m.PlayerSide.Hero);
                    break;
                case 6:
                    AttackBest(m);                     // upada u tvoju skrivenu zamku
                    break;
                case 7:
                    // MORA ici licem prema dolje, inace Scout u koraku 27 nema sto razoruzati
                    Play(m, InHand("Holy Ward"), hidden: true);
                    break;
                default:
                    AttackBest(m);
                    break;
            }
        }

        private static void Play(GameMatch m, CardData card, bool hidden = false)
        {
            if (card != null) m.PlayCard(1, card, hidden);
        }

        private static void Cast(GameMatch m, SpellCardData spell, ITargetable target)
        {
            if (spell != null) m.CastSpell(1, spell, target);
        }

        // napadni prvom raspolozivom jedinicom; LegalTargets sam postuje Taunt
        private static void AttackBest(GameMatch m)
        {
            var attacker = m.OpponentSide.Board.FirstOrDefault(
                c => c.IsAlive && !c.IsHidden && c.CanAttack && c.Attack > 0);
            if (attacker == null) return;
            var legal = m.LegalTargets(1, attacker);
            if (legal.Count > 0) m.Attack(1, attacker, legal[0]);
        }
    }
}
