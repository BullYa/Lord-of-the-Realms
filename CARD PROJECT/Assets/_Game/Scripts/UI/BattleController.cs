using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using DG.Tweening;

namespace LordOfTheRealms
{
    // Ekran meca protiv bota: ti dolje, bot gore. Pravila zive u GameMatchu;
    // ova klasa samo crta stanje, prosljeduje klikove i pusta animacije.
    public class BattleController : MonoBehaviour
    {
        private GameMatch _match;
        private BotPlayer _bot;
        private Canvas _canvas;
        private RectTransform _root;   // sve osim pozadine; trese se na udarce
        private bool _hitStopping;

        private RectTransform _oppHandRow, _oppBoardRow, _playerBoardRow, _playerHandRow;
        private PlayerFaceView _oppFace, _playerFace;
        private Text _logText, _turnBanner;
        private Button _endTurnBtn;

        // deck pileovi uz desni rub, jedan po igracu, s brojacem preostalih cards
        private RectTransform _playerDeckPile, _oppDeckPile;
        private Text _playerDeckCount, _oppDeckCount;
        private Text _playerDiscardCount, _oppDiscardCount;

        private CardInstance _selectedAttacker;
        private SpellCardData _pendingSpell;

        private readonly List<string> _log = new();
        private bool _inputLocked;

        // QoL: povijest botovih karata, brojac ruke, end-turn zastita, pause, mulligan
        private readonly List<CardData> _oppHistory = new();
        private RectTransform _historyRow;
        private Text _oppHandCount;
        private bool _endTurnArmed;
        private Color _endTurnDefaultColor;
        private RectTransform _pausePanel;
        private Text _volLabel;
        private readonly List<Button> _speedBtns = new();
        private RectTransform _mullPanel;
        private readonly HashSet<int> _mullPicks = new();
        private Text _histLabel;
        private Image _lowHpVignette;   // crveni puls kad ti HP padne nisko
        private RectTransform _pauseDim; // blokira klikove po plodi dok su postavke otvorene
        private bool _paused;

        // online predaja: razlog se ispise na ekranu rezultata, a zastavica sprjecava
        // da OnDestroy jos jednom posalje predaju nakon sto smo je vec poslali
        private string _forfeitReason;
        private bool _conceded;
        private Coroutine _dcWatch;

        // polje: zone se osvjetljavaju po tome tko je na potezu, romb bljesne na udarac
        private Image _oppZone, _myZone, _seamGem;
        private static readonly Color OppZoneIdle = new(0.6f, 0.18f, 0.18f, 0.07f);
        private static readonly Color OppZoneLive = new(0.7f, 0.22f, 0.22f, 0.16f);
        private static readonly Color MyZoneIdle = new(0.85f, 0.66f, 0.28f, 0.06f);
        private static readonly Color MyZoneLive = new(0.9f, 0.72f, 0.32f, 0.15f);
        private static readonly Color SeamGemIdle = new(0.88f, 0.72f, 0.36f, 0.5f);
        private TutorialDirector _tutor; // null osim u tutorial mecu

        // koje karte su vec na plodi (da pop-in ide samo za nove) + card -> view lookup
        private readonly HashSet<CardInstance> _seenOnBoard = new();
        private readonly Dictionary<CardInstance, CardView> _viewByCard = new();

        private const int PlayerSide = 0;
        private const int OppSide = 1;

        private static readonly Color BgColor = new Color(0.09f, 0.08f, 0.11f, 1f);
        private static readonly Color Ink = new Color(0.96f, 0.95f, 0.92f);
        private static readonly Color BlueUsable = new Color(0.4f, 0.6f, 1f);
        private static readonly Color GreenTarget = new Color(0.35f, 1f, 0.45f);
        private static readonly Color CyanReveal = new Color(0.35f, 0.85f, 1f);
        private static readonly Color OrangeSpell = new Color(1f, 0.55f, 0.3f);
        private static readonly Color YellowSel = new Color(1f, 0.9f, 0.35f);
        private static readonly Color GreenAfford = new Color(0.45f, 1f, 0.55f);

        private void Start()
        {
            AudioManager.EnsureExists();
            SetupMatch();
            // svaka rasa protivnika ima svoju battle glazbu
            AudioManager.Instance.PlayMusic("Music_" + _match.OpponentSide.Hero.Race);
            // brzina igre (postavka iz pause panela, pamti se)
            Time.timeScale = PlayerPrefs.GetFloat("game_speed", 1f);
            BuildUI();
            HookEvents();
            RefreshAll();
            if (MatchConfig.IsTutorial)
            {
                // tutorial: bez mulligana (ruke su fiksne), vodi ga TutorialDirector
                _tutor = TutorialDirector.Create(_canvas, this);
                _match.BeginTurn();
            }
            else if (MatchConfig.IsOnline)
            {
                // online: mulligan radi, ali se salje mrezom (vidi ConfirmMulligan),
                // jer bi inace svaka strana promijesala samo svoj deck i mecevi bi
                // se razisli. BeginTurn se zove tek kad stigne protivnikov izbor.
                NetMatch.OnRemoteForfeit += HandleOpponentForfeit;
                NetMatch.OnDisconnected += HandleOpponentDisconnect;
                ShowMulligan();
            }
            else
            {
                // prvo mulligan, BeginTurn se zove tek kad igrac potvrdi ruku
                ShowMulligan();
            }
        }

        private void OnDestroy()
        {
            Time.timeScale = 1f;
            // online mec zavrsava sa scenom; odjavi handlere da ne vise
            if (MatchConfig.IsOnline)
            {
                NetMatch.OnRemoteForfeit -= HandleOpponentForfeit;
                NetMatch.OnDisconnected -= HandleOpponentDisconnect;
                // Izlazak iz scene usred meca (alt-tab, gasenje igre, povratak u
                // izbornik nekim drugim putem) javi protivniku kao predaju, da ne
                // ceka istek mreznog timeouta.
                if (_match != null && !_match.IsOver && !_conceded) NetMatch.SendForfeit();
                NetMatch.End();
            }
        }

        // ---- online: protivnik je otisao ----

        // Protivnik je poslao predaju. Nema cekanja, pobjeda ide odmah.
        private void HandleOpponentForfeit() => WinByForfeit(Localization.T("bt.opp_left"));

        // Veza je pukla. Prekid moze biti i kratak, pa se ceka nekoliko sekundi
        // prije nego se mec zakljuci, umjesto da svaki treptaj mreze zavrsi partiju.
        private void HandleOpponentDisconnect()
        {
            if (!MatchConfig.IsOnline || _match == null || _match.IsOver) return;
            if (_dcWatch != null) return;
            _dcWatch = StartCoroutine(DisconnectGrace());
        }

        private IEnumerator DisconnectGrace()
        {
            AddLog(Localization.T("bt.opp_lost"));
            float waited = 0f;
            // unscaled, da odbrojavanje tece i ako je igrac u pauzi
            while (waited < 8f && _match != null && !_match.IsOver)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
            _dcWatch = null;
            WinByForfeit(Localization.T("bt.opp_left"));
        }

        // Zajednicki zavrsetak za sve nacine na koje protivnik moze nestati.
        private void WinByForfeit(string reason)
        {
            if (_match == null || _match.IsOver) return;
            _forfeitReason = reason;
            _inputLocked = true;
            // pauza bi zaustavila animacije ekrana rezultata
            if (_paused) TogglePause();
            _match.Concede(OppSide);
        }

        // Igrac sam napusta online mec. Protivniku se javi predaja, a nama se mec
        // zavrsava kao poraz, pa obje strane zabiljeze isti ishod.
        private void ConcedeOnlineMatch()
        {
            if (_match == null || _match.IsOver) return;
            _conceded = true;
            _forfeitReason = Localization.T("bt.you_left");
            _inputLocked = true;
            if (_paused) TogglePause();
            NetMatch.SendForfeit();
            _match.Concede(PlayerSide);
        }

        private void SetupMatch()
        {
            var lib = CardLibrary.All;
            // finalni story battle = boss mod: Vor'gathul 50 HP + Rain of Chaos
            bool finalBoss = MatchConfig.IsStory && MatchConfig.StoryNodeId != null
                             && StoryData.Get(MatchConfig.StoryNodeId) is { IsFinal: true };
            _match = new GameMatch(MatchConfig.PlayerRace, MatchConfig.OpponentRace, lib,
                                   MatchConfig.Seed,
                                   opponentDeckOverride: MatchConfig.OpponentDeckOverride,
                                   playerDeckOverride: MatchConfig.PlayerDeckOverride,
                                   opponentHealth: finalBoss ? 50 : -1,
                                   localGoesFirst: !MatchConfig.IsOnline || MatchConfig.LocalGoesFirst);
            _match.FinalBossMode = finalBoss;
            _bot = new BotPlayer(_match, OppSide, MatchConfig.Difficulty);

            // tutorial: fiksne ruke i redoslijed izvlacenja umjesto mijesanja
            if (MatchConfig.IsTutorial)
            {
                TutorialFlow.Begin();
                TutorialFlow.SetupDecks(_match, lib);
            }
        }

        // U online mecu smijes igrati samo kad si ti na potezu. Offline to cuva
        // _inputLocked dok bot igra, ali online potez traje dok protivnik ne posalje
        // kraj poteza, pa provjeravamo cije je stanje u motoru.
        private bool NotMyTurn => MatchConfig.IsOnline && _match != null && _match.Current != PlayerSide;

        // ---- online: slanje i primanje poteza ----

        // opisi metu u NASEM okviru; NetMatch je pri primitku zrcali
        private NetMove Describe(ITargetable t)
        {
            var m = new NetMove { TargetKind = NetTargetKind.None };
            if (t is PlayerEntity pe)
            {
                m.TargetKind = NetTargetKind.Hero;
                m.TargetSide = pe.Owner == Owner.Player ? 0 : 1;
            }
            else if (t is CardTargetable ct)
            {
                m.TargetKind = NetTargetKind.Unit;
                int side = ct.Card.Owner == Owner.Player ? 0 : 1;
                m.TargetSide = side;
                m.TargetIndex = _match.Sides[side].Board.IndexOf(ct.Card);
            }
            return m;
        }

        private void SendNet(NetMove m)
        {
            if (MatchConfig.IsOnline) NetMatch.SendMove(m);
        }

        // pretvori primljeni potez u metu na NASOJ ploci
        private ITargetable ResolveTarget(NetMove m)
        {
            if (m.TargetKind == NetTargetKind.Hero) return _match.Sides[m.TargetSide].Hero;
            if (m.TargetKind != NetTargetKind.Unit) return null;
            var board = _match.Sides[m.TargetSide].Board;
            if (m.TargetIndex < 0 || m.TargetIndex >= board.Count) return null;
            return board[m.TargetIndex].AsTargetable();
        }

