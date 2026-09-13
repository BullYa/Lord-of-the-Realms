using UnityEngine;
using UnityEngine.UI;

namespace LordOfTheRealms
{
    // Match setup ekran: protivnikova (bot) rasa i bot tezina (1-6). TVOJA rasa i
    // spil se preuzimaju iz Free Playa (isti izbor kao za ranked 1v1) pa se ovdje
    // samo prikazuju. Gradi se iz koda. "Start Match" ucita Battle scenu.
    public class MatchSetupController : MonoBehaviour
    {
        private const string MpRaceKey = "mp_race"; // isti kljuc kao Free Play

        private Race _playerRace = Race.Humans;
        private Race _oppRace = Race.Orcs;
        private BotDifficulty _difficulty = BotDifficulty.Medium;

        private Text _playerLabel, _oppLabel, _diffLabel;

        private void Start()
        {
            AudioManager.EnsureExists();
            // rasa je ona izabrana u Free Playu (koristi se i za ranked)
            _playerRace = (Race)PlayerPrefs.GetInt(MpRaceKey, (int)Race.Humans);
            BuildUI();
        }

        private void BuildUI()
        {
            var canvasGO = new GameObject("SetupCanvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            EnsureEventSystem();

            var bg = UIFactory.CreatePanel(canvasGO.transform, "Background",
                new Color(0.06f, 0.05f, 0.07f, 1f));
            UIFactory.Stretch(bg);

            var title = UIFactory.CreateText(canvasGO.transform, "Title", Localization.T("ms.title"),
                56, TextAnchor.MiddleCenter, UIFactory.Ink);
            UIFactory.Anchor(title.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(0.5f, 1), new Vector2(0, -80), new Vector2(800, 80));

            // TVOJA RASA: samo prikaz (dolazi iz Free Playa, zajedno sa spilom)
            _playerLabel = BuildReadonly(canvasGO.transform, Localization.T("ms.yourrace"), 200);

            // OPPONENT RACE
            _oppLabel = BuildSelector(canvasGO.transform, Localization.T("ms.opprace"), 20,
                () => { _oppRace = Prev(_oppRace); RefreshLabels(); },
                () => { _oppRace = Next(_oppRace); RefreshLabels(); });

            // DIFFICULTY
            _diffLabel = BuildSelector(canvasGO.transform, Localization.T("ms.difficulty"), -160,
                () => { _difficulty = PrevDiff(_difficulty); RefreshLabels(); },
                () => { _difficulty = NextDiff(_difficulty); RefreshLabels(); });

            // START
            var start = UIFactory.CreateButton(canvasGO.transform, "StartButton", Localization.T("ms.startmatch"), 30);
            UIFactory.Anchor(start.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -320), new Vector2(400, 90));
            start.onClick.AddListener(StartMatch);

            // BACK
            var back = UIFactory.CreateButton(canvasGO.transform, "BackButton", Localization.T("common.back"), 22);
            UIFactory.Anchor(back.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(0, 1), new Vector2(20, -20), new Vector2(140, 54));
            back.onClick.AddListener(() =>
            {
                if (GameFlow.MatchSetupFromFreePlay)
                {
                    GameFlow.MatchSetupFromFreePlay = false;
                    GameFlow.LoadMultiplayer();
                }
                else GameFlow.LoadMainMenu();
            });

            RefreshLabels();
        }

        // isti okvir kao selector, ali bez strelica; vrijednost se ne mijenja ovdje
        private Text BuildReadonly(Transform parent, string caption, float y)
        {
            var group = UIFactory.CreatePanel(parent, caption + "Group", UIFactory.Panel);
            UIFactory.Anchor(group, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(0, y), new Vector2(760, 120));

            var cap = UIFactory.CreateText(group, "Caption", caption, 22,
                TextAnchor.UpperCenter, new Color(0.7f, 0.6f, 0.6f));
            UIFactory.Anchor(cap.rectTransform, new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0.5f, 1), new Vector2(0, -8), new Vector2(0, 30));

