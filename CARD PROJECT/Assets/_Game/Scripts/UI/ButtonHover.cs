using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LordOfTheRealms
{
    // Hover/press feedback za svaki gumb koji UIFactory izgradi: iza natpisa se
    // rasplamsa topli ember sjaj (s laganim treperenjem, kao zeravica), tekst
    // posvijetli u zlatno, sa strana se razvuku tanke crte i gumb jedva primjetno
    // naraste; klik ga kratko utisne.
    //
    // Pozadinsku boju NAMJERNO ne dira: neki gumbi je postavljaju rucno (zlatni
    // aktivni deck slot, crveni DELETE), pa bi im je stalno pregazio. Zato je i
    // Selectable transition u UIFactory postavljen na None.
    public class ButtonHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler
    {
        private const float HoverScale = 1.04f;
        private const float PressScale = 0.97f;
        private const float Speed = 9f;
        private const float StreakLen = 46f;   // koliko se crta razvuce sa strane
        private const float WideButton = 190f; // uze gumbe (strelice, brojevi) crte bi zagusile

        private static readonly Color Ember = new Color(1f, 0.40f, 0.10f);    // sjaj
        private static readonly Color EmberInk = new Color(1f, 0.90f, 0.66f); // tekst na hoveru

        private RectTransform _rt;
        private Button _btn;
        private CanvasGroup _group;
        private Text _label;
        private Outline _labelOutline;
        private Image _glow;
        private RectTransform _streakL, _streakR;

        private Color _inkBase, _outlineBase;
        private float _t;             // 0..1 koliko je hover "upaljen"
        private bool _over, _down;
        private bool _dimmed;         // drzimo li MI trenutno alfu (disabled stanje)

        private void Awake()
        {
            _rt = GetComponent<RectTransform>();
            _btn = GetComponent<Button>();
            // Posto je Selectable transition None (da ne pregazi rucne boje), sami
            // moramo pokazati disabled stanje, inace bi zakljucan gumb (END TURN dok
            // traje animacija, FIGHT na zatvorenom nodeu) izgledao potpuno normalno.
            _group = gameObject.GetComponent<CanvasGroup>();
            if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();
        }

        // Start, a NE Awake: UIFactory doda ovu komponentu jos dok gumb ima default
        // velicinu; pravi sizeDelta stigne tek kad pozivatelj pozove UIFactory.Anchor,
        // pa bi u Awakeu provjera sirine (crte) uvijek gledala krivi broj.
        private void Start()
        {
            _label = GetComponentInChildren<Text>();
            if (_label != null)
            {
                _inkBase = _label.color;
                _labelOutline = _label.GetComponent<Outline>();
                if (_labelOutline != null) _outlineBase = _labelOutline.effectColor;
            }
            BuildVisuals();
        }

        private void BuildVisuals()
        {
            // sjaj ide IZA natpisa (sibling index 0) ali iznad pozadine gumba;
            // smije prelaziti rub gumba jer nista ne masksira, pa svjetlo "curi" van
            _glow = MakeImage("Glow", UISprites.Glow, new Vector2(-30, -22), new Vector2(30, 22));
            _glow.transform.SetSiblingIndex(0);

            if (_rt.sizeDelta.x < WideButton) return; // crte samo na sirokim gumbima

            _streakL = MakeStreak("StreakL", new Vector2(0, 0.5f), new Vector2(1, 0.5f), -8f);
            _streakR = MakeStreak("StreakR", new Vector2(1, 0.5f), new Vector2(0, 0.5f), 8f);
        }

        private Image MakeImage(string name, Sprite sprite, Vector2 offMin, Vector2 offMax)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = offMin; rt.offsetMax = offMax;
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.raycastTarget = false; // ne smije krasti klik gumbu
            img.color = new Color(Ember.r, Ember.g, Ember.b, 0f);
            return img;
        }

        // crta usidrena uz rub gumba koja raste PREMA VAN
        private RectTransform MakeStreak(string name, Vector2 anchor, Vector2 pivot, float x)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = new Vector2(x, 0);
            rt.sizeDelta = new Vector2(0, 3);
            var img = go.GetComponent<Image>();
            img.sprite = UISprites.Streak;
            img.raycastTarget = false;
            img.color = new Color(Ember.r, Ember.g, Ember.b, 0f);
            go.transform.SetSiblingIndex(0);
            return rt;
        }

        public void OnPointerEnter(PointerEventData e)
        {
            if (_btn != null && !_btn.interactable) return; // zakljucan gumb se ne pali
            _over = true;
            AudioManager.Hover(); // tihi tick (ako Sfx_Hover ne postoji, samo sutnja)
        }
        public void OnPointerExit(PointerEventData e) { _over = false; _down = false; }
        public void OnPointerDown(PointerEventData e) => _down = true;
        public void OnPointerUp(PointerEventData e) => _down = false;

        private void OnDisable()
        {
            // ne ostani zapaljen ako se gumb sakrije dok je mis na njemu
            _over = _down = false;
            _t = 0f;
            Apply(0f);
        }

        private void Update()
        {
            // zakljucan gumb se ne pali i vidljivo potamni
            bool usable = _btn == null || _btn.interactable;
            if (!usable) { _over = false; _down = false; }
            // Alfu diramo SAMO dok je gumb zakljucan (i dok je vracamo na 1), inace
            // bismo se otimali s Anim/Toast fadeovima koji isto koriste CanvasGroup.
            if (_group != null)
            {
                if (!usable)
                {
                    _group.alpha = Mathf.MoveTowards(_group.alpha, 0.45f, Time.unscaledDeltaTime * 6f);
                    _dimmed = true;
                }
                else if (_dimmed)
                {
                    _group.alpha = Mathf.MoveTowards(_group.alpha, 1f, Time.unscaledDeltaTime * 6f);
                    if (_group.alpha >= 0.999f) _dimmed = false;
                }
            }

            // unscaled: pause meni mijenja Time.timeScale (SPEED x1/1.5/2), a hover
            // mora raditi jednako i kad je igra usporena ili stopirana
            _t = Mathf.MoveTowards(_t, _over ? 1f : 0f, Time.unscaledDeltaTime * Speed);
            Apply(_t * _t * (3f - 2f * _t)); // smoothstep = mekani ulaz/izlaz
        }

        private void Apply(float e)
        {
            if (_rt == null) return;

            float scale = Mathf.Lerp(1f, HoverScale, e) * (_down ? PressScale : 1f);
            _rt.localScale = new Vector3(scale, scale, 1f);

            if (_glow != null)
            {
                // zeravica lagano dise dok je gumb aktivan
                float flicker = e > 0.01f ? 1f + Mathf.Sin(Time.unscaledTime * 7f) * 0.06f : 1f;
                _glow.color = new Color(Ember.r, Ember.g, Ember.b, e * 0.5f * flicker);
            }

            if (_streakL != null)
            {
                float len = StreakLen * e;
                _streakL.sizeDelta = new Vector2(len, 3);
                _streakR.sizeDelta = new Vector2(len, 3);
                var c = new Color(Ember.r, Ember.g, Ember.b, e * 0.85f);
                _streakL.GetComponent<Image>().color = c;
                _streakR.GetComponent<Image>().color = c;
            }

            if (_label != null)
            {
                _label.color = Color.Lerp(_inkBase, EmberInk, e);
                if (_labelOutline != null)
                    _labelOutline.effectColor = Color.Lerp(_outlineBase, new Color(0.8f, 0.25f, 0.05f, 0.9f), e);
            }
        }
    }
}
