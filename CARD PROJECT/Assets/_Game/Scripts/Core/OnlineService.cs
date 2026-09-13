using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;

namespace LordOfTheRealms
{
    // Online sloj za ranked 1v1. Radi na Unity Multiplayer Services (Sessions),
    // koji ispod sebe koristi Lobby za pronalazak igraca i Relay za vezu, pa nitko
    // ne mora otvarati portove. Jedan klijent je host sessiona.
    //
    // Sve je async, a UI je na coroutinama, pa se stanje cita preko State i UI ga
    // samo poll-a. Nijedna metoda ne baca prema van; greske zavrse u Error.
    public static class OnlineService
    {
        public enum Phase
        {
            Idle,        // nista se ne dogada
            Connecting,  // init servisa i prijava
            Searching,   // trazimo protivnika
            Matched,     // protivnik je tu, mec moze poceti
            Failed       // nesto je puklo, razlog je u Error
        }

        public static Phase State { get; private set; } = Phase.Idle;
        public static string Error { get; private set; }
        public static bool IsHost { get; private set; }
        public static string PlayerId { get; private set; }

        // Ime koje protivnik vidi. Cuva se lokalno; nema registracije ni provjere
        // jedinstvenosti, jer bi to trazilo bazu i racune.
        private const string NameKey = "player_name";
        public static string PlayerName
        {
            get
            {
                string n = PlayerPrefs.GetString(NameKey, "");
                return string.IsNullOrWhiteSpace(n) ? "Player" : n;
            }
            set
            {
                PlayerPrefs.SetString(NameKey, (value ?? "").Trim());
                PlayerPrefs.Save();
            }
        }

        private static ISession _session;
        public static ISession Session => _session;

        // koliko igraca je trenutno u sessionu (host + gost = 2 znaci puno)
        public static int PlayerCount => _session?.Players?.Count ?? 0;

        // ---- init ----

        private static bool _ready;

        // Inicijalizira UGS i anonimno prijavljuje igraca. Anonimna prijava vezuje
        // identitet uz uredaj, bez registracije i bez lozinke.
        private static async Task<bool> EnsureSignedInAsync()
        {
            if (_ready && AuthenticationService.Instance.IsSignedIn) return true;

            await UnityServices.InitializeAsync();
            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();

            PlayerId = AuthenticationService.Instance.PlayerId;
            _ready = true;
            return true;
        }

        // Sessions preko Relaya traze NGO NetworkManager u sceni. Scene se grade iz
        // koda i nemaju ga, pa ga radimo ovdje i nosimo kroz ucitavanja scena.
        private static void EnsureNetworkManager()
        {
            if (NetworkManager.Singleton != null) return;

            var go = new GameObject("NetworkManager");
            go.SetActive(false); // konfiguriraj prije nego Awake odradi svoje
            var nm = go.AddComponent<NetworkManager>();
            var utp = go.AddComponent<UnityTransport>();
            nm.NetworkConfig ??= new NetworkConfig();
            nm.NetworkConfig.NetworkTransport = utp;
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.SetActive(true);
        }

        // Relay regija. Kad je zadana, preskace se QoS mjerenje koje je kod nas padalo
        // ("Could not do Qos region selection"), a bez regije Relay onda ne dobije
        // alokaciju. Za diplomski je dovoljna jedna europska regija.
        private const string RelayRegion = "europe-west4";

        // ---- matchmaking ----

        // Trazi protivnika. Quick Join prvo pokusa uci u tudi otvoreni session; ako
        // ga nema, sam stvori jedan i ceka da netko dode. Zato poslije jos cekamo
        // drugog igraca, i tek ako ga nema, pozivatelj pada na bota.
        public static async void BeginFindMatch()
        {
            if (State == Phase.Connecting || State == Phase.Searching) return;

            State = Phase.Connecting;
            Error = null;
            IsHost = false;

            try
            {
                await EnsureSignedInAsync();
                EnsureNetworkManager();

                State = Phase.Searching;

                var quickJoin = new QuickJoinOptions
                {
                    Filters = new List<FilterOption>
                    {
                        new(FilterField.AvailableSlots, "1", FilterOperation.GreaterOrEqual),
                    },
                    Timeout = TimeSpan.FromSeconds(5),
                    CreateSession = true, // nema li nikoga, otvori svoj i cekaj
                };

                _session = await MatchmakeAsync(quickJoin, RelayRegion);

                IsHost = _session.IsHost;

                // Mreza se podize odvojeno od sessiona i moze pasti tiho, pa slusamo
                // njezino stanje. Bez ovoga se vidi samo da veza nikad nije proradila.
                if (_session.Network != null)
                {
                    _session.Network.StartFailed += err =>
                        Debug.LogWarning($"[Online] mrezni start NIJE uspio: {err}");
                    _session.Network.StateChanged += s =>
                        Debug.Log($"[Online] mrezno stanje: {s}");
                }

                Debug.Log($"[Online] session {_session.Id}, host={IsHost}, igraca={PlayerCount}");

                // ako smo usli u tudi session, protivnik je vec unutra
                State = PlayerCount >= 2 ? Phase.Matched : Phase.Searching;
            }
            catch (Exception e)
            {
                Error = e.Message;
                State = Phase.Failed;
                Debug.LogWarning($"[Online] matchmaking failed: {e.Message}");
            }
        }