        // Odigraj protivnikov potez. Protivnik je kod nas uvijek strana 1.
        private void ApplyRemote(NetMove m)
        {
            var lib = CardLibrary.All;
            switch (m.Kind)
            {
                case NetMoveKind.PlayCard:
                {
                    var card = _match.OpponentSide.Hand.FirstOrDefault(c => c.cardName == m.CardName)
                               ?? lib.FirstOrDefault(c => c.cardName == m.CardName);
                    if (card != null) _match.PlayCard(OppSide, card, m.Hidden);
                    break;
                }
                case NetMoveKind.CastSpell:
                {
                    var spell = _match.OpponentSide.Hand.FirstOrDefault(c => c.cardName == m.CardName)
                                as SpellCardData;
                    var target = ResolveTarget(m);
                    if (spell != null && target != null) _match.CastSpell(OppSide, spell, target);
                    break;
                }
                case NetMoveKind.Attack:
                {
                    var board = _match.OpponentSide.Board;
                    if (m.AttackerIndex < 0 || m.AttackerIndex >= board.Count) break;
                    var target = ResolveTarget(m);
                    if (target != null) _match.Attack(OppSide, board[m.AttackerIndex], target);
                    break;
                }
            }
            RefreshAll();
        }

        // Cekaj protivnikove poteze dok ne posalje kraj poteza. Zamjenjuje bota.
        private IEnumerator RunRemoteTurn()
        {
            float guard = 0f;
            while (!_match.IsOver && guard < 120f)
            {
                while (NetMatch.HasPendingMove)
                {
                    var m = NetMatch.DequeueMove();
                    if (m.Kind == NetMoveKind.EndTurn) yield break;
                    ApplyRemote(m);
                    yield return new WaitForSeconds(0.45f); // da se potez vidi
                }
                guard += Time.unscaledDeltaTime;
                yield return null;
            }
            // Dvije minute bez ijedne poruke. Veza je mozda formalno ziva, ali igrac
            // s druge strane ocito nije, pa se to racuna kao napustanje meca.
            if (!_match.IsOver) WinByForfeit(Localization.T("bt.opp_timeout"));
        }

        // eventi iz matcha -> animacije (draw let, dmg numberi, botov napad, spell showcase)
        private void HookEvents()
        {
            _match.OnLog += AddLog;
            _match.OnStateChanged += RefreshAll;
            _match.OnGameOver += HandleGameOver;
            _match.OnCardsDrawn += (side, n) =>
            {
                StartCoroutine(DrawFlights(side, n));
            };
            // povijest botovih odigranih karata (skrivene se ne javljaju);
            // cisti se kad NJEGOV potez pocne, prikazuje samo zadnji potez
            _match.OnCardPlayed += (side, card) =>
            {
                if (side == OppSide) AddHistory(card);
            };
            _match.OnTurnStarted += side =>
            {
                if (side == OppSide) { _oppHistory.Clear(); RenderHistory(); }
                ShowTurnBanner(side == PlayerSide);
            };
            // dmg numberi za sve (i botove poteze)
            _match.OnDamageDealt += (target, amount) =>
            {
                if (amount > 0) ShowDamageAt(target, amount);
            };
            // botov napad -> lunge animacija + zvuk
            _match.OnAttackResolved += (attacker, target, _) =>
            {
                AudioManager.Instance?.PlaySfx("Sfx_Attack");
                if (attacker != null && attacker.Owner == Owner.Opponent)
                    AnimateBotLunge(attacker, target);
            };
            // botov spell -> pokazi kartu u sredini
            _match.OnSpellCast += (spell, side) =>
            {
                if (side == OppSide) StartCoroutine(ShowSpellCast(spell));
            };
        }

        // dmg number + shake + crveni flash na pogodenoj meti
        private void ShowDamageAt(ITargetable target, int amount)
        {
            RectTransform rt = null;
            Image img = null;

            // ako je stit upio hit, umjesto broja ide "BLOCKED" (bez trzaja i crvenog)
            bool blocked = target is CardTargetable bc && bc.Card.ConsumeBlocked();

            if (target is CardTargetable ct && _viewByCard.TryGetValue(ct.Card, out var view) && view != null)
            {
                rt = view.GetComponent<RectTransform>();
                img = view.GetComponent<Image>();
            }
            else if (target is PlayerEntity pe)
            {
                var face = pe.Owner == Owner.Player ? _playerFace : _oppFace;
                if (face != null) { rt = face.GetComponent<RectTransform>(); img = face.GetComponent<Image>(); }
            }

            if (blocked)
            {
                if (img != null) StartCoroutine(Anim.Flash(img, new Color(0.55f, 0.85f, 1f)));
                if (rt != null) SpawnFloatingNumber(rt.position, Localization.T("bt.blocked"),
                    new Color(0.6f, 0.88f, 1f), 24);
                return;
            }

            if (rt != null) StartCoroutine(Anim.Shake(rt));
            if (img != null) StartCoroutine(Anim.Flash(img, new Color(1f, 0.3f, 0.25f)));
            if (rt != null) SpawnFloatingNumber(rt.position, $"-{amount}", new Color(1f, 0.5f, 0.4f));
            // jaci pogodak se osjeti: trzaj ploce + kratki hit-stop
            if (amount >= 4) Punch(amount);
        }

        // botov napad: kopija karte se zaleti prema meti (original se unisti pri refreshu)
        private void AnimateBotLunge(CardInstance attacker, ITargetable target)
        {
            if (!_viewByCard.TryGetValue(attacker, out var av) || av == null) return;

            Vector3 targetPos;
            if (target is CardTargetable ct && _viewByCard.TryGetValue(ct.Card, out var tv) && tv != null)
                targetPos = tv.transform.position;
            else if (target is PlayerEntity pe)
                targetPos = (pe.Owner == Owner.Player ? _playerFace : _oppFace).transform.position;
            else
                return;

            var src = av.GetComponent<RectTransform>();
            var ghost = new GameObject("BotLungeGhost", typeof(RectTransform)).GetComponent<RectTransform>();
            ghost.SetParent(_canvas.transform, false);
            ghost.sizeDelta = src.sizeDelta;
            ghost.position = src.position;
            CardArtView.Paint(ghost, attacker.Data);
            StartCoroutine(BotLungeGhost(ghost, targetPos));
        }

        private IEnumerator BotLungeGhost(RectTransform ghost, Vector3 targetPos)
        {
            yield return Anim.Lunge(ghost, targetPos);
            if (ghost != null) Destroy(ghost.gameObject);
        }

        // odigrani spell se pokaze velik u sredini pa nestane
        private IEnumerator ShowSpellCast(SpellCardData spell)
        {
            var card = new GameObject("SpellCast", typeof(RectTransform)).GetComponent<RectTransform>();
            card.SetParent(_canvas.transform, false);
            card.sizeDelta = new Vector2(180, 250);
            card.anchorMin = card.anchorMax = new Vector2(0.5f, 0.5f);
            card.pivot = new Vector2(0.5f, 0.5f);
            card.anchoredPosition = new Vector2(0, 40);
            CardArtView.Paint(card, spell);

            var cg = card.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            card.localScale = Vector3.one * 0.6f;

            // scale i fade in uz opruzni pop
            card.DOScale(1f, 0.22f).SetEase(Ease.OutBack).SetLink(card.gameObject);
            cg.DOFade(1f, 0.16f).SetLink(card.gameObject);
            float t = 0f;
            while (t < 0.9f) { t += Time.deltaTime; yield return null; }

            // fade + shrink out
            card.DOScale(0.85f, 0.2f).SetEase(Ease.InQuad).SetLink(card.gameObject);
            cg.DOFade(0f, 0.2f).SetLink(card.gameObject);
            t = 0f;
            while (t < 0.22f) { t += Time.deltaTime; yield return null; }
            if (card != null) Destroy(card.gameObject);
        }

        // za draw animaciju: leti onoliko cards koliko je izvuceno, s razmakom
        private IEnumerator DrawFlights(int side, int count)
        {
            var pile = side == PlayerSide ? _playerDeckPile : _oppDeckPile;
            var handRow = side == PlayerSide ? _playerHandRow : _oppHandRow;
            if (pile == null || handRow == null) yield break;

            Race race = (side == PlayerSide ? _match.PlayerSide : _match.OpponentSide).Hero.Race;

            // pricekaj frame da se hand ponovno iscrta s novim cards
            yield return null;

            // izvucene cards su ZADNJIH `count` djece; sakrij igraceve dok
            // svaki let ne sleti, i zapamti odredista za obje strane
            var landings = new List<(CanvasGroup cg, Vector3 pos)>();
            int cc = handRow.childCount;
            for (int i = Mathf.Max(0, cc - count); i < cc; i++)
            {
                var go = handRow.GetChild(i).gameObject;
                CanvasGroup cg = null;
                if (side == PlayerSide)
                {
                    cg = go.GetComponent<CanvasGroup>();
                    if (cg == null) cg = go.AddComponent<CanvasGroup>();
                    cg.alpha = 0f;
                }
                landings.Add((cg, go.transform.position));
            }

            for (int i = 0; i < count; i++)
            {
                CanvasGroup handCard = i < landings.Count ? landings[i].cg : null;
                Vector3 target = i < landings.Count
                    ? landings[i].pos
                    : handRow.position;
                StartCoroutine(FlightOne(pile.position, race, target, handCard));

                // mali razmak da se dva drawa vide kao dvije odvojene cards
                float t = 0f;
                while (t < 0.28f) { t += Time.deltaTime; yield return null; }
            }
        }

        // jedan let card backa iz decka do ruke (hand)
        private IEnumerator FlightOne(Vector3 from, Race race, Vector3 target, CanvasGroup handCard)
        {
            var fly = new GameObject("DrawFlight", typeof(RectTransform)).GetComponent<RectTransform>();
            fly.SetParent(_canvas.transform, false);
            fly.sizeDelta = new Vector2(84, 120);
            PaintCardBack(fly, race);
            fly.position = from;
            AudioManager.Instance?.PlaySfx("Sfx_Draw");

            var cg = fly.gameObject.AddComponent<CanvasGroup>();
            fly.DOMove(target, 0.45f).SetEase(Ease.InOutCubic).SetLink(fly.gameObject);
            fly.DOLocalRotate(new Vector3(0, 0, -16f), 0.45f).SetEase(Ease.OutCubic).SetLink(fly.gameObject);
            fly.DOScale(1.25f, 0.25f).SetEase(Ease.OutQuad).SetLink(fly.gameObject);

            float t = 0f;
            while (t < 0.34f) { t += Time.deltaTime; yield return null; }
            if (cg != null) cg.DOFade(0f, 0.16f).SetLink(fly.gameObject);
            if (handCard != null) handCard.DOFade(1f, 0.16f).SetLink(handCard.gameObject);
            t = 0f;
            while (t < 0.18f) { t += Time.deltaTime; yield return null; }
            if (handCard != null) handCard.alpha = 1f;
            if (fly != null) Destroy(fly.gameObject);
        }

        // ---- build UI ----

