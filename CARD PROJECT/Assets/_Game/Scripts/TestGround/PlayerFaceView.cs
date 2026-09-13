using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace LordOfTheRealms
{
    // Klikabilni "hero" panel igraca: gornji red rasa/owner, donji HEALTH + POWER.
    // Targetable kao jedinica, pa se moze provjeriti da taunt stiti heroja od
    // precise napadaca.
    public class PlayerFaceView : MonoBehaviour, IPointerClickHandler
    {
        public PlayerEntity Entity { get; private set; }
        public event Action<PlayerFaceView> OnClicked;

        private Text _titleText;
        private Text _hpText;
        private Text _powerText;
        private Image _highlight;

        public void Bind(PlayerEntity entity)
        {
            Entity = entity;
            Build();
            Refresh();
            entity.OnHealthChanged += _ => Refresh();
            entity.Power.OnChanged += _ => Refresh();
        }

        private void Build()
        {
            _highlight = UIFactory.CreatePanel(transform, "FaceHighlight", new Color(1f, 0.9f, 0.3f, 0f))
                .GetComponent<Image>();
            var hrt = _highlight.rectTransform;
            hrt.anchorMin = Vector2.zero; hrt.anchorMax = Vector2.one;
            hrt.offsetMin = new Vector2(-4, -4); hrt.offsetMax = new Vector2(4, 4);
            _highlight.transform.SetAsFirstSibling();

            // gornji red: rasa / owner
            _titleText = UIFactory.CreateText(transform, "Title", "", 22,
                TextAnchor.UpperCenter, new Color(0.96f, 0.95f, 0.92f));
            var trt = _titleText.rectTransform;
            trt.anchorMin = new Vector2(0, 1); trt.anchorMax = new Vector2(1, 1);
            trt.pivot = new Vector2(0.5f, 1);
            trt.anchoredPosition = new Vector2(0, -8);
            trt.sizeDelta = new Vector2(-16, 34);

            // dolje lijevo: HP
            _hpText = UIFactory.CreateText(transform, "HP", "", 26,
                TextAnchor.LowerLeft, new Color(1f, 0.5f, 0.5f));
            var hp = _hpText.rectTransform;
            hp.anchorMin = new Vector2(0, 0); hp.anchorMax = new Vector2(0.5f, 0);
            hp.pivot = new Vector2(0, 0);
            hp.anchoredPosition = new Vector2(14, 10);
            hp.sizeDelta = new Vector2(-14, 40);

            // dolje desno: POWER
            _powerText = UIFactory.CreateText(transform, "Power", "", 26,
                TextAnchor.LowerRight, new Color(0.55f, 0.8f, 1f));
            var pw = _powerText.rectTransform;
            pw.anchorMin = new Vector2(0.5f, 0); pw.anchorMax = new Vector2(1, 0);
            pw.pivot = new Vector2(1, 0);
            pw.anchoredPosition = new Vector2(-14, 10);
            pw.sizeDelta = new Vector2(-14, 40);
        }

        public void Refresh()
        {
            if (Entity == null) return;
            _titleText.text = $"{Entity.Owner} · {Entity.Race}";
            _hpText.text = $"♥ {Entity.CurrentHealth}";
            _powerText.text = $"◆ {Entity.Power.CurrentPower}/{Entity.Power.MaxPower}";
        }

        public void SetHighlight(bool on, Color? color = null)
        {
            var c = color ?? new Color(1f, 0.9f, 0.3f);
            _highlight.color = on ? new Color(c.r, c.g, c.b, 0.9f) : new Color(c.r, c.g, c.b, 0f);
        }

        public void OnPointerClick(PointerEventData eventData) => OnClicked?.Invoke(this);
    }
}
