#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace LordOfTheRealms.EditorTools
{
    // Generira pixel-art placeholdere za SVE karte, razdvojene u dva sloja:
    //
    //   Assets/Resources/CardArt/Banner/Banner_{Race}_{n}.png   96x64, neprozirno
    //   Assets/Resources/CardArt/Sprite/{Race}_{Card_Name}.png  64x64, prozirno
    //
    // Banner je pozadina: gradijent u boji rase, dither i tanki pojas svjetla.
    // Po rasi se generiraju tri varijante, a karta bira svoju po imenu, pa dvije
    // karte iste rase ne stoje uvijek na istoj pozadini. Sprite je samo lik,
    // bez pozadine, s prozirnim rubom. CardArtView ih slaze jedan preko drugoga.
    //
    // Razdvajanje znaci da se pozadina i lik mogu mijenjati odvojeno. Nova
    // pozadina za cijelu rasu je jedna datoteka umjesto trideset, a nacrtani lik
    // se ubacuje tako da se prepise sprite istog imena, dok pozadina ostaje.
    //
    // Pokreni: Lord of the Realms > Generate Card Art Placeholders
    public static class ArtPlaceholderGenerator
    {
        private const string RootDir = "Assets/Resources/CardArt";
        private const string BannerDir = RootDir + "/Banner";
        private const string SpriteDir = RootDir + "/Sprite";
        private const int SpriteSize = 64;   // lik
        private const int BannerW = 96;      // pozadina je siroka kao art okvir
        private const int BannerH = 64;
        private const int Scale = 4;         // 13x13 glyph * 4 = 52px, centriran

        [MenuItem("Lord of the Realms/Generate Card Art Placeholders")]
        public static void Generate()
        {
            Directory.CreateDirectory(BannerDir);
            Directory.CreateDirectory(SpriteDir);

            // 1) banneri: tri varijante po rasi
            int banners = 0;
            foreach (Race race in System.Enum.GetValues(typeof(Race)))
            {
                for (int v = 0; v < CardArtView.BannerVariants; v++)
                {
                    var bt = DrawBanner(race, v);
                    File.WriteAllBytes($"{BannerDir}/{CardArtView.BannerName(race, v)}.png",
                                       bt.EncodeToPNG());
                    Object.DestroyImmediate(bt);
                    banners++;
                }
            }

            // 2) sprite po karti
            var guids = AssetDatabase.FindAssets("t:CardData", new[] { "Assets/_Game/CardData" });
            int n = 0, removed = 0;
            foreach (var g in guids)
            {
                var card = AssetDatabase.LoadAssetAtPath<CardData>(AssetDatabase.GUIDToAssetPath(g));
                if (card == null) continue;
                var tex = DrawSprite(card);
                File.WriteAllBytes($"{SpriteDir}/{FileName(card)}.png", tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                n++;

                // Stara jednodijelna slika iz ranijih verzija stajala je u korijenu
                // mape i imala bi prednost pri ucitavanju, pa bi razdvajanje ostalo
                // nevidljivo. Zato se uklanja.
                string old = $"{RootDir}/{FileName(card)}.png";
                if (File.Exists(old) && AssetDatabase.DeleteAsset(old)) removed++;
            }
            AssetDatabase.Refresh();

            // point filter + bez kompresije = ostar pixel look
            ApplyImportSettings(BannerDir, alpha: false);
            ApplyImportSettings(SpriteDir, alpha: true);

            EditorUtility.DisplayDialog("Lord of the Realms",
                $"{banners} bannera i {n} spriteova generirano.\n"
                + $"Uklonjeno starih jednodijelnih slika: {removed}.", "OK");
        }

        private static void ApplyImportSettings(string dir, bool alpha)
        {
            foreach (var file in Directory.GetFiles(dir, "*.png"))
            {
                var imp = AssetImporter.GetAtPath(file.Replace('\\', '/')) as TextureImporter;
                if (imp == null) continue;
                imp.textureType = TextureImporterType.Sprite;
                imp.filterMode = FilterMode.Point;
                imp.textureCompression = TextureImporterCompression.Uncompressed;
                imp.alphaIsTransparency = alpha;
                imp.SaveAndReimport();
            }
        }

        // isto pravilo imena koristi i CardArtView pri ucitavanju
        private static string FileName(CardData c) =>
            $"{c.race}_{c.cardName.Replace(",", "").Replace(" ", "_").Replace("'", "")}";

        // ---- banner (pozadina) ----

        private static Texture2D DrawBanner(Race race, int variant)
        {
            var tex = new Texture2D(BannerW, BannerH, TextureFormat.RGBA32, false);
            Color baseCol = CardArtView.RaceColor(race);
            int seed = CardArtView.StableHash(race.ToString() + variant);

            // Varijante se razlikuju visinom pojasa svjetla. Time pozadine iste rase
            // ostaju povezane bojom, ali se ne cine kao ista slika ponovljena.
            float horizon = 0.28f + 0.22f * variant;

            for (int y = 0; y < BannerH; y++)
            {
                float t = y / (float)(BannerH - 1);
                Color row = Color.Lerp(baseCol * 0.28f, baseCol * 0.60f, t);
                float glow = Mathf.Exp(-Mathf.Abs(t - horizon) * 9f) * 0.30f;
                row = Color.Lerp(row, baseCol * 1.10f, glow);

                for (int x = 0; x < BannerW; x++)
                {
                    // rubovi su tamniji, pa lik u sredini bolje odskace
                    float ex = Mathf.Abs(x / (float)(BannerW - 1) - 0.5f) * 2f;
                    Color c = Color.Lerp(row, row * 0.72f, ex * ex * 0.8f);

                    int h = Hash(x, y, seed);
                    float d = ((h & 15) / 15f - 0.5f) * 0.06f;   // -0.03..+0.03
                    c = new Color(Mathf.Clamp01(c.r + d),
                                  Mathf.Clamp01(c.g + d),
                                  Mathf.Clamp01(c.b + d), 1f);
                    if ((h & 1023) == 7) c = Color.Lerp(c, Color.white, 0.35f);
                    tex.SetPixel(x, y, c);
                }
            }
            tex.Apply();
            return tex;
        }

        // ---- sprite (lik bez pozadine) ----

        private static Texture2D DrawSprite(CardData card)
        {
            var tex = new Texture2D(SpriteSize, SpriteSize, TextureFormat.RGBA32, false);
            int seed = card.cardName.GetHashCode();

            var clear = new Color(0f, 0f, 0f, 0f);
            for (int y = 0; y < SpriteSize; y++)
                for (int x = 0; x < SpriteSize; x++)
                    tex.SetPixel(x, y, clear);

            // Glif po tipu daje SILUETU (odmah se vidi je li unit, spell ili heroj),
            // a preko njega ide uzorak izveden iz imena karte, pa dvije karte istog
            // tipa vise ne izgledaju identicno.
            var (glyph, main, shade) = GlyphFor(card);
            Color outline = new Color(0.05f, 0.05f, 0.07f, 1f);
            int gw = glyph[0].Length, gh = glyph.Length;
            int ox = (SpriteSize - gw * Scale) / 2;
            int oy = (SpriteSize - gh * Scale) / 2;

            // naglasna nijansa iz istog seeda: svaka karta ima svoju
            Color accent = AccentFor(seed, CardArtView.RaceColor(card.race));

            for (int gy = 0; gy < gh; gy++)
            {
                // glyph stringovi idu od vrha prema dolje, tekstura od dna -> obrni y
                string rowStr = glyph[gy];
                for (int gx = 0; gx < gw; gx++)
                {
                    char ch = rowStr[gx];
                    if (ch == '.') continue;
                    Color c = ch == 'X' ? main : ch == 'o' ? shade : outline;

                    // Uzorak je zrcalan po sredini glifa, pa izgleda kao namjeran
                    // dizajn (lice, oklop, runa), a ne kao nasumicni sum. Obrub se
                    // ne dira da silueta ostane citka.
                    if (ch != '#')
                    {
                        int mx = gx < gw / 2 ? gx : gw - 1 - gx;
                        int p = Hash(mx, gy, seed) & 7;
                        if (p == 0) c = accent;
                        else if (p == 1) c = Color.Lerp(c, Color.black, 0.35f);
                        else if (p == 2) c = Color.Lerp(c, Color.white, 0.25f);
                    }

                    int px = ox + gx * Scale;
                    int py = oy + (gh - 1 - gy) * Scale;
                    for (int dy = 0; dy < Scale; dy++)
                        for (int dx = 0; dx < Scale; dx++)
                            tex.SetPixel(px + dx, py + dy, c);
                }
            }

            tex.Apply();
            return tex;
        }

        // Naglasna boja izvedena iz imena karte. Svijetla je i dovoljno zasicena da
        // se vidi na tamnoj pozadini, ali povucena prema boji rase da paleta ostane
        // povezana.
        private static Color AccentFor(int seed, Color raceCol)
        {
            unchecked
            {
                // 2246822519 ne stane u int, pa mnozimo kao uint i vracamo natrag
                int h = (int)((uint)(seed ^ 0x5bf03635) * 2246822519u);
                float hue = ((h >> 8) & 255) / 255f;
                var c = Color.HSVToRGB(hue, 0.55f, 1f);
                return Color.Lerp(c, raceCol, 0.35f);
            }
        }

        private static int Hash(int x, int y, int seed)
        {
            unchecked
            {
                int h = x * 73856093 ^ y * 19349663 ^ seed * 83492791;
                h = (h ^ (h >> 13)) * 1274126177;
                return h ^ (h >> 16);
            }
        }

        // koji glyph + boje za koju kartu
        private static (string[] glyph, Color main, Color shade) GlyphFor(CardData card)
        {
            switch (card)
            {
                case HeroCardData _:
                    return (Crown, new Color(0.95f, 0.80f, 0.30f), new Color(0.75f, 0.55f, 0.15f));
                case SpellCardData s:
                    if (s.spellType == SpellType.Offensive)
                        return (Flame, new Color(1f, 0.55f, 0.15f), new Color(1f, 0.85f, 0.35f));
                    if (s.spellType == SpellType.Field)
                        return (Banner, new Color(0.90f, 0.80f, 0.50f), new Color(0.70f, 0.55f, 0.30f));
                    return (Heart, new Color(0.85f, 0.25f, 0.35f), new Color(1f, 0.55f, 0.60f)); // defensive
                case UnitCardData u:
                    if (u.ability == CardAbility.Lifesteal)
                        return (Droplet, new Color(0.75f, 0.20f, 0.55f), new Color(0.95f, 0.45f, 0.75f));
                    if (u.ability == CardAbility.DivineShield)
                        return (Shield, new Color(0.95f, 0.90f, 0.55f), new Color(0.80f, 0.70f, 0.30f));
                    return u.unitType switch
                    {
                        UnitType.Taunt => (Shield, new Color(0.80f, 0.66f, 0.30f), new Color(0.60f, 0.48f, 0.20f)),
                        UnitType.Assassin => (Dagger, new Color(0.72f, 0.76f, 0.84f), new Color(0.45f, 0.50f, 0.60f)),
                        UnitType.Scout => (Eye, new Color(0.92f, 0.92f, 0.96f), new Color(0.55f, 0.75f, 0.85f)),
                        _ => (Sword, new Color(0.76f, 0.80f, 0.88f), new Color(0.50f, 0.55f, 0.65f)),
                    };
            }
            return (Sword, Color.white, Color.gray);
        }

        // ---- 13x13 glyphovi ('.'=prazno, 'X'=glavna, 'o'=sjena/detalj, '#'=obrub) ----

        private static readonly string[] Sword =
        {
            "......#......",
            ".....#X#.....",
            ".....#X#.....",
            ".....#X#.....",
            ".....#X#.....",
            ".....#X#.....",
            ".....#X#.....",
            "..#########..",
            "..#ooooooo#..",
            ".....#X#.....",
            ".....#X#.....",
            "....#ooo#....",
            "....#####....",
        };

        private static readonly string[] Dagger =
        {
            ".............",
            ".............",
            "......#......",
            ".....#X#.....",
            ".....#X#.....",
            ".....#X#.....",
            "...#######...",
            "...#ooooo#...",
            ".....#X#.....",
            "....#ooo#....",
            "....#####....",
            ".............",
            ".............",
        };

        private static readonly string[] Shield =
        {
            "..#########..",
            ".#XXXXXXXXX#.",
            ".#XXXoXXXXX#.",
            ".#XXXXXXXXX#.",
            ".#XoXXXXXoX#.",
            ".#XXXXXXXXX#.",
            "..#XXXXXXX#..",
            "..#XXXXXXX#..",
            "...#XXXXX#...",
            "....#XXX#....",
            ".....#X#.....",
            "......#......",
            ".............",
        };

        private static readonly string[] Eye =
        {
            ".............",
            ".............",
            "...#######...",
            ".##ooooooo##.",
            "#oXXX###XXXo#",
            "#oXX##o##XXo#",
            "#oXXX###XXXo#",
            ".##ooooooo##.",
            "...#######...",
            ".............",
            ".............",
            ".............",
            ".............",
        };

        private static readonly string[] Flame =
        {
            "......#......",
            ".....#X#.....",
            "....#XX#.....",
            "....#XXX#....",
            "...#XXXXX#...",
            "...#XXoXX#...",
            "..#XXooXXX#..",
            "..#XXoooXX#..",
            "..#XXXoXXX#..",
            "...#XXXXX#...",
            "....#XXX#....",
            ".....###.....",
            ".............",
        };

        private static readonly string[] Heart =
        {
            ".............",
            "..###...###..",
            ".#XXX#.#XXX#.",
            "#XXXXX#XXXXX#",
            "#XXoXXXXXXXX#",
            "#XXXXXXXXXXX#",
            ".#XXXXXXXXX#.",
            "..#XXXXXXX#..",
            "...#XXXXX#...",
            "....#XXX#....",
            ".....#X#.....",
            "......#......",
            ".............",
        };

        private static readonly string[] Banner =
        {
            "..#..........",
            "..##########.",
            "..#XXXXXXXX#.",
            "..#XXoXXXXX#.",
            "..#XXXXXXX#..",
            "..#XXXXXX#...",
            "..#XXXXX#....",
            "..#######....",
            "..#..........",
            "..#..........",
            "..#..........",
            "..#..........",
            ".............",
        };

        private static readonly string[] Crown =
        {
            ".............",
            ".............",
            "..#...#...#..",
            ".#X#.#X#.#X#.",
            ".#X###X###X#.",
            ".#XXXXXXXXX#.",
            ".#XXXXXXXXX#.",
            ".#XoXXoXXoX#.",
            ".#XXXXXXXXX#.",
            ".###########.",
            ".............",
            ".............",
            ".............",
        };

        private static readonly string[] Droplet =
        {
            "......#......",
            ".....#X#.....",
            ".....#X#.....",
            "....#XXX#....",
            "....#XXX#....",
            "...#XXXXX#...",
            "...#XXXXX#...",
            "..#XoXXXXX#..",
            "..#XooXXXX#..",
            "..#XXoXXXX#..",
            "...#XXXXX#...",
            "....#####....",
            ".............",
        };
    }
}
#endif