        private void BuildUI()
        {
            var canvasGO = new GameObject("BattleCanvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _canvas = canvasGO.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            EnsureEventSystem();

            var bg = UIFactory.CreatePanel(canvasGO.transform, "Background", BgColor);
            UIFactory.Stretch(bg);
            // klik na prazno ponistava odabir (bez klik zvuka, nije UIFactory gumb)
            var bgBtn = bg.gameObject.AddComponent<Button>();
            bgBtn.transition = Selectable.Transition.None;
            bgBtn.onClick.AddListener(Deselect);

            BuildPlaymat(canvasGO.transform);

            _oppHandRow = MakeRow(canvasGO.transform, "OppHandRow", 0.92f, 90);
            _oppBoardRow = MakeRow(canvasGO.transform, "OppBoardRow", 0.68f, 200);
            _playerBoardRow = MakeRow(canvasGO.transform, "PlayerBoardRow", 0.42f, 200);
            // u tutorialu je ruka podignuta da je traka s uputama ne prekrije
            _playerHandRow = MakeRow(canvasGO.transform, "PlayerHandRow",
                MatchConfig.IsTutorial ? 0.22f : 0.15f, 210);

            _oppFace = MakeFace(canvasGO.transform, _match.OpponentSide.Hero, 0.80f);
            _playerFace = MakeFace(canvasGO.transform, _match.PlayerSide.Hero, 0.28f);

            // deck piles: player's near their hand (bottom-right), opponent's top-right
            _playerDeckPile = MakeDeckPile(canvasGO.transform, "PlayerDeck",
                MatchConfig.IsTutorial ? 0.26f : 0.15f,
                _match.PlayerSide.Hero.Race, out _playerDeckCount);
            _oppDeckPile = MakeDeckPile(canvasGO.transform, "OppDeck", 0.92f,
                _match.OpponentSide.Hero.Race, out _oppDeckCount);

            // odbacene karte: uz deck pile, malo prema sredini i prigusene, da se
            // odmah razlikuju od decka iz kojeg se jos vuce
            MakeDiscardPile(canvasGO.transform, "PlayerDiscard",
                MatchConfig.IsTutorial ? 0.26f : 0.15f, out _playerDiscardCount);
            MakeDiscardPile(canvasGO.transform, "OppDiscard", 0.92f, out _oppDiscardCount);

            _endTurnBtn = UIFactory.CreateButton(canvasGO.transform, "EndTurn", Localization.T("bt.endturn"), 24);
            UIFactory.Anchor(_endTurnBtn.GetComponent<RectTransform>(),
                new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f),
                new Vector2(-30, 0), new Vector2(210, 90));
            _endTurnBtn.onClick.AddListener(OnEndTurnClicked);

            var menu = UIFactory.CreateButton(canvasGO.transform, "MenuBtn", Localization.T("common.menu"), 18);
            UIFactory.Anchor(menu.GetComponent<RectTransform>(),
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(20, -20), new Vector2(120, 46));
            menu.onClick.AddListener(TogglePause);

            _turnBanner = UIFactory.CreateText(canvasGO.transform, "TurnBanner", "",
                30, TextAnchor.MiddleCenter, Ink);
            UIFactory.Anchor(_turnBanner.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(0.5f, 1), new Vector2(0, -28), new Vector2(700, 50));

            var logPanel = UIFactory.CreatePanel(canvasGO.transform, "LogPanel",
                new Color(0.03f, 0.03f, 0.04f, 0.92f));
            UIFactory.Anchor(logPanel, new Vector2(0, 0.5f), new Vector2(0, 0.5f),
                new Vector2(0, 0.5f), new Vector2(20, 0), new Vector2(340, 230));
            _logText = UIFactory.CreateText(logPanel, "LogText", "", 14,
                TextAnchor.LowerLeft, new Color(0.82f, 0.87f, 0.82f));
            UIFactory.Stretch(_logText.rectTransform);
            _logText.rectTransform.offsetMin = new Vector2(12, 12);
            _logText.rectTransform.offsetMax = new Vector2(-12, -12);
            // dugacke poruke idu u novi red umjesto da clipaju van panela
            _logText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _logText.verticalOverflow = VerticalWrapMode.Overflow; // truncate bi rezao najnovije

            // "enemy played" povijest, gore-lijevo ispod MENU (label skriven dok je prazno)
            _histLabel = UIFactory.CreateText(canvasGO.transform, "HistLbl", Localization.T("bt.enemyplayed"), 13,
                TextAnchor.UpperLeft, new Color(0.7f, 0.6f, 0.6f));
            UIFactory.Anchor(_histLabel.rectTransform, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(0, 1), new Vector2(22, -74), new Vector2(200, 20));
            _histLabel.gameObject.SetActive(false);
            _historyRow = new GameObject("HistoryRow", typeof(RectTransform)).GetComponent<RectTransform>();
            _historyRow.SetParent(canvasGO.transform, false);
            UIFactory.Anchor(_historyRow, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(0, 1), new Vector2(22, -96), new Vector2(320, 96));

            // brojac karata u protivnikovoj ruci, ispod njegovog spila
            _oppHandCount = UIFactory.CreateText(canvasGO.transform, "OppHand", "", 16,
                TextAnchor.MiddleRight, new Color(0.85f, 0.8f, 0.9f));
            UIFactory.Anchor(_oppHandCount.rectTransform, new Vector2(1, 0.80f), new Vector2(1, 0.80f),
                new Vector2(1, 0.5f), new Vector2(-28, 0), new Vector2(130, 26));

            _endTurnDefaultColor = _endTurnBtn.image.color;
            BuildPausePanel(canvasGO.transform);

            // crvena vinjeta za nizak HP, zadnja da je preko ploce (pause se na
            // otvaranju svejedno digne iznad nje preko SetAsLastSibling)
            _lowHpVignette = UIFactory.AddVignette(canvasGO.transform);
            _lowHpVignette.color = new Color(0.8f, 0.05f, 0.05f, 0f);

            MakeShakeRoot();
        }

        // Univerzalno igrace polje umjesto pozadine po rasi: mjedeno uokvirena ploca
        // preko obje borbene zone, s toplim tonom na tvojoj i hladnim na protivnickoj
        // strani te sredisnjim savom. Sve proceduralno, ne trazi art. Ne hvata klikove,
        // pa klik na prazno i dalje ponistava odabir preko Backgrounda ispod.
        private void BuildPlaymat(Transform parent)
        {
            // ukrasna ploha: uvijek centriran pivot i nikad ne hvata klikove
            static RectTransform Decor(Transform p, string name, Color col,
                Vector2 aMin, Vector2 aMax, Vector2 size)
            {
                var rt = UIFactory.CreatePanel(p, name, col);
                UIFactory.Anchor(rt, aMin, aMax, new Vector2(0.5f, 0.5f), Vector2.zero, size);
                rt.GetComponent<Image>().raycastTarget = false;
                return rt;
            }

            var mat = Decor(parent, "Playmat", new Color(0.13f, 0.12f, 0.15f, 0.95f),
                new Vector2(0f, 0.28f), new Vector2(1f, 0.82f), new Vector2(-70, 0));
            UIFactory.AddFrame(mat);

            // protivnicka polovica hladni crveni ton, tvoja topli zlatni
            _oppZone = Decor(mat, "OppZone", OppZoneIdle,
                new Vector2(0f, 0.5f), new Vector2(1f, 1f), new Vector2(-18, -14))
                .GetComponent<Image>();
            _myZone = Decor(mat, "PlayerZone", MyZoneIdle,
                new Vector2(0f, 0f), new Vector2(1f, 0.5f), new Vector2(-18, -14))
                .GetComponent<Image>();

            // Blagi obris borbene zone. NAMJERNO nije podijeljen na mjesta za karte:
            // karte se na plocu slazu centrirano i dinamicke sirine (vidi RefreshRow),
            // pa bi fiksni okviri stajali pomaknuto u odnosu na njih.
            BuildZoneOutline(_oppZone.rectTransform);
            BuildZoneOutline(_myZone.rectTransform);

            // tanka linija u boji rase uz svaku stranu: polje se vezuje uz igraca
            Color myRace = CardArtView.RaceColor(_match.PlayerSide.Hero.Race);
            Color oppRace = CardArtView.RaceColor(_match.OpponentSide.Hero.Race);
            Decor(mat, "OppEdge", new Color(oppRace.r, oppRace.g, oppRace.b, 0.55f),
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(-40, 3));
            Decor(mat, "MyEdge", new Color(myRace.r, myRace.g, myRace.b, 0.55f),
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(-40, 3));

            // sredisnji sav s malim rombom na sredini
            Decor(mat, "Seam", new Color(0.72f, 0.58f, 0.3f, 0.32f),
                new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-46, 2));
            _seamGem = Decor(mat, "SeamGem", SeamGemIdle,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(20, 20))
                .GetComponent<Image>();
            _seamGem.rectTransform.localRotation = Quaternion.Euler(0, 0, 45f);

            // mjedeni kutovi: polje djeluje kao predmet, a ne kao pravokutnik
            foreach (var (ax, ay) in new[] { (0f, 0f), (0f, 1f), (1f, 0f), (1f, 1f) })
            {
                var c = Decor(mat, $"Corner{ax}{ay}", new Color(0.72f, 0.58f, 0.3f, 0.5f),
                    new Vector2(ax, ay), new Vector2(ax, ay), new Vector2(26, 26));
                c.anchoredPosition = new Vector2(ax < 0.5f ? 14 : -14, ay < 0.5f ? 14 : -14);
                c.localRotation = Quaternion.Euler(0, 0, 45f);
            }
        }

