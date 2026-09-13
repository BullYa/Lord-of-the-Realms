using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace LordOfTheRealms
{
    // Story mapa: 4 regije x 5 battleova + finalni u sredini.
    public class StoryMapController : MonoBehaviour
    {
        private Canvas _canvas;
        private RectTransform _infoPanel;
        private RectTransform _lorePanel;
        private StoryNode _selected;
        private readonly Dictionary<string, RectTransform> _nodeCircles = new();
        private RectTransform _selectedCircle;

        // pozicije krugova po regiji (postotak ekrana), rasireno preko cijele mape
        private static readonly Dictionary<Race, Vector2[]> RegionLayout = new()
        {
            { Race.Orcs, new[]{ new Vector2(0.06f,0.20f), new Vector2(0.12f,0.33f), new Vector2(0.18f,0.27f), new Vector2(0.24f,0.42f), new Vector2(0.30f,0.56f) } },
            { Race.Elves, new[]{ new Vector2(0.29f,0.77f), new Vector2(0.36f,0.90f), new Vector2(0.43f,0.80f), new Vector2(0.50f,0.72f), new Vector2(0.58f,0.87f) } },
            { Race.Humans, new[]{ new Vector2(0.66f,0.90f), new Vector2(0.73f,0.82f), new Vector2(0.79f,0.70f), new Vector2(0.85f,0.58f), new Vector2(0.89f,0.46f) } },
            { Race.Demons, new[]{ new Vector2(0.91f,0.33f), new Vector2(0.86f,0.21f), new Vector2(0.79f,0.12f), new Vector2(0.71f,0.06f), new Vector2(0.62f,0.08f) } },
        };
        private static readonly Vector2 FinalPos = new Vector2(0.47f, 0.45f);

        // pozicije naslova regija
        private static readonly Dictionary<Race, Vector2> LabelPos = new()
        {
            { Race.Orcs,   new Vector2(0.07f, 0.66f) },
            { Race.Elves,  new Vector2(0.25f, 0.95f) },
            { Race.Humans, new Vector2(0.74f, 0.96f) },
            { Race.Demons, new Vector2(0.95f, 0.44f) },
        };

        private void Start()
        {
            AudioManager.EnsureExists();
            AudioManager.Instance.PlayMusic("Music_MainMenu");
            MatchConfig.ClearStory();
            BuildUI();

            // Pitaj samo kad je igrac otvorio Story iz glavnog menija. Povratak iz
            // deck editora ili bitke samo opet pokaze mapu.
            if (GameFlow.StoryEnteredFromMenu)
            {
                GameFlow.StoryEnteredFromMenu = false; // consume it
                ShowSlotPanel(); // izbor jednog od 4 save slota
            }
            else if (!StorySave.Exists)
            {
                // sigurnosna mreza: jos nema savea u aktivnom slotu -> mora birati
                ShowNewGamePanel(firstTime: true);
            }

            // reward iz upravo dobivenog meca -> animirani toast (klizne odozgo)
            var toast = MapToast.Consume();
            if (!string.IsNullOrEmpty(toast))
                Toast.Show(_canvas.transform, Localization.T("sm.reward"), toast, new Color(0.55f, 1f, 0.65f), 0, 2.8f);
        }

        private void BuildUI()
        {
            var canvasGO = new GameObject("StoryCanvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _canvas = canvasGO.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            EnsureEventSystem();

            var bg = UIFactory.CreatePanel(canvasGO.transform, "Background",
                new Color(0.08f, 0.06f, 0.08f, 1f));
            UIFactory.Stretch(bg);

            var title = UIFactory.CreateText(canvasGO.transform, "Title", Localization.T("sm.title"),
                28, TextAnchor.UpperCenter, UIFactory.Ink);
            UIFactory.Anchor(title.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(0.5f, 1), new Vector2(0, -14), new Vector2(900, 40));

            var back = UIFactory.CreateButton(canvasGO.transform, "Back", Localization.T("common.menu"), 18);
            UIFactory.Anchor(back.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(0, 1), new Vector2(20, -16), new Vector2(120, 42));
            back.onClick.AddListener(GameFlow.LoadMainMenu);

            // naslovi regija + krugovi + crtice puta
            foreach (var race in StoryData.RegionOrder)
            {
                var pts = RegionLayout[race];
                var label = UIFactory.CreateText(canvasGO.transform, $"Lbl_{race}", Localization.RaceName(race).ToUpper(),
                    20, TextAnchor.MiddleCenter, CardArtView.RaceColor(race) * 1.6f);
                PlacePct(label.rectTransform, LabelPos[race], new Vector2(180, 30));

                var nodes = StoryData.RegionNodes(race).ToList();
                for (int i = 0; i < nodes.Count; i++)
                {
                    if (i > 0) Dashes(canvasGO.transform, pts[i - 1], pts[i]);
                    MakeNodeCircle(canvasGO.transform, nodes[i], pts[i], 62);
                }
            }

            // crtice IZMEDU regija: put ide od rase do rase pa do finala
            var order = StoryData.RegionOrder.ToList();
            for (int r = 0; r < order.Count - 1; r++)
                Dashes(canvasGO.transform, RegionLayout[order[r]][4], RegionLayout[order[r + 1]][0]);
            Dashes(canvasGO.transform, RegionLayout[order[order.Count - 1]][4], FinalPos);

            // zavrsna bitka, veliki krug u sredini
            var final = StoryData.Get("Final");
            MakeNodeCircle(canvasGO.transform, final, FinalPos, 110);

            // info panel (skriven dok se ne klikne node)
            _infoPanel = UIFactory.CreatePanel(canvasGO.transform, "InfoPanel",
                new Color(0.05f, 0.04f, 0.06f, 0.97f));
            UIFactory.Anchor(_infoPanel, new Vector2(0, 0.5f), new Vector2(0, 0.5f),
                new Vector2(0, 0.5f), new Vector2(18, 0), new Vector2(330, 640));
            _infoPanel.gameObject.SetActive(false);

            // centralni lore prozor (skriven dok se ne klikne level)
            _lorePanel = UIFactory.CreatePanel(canvasGO.transform, "LorePanel",
                new Color(0.05f, 0.04f, 0.06f, 0.97f));
            UIFactory.Anchor(_lorePanel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(60, 0), new Vector2(560, 340));
            _lorePanel.gameObject.SetActive(false);

            // playing-as banner + deck edit button, top right
            _saveLabel = UIFactory.CreateText(canvasGO.transform, "SaveLbl", "", 16,
                TextAnchor.UpperRight, new Color(0.85f, 0.9f, 1f));
            UIFactory.Anchor(_saveLabel.rectTransform, new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(1, 1), new Vector2(-160, -20), new Vector2(400, 26));
            RefreshSaveLabel();

            var deckBtn = UIFactory.CreateButton(canvasGO.transform, "DeckBtn", Localization.T("mp.editdeck"), 16);
            UIFactory.Anchor(deckBtn.GetComponent<RectTransform>(), new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(1, 1), new Vector2(-16, -14), new Vector2(130, 40));
            deckBtn.onClick.AddListener(() =>
            {
                CollectionContext.DeckEdit = true;
                CollectionContext.EditRace = StorySave.PlayerRace;
                CollectionContext.ReturnTo = CollectionContext.Return.StoryMap;
                GameFlow.LoadCollection();
            });
        }

        private Text _saveLabel;
        private Race _pickRace = Race.Humans;
        private int _pickHeroIdx;
        private RectTransform _entryPanel;

        private void RefreshSaveLabel()
        {
            if (_saveLabel != null && StorySave.Exists)
                _saveLabel.text = $"Slot {StorySlots.Active + 1} · {Localization.RaceName(StorySave.PlayerRace)} · {StorySave.HeroName} · {StoryProgress.CompletedCount()}/21";
        }

        // full-screen overlay: 4 save slota, CONTINUE postojeci playthrough ili
        // NEW GAME u slotu; svaki slot pamti svoju rasu/heroja/napredak
        private void ShowSlotPanel()
        {
            var panel = MakeOverlay("SlotPanel");
            var t = UIFactory.CreateText(panel, "T", Localization.T("sm.choose_campaign"), 34,
                TextAnchor.MiddleCenter, UIFactory.Ink);
            UIFactory.Anchor(t.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(0.5f, 1), new Vector2(0, -60), new Vector2(700, 50));

            for (int i = 0; i < StorySlots.Count; i++)
            {
                int slot = i;
                bool exists = StorySave.ExistsIn(slot);
                float y = -150 - i * 120;

                var row = UIFactory.CreatePanel(panel, $"Slot{i}", new Color(0.09f, 0.08f, 0.12f, 1f));
                UIFactory.Anchor(row, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                    new Vector2(0.5f, 1), new Vector2(0, y), new Vector2(920, 100));
                var edge = row.gameObject.AddComponent<Outline>();
                edge.effectColor = slot == StorySlots.Active
                    ? new Color(0.85f, 0.7f, 0.25f, 0.9f) : new Color(0.35f, 0.3f, 0.4f, 0.8f);
                edge.effectDistance = new Vector2(2, -2);

                // naslov slota (gornji redak) + detalji save-a (donji redak).
                // Anchor centrira rect na zadani y, pa razmak mora biti veci od pola
                // visine oba retka, inace se tekstovi preklope.
                var slotTitle = UIFactory.CreateText(row, "ST", $"SLOT {slot + 1}", 20,
                    TextAnchor.MiddleLeft, exists ? new Color(0.95f, 0.92f, 0.86f) : new Color(0.6f, 0.6f, 0.65f));
                UIFactory.Anchor(slotTitle.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f),
                    new Vector2(0, 0.5f), new Vector2(22, 18), new Vector2(430, 26));

                string info = exists
                    ? string.Format(Localization.T("sm.slot_info"), Localization.RaceName(StorySave.RaceIn(slot)), StorySave.HeroIn(slot), StoryProgress.CompletedCountFor(slot))
                    : Localization.T("sm.slot_empty");
                var lbl = UIFactory.CreateText(row, "L", info, 15,
                    TextAnchor.MiddleLeft, exists ? new Color(0.75f, 0.82f, 0.92f) : new Color(0.5f, 0.5f, 0.55f));
                UIFactory.Anchor(lbl.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f),
                    new Vector2(0, 0.5f), new Vector2(22, -16), new Vector2(430, 24));

                // gumbi u desnoj polovici, jednako razmaknuti (NEW GAME uvijek skroz desno)
                var ng = UIFactory.CreateButton(row, "New", Localization.T("sm.newgame"), 15);
                UIFactory.Anchor(ng.GetComponent<RectTransform>(), new Vector2(1, 0.5f), new Vector2(1, 0.5f),
                    new Vector2(1, 0.5f), new Vector2(-16, 0), new Vector2(140, 48));
                ng.onClick.AddListener(() =>
                {
                    StorySlots.Active = slot;
                    Destroy(panel.gameObject);
                    ShowNewGamePanel(firstTime: !StorySave.ExistsIn(slot));
                });

                if (exists)
                {
                    var cont = UIFactory.CreateButton(row, "Cont", Localization.T("bt.continue"), 15);
                    UIFactory.Anchor(cont.GetComponent<RectTransform>(), new Vector2(1, 0.5f), new Vector2(1, 0.5f),
                        new Vector2(1, 0.5f), new Vector2(-168, 0), new Vector2(140, 48));
                    cont.onClick.AddListener(() =>
                    {
                        StorySlots.Active = slot;
                        GameFlow.LoadStoryMap(); // reload da mapa cita napredak tog slota
                    });

                    // DELETE s dvoklik potvrdom ("SURE?") da se save ne obrise slucajno
                    bool armed = false;
                    var del = UIFactory.CreateButton(row, "Del", Localization.T("sm.delete"), 14);
                    UIFactory.Anchor(del.GetComponent<RectTransform>(), new Vector2(1, 0.5f), new Vector2(1, 0.5f),
                        new Vector2(1, 0.5f), new Vector2(-320, 0), new Vector2(120, 48));
                    del.GetComponent<Image>().color = new Color(0.5f, 0.18f, 0.18f);
                    del.onClick.AddListener(() =>
                    {
                        if (!armed)
                        {
                            armed = true;
                            var dt = del.GetComponentInChildren<Text>();
                            if (dt != null) dt.text = Localization.T("sm.sure");
                            del.GetComponent<Image>().color = new Color(0.85f, 0.25f, 0.2f);
                            return;
                        }
                        StorySave.DeleteSlot(slot);
                        Destroy(panel.gameObject);
                        ShowSlotPanel(); // rebuild s osvjezenim stanjem slotova
                    });
                }
            }

            var back = UIFactory.CreateButton(panel, "Back", Localization.T("common.menu"), 16);
            UIFactory.Anchor(back.GetComponent<RectTransform>(), new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                new Vector2(0.5f, 0), new Vector2(0, 24), new Vector2(160, 44));
            back.onClick.AddListener(GameFlow.LoadMainMenu);
        }

        // (stari ShowContinuePanel zamijenjen slot pickerom gore)

        // full-screen overlay: izaberi rasu i heroja za kampanju
        private void ShowNewGamePanel(bool firstTime)
        {
            _pickRace = Race.Humans;
            _pickHeroIdx = 0;
            _entryPanel = MakeOverlay("NewGamePanel");
            RebuildNewGamePanel(firstTime);
        }

        private void RebuildNewGamePanel(bool firstTime)
        {
            for (int i = _entryPanel.childCount - 1; i >= 0; i--)
                Destroy(_entryPanel.GetChild(i).gameObject);

            var t = UIFactory.CreateText(_entryPanel, "T",
                firstTime ? Localization.T("sm.choose_realm") : Localization.T("sm.new_campaign"), 34,
                TextAnchor.MiddleCenter, UIFactory.Ink);
            UIFactory.Anchor(t.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(0.5f, 1), new Vector2(0, -70), new Vector2(700, 50));

            if (!firstTime)
            {
                var warn = UIFactory.CreateText(_entryPanel, "W",
                    Localization.T("sm.warn"), 15,
                    TextAnchor.MiddleCenter, new Color(1f, 0.7f, 0.6f));
                UIFactory.Anchor(warn.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                    new Vector2(0.5f, 1), new Vector2(0, -114), new Vector2(800, 40));
            }

            // race selector
            var raceLbl = UIFactory.CreateText(_entryPanel, "RL", string.Format(Localization.T("sm.race_pick"), _pickRace), 24,
                TextAnchor.MiddleCenter, CardArtView.RaceColor(_pickRace) * 1.7f);
            UIFactory.Anchor(raceLbl.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(0.5f, 1), new Vector2(0, -170), new Vector2(400, 40));
            AddArrow(_entryPanel, new Vector2(-240, -170), "<", () => { CyclePickRace(-1, firstTime); });
            AddArrow(_entryPanel, new Vector2(240, -170), ">", () => { CyclePickRace(1, firstTime); });

            // izbor heroja: prava hero card, s hoverom
            var heroes = HeroesOf(_pickRace);
            if (_pickHeroIdx >= heroes.Count) _pickHeroIdx = 0;
            if (heroes.Count > 0)
            {
                var hero = heroes[_pickHeroIdx];
                var tile = new GameObject("HeroTile", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
                tile.SetParent(_entryPanel, false);
                tile.anchorMin = new Vector2(0.5f, 1); tile.anchorMax = new Vector2(0.5f, 1);
                tile.pivot = new Vector2(0.5f, 1);
                tile.anchoredPosition = new Vector2(0, -230);
                tile.sizeDelta = new Vector2(170, 235);
                CardArtView.Paint(tile, hero);
                tile.gameObject.AddComponent<RowHover>().Init(hero, _canvas);

                // strelice samo ako ima vise od jednog otkljucanog heroja
                if (heroes.Count > 1)
                {
                    AddArrow(_entryPanel, new Vector2(-150, -340), "<",
                        () => { _pickHeroIdx = (_pickHeroIdx - 1 + heroes.Count) % heroes.Count; RebuildNewGamePanel(firstTime); });
                    AddArrow(_entryPanel, new Vector2(150, -340), ">",
                        () => { _pickHeroIdx = (_pickHeroIdx + 1) % heroes.Count; RebuildNewGamePanel(firstTime); });
                }
            }

            var start = UIFactory.CreateButton(_entryPanel, "Start", Localization.T("sm.begin"), 24);
            UIFactory.Anchor(start.GetComponent<RectTransform>(), new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                new Vector2(0.5f, 0), new Vector2(0, 40), new Vector2(280, 66));
            start.onClick.AddListener(() =>
            {
                var hs = HeroesOf(_pickRace);
                string heroName = hs.Count > 0 ? hs[Mathf.Clamp(_pickHeroIdx, 0, hs.Count - 1)].cardName
                                               : DeckStorage.DefaultHero(_pickRace);
                StorySave.NewGame(_pickRace, heroName);
                RefreshSaveLabel();
                Destroy(_entryPanel.gameObject);
                GameFlow.LoadStoryMap(); // reload so node states reflect the fresh save
            });

            if (!firstTime)
            {
                var cancel = UIFactory.CreateButton(_entryPanel, "Cancel", Localization.T("sm.cancel"), 18);
                UIFactory.Anchor(cancel.GetComponent<RectTransform>(), new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                    new Vector2(0.5f, 0), new Vector2(0, 10), new Vector2(180, 26));
                cancel.onClick.AddListener(() => Destroy(_entryPanel.gameObject));
            }
        }

        private void CyclePickRace(int dir, bool firstTime)
        {
            _pickRace = (Race)(((int)_pickRace + dir + 4) % 4);
            _pickHeroIdx = 0;
            RebuildNewGamePanel(firstTime);
        }

        // Heroji koje mozes izabrati na pocetku kampanje: zadani heroj uvijek, DRUGI
        // heroj tek kad je otkljucan kroz igranje (Demons 5 reward). Unlockovi su
        // globalni, pa jednom osvojen heroj ostaje dostupan u svim novim kampanjama.
        private List<HeroCardData> HeroesOf(Race race)
            => CardLibrary.All.OfType<HeroCardData>()
                .Where(h => h.race == race &&
                       (h.cardName == DeckStorage.DefaultHero(race) ||
                        StoryProgress.IsCardUnlocked(race, h.cardName)))
                .OrderBy(h => h.cardName).ToList();

        private void AddArrow(RectTransform parent, Vector2 pos, string label, System.Action onClick)
        {
            var b = UIFactory.CreateButton(parent, "Arrow" + pos.x + label, label, 22);
            UIFactory.Anchor(b.GetComponent<RectTransform>(), new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(0.5f, 1), pos, new Vector2(48, 40));
            b.onClick.AddListener(() => onClick());
        }

        private RectTransform MakeOverlay(string name)
        {
            var p = UIFactory.CreatePanel(_canvas.transform, name, new Color(0.03f, 0.02f, 0.04f, 0.96f));
            UIFactory.Stretch(p);
            return p;
        }

        // isprekidana linija (crtice) izmedu dvije tocke mape
        private void Dashes(Transform parent, Vector2 aPct, Vector2 bPct)
        {
            // udaljenost u "ekranskim" jedinicama da broj crtica prati duljinu
            Vector2 aScr = new Vector2(aPct.x * 1920f, aPct.y * 1080f);
            Vector2 bScr = new Vector2(bPct.x * 1920f, bPct.y * 1080f);
            float dist = Vector2.Distance(aScr, bScr);
            float angle = Mathf.Atan2(bScr.y - aScr.y, bScr.x - aScr.x) * Mathf.Rad2Deg;
            int n = Mathf.Clamp(Mathf.RoundToInt(dist / 34f), 2, 24);

            for (int i = 1; i < n; i++)
            {
                float t = i / (float)n;
                var p = Vector2.Lerp(aPct, bPct, t);
                var dash = UIFactory.CreatePanel(parent, "dash",
                    new Color(0.55f, 0.35f, 0.25f, 0.7f));
                PlacePct(dash, p, new Vector2(14, 4));
                dash.localRotation = Quaternion.Euler(0, 0, angle);
                dash.GetComponent<Image>().raycastTarget = false;
                // odmah iznad pozadine, da crtice nikad ne idu preko krugova
                dash.SetSiblingIndex(1);
            }
        }

        private void MakeNodeCircle(Transform parent, StoryNode node, Vector2 pct, float size)
        {
            // level koji otkljucava DRUGOG HEROJA se istice zlatnim halo prstenom
            // i "HERO" tagom (halo je zaseban objekt IZA kruga, da se ne sudara s
            // Outline highlightom kod selektiranja)
            bool heroNode = !node.IsFinal && StorySave.Exists &&
                StoryData.RewardFor(StorySave.PlayerRace, node) == StoryData.SecondHero(StorySave.PlayerRace);
            if (heroNode)
            {
                var halo = new GameObject($"Halo_{node.Id}", typeof(RectTransform), typeof(Image))
                    .GetComponent<RectTransform>();
                halo.SetParent(parent, false);
                PlacePct(halo, pct, new Vector2(size + 18, size + 18));
                var hImg = halo.GetComponent<Image>();
                hImg.sprite = CircleSprite();
                hImg.color = new Color(0.95f, 0.78f, 0.25f, 0.9f); // golden, kao golden frame
                hImg.raycastTarget = false;

                var tag = UIFactory.CreateText(parent, $"HeroTag_{node.Id}", Localization.T("sm.hero_tag"), 13,
                    TextAnchor.MiddleCenter, new Color(0.95f, 0.78f, 0.25f));
                PlacePct(tag.rectTransform, pct + new Vector2(0, -0.052f), new Vector2(90, 20));
                tag.raycastTarget = false;
            }

            var go = new GameObject($"Node_{node.Id}",
                typeof(RectTransform), typeof(Image), typeof(Button)).GetComponent<RectTransform>();
            go.SetParent(parent, false);
            PlacePct(go, pct, new Vector2(size, size));

            var img = go.GetComponent<Image>();
            // crtamo vlastiti okrugli sprite, stari builtin UI/Skin/Knob.psd je maknut
            // u novijem Unityju, pa krug radimo iz koda
            img.sprite = CircleSprite();
            img.type = Image.Type.Simple;
            img.preserveAspect = true;

            bool done = StoryProgress.IsDone(node.Id);
            bool open = StoryProgress.IsAvailable(node);
            img.color = done ? new Color(0.35f, 0.75f, 0.4f)
                     : open ? CardArtView.RaceColor(node.EnemyRace) * 1.3f
                     : new Color(0.28f, 0.28f, 0.3f);

            string lbl = node.IsFinal ? "★" : node.IndexInRegion.ToString();
            var num = UIFactory.CreateText(go, "Num", lbl, node.IsFinal ? 40 : 24,
                TextAnchor.MiddleCenter, Color.black);
            UIFactory.Stretch(num.rectTransform);

            // kvacica na zavrsenim levelima (uz zelenu boju), odmah se vidi sto je claimed
            if (done && !node.IsFinal)
            {
                var check = UIFactory.CreateText(go, "Done", "\u2713", 16,
                    TextAnchor.LowerRight, new Color(0.05f, 0.25f, 0.08f));
                UIFactory.Stretch(check.rectTransform);
                check.rectTransform.offsetMax = new Vector2(-8, 0);
                check.rectTransform.offsetMin = new Vector2(0, 4);
                check.raycastTarget = false;
            }

            go.GetComponent<Button>().onClick.AddListener(() => SelectNode(node));
            _nodeCircles[node.Id] = go;

            // krugovi iskacu jedan za drugim dok se mapa otvara
            StartCoroutine(StaggeredPop(go, _nodePopIndex++));
        }

        private int _nodePopIndex;
        private System.Collections.IEnumerator StaggeredPop(RectTransform rt, int order)
        {
            rt.localScale = Vector3.zero;
            yield return new WaitForSeconds(0.03f * order);
            yield return Anim.PopScale(rt);
        }

        // Ispunjeni krug s mekim rubom, generira se jednom i koristi za svaki node.
        private static Sprite _circleSprite;
        private static Sprite CircleSprite()
        {
            if (_circleSprite != null) return _circleSprite;

            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            float r = size / 2f;
            var center = new Vector2(r, r);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                    // 1 unutra, pada na 0 tocno na rubu za glatki obris
                    float a = Mathf.Clamp01((r - d) / 1.5f);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            _circleSprite = Sprite.Create(tex, new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f), 100f);
            return _circleSprite;
        }

        private void PlacePct(RectTransform rt, Vector2 pct, Vector2 size)
        {
            rt.anchorMin = pct; rt.anchorMax = pct;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = size;
        }

        // ---- info panel ----

        private void SelectNode(StoryNode node)
        {
            _selected = node;
            _infoPanel.gameObject.SetActive(true);
            HighlightCircle(node);
            ShowLoreWindow(node);

            // sadrzaj panela se svaki put gradi ispocetka
            for (int i = _infoPanel.childCount - 1; i >= 0; i--)
                Destroy(_infoPanel.GetChild(i).gameObject);

            bool done = StoryProgress.IsDone(node.Id);
            bool open = StoryProgress.IsAvailable(node);

            var header = UIFactory.CreateText(_infoPanel, "H",
                node.IsFinal ? Localization.T("sm.final_battle") : string.Format(Localization.T("sm.battle_n"), node.IndexInRegion), 24,
                TextAnchor.UpperCenter, UIFactory.Ink);
            Top(header.rectTransform, -10, 30);

            // ime protivnika iz price (StoryLore)
            var who = UIFactory.CreateText(_infoPanel, "W",
                StoryLore.CommanderName(node), 15,
                TextAnchor.UpperCenter, new Color(1f, 0.86f, 0.55f));
            Top(who.rectTransform, -42, 22);

            var enemy = UIFactory.CreateText(_infoPanel, "E",
                string.Format(Localization.T("sm.enemy_line"), Localization.RaceName(node.EnemyRace), (int)node.Difficulty, Localization.DiffName(node.Difficulty)),
                16, TextAnchor.UpperCenter, new Color(0.9f, 0.8f, 0.7f));
            Top(enemy.rectTransform, -66, 22);

            // mini card protivnickog heroja, hover daje puni tooltip
            var heroData = CardLibrary.All.FirstOrDefault(c => c.cardName == node.EnemyHeroName);
            var heroLbl = UIFactory.CreateText(_infoPanel, "HL", Localization.T("sm.enemy_hero"), 14,
                TextAnchor.UpperCenter, new Color(0.7f, 0.7f, 0.8f));
            Top(heroLbl.rectTransform, -92, 18);
            if (heroData != null)
            {
                var tile = new GameObject("HeroTile", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
                tile.SetParent(_infoPanel, false);
                tile.anchorMin = new Vector2(0.5f, 1); tile.anchorMax = new Vector2(0.5f, 1);
                tile.pivot = new Vector2(0.5f, 1);
                tile.anchoredPosition = new Vector2(0, -112);
                tile.sizeDelta = new Vector2(150, 210);
                CardArtView.Paint(tile, heroData);
                tile.gameObject.AddComponent<RowHover>().Init(heroData, _canvas);
            }

            // reward: FIKSNA karta ovog levela za tvoju rasu; finale daje golden okvir
            float rewardTop = -336;
            string nodeReward = node.IsFinal ? null : StoryData.RewardFor(StorySave.PlayerRace, node);
            string rwText = node.IsFinal ? Localization.T("sm.reward_golden")
                          : nodeReward == null ? Localization.T("sm.reward_none")
                          : done ? Localization.T("sm.reward_claimed")
                          : Localization.T("sm.reward_onwin");
            var rw = UIFactory.CreateText(_infoPanel, "RW", rwText, 14,
                TextAnchor.UpperCenter, new Color(0.75f, 0.9f, 0.7f));
            Top(rw.rectTransform, rewardTop, 20);
            if (!node.IsFinal && !done && nodeReward != null)
            {
                var rewardData = CardLibrary.All.FirstOrDefault(c => c.cardName == nodeReward);
                if (rewardData != null)
                {
                    var tile = new GameObject("RewardTile", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
                    tile.SetParent(_infoPanel, false);
                    tile.anchorMin = new Vector2(0.5f, 1); tile.anchorMax = new Vector2(0.5f, 1);
                    tile.pivot = new Vector2(0.5f, 1);
                    tile.anchoredPosition = new Vector2(0, rewardTop - 24);
                    tile.sizeDelta = new Vector2(150, 210);
                    CardArtView.Paint(tile, rewardData);
                    tile.gameObject.AddComponent<RowHover>().Init(rewardData, _canvas);
                }
            }

            var fight = UIFactory.CreateButton(_infoPanel, "Fight",
                done ? Localization.T("sm.replay") : open ? Localization.T("sm.fight") : Localization.T("col.locked"), 20);
            var frt = fight.GetComponent<RectTransform>();
            frt.anchorMin = new Vector2(0.5f, 0); frt.anchorMax = new Vector2(0.5f, 0);
            frt.pivot = new Vector2(0.5f, 0);
            frt.anchoredPosition = new Vector2(0, 12); frt.sizeDelta = new Vector2(200, 44);
            fight.interactable = open;
            fight.onClick.AddListener(StartSelectedBattle);
        }

        // highlight: selektirani krug se poveca i dobije bijeli rub
        private void HighlightCircle(StoryNode node)
        {
            if (_selectedCircle != null)
            {
                _selectedCircle.localScale = Vector3.one;
                var oldOutline = _selectedCircle.GetComponent<Outline>();
                if (oldOutline != null) oldOutline.enabled = false;
            }
            if (!_nodeCircles.TryGetValue(node.Id, out var circle) || circle == null) return;
            _selectedCircle = circle;
            circle.localScale = Vector3.one * 1.22f;
            var outline = circle.GetComponent<Outline>();
            if (outline == null) outline = circle.gameObject.AddComponent<Outline>();
            outline.enabled = true;
            outline.effectColor = new Color(1f, 1f, 1f, 0.9f);
            outline.effectDistance = new Vector2(3, -3);
        }

        // centralni prozor s narativom za level
        private void ShowLoreWindow(StoryNode node)
        {
            _lorePanel.gameObject.SetActive(true);
            for (int i = _lorePanel.childCount - 1; i >= 0; i--)
                Destroy(_lorePanel.GetChild(i).gameObject);

            // naslov = ime protivnika, podnaslov = regija i broj borbe
            var t = UIFactory.CreateText(_lorePanel, "T", StoryLore.CommanderName(node), 21,
                TextAnchor.UpperCenter, CardArtView.RaceColor(node.EnemyRace) * 1.6f);
            Top(t.rectTransform, -12, 28);

            string sub = node.IsFinal ? Localization.T("sm.lore_final")
                : string.Format(Localization.T("sm.lore_sub"), Localization.RaceName(node.EnemyRace).ToUpper(), node.IndexInRegion);
            var st = UIFactory.CreateText(_lorePanel, "ST", sub, 13,
                TextAnchor.UpperCenter, new Color(0.65f, 0.65f, 0.7f));
            Top(st.rectTransform, -40, 18);

            var body = UIFactory.CreateText(_lorePanel, "B", StoryLore.For(node), 16,
                TextAnchor.UpperLeft, new Color(0.88f, 0.85f, 0.8f));
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
            var brt = body.rectTransform;
            brt.anchorMin = new Vector2(0, 0); brt.anchorMax = new Vector2(1, 1);
            brt.offsetMin = new Vector2(24, 20); brt.offsetMax = new Vector2(-24, -66);

            var close = UIFactory.CreateButton(_lorePanel, "X", "X", 16);
            UIFactory.Anchor(close.GetComponent<RectTransform>(), new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(1, 1), new Vector2(-6, -6), new Vector2(34, 34));
            close.onClick.AddListener(CloseNodePanels);
        }

        // X na lore prozoru zatvara i bocni info panel i makne highlight s kruga
        private void CloseNodePanels()
        {
            _lorePanel.gameObject.SetActive(false);
            _infoPanel.gameObject.SetActive(false);
            if (_selectedCircle != null)
            {
                _selectedCircle.localScale = Vector3.one;
                var o = _selectedCircle.GetComponent<Outline>();
                if (o != null) o.enabled = false;
                _selectedCircle = null;
            }
            _selected = null;
        }

        private void Top(RectTransform rt, float y, float h)
        {
            rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1);
            rt.anchoredPosition = new Vector2(0, y);
            rt.sizeDelta = new Vector2(-16, h);
        }

        private void Bottom(RectTransform rt, float y, float h)
        {
            rt.anchorMin = new Vector2(0, 0); rt.anchorMax = new Vector2(1, 0);
            rt.pivot = new Vector2(0.5f, 0);
            rt.anchoredPosition = new Vector2(0, y);
            rt.sizeDelta = new Vector2(-16, h);
        }


        private void StartSelectedBattle()
        {
            if (_selected == null || !StoryProgress.IsAvailable(_selected)) return;
            _lorePanel.gameObject.SetActive(false);

            // protivnik vozi zadani deck svoje rase, sa zamijenjenim herojem nodea
            var enemyDeck = DeckStorage.Default(_selected.EnemyRace);
            enemyDeck.heroName = _selected.EnemyHeroName;

            MatchConfig.PlayerRace = StorySave.PlayerRace;
            MatchConfig.OpponentRace = _selected.EnemyRace;
            MatchConfig.Difficulty = _selected.Difficulty;
            MatchConfig.Seed = 0;
            MatchConfig.IsStory = true;
            MatchConfig.StoryNodeId = _selected.Id;
            MatchConfig.OpponentDeckOverride = enemyDeck;

            GameFlow.LoadBattle();
        }

        private void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() != null) return;
            new GameObject("EventSystem",
                typeof(UnityEngine.EventSystems.EventSystem),
                typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
        }

        // ESC vraca korak natrag; ako je otvoren panel, prvo njega zatvori
        private void Update()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null || !kb.escapeKey.wasPressedThisFrame) return;
            if (_infoPanel != null) { Destroy(_infoPanel.gameObject); _infoPanel = null; return; }
            GameFlow.LoadMainMenu();
        }
    }
}
