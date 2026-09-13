using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace LordOfTheRealms
{
    // Govori collection sceni kako se otvara: kao obicni pregled (iz glavnog
    // menija) ili kao deck editor za jednu rasu (iz story mape, rasa tog
    // aktivnog savea). Multiplayer ce ovo kasnije postavljati na isti nacin.
    public static class CollectionContext
    {
        public enum Return { Menu, Multiplayer, StoryMap, Gauntlet }

        public static bool DeckEdit = false;
        public static Race EditRace = Race.Humans;
        // AllCards: prikazi/dozvoli sve karte bez obzira na unlockove.
        // (trenutno nitko ne pali, svi modovi postuju story unlockove)
        public static bool AllCards = false;
        // BackToMultiplayer: koristi MP deck slot set (mp_/mp_s2_/mp_s3_) umjesto story;
        // ranked i gauntlet dijele MP deck po rasi.
        public static bool BackToMultiplayer = false;
        // kamo vodi BACK gumb iz editora/vievera
        public static Return ReturnTo = Return.Menu;
    }

    // Dva ekrana u jednom:
    //  VIEW mode: pregled svih karata svih rasa (heroji ukljuceni, prikazani kao
    //             prave karte). Hover daje pravila i lore. Ovdje se ne ureduje.
    //  EDIT mode: deck builder za jednu rasu, otvoren iz moda u kojem si
    //             (story). Pool lijevo, tvoj deck desno, heroj kao prava karta
    //             s < > za promjenu. 30 karata + 1 heroj, max 4 unit / 2 spell kopije.
    //             Story/gauntlet edit ima 3 deck slota; jedan je aktivan za bitke.
    public class CollectionController : MonoBehaviour
    {
        private const int DeckSize = 30;
        // max kopija po karti nije vise fiksno 3, dolazi iz DeckLists (unit 4 / spell 2),
        // vidi MaxCopiesFor().

        private Canvas _canvas;
        private bool _editMode;
        private Race _race;
        private SavedDeck _deck;

        private RectTransform _poolContent, _deckContent, _heroHolder;
        private Text _deckCountText;
        private RectTransform _curveHolder;

        private List<CardData> _libraryForRace = new();
        private List<HeroCardData> _heroesForRace = new();
        private int _heroIndex;

        // ---- viewer: napredak collectiona, sort i filter ----
        private enum SortMode { Cost, Name, Type }
        private SortMode _sortMode = SortMode.Cost;
        private bool _hideLocked;
        private Text _progressText;
        private RectTransform _progressFill;
        private Button[] _sortButtons;
        private Button _hideBtn;
        private readonly Dictionary<Race, Button> _raceTabs = new();
        private const float ProgBarWidth = 600f;

        private static readonly Vector2 CardSize = new Vector2(150, 210);

        private void Start()
        {
            AudioManager.EnsureExists();
            AudioManager.Instance.PlayMusic("Music_MainMenu");
            _editMode = CollectionContext.DeckEdit;
            _race = _editMode ? CollectionContext.EditRace : Race.Humans;
            _allCards = CollectionContext.AllCards;
            _backToMp = CollectionContext.BackToMultiplayer;
            _returnTo = CollectionContext.ReturnTo;
            CollectionContext.DeckEdit = false; // one-shot flags
            CollectionContext.AllCards = false;
            CollectionContext.BackToMultiplayer = false;
            CollectionContext.ReturnTo = CollectionContext.Return.Menu;
            _slotIndex = DeckStorage.ActiveSlotIndex(_race, _backToMp); // krece od aktivnog slota (story ili MP)
            BuildUI();
            LoadRace(_race);
        }

        private bool _allCards;
        private bool _backToMp;
        private CollectionContext.Return _returnTo;
        // story/gauntlet: 3 deck slota (DeckStorage.Slots); koji se trenutno editira
        private int _slotIndex;
        private Button[] _slotButtons;
        // slot iz odgovarajuceg seta (MP ili story) po trenutnom indeksu
        private string Slot => DeckStorage.SlotsFor(_backToMp)[_slotIndex];

        // otkljucana karta? (story unlockovi vrijede i u multiplayeru)
        private bool Unlocked(string cardName)
            => _allCards || StoryProgress.IsCardUnlocked(_race, cardName);

        private void BuildUI()
        {
            var canvasGO = new GameObject("CollectionCanvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _canvas = canvasGO.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            EnsureEventSystem();

            var bg = UIFactory.CreatePanel(canvasGO.transform, "Background",
                new Color(0.07f, 0.06f, 0.09f, 1f));
            UIFactory.Stretch(bg);

            var title = UIFactory.CreateText(canvasGO.transform, "Title",
                _editMode ? string.Format(Localization.T("col.deck_title"), Localization.RaceName(_race).ToUpper()) : Localization.T("col.title"),
                30, TextAnchor.UpperCenter, UIFactory.Ink);
            UIFactory.Anchor(title.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(0.5f, 1), new Vector2(0, -12), new Vector2(700, 40));

            // BACK vodi tamo odakle se doslo (menu / free play / gauntlet / story mapa)
            string backLbl = _returnTo switch
            {
                CollectionContext.Return.Multiplayer => Localization.T("menu.freeplay"),
                CollectionContext.Return.Gauntlet => Localization.T("col.gauntlet"),
                CollectionContext.Return.StoryMap => Localization.T("col.storymap"),
                _ => Localization.T("common.menu"),
            };
            var back = UIFactory.CreateButton(canvasGO.transform, "Back", backLbl, 16);
            UIFactory.Anchor(back.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(0, 1), new Vector2(20, -16), new Vector2(140, 42));
            back.onClick.AddListener(GoBack);

            if (!_editMode)
            {
                // tabovi rasa imaju smisla samo u pregledniku
                var races = new[] { Race.Orcs, Race.Elves, Race.Humans, Race.Demons };
                for (int i = 0; i < races.Length; i++)
                {
                    var r = races[i];
                    var tab = UIFactory.CreateButton(canvasGO.transform, $"Tab_{r}", Localization.RaceName(r), 17);
                    UIFactory.Anchor(tab.GetComponent<RectTransform>(), new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                        new Vector2(0.5f, 1), new Vector2(-330 + i * 220, -58), new Vector2(200, 42));
                    tab.onClick.AddListener(() => LoadRace(r));
                    _raceTabs[r] = tab;
                }

                BuildViewerBar(canvasGO.transform);

                // jedan veliki grid preko cijelog ekrana (nizi vrh: iznad je bar)
                _poolContent = MakeGridArea(canvasGO.transform, "Pool",
                    new Vector2(0, 0), new Vector2(1, 1),
                    new Vector2(30, 24), new Vector2(-30, -158));
                return;
            }

            // ---- edit mode layout ----

            // slot za hero card sa strelicama < >, gore u sredini
            _heroHolder = new GameObject("HeroHolder", typeof(RectTransform)).GetComponent<RectTransform>();
            _heroHolder.SetParent(canvasGO.transform, false);
            UIFactory.Anchor(_heroHolder, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(0.5f, 1), new Vector2(0, -54), new Vector2(150, 200));

            var hPrev = UIFactory.CreateButton(canvasGO.transform, "HPrev", "<", 22);
            UIFactory.Anchor(hPrev.GetComponent<RectTransform>(), new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(0.5f, 1), new Vector2(-120, -130), new Vector2(44, 40));
            hPrev.onClick.AddListener(() => CycleHero(-1));

            var hNext = UIFactory.CreateButton(canvasGO.transform, "HNext", ">", 22);
            UIFactory.Anchor(hNext.GetComponent<RectTransform>(), new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(0.5f, 1), new Vector2(120, -130), new Vector2(44, 40));
            hNext.onClick.AddListener(() => CycleHero(1));

            // slot barica: i story/gauntlet i multiplayer imaju 3 slota
            BuildSlotBar(canvasGO.transform);

            var poolHeader = UIFactory.CreateText(canvasGO.transform, "PoolHeader", Localization.T("col.allcards"),
                18, TextAnchor.UpperLeft, new Color(0.7f, 0.8f, 0.9f));
            UIFactory.Anchor(poolHeader.rectTransform, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(0, 1), new Vector2(40, -262), new Vector2(500, 26));

            _deckCountText = UIFactory.CreateText(canvasGO.transform, "DeckHeader", "",
                18, TextAnchor.UpperRight, new Color(0.9f, 0.8f, 0.7f));
            UIFactory.Anchor(_deckCountText.rectTransform, new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(1, 1), new Vector2(-40, -262), new Vector2(500, 26));

            // krivulja cijena: 30 karata je tesko procijeniti napamet, a stupci
            // odmah pokazu je li spil pretezak. Stoji IZNAD naslova spila (-262),
            // jer oznake ispod stupaca idu jos malo nize od okvira.
            _curveHolder = UIFactory.CreatePanel(canvasGO.transform, "Curve", new Color(0, 0, 0, 0));
            UIFactory.Anchor(_curveHolder, new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(1, 1), new Vector2(-40, -190), new Vector2(340, 34));
            _curveHolder.GetComponent<Image>().raycastTarget = false;

            _poolContent = MakeGridArea(canvasGO.transform, "Pool",
                new Vector2(0, 0), new Vector2(0.52f, 1),
                new Vector2(30, 78), new Vector2(-14, -294));
            _deckContent = MakeGridArea(canvasGO.transform, "Deck",
                new Vector2(0.52f, 0), new Vector2(1, 1),
                new Vector2(14, 78), new Vector2(-30, -294));

            var save = UIFactory.CreateButton(canvasGO.transform, "Save", Localization.T("col.savedeck"), 20);
            UIFactory.Anchor(save.GetComponent<RectTransform>(), new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                new Vector2(0.5f, 0), new Vector2(-120, 12), new Vector2(200, 52));
            save.onClick.AddListener(SaveDeck);

            _resetBtn = UIFactory.CreateButton(canvasGO.transform, "Reset", Localization.T("col.reset"), 20);
            UIFactory.Anchor(_resetBtn.GetComponent<RectTransform>(), new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                new Vector2(0.5f, 0), new Vector2(120, 12), new Vector2(200, 52));
            // reset brise cijeli spil, pa trazi drugi klik za potvrdu (isto kao
            // brisanje save slota na story mapi)
            _resetBtn.onClick.AddListener(() =>
            {
                if (!_resetArmed)
                {
                    _resetArmed = true;
                    SetResetLabel(Localization.T("sm.sure"));
                    Flash(Localization.T("col.reset_confirm"));
                    return;
                }
                ResetDeck();
            });
        }

        private RectTransform MakeGridArea(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offMin, Vector2 offMax)
        {
            var viewport = new GameObject(name + "Viewport",
                typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect)).GetComponent<RectTransform>();
            viewport.SetParent(parent, false);
            viewport.anchorMin = anchorMin; viewport.anchorMax = anchorMax;
            viewport.offsetMin = offMin; viewport.offsetMax = offMax;
            viewport.GetComponent<Image>().color = new Color(0.03f, 0.03f, 0.05f, 0.6f);

            var content = new GameObject(name + "Content", typeof(RectTransform),
                typeof(GridLayoutGroup), typeof(ContentSizeFitter)).GetComponent<RectTransform>();
            content.SetParent(viewport, false);
            content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.anchoredPosition = Vector2.zero;
            // svjezi RectTransform ima sizeDelta (100,100), postavi na nulu inace
            // content ispadne siri od viewporta i karte iscure sa strane
            content.sizeDelta = Vector2.zero;

            var grid = content.GetComponent<GridLayoutGroup>();
            grid.cellSize = CardSize;
            grid.spacing = new Vector2(12, 12);
            grid.padding = new RectOffset(14, 14, 14, 14);
            grid.childAlignment = TextAnchor.UpperCenter;

            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = viewport.GetComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 34;

            return content;
        }

        private void LoadRace(Race race)
        {
            _race = race;

            var lib = CardLibrary.All.Where(c => c.race == race).ToList();

            if (_editMode)
            {
                _deck = DeckStorage.Load(race, Slot);
                _libraryForRace = lib.Where(c => c.Category != CardCategory.Hero)
                                     .OrderBy(c => c.cost).ThenBy(c => c.cardName).ToList();
                _heroesForRace = lib.OfType<HeroCardData>()
                                    .Where(h => _allCards || StoryProgress.IsCardUnlocked(race, h.cardName))
                                    .OrderBy(h => h.cardName).ToList();
                _heroIndex = Mathf.Max(0, _heroesForRace.FindIndex(h => h.cardName == _deck.heroName));
                RefreshHeroTile();
                RefreshPool();
                RefreshDeck();
                RefreshSlotButtons();
            }
            else
            {
                // viewer: cijela biblioteka rase (heroji ukljuceni); redoslijed i
                // filtriranje rjesava RefreshViewer prema sortu/toggleu
                _libraryForRace = lib;
                RefreshTabs();
                RefreshViewer();
            }
        }

        // ---- edit mode: hero as an actual card ----

        private void CycleHero(int dir)
        {
            if (_heroesForRace.Count == 0) return;
            _heroIndex = (_heroIndex + dir + _heroesForRace.Count) % _heroesForRace.Count;
            _deck.heroName = _heroesForRace[_heroIndex].cardName;
            RefreshHeroTile();
        }

        private void RefreshHeroTile()
        {
            for (int i = _heroHolder.childCount - 1; i >= 0; i--)
                Destroy(_heroHolder.GetChild(i).gameObject);
            if (_heroesForRace.Count == 0) return;

            var hero = _heroesForRace[Mathf.Clamp(_heroIndex, 0, _heroesForRace.Count - 1)];
            _deck.heroName = hero.cardName;

            var tile = new GameObject("HeroTile", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            tile.SetParent(_heroHolder, false);
            UIFactory.Stretch(tile);
            CardArtView.Paint(tile, hero);
            tile.gameObject.AddComponent<RowHover>().Init(hero, _canvas);
        }

        // ---- grids ----

        private int CopiesInDeck(string name)
        {
            var e = _deck.cards.FirstOrDefault(c => c.name == name);
            return e.name == name ? e.copies : 0;
        }

        private void RefreshViewer()
        {
            ClearContent(_poolContent);

            // napredak se racuna na CIJELOJ biblioteci rase, ne na filtriranom prikazu
            int total = _libraryForRace.Count;
            int unlocked = _libraryForRace.Count(c => StoryProgress.IsCardUnlocked(_race, c.cardName));
            RefreshProgress(unlocked, total);

            IEnumerable<CardData> shown = _libraryForRace;
            if (_hideLocked)
                shown = shown.Where(c => StoryProgress.IsCardUnlocked(_race, c.cardName));
            shown = _sortMode switch
            {
                SortMode.Name => shown.OrderBy(c => c.cardName),
                SortMode.Type => shown.OrderBy(c => TypeRank(c)).ThenBy(c => c.cost).ThenBy(c => c.cardName),
                _ => shown.OrderBy(c => c.cost).ThenBy(c => c.cardName),
            };

            foreach (var card in shown)
            {
                bool locked = !StoryProgress.IsCardUnlocked(_race, card.cardName);
                MakeCardTile(_poolContent, card,
                    badge: locked ? Localization.T("col.locked") : "",
                    greyed: locked,
                    onClick: null); // viewer: look, don't touch
            }
        }

        // heroji prvo, pa uniti, pa spellovi
        private static int TypeRank(CardData c) => c.Category switch
        {
            CardCategory.Hero => 0,
            CardCategory.Unit => 1,
            _ => 2,
        };

        // ---- viewer bar: napredak + sort + filter ----

        private void BuildViewerBar(Transform parent)
        {
            _progressText = UIFactory.CreateText(parent, "Prog", "", 16,
                TextAnchor.MiddleCenter, new Color(0.9f, 0.85f, 0.7f));
            UIFactory.Anchor(_progressText.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(0.5f, 1), new Vector2(0, -106), new Vector2(ProgBarWidth, 22));

            var barBg = UIFactory.CreatePanel(parent, "ProgBg", new Color(0.05f, 0.05f, 0.07f, 0.95f));
            UIFactory.Anchor(barBg, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(0.5f, 1), new Vector2(0, -130), new Vector2(ProgBarWidth, 12));

            _progressFill = UIFactory.CreatePanel(barBg, "Fill", new Color(0.85f, 0.7f, 0.25f, 1f));
            _progressFill.anchorMin = new Vector2(0, 0);
            _progressFill.anchorMax = new Vector2(0, 1);   // visina se rasteze, sirina = sizeDelta.x
            _progressFill.pivot = new Vector2(0, 0.5f);
            _progressFill.anchoredPosition = Vector2.zero;
            _progressFill.sizeDelta = new Vector2(0, 0);
            _progressFill.GetComponent<Image>().raycastTarget = false;

            // HIDE/SHOW LOCKED lijevo, ispod BACK gumba
            _hideBtn = UIFactory.CreateButton(parent, "HideLocked", Localization.T("col.hide_locked"), 13);
            UIFactory.Anchor(_hideBtn.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(0, 1), new Vector2(20, -106), new Vector2(170, 36));
            _hideBtn.onClick.AddListener(() =>
            {
                _hideLocked = !_hideLocked;
                RefreshHideBtn();
                RefreshViewer();
            });
            RefreshHideBtn();

            // SORT BY: COST / NAME / TYPE desno
            var sortLbl = UIFactory.CreateText(parent, "SortLbl", Localization.T("col.sortby"), 13,
                TextAnchor.MiddleRight, new Color(0.7f, 0.75f, 0.85f));
            UIFactory.Anchor(sortLbl.rectTransform, new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(1, 1), new Vector2(-306, -106), new Vector2(90, 36));

            var modes = new[] { SortMode.Cost, SortMode.Name, SortMode.Type };
            _sortButtons = new Button[modes.Length];
            for (int i = 0; i < modes.Length; i++)
            {
                var m = modes[i];
                var b = UIFactory.CreateButton(parent, $"Sort_{m}", SortName(m), 13);
                UIFactory.Anchor(b.GetComponent<RectTransform>(), new Vector2(1, 1), new Vector2(1, 1),
                    new Vector2(1, 1), new Vector2(-208 + i * 94, -106), new Vector2(88, 36));
                b.onClick.AddListener(() =>
                {
                    _sortMode = m;
                    RefreshSortButtons();
                    RefreshViewer();
                });
                _sortButtons[i] = b;
            }
            RefreshSortButtons();
        }

        private void RefreshHideBtn()
        {
            if (_hideBtn == null) return;
            var t = _hideBtn.GetComponentInChildren<Text>();
            if (t != null) t.text = _hideLocked ? Localization.T("col.show_locked") : Localization.T("col.hide_locked");
            _hideBtn.GetComponent<Image>().color =
                _hideLocked ? new Color(0.85f, 0.7f, 0.25f) : UIFactory.Accent;
        }

        private void RefreshSortButtons()
        {
            if (_sortButtons == null) return;
            var modes = new[] { SortMode.Cost, SortMode.Name, SortMode.Type };
            for (int i = 0; i < _sortButtons.Length; i++)
                _sortButtons[i].GetComponent<Image>().color =
                    modes[i] == _sortMode ? new Color(0.85f, 0.7f, 0.25f) : UIFactory.Accent;
        }

        // vrati se onamo odakle je collection otvoren
        private void GoBack()
        {
            switch (_returnTo)
            {
                case CollectionContext.Return.Multiplayer: GameFlow.LoadMultiplayer(); break;
                case CollectionContext.Return.Gauntlet: GameFlow.LoadGauntlet(); break;
                case CollectionContext.Return.StoryMap: GameFlow.LoadStoryMap(); break;
                default: GameFlow.LoadMainMenu(); break;
            }
        }

        // ESC vraca korak natrag, isto kamo i BACK gumb
        private void Update()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) GoBack();
        }

        // lokalizirani naziv sort moda za gumb
        private static string SortName(SortMode m) => m switch
        {
            SortMode.Name => Localization.T("col.sort_name"),
            SortMode.Type => Localization.T("col.sort_type"),
            _ => Localization.T("col.sort_cost"),
        };

        // aktivna rasa u tabovima svijetli
        private void RefreshTabs()
        {
            foreach (var kv in _raceTabs)
                kv.Value.GetComponent<Image>().color = kv.Key == _race
                    ? CardArtView.RaceColor(_race) * 1.5f
                    : UIFactory.Accent;
        }

        private void RefreshProgress(int unlocked, int total)
        {
            if (_progressText == null) return;
            float pct = total > 0 ? (float)unlocked / total : 0f;
            _progressText.text = string.Format(Localization.T("col.progress"), Localization.RaceName(_race).ToUpper(), unlocked, total, (pct * 100f).ToString("0"));
            _progressText.color = unlocked >= total
                ? new Color(0.6f, 1f, 0.65f) : new Color(0.9f, 0.85f, 0.7f);
            if (_progressFill != null)
            {
                _progressFill.sizeDelta = new Vector2(ProgBarWidth * pct, 0);
                _progressFill.GetComponent<Image>().color = unlocked >= total
                    ? new Color(0.45f, 0.9f, 0.5f) : new Color(0.85f, 0.7f, 0.25f);
            }
        }

        private void RefreshPool()
        {
            ClearContent(_poolContent);
            foreach (var card in _libraryForRace)
            {
                bool locked = !Unlocked(card.cardName);
                int inDeck = CopiesInDeck(card.cardName);
                bool maxed = inDeck >= MaxCopiesFor(card.cardName);
                string badge = locked ? Localization.T("col.locked") : (inDeck > 0 ? string.Format(Localization.T("col.indeck"), inDeck) : "");
                MakeCardTile(_poolContent, card, badge,
                    greyed: maxed || locked,
                    onClick: () =>
                    {
                        if (locked) { Flash(Localization.T("col.flash_locked")); return; }
                        AddCard(card.cardName);
                    });
            }
        }

        private void RefreshDeck()
        {
            ClearContent(_deckContent);
            int total = _deck.cards.Sum(c => c.copies);
            _deckCountText.text = string.Format(Localization.T("col.yourdeck"), total, DeckSize);
            _deckCountText.color = total == DeckSize ? new Color(0.6f, 1f, 0.6f)
                                                     : new Color(1f, 0.8f, 0.5f);
            RefreshCurve();
            DisarmReset();   // svaka promjena spila ponisti cekanje na potvrdu

            foreach (var cc in _deck.cards.OrderBy(c => CostOf(c.name)).ThenBy(c => c.name))
            {
                var card = _libraryForRace.FirstOrDefault(c => c.cardName == cc.name);
                if (card == null) continue;
                MakeCardTile(_deckContent, card, badge: $"x{cc.copies}",
                    greyed: false, onClick: () => RemoveCard(card.cardName));
            }
        }

        private void MakeCardTile(RectTransform parent, CardData card, string badge,
            bool greyed, System.Action onClick)
        {
            var tile = new GameObject($"Card_{card.cardName}",
                typeof(RectTransform), typeof(Image), typeof(Button)).GetComponent<RectTransform>();
            tile.SetParent(parent, false);

            CardArtView.Paint(tile, card, greyed);

            if (!string.IsNullOrEmpty(badge))
            {
                var bd = UIFactory.CreateText(tile, "Badge", badge, 13, TextAnchor.UpperRight,
                    greyed ? new Color(1f, 0.55f, 0.55f) : new Color(0.7f, 1f, 0.7f));
                var brt = bd.rectTransform;
                brt.anchorMin = new Vector2(1, 1); brt.anchorMax = new Vector2(1, 1);
                brt.pivot = new Vector2(1, 1);
                // drain karte imaju crveni box gore-desno, badge ide ispod njega
                brt.anchoredPosition = new Vector2(-6, card.maxPowerCost > 0 ? -36 : -6);
                brt.sizeDelta = new Vector2(90, 22);
            }

            if (onClick != null)
                tile.GetComponent<Button>().onClick.AddListener(() => onClick());

            tile.gameObject.AddComponent<RowHover>().Init(card, _canvas);
        }

        // ---- deck edits ----

        private void AddCard(string name)
        {
            int total = _deck.cards.Sum(c => c.copies);
            if (total >= DeckSize) { Flash(Localization.T("col.flash_full")); return; }

            var existing = _deck.cards.FindIndex(c => c.name == name);
            if (existing >= 0)
            {
                int lim = MaxCopiesFor(name);
                if (_deck.cards[existing].copies >= lim) { Flash(string.Format(Localization.T("col.flash_maxcopies"), lim)); return; }
                var cc = _deck.cards[existing];
                cc.copies++;
                _deck.cards[existing] = cc;
            }
            else
            {
                _deck.cards.Add(new SavedDeck.CardCount { name = name, copies = 1 });
            }
            RefreshPool();
            RefreshDeck();
        }

        private void RemoveCard(string name)
        {
            var idx = _deck.cards.FindIndex(c => c.name == name);
            if (idx < 0) return;
            var cc = _deck.cards[idx];
            cc.copies--;
            if (cc.copies <= 0) _deck.cards.RemoveAt(idx);
            else _deck.cards[idx] = cc;
            RefreshPool();
            RefreshDeck();
        }

        private void SaveDeck()
        {
            int total = _deck.cards.Sum(c => c.copies);
            if (total != DeckSize) { Flash(string.Format(Localization.T("col.flash_exact"), DeckSize, total)); return; }
            if (string.IsNullOrEmpty(_deck.heroName)) { Flash(Localization.T("col.flash_pickhero")); return; }
            // stari deckovi (spremljeni dok je sve bilo otvoreno) mogu sadrzavati
            // zakljucane karte: ne daj spremiti dok se ne izbace
            var lockedInDeck = _deck.cards.Where(c => !Unlocked(c.name)).Select(c => c.name).ToList();
            if (lockedInDeck.Count > 0)
            {
                Flash(string.Format(Localization.T("col.flash_lockedcards"), string.Join(", ", lockedInDeck)));
                return;
            }
            DeckStorage.Save(_race, _deck, Slot);
            Flash(Localization.T("col.flash_saved"));
        }

        // Stupci po cijeni (1..7+), visina prema najvecoj skupini. Crta se iz koda
        // kao i sve ostalo, bez ijednog asseta.
        private void RefreshCurve()
        {
            if (_curveHolder == null) return;
            for (int i = _curveHolder.childCount - 1; i >= 0; i--)
                Destroy(_curveHolder.GetChild(i).gameObject);

            const int Buckets = 7;   // zadnji je "7 i vise"
            var counts = new int[Buckets];
            foreach (var cc in _deck.cards)
            {
                int cost = CostOf(cc.name);
                int b = Mathf.Clamp(cost - 1, 0, Buckets - 1);
                counts[b] += cc.copies;
            }

            int max = 1;
            foreach (var c in counts) if (c > max) max = c;

            const float colW = 40f, gap = 6f, maxH = 24f;
            float startX = -(Buckets * colW + (Buckets - 1) * gap) / 2f + colW / 2f;

            for (int i = 0; i < Buckets; i++)
            {
                float x = startX + i * (colW + gap);
                float h = Mathf.Max(2f, maxH * counts[i] / max);

                var bar = UIFactory.CreatePanel(_curveHolder, $"Bar{i}",
                    counts[i] > 0 ? new Color(0.72f, 0.58f, 0.3f, 0.9f)
                                  : new Color(0.3f, 0.28f, 0.33f, 0.6f));
                UIFactory.Anchor(bar, new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                    new Vector2(0.5f, 0), new Vector2(x, 10), new Vector2(colW - 8, h));
                bar.GetComponent<Image>().raycastTarget = false;

                var lbl = UIFactory.CreateText(_curveHolder, $"L{i}",
                    i == Buckets - 1 ? $"{i + 1}+" : (i + 1).ToString(), 11,
                    TextAnchor.LowerCenter, new Color(0.7f, 0.67f, 0.72f));
                UIFactory.Anchor(lbl.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                    new Vector2(0.5f, 0), new Vector2(x, -4), new Vector2(colW, 14));
                lbl.raycastTarget = false;
            }
        }

        // reset trazi dvoklik. Potvrda se ponisti cim igrac ucini bilo sto drugo,
        // jer bi inace gumb ostao naoruzan i zbunio pri sljedecem kliku.
        private bool _resetArmed;
        private Button _resetBtn;

        private void SetResetLabel(string s)
        {
            if (_resetBtn == null) return;
            var t = _resetBtn.GetComponentInChildren<Text>();
            if (t != null) t.text = s;
        }

        private void DisarmReset()
        {
            if (!_resetArmed) return;
            _resetArmed = false;
            SetResetLabel(Localization.T("col.reset"));
        }

        private void ResetDeck()
        {
            DeckStorage.Reset(_race, Slot);
            _deck = DeckStorage.Default(_race);
            _heroIndex = Mathf.Max(0, _heroesForRace.FindIndex(h => h.cardName == _deck.heroName));
            RefreshHeroTile();
            RefreshPool();
            RefreshDeck();
            Flash(Localization.T("col.flash_reset"));
        }

        // ---- deck slotovi (story/gauntlet) ----

        // 3 gumba za slot + SET ACTIVE, gore lijevo ispod BACK gumba
        private void BuildSlotBar(Transform parent)
        {
            var lbl = UIFactory.CreateText(parent, "SlotLabel", Localization.T("col.deckslot"), 14,
                TextAnchor.UpperLeft, new Color(0.7f, 0.8f, 0.9f));
            UIFactory.Anchor(lbl.rectTransform, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(0, 1), new Vector2(24, -72), new Vector2(180, 22));

            _slotButtons = new Button[DeckStorage.SlotsFor(_backToMp).Length];
            for (int i = 0; i < _slotButtons.Length; i++)
            {
                int idx = i;
                var b = UIFactory.CreateButton(parent, $"Slot{i + 1}", (i + 1).ToString(), 16);
                UIFactory.Anchor(b.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(0, 1),
                    new Vector2(0, 1), new Vector2(24 + i * 52, -100), new Vector2(46, 38));
                b.onClick.AddListener(() => SelectSlot(idx));
                _slotButtons[i] = b;
            }

            var setA = UIFactory.CreateButton(parent, "SetActive", Localization.T("col.setactive"), 13);
            UIFactory.Anchor(setA.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(0, 1), new Vector2(24, -146), new Vector2(150, 38));
            setA.onClick.AddListener(SetActiveSlot);

            RefreshSlotButtons();
        }

        // editirani slot = zlatno; aktivni (ide u bitke) dobiva zvjezdicu u labeli
        private void RefreshSlotButtons()
        {
            if (_slotButtons == null) return;
            int active = DeckStorage.ActiveSlotIndex(_race, _backToMp);
            for (int i = 0; i < _slotButtons.Length; i++)
            {
                _slotButtons[i].GetComponent<Image>().color =
                    i == _slotIndex ? new Color(0.85f, 0.7f, 0.25f) : UIFactory.Accent;
                var t = _slotButtons[i].GetComponentInChildren<Text>();
                if (t != null) t.text = i == active ? $"{i + 1}*" : (i + 1).ToString();
            }
        }

        // prebaci editiranje na drugi slot (nespremljene izmjene se odbacuju)
        private void SelectSlot(int idx)
        {
            _slotIndex = Mathf.Clamp(idx, 0, DeckStorage.SlotsFor(_backToMp).Length - 1);
            _deck = DeckStorage.Load(_race, Slot);
            _heroIndex = Mathf.Max(0, _heroesForRace.FindIndex(h => h.cardName == _deck.heroName));
            RefreshHeroTile();
            RefreshPool();
            RefreshDeck();
            RefreshSlotButtons();
        }

        // oznaci trenutno editirani slot kao aktivni za bitke
        private void SetActiveSlot()
        {
            DeckStorage.SetActiveSlot(_race, _slotIndex, _backToMp);
            RefreshSlotButtons();
            Flash(string.Format(Localization.T("col.flash_slotactive"), _slotIndex + 1));
        }

        // max kopija ovisi o tipu karte: unit 4, spell 2 (DeckLists)
        private int MaxCopiesFor(string name)
        {
            var c = _libraryForRace.FirstOrDefault(x => x.cardName == name);
            return c != null && c.Category == CardCategory.Spell
                ? DeckLists.MaxSpellCopies
                : DeckLists.MaxUnitCopies;
        }

        // ---- misc ----

        private int CostOf(string name)
        {
            var c = _libraryForRace.FirstOrDefault(x => x.cardName == name);
            return c != null ? c.cost : 99;
        }

        private void ClearContent(RectTransform content)
        {
            for (int i = content.childCount - 1; i >= 0; i--)
                Destroy(content.GetChild(i).gameObject);
        }

        private Text _flash;
        private void Flash(string msg)
        {
            if (_flash == null)
            {
                _flash = UIFactory.CreateText(_canvas.transform, "Flash", "", 22,
                    TextAnchor.MiddleCenter, new Color(1f, 0.9f, 0.5f));
                UIFactory.Anchor(_flash.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                    new Vector2(0.5f, 0), new Vector2(0, 68), new Vector2(700, 32));
            }
            _flash.text = msg;
            CancelInvoke(nameof(ClearFlash));
            Invoke(nameof(ClearFlash), 2f);
        }
        private void ClearFlash() { if (_flash != null) _flash.text = ""; }

        private void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() != null) return;
            new GameObject("EventSystem",
                typeof(UnityEngine.EventSystems.EventSystem),
                typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
        }
    }

    // Prikazuje tooltip card dok je pokazivac nad njezinom plocicom.
    public class RowHover : MonoBehaviour,
        UnityEngine.EventSystems.IPointerEnterHandler,
        UnityEngine.EventSystems.IPointerExitHandler
    {
        private CardData _card;
        private Canvas _canvas;
        public void Init(CardData card, Canvas canvas) { _card = card; _canvas = canvas; }
        public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData e)
            => CardTooltip.Ensure(_canvas).Show(_card);
        public void OnPointerExit(UnityEngine.EventSystems.PointerEventData e)
            => CardTooltip.Ensure(_canvas).Hide();
    }
}
