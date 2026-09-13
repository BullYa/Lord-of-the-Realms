using UnityEngine;

namespace LordOfTheRealms
{
    // Proceduralni sprajtovi za UI. Projekt nema art assete ni prefabe, pa se sve
    // crta iz koda: generira se jednom, kesira i dijeli izmedu svih scena.
    // Stil: medieval fantasy pixel art, mjedeni okviri s tvrdim pikselima (point
    // filter), sjaj i vinjeta glatki (bilinear) da ne stvaraju stepenice.
    public static class UISprites
    {
        private static Sprite _glow, _frame, _streak, _vignette;

        // ---- ember sjaj: mekana elipsa, sira po X (svjetlo iza natpisa) ----
        public static Sprite Glow
        {
            get
            {
                if (_glow != null) return _glow;
                const int S = 128;
                var tex = new Texture2D(S, S, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Bilinear, // sjaj mora biti gladak
                    wrapMode = TextureWrapMode.Clamp,
                };
                var px = new Color32[S * S];
                float mid = (S - 1) * 0.5f;
                for (int y = 0; y < S; y++)
                {
                    for (int x = 0; x < S; x++)
                    {
                        float dx = (x - mid) / (S * 0.5f);
                        float dy = (y - mid) / (S * 0.5f * 0.60f); // spljosteno = izduzen sjaj
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        float a = Mathf.Clamp01(1f - d);
                        a = a * a * a; // brzi pad -> jezgra svijetli, rub se gubi
                        px[y * S + x] = new Color(1f, 1f, 1f, a);
                    }
                }
                tex.SetPixels32(px);
                tex.Apply();
                _glow = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f);
                return _glow;
            }
        }

        // ---- mjedeni pixel okvir (9-slice): tamni rub, mjed, highlight, sjena ----
        public static Sprite Frame
        {
            get
            {
                if (_frame != null) return _frame;
                const int S = 24, B = 6; // border < S/2
                var tex = new Texture2D(S, S, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point, // tvrdi pikseli = pixel art
                    wrapMode = TextureWrapMode.Clamp,
                };
                var px = new Color32[S * S];
                for (int y = 0; y < S; y++)
                {
                    for (int x = 0; x < S; x++)
                    {
                        int d = Mathf.Min(Mathf.Min(x, S - 1 - x), Mathf.Min(y, S - 1 - y));
                        Color c;
                        if (d == 0) c = new Color(0.04f, 0.03f, 0.05f, 1f);      // vanjska kontura
                        else if (d == 1) c = new Color(0.78f, 0.62f, 0.32f, 1f); // mjed (svijetlo)
                        else if (d == 2) c = new Color(0.42f, 0.32f, 0.17f, 1f); // mjed (tamno)
                        else if (d == 3) c = new Color(0.09f, 0.07f, 0.10f, 1f); // unutarnja sjena
                        else c = new Color(0f, 0f, 0f, 0f);                      // sredina prozirna
                        px[y * S + x] = c;
                    }
                }
                tex.SetPixels32(px);
                tex.Apply();
                _frame = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f,
                    0, SpriteMeshType.FullRect, new Vector4(B, B, B, B));
                return _frame;
            }
        }

        // ---- tanka crta koja se gubi prema krajevima (hover streak) ----
        public static Sprite Streak
        {
            get
            {
                if (_streak != null) return _streak;
                const int W = 64, H = 4;
                var tex = new Texture2D(W, H, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                };
                var px = new Color32[W * H];
                float mid = (W - 1) * 0.5f;
                for (int x = 0; x < W; x++)
                {
                    float a = 1f - Mathf.Abs(x - mid) / mid;
                    a = a * a;
                    for (int y = 0; y < H; y++)
                    {
                        // srednja dva reda puna, vanjska poluprozirna -> mekani rub
                        float rowMul = (y == 0 || y == H - 1) ? 0.35f : 1f;
                        px[y * W + x] = new Color(1f, 1f, 1f, a * rowMul);
                    }
                }
                tex.SetPixels32(px);
                tex.Apply();
                _streak = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 100f);
                return _streak;
            }
        }

        // ---- vinjeta: tamni rubovi ekrana, sredina cista ----
        public static Sprite Vignette
        {
            get
            {
                if (_vignette != null) return _vignette;
                const int S = 128;
                var tex = new Texture2D(S, S, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                };
                var px = new Color32[S * S];
                float mid = (S - 1) * 0.5f;
                for (int y = 0; y < S; y++)
                {
                    for (int x = 0; x < S; x++)
                    {
                        float dx = (x - mid) / (S * 0.5f);
                        float dy = (y - mid) / (S * 0.5f);
                        float d = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy) / 1.414f);
                        float a = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((d - 0.45f) / 0.55f));
                        px[y * S + x] = new Color(0f, 0f, 0f, a * 0.85f);
                    }
                }
                tex.SetPixels32(px);
                tex.Apply();
                _vignette = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f);
                return _vignette;
            }
        }
    }
}
