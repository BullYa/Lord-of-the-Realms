using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace LordOfTheRealms
{
    // Multiplayer meni: Ranked 1v1, Invite Player (placeholder do netcodea),
    // Edit Deck za multiplayer rasu. Ranked trazi protivnika; posto pravog
    // matchmakinga jos nema, "ne nade" igraca i upari te s botom cija tezina
    // ovisi o tvom elu (svakih 500 ela jaci bot).
    public class MultiplayerController : MonoBehaviour
    {
        private const string MpRaceKey = "mp_race";
        // dopusti mec protiv bota kad se ne nade igrac; ukljuceno dok netcode ne postoji
        private const string AllowBotsKey = "mp_allow_bots";
        private static bool AllowBots
        {
            get => PlayerPrefs.GetInt(AllowBotsKey, 1) == 1;
            set { PlayerPrefs.SetInt(AllowBotsKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        private Button _botToggle;

        private Canvas _canvas;
        private Text _rankText, _raceText, _statusText, _deckStatus;

        private Race MpRace
        {
            get => (Race)PlayerPrefs.GetInt(MpRaceKey, (int)Race.Humans);
            set { PlayerPrefs.SetInt(MpRaceKey, (int)value); PlayerPrefs.Save(); }
        }

        private void Start()
        {
            AudioManager.EnsureExists();
            AudioManager.Instance.PlayMusic("Music_MainMenu");
            MatchConfig.ClearStory();
            BuildUI();
        }

        private void BuildUI()
        {
            var canvasGO = new GameObject("MpCanvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _canvas = canvasGO.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
                new GameObject("EventSystem",
                    typeof(UnityEngine.EventSystems.EventSystem),
                    typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));

            var bg = UIFactory.CreatePanel(canvasGO.transform, "Bg", new Color(0.07f, 0.06f, 0.09f, 1f));
            UIFactory.Stretch(bg);

            var title = UIFactory.CreateText(canvasGO.transform, "Title", Localization.T("menu.freeplay"), 34,
                TextAnchor.UpperCenter, UIFactory.Ink);
            UIFactory.Anchor(title.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(0.5f, 1), new Vector2(0, -24), new Vector2(700, 44));

            // trenutni rank
            _rankText = UIFactory.CreateText(canvasGO.transform, "Rank", "", 22,
                TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0.4f));
            UIFactory.Anchor(_rankText.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(0.5f, 1), new Vector2(0, -78), new Vector2(700, 32));
            RefreshRank();

            // gumbi: ranked, gauntlet, edit deck, invite; race picker ispod
            var ranked = UIFactory.CreateButton(canvasGO.transform, "Ranked", Localization.T("mp.ranked"), 24);
            UIFactory.Anchor(ranked.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(0, 177), new Vector2(400, 68));
            ranked.onClick.AddListener(() => StartCoroutine(FindRankedMatch()));

            var gauntlet = UIFactory.CreateButton(canvasGO.transform, "Gauntlet", Localization.T("mp.gauntlet"), 24);
            UIFactory.Anchor(gauntlet.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(0, 103), new Vector2(400, 68));
            gauntlet.onClick.AddListener(GameFlow.LoadGauntlet);

            // prilagodeni mec: sam biras obje rase i tezinu bota
            var custom = UIFactory.CreateButton(canvasGO.transform, "Custom", Localization.T("mp.custom"), 24);
            UIFactory.Anchor(custom.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(0, -45), new Vector2(400, 68));
            custom.onClick.AddListener(() =>
            {
                GameFlow.MatchSetupFromFreePlay = true;
                GameFlow.LoadMatchSetup();
            });

            var edit = UIFactory.CreateButton(canvasGO.transform, "Edit", Localization.T("mp.editdeck"), 24);
            UIFactory.Anchor(edit.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(0, -119), new Vector2(400, 68));
            edit.onClick.AddListener(() =>
            {
                CollectionContext.DeckEdit = true;
                CollectionContext.EditRace = MpRace;
                CollectionContext.AllCards = false;  // Free Play deck postuje story unlockove
                CollectionContext.BackToMultiplayer = true; // MP deck slot set (dijele ranked i gauntlet)
                CollectionContext.ReturnTo = CollectionContext.Return.Multiplayer;
                GameFlow.LoadCollection();
            });

            var invite = UIFactory.CreateButton(canvasGO.transform, "Invite", Localization.T("mp.invite"), 24);
            UIFactory.Anchor(invite.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(0, 29), new Vector2(400, 68));
            invite.onClick.AddListener(GameFlow.LoadLobby);

            // izbor rase (< RACE >): vrijedi za ranked, gauntlet i edit deck
            _raceText = UIFactory.CreateText(canvasGO.transform, "RaceLbl", "", 20,
                TextAnchor.MiddleCenter, CardArtView.RaceColor(MpRace) * 1.6f);
            UIFactory.Anchor(_raceText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(0, -186), new Vector2(300, 30));
            RefreshRace();

            MakeArrow(canvasGO.transform, new Vector2(-190, -186), "<", -1);
            MakeArrow(canvasGO.transform, new Vector2(190, -186), ">", 1);

            // indikator aktivnog Free Play decka (slot + je li legalan 30/30)
            _deckStatus = UIFactory.CreateText(canvasGO.transform, "DeckStatus", "", 16,
                TextAnchor.MiddleCenter, new Color(0.7f, 0.9f, 0.7f));
            UIFactory.Anchor(_deckStatus.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(0, -228), new Vector2(520, 26));
            RefreshDeckStatus();

            // izbor: smije li te matchmaking upariti s botom kad nema igraca
            _botToggle = UIFactory.CreateButton(canvasGO.transform, "BotToggle", "", 16);
            UIFactory.Anchor(_botToggle.GetComponent<RectTransform>(),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(0, -272), new Vector2(560, 40));
            _botToggle.onClick.AddListener(() => { AllowBots = !AllowBots; RefreshBotToggle(); });
            RefreshBotToggle();

            // status linija (matchmaking poruke); dvoredna, jer poruka o botovima
            // ima i objasnjenje zasto online jos ne radi
            _statusText = UIFactory.CreateText(canvasGO.transform, "Status", "", 18,
                TextAnchor.UpperCenter, new Color(0.8f, 0.9f, 1f));
            _statusText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _statusText.verticalOverflow = VerticalWrapMode.Overflow;
            UIFactory.Anchor(_statusText.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                new Vector2(0.5f, 0), new Vector2(0, 96), new Vector2(900, 60));

            var back = UIFactory.CreateButton(canvasGO.transform, "Back", Localization.T("common.menu"), 18);
            UIFactory.Anchor(back.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(0, 1), new Vector2(20, -16), new Vector2(140, 44));
            back.onClick.AddListener(GameFlow.LoadMainMenu);

            // statistika karijere, dolje-lijevo
            var stats = UIFactory.CreateText(canvasGO.transform, "Stats", PlayerStats.Summary(), 15,
                TextAnchor.LowerLeft, new Color(0.75f, 0.8f, 0.85f));
            UIFactory.Anchor(stats.rectTransform, new Vector2(0, 0), new Vector2(0, 0),
                new Vector2(0, 0), new Vector2(20, 16), new Vector2(560, 130));

            // povijest ranked meceva, gumb dolje-desno
            var histBtn = UIFactory.CreateButton(canvasGO.transform, "HistBtn", Localization.T("mp.history"), 18);
            UIFactory.Anchor(histBtn.GetComponent<RectTransform>(), new Vector2(1, 0), new Vector2(1, 0),
                new Vector2(1, 0), new Vector2(-20, 16), new Vector2(240, 50));
            histBtn.onClick.AddListener(ToggleHistory);
            BuildHistoryPanel(canvasGO.transform);
        }

        private void MakeArrow(Transform parent, Vector2 pos, string label, int dir)
        {
            var b = UIFactory.CreateButton(parent, "Arrow" + label, label, 20);
            UIFactory.Anchor(b.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), pos, new Vector2(46, 38));
            b.onClick.AddListener(() =>
            {
                MpRace = (Race)(((int)MpRace + dir + 4) % 4);
                RefreshRace();
                RefreshDeckStatus();
            });
        }

        // pokazi koji je MP slot aktivan i je li deck legalan (30/30 + heroj)
        private void RefreshDeckStatus()
        {
            if (_deckStatus == null) return;
            int slot = DeckStorage.ActiveSlotIndex(MpRace, true) + 1;
            var deck = DeckStorage.LoadActive(MpRace, true);
            int count = 0;
            foreach (var c in deck.cards) count += c.copies;
            bool ok = count == 30 && !string.IsNullOrEmpty(deck.heroName);
            _deckStatus.text = ok
                ? string.Format(Localization.T("mp.deck_ok"), slot, count)
                : string.Format(Localization.T("mp.deck_edit"), slot, count);
            _deckStatus.color = ok ? new Color(0.6f, 1f, 0.6f) : new Color(1f, 0.72f, 0.5f);
        }

        private void RefreshRank()
        {
            int elo = RankedSystem.Elo;
            _rankText.text = $"{RankedSystem.RankName(elo)}  \u00b7  {elo} ELO";
        }

        // kvacica ispred teksta pokazuje stanje; zeleno kad je ukljuceno
        private void RefreshBotToggle()
        {
            if (_botToggle == null) return;
            var t = _botToggle.GetComponentInChildren<Text>();
            if (t != null)
            {
                t.text = (AllowBots ? "[X]  " : "[  ]  ") + Localization.T("mp.allowbots");
                t.color = AllowBots ? new Color(0.7f, 1f, 0.75f) : new Color(0.75f, 0.72f, 0.78f);
            }
            _botToggle.image.color = AllowBots
                ? new Color(0.18f, 0.34f, 0.2f)
                : new Color(0.2f, 0.19f, 0.23f);
        }

        private void RefreshRace()
        {
            _raceText.text = $"{Localization.T("mp.yourrace")}  {Localization.RaceName(MpRace)}";
            _raceText.color = CardArtView.RaceColor(MpRace) * 1.6f;
        }

        private void SetStatus(string msg) { if (_statusText != null) _statusText.text = msg; }

        // ESC vraca korak natrag; ako je otvorena povijest, prvo nju zatvori
        private void Update()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null || !kb.escapeKey.wasPressedThisFrame) return;
            if (_historyPanel != null) { ToggleHistory(); return; }
            GameFlow.LoadMainMenu();
        }

        // ---- match history panel ----

        private RectTransform _historyPanel;
        private RectTransform _historyDim;

        private void BuildHistoryPanel(Transform parent)
        {
            // full-screen zatamnjenje iza panela (klik na njega zatvara)
            _historyDim = UIFactory.CreatePanel(parent, "HistoryDim", new Color(0f, 0f, 0f, 0.85f));
            UIFactory.Stretch(_historyDim);
            var dimBtn = _historyDim.gameObject.AddComponent<Button>();
            dimBtn.transition = Selectable.Transition.None;
            dimBtn.onClick.AddListener(ToggleHistory);
            _historyDim.gameObject.SetActive(false);

            // panel: puna boja, bez prozirnosti
            _historyPanel = UIFactory.CreatePanel(parent, "HistoryPanel",
                new Color(0.08f, 0.07f, 0.10f, 1f));
            UIFactory.Anchor(_historyPanel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(880, 600));
            _historyPanel.gameObject.SetActive(false);
        }

        private void ToggleHistory()
        {
            bool show = !_historyPanel.gameObject.activeSelf;
            _historyDim.gameObject.SetActive(show);
            _historyPanel.gameObject.SetActive(show);
            if (!show) return;
            _historyDim.SetAsLastSibling();
            _historyPanel.SetAsLastSibling(); // panel iznad dima

            // rebuild sadrzaja svaki put (mecevi se mijenjaju)
            for (int i = _historyPanel.childCount - 1; i >= 0; i--)
                Destroy(_historyPanel.GetChild(i).gameObject);

            // okvir ide poslije brisanja djece, inace bi ga rebuild pojeo
            UIFactory.AddFrame(_historyPanel);

            var title = UIFactory.CreateText(_historyPanel, "T", Localization.T("mp.history"), 26,
                TextAnchor.UpperCenter, UIFactory.Ink);
            UIFactory.Anchor(title.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(0.5f, 1), new Vector2(0, -14), new Vector2(400, 32));

            var entries = MatchHistory.Load();
            if (entries.Count == 0)
            {
                var none = UIFactory.CreateText(_historyPanel, "None",
                    Localization.T("mp.history_none"), 18,
                    TextAnchor.MiddleCenter, new Color(0.7f, 0.7f, 0.75f));
                UIFactory.Stretch(none.rectTransform);
            }
            else
            {
                // sazetak iznad liste: W-L, winrate i prosjecni elo delta
                int wins = 0, deltaSum = 0;
                foreach (var en in entries) { if (en.Won) wins++; deltaSum += en.Delta; }
                int losses = entries.Count - wins;
                float wr = 100f * wins / entries.Count;
                float avg = (float)deltaSum / entries.Count;
                string wrStr = wr.ToString("0.#");
                string avgStr = (avg >= 0 ? "+" : "") + avg.ToString("0.#");
                var sum = UIFactory.CreateText(_historyPanel, "Sum",
                    string.Format(Localization.T("mp.history_summary"), wins, losses, wrStr, avgStr, entries.Count),
                    16, TextAnchor.MiddleCenter,
                    wr >= 50f ? new Color(0.6f, 1f, 0.65f) : new Color(1f, 0.75f, 0.6f));
                UIFactory.Anchor(sum.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                    new Vector2(0.5f, 1), new Vector2(0, -48), new Vector2(820, 24));

                // scrollabilna lista svih meceva (do 50)
                const float rowH = 54f;
                UIFactory.CreateScrollView(_historyPanel, "HistScroll",
                    new Vector2(0, -74), new Vector2(844, 458), out var content);
                content.sizeDelta = new Vector2(0, entries.Count * rowH + 12);
                for (int i = 0; i < entries.Count; i++)
                    BuildHistoryRow(content, entries[i], i);
            }

            var close = UIFactory.CreateButton(_historyPanel, "Close", Localization.T("common.close"), 18);
            UIFactory.Anchor(close.GetComponent<RectTransform>(), new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                new Vector2(0.5f, 0), new Vector2(0, 18), new Vector2(200, 48));
            close.onClick.AddListener(ToggleHistory);
        }

        // kartica jednog meca: WIN/LOSS badge | rase+heroji+ratinzi | elo delta,
        // pozadina i outline u boji ishoda (zeleno win / crveno loss)
        private void BuildHistoryRow(RectTransform parent, MatchHistory.Entry e, int index)
        {
            Color bg = e.Won ? new Color(0.09f, 0.20f, 0.11f, 1f) : new Color(0.23f, 0.09f, 0.10f, 1f);
            Color line = e.Won ? new Color(0.35f, 0.90f, 0.45f, 0.95f) : new Color(0.95f, 0.35f, 0.35f, 0.95f);
            Color txt = e.Won ? new Color(0.80f, 1f, 0.85f) : new Color(1f, 0.82f, 0.82f);

            var row = UIFactory.CreatePanel(parent, $"Row{index}", bg);
            UIFactory.Anchor(row, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(0.5f, 1), new Vector2(0, -6 - index * 54), new Vector2(820, 48));
            var edge = row.gameObject.AddComponent<Outline>();
            edge.effectColor = line;
            edge.effectDistance = new Vector2(1.5f, -1.5f);

            // WIN/LOSS badge, lijevo
            var badge = UIFactory.CreateText(row, "Badge", e.Won ? Localization.T("common.win") : Localization.T("common.loss"), 18,
                TextAnchor.MiddleCenter, line);
            UIFactory.Anchor(badge.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f),
                new Vector2(0, 0.5f), new Vector2(10, 0), new Vector2(80, 40));

            // PVP ili PVE: je li protivnik bio pravi igrac ili bot
            var mode = UIFactory.CreateText(row, "Mode", e.VsHuman ? "PVP" : "PVE", 13,
                TextAnchor.MiddleCenter,
                e.VsHuman ? new Color(0.65f, 0.85f, 1f) : new Color(0.72f, 0.70f, 0.76f));
            UIFactory.Anchor(mode.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f),
                new Vector2(0, 0.5f), new Vector2(94, 0), new Vector2(46, 36));

            // detalji u sredini: tko (heroj) rating vs tko (heroj) rating
            var mid = UIFactory.CreateText(row, "Mid",
                $"{Localization.RaceName(e.PlayerRace)} ({e.PlayerHero})  {e.PlayerRating}    vs    " +
                $"{Localization.RaceName(e.OppRace)} ({e.OppHero})  {e.OppRating}", 15,
                TextAnchor.MiddleCenter, txt);
            mid.resizeTextForBestFit = true;
            mid.resizeTextMinSize = 10;
            mid.resizeTextMaxSize = 15;
            UIFactory.Anchor(mid.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(0, 0), new Vector2(560, 40));

            // elo delta, desno
            var delta = UIFactory.CreateText(row, "Delta",
                $"{(e.Delta >= 0 ? "+" : "")}{e.Delta} ELO", 17,
                TextAnchor.MiddleRight, line);
            UIFactory.Anchor(delta.rectTransform, new Vector2(1, 0.5f), new Vector2(1, 0.5f),
                new Vector2(1, 0.5f), new Vector2(-12, 0), new Vector2(110, 40));
        }

        // Protivnik je pronaden i veza stoji. Obje strane grade isti mec: host salje
        // seed, a svaka strana salje svoju rasu i deck da druga moze sagraditi njezinu
        // stranu. Cekamo da razmjena zavrsi pa ulazimo u bitku.
        private void StartOnlineMatch()
        {
            var myDeck = DeckStorage.LoadActive(MpRace, true);
            StartCoroutine(WaitForSetupThenPlay(myDeck));
        }

        private IEnumerator WaitForSetupThenPlay(SavedDeck myDeck)
        {
            // Session je pronaden, ali NGO veza se podize jos par frameova. Dok ne
            // slusa, CustomMessagingManager ne postoji i registracija bi pukla.
            float netWait = 0f;
            while (!NetMatch.NetworkReady && netWait < 10f)
            {
                netWait += Time.deltaTime;
                yield return null;
            }

            if (!NetMatch.NetworkReady)
            {
                var nm = Unity.Netcode.NetworkManager.Singleton;
                Debug.LogWarning("[Net] mreza nije proradila u 10s. " +
                    $"NetworkManager={(nm == null ? "NEMA" : "ima")}, " +
                    $"IsListening={(nm != null && nm.IsListening)}, " +
                    $"IsServer={(nm != null && nm.IsServer)}, " +
                    $"IsClient={(nm != null && nm.IsClient)}");
                SetStatus(Localization.T("mp.online_failed"));
                OnlineService.Leave();
                _searching = false;
                yield break;
            }

            NetMatch.Begin(OnlineService.IsHost, MpRace, myDeck);

            float waited = 0f;
            while (!NetMatch.SetupDone && waited < 10f)
            {
                NetMatch.TickSetup(Time.deltaTime); // ponavljaj slanje dok ne stigne odgovor
                waited += Time.deltaTime;
                yield return null;
            }

            if (!NetMatch.SetupDone)
            {
                Debug.LogWarning("[Net] razmjena podataka o mecu nije zavrsila u 10s");
                SetStatus(Localization.T("mp.online_failed"));
                NetMatch.End();
                OnlineService.Leave();
                _searching = false;
                yield break;
            }

            MatchConfig.ClearStory();
            MatchConfig.IsRanked = true;
            MatchConfig.IsOnline = true;
            MatchConfig.LocalGoesFirst = OnlineService.IsHost; // host igra prvi
            MatchConfig.Seed = NetMatch.Seed;
            MatchConfig.PlayerRace = MpRace;
            MatchConfig.OpponentRace = NetMatch.RemoteRace;
            MatchConfig.PlayerDeckOverride = myDeck;
            MatchConfig.OpponentDeckOverride = NetMatch.RemoteDeck;
            MatchConfig.RankedOpponentRating = RankedSystem.OpponentRating(RankedSystem.Elo);
            GameFlow.LoadBattle();
        }

        private bool _searching;

        // Matchmaking. Prvo trazi pravog igraca preko Unity Sessions (Lobby + Relay).
        // Ako se nitko ne javi u zadanom roku, pada na bota, ali samo ako je igrac to
        // dopustio prekidacem. Inace stane i javi da nema nikoga.
        private IEnumerator FindRankedMatch()
        {
            if (_searching) yield break; // ne dopusti dupli klik
            _searching = true;

            SetStatus(Localization.T("mp.connecting"));
            OnlineService.ResetState();
            OnlineService.BeginFindMatch();

            // cekaj protivnika; tocke pokazuju da se jos trazi
            float waited = 0f;
            const float SearchTimeout = 12f;
            while (waited < SearchTimeout)
            {
                OnlineService.Poll();

                if (OnlineService.State == OnlineService.Phase.Matched)
                {
                    SetStatus(Localization.T("mp.opponent_found"));
                    yield return new WaitForSeconds(0.8f);
                    StartOnlineMatch();
                    yield break;
                }

                if (OnlineService.State == OnlineService.Phase.Failed)
                {
                    // server nedostupan: nema smisla dalje cekati
                    SetStatus(Localization.T("mp.online_failed"));
                    yield return new WaitForSeconds(1f);
                    break;
                }

                if (OnlineService.State == OnlineService.Phase.Searching)
                {
                    int dots = Mathf.FloorToInt(waited * 2f) % 4;
                    SetStatus(Localization.T("mp.searching") + new string('.', dots));
                }

                waited += Time.deltaTime;
                yield return null;
            }

            // nitko se nije javio: napusti session da ne ostane visjeti
            OnlineService.Leave();

            if (!AllowBots)
            {
                // igrac izricito ne zeli bota, pa mu ne podvaljujemo jednog
                SetStatus(Localization.T("mp.nobody_found"));
                _searching = false;
                yield break;
            }

            int elo = RankedSystem.Elo;
            int oppRating = RankedSystem.OpponentRating(elo);
            // tezina bota prati PROTIVNIKOV rating (streak = jaci protivnik)
            int diff = RankedSystem.DifficultyFor(oppRating);

            int streak = PlayerStats.CurrentStreak;
            string streakNote = streak >= 2 ? string.Format(Localization.T("mp.streak_note"), streak) : "";
            SetStatus(string.Format(Localization.T("mp.no_player"), oppRating) + streakNote);
            yield return new WaitForSeconds(1.1f);

            MatchConfig.ClearStory();
            MatchConfig.IsRanked = true;
            MatchConfig.RankedOpponentRating = oppRating;
            MatchConfig.PlayerRace = MpRace;
            MatchConfig.OpponentRace = (Race)Random.Range(0, 4);
            // multiplayer deck iz AKTIVNOG MP slota (sve karte), story deck netaknut
            MatchConfig.PlayerDeckOverride = DeckStorage.LoadActive(MpRace, true);
            // BotDifficulty enum je 1-based (VeryEasy=1..Nightmare=6), DifficultyFor
            // vraca tocno 1..6 pa se casta direktno, BEZ -1
            MatchConfig.Difficulty = (BotDifficulty)diff;
            GameFlow.LoadBattle();
        }
    }
}
