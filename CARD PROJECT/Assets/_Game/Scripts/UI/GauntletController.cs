using UnityEngine;
using UnityEngine.UI;

namespace LordOfTheRealms
{
    // Stanje jednog gauntlet runa (zivi kroz scene dok run traje)
    public static class GauntletRun
    {
        public static bool Active;
        public static int Wins;
        public static Race Race;

        // postavi MatchConfig za sljedeci mec u runu: bot jaca svaka 2 winova
        public static void ConfigureNextMatch()
        {
            MatchConfig.ClearStory();
            MatchConfig.IsGauntlet = true;
            MatchConfig.PlayerRace = Race;
            MatchConfig.OpponentRace = (Race)Random.Range(0, 4);
            MatchConfig.Difficulty = (BotDifficulty)Mathf.Min(6, 1 + Wins / 2);
            MatchConfig.Seed = 0;
            // gauntlet koristi isti Free Play deck (MP slot) za tu rasu kao ranked
            MatchConfig.PlayerDeckOverride = DeckStorage.LoadActive(Race, true);
        }
    }

    // Povijest zadnjih 5 gauntlet runova (streak + rasa), spremljena u PlayerPrefs.
    // Zapis = wins|raceInt.
    public static class GauntletHistory
    {
        private const string Key = "gauntlet_runs";
        private const int Max = 5;

        public struct Run { public int Wins; public Race Race; }

        public static void Record(int wins, Race race)
        {
            var list = LoadRaw();
            list.Insert(0, $"{wins}|{(int)race}");
            while (list.Count > Max) list.RemoveAt(list.Count - 1);
            PlayerPrefs.SetString(Key, string.Join("\n", list));
            PlayerPrefs.Save();
        }

        public static System.Collections.Generic.List<Run> Load()
        {
            var result = new System.Collections.Generic.List<Run>();
            foreach (var line in LoadRaw())
            {
                var p = line.Split('|');
                if (p.Length < 2) continue;
                result.Add(new Run { Wins = int.Parse(p[0]), Race = (Race)int.Parse(p[1]) });
            }
            return result;
        }

        private static System.Collections.Generic.List<string> LoadRaw()
        {
            var s = PlayerPrefs.GetString(Key, "");
            return string.IsNullOrEmpty(s)
                ? new System.Collections.Generic.List<string>()
                : new System.Collections.Generic.List<string>(s.Split('\n'));
        }
    }

    // Infinite Gauntlet lobby: odaberi rasu, START RUN, penji se dok ne izgubis.
    // Bez rewarda, samo best streak i achievementi.
    public class GauntletController : MonoBehaviour
    {
        private const string RaceKey = "gauntlet_race";
        private Canvas _canvas;
        private Text _raceText, _bestText;

        private Race PickedRace
        {
            get => (Race)PlayerPrefs.GetInt(RaceKey, (int)Race.Humans);
            set { PlayerPrefs.SetInt(RaceKey, (int)value); PlayerPrefs.Save(); }
        }

        private void Start()
        {
            AudioManager.EnsureExists();
            AudioManager.Instance.PlayMusic("Music_MainMenu");
            MatchConfig.ClearStory();
            GauntletRun.Active = false;
            BuildUI();
        }

