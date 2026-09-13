using UnityEngine;
using UnityEngine.UI;

namespace LordOfTheRealms
{
    // Tutorial ekran iz glavnog menija: visestranicni pregled pravila (POWER, potez,
    // jedinice, spellovi/zamke, keywordi, junaci). Zadnja stranica nudi probni mec
    // protiv laganog bota. Ne dira GameMatch ni BattleController.
    public class TutorialController : MonoBehaviour
    {
        // svaka stranica su dva loc kljuca: naslov (.h) i tekst (.b)
        private static readonly string[] Pages =
            { "tut.p1", "tut.p2", "tut.p3", "tut.p4", "tut.p5", "tut.p6", "tut.p7", "tut.p8" };

        private int _page;
        private Transform _root;
        private RectTransform _panel;

        private static readonly Color Ink = new Color(0.96f, 0.95f, 0.92f);
        private static readonly Color Body = new Color(0.85f, 0.84f, 0.88f);

        private void Start()
        {
            AudioManager.EnsureExists();
            AudioManager.Instance.PlayMusic("Music_MainMenu");
            BuildUI();
        }

        private void BuildUI()
        {
            var canvasGO = new GameObject("TutorialCanvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            EnsureEventSystem();
            _root = canvasGO.transform;

            var bg = UIFactory.CreatePanel(_root, "Background", new Color(0.06f, 0.05f, 0.07f, 1f));
            UIFactory.Stretch(bg);
            UIFactory.AddVignette(_root);

            var title = UIFactory.CreateText(_root, "Title", Localization.T("menu.howtoplay"), 46,
                TextAnchor.MiddleCenter, Ink);
            UIFactory.Anchor(title.rectTransform,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0, -56), new Vector2(1200, 70));

            // glavni put: skriptirani mec koji te vodi kroz mehanike
            var startBtn = UIFactory.CreateButton(_root, "StartTut", Localization.T("tut.start"), 26);
            UIFactory.Anchor(startBtn.GetComponent<RectTransform>(),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0, -150), new Vector2(440, 72));
            startBtn.onClick.AddListener(StartScriptedTutorial);

            // stalni izlaz gore-lijevo
            var back = UIFactory.CreateButton(_root, "Back", Localization.T("common.menu"), 18);
            UIFactory.Anchor(back.GetComponent<RectTransform>(),
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(20, -20), new Vector2(140, 48));
            back.onClick.AddListener(GameFlow.LoadMainMenu);

            // kartica s pravilima: sve na vrsnim kotvama da raspored drzi na svakoj rezoluciji
            var rulesLbl = UIFactory.CreateText(_root, "RulesLbl", Localization.T("tut.rules"), 19,
                TextAnchor.MiddleCenter, new Color(0.72f, 0.68f, 0.76f));
            UIFactory.Anchor(rulesLbl.rectTransform,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0, -232), new Vector2(600, 26));

            _panel = UIFactory.CreatePanel(_root, "Panel", new Color(0.09f, 0.08f, 0.11f, 0.98f));
            UIFactory.Anchor(_panel, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0, -268), new Vector2(1000, 420));

            RenderPage();
        }

        // pokreni skriptirani tutorial mec (Demoni vs Ljudi, fiksne karte)
        private void StartScriptedTutorial()
        {
            MatchConfig.ClearStory();
            MatchConfig.IsTutorial = true;
            // Demoni imaju draw/ramp/lifesteal, a ljudski Templar nosi Shield
            // koji se uci napadom na njega; zajedno pokrivaju sve mehanike
            MatchConfig.PlayerRace = Race.Demons;
            MatchConfig.OpponentRace = Race.Humans;
            MatchConfig.Difficulty = BotDifficulty.VeryEasy;
            MatchConfig.Seed = 0;
            GameFlow.LoadBattle();
        }

        // panel se gradi ispocetka na svaku promjenu stranice
        private void RenderPage()
        {
            for (int i = _panel.childCount - 1; i >= 0; i--)
                Destroy(_panel.GetChild(i).gameObject);

            UIFactory.AddFrame(_panel);

            string key = Pages[_page];

            var head = UIFactory.CreateText(_panel, "Head", Localization.T(key + ".h"), 34,
                TextAnchor.MiddleCenter, new Color(1f, 0.88f, 0.55f));
            UIFactory.Anchor(head.rectTransform,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0, -34), new Vector2(880, 44));

            var body = UIFactory.CreateText(_panel, "Body", Localization.T(key + ".b"), 21,
                TextAnchor.UpperCenter, Body);
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
            body.verticalOverflow = VerticalWrapMode.Overflow;
            UIFactory.Anchor(body.rectTransform,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0, -92), new Vector2(840, 190));

            // indikator stranice
            var dots = UIFactory.CreateText(_panel, "Dots", (_page + 1) + " / " + Pages.Length, 16,
                TextAnchor.MiddleCenter, new Color(0.65f, 0.62f, 0.7f));
            UIFactory.Anchor(dots.rectTransform,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f), new Vector2(0, 24), new Vector2(200, 26));

            if (_page > 0)
            {
                var prev = UIFactory.CreateButton(_panel, "Prev", Localization.T("common.back"), 20);
                UIFactory.Anchor(prev.GetComponent<RectTransform>(),
                    new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f),
                    new Vector2(40, 60), new Vector2(220, 62));
                prev.onClick.AddListener(() => { _page--; RenderPage(); });
            }

            bool last = _page == Pages.Length - 1;
            if (!last)
            {
                var next = UIFactory.CreateButton(_panel, "Next", Localization.T("tut.next"), 20);
                UIFactory.Anchor(next.GetComponent<RectTransform>(),
                    new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
                    new Vector2(-40, 60), new Vector2(220, 62));
                next.onClick.AddListener(() => { _page++; RenderPage(); });
            }
            else
            {
                var play = UIFactory.CreateButton(_panel, "Practice", Localization.T("tut.practice"), 20);
                UIFactory.Anchor(play.GetComponent<RectTransform>(),
                    new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
                    new Vector2(-40, 60), new Vector2(300, 62));
                play.onClick.AddListener(StartPractice);
            }
        }

        // probni mec: obican bot mec na najlaksoj tezini, bez story/ranked/gauntlet flagova
        private void StartPractice()
        {
            MatchConfig.ClearStory();
            MatchConfig.PlayerRace = Race.Humans;
            MatchConfig.OpponentRace = Race.Orcs;
            MatchConfig.Difficulty = BotDifficulty.VeryEasy;
            MatchConfig.Seed = 0;
            GameFlow.LoadBattle();
        }

        private void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() != null) return;
            new GameObject("EventSystem",
                typeof(UnityEngine.EventSystems.EventSystem),
                typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
        }

        // ESC vraca korak natrag u glavni izbornik
        private void Update()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) GameFlow.LoadMainMenu();
        }
    }
}
