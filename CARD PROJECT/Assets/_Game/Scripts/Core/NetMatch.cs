using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace LordOfTheRealms
{
    // vrsta poteza koji putuje mrezom
    public enum NetMoveKind : byte { PlayCard = 0, CastSpell = 1, Attack = 2, EndTurn = 3, Mulligan = 4, StartMatch = 5, Forfeit = 6 }

    // meta poteza; strana je uvijek u POSILJATELJEVOM okviru (0 = on, 1 = protivnik)
    public enum NetTargetKind : byte { None = 0, Hero = 1, Unit = 2 }

    public struct NetMove
    {
        public NetMoveKind Kind;
        public string CardName;      // za PlayCard i CastSpell
        public bool Hidden;          // spell postavljen licem prema dolje
        public int AttackerIndex;    // indeks na posiljateljevom boardu
        public NetTargetKind TargetKind;
        public int TargetSide;       // 0 = posiljatelj, 1 = protivnik
        public int TargetIndex;
    }

    // Razmjena poteza za online mec. Oba klijenta vrte SVOJ GameMatch s istim
    // seedom, a mrezom putuju samo potezi, ne cijelo stanje ploce. Svaki klijent
    // sebe drzi na strani 0, pa se strane u porukama zrcale pri primitku.
    //
    // Koristi CustomMessagingManager umjesto RPC-ova, jer RPC traze spawnani
    // NetworkObject, a taj traze registrirane prefabe. Ovaj projekt nema nijedan
    // prefab (sve se gradi iz koda), pa bi to znacilo uvoditi assete bez potrebe.
    public static class NetMatch
    {
        private const string SetupMsg = "lotr_setup";
        private const string MoveMsg = "lotr_move";

        public static bool Active { get; private set; }
        public static bool IsHost { get; private set; }

        // stanje dogovoreno na pocetku meca
        public static int Seed { get; private set; }
        public static Race LocalRace { get; private set; }
        public static Race RemoteRace { get; private set; }
        public static SavedDeck RemoteDeck { get; private set; }
        public static string RemoteName { get; private set; } = "Player";
        public static bool SetupDone { get; private set; }

        // potezi koje je protivnik poslao, a jos nisu odigrani lokalno
        private static readonly Queue<NetMove> _pending = new();
        public static bool HasPendingMove => _pending.Count > 0;
        public static NetMove DequeueMove() => _pending.Dequeue();

        public static event Action OnSetupComplete;
        public static event Action OnDisconnected;

        // Protivnik je sam izasao iz meca. Salje se prije nego se veza zatvori, pa
        // stigne odmah, dok se prekid veze primijeti tek kad istekne mrezni timeout.
        public static bool RemoteForfeited { get; private set; }
        public static event Action OnRemoteForfeit;

        public static void SendForfeit() => SendMove(new NetMove { Kind = NetMoveKind.Forfeit });

        // Mulligan se ne stavlja u red poteza, jer ga u toj fazi nitko ne prazni.
        // Cuva se zasebno dok obje strane ne potvrde svoj izbor.
        public static bool RemoteMulliganDone { get; private set; }
        public static string[] RemoteMulliganCards { get; private set; } = Array.Empty<string>();

        // domacin javlja gostu da mec krece; bez toga gost ostane u lobbyju
        public static bool MatchStarted { get; private set; }
        public static void SendStartMatch()
        {
            MatchStarted = true;
            SendMove(new NetMove { Kind = NetMoveKind.StartMatch });
        }

        public static void SendMulligan(IEnumerable<string> cardNames)
        {
            SendMove(new NetMove
            {
                Kind = NetMoveKind.Mulligan,
                CardName = string.Join(",", cardNames),
            });
        }

        // ---- zivotni ciklus ----

        // Mreza je spremna tek kad NetworkManager slusa; CustomMessagingManager
        // do tada ne postoji. Sessions ga pokrece, ali ne u istom frameu.
        public static bool NetworkReady =>
            NetworkManager.Singleton != null
            && NetworkManager.Singleton.IsListening
            && NetworkManager.Singleton.CustomMessagingManager != null;

        public static void Begin(bool isHost, Race localRace, SavedDeck localDeck)
        {
            Active = true;
            IsHost = isHost;
            SetupDone = false;
            LocalRace = localRace;
            _pending.Clear();

            if (!NetworkReady) { Debug.LogWarning("[Net] mreza jos nije spremna"); return; }
            var nm = NetworkManager.Singleton;

            nm.CustomMessagingManager.RegisterNamedMessageHandler(SetupMsg, OnSetup);
            nm.CustomMessagingManager.RegisterNamedMessageHandler(MoveMsg, OnMove);
            nm.OnClientDisconnectCallback += HandleDisconnect;

            // Host odreduje seed da obje strane izvlace isti redoslijed. Obje strane
            // salju svoju rasu i deck, jer svaka mora sagraditi i protivnikovu stranu.
            if (isHost) Seed = UnityEngine.Random.Range(1, int.MaxValue);
            _localRaceCache = localRace;
            _localDeckCache = localDeck;
            _resendTimer = 0f;
            SendSetup(localRace, localDeck);
        }

        public static void End()
        {
            var nm = NetworkManager.Singleton;
            if (nm != null && nm.CustomMessagingManager != null)
            {
                nm.CustomMessagingManager.UnregisterNamedMessageHandler(SetupMsg);
                nm.CustomMessagingManager.UnregisterNamedMessageHandler(MoveMsg);
                nm.OnClientDisconnectCallback -= HandleDisconnect;
            }
            Active = false;
            SetupDone = false;
            MatchStarted = false;
            RemoteMulliganDone = false;
            RemoteForfeited = false;
            RemoteMulliganCards = Array.Empty<string>();
            _pending.Clear();
        }

        private static void HandleDisconnect(ulong _) => OnDisconnected?.Invoke();

        // ---- setup ----

        // deck se salje kao tekst: "heroj|ime:kopije,ime:kopije,..."
        private static string Encode(SavedDeck d)
        {
            if (d == null) return "|";
            var parts = d.cards.Select(c => $"{c.name}:{c.copies}");
            return d.heroName + "|" + string.Join(",", parts);
        }

        private static SavedDeck Decode(string s)
        {
            var deck = new SavedDeck();
            if (string.IsNullOrEmpty(s)) return deck;
            int bar = s.IndexOf('|');
            if (bar < 0) return deck;
            deck.heroName = s.Substring(0, bar);
            string body = s.Substring(bar + 1);
            if (string.IsNullOrEmpty(body)) return deck;
            foreach (var part in body.Split(','))
            {
                int colon = part.LastIndexOf(':');
                if (colon <= 0) continue;
                if (!int.TryParse(part.Substring(colon + 1), out int n)) continue;
                deck.cards.Add(new SavedDeck.CardCount { name = part.Substring(0, colon), copies = n });
            }
            return deck;
        }

        private static void SendSetup(Race race, SavedDeck deck)
        {
            // ime se cisti od razdjelnika, inace bi razbilo poruku
            string name = (OnlineService.PlayerName ?? "Player").Replace("|", " ").Replace(",", " ");
            string payload = $"{Seed}|{(int)race}|{Encode(deck)}|{name}";
            SendString(SetupMsg, payload);
        }

        // Setup se PONAVLJA dok ne stigne protivnikov. Obje strane salju cim se
        // registriraju, pa poruka poslana prije nego druga strana registrira
        // rukovatelja jednostavno propadne. Ponavljanje to rjesava bez dogovaranja
        // tko je prvi.
        private static float _resendTimer;
        private static Race _localRaceCache;
        private static SavedDeck _localDeckCache;

        public static void TickSetup(float dt)
        {
            if (!Active || SetupDone || !NetworkReady) return;
            _resendTimer -= dt;
            if (_resendTimer > 0f) return;
            _resendTimer = 0.5f;
            SendSetup(_localRaceCache, _localDeckCache);
        }

        private static void OnSetup(ulong sender, FastBufferReader reader)
        {
            reader.ReadValueSafe(out string payload);
            var bits = payload.Split('|');
            if (bits.Length < 4) return;

            // gost preuzima hostov seed; host svoj vec ima
            if (!IsHost && int.TryParse(bits[0], out int seed)) Seed = seed;
            if (int.TryParse(bits[1], out int raceId)) RemoteRace = (Race)raceId;
            RemoteDeck = Decode(bits[2] + "|" + bits[3]);
            if (bits.Length >= 5 && !string.IsNullOrWhiteSpace(bits[4])) RemoteName = bits[4];

            SetupDone = true;
            // gost je upravo dobio hostov seed, pa mu vrati potvrdu s vlastitim
            // podacima, da i host sigurno dobije nase
            SendSetup(_localRaceCache, _localDeckCache);
            OnSetupComplete?.Invoke();
        }

        // ---- potezi ----

        public static void SendMove(NetMove m)
        {
            if (!Active) return;
            string payload = string.Join("|", new[]
            {
                ((byte)m.Kind).ToString(),
                m.CardName ?? "",
                m.Hidden ? "1" : "0",
                m.AttackerIndex.ToString(),
                ((byte)m.TargetKind).ToString(),
                m.TargetSide.ToString(),
                m.TargetIndex.ToString(),
            });
            SendString(MoveMsg, payload);
        }

        private static void OnMove(ulong sender, FastBufferReader reader)
        {
            reader.ReadValueSafe(out string payload);
            var b = payload.Split('|');
            if (b.Length < 7) return;

            var m = new NetMove
            {
                Kind = (NetMoveKind)byte.Parse(b[0]),
                CardName = b[1],
                Hidden = b[2] == "1",
                AttackerIndex = int.Parse(b[3]),
                TargetKind = (NetTargetKind)byte.Parse(b[4]),
                // posiljatelj sebe drzi na strani 0, pa kod nas ide obrnuto
                TargetSide = 1 - int.Parse(b[5]),
                TargetIndex = int.Parse(b[6]),
            };

            if (m.Kind == NetMoveKind.StartMatch)
            {
                MatchStarted = true;
                return;
            }

            // predaja ne ide u red poteza: red se prazni samo dok traje protivnikov
            // potez, a protivnik moze izaci i dok smo mi na potezu
            if (m.Kind == NetMoveKind.Forfeit)
            {
                RemoteForfeited = true;
                OnRemoteForfeit?.Invoke();
                return;
            }

            if (m.Kind == NetMoveKind.Mulligan)
            {
                RemoteMulliganCards = string.IsNullOrEmpty(m.CardName)
                    ? Array.Empty<string>()
                    : m.CardName.Split(',');
                RemoteMulliganDone = true;
                return;
            }

            _pending.Enqueue(m);
        }

        // ---- slanje ----

        // salje svima osim sebi; u mecu 1v1 to je tocno jedan primatelj
        private static void SendString(string channel, string payload)
        {
            if (!NetworkReady) return;
            var nm = NetworkManager.Singleton;

            int size = FastBufferWriter.GetWriteSize(payload);
            using var writer = new FastBufferWriter(size, Allocator.Temp);
            writer.WriteValueSafe(payload);

            if (nm.IsServer)
            {
                foreach (var id in nm.ConnectedClientsIds)
                    if (id != nm.LocalClientId)
                        nm.CustomMessagingManager.SendNamedMessage(channel, id, writer);
            }
            else
            {
                nm.CustomMessagingManager.SendNamedMessage(channel, NetworkManager.ServerClientId, writer);
            }
        }
    }
}