        // Prvo s fiksnom regijom. Ako ta regija nije dostupna, jos jednom bez nje,
        // pa neka Unity bira sam.
        private static async Task<ISession> MatchmakeAsync(QuickJoinOptions quickJoin, string region)
        {
            try
            {
                return await MultiplayerService.Instance.MatchmakeSessionAsync(
                    quickJoin, BuildSessionOptions(region));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Online] regija '{region}' nije prosla ({e.Message}), pokusavam bez nje");
                return await MultiplayerService.Instance.MatchmakeSessionAsync(
                    quickJoin, BuildSessionOptions(null));
            }
        }

        // Regija se zadaje da se preskoci QoS mjerenje koje je kod nas padalo.
        // Konstruktor je oznacen zastarjelim (upozorenje CS0618), ali radi; noviji
        // put ide preko WithNetworkOptions i moze se prebaciti kasnije.
        private static SessionOptions BuildSessionOptions(string region)
        {
            return new SessionOptions
            {
                MaxPlayers = 2,
                Type = "ranked1v1",
            }.WithRelayNetwork(new RelayNetworkOptions(RelayProtocol.Default, region, true));
        }

        // UI ovo zove svaki frame dok ceka; kad dode drugi igrac, faza se prebaci
        public static void Poll()
        {
            if (State == Phase.Searching && PlayerCount >= 2)
                State = Phase.Matched;
        }

        // ---- privatna soba (izazovi prijatelja) ----

        // Kod koji domacin dijeli prijatelju. Sessions ga sam generira.
        public static string RoomCode => _session?.Code;

        // Otvori privatnu sobu i cekaj prijatelja. Privatna znaci da je Quick Join
        // ranked pretraga ne moze naci, pa se ulazi samo preko koda.
        public static async void CreateRoom()
        {
            if (State == Phase.Connecting || State == Phase.Searching) return;
            State = Phase.Connecting;
            Error = null;
            IsHost = true;

            try
            {
                await EnsureSignedInAsync();
                EnsureNetworkManager();
                State = Phase.Searching;

                var opts = BuildSessionOptions(RelayRegion);
                opts.IsPrivate = true;
                _session = await MultiplayerService.Instance.CreateSessionAsync(opts);
                HookNetworkLogs();

                IsHost = true;
                State = PlayerCount >= 2 ? Phase.Matched : Phase.Searching;
            }
            catch (Exception e)
            {
                Error = e.Message;
                State = Phase.Failed;
                Debug.LogWarning($"[Online] soba nije otvorena: {e.Message}");
            }
        }

        // Udi u prijateljevu sobu po kodu.
        public static async void JoinRoom(string code)
        {
            if (State == Phase.Connecting || State == Phase.Searching) return;
            State = Phase.Connecting;
            Error = null;
            IsHost = false;

            try
            {
                await EnsureSignedInAsync();
                EnsureNetworkManager();
                State = Phase.Searching;

                _session = await MultiplayerService.Instance
                    .JoinSessionByCodeAsync(code.Trim().ToUpperInvariant());
                HookNetworkLogs();

                IsHost = _session.IsHost;
                State = PlayerCount >= 2 ? Phase.Matched : Phase.Searching;
            }
            catch (Exception e)
            {
                Error = e.Message;
                State = Phase.Failed;
                Debug.LogWarning($"[Online] ulazak u sobu nije uspio: {e.Message}");
            }
        }

        private static void HookNetworkLogs()
        {
            if (_session?.Network == null) return;
            _session.Network.StartFailed += err => Debug.LogWarning($"[Online] mrezni start NIJE uspio: {err}");
            _session.Network.StateChanged += s => Debug.Log($"[Online] mrezno stanje: {s}");
        }

        // ---- izlaz ----

        // Napusti session i pospremi. Zove se i kad igrac odustane i kad padnemo na bota.
        public static async void Leave()
        {
            var s = _session;
            _session = null;
            State = Phase.Idle;
            IsHost = false;

            try
            {
                if (s != null) await s.LeaveAsync();
                if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                    NetworkManager.Singleton.Shutdown();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Online] leave failed: {e.Message}");
            }
        }

        public static void ResetState()
        {
            if (State == Phase.Failed || State == Phase.Matched) State = Phase.Idle;
        }
    }
}
