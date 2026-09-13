using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace LordOfTheRealms
{
    // Lobby za izazivanje prijatelja. Domacin otvori privatnu sobu i dobije kod,
    // prijatelj ga upise i pridruzi se. Rasa i deck se uzimaju iz Free Playa, a
    // mec je prijateljski, pa ne dira ELO.
    public class LobbyController : MonoBehaviour
    {
        private const string MpRaceKey = "mp_race";

        private Canvas _canvas;
        private Transform _root;
        private InputField _nameField, _codeField;
        private Text _status, _codeLabel;
        private Button _createBtn, _joinBtn, _startBtn, _leaveBtn;
        private bool _inRoom;

        private static Race MpRace => (Race)PlayerPrefs.GetInt(MpRaceKey, (int)Race.Humans);

        private void Start()
        {
            AudioManager.EnsureExists();
            AudioManager.Instance.PlayMusic("Music_MainMenu");
            BuildUI();
        }

        private void OnDestroy()
        {
            // ime se sprema i kad igrac samo ode natrag
            if (_nameField != null && !string.IsNullOrWhiteSpace(_nameField.text))
                OnlineService.PlayerName = _nameField.text;
        }

        private void BuildUI()
        {
            var canvasGO = new GameObject("LobbyCanvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _canvas = canvasGO.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            EnsureEventSystem();
            _root = canvasGO.transform;

            var bg = UIFactory.CreatePanel(_root, "Background", new Color(0.06f, 0.05f, 0.07f, 1f));
            UIFactory.Stretch(bg);
            UIFactory.AddVignette(_root);

            var title = UIFactory.CreateText(_root, "Title", Localization.T("lb.title"), 42,
                TextAnchor.MiddleCenter, UIFactory.Ink);
            UIFactory.Anchor(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0, -70), new Vector2(1100, 60));

            var back = UIFactory.CreateButton(_root, "Back", Localization.T("common.menu"), 18);
            UIFactory.Anchor(back.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(0, 1), new Vector2(20, -16), new Vector2(140, 44));
            back.onClick.AddListener(() => { OnlineService.Leave(); GameFlow.LoadMultiplayer(); });

            // ---- ime ----
            var nameLbl = UIFactory.CreateText(_root, "NameLbl", Localization.T("lb.yourname"), 18,
                TextAnchor.MiddleCenter, new Color(0.72f, 0.68f, 0.76f));
            UIFactory.Anchor(nameLbl.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0, -150), new Vector2(400, 26));

            _nameField = UIFactory.CreateInput(_root, "NameField", Localization.T("lb.name_ph"), 22, 16);
            UIFactory.Anchor(_nameField.GetComponent<RectTransform>(),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0, -195), new Vector2(420, 56));
            _nameField.text = OnlineService.PlayerName;
            _nameField.onEndEdit.AddListener(v => OnlineService.PlayerName = v);

            // ---- otvori sobu ----
            _createBtn = UIFactory.CreateButton(_root, "Create", Localization.T("lb.create"), 24);
            UIFactory.Anchor(_createBtn.GetComponent<RectTransform>(),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0, -280), new Vector2(420, 66));
            _createBtn.onClick.AddListener(OnCreate);

            // ---- udi po kodu ----
            _codeField = UIFactory.CreateInput(_root, "CodeField", Localization.T("lb.code_ph"), 22, 12);
            UIFactory.Anchor(_codeField.GetComponent<RectTransform>(),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(-110, -365), new Vector2(280, 56));

            _joinBtn = UIFactory.CreateButton(_root, "Join", Localization.T("lb.join"), 22);
            UIFactory.Anchor(_joinBtn.GetComponent<RectTransform>(),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(120, -365), new Vector2(180, 56));
            _joinBtn.onClick.AddListener(OnJoin);

            // ---- soba ----
            _codeLabel = UIFactory.CreateText(_root, "CodeLabel", "", 30,
                TextAnchor.MiddleCenter, new Color(1f, 0.88f, 0.55f));
            UIFactory.Anchor(_codeLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0, -270), new Vector2(900, 44));
            _codeLabel.gameObject.SetActive(false);

            _startBtn = UIFactory.CreateButton(_root, "Start", Localization.T("lb.start"), 26);
            UIFactory.Anchor(_startBtn.GetComponent<RectTransform>(),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0, -400), new Vector2(420, 66));
            _startBtn.onClick.AddListener(OnStart);
            _startBtn.gameObject.SetActive(false);

            _leaveBtn = UIFactory.CreateButton(_root, "Leave", Localization.T("lb.leave"), 20);
            UIFactory.Anchor(_leaveBtn.GetComponent<RectTransform>(),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0, -480), new Vector2(300, 54));
            _leaveBtn.onClick.AddListener(OnLeave);
            _leaveBtn.gameObject.SetActive(false);

            _status = UIFactory.CreateText(_root, "Status", Localization.T("lb.unranked"), 18,
                TextAnchor.UpperCenter, new Color(0.8f, 0.9f, 1f));
            _status.horizontalOverflow = HorizontalWrapMode.Wrap;
            UIFactory.Anchor(_status.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                new Vector2(0.5f, 0), new Vector2(0, 120), new Vector2(900, 60));
        }

        // ---- radnje ----

        private void OnCreate()
        {
            OnlineService.PlayerName = _nameField.text;
            SetLobbyMode(true);
            SetStatus(Localization.T("mp.connecting"));
            OnlineService.ResetState();
            OnlineService.CreateRoom();
            StartCoroutine(WaitForFriend());
        }

        private void OnJoin()
        {
            string code = _codeField.text;
            if (string.IsNullOrWhiteSpace(code)) return;

            OnlineService.PlayerName = _nameField.text;
            SetLobbyMode(true);
            SetStatus(Localization.T("mp.connecting"));
            OnlineService.ResetState();
            OnlineService.JoinRoom(code);
            StartCoroutine(WaitForFriend());
        }

        private void OnLeave()
        {
            StopAllCoroutines();
            OnlineService.Leave();
            NetMatch.End();
            SetLobbyMode(false);
            SetStatus(Localization.T("lb.unranked"));
        }

        // prikazi ili sakrij dio za sobu
        private void SetLobbyMode(bool inRoom)
        {
            _inRoom = inRoom;
            _createBtn.gameObject.SetActive(!inRoom);
            _joinBtn.gameObject.SetActive(!inRoom);
            _codeField.gameObject.SetActive(!inRoom);
            _codeLabel.gameObject.SetActive(inRoom);
            _leaveBtn.gameObject.SetActive(inRoom);
            _startBtn.gameObject.SetActive(false);
        }

        // ---- cekanje ----

        private IEnumerator WaitForFriend()
        {
            float waited = 0f;
            while (_inRoom && waited < 300f)
            {
                OnlineService.Poll();

                if (OnlineService.State == OnlineService.Phase.Failed)
                {
                    SetStatus(Localization.T("lb.bad_code"));
                    OnlineService.Leave();
                    SetLobbyMode(false);
                    yield break;
                }

                if (!string.IsNullOrEmpty(OnlineService.RoomCode))
                    _codeLabel.text = string.Format(Localization.T("lb.your_code"), OnlineService.RoomCode);

                if (OnlineService.State == OnlineService.Phase.Matched)
                {
                    // veza stoji: razmijeni imena i deckove pa cekaj domacina
                    yield return StartCoroutine(HandshakeThenWaitStart());
                    yield break;
                }

                int dots = Mathf.FloorToInt(waited * 2f) % 4;
                SetStatus(Localization.T("lb.waiting") + new string('.', dots)
                          + "\n" + Localization.T("lb.share"));

                waited += Time.deltaTime;
                yield return null;
            }
        }

        private IEnumerator HandshakeThenWaitStart()
        {
            float net = 0f;
            while (!NetMatch.NetworkReady && net < 10f) { net += Time.deltaTime; yield return null; }
            if (!NetMatch.NetworkReady)
            {
                SetStatus(Localization.T("mp.online_failed"));
                OnLeave();
                yield break;
            }

            var myDeck = DeckStorage.LoadActive(MpRace, true);
            NetMatch.Begin(OnlineService.IsHost, MpRace, myDeck);

            float waited = 0f;
            while (!NetMatch.SetupDone && waited < 10f)
            {
                NetMatch.TickSetup(Time.deltaTime);
                waited += Time.deltaTime;
                yield return null;
            }

            if (!NetMatch.SetupDone)
            {
                SetStatus(Localization.T("mp.online_failed"));
                OnLeave();
                yield break;
            }

            SetStatus(string.Format(Localization.T("lb.joined"), NetMatch.RemoteName));

            // samo domacin pokrece mec; gost ceka signal i krene s njim
            if (OnlineService.IsHost) _startBtn.gameObject.SetActive(true);
            else
            {
                SetStatus(string.Format(Localization.T("lb.joined"), NetMatch.RemoteName)
                          + "\n" + Localization.T("lb.host_starts"));
                StartCoroutine(WaitForHostStart());
            }
        }

        // gost ceka da domacin posalje signal, pa ulazi u istu bitku
        private IEnumerator WaitForHostStart()
        {
            while (!NetMatch.MatchStarted) yield return null;
            EnterMatch();
        }

        private void OnStart()
        {
            NetMatch.SendStartMatch(); // javi gostu da krecemo
            EnterMatch();
        }

        private void EnterMatch()
        {
            var myDeck = DeckStorage.LoadActive(MpRace, true);

            MatchConfig.ClearStory();
            MatchConfig.IsOnline = true;
            MatchConfig.IsFriendly = true;          // prijateljski mec ne dira ELO
            MatchConfig.LocalGoesFirst = OnlineService.IsHost;
            MatchConfig.Seed = NetMatch.Seed;
            MatchConfig.PlayerRace = MpRace;
            MatchConfig.OpponentRace = NetMatch.RemoteRace;
            MatchConfig.PlayerDeckOverride = myDeck;
            MatchConfig.OpponentDeckOverride = NetMatch.RemoteDeck;
            GameFlow.LoadBattle();
        }

        private void SetStatus(string msg) { if (_status != null) _status.text = msg; }

        private void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() != null) return;
            new GameObject("EventSystem",
                typeof(UnityEngine.EventSystems.EventSystem),
                typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
        }

        // ESC vraca korak natrag u Free Play. NE reagira dok tipkas u polje, jer
        // bi te izbacilo usred upisivanja imena ili koda.
        private void Update()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null || !kb.escapeKey.wasPressedThisFrame) return;
            if (_nameField != null && _nameField.isFocused) { _nameField.DeactivateInputField(); return; }
            if (_codeField != null && _codeField.isFocused) { _codeField.DeactivateInputField(); return; }

            OnlineService.Leave();
            NetMatch.End();
            GameFlow.LoadMultiplayer();
        }
    }
}
