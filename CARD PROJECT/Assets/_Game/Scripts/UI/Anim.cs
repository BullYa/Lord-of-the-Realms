using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace LordOfTheRealms
{
    // Pomocnici za animacije, radeni na DOTweenu. Vecina vraca IEnumerator da
    // pozivatelj moze `yield return StartCoroutine(Anim.X(...))`, a DOTween vozi gibanje.
    //
    // Svaki tween nosi .SetLink(target.gameObject): ako se objekt unisti usred
    // animacije, DOTween sam ubije tween umjesto da se buni zbog mrtvog objekta.
    public static class Anim
    {
        // ---- draw: card uleti uz mali okret i povecanje ----
        public static IEnumerator DrawToHand(RectTransform rt, Vector2 fromOffset, float dur = 0.4f)
        {
            if (rt == null) yield break;
            Vector2 home = rt.anchoredPosition;
            rt.anchoredPosition = home + fromOffset;
            rt.localScale = Vector3.one * 0.7f;
            rt.localRotation = Quaternion.Euler(0, 0, -12f);

            rt.DOAnchorPos(home, dur).SetEase(Ease.OutCubic).SetLink(rt.gameObject);
            rt.DOScale(1f, dur).SetEase(Ease.OutBack).SetLink(rt.gameObject);
            rt.DOLocalRotate(Vector3.zero, dur).SetEase(Ease.OutCubic).SetLink(rt.gameObject);
            yield return Wait(dur);
            if (rt != null) { rt.anchoredPosition = home; rt.localScale = Vector3.one; rt.localRotation = Quaternion.identity; }
        }

        // staro ime zadrzano da stariji pozivatelji jos kompajliraju; obicni slide-in
        public static IEnumerator PopIn(RectTransform rt, Vector2 fromOffset, float dur = 0.22f)
            => DrawToHand(rt, fromOffset, dur);

        // ---- summon: card naraste iz male uz opruzni prebacaj ----
        public static IEnumerator PopScale(RectTransform rt, float dur = 0.26f)
        {
            if (rt == null) yield break;
            rt.localScale = Vector3.one * 0.5f;
            rt.DOScale(1f, dur).SetEase(Ease.OutBack).SetLink(rt.gameObject);
            yield return Wait(dur);
            if (rt != null) rt.localScale = Vector3.one;
        }

        // brzi punch-scale kao potvrda klika
        public static IEnumerator Punch(RectTransform rt, float strength = 0.16f, float dur = 0.24f)
        {
            if (rt == null) yield break;
            rt.DOPunchScale(Vector3.one * strength, dur, 6, 0.8f).SetLink(rt.gameObject);
            yield return Wait(dur);
            if (rt != null) rt.localScale = Vector3.one;
        }

        // ---- attack: lagano se povuci, zaleti u metu s nagibom, pa trzaj natrag ----
        public static IEnumerator Lunge(RectTransform rt, Vector3 targetWorldPos, float dur = 0.34f)
        {
            if (rt == null) yield break;
            Vector3 home = rt.position;
            Vector3 toward = (targetWorldPos - home);
            Vector3 peak = home + toward * 0.55f;
            float lean = toward.x >= 0 ? 12f : -12f;

            rt.DOMove(home - toward.normalized * 20f, dur * 0.25f).SetEase(Ease.OutQuad).SetLink(rt.gameObject);
            rt.DOLocalRotate(new Vector3(0, 0, -lean * 0.4f), dur * 0.25f).SetLink(rt.gameObject);
            yield return Wait(dur * 0.25f);
            if (rt == null) yield break;

            rt.DOMove(peak, dur * 0.30f).SetEase(Ease.InCubic).SetLink(rt.gameObject);
            rt.DOLocalRotate(new Vector3(0, 0, lean), dur * 0.30f).SetLink(rt.gameObject);
            yield return Wait(dur * 0.30f);
            if (rt == null) yield break;

            rt.DOMove(home, dur * 0.45f).SetEase(Ease.OutBack).SetLink(rt.gameObject);
            rt.DOLocalRotate(Vector3.zero, dur * 0.45f).SetLink(rt.gameObject);
            yield return Wait(dur * 0.45f);
            if (rt != null) { rt.position = home; rt.localRotation = Quaternion.identity; }
        }

        // ---- death: card se trgne, zavrti, smanji i izblijedi, pa se unisti ----
        public static IEnumerator Death(RectTransform rt, float dur = 0.45f)
        {
            if (rt == null) yield break;
            var cg = rt.GetComponent<CanvasGroup>();
            if (cg == null) cg = rt.gameObject.AddComponent<CanvasGroup>();

            rt.DOScale(1.15f, dur * 0.2f).SetEase(Ease.OutQuad).SetLink(rt.gameObject);
            yield return Wait(dur * 0.2f);
            if (rt == null) yield break;

            rt.DOScale(0.1f, dur * 0.8f).SetEase(Ease.InBack).SetLink(rt.gameObject);
            rt.DOLocalRotate(new Vector3(0, 0, Random.Range(-60f, 60f)), dur * 0.8f).SetLink(rt.gameObject);
            cg.DOFade(0f, dur * 0.8f).SetLink(rt.gameObject);
            yield return Wait(dur * 0.8f);
            if (rt != null) Object.Destroy(rt.gameObject);
        }

        // opadajuce tresenje za pogodak
        public static IEnumerator Shake(RectTransform rt, float amount = 14f, float dur = 0.3f)
        {
            if (rt == null) yield break;
            rt.DOShakeAnchorPos(dur, new Vector2(amount, amount * 0.4f), 14, 90, false, true)
              .SetLink(rt.gameObject);
            yield return Wait(dur);
        }

        // obojaj Image prema boji i natrag: damage (crveno) / heal (zeleno) bljesak
        public static IEnumerator Flash(Image img, Color flash, float dur = 0.3f)
        {
            if (img == null) yield break;
            Color baseColor = img.color;
            img.DOColor(flash, dur * 0.5f).SetLink(img.gameObject);
            yield return Wait(dur * 0.5f);
            if (img == null) yield break;
            img.DOColor(baseColor, dur * 0.5f).SetLink(img.gameObject);
            yield return Wait(dur * 0.5f);
            if (img != null) img.color = baseColor;
        }

        // fade CanvasGroupa izmedu dvije alfe
        public static IEnumerator Fade(CanvasGroup cg, float from, float to, float dur = 0.25f)
        {
            if (cg == null) yield break;
            cg.alpha = from;
            cg.DOFade(to, dur).SetEase(Ease.OutCubic).SetLink(cg.gameObject);
            yield return Wait(dur);
            if (cg != null) cg.alpha = to;
        }

        // damage ili heal brojka koja odleti gore, izblijedi i sama se makne
        public static IEnumerator FloatText(Text label, float rise = 70f, float dur = 0.95f)
        {
            if (label == null) yield break;
            var rt = label.rectTransform;
            Vector2 start = rt.anchoredPosition;

            rt.DOAnchorPosY(start.y + rise, dur).SetEase(Ease.OutCubic).SetLink(label.gameObject);
            rt.DOScale(1.25f, dur * 0.3f).SetEase(Ease.OutBack).SetLink(label.gameObject);
            label.DOFade(0f, dur).SetEase(Ease.InCubic).SetLink(label.gameObject);
            yield return Wait(dur);
            if (label != null) Object.Destroy(label.gameObject);
        }

        // ---- banner: naslov uleti odozgo, kratko stoji, pa odleti i nestane ----
        // Koristi se za VICTORY/DEFEAT i za "YOUR TURN"/"ENEMY TURN".
        public static IEnumerator Banner(RectTransform rt, float hold = 0.7f,
            float from = 140f, float exit = -60f)
        {
            if (rt == null) yield break;
            var cg = rt.GetComponent<CanvasGroup>();
            if (cg == null) cg = rt.gameObject.AddComponent<CanvasGroup>();

            Vector2 home = rt.anchoredPosition;
            rt.anchoredPosition = home + new Vector2(0, from);
            rt.localScale = new Vector3(1.4f, 1.4f, 1f);
            cg.alpha = 0f;

            // ulaz: padne na mjesto i slegne se (OutBack = mali prebacaj)
            rt.DOAnchorPos(home, 0.30f).SetEase(Ease.OutCubic).SetLink(rt.gameObject);
            rt.DOScale(1f, 0.30f).SetEase(Ease.OutBack).SetLink(rt.gameObject);
            cg.DOFade(1f, 0.18f).SetLink(rt.gameObject);
            yield return Wait(0.30f + hold);
            if (rt == null) yield break;

            // izlaz: lagano odlebdi gore i izblijedi
            rt.DOAnchorPosY(home.y - exit, 0.35f).SetEase(Ease.InCubic).SetLink(rt.gameObject);
            cg.DOFade(0f, 0.35f).SetLink(rt.gameObject);
            yield return Wait(0.35f);
            if (rt != null) Object.Destroy(rt.gameObject);
        }

        // ---- linija koja se razvuce iz sredine (ukras ispod bannera) ----
        public static IEnumerator Sweep(RectTransform rt, float width, float dur = 0.4f)
        {
            if (rt == null) yield break;
            rt.sizeDelta = new Vector2(0, rt.sizeDelta.y);
            rt.DOSizeDelta(new Vector2(width, rt.sizeDelta.y), dur)
              .SetEase(Ease.OutCubic).SetLink(rt.gameObject);
            yield return Wait(dur);
        }

        // plain scaled-time wait; tweens clean themselves up via SetLink
        private static IEnumerator Wait(float dur)
        {
            float t = 0f;
            while (t < dur) { t += Time.deltaTime; yield return null; }
        }
    }
}