        // Jedan blagi okvir po zoni: prazna ploca izgleda namjerno, a ne kao propust.
        private void BuildZoneOutline(RectTransform zone)
        {
            var s = UIFactory.CreatePanel(zone, "ZoneField", new Color(1f, 1f, 1f, 0.02f));
            UIFactory.Anchor(s, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1360, 196));
            s.GetComponent<Image>().raycastTarget = false;
            var o = s.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(1f, 0.9f, 0.7f, 0.08f);
            o.effectDistance = new Vector2(2, -2);
        }

        // Sve osim pozadine ide pod _root, koji se trese na jace udarce. Playmat
        // ide unutra i trese se s kartama, a pozadina NAMJERNO ostaje vani, da se
        // pri trzaju ne vide crni rubovi ekrana.
        private void MakeShakeRoot()
        {
            _root = new GameObject("ShakeRoot", typeof(RectTransform)).GetComponent<RectTransform>();
            _root.SetParent(_canvas.transform, false);
            UIFactory.Stretch(_root);

            var kids = new List<Transform>();
            foreach (Transform c in _canvas.transform)
                if (c != _root.transform && c.name != "Background")
                    kids.Add(c);
            foreach (var c in kids) c.SetParent(_root, false); // redoslijed se cuva
            _root.SetAsLastSibling(); // iznad pozadine
        }

        // Trzaj ploce + hit-stop na jace udarce. Shake ide na unscaled update da tece
        // i dok hit-stop drzi Time.timeScale na nuli.
        private void Punch(int amount)
        {
            if (_root != null)
            {
                _root.DOKill();
                _root.anchoredPosition = Vector2.zero;
                float power = Mathf.Clamp(amount * 1.6f, 6f, 24f);
                _root.DOShakeAnchorPos(0.22f, power, 14, 90f, false, true)
                     .SetUpdate(true)
                     .SetLink(_root.gameObject)
                     .OnComplete(() => { if (_root != null) _root.anchoredPosition = Vector2.zero; });
            }
            // romb na sredini polja kratko bljesne, pa udarac osjeti i sama arena
            if (_seamGem != null)
            {
                _seamGem.DOKill();
                _seamGem.color = new Color(1f, 0.92f, 0.6f, 0.95f);
                _seamGem.DOColor(SeamGemIdle, 0.35f)
                        .SetUpdate(true)
                        .SetLink(_seamGem.gameObject);
            }
            if (!_hitStopping) StartCoroutine(HitStop(0.05f));
        }

        // kratko zamrzne vrijeme pa ga vrati na IZABRANU brzinu igre (ne na 1)
        private IEnumerator HitStop(float seconds)
        {
            _hitStopping = true;
            float restore = PlayerPrefs.GetFloat("game_speed", 1f);
            Time.timeScale = 0f;
            yield return new WaitForSecondsRealtime(seconds);
            // ako je igrac u meduvremenu otvorio pauzu, ne odmrzavaj mu igru
            if (!_paused) Time.timeScale = restore;
            _hitStopping = false;
        }

        private RectTransform MakeRow(Transform parent, string name, float yAnchor, float height)
        {
            var row = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            row.SetParent(parent, false);
            row.anchorMin = new Vector2(0.5f, yAnchor);
            row.anchorMax = new Vector2(0.5f, yAnchor);
            row.pivot = new Vector2(0.5f, 0.5f);
            row.sizeDelta = new Vector2(1400, height);
            return row;
        }

        private PlayerFaceView MakeFace(Transform parent, PlayerEntity hero, float yAnchor)
        {
            var panel = UIFactory.CreatePanel(parent, hero.Owner + "Face",
                new Color(0.17f, 0.15f, 0.19f, 0.97f));
            UIFactory.Anchor(panel, new Vector2(0, yAnchor), new Vector2(0, yAnchor),
                new Vector2(0, 0.5f), new Vector2(190, 0), new Vector2(320, 110));
            var face = panel.gameObject.AddComponent<PlayerFaceView>();
            face.Bind(hero);
            face.OnClicked += OnFaceClicked;
            return face;
        }

        // izgled poledine karte (spil, let, protivnikova ruka)
        private static void PaintCardBack(RectTransform rt, Race race)
        {
            var img = rt.GetComponent<Image>();
            if (img == null) img = rt.gameObject.AddComponent<Image>();
            img.color = CardArtView.RaceColor(race);

            var inner = UIFactory.CreatePanel(rt, "BackInner", new Color(0.08f, 0.075f, 0.09f, 1f));
            inner.anchorMin = Vector2.zero; inner.anchorMax = Vector2.one;
            inner.offsetMin = new Vector2(3, 3); inner.offsetMax = new Vector2(-3, -3);
            inner.gameObject.AddComponent<RectMask2D>();

            var c = CardArtView.RaceColor(race);
            var swoosh = UIFactory.CreatePanel(inner, "BackSwoosh", new Color(c.r, c.g, c.b, 0.22f));
            swoosh.anchorMin = swoosh.anchorMax = new Vector2(0.5f, 0.5f);
            swoosh.pivot = new Vector2(0.5f, 0.5f);
            swoosh.anchoredPosition = Vector2.zero;
            swoosh.sizeDelta = new Vector2(200, 8);
            swoosh.localRotation = Quaternion.Euler(0, 0, 55f);
        }

        // Odbacene karte. Namjerno bez card back sprajta: prazan uokvireni okvir
        // odmah kaze da se odavde ne vuce, a broj govori koliko je karata otislo.
        private RectTransform MakeDiscardPile(Transform parent, string name, float yAnchor,
            out Text countLabel)
        {
            var holder = UIFactory.CreatePanel(parent, name, new Color(0.10f, 0.09f, 0.12f, 0.85f));
            UIFactory.Anchor(holder, new Vector2(1, yAnchor), new Vector2(1, yAnchor),
                new Vector2(1, 0.5f), new Vector2(-140, 0), new Vector2(80, 116));
            holder.GetComponent<Image>().raycastTarget = false;
            UIFactory.AddFrame(holder);

            countLabel = UIFactory.CreateText(holder, "Count", "0", 20,
                TextAnchor.MiddleCenter, new Color(0.72f, 0.68f, 0.75f));
            UIFactory.Stretch(countLabel.rectTransform);
            var edge = countLabel.gameObject.AddComponent<Outline>();
            edge.effectColor = new Color(0f, 0f, 0f, 0.9f);
            edge.effectDistance = new Vector2(1.5f, -1.5f);

            return holder;
        }

        // deck pile na desnom rubu s brojem preostalih cards
        private RectTransform MakeDeckPile(Transform parent, string name, float yAnchor,
            Race race, out Text countLabel)
        {
            var holder = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            holder.SetParent(parent, false);
            UIFactory.Anchor(holder, new Vector2(1, yAnchor), new Vector2(1, yAnchor),
                new Vector2(1, 0.5f), new Vector2(-40, 0), new Vector2(90, 130));

            // nekoliko pomaknutih card backova da izgleda kao hrpa
            for (int i = 0; i < 3; i++)
            {
                var back = new GameObject($"Back{i}", typeof(RectTransform)).GetComponent<RectTransform>();
                back.SetParent(holder, false);
                back.anchorMin = back.anchorMax = new Vector2(0.5f, 0.5f);
                back.pivot = new Vector2(0.5f, 0.5f);
                back.anchoredPosition = new Vector2(i * 3, i * 3);
                back.sizeDelta = new Vector2(80, 116);
                PaintCardBack(back, race);
            }

            countLabel = UIFactory.CreateText(holder, "Count", "", 22,
                TextAnchor.MiddleCenter, UIFactory.Ink);
            UIFactory.Stretch(countLabel.rectTransform);
            // crni obrub da je broj citljiv preko poledina karata
            var edge = countLabel.gameObject.AddComponent<Outline>();
            edge.effectColor = new Color(0f, 0f, 0f, 0.9f);
            edge.effectDistance = new Vector2(1.5f, -1.5f);

            return holder;
        }

        // ---- tutorial ----

        // koji se element istice u trenutnom koraku (glavni okvir)
        public RectTransform HighlightTarget(TutHi hi) => hi switch
        {
            TutHi.PlayerFace => _playerFace != null ? _playerFace.GetComponent<RectTransform>() : null,
            TutHi.BothFaces => _playerFace != null ? _playerFace.GetComponent<RectTransform>() : null,
            TutHi.OppFace => _oppFace != null ? _oppFace.GetComponent<RectTransform>() : null,
            TutHi.Hand => _playerHandRow,
            TutHi.OppBoard => _oppBoardRow,
            TutHi.PlayerBoard => _playerBoardRow,
            TutHi.EndTurn => _endTurnBtn != null ? _endTurnBtn.GetComponent<RectTransform>() : null,
            _ => null,
        };

        // drugi okvir (za korak koji istice oba lica odjednom)
        public RectTransform HighlightTargetSecondary(TutHi hi)
            => hi == TutHi.BothFaces && _oppFace != null ? _oppFace.GetComponent<RectTransform>() : null;

        // ---- rendering ----

        private void RefreshAll()
        {
            if (_match == null) return;

            // mrtve karte prvo odigraju death animaciju
            PlayDeathsForRemoved();

            _viewByCard.Clear();
            RenderBoard(_oppBoardRow, _match.OpponentSide.Board, OppSide);
            RenderBoard(_playerBoardRow, _match.PlayerSide.Board, PlayerSide);
            RenderPlayerHand();
            RenderOppHand();
            _oppFace.Refresh();
            _playerFace.Refresh();
            if (_playerDeckCount != null) _playerDeckCount.text = _match.PlayerSide.Deck.Count.ToString();
            if (_oppDeckCount != null) _oppDeckCount.text = _match.OpponentSide.Deck.Count.ToString();
            // strana koja je na potezu svijetli jace, pa se ciji je potez vidi i
            // bez citanja natpisa gore
            if (_myZone != null && _oppZone != null)
            {
                bool mine = _match.Current == PlayerSide;
                _myZone.color = mine ? MyZoneLive : MyZoneIdle;
                _oppZone.color = mine ? OppZoneIdle : OppZoneLive;
            }
            if (_playerDiscardCount != null) _playerDiscardCount.text = _match.PlayerSide.Discard.Count.ToString();
            if (_oppDiscardCount != null) _oppDiscardCount.text = _match.OpponentSide.Discard.Count.ToString();
            // online: banner prati cije je stanje u motoru, jer protivnikov potez
            // traje dok on ne posalje kraj, a ne dok traje nasa animacija
            bool notMine = MatchConfig.IsOnline ? NotMyTurn : _inputLocked;
            _turnBanner.text = notMine ? Localization.T("bt.opponentsturn") : Localization.T("bt.yourturn");
            _turnBanner.color = _inputLocked ? new Color(1f, 0.6f, 0.5f) : new Color(0.6f, 1f, 0.7f);
            _endTurnBtn.interactable = !_inputLocked && !_match.IsOver;
            // END TURN zasvijetli zuto dok jos imas poteza; arm se resetira na akciju
            _endTurnArmed = false;
            bool moves = !_inputLocked && !_match.IsOver && HasMovesLeft();
            _endTurnBtn.image.color = moves
                ? new Color(0.72f, 0.6f, 0.22f)
                : _endTurnDefaultColor;
            if (_oppHandCount != null)
                _oppHandCount.text = string.Format(Localization.T("bt.hand"), _match.OpponentSide.Hand.Count);
            RenderLog();

            // zaboravi karte koje vise nisu na plodi
            var alive = new HashSet<CardInstance>(
                _match.OpponentSide.Board.Concat(_match.PlayerSide.Board));
            _seenOnBoard.IntersectWith(alive);
        }

        // death animacija za karte koje su upravo umrle
        private void PlayDeathsForRemoved()
        {
            var alive = new HashSet<CardInstance>(
                _match.OpponentSide.Board.Concat(_match.PlayerSide.Board));
            foreach (var kv in _viewByCard)
            {
                if (alive.Contains(kv.Key) || kv.Value == null) continue;
                var view = kv.Value;
                // prebaci na canvas da je ClearRow ne unisti usred animacije
                var rt = view.GetComponent<RectTransform>();
                rt.DOKill();
                Vector3 worldPos = rt.position;
                view.transform.SetParent(_canvas.transform, true);
                rt.position = worldPos;
                AudioManager.Instance?.PlaySfx("Sfx_Death");
                StartCoroutine(Anim.Death(rt));
            }
        }

        private void ClearRow(RectTransform row)
        {
            for (int i = row.childCount - 1; i >= 0; i--)
                Destroy(row.GetChild(i).gameObject);
        }

        private void RenderBoard(RectTransform row, List<CardInstance> board, int side)
        {
            ClearRow(row);
            LayoutCards(row, board.Count, (i, x, w, h) =>
            {
                var card = board[i];
                var go = new GameObject($"Board_{side}_{i}", typeof(RectTransform));
                go.transform.SetParent(row, false);
                var rt = go.GetComponent<RectTransform>();
                rt.anchoredPosition = new Vector2(x, 0);
                rt.sizeDelta = new Vector2(w, h);
                var view = go.AddComponent<CardView>();
                view.Bind(card, card.Owner);
                view.OnClicked += cv => OnBoardCardClicked(cv, side);
                _viewByCard[card] = view;

                if (!_inputLocked && side == PlayerSide && _pendingSpell == null)
                {
                    if (_selectedAttacker == null && card.CanAttack && card.IsAlive
                        && card.Data.Category != CardCategory.Spell)
                        view.SetHighlight(true, BlueUsable);
                    if (_selectedAttacker == card)
                        view.SetHighlight(true, YellowSel);
                }

                // pop-in animacija za card koja je upravo sletjela na board
                if (!_seenOnBoard.Contains(card))
                {
                    _seenOnBoard.Add(card);
                    StartCoroutine(Anim.PopScale(rt));
                }
            });
        }

        private void RenderPlayerHand()
        {
            ClearRow(_playerHandRow);
            var hand = _match.PlayerSide.Hand;

            LayoutCards(_playerHandRow, hand.Count, (i, x, w, h) =>
            {
                var data = hand[i];
                var go = new GameObject($"Hand_{i}", typeof(RectTransform));
                go.transform.SetParent(_playerHandRow, false);
                var rt = go.GetComponent<RectTransform>();
                rt.anchoredPosition = new Vector2(x, 0);
                rt.sizeDelta = new Vector2(w, h);
                var view = go.AddComponent<CardView>();
                var preview = new CardInstance(data, Owner.Player);
                bool affordable = !_inputLocked && _match.CanPlay(PlayerSide, data);
                // posivljena card se odmah vidi kao neigriva; klik i dalje prolazi
                // da dobijes poruku zasto je ne mozes odigrati
                view.Bind(preview, Owner.Player, greyed: !affordable);
                view.OnClicked += _ => OnHandCardClicked(data);
                view.SetHighlight(affordable, GreenAfford);
            });
        }

        private void RenderOppHand()
        {
            ClearRow(_oppHandRow);
            int count = _match.OpponentSide.Hand.Count;
            LayoutCards(_oppHandRow, count, (i, x, w, h) =>
            {
                var go = new GameObject($"OppBack_{i}", typeof(RectTransform)).GetComponent<RectTransform>();
                go.SetParent(_oppHandRow, false);
                go.anchoredPosition = new Vector2(x, 0);
                go.sizeDelta = new Vector2(Mathf.Min(w, 70), h * 0.8f);
                PaintCardBack(go, _match.OpponentSide.Hero.Race);
            });
        }

        private void LayoutCards(RectTransform row, int count, System.Action<int, float, float, float> place)
        {
            if (count == 0) return;
            float cardW = Mathf.Min(150, 1300f / Mathf.Max(count, 1));
            float cardH = row.sizeDelta.y * 0.95f;
            float gap = 14f;
            float totalW = count * cardW + (count - 1) * gap;
            float startX = -totalW / 2f + cardW / 2f;
            for (int i = 0; i < count; i++)
                place(i, startX + i * (cardW + gap), cardW, cardH);
        }

        // ---- input ----

        private void OnHandCardClicked(CardData data)
        {
            if (_inputLocked || _match.IsOver || NotMyTurn) return;
            // tutorial: prolazi samo karta koju trenutni korak trazi
            if (!TutorialFlow.AllowPlay(data)) { _tutor?.FlashBlocked(); return; }
            if (!_match.CanPlay(PlayerSide, data))
            {
                AddLog(Localization.T("bt.log_nopower"));
                return;
            }

            // offensive spell -> sad biraj metu
            if (data is SpellCardData sp && sp.IsTargeted)
            {
                _pendingSpell = sp;
                _selectedAttacker = null;
                AddLog(string.Format(Localization.T("bt.log_selecttarget"), sp.cardName));
                HighlightSpellTargets();
                return;
            }

            bool hidden = data is SpellCardData ds && ds.canBePlayedHidden;

            // spell bez mete ide kroz showcase prvo
            if (data is SpellCardData directSpell && !hidden)
            {
                StartCoroutine(CastDirectSpell(directSpell));
                return;
            }

            int hpBefore = _match.PlayerSide.Hero.CurrentHealth;
            _match.PlayCard(PlayerSide, data, hidden);

            // heal broj na tvom playeru ako te karta izlijecila
            int healed = _match.PlayerSide.Hero.CurrentHealth - hpBefore;
            if (healed > 0)
            {
                StartCoroutine(Anim.Flash(_playerFace.GetComponent<Image>(), new Color(0.4f, 1f, 0.5f)));
                SpawnFloatingNumber(_playerFace.transform.position, $"+{healed}", new Color(0.5f, 1f, 0.6f));
            }
            RefreshAll();
            SendNet(new NetMove { Kind = NetMoveKind.PlayCard, CardName = data.cardName, Hidden = hidden });
            _tutor?.NotifyAction(TutGate.PlayCard);
        }

        // spell bez mete: prvo showcase, pa efekt
        private IEnumerator CastDirectSpell(SpellCardData spell)
        {
            _inputLocked = true;
            yield return ShowSpellCast(spell);

            int hpBefore = _match.PlayerSide.Hero.CurrentHealth;
            _match.PlayCard(PlayerSide, spell, false);
            int healed = _match.PlayerSide.Hero.CurrentHealth - hpBefore;
            if (healed > 0)
            {
                StartCoroutine(Anim.Flash(_playerFace.GetComponent<Image>(), new Color(0.4f, 1f, 0.5f)));
                SpawnFloatingNumber(_playerFace.transform.position, $"+{healed}", new Color(0.5f, 1f, 0.6f));
            }
            _inputLocked = false;
            RefreshAll();
            SendNet(new NetMove { Kind = NetMoveKind.PlayCard, CardName = spell.cardName, Hidden = false });
            _tutor?.NotifyAction(TutGate.PlayCard);
        }

        private void OnBoardCardClicked(CardView view, int side)
        {
            if (_inputLocked || _match.IsOver || NotMyTurn) return;
            var card = view.Card;

            // rjesavanje offensive spella koji ceka, na protivnicki unit
            if (_pendingSpell != null && side == OppSide && !card.IsHidden)
            {
                StartCoroutine(SpellThenRefresh(_pendingSpell, card.AsTargetable(), view));
                _pendingSpell = null;
                return;
            }

            // selecting your own attacker
            if (side == PlayerSide)
            {
                if (card.Data.Category == CardCategory.Spell) return;
                if (!card.CanAttack || !card.IsAlive)
                {
                    AddLog(card.IsAlive ? string.Format(Localization.T("bt.log_cantattack"), card.Data.cardName) : Localization.T("bt.log_dead"));
                    return;
                }
                _selectedAttacker = card;
                view.SetSelected(true); // lift it into the air
                HighlightAttackTargets();
                return;
            }

            // klik na protivnicku card kao metu tvog odabranog napadaca
            if (side == OppSide && _selectedAttacker != null)
            {
                if (_selectedAttacker.IsScout && card.IsHiddenSpell)
                {
                    if (!TutorialFlow.Allows(TutGate.ScoutReveal)) { _tutor?.FlashBlocked(); return; }
                    _match.ScoutReveal(PlayerSide, _selectedAttacker, card);
                    _selectedAttacker = null;
                    RefreshAll();
                    _tutor?.NotifyAction(TutGate.ScoutReveal);
                    return;
                }
                if (!TutorialFlow.Allows(TutGate.AttackUnit)) { _tutor?.FlashBlocked(); return; }
                StartCoroutine(AttackThenRefresh(_selectedAttacker, card.AsTargetable(), view));
                _selectedAttacker = null;
            }
        }

        private void OnFaceClicked(PlayerFaceView face)
        {
            if (_inputLocked || _match.IsOver || NotMyTurn) return;

            if (_pendingSpell != null && face.Entity.Owner == Owner.Opponent)
            {
                StartCoroutine(SpellThenRefresh(_pendingSpell, face.Entity, null));
                _pendingSpell = null;
                return;
            }

            if (_selectedAttacker != null && face.Entity.Owner == Owner.Opponent)
            {
                if (!TutorialFlow.Allows(TutGate.AttackFace)) { _tutor?.FlashBlocked(); return; }
                StartCoroutine(AttackFaceThenRefresh(_selectedAttacker, face));
                _selectedAttacker = null;
            }
        }

        // tvoj napad: lunge pa efekt (dmg numberi idu preko OnDamageDealt eventa)
        private IEnumerator AttackThenRefresh(CardInstance attacker, ITargetable target, CardView targetView)
        {
            _inputLocked = true;
            if (_viewByCard.TryGetValue(attacker, out var av) && av != null && targetView != null)
                yield return Anim.Lunge(av.GetComponent<RectTransform>(), targetView.transform.position);
            // indekse uzimamo PRIJE napada, jer mrtve jedinice odmah nestanu s boarda
            var nau = Describe(target);
            nau.Kind = NetMoveKind.Attack;
            nau.AttackerIndex = _match.PlayerSide.Board.IndexOf(attacker);
            _match.Attack(PlayerSide, attacker, target);
            _inputLocked = false;
            RefreshAll();
            SendNet(nau);
            _tutor?.NotifyAction(TutGate.AttackUnit);
        }

        private IEnumerator AttackFaceThenRefresh(CardInstance attacker, PlayerFaceView face)
        {
            _inputLocked = true;
            if (_viewByCard.TryGetValue(attacker, out var av) && av != null)
                yield return Anim.Lunge(av.GetComponent<RectTransform>(), face.transform.position);
            var nfa = Describe(face.Entity);
            nfa.Kind = NetMoveKind.Attack;
            nfa.AttackerIndex = _match.PlayerSide.Board.IndexOf(attacker);
            _match.Attack(PlayerSide, attacker, face.Entity);
            _inputLocked = false;
            RefreshAll();
            SendNet(nfa);
            _tutor?.NotifyAction(TutGate.AttackFace);
        }

        private IEnumerator SpellThenRefresh(SpellCardData spell, ITargetable target, CardView targetView)
        {
            _inputLocked = true;
            yield return ShowSpellCast(spell); // show the card big before it lands
            // metu opisujemo prije bacanja, jer pogodena jedinica moze odmah nestati
            var ns = Describe(target);
            ns.Kind = NetMoveKind.CastSpell;
            ns.CardName = spell.cardName;
            _match.CastSpell(PlayerSide, spell, target);
            _inputLocked = false;
            RefreshAll();
            SendNet(ns);
        }

        // dmg/heal broj koji odleti gore i nestane
        private void SpawnFloatingNumber(Vector3 worldPos, string text, Color color, int size = 34)
        {
            var label = UIFactory.CreateText(_canvas.transform, "Floating", text, size,
                TextAnchor.MiddleCenter, color);
            label.horizontalOverflow = HorizontalWrapMode.Overflow; // "BLOCKED" ne smije clipati
            label.transform.position = worldPos;
            var lrt = label.rectTransform;
            lrt.sizeDelta = new Vector2(120, 50);
            var outline = label.gameObject.AddComponent<UnityEngine.UI.Outline>();
            outline.effectColor = new Color(0, 0, 0, 0.8f);
            outline.effectDistance = new Vector2(2, -2);
            StartCoroutine(Anim.FloatText(label));
        }

        private void HighlightAttackTargets()
        {
            RefreshAll();
            if (_selectedAttacker == null) return;
            var legal = _match.LegalTargets(PlayerSide, _selectedAttacker);

            foreach (Transform child in _oppBoardRow)
            {
                var v = child.GetComponent<CardView>();
                if (v == null) continue;
                if (_selectedAttacker.IsScout && v.Card.IsHiddenSpell)
                    v.SetHighlight(true, CyanReveal);
                else
                    v.SetHighlight(legal.Contains(v.Card.AsTargetable()), GreenTarget);
            }
            _oppFace.SetHighlight(legal.Contains(_match.OpponentSide.Hero), GreenTarget);
        }

        private void HighlightSpellTargets()
        {
            RefreshAll();
            if (_pendingSpell == null) return;
            var enemy = _match.OpponentSide;
            var units = enemy.Board.Where(c => !c.IsHidden).ToList();
            var legal = TargetingRules.GetLegalTargets(TargetingMode.Precise, units, enemy.Hero);
            foreach (Transform child in _oppBoardRow)
            {
                var v = child.GetComponent<CardView>();
                if (v == null) continue;
                v.SetHighlight(legal.Contains(v.Card.AsTargetable()), OrangeSpell);
            }
            _oppFace.SetHighlight(legal.Contains(enemy.Hero), OrangeSpell);
        }

        // ---- turn flow ----

        private void OnEndTurnClicked()
        {
            if (_inputLocked || _match.IsOver || NotMyTurn) return;
            if (!TutorialFlow.Allows(TutGate.EndTurn)) { _tutor?.FlashBlocked(); return; }
            // zastita: ako jos ima poteza, trazi drugi klik (u tutorialu ne treba)
            if (!MatchConfig.IsTutorial && HasMovesLeft() && !_endTurnArmed)
            {
                _endTurnArmed = true;
                AddLog(string.Format(Localization.T("bt.log_stillmoves"), Localization.T("bt.endturn")));
                return;
            }
            _selectedAttacker = null;
            _pendingSpell = null;
            SendNet(new NetMove { Kind = NetMoveKind.EndTurn });
            StartCoroutine(RunOpponentTurn());
        }

        // ima li igrac jos smislenih poteza (karta koju moze platiti / jedinica koja moze napasti)
        private bool HasMovesLeft()
        {
            if (_match.PlayerSide.Hand.Any(c => _match.CanPlay(PlayerSide, c))) return true;
            return _match.PlayerSide.Board.Any(u => u.CanAttack && u.IsAlive && u.Attack > 0
                                                    && u.Data.Category != CardCategory.Spell);
        }

        // ESC / klik na prazno: ponisti odabir; ESC bez odabira otvara pause
        private void Deselect()
        {
            if (_mullPanel != null) return;
            if (_selectedAttacker == null && _pendingSpell == null) return;
            _selectedAttacker = null;
            _pendingSpell = null;
            RefreshAll();
        }

        // ---- mulligan ----

        // pocetna ruka: klikni karte koje zelis zamijeniti, pa CONFIRM
        private void ShowMulligan()
        {
            _mullPicks.Clear();
            _mullPanel = UIFactory.CreatePanel(_canvas.transform, "Mulligan",
                new Color(0.03f, 0.02f, 0.04f, 0.96f));
            UIFactory.Stretch(_mullPanel);
            RebuildMulligan();
        }

        private void RebuildMulligan()
        {
            CardTooltip.HideActive(); // rebuild unistava hoveranu karticu, makni tooltip
            for (int i = _mullPanel.childCount - 1; i >= 0; i--)
                Destroy(_mullPanel.GetChild(i).gameObject);

            var t = UIFactory.CreateText(_mullPanel, "T", Localization.T("bt.startinghand"), 32,
                TextAnchor.MiddleCenter, UIFactory.Ink);
            UIFactory.Anchor(t.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(0.5f, 1), new Vector2(0, -70), new Vector2(700, 44));

            var hint = UIFactory.CreateText(_mullPanel, "H",
                Localization.T("bt.mull_hint"), 17,
                TextAnchor.MiddleCenter, new Color(0.8f, 0.85f, 0.9f));
            UIFactory.Anchor(hint.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(0.5f, 1), new Vector2(0, -112), new Vector2(800, 26));

            var hand = _match.PlayerSide.Hand;
            float spread = 195f;
            float startX = -(hand.Count - 1) * spread / 2f;
            for (int i = 0; i < hand.Count; i++)
            {
                int idx = i;
                var tile = new GameObject($"Mull_{i}", typeof(RectTransform), typeof(Image), typeof(Button))
                    .GetComponent<RectTransform>();
                tile.SetParent(_mullPanel, false);
                tile.anchorMin = tile.anchorMax = new Vector2(0.5f, 0.5f);
                tile.pivot = new Vector2(0.5f, 0.5f);
                tile.anchoredPosition = new Vector2(startX + i * spread, 20);
                tile.sizeDelta = new Vector2(175, 240);
                CardArtView.Paint(tile, hand[i]);
                tile.gameObject.AddComponent<RowHover>().Init(hand[i], _canvas);
                tile.GetComponent<Button>().onClick.AddListener(() =>
                {
                    if (!_mullPicks.Remove(idx)) _mullPicks.Add(idx);
                    RebuildMulligan();
                });

                if (_mullPicks.Contains(i))
                {
                    var mark = UIFactory.CreatePanel(tile, "Replace", new Color(0.7f, 0.15f, 0.15f, 0.85f));
                    mark.anchorMin = new Vector2(0, 0.5f); mark.anchorMax = new Vector2(1, 0.5f);
                    mark.offsetMin = new Vector2(4, -16); mark.offsetMax = new Vector2(-4, 16);
                    var mt = UIFactory.CreateText(mark, "T", Localization.T("bt.replace"), 18,
                        TextAnchor.MiddleCenter, Color.white);
                    UIFactory.Stretch(mt.rectTransform);
                    mark.GetComponent<Image>().raycastTarget = false;
                    mt.raycastTarget = false;
                }
            }

            var confirm = UIFactory.CreateButton(_mullPanel, "Confirm",
                _mullPicks.Count > 0 ? string.Format(Localization.T("bt.mull_replace"), _mullPicks.Count) : Localization.T("bt.mull_keep"), 22);
            UIFactory.Anchor(confirm.GetComponent<RectTransform>(), new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                new Vector2(0.5f, 0), new Vector2(0, 60), new Vector2(360, 70));
            confirm.onClick.AddListener(ConfirmMulligan);
        }

        private void ConfirmMulligan()
        {
            // pokupi karte PRIJE zamjene (indeksi vrijede za trenutnu ruku)
            var hand = _match.PlayerSide.Hand;
            var toReplace = _mullPicks.Where(i => i < hand.Count).Select(i => hand[i]).ToList();

            Destroy(_mullPanel.gameObject);
            _mullPanel = null;

            if (MatchConfig.IsOnline)
            {
                // online: obje strane biraju istovremeno, pa cekamo protivnikov
                // izbor prije nego mec krene, inace bi mu deck bio drukciji kod nas
                if (toReplace.Count > 0) _match.Mulligan(PlayerSide, toReplace);
                NetMatch.SendMulligan(toReplace.Select(c => c.cardName));
                StartCoroutine(WaitForRemoteMulligan());
                return;
            }

            BotAutoMulligan();
            if (toReplace.Count > 0) _match.Mulligan(PlayerSide, toReplace);
            _match.BeginTurn();
        }

        // Primi protivnikov mulligan i tek onda pokreni mec. Ako ne stigne, krecemo
        // svejedno; njegov deck ostaje kakav je bio, sto je bolje nego stajati.
        private IEnumerator WaitForRemoteMulligan()
        {
            float waited = 0f;
            while (!NetMatch.RemoteMulliganDone && waited < 20f)
            {
                waited += Time.deltaTime;
                yield return null;
            }

            if (NetMatch.RemoteMulliganDone && NetMatch.RemoteMulliganCards.Length > 0)
            {
                var oppHand = _match.OpponentSide.Hand;
                var theirs = new List<CardData>();
                foreach (var n in NetMatch.RemoteMulliganCards)
                {
                    var c = oppHand.FirstOrDefault(x => x.cardName == n && !theirs.Contains(x));
                    if (c != null) theirs.Add(c);
                }
                if (theirs.Count > 0) _match.Mulligan(OppSide, theirs);
            }

            _match.BeginTurn();
            if (!MatchConfig.LocalGoesFirst) StartCoroutine(RunFirstRemoteTurn());
        }

        // bot mijenja skupe karte (cost 5+) da ima igrivu ranu ruku
        private void BotAutoMulligan()
        {
            var swap = _match.OpponentSide.Hand.Where(c => c.cost >= 5).ToList();
            if (swap.Count > 0) _match.Mulligan(OppSide, swap);
        }

        // ---- "enemy played" povijest ----

        private void AddHistory(CardData card)
        {
            _oppHistory.Add(card);
            while (_oppHistory.Count > 6) _oppHistory.RemoveAt(0);
            RenderHistory();
        }

        private void RenderHistory()
        {
            if (_historyRow == null) return;
            if (_histLabel != null) _histLabel.gameObject.SetActive(_oppHistory.Count > 0);
            for (int i = _historyRow.childCount - 1; i >= 0; i--)
                Destroy(_historyRow.GetChild(i).gameObject);
            for (int i = 0; i < _oppHistory.Count; i++)
            {
                var card = _oppHistory[i];
                // mini prikaz bez rules teksta: boja rase + cost + ime; detalji na hover
                var tile = new GameObject($"Hist_{i}", typeof(RectTransform), typeof(Image))
                    .GetComponent<RectTransform>();
                tile.SetParent(_historyRow, false);
                tile.anchorMin = tile.anchorMax = new Vector2(0, 0.5f);
                tile.pivot = new Vector2(0, 0.5f);
                tile.anchoredPosition = new Vector2(i * 74, 0);
                tile.sizeDelta = new Vector2(66, 92);
                tile.GetComponent<Image>().color = CardArtView.RaceColor(card.race);

                // mali art, centriran i malo nize; crta se PRIJE cost badgea da badge
                // ostane citljiv preko kuta slike
                var art = UIFactory.CreatePanel(tile, "Art", new Color(0f, 0f, 0f, 0.35f));
                art.anchorMin = new Vector2(0.5f, 1); art.anchorMax = new Vector2(0.5f, 1);
                art.pivot = new Vector2(0.5f, 1);
                art.anchoredPosition = new Vector2(0, -18);
                art.sizeDelta = new Vector2(36, 36);
                var artImg = art.GetComponent<Image>();
                artImg.raycastTarget = false;
                var sp = CardArtView.LoadArt(card);
                if (sp != null) { artImg.sprite = sp; artImg.preserveAspect = true; artImg.color = Color.white; }

                var costBox = UIFactory.CreatePanel(tile, "Cost", new Color(0.12f, 0.16f, 0.3f, 0.95f));
                costBox.anchorMin = costBox.anchorMax = new Vector2(0, 1);
                costBox.pivot = new Vector2(0, 1);
                costBox.anchoredPosition = new Vector2(2, -2);
                costBox.sizeDelta = new Vector2(22, 22);
                var costTxt = UIFactory.CreateText(costBox, "T", card.cost.ToString(), 13,
                    TextAnchor.MiddleCenter, UIFactory.Ink);
                UIFactory.Stretch(costTxt.rectTransform);

                var nm = UIFactory.CreateText(tile, "Name", card.cardName, 11,
                    TextAnchor.LowerCenter, UIFactory.Ink);
                nm.horizontalOverflow = HorizontalWrapMode.Wrap;
                nm.verticalOverflow = VerticalWrapMode.Truncate;
                nm.resizeTextForBestFit = true;
                nm.resizeTextMinSize = 8;
                nm.resizeTextMaxSize = 11;
                UIFactory.Stretch(nm.rectTransform);
                nm.rectTransform.offsetMin = new Vector2(3, 3);
                nm.rectTransform.offsetMax = new Vector2(-3, -3);

                tile.gameObject.AddComponent<RowHover>().Init(card, _canvas);
            }
        }

        // ---- pause / settings ----

        private void BuildPausePanel(Transform parent)
        {
            // zatamnjenje preko cijelog ekrana: bez njega se moglo klikati po plodi
            // oko panela (igrati karte, napadati) dok su postavke otvorene
            _pauseDim = UIFactory.CreatePanel(parent, "PauseDim", new Color(0f, 0f, 0f, 0.55f));
            UIFactory.Stretch(_pauseDim);
            _pauseDim.GetComponent<Image>().raycastTarget = true;
            _pauseDim.gameObject.SetActive(false);

            _pausePanel = UIFactory.CreatePanel(parent, "PausePanel",
                new Color(0.04f, 0.03f, 0.05f, 0.97f));
            UIFactory.Anchor(_pausePanel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(430, 400));

            var t = UIFactory.CreateText(_pausePanel, "T", Localization.T("bt.settings"), 26,
                TextAnchor.UpperCenter, UIFactory.Ink);
            UIFactory.Anchor(t.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(0.5f, 1), new Vector2(0, -16), new Vector2(300, 34));

            var resume = UIFactory.CreateButton(_pausePanel, "Resume", Localization.T("bt.resume"), 20);
            UIFactory.Anchor(resume.GetComponent<RectTransform>(), new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(0.5f, 1), new Vector2(0, -66), new Vector2(300, 54));
            resume.onClick.AddListener(TogglePause);

            // brzina igre (animacije + bot tempo), pamti se
            var spd = UIFactory.CreateText(_pausePanel, "Spd", Localization.T("bt.gamespeed"), 16,
                TextAnchor.MiddleCenter, new Color(0.8f, 0.85f, 0.9f));
            UIFactory.Anchor(spd.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(0.5f, 1), new Vector2(0, -136), new Vector2(300, 24));
            float[] speeds = { 1f, 1.5f, 2f };
            _speedBtns.Clear();
            for (int i = 0; i < speeds.Length; i++)
            {
                float s = speeds[i];
                var b = UIFactory.CreateButton(_pausePanel, $"Spd{s}", $"x{s}", 17);
                UIFactory.Anchor(b.GetComponent<RectTransform>(), new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                    new Vector2(0.5f, 1), new Vector2(-120 + i * 120, -172), new Vector2(100, 44));
                b.onClick.AddListener(() => SetSpeed(s));
                _speedBtns.Add(b);
            }

            // volume - / +
            _volLabel = UIFactory.CreateText(_pausePanel, "Vol", "", 17,
                TextAnchor.MiddleCenter, new Color(0.85f, 0.9f, 1f));
            UIFactory.Anchor(_volLabel.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(0.5f, 1), new Vector2(0, -238), new Vector2(160, 30));
            var vm = UIFactory.CreateButton(_pausePanel, "Vm", "-", 20);
            UIFactory.Anchor(vm.GetComponent<RectTransform>(), new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(0.5f, 1), new Vector2(-130, -238), new Vector2(54, 42));
            vm.onClick.AddListener(() => NudgeVolume(-0.1f));
            var vp = UIFactory.CreateButton(_pausePanel, "Vp", "+", 20);
            UIFactory.Anchor(vp.GetComponent<RectTransform>(), new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(0.5f, 1), new Vector2(130, -238), new Vector2(54, 42));
            vp.onClick.AddListener(() => NudgeVolume(0.1f));

            var quit = UIFactory.CreateButton(_pausePanel, "Quit",
                MatchConfig.IsOnline ? Localization.T("bt.quit_online") : Localization.T("bt.quittomenu"), 18);
            UIFactory.Anchor(quit.GetComponent<RectTransform>(), new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                new Vector2(0.5f, 0), new Vector2(0, 28), new Vector2(280, 52));
            quit.onClick.AddListener(() =>
            {
                // online: izlazak usred meca je predaja, pa protivnik dobiva pobjedu
                // umjesto da ceka istek timeouta. Rezultat se prikaze kao poraz.
                if (MatchConfig.IsOnline && _match != null && !_match.IsOver)
                {
                    ConcedeOnlineMatch();
                    return;
                }
                Time.timeScale = 1f;
                MatchConfig.ClearStory();
                GameFlow.LoadMainMenu();
            });

            _pausePanel.gameObject.SetActive(false);
        }

        private void TogglePause()
        {
            if (_pausePanel == null) return;
            bool show = !_pausePanel.gameObject.activeSelf;
            _pausePanel.gameObject.SetActive(show);
            if (_pauseDim != null) _pauseDim.gameObject.SetActive(show);
            _paused = show;

            // prava pauza: prije se mec nastavljao iza panela, pa je bot igrao
            // svoj potez dok si bio u postavkama
            Time.timeScale = show ? 0f : PlayerPrefs.GetFloat("game_speed", 1f);

            if (show)
            {
                if (_pauseDim != null) _pauseDim.SetAsLastSibling();
                _pausePanel.SetAsLastSibling(); // iznad svega
                RefreshVolLabel();
                HighlightSpeed();
            }
        }

        private void SetSpeed(float s)
        {
            PlayerPrefs.SetFloat("game_speed", s);
            PlayerPrefs.Save();
            if (!_paused) Time.timeScale = s;   // u pauzi vrijeme ostaje stalo
            HighlightSpeed();
        }

        private void HighlightSpeed()
        {
            float cur = PlayerPrefs.GetFloat("game_speed", 1f);
            float[] speeds = { 1f, 1.5f, 2f };
            for (int i = 0; i < _speedBtns.Count && i < speeds.Length; i++)
                _speedBtns[i].image.color = Mathf.Approximately(cur, speeds[i])
                    ? new Color(0.55f, 0.45f, 0.2f)
                    : new Color(0.25f, 0.22f, 0.28f);
        }

        private void NudgeVolume(float d)
        {
            AudioManager.Instance.SetVolume(AudioManager.Instance.Volume + d);
            RefreshVolLabel();
        }

        private void RefreshVolLabel()
        {
            if (_volLabel != null)
                _volLabel.text = string.Format(Localization.T("bt.volume"), Mathf.RoundToInt(AudioManager.Instance.Volume * 100));
        }

        // Isto kao RunOpponentTurn, ali BEZ pocetnog PassTurn, jer na pocetku meca
        // protivnik je vec na potezu i ne smijemo ga preskociti.
        private IEnumerator RunFirstRemoteTurn()
        {
            _inputLocked = true;
            RefreshAll();
            yield return new WaitForSeconds(0.5f);

            if (!_match.IsOver) yield return RunRemoteTurn();

            yield return new WaitForSeconds(0.3f);
            if (!_match.IsOver)
            {
                _inputLocked = false;
                _match.PassTurn(); // sad je red na nas
            }
        }

        private IEnumerator RunOpponentTurn()
        {
            _inputLocked = true;
            RefreshAll(); // reflect the locked state immediately

            // PassTurn -> BeginTurn fires OnStateChanged -> RefreshAll (no manual call,
            // da drugi refresh ne pregazi protivnikovu draw animaciju)
            _match.PassTurn();
            yield return new WaitForSeconds(0.5f);

            if (!_match.IsOver)
            {
                // online: cekamo poteze pravog igraca umjesto bota
                if (MatchConfig.IsOnline) yield return RunRemoteTurn();
                // tutorial: protivnik igra po scenariju dok skripta traje;
                // kad koraci zavrse, preuzima pravi bot da mec ostane igriv
                else if (MatchConfig.IsTutorial && !TutorialFlow.Finished) TutorialFlow.RunOpponentTurn(_match);
                else yield return _bot.TakeTurn();
            }

            yield return new WaitForSeconds(0.3f);

            if (!_match.IsOver)
            {
                _inputLocked = false;
                // tvoj turn pocinje; OnStateChanged iz BeginTurn osvjezi ekran, a
                // draw zastavica koju je postavio pusti novu card da uleti
                _match.PassTurn();
                // korak se zatvara tek kad protivnik odigra i tvoj potez pocne
                _tutor?.NotifyAction(TutGate.EndTurn);
            }
        }

        private void HandleGameOver(Owner winner)
        {
            _inputLocked = true;
            ShowGameOver(winner == Owner.Player);
        }

        private void ShowGameOver(bool playerWon)
        {
            // story pobjeda upisuje napredak i otkljucava nagradnu card tog nodea
            bool wasStory = MatchConfig.IsStory && MatchConfig.StoryNodeId != null;
            // prijateljski mec preko koda sobe se ne rangira, pa ne dira ELO ni povijest
            bool wasRanked = MatchConfig.IsRanked && !MatchConfig.IsFriendly;
            // tutorial mec ne ulazi u statistiku karijere ni u achievemente
            if (!MatchConfig.IsTutorial && !MatchConfig.IsFriendly)
                PlayerStats.Record(playerWon, MatchConfig.PlayerRace);
            StoryNode node = wasStory ? StoryData.Get(MatchConfig.StoryNodeId) : null;
            string rewardMsg = null;
            if (wasStory && playerWon && node != null)
            {
                bool firstClear = !StoryProgress.IsDone(node.Id);
                StoryProgress.Complete(node);
                if (node.IsFinal)
                {
                    // finale = golden okvir cosmetic za rasu kojom si prosao story
                    GoldenFrames.Unlock(MatchConfig.PlayerRace);
                    rewardMsg = string.Format(Localization.T("bt.reward_campaign"), Localization.RaceName(MatchConfig.PlayerRace));
                }
                else if (firstClear)
                {
                    // svaki level ima FIKSAN reward tvoje rase: borbe 1-14 karte,
                    // Demons 5 drugi heroj (vidi StoryData.RewardFor)
                    var cardReward = StoryData.RewardFor(MatchConfig.PlayerRace, node);
                    if (cardReward != null)
                    {
                        StoryProgress.Unlock(cardReward);
                        rewardMsg = string.Format(Localization.T("bt.reward_unlocked"), cardReward);
                    }
                }
                MapToast.Pending = rewardMsg; // mapa pokaze animirani toast po povratku
            }

            // ranked: elo se mijenja po rezultatu i razlici ratinga; mec ide u povijest
            if (wasRanked)
            {
                int eloBefore = RankedSystem.Elo;
                int delta = RankedSystem.ApplyResult(playerWon, MatchConfig.RankedOpponentRating);
                int elo = RankedSystem.Elo;
                rewardMsg = $"{(delta >= 0 ? "+" : "")}{delta} ELO  →  {elo}  ({RankedSystem.RankName(elo)})";

                MatchHistory.Add(new MatchHistory.Entry
                {
                    Won = playerWon,
                    PlayerRace = MatchConfig.PlayerRace,
                    PlayerHero = MatchConfig.PlayerDeckOverride?.heroName
                                 ?? DeckStorage.Load(MatchConfig.PlayerRace).heroName,
                    PlayerRating = eloBefore,
                    OppRace = MatchConfig.OpponentRace,
                    OppHero = DeckStorage.DefaultHero(MatchConfig.OpponentRace),
                    OppRating = MatchConfig.RankedOpponentRating,
                    Delta = delta,
                    VsHuman = MatchConfig.IsOnline, // online = pravi igrac, inace bot
                });
            }

            // gauntlet: win nastavlja run, loss ga zavrsava i sprema best streak
            if (MatchConfig.IsGauntlet)
            {
                if (playerWon)
                {
                    GauntletRun.Wins++;
                    rewardMsg = string.Format(Localization.T("bt.reward_gauntlet"), GauntletRun.Wins);
                }
                else
                {
                    int best = Mathf.Max(Achievements.BestGauntlet, GauntletRun.Wins);
                    PlayerPrefs.SetInt("gauntlet_best", best);
                    PlayerPrefs.Save();
                    GauntletHistory.Record(GauntletRun.Wins, GauntletRun.Race);
                    GauntletRun.Active = false;
                    rewardMsg = string.Format(Localization.T("bt.reward_runover"), GauntletRun.Wins, best);
                }
            }

            // achievementi se provjeravaju nakon svakog meca; novi -> animirani toast
            var freshAch = MatchConfig.IsTutorial
                ? new List<Achievements.Def>()
                : Achievements.CheckAll();

            var overlay = UIFactory.CreatePanel(_canvas.transform, "GameOver",
                new Color(0, 0, 0, 0.82f));
            UIFactory.Stretch(overlay);
            var cg = overlay.gameObject.AddComponent<CanvasGroup>();
            StartCoroutine(Anim.Fade(cg, 0f, 1f, 0.3f));

            // sav sadrzaj ide u uokvireni prozor, da rezultat, nagrada, lore i gumbi
            // budu jedna cjelina umjesto razbacani po ekranu
            bool hasQuote = wasStory && playerWon && node != null
                            && !string.IsNullOrEmpty(StoryLore.DefeatLine(node));
            var win = UIFactory.CreatePanel(overlay, "ResultWindow",
                new Color(0.08f, 0.07f, 0.10f, 0.98f));
            UIFactory.Anchor(win, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(840, hasQuote ? 580 : 420));
            UIFactory.AddFrame(win);

            var text = UIFactory.CreateText(win, "Result",
                playerWon ? Localization.T("bt.victory") : Localization.T("bt.defeat"), 76, TextAnchor.MiddleCenter,
                playerWon ? new Color(0.5f, 1f, 0.6f) : new Color(1f, 0.4f, 0.4f));
            UIFactory.Anchor(text.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0, -80), new Vector2(780, 110));
            var rEdge = text.gameObject.AddComponent<Outline>();
            rEdge.effectColor = new Color(0, 0, 0, 0.9f);
            rEdge.effectDistance = new Vector2(3, -3);
            StartCoroutine(ResultEntrance(text.rectTransform,
                playerWon ? new Color(0.5f, 1f, 0.6f, 0.8f) : new Color(1f, 0.4f, 0.4f, 0.8f)));

            // online predaja: bez objasnjenja bi nagli kraj izgledao kao greska
            if (_forfeitReason != null)
            {
                var fr = UIFactory.CreateText(win, "ForfeitReason", _forfeitReason, 20,
                    TextAnchor.MiddleCenter, new Color(0.86f, 0.83f, 0.76f));
                fr.horizontalOverflow = HorizontalWrapMode.Wrap;
                UIFactory.Anchor(fr.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(0.5f, 1f), new Vector2(0, -180), new Vector2(760, 30));
            }

            if (rewardMsg != null)
            {
                var rw = UIFactory.CreateText(win, "Reward", rewardMsg, 24,
                    TextAnchor.MiddleCenter, new Color(1f, 0.9f, 0.5f));
                rw.horizontalOverflow = HorizontalWrapMode.Wrap;
                // bez citata stoji na sredini izmedu naslova i gumba; s citatom
                // ide odmah ispod naslova jer citat zauzima sredinu
                UIFactory.Anchor(rw.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(0.5f, 1f), new Vector2(0, hasQuote ? -158 : -215), new Vector2(760, 46));
            }

            // story pobjeda: porazeni protivnik kaze par rijeci
            if (hasQuote)
            {
                var name = UIFactory.CreateText(win, "QuoteName", StoryLore.CommanderName(node), 20,
                    TextAnchor.MiddleCenter, new Color(1f, 0.86f, 0.6f));
                UIFactory.Anchor(name.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(0.5f, 1f), new Vector2(0, -218), new Vector2(760, 30));

                var q = UIFactory.CreateText(win, "DefeatQuote",
                    "\"" + StoryLore.DefeatLine(node) + "\"", 19,
                    TextAnchor.UpperCenter, new Color(0.86f, 0.83f, 0.76f));
                q.horizontalOverflow = HorizontalWrapMode.Wrap;
                q.verticalOverflow = VerticalWrapMode.Overflow;
                q.raycastTarget = false;
                UIFactory.Anchor(q.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(0.5f, 1f), new Vector2(0, -256), new Vector2(720, 150));
            }

            // gumbi se skupljaju pa poredaju u red na dnu prozora
            var buttons = new List<RectTransform>();

            if (wasStory)
            {
                var toMap = UIFactory.CreateButton(win, "ToMap",
                    playerWon ? Localization.T("bt.continue") : Localization.T("bt.backtomap"), 22);
                toMap.onClick.AddListener(() => { MatchConfig.ClearStory(); GameFlow.LoadStoryMap(); });
                buttons.Add(toMap.GetComponent<RectTransform>());

                if (!playerWon)
                {
                    var retry = UIFactory.CreateButton(win, "Retry", Localization.T("bt.retry"), 22);
                    retry.onClick.AddListener(GameFlow.LoadBattle);
                    buttons.Add(retry.GetComponent<RectTransform>());
                }
            }
            else if (MatchConfig.IsGauntlet)
            {
                if (playerWon)
                {
                    var next = UIFactory.CreateButton(win, "Next", Localization.T("bt.nextbattle"), 22);
                    next.onClick.AddListener(() =>
                    {
                        GauntletRun.ConfigureNextMatch();
                        GameFlow.LoadBattle();
                    });
                    buttons.Add(next.GetComponent<RectTransform>());
                }
                else
                {
                    var backG = UIFactory.CreateButton(win, "BackG", Localization.T("bt.backgauntlet"), 20);
                    backG.onClick.AddListener(() => { MatchConfig.ClearStory(); GameFlow.LoadGauntlet(); });
                    buttons.Add(backG.GetComponent<RectTransform>());
                }
            }
            else if (wasRanked)
            {
                var backMp = UIFactory.CreateButton(win, "BackMp", Localization.T("bt.backranked"), 20);
                backMp.onClick.AddListener(() => { MatchConfig.ClearStory(); GameFlow.LoadMultiplayer(); });
                buttons.Add(backMp.GetComponent<RectTransform>());
            }
            else
            {
                var again = UIFactory.CreateButton(win, "Again", Localization.T("bt.newmatch"), 22);
                again.onClick.AddListener(GameFlow.LoadMatchSetup);
                buttons.Add(again.GetComponent<RectTransform>());
            }

            var menu = UIFactory.CreateButton(win, "ToMenu", Localization.T("bt.mainmenu"), 20);
            menu.onClick.AddListener(() => { MatchConfig.ClearStory(); GameFlow.LoadMainMenu(); });
            buttons.Add(menu.GetComponent<RectTransform>());

            // poredaj ih vodoravno, centrirano, uz dno prozora
            const float bw = 250f, bh = 66f, gap = 20f;
            float total = buttons.Count * bw + (buttons.Count - 1) * gap;
            for (int i = 0; i < buttons.Count; i++)
            {
                float x = -total / 2f + bw / 2f + i * (bw + gap);
                UIFactory.Anchor(buttons[i], new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                    new Vector2(0.5f, 0f), new Vector2(x, 56), new Vector2(bw, bh));
            }

            // novo-otkljucani achievementi kao animirani toastevi (max 4 vidljiva)
            for (int i = 0; i < freshAch.Count && i < 4; i++)
                Toast.Show(_canvas.transform, Localization.T("bt.ach_unlocked"), Achievements.TitleOf(freshAch[i]),
                    new Color(1f, 0.85f, 0.35f), i, 2.4f);
        }

        // "YOUR TURN" / "ENEMY TURN": uleti odozgo, kratko stoji, pa nestane
        private void ShowTurnBanner(bool playerTurn)
        {
            if (_canvas == null) return;
            var lbl = UIFactory.CreateText(_canvas.transform, "TurnFlash",
                playerTurn ? Localization.T("bt.yourturn") : Localization.T("bt.enemyturn"), 58, TextAnchor.MiddleCenter,
                playerTurn ? new Color(0.6f, 1f, 0.7f) : new Color(1f, 0.55f, 0.45f));
            UIFactory.Anchor(lbl.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(0, 150), new Vector2(900, 80));
            var edge = lbl.gameObject.AddComponent<Outline>();
            edge.effectColor = new Color(0, 0, 0, 0.85f);
            edge.effectDistance = new Vector2(2, -2);
            lbl.raycastTarget = false;
            StartCoroutine(Anim.Banner(lbl.rectTransform, 0.45f));
        }

        // VICTORY/DEFEAT: naslov padne i slegne se, ispod njega se razvuce crta
        private IEnumerator ResultEntrance(RectTransform rt, Color accent)
        {
            var cg = rt.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            rt.localScale = Vector3.one * 1.7f;
            Vector2 home = rt.anchoredPosition;
            rt.anchoredPosition = home + new Vector2(0, 70);

            rt.DOScale(1f, 0.45f).SetEase(Ease.OutBack).SetLink(rt.gameObject);
            rt.DOAnchorPos(home, 0.45f).SetEase(Ease.OutCubic).SetLink(rt.gameObject);
            cg.DOFade(1f, 0.28f).SetLink(rt.gameObject);
            yield return new WaitForSeconds(0.42f);
            if (rt == null) yield break;

            // crta koja se razvuce ispod naslova
            var line = UIFactory.CreatePanel(rt.parent, "ResultLine", accent);
            UIFactory.Anchor(line, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(0, 3));
            line.GetComponent<Image>().raycastTarget = false;
            StartCoroutine(Anim.Sweep(line, 520f, 0.45f));
            yield return StartCoroutine(Anim.Punch(rt, 0.1f, 0.2f));
        }

        // ---- log ----

        private void AddLog(string msg)
        {
            _log.Add(msg);
            while (_log.Count > 11) _log.RemoveAt(0);
            RenderLog();
        }

        private void RenderLog()
        {
            if (_logText != null) _logText.text = string.Join("\n", _log);
        }

        // pulsiranje bannera dok je tvoj potez + ESC handling
        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (_selectedAttacker != null || _pendingSpell != null) Deselect();
                // ne otvaraj postavke preko game over ekrana ni tijekom mulligana
                else if (_mullPanel == null && (_match == null || !_match.IsOver)) TogglePause();
            }

            if (_turnBanner == null) return;

            // nizak HP: crvena vinjeta pulsira (unscaled da radi i u pauzi/hit-stopu)
            if (_lowHpVignette != null && _match != null)
            {
                int hp = _match.PlayerSide.Hero.CurrentHealth;
                bool danger = !_match.IsOver && hp > 0 && hp <= 12;
                float amp = danger ? 0.22f + Mathf.Sin(Time.unscaledTime * 2.6f) * 0.10f : 0f;
                var c = _lowHpVignette.color;
                _lowHpVignette.color = new Color(c.r, c.g, c.b,
                    Mathf.MoveTowards(c.a, amp, Time.unscaledDeltaTime * 0.9f));
            }

            if (_inputLocked || _match == null || _match.IsOver)
            {
                _turnBanner.transform.localScale = Vector3.one;
                return;
            }
            // unscaled: puls je UI ukras, ne smije ubrzati kad podignes brzinu igre
            float pulse = 1f + Mathf.Sin(Time.unscaledTime * 3f) * 0.03f;
            _turnBanner.transform.localScale = new Vector3(pulse, pulse, 1f);
        }

        private void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() != null) return;
            new GameObject("EventSystem",
                typeof(UnityEngine.EventSystems.EventSystem),
                typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
        }
    }
}
