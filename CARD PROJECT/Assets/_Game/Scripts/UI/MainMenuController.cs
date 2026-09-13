using UnityEngine;
using UnityEngine.UI;

namespace LordOfTheRealms
{
    // Glavni meni: naslov, gumbi, volume slider. Sve iz koda.
    public class MainMenuController : MonoBehaviour
    {
        private Slider _volumeSlider;

        private void Start()
        {
            AudioManager.EnsureExists();
            AudioManager.Instance.PlayMusic("Music_MainMenu");
            GameFlow.MatchSetupFromFreePlay = false; // povratak u meni cisti kontekst
            BuildUI();
        }

        private void BuildUI()
        {
            var canvasGO = new GameObject("MenuCanvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            EnsureEventSystem();

            var bg = UIFactory.CreatePanel(canvasGO.transform, "Background",
                new Color(0.06f, 0.05f, 0.07f, 1f));
            UIFactory.Stretch(bg);

            // vinjeta odmah iza pozadine: rubovi tonu u crno, sredina ostaje citljiva
            UIFactory.AddVignette(canvasGO.transform);

            var title = UIFactory.CreateText(canvasGO.transform, "Title",
                Localization.T("menu.title"), 72, TextAnchor.MiddleCenter, UIFactory.Ink);
            UIFactory.Anchor(title.rectTransform,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0, -150), new Vector2(1200, 110));

            // FREE PLAY objedinjuje ranked, gauntlet i deck edit; pa story, collection, achievements
            var mpBtn = UIFactory.CreateButton(canvasGO.transform, "MpButton", Localization.T("menu.freeplay"));
            UIFactory.Anchor(mpBtn.GetComponent<RectTransform>(),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(0, 110), new Vector2(400, 70));
            mpBtn.onClick.AddListener(GameFlow.LoadMultiplayer);

            var storyBtn = UIFactory.CreateButton(canvasGO.transform, "StoryButton", Localization.T("menu.story"));
            UIFactory.Anchor(storyBtn.GetComponent<RectTransform>(),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(0, 190), new Vector2(400, 70));
            storyBtn.onClick.AddListener(() => GameFlow.LoadStoryMap(fromMenu: true));

            var collectionBtn = UIFactory.CreateButton(canvasGO.transform, "CollectionButton", Localization.T("menu.collection"));
            UIFactory.Anchor(collectionBtn.GetComponent<RectTransform>(),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(0, 30), new Vector2(400, 70));
            collectionBtn.onClick.AddListener(GameFlow.LoadCollection);

            // tutorial: objasni pravila prije prvog meca
            var tutBtn = UIFactory.CreateButton(canvasGO.transform, "TutorialButton", Localization.T("menu.howtoplay"));
            UIFactory.Anchor(tutBtn.GetComponent<RectTransform>(),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(0, -130), new Vector2(400, 70));
            tutBtn.onClick.AddListener(GameFlow.LoadTutorial);

            var achBtn = UIFactory.CreateButton(canvasGO.transform, "AchButton", Localization.T("menu.achievements"));
            UIFactory.Anchor(achBtn.GetComponent<RectTransform>(),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(0, -50), new Vector2(400, 70));
            achBtn.onClick.AddListener(ToggleAchievements);
            BuildAchievementsPanel(canvasGO.transform);

            var quitBtn = UIFactory.CreateButton(canvasGO.transform, "QuitButton", Localization.T("menu.quit"));
            UIFactory.Anchor(quitBtn.GetComponent<RectTransform>(),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(0, -210), new Vector2(400, 70));
            quitBtn.onClick.AddListener(GameFlow.QuitGame);

            BuildVolumeControl(canvasGO.transform);
            BuildLanguageControl(canvasGO.transform);
        }

        // ---- achievements panel ----

        private RectTransform _achPanel, _achDim;

        private void BuildAchievementsPanel(Transform parent)
        {
            _achDim = UIFactory.CreatePanel(parent, "AchDim", new Color(0f, 0f, 0f, 0.85f));
            UIFactory.Stretch(_achDim);
            var dimBtn = _achDim.gameObject.AddComponent<Button>();
            dimBtn.transition = Selectable.Transition.None;
            dimBtn.onClick.AddListener(ToggleAchievements);
            _achDim.gameObject.SetActive(false);

            _achPanel = UIFactory.CreatePanel(parent, "AchPanel", new Color(0.08f, 0.07f, 0.10f, 1f));
            UIFactory.Anchor(_achPanel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760, 640));
            _achPanel.gameObject.SetActive(false);
        }

