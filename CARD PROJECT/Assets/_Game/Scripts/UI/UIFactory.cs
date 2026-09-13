using UnityEngine;
using UnityEngine.UI;

namespace LordOfTheRealms
{
    // Mali pomocnik koji gradi standardne uGUI elemente iz koda, da se scene mogu
    // generirati programski umjesto rucnog slaganja u editoru.
    // Koristi ugradeni legacy Text (uvijek dostupan) da se izbjegne TextMeshPro
    // import tijekom prototipiranja; tekst dobiva tamni obrub da se cita na
    // bilo kojoj pozadini.
    public static class UIFactory
    {
        public static readonly Color Ink = new Color(0.96f, 0.95f, 0.92f);
        public static readonly Color Panel = new Color(0.14f, 0.13f, 0.16f, 0.96f);
        public static readonly Color Accent = new Color(0.6f, 0.16f, 0.2f); // dark blood red
        public static readonly Color SlotEmpty = new Color(0.20f, 0.19f, 0.23f, 0.9f);

        private static Font _font;
        public static Font DefaultFont
        {
            get
            {
                if (_font != null) return _font;
                // Vlastiti font ima prednost: ubaci TTF kao Assets/Resources/Fonts/UIFont.ttf
                // (npr. pixel ili medieval) i cijeli UI ga preuzme, bez ijedne izmjene koda.
                _font = Resources.Load<Font>("Fonts/UIFont");
                if (_font == null)
                    _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (_font == null)
                    _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                return _font;
            }
        }

        public static RectTransform CreatePanel(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = color;
            return go.GetComponent<RectTransform>();
        }

        public static Text CreateText(Transform parent, string name, string content,
            int fontSize, TextAnchor anchor, Color? color = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.text = content;
            t.font = DefaultFont;
            t.fontSize = fontSize;
            t.fontStyle = FontStyle.Bold;
            t.alignment = anchor;
            t.color = color ?? Ink;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;

            // Tamni obrub da se tekst cita na bilo kojoj boji card ili panela.
            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(1.4f, -1.4f);

            return t;
        }

        public static Button CreateButton(Transform parent, string name, string label,
            int fontSize = 26)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);

            var img = go.GetComponent<Image>();
            img.color = Accent;

            var btn = go.GetComponent<Button>();
            // Transition None: Unityjev tint je pisao po Image.color i time brisao boje
            // koje neki gumbi postavljaju rucno (zlatni aktivni slot, crveni DELETE).
            // Sav hover/press feedback radi ButtonHover (ember sjaj + scale).
            btn.transition = Selectable.Transition.None;

            // mjedeni pixel okvir preko pozadine (sredina sprajta je prozirna)
            AddFrame(go.GetComponent<RectTransform>());

            var label_ = CreateText(go.transform, "Label", label, fontSize, TextAnchor.MiddleCenter);
            var lrt = label_.rectTransform;
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;

            // blago povecanje na hover da gumbi djeluju zivo
            go.AddComponent<ButtonHover>();
            // klik zvuk
            btn.onClick.AddListener(AudioManager.Click);

