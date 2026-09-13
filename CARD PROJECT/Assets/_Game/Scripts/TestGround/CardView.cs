using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System;
using DG.Tweening;

namespace LordOfTheRealms
{
    // Jedna card na ekranu, u handu ili na boardu. Crta isto lice card kao i
    // collection (cost, art placeholder, ime i footer po vrsti), a preko toga
    // dodaje zivo stanje meca: trenutni health, ready/summoning sick oznaku,
    // selection highlight i face-down izgled za skrivene protivnicke spellove.
    public class CardView : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        public CardInstance Card { get; private set; }
        public Owner Side { get; private set; }

        public event Action<CardView> OnClicked;

        private Image _frame;
        private Image _highlight;
        private Text _liveStats;   // current attack/health, overrides the printed base stats
        private Text _stateText;   // READY / sick
        private bool _concealed;
        private bool _greyed;      // neigriva card se crta posivljeno

        public void Bind(CardInstance card, Owner side, bool greyed = false)
        {
            Card = card;
            Side = side;
            _greyed = greyed;
            Build();
            Refresh();
            card.OnStatsChanged += _ => Refresh();
        }

        private void Build()
        {
            var rt = GetComponent<RectTransform>();
            if (rt == null) rt = gameObject.AddComponent<RectTransform>();

            _concealed = Card.IsHidden && Side == Owner.Opponent;

            if (_concealed)
            {
                _frame = gameObject.AddComponent<Image>();
                _frame.color = new Color(0.25f, 0.12f, 0.14f);
                var q = UIFactory.CreateText(transform, "Q", "?", 40, TextAnchor.MiddleCenter, UIFactory.Ink);
                Stretch(q.rectTransform);
                AddHighlight();
                return;
            }

            // nacrtaj zajednicko lice card, pa preko toga zivo stanje
            CardArtView.Paint(rt, Card.Data, greyed: _greyed);
            _frame = GetComponent<Image>();

            // umjesto da lijepim drugi stats label preko ispisanog, uzmi "Stats"
            // text koji je CardArtView vec napravio i osvjezi ga zivim vrijednostima
            _liveStats = FindDeep(transform, "Stats");

            // READY / sick oznaka gore desno, preko arta, da ne smeta
            _stateText = UIFactory.CreateText(transform, "State", "", 12, TextAnchor.UpperRight,
                new Color(0.85f, 0.85f, 0.85f));
            var srt = _stateText.rectTransform;
            srt.anchorMin = new Vector2(1, 1); srt.anchorMax = new Vector2(1, 1);
            srt.pivot = new Vector2(1, 1);
            // drain karte imaju crveni -N badge u istom kutu, pa oznaku spustamo ispod njega
            srt.anchoredPosition = new Vector2(-6, Card.Data.maxPowerCost > 0 ? -40 : -6);
            srt.sizeDelta = new Vector2(64, 20);

            AddHighlight();
        }

        // trazi child Text po imenu u dubinu (ispisani footer ga ugnijezdi)
        private Text FindDeep(Transform root, string childName)
        {
            for (int i = 0; i < root.childCount; i++)
            {
                var c = root.GetChild(i);
                if (c.name == childName)
                {
                    var t = c.GetComponent<Text>();
                    if (t != null) return t;
                }
                var found = FindDeep(c, childName);
                if (found != null) return found;
            }
            return null;
        }

        private void AddHighlight()
        {
            _highlight = UIFactory.CreatePanel(transform, "Highlight", new Color(1f, 0.9f, 0.3f, 0f))
                .GetComponent<Image>();
            var hrt = _highlight.rectTransform;
            hrt.anchorMin = Vector2.zero; hrt.anchorMax = Vector2.one;
            hrt.offsetMin = new Vector2(-4, -4); hrt.offsetMax = new Vector2(4, 4);
            _highlight.transform.SetAsFirstSibling();
        }

        public void Refresh()
        {
            if (Card == null || _concealed) return;

            if (_liveStats != null && Card.Data.Category != CardCategory.Spell)
                _liveStats.text = $"{Card.Attack} / {Card.CurrentHealth}";

            if (_stateText != null)
            {
                if (Card.IsHidden) _stateText.text = "HIDDEN";
                else if (Card.Data.Category != CardCategory.Spell)
                    _stateText.text = Card.CanAttack ? "READY" : "sick";
                else _stateText.text = "";
            }

            if (_frame != null && !Card.IsAlive)
                _frame.color = new Color(0.15f, 0.15f, 0.15f);
        }

