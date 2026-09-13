using UnityEngine;
using UnityEngine.UI;

namespace LordOfTheRealms
{
    // UI sloj tutoriala: traka s uputom na dnu, NEXT gumb za tekstualne korake i
    // pulsirajuci okvir oko elementa o kojem korak prica. Podatke o koracima drzi
    // TutorialFlow; BattleController javlja kad je neki potez odigran.
    public class TutorialDirector : MonoBehaviour
    {
        private BattleController _battle;
        private Canvas _canvas;
        private RectTransform _bar, _highlight, _highlight2;
        private Text _text, _hintText, _stepLbl;
        private Button _nextBtn;
        private RectTransform _hiTarget, _hiTarget2;

        public static TutorialDirector Create(Canvas canvas, BattleController battle)
        {
            var go = new GameObject("TutorialDirector", typeof(RectTransform));
            go.transform.SetParent(canvas.transform, false);
            var d = go.AddComponent<TutorialDirector>();
            d._canvas = canvas;
            d._battle = battle;
            d.Build();
            return d;
        }

        private void Build()
        {
            // pulsirajuci okvir ide ispod trake da je nikad ne prekrije
            _highlight = MakeHighlightBox("TutHighlight");
            _highlight2 = MakeHighlightBox("TutHighlight2");

            _bar = UIFactory.CreatePanel(_canvas.transform, "TutBar",
                new Color(0.05f, 0.04f, 0.07f, 0.96f));
            UIFactory.Anchor(_bar, new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                new Vector2(0.5f, 0), new Vector2(0, 18), new Vector2(1180, 108));
            UIFactory.AddFrame(_bar);

            _text = UIFactory.CreateText(_bar, "Txt", "", 19,
                TextAnchor.MiddleLeft, new Color(0.95f, 0.93f, 0.88f));
            _text.horizontalOverflow = HorizontalWrapMode.Wrap;
            UIFactory.Anchor(_text.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f),
                new Vector2(0, 0.5f), new Vector2(26, 6), new Vector2(860, 78));

            _hintText = UIFactory.CreateText(_bar, "Hint", "", 15,
                TextAnchor.LowerLeft, new Color(1f, 0.6f, 0.5f));
            UIFactory.Anchor(_hintText.rectTransform, new Vector2(0, 0), new Vector2(0, 0),
                new Vector2(0, 0), new Vector2(26, 8), new Vector2(860, 20));

            // brojac koraka, gore desno u traci
            _stepLbl = UIFactory.CreateText(_bar, "StepLbl", "", 14,
                TextAnchor.UpperRight, new Color(0.62f, 0.6f, 0.68f));
            UIFactory.Anchor(_stepLbl.rectTransform, new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(1, 1), new Vector2(-26, -8), new Vector2(220, 20));

            _nextBtn = UIFactory.CreateButton(_bar, "TutNext", Localization.T("tut.next"), 20);
            UIFactory.Anchor(_nextBtn.GetComponent<RectTransform>(),
                new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f),
                new Vector2(-26, 0), new Vector2(230, 62));
            _nextBtn.onClick.AddListener(OnNext);