        private void ToggleAchievements()
        {
            bool show = !_achPanel.gameObject.activeSelf;
            _achDim.gameObject.SetActive(show);
            _achPanel.gameObject.SetActive(show);
            if (!show) return;
            _achDim.SetAsLastSibling();
            _achPanel.SetAsLastSibling();

            for (int i = _achPanel.childCount - 1; i >= 0; i--)
                Destroy(_achPanel.GetChild(i).gameObject);

            // okvir se gradi ovdje, a ne u BuildAchievementsPanel: linija iznad brise
            // svu djecu pri svakom otvaranju pa bi ga inace pojela
            UIFactory.AddFrame(_achPanel);

            // Achievementi se inace provjeravaju tek na kraju meca. Neke stvari se
            // otkljucaju izvan meca (npr. izbor drugog junaka na New Game), pa bi
            // ostale nepriznate do sljedeceg meca. Provjera ovdje to hvata.
            Achievements.CheckAll();

            int unlockedCount = 0;
            foreach (var d in Achievements.All)
                if (Achievements.IsUnlocked(d.Id)) unlockedCount++;

            var title = UIFactory.CreateText(_achPanel, "T",
                $"{Localization.T("menu.achievements")}   {unlockedCount} / {Achievements.All.Length}", 26,
                TextAnchor.UpperCenter, UIFactory.Ink);
            UIFactory.Anchor(title.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(0.5f, 1), new Vector2(0, -16), new Vector2(400, 34));

            // scrollabilna lista (ima ih vise nego sto stane na ekran)
            int n = Achievements.All.Length;
            const float rowH = 54f;
            UIFactory.CreateScrollView(_achPanel, "AchScroll",
                new Vector2(0, -56), new Vector2(724, 516), out var content);
            content.sizeDelta = new Vector2(0, n * rowH + 12);

            for (int i = 0; i < n; i++)
            {
                var a = Achievements.All[i];
                bool got = Achievements.IsUnlocked(a.Id);
                Color bg = got ? new Color(0.09f, 0.20f, 0.11f, 1f) : new Color(0.13f, 0.12f, 0.15f, 1f);
                Color line = got ? new Color(0.35f, 0.90f, 0.45f, 0.95f) : new Color(0.4f, 0.4f, 0.45f, 0.8f);

                var row = UIFactory.CreatePanel(content, $"A{i}", bg);
                UIFactory.Anchor(row, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                    new Vector2(0.5f, 1), new Vector2(0, -6 - i * rowH), new Vector2(700, 48));
                var re = row.gameObject.AddComponent<Outline>();
                re.effectColor = line; re.effectDistance = new Vector2(1.5f, -1.5f);

                var name = UIFactory.CreateText(row, "N", Achievements.TitleOf(a), 17,
                    TextAnchor.MiddleLeft, got ? new Color(0.85f, 1f, 0.88f) : new Color(0.65f, 0.65f, 0.7f));
                UIFactory.Anchor(name.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f),
                    new Vector2(0, 0.5f), new Vector2(14, 0), new Vector2(240, 40));

                var desc = UIFactory.CreateText(row, "D", Achievements.DescOf(a), 14,
                    TextAnchor.MiddleLeft, new Color(0.75f, 0.75f, 0.78f));
                desc.resizeTextForBestFit = true; desc.resizeTextMinSize = 10; desc.resizeTextMaxSize = 14;
                // uzi nego prije, jer desni stupac sad nosi napredak umjesto same kvacice
                UIFactory.Anchor(desc.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f), new Vector2(60, 0), new Vector2(360, 40));