            return btn;
        }

        public static Slider CreateSlider(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var slider = go.AddComponent<Slider>();

            var bg = CreatePanel(go.transform, "Background", new Color(0.05f, 0.05f, 0.06f, 1f));
            Stretch(bg);

            var fillArea = new GameObject("Fill Area", typeof(RectTransform)).GetComponent<RectTransform>();
            fillArea.SetParent(go.transform, false);
            Stretch(fillArea);
            fillArea.offsetMin = new Vector2(5, 0);
            fillArea.offsetMax = new Vector2(-5, 0);

            var fill = CreatePanel(fillArea, "Fill", Accent);
            fill.anchorMin = new Vector2(0, 0);
            fill.anchorMax = new Vector2(0, 1);
            fill.sizeDelta = new Vector2(10, 0);

            var handleArea = new GameObject("Handle Slide Area", typeof(RectTransform)).GetComponent<RectTransform>();
            handleArea.SetParent(go.transform, false);
            Stretch(handleArea);
            var handle = CreatePanel(handleArea, "Handle", Ink);
            handle.sizeDelta = new Vector2(16, 0);

            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handle.GetComponent<Image>();
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 1f;

            return slider;
        }

        // Mjedeni pixel-art okvir kao dijete zadanog rect-a (9-slice, sredina prozirna
        // pa pozadina ostaje vidljiva). Ne hvata klikove.
        public static Image AddFrame(RectTransform target, float alpha = 0.95f)
        {
            var go = new GameObject("Frame", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(target, false);
            Stretch(go.GetComponent<RectTransform>());
            var img = go.GetComponent<Image>();
            img.sprite = UISprites.Frame;
            img.type = Image.Type.Sliced;
            img.raycastTarget = false;
            img.color = new Color(1f, 1f, 1f, alpha);
            return img;
        }

        // Vinjeta preko cijelog ekrana: tamni rubovi, sredina cista. Zove se odmah
        // nakon pozadine da ostane ispod ostalog UI-a.
        public static Image AddVignette(Transform parent)
        {
            var go = new GameObject("Vignette", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            Stretch(go.GetComponent<RectTransform>());
            var img = go.GetComponent<Image>();
            img.sprite = UISprites.Vignette;
            img.raycastTarget = false;
            img.color = Color.white;
            return img;
        }

        public static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        // Polje za unos teksta. Projekt inace nema unos, sve je gumbi i natpisi,
        // pa ovo koristi legacy InputField zbog istog razloga kao i Text: radi bez
        // uvoza TextMeshPro paketa.
        public static InputField CreateInput(Transform parent, string name, string placeholder,
                                             int fontSize = 22, int charLimit = 0)
        {
            var box = CreatePanel(parent, name, new Color(0.06f, 0.05f, 0.08f, 0.95f));
            AddFrame(box);

            var ph = CreateText(box, "Placeholder", placeholder, fontSize,
                TextAnchor.MiddleCenter, new Color(0.55f, 0.52f, 0.6f));
            Stretch(ph.rectTransform);

            var txt = CreateText(box, "Text", "", fontSize, TextAnchor.MiddleCenter, Ink);
            Stretch(txt.rectTransform);

            var field = box.gameObject.AddComponent<InputField>();
            field.textComponent = txt;
            field.placeholder = ph;
            field.targetGraphic = box.GetComponent<Image>();
            if (charLimit > 0) field.characterLimit = charLimit;

            return field;
        }

        // Vertikalni scroll. Vraca CONTENT rect; pozicioniraj se od vrha (anchor
        // gore) i postavi mu visinu (sizeDelta.y) prema broju redaka; viewport maskira,
        // scroll radi kotacicem misa. Root je usidren gore-centar u parentu.
        public static ScrollRect CreateScrollView(Transform parent, string name,
            Vector2 anchoredPos, Vector2 size, out RectTransform content)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(ScrollRect));
            root.transform.SetParent(parent, false);
            var rootRt = root.GetComponent<RectTransform>();
            rootRt.anchorMin = new Vector2(0.5f, 1); rootRt.anchorMax = new Vector2(0.5f, 1);
            rootRt.pivot = new Vector2(0.5f, 1);
            rootRt.anchoredPosition = anchoredPos;
            rootRt.sizeDelta = size;

            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(CanvasRenderer),
                typeof(Image), typeof(RectMask2D)).GetComponent<RectTransform>();
            viewport.SetParent(root.transform, false);
            Stretch(viewport);
            viewport.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.001f); // mask treba graphic

            content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(viewport, false);
            content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(0, size.y);

            var sr = root.GetComponent<ScrollRect>();
            sr.viewport = viewport;
            sr.content = content;
            sr.horizontal = false;
            sr.vertical = true;
            sr.movementType = ScrollRect.MovementType.Clamped;
            sr.scrollSensitivity = 28f;
            return sr;
        }

        public static void Anchor(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 pivot, Vector2 anchoredPos, Vector2 size)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
        }
    }
}