        public void SetHighlight(bool on, Color? color = null)
        {
            if (_highlight == null) return;
            var c = color ?? new Color(1f, 0.9f, 0.3f);
            _highlight.color = on ? new Color(c.r, c.g, c.b, 0.9f) : new Color(c.r, c.g, c.b, 0f);
        }

        public void OnPointerClick(PointerEventData eventData) => OnClicked?.Invoke(this);

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_concealed || _selected) return; // don't fight the selected lift
            _hoverTarget = HoverScale;
            transform.SetAsLastSibling(); // lift above neighbours while hovered
            var canvas = GetComponentInParent<Canvas>();
            if (canvas != null) CardTooltip.Ensure(canvas).Show(Card.Data);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _hoverTarget = 1f;
            var canvas = GetComponentInParent<Canvas>();
            if (canvas != null) CardTooltip.Ensure(canvas).Hide();
        }

        // Zove se kad ova card postane ili prestane biti odabrani napadac. Odabrane
        // cards se podignu i lagano njisu; hover skaliranje se pauzira da se to dvoje
        // ne otima oko transforma.
        private bool _selected;
        private Vector2 _restPos;
        private bool _capturedRest;
        public void SetSelected(bool on)
        {
            var rt = (RectTransform)transform;
            if (on && !_capturedRest) { _restPos = rt.anchoredPosition; _capturedRest = true; }
            _selected = on;
            _hoverTarget = 1f;
            rt.DOKill();
            if (on)
            {
                transform.SetAsLastSibling();
                rt.DOAnchorPosY(_restPos.y + 30f, 0.16f).SetEase(Ease.OutBack).SetLink(gameObject)
                  .OnComplete(() =>
                  {
                      if (this == null) return;
                      rt.DOAnchorPosY(_restPos.y + 38f, 0.7f)
                        .SetEase(Ease.InOutSine)
                        .SetLoops(-1, LoopType.Yoyo)
                        .SetLink(gameObject);
                  });
                rt.DOScale(1.16f, 0.16f).SetEase(Ease.OutBack).SetLink(gameObject);
            }
            else if (_capturedRest)
            {
                rt.DOAnchorPosY(_restPos.y, 0.14f).SetEase(Ease.OutQuad).SetLink(gameObject);
                rt.DOScale(1f, 0.14f).SetEase(Ease.OutQuad).SetLink(gameObject);
            }
        }

        // pobij tweenove koji jos ciljaju ovu card kad se makne ili ugasi, da ne
        // pomicu unisteni transform (izbjegavamo SetLink zbog starijih DOTween
        // buildova, pa ciscenje ide rucno)
        private void OnDisable()
        {
            ((RectTransform)transform).DOKill();
            HideTooltipIfMine();
        }

        private void OnDestroy()
        {
            ((RectTransform)transform).DOKill();
            HideTooltipIfMine();
        }

        // ako se card unisti ili ugasi dok je mis na njoj, OnPointerExit se nikad ne
        // javi i tooltip bi ostao visjeti. Zato ga skrivamo ovdje.
        private void HideTooltipIfMine()
        {
            if (_concealed) return;
            CardTooltip.HideActive();
        }

        // glatko rastuci ili padajuci prema hover skali
        private const float HoverScale = 1.08f;
        private float _hoverTarget = 1f;
        private void Update()
        {
            if (_selected) return; // selected lift owns the transform
            // ne otimaj se s pop-in i pop-scale animacijama koje isto voze scale
            // unscaled: hover ne smije zamrznuti u pauzi ni ubrzati na 2x brzini
            float s = Mathf.Lerp(transform.localScale.x, _hoverTarget, Time.unscaledDeltaTime * 12f);
            if (Mathf.Abs(_hoverTarget - 1f) > 0.001f || Mathf.Abs(transform.localScale.x - 1f) > 0.01f)
                transform.localScale = new Vector3(s, s, 1f);
        }

        private void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }
    }
}