            Refresh();
        }

        // zlatni pulsirajuci okvir; ne hvata klikove
        private RectTransform MakeHighlightBox(string name)
        {
            var box = UIFactory.CreatePanel(_canvas.transform, name, new Color(1f, 0.85f, 0.35f, 0f));
            var edge = box.gameObject.AddComponent<Outline>();
            edge.effectColor = new Color(1f, 0.85f, 0.35f, 0.9f);
            edge.effectDistance = new Vector2(4, -4);   // deblji rub, jer ispuna je slaba
            box.GetComponent<Image>().raycastTarget = false;
            box.gameObject.SetActive(false);
            return box;
        }

        // NEXT vrijedi samo za tekstualne korake; ostale zatvara sam potez
        private void OnNext()
        {
            if (!TutorialFlow.Active)
            {
                // izlaz: OBAVEZNO ocisti zastavicu da sljedeci mec ne bude tutorial
                MatchConfig.ClearStory();
                GameFlow.LoadMainMenu();
                return;
            }
            if (TutorialFlow.Current.Gate != TutGate.Text) return;
            TutorialFlow.Advance();
            Refresh();
        }

        // BattleController zove nakon svakog uspjesnog poteza igraca
        public void NotifyAction(TutGate done)
        {
            if (!TutorialFlow.Active) return;
            if (TutorialFlow.Current.Gate != done) return;
            TutorialFlow.Advance();
            Refresh();
        }

        // kratki podsjetnik kad igrac klikne nesto sto trenutni korak ne trazi
        public void FlashBlocked()
        {
            if (_hintText == null) return;
            _hintText.text = Localization.T("tut.blocked");
            CancelInvoke(nameof(ClearHint));
            Invoke(nameof(ClearHint), 1.6f);
        }

        private void ClearHint() { if (_hintText != null) _hintText.text = ""; }

        public void Refresh()
        {
            if (_text == null) return;

            if (TutorialFlow.Finished)
            {
                _text.text = Localization.T("tut.done");
                SetNextLabel(Localization.T("tut.exit"));
                _nextBtn.gameObject.SetActive(true);
                SetHighlight(TutHi.None);
                if (_stepLbl != null) _stepLbl.text = "";
                return;
            }

            var step = TutorialFlow.Current;
            _text.text = Localization.T(step.Key);
            if (_stepLbl != null)
                _stepLbl.text = (TutorialFlow.Index + 1) + " / " + TutorialFlow.Steps.Length;
            _nextBtn.gameObject.SetActive(step.Gate == TutGate.Text);
            SetNextLabel(Localization.T("tut.next"));
            SetHighlight(step.Hi);
            ClearHint();
        }

        private void SetNextLabel(string s)
        {
            var t = _nextBtn.GetComponentInChildren<Text>();
            if (t != null) t.text = s;
        }

        // okvir prati element (pozicija se osvjezava jer se ruka i ploca iznova crtaju)
        private void SetHighlight(TutHi hi)
        {
            _hiTarget = _battle != null ? _battle.HighlightTarget(hi) : null;
            _hiTarget2 = _battle != null ? _battle.HighlightTargetSecondary(hi) : null;
            _highlight.gameObject.SetActive(_hiTarget != null);
            _highlight2.gameObject.SetActive(_hiTarget2 != null);
        }

        private void Update()
        {
            // Blink nosi OBRUB, a ispuna ostaje jedva vidljiva. Obrnuto je prije
            // prekrivalo ono sto korak pokazuje, pa se kroz highlight slabo vidjelo.
            float t = (Mathf.Sin(Time.unscaledTime * 3.2f) + 1f) * 0.5f;   // 0..1
            float fill = Mathf.Lerp(0.03f, 0.09f, t);
            float edge = Mathf.Lerp(0.55f, 1f, t);
            FollowTarget(_highlight, _hiTarget, fill, edge);
            FollowTarget(_highlight2, _hiTarget2, fill, edge);
        }

        // okvir prati element (pozicija se osvjezava jer se ruka i ploca iznova crtaju)
        private static void FollowTarget(RectTransform box, RectTransform target, float fill, float edge)
        {
            if (box == null || !box.gameObject.activeSelf) return;
            if (target == null) { box.gameObject.SetActive(false); return; }

            // VAZNO: target.position je pozicija PIVOTA (lice ima pivot na rubu),
            // pa se mora racunati prava sredina rectangla
            box.position = target.TransformPoint(target.rect.center);
            float scale = Mathf.Max(box.lossyScale.x, 0.0001f);
            box.sizeDelta = target.rect.size * target.lossyScale.x / scale + new Vector2(18, 18);
            box.GetComponent<Image>().color = new Color(1f, 0.85f, 0.35f, fill);
            var o = box.GetComponent<Outline>();
            if (o != null) o.effectColor = new Color(1f, 0.85f, 0.35f, edge);
        }
    }
}