        private void BuildUI()
        {
            var canvasGO = new GameObject("GauntletCanvas",
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

            var bg = UIFactory.CreatePanel(canvasGO.transform, "Bg", new Color(0.08f, 0.05f, 0.05f, 1f));
            UIFactory.Stretch(bg);

            var title = UIFactory.CreateText(canvasGO.transform, "Title", Localization.T("mp.gauntlet"), 36,
                TextAnchor.UpperCenter, UIFactory.Ink);
            UIFactory.Anchor(title.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(0.5f, 1), new Vector2(0, -26), new Vector2(800, 46));

            var sub = UIFactory.CreateText(canvasGO.transform, "Sub",
                Localization.T("g.sub"),
                17, TextAnchor.UpperCenter, new Color(0.85f, 0.8f, 0.8f));
            UIFactory.Anchor(sub.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(0.5f, 1), new Vector2(0, -80), new Vector2(1000, 26));

            _bestText = UIFactory.CreateText(canvasGO.transform, "Best", "", 24,
                TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0.4f));
            UIFactory.Anchor(_bestText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(0, 110), new Vector2(600, 34));
            _bestText.text = string.Format(Localization.T("g.beststreak"), Achievements.BestGauntlet);

            // scoreboard zadnjih runova, gore-desno
            var runs = GauntletHistory.Load();
            if (runs.Count > 0)
            {
                var hdr = UIFactory.CreateText(canvasGO.transform, "RunsHdr", Localization.T("g.recentruns"), 16,
                    TextAnchor.MiddleCenter, new Color(0.8f, 0.8f, 0.85f));
                UIFactory.Anchor(hdr.rectTransform, new Vector2(1, 0.5f), new Vector2(1, 0.5f),
                    new Vector2(1, 0.5f), new Vector2(-200, 150), new Vector2(320, 24));
                for (int i = 0; i < runs.Count; i++)
                {
                    var r = runs[i];
                    var row = UIFactory.CreateText(canvasGO.transform, $"Run{i}",
                        string.Format(Localization.T("g.run_row"), r.Wins, Localization.RaceName(r.Race)), 15, TextAnchor.MiddleCenter,
                        CardArtView.RaceColor(r.Race) * 1.5f);
                    UIFactory.Anchor(row.rectTransform, new Vector2(1, 0.5f), new Vector2(1, 0.5f),
                        new Vector2(1, 0.5f), new Vector2(-200, 118 - i * 30), new Vector2(320, 26));
                }
            }

            // race picker
            _raceText = UIFactory.CreateText(canvasGO.transform, "RaceLbl", "", 22,
                TextAnchor.MiddleCenter, Color.white);
            UIFactory.Anchor(_raceText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(0, 30), new Vector2(320, 34));
            RefreshRace();
            MakeArrow(canvasGO.transform, new Vector2(-200, 30), -1);
            MakeArrow(canvasGO.transform, new Vector2(200, 30), 1);

            var start = UIFactory.CreateButton(canvasGO.transform, "Start", Localization.T("g.startrun"), 26);
            UIFactory.Anchor(start.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(0, -70), new Vector2(340, 74));
            start.onClick.AddListener(() =>
            {
                GauntletRun.Active = true;
                GauntletRun.Wins = 0;
                GauntletRun.Race = PickedRace;
                GauntletRun.ConfigureNextMatch();
                GameFlow.LoadBattle();
            });

            // deck edit za gauntlet rasu: isti Free Play (MP) deck kao ranked
            var edit = UIFactory.CreateButton(canvasGO.transform, "Edit", Localization.T("mp.editdeck"), 20);
            UIFactory.Anchor(edit.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(0, -158), new Vector2(300, 56));
            edit.onClick.AddListener(() =>
            {
                CollectionContext.DeckEdit = true;
                CollectionContext.EditRace = PickedRace;
                CollectionContext.AllCards = false;
                CollectionContext.BackToMultiplayer = true; // MP deck slot (isti kao Free Play)
                CollectionContext.ReturnTo = CollectionContext.Return.Gauntlet;
                GameFlow.LoadCollection();
            });

            var back = UIFactory.CreateButton(canvasGO.transform, "Back", Localization.T("menu.freeplay"), 18);
            UIFactory.Anchor(back.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(0, 1), new Vector2(20, -16), new Vector2(140, 44));
            back.onClick.AddListener(GameFlow.LoadMultiplayer);
        }

        private void MakeArrow(Transform parent, Vector2 pos, int dir)
        {
            var b = UIFactory.CreateButton(parent, "Arrow" + dir, dir < 0 ? "<" : ">", 20);
            UIFactory.Anchor(b.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), pos, new Vector2(46, 38));
            b.onClick.AddListener(() =>
            {
                PickedRace = (Race)(((int)PickedRace + dir + 4) % 4);
                RefreshRace();
            });
        }

        private void RefreshRace()
        {
            _raceText.text = $"{Localization.T("mp.yourrace")}  {Localization.RaceName(PickedRace)}";
            _raceText.color = CardArtView.RaceColor(PickedRace) * 1.6f;
        }

        // ESC vraca korak natrag u Free Play
        private void Update()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) GameFlow.LoadMultiplayer();
        }
    }
}