                // kvacica kad je osvojen; inace napredak (npr. "7 / 50") ako se broji,
                // a crtica za one koje ili imas ili nemas
                string right = got ? "\u2713" : Achievements.ProgressOf(a);
                if (string.IsNullOrEmpty(right)) right = "\u2014";
                var mark = UIFactory.CreateText(row, "M", right, got ? 22 : 15,
                    TextAnchor.MiddleRight, got ? line : new Color(0.8f, 0.78f, 0.55f));
                UIFactory.Anchor(mark.rectTransform, new Vector2(1, 0.5f), new Vector2(1, 0.5f),
                    new Vector2(1, 0.5f), new Vector2(-14, 0), new Vector2(80, 40));
            }

            var close = UIFactory.CreateButton(_achPanel, "Close", Localization.T("common.close"), 18);
            UIFactory.Anchor(close.GetComponent<RectTransform>(), new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                new Vector2(0.5f, 0), new Vector2(0, 16), new Vector2(200, 46));
            close.onClick.AddListener(ToggleAchievements);
        }

        private void BuildVolumeControl(Transform parent)
        {
            var box = UIFactory.CreatePanel(parent, "VolumeBox", UIFactory.Panel);
            UIFactory.Anchor(box,
                new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(1f, 1f), new Vector2(-24, -24), new Vector2(320, 96));
            UIFactory.AddFrame(box);

            var label = UIFactory.CreateText(box, "VolumeLabel", Localization.T("settings.sound"), 22,
                TextAnchor.MiddleLeft, UIFactory.Ink);
            UIFactory.Anchor(label.rectTransform,
                new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, 1), new Vector2(18, -8), new Vector2(-36, 34));

            _volumeSlider = UIFactory.CreateSlider(box, "VolumeSlider");
            UIFactory.Anchor(_volumeSlider.GetComponent<RectTransform>(),
                new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(0.5f, 0), new Vector2(0, 26), new Vector2(-36, 22));

            _volumeSlider.value = AudioManager.Instance != null ? AudioManager.Instance.Volume : 1f;
            _volumeSlider.onValueChanged.AddListener(v =>
            {
                if (AudioManager.Instance != null)
                    AudioManager.Instance.SetVolume(v);
            });

            var valueText = UIFactory.CreateText(box, "VolumeValue", "", 18,
                TextAnchor.MiddleRight, new Color(0.7f, 0.7f, 0.7f));
            UIFactory.Anchor(valueText.rectTransform,
                new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(1, 1), new Vector2(-18, -8), new Vector2(-36, 34));
            valueText.alignment = TextAnchor.MiddleRight;
            UpdateValueText(valueText, _volumeSlider.value);
            _volumeSlider.onValueChanged.AddListener(v => UpdateValueText(valueText, v));
        }

        // EN / HR jezik prekidac ispod volume boxa; klik reloada aktivnu scenu
        private void BuildLanguageControl(Transform parent)
        {
            var box = UIFactory.CreatePanel(parent, "LanguageBox", UIFactory.Panel);
            UIFactory.Anchor(box,
                new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(1f, 1f), new Vector2(-24, -128), new Vector2(320, 56));
            UIFactory.AddFrame(box);

            var label = UIFactory.CreateText(box, "LangLabel", Localization.T("settings.language"), 20,
                TextAnchor.MiddleLeft, UIFactory.Ink);
            UIFactory.Anchor(label.rectTransform,
                new Vector2(0, 0.5f), new Vector2(0, 0.5f),
                new Vector2(0, 0.5f), new Vector2(18, 0), new Vector2(160, 32));

            // aktivni jezik zlatan, drugi prigusen; klik na aktivni ne radi nista
            Color on = new Color(0.55f, 0.45f, 0.2f);
            Color off = new Color(0.25f, 0.22f, 0.28f);

            var enBtn = UIFactory.CreateButton(box, "LangEN", "EN", 16);
            UIFactory.Anchor(enBtn.GetComponent<RectTransform>(),
                new Vector2(1, 0.5f), new Vector2(1, 0.5f),
                new Vector2(1, 0.5f), new Vector2(-70, 0), new Vector2(52, 38));
            enBtn.image.color = Localization.Current == Language.English ? on : off;
            enBtn.onClick.AddListener(() => Localization.SetLanguage(Language.English));

            var hrBtn = UIFactory.CreateButton(box, "LangHR", "HR", 16);
            UIFactory.Anchor(hrBtn.GetComponent<RectTransform>(),
                new Vector2(1, 0.5f), new Vector2(1, 0.5f),
                new Vector2(1, 0.5f), new Vector2(-14, 0), new Vector2(52, 38));
            hrBtn.image.color = Localization.Current == Language.Croatian ? on : off;
            hrBtn.onClick.AddListener(() => Localization.SetLanguage(Language.Croatian));
        }

        private void UpdateValueText(Text t, float v) => t.text = Mathf.RoundToInt(v * 100f) + "%";

        private void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() != null)
                return;
            new GameObject("EventSystem",
                typeof(UnityEngine.EventSystems.EventSystem),
                typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
        }
    }
}