            var value = UIFactory.CreateText(group, "Value", "", 34,
                TextAnchor.MiddleCenter, UIFactory.Ink);
            UIFactory.Anchor(value.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(0, -12), new Vector2(500, 70));

            // podsjetnik da se spil mijenja u Free Playu
            var hint = UIFactory.CreateText(group, "Hint", Localization.T("ms.deck_hint"), 15,
                TextAnchor.LowerCenter, new Color(0.65f, 0.62f, 0.68f));
            UIFactory.Anchor(hint.rectTransform, new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(0.5f, 0), new Vector2(0, 6), new Vector2(0, 22));

            return value;
        }

        private Text BuildSelector(Transform parent, string caption, float y,
            System.Action onLeft, System.Action onRight)
        {
            var group = UIFactory.CreatePanel(parent, caption + "Group", UIFactory.Panel);
            UIFactory.Anchor(group, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(0, y), new Vector2(760, 120));

            var cap = UIFactory.CreateText(group, "Caption", caption, 22,
                TextAnchor.UpperCenter, new Color(0.7f, 0.6f, 0.6f));
            UIFactory.Anchor(cap.rectTransform, new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0.5f, 1), new Vector2(0, -8), new Vector2(0, 30));

            var left = UIFactory.CreateButton(group, "Left", "<", 34);
            UIFactory.Anchor(left.GetComponent<RectTransform>(), new Vector2(0, 0.5f), new Vector2(0, 0.5f),
                new Vector2(0, 0.5f), new Vector2(20, -12), new Vector2(70, 70));
            left.onClick.AddListener(() => onLeft());

            var right = UIFactory.CreateButton(group, "Right", ">", 34);
            UIFactory.Anchor(right.GetComponent<RectTransform>(), new Vector2(1, 0.5f), new Vector2(1, 0.5f),
                new Vector2(1, 0.5f), new Vector2(-20, -12), new Vector2(70, 70));
            right.onClick.AddListener(() => onRight());

            var value = UIFactory.CreateText(group, "Value", "", 34,
                TextAnchor.MiddleCenter, UIFactory.Ink);
            UIFactory.Anchor(value.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(0, -12), new Vector2(500, 70));

            return value;
        }

        private void RefreshLabels()
        {
            _playerLabel.text = Localization.RaceName(_playerRace);
            _oppLabel.text = Localization.RaceName(_oppRace);
            _diffLabel.text = $"{(int)_difficulty} - {Localization.DiffName(_difficulty)}";
        }

        private void StartMatch()
        {
            MatchConfig.ClearStory(); // prilagodeni mec je obican mec (bez story/ranked/gauntlet)
            MatchConfig.PlayerRace = _playerRace;
            MatchConfig.OpponentRace = _oppRace;
            MatchConfig.Difficulty = _difficulty;
            MatchConfig.Seed = 0;
            // isti spil koji bi isao u ranked 1v1 (aktivni Free Play slot te rase)
            MatchConfig.PlayerDeckOverride = DeckStorage.LoadActive(_playerRace, true);
            GameFlow.LoadBattle();
        }

        // enum cycling helpers
        private static Race Next(Race r) => (Race)(((int)r + 1) % 4);
        private static Race Prev(Race r) => (Race)(((int)r + 3) % 4);
        private static BotDifficulty NextDiff(BotDifficulty d)
            => (BotDifficulty)Mathf.Clamp((int)d + 1, 1, 6);
        private static BotDifficulty PrevDiff(BotDifficulty d)
            => (BotDifficulty)Mathf.Clamp((int)d - 1, 1, 6);

        private void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() != null) return;
            new GameObject("EventSystem",
                typeof(UnityEngine.EventSystems.EventSystem),
                typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
        }

        // ESC vraca korak natrag, isto kamo i BACK gumb
        private void Update()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null || !kb.escapeKey.wasPressedThisFrame) return;
            if (GameFlow.MatchSetupFromFreePlay)
            {
                GameFlow.MatchSetupFromFreePlay = false;
                GameFlow.LoadMultiplayer();
            }
            else GameFlow.LoadMainMenu();
        }
    }
}
