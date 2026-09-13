using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace LordOfTheRealms
{
    // Animirani toast banner: klizne odozgo, poskoci (OutBack), zadrzi se, pa
    // nestane prema gore. Cisti se sam preko DOTween sekvence (SetLink), pa ne
    // treba coroutine host. Vise toasteva se slaze vertikalno preko 'slot'.
    public static class Toast
    {
        public static void Show(Transform canvas, string title, string body, Color accent,
            int slot = 0, float hold = 2.0f)
        {
            if (canvas == null) return;

            var panel = UIFactory.CreatePanel(canvas, "Toast", new Color(0.08f, 0.07f, 0.10f, 0.98f));
            panel.anchorMin = new Vector2(0.5f, 1); panel.anchorMax = new Vector2(0.5f, 1);
            panel.pivot = new Vector2(0.5f, 1);
            panel.sizeDelta = new Vector2(540, string.IsNullOrEmpty(body) ? 66 : 92);

            float shownY = -110f - slot * 104f;
            float hiddenY = 44f - slot * 104f;
            panel.anchoredPosition = new Vector2(0, hiddenY);

            var edge = panel.gameObject.AddComponent<Outline>();
            edge.effectColor = accent;
            edge.effectDistance = new Vector2(2, -2);

            // lijeva accent traka
            var bar = UIFactory.CreatePanel(panel, "Bar", accent);
            bar.anchorMin = new Vector2(0, 0); bar.anchorMax = new Vector2(0, 1);
            bar.pivot = new Vector2(0, 0.5f);
            bar.anchoredPosition = Vector2.zero;
            bar.sizeDelta = new Vector2(6, 0);
            bar.GetComponent<Image>().raycastTarget = false;

            bool hasBody = !string.IsNullOrEmpty(body);
            var t = UIFactory.CreateText(panel, "T", title, 20,
                hasBody ? TextAnchor.UpperCenter : TextAnchor.MiddleCenter, accent);
            UIFactory.Anchor(t.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1),
                new Vector2(0, hasBody ? -12 : -33), new Vector2(-28, hasBody ? 26 : 30));
            t.raycastTarget = false;

            if (hasBody)
            {
                var b = UIFactory.CreateText(panel, "B", body, 16, TextAnchor.UpperCenter, UIFactory.Ink);
                UIFactory.Anchor(b.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1),
                    new Vector2(0, -44), new Vector2(-28, 40));
                b.horizontalOverflow = HorizontalWrapMode.Wrap;
                b.raycastTarget = false;
            }

            var cg = panel.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            cg.blocksRaycasts = false;
            cg.interactable = false;
            panel.localScale = Vector3.one * 0.85f;

            var seq = DOTween.Sequence().SetLink(panel.gameObject);
            seq.Append(panel.DOAnchorPosY(shownY, 0.36f).SetEase(Ease.OutBack));
            seq.Join(cg.DOFade(1f, 0.28f));
            seq.Join(panel.DOScale(1f, 0.36f).SetEase(Ease.OutBack));
            seq.AppendInterval(hold);
            seq.Append(panel.DOAnchorPosY(hiddenY, 0.3f).SetEase(Ease.InBack));
            seq.Join(cg.DOFade(0f, 0.3f));
            seq.OnComplete(() => { if (panel != null) Object.Destroy(panel.gameObject); });
        }
    }
}
