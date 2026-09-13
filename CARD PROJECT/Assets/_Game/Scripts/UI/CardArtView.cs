using UnityEngine;
using UnityEngine.UI;

namespace LordOfTheRealms
{
    // Crta lice jedne card na RectTransform. Koristi ga i collection grid i cards
    // u mecu, da svaka card izgleda isto.
    //
    // NAPOMENA o layoutu: elementi koji idu cijelom sirinom card koriste RASTEGNUTE
    // horizontalne anchore (min.x=0, max.x=1) uz negativan sizeDelta.x kao marginu.
    // Tockasto usidreni elementi (npr. cost box) uvijek dobivaju izricite pozitivne
    // velicine. Kad se to dvoje pomijesa, element nestane jer dobije negativnu
    // sirinu, i to je bug koji je ova verzija popravila.
    //
    // Layout odozgo prema dolje:
    //   art placeholder (puna sirina), cost box preklapa gornji lijevi kut
    //   ime
    //   footer po vrsti card:
    //     unit  = uokvireni box s tipom i attack/health
    //     spell = tip spella i kratki opis efekta
    //     hero  = passive ili on-enter linija i attack/health
    public static class CardArtView
    {
        public static void Paint(RectTransform tile, CardData card, bool greyed = false)
        {
            var frame = tile.GetComponent<Image>();
            if (frame == null) frame = tile.gameObject.AddComponent<Image>();
            frame.color = greyed ? new Color(0.18f, 0.18f, 0.2f, 0.95f) : RaceColor(card.race);

            // golden okvir se sada radi bojom mjedenog okvira (dolje), ne Outlineom

            Color ink = greyed ? new Color(0.55f, 0.55f, 0.55f) : UIFactory.Ink;
            Color dim = greyed ? new Color(0.5f, 0.5f, 0.5f) : new Color(0.9f, 0.9f, 0.78f);

            // Art se sastoji od dva sloja. Banner je pozadina i ide preko cijelog
            // okvira, sprite je lik i stoji centriran preko njega. Slojevi se
            // ucitavaju odvojeno, pa se pozadina rase moze zamijeniti bez diranja
            // likova, i obrnuto. Ako u korijenu mape stoji stara jednodijelna
            // slika istog imena, ona ima prednost i crta se sama.
            var art = UIFactory.CreatePanel(tile, "Art", greyed
                ? new Color(0.12f, 0.12f, 0.14f)
                : new Color(0f, 0f, 0f, 0.30f));
            StretchTop(art, topOffset: -8, height: 78, sideMargin: 16);

            var combined = LoadArt(card);
            var banner = combined == null ? LoadBanner(card) : null;
            var figure = combined == null ? LoadSprite(card) : null;
            Color artTint = greyed ? new Color(0.45f, 0.45f, 0.45f, 1f) : Color.white;

            if (combined != null)
            {
                var img = art.GetComponent<Image>();
                img.sprite = combined;
                img.preserveAspect = true;
                img.color = artTint;
            }
            else if (banner != null || figure != null)
            {
                if (banner != null)
                {
                    var img = art.GetComponent<Image>();
                    img.sprite = banner;
                    // banner popunjava cijeli okvir, zato bez preserveAspect
                    img.preserveAspect = false;
                    img.color = artTint;
                }
                if (figure != null)
                {
                    var fig = UIFactory.CreatePanel(art, "Figure", new Color(0f, 0f, 0f, 0f));
                    StretchAll(fig);
                    var fimg = fig.GetComponent<Image>();
                    fimg.sprite = figure;
                    // lik ostaje kvadratan bez obzira na oblik okvira
                    fimg.preserveAspect = true;
                    fimg.color = artTint;
                    fimg.raycastTarget = false;
                }
            }
            else
            {
                var artLabel = UIFactory.CreateText(art, "ArtLabel", "[ art ]", 12,
                    TextAnchor.MiddleCenter, new Color(0.55f, 0.55f, 0.55f));
                StretchAll(artLabel.rectTransform);
            }

            // cost box: fiksna velicina, preko gornjeg lijevog kuta arta
            var costBg = UIFactory.CreatePanel(tile, "CostBg",
                greyed ? new Color(0.1f, 0.1f, 0.12f, 0.95f) : new Color(0.1f, 0.16f, 0.28f, 0.98f));
            costBg.anchorMin = new Vector2(0, 1);
            costBg.anchorMax = new Vector2(0, 1);
            costBg.pivot = new Vector2(0, 1);
            costBg.anchoredPosition = new Vector2(4, -4);
            costBg.sizeDelta = new Vector2(30, 30);
            var cost = UIFactory.CreateText(costBg, "Cost", $"{card.cost}", 20,
                TextAnchor.MiddleCenter, greyed ? new Color(0.6f, 0.7f, 0.8f) : new Color(0.85f, 0.95f, 1f));
            StretchAll(cost.rectTransform);

            // drain badge: crveni box gore-desno, zrcalno cost boxu; oznacava karte
            // koje uz cost trajno spuste tvoj MAX power (tooltip objasnjava detalje)
            if (card.maxPowerCost > 0)
            {
                var drainBg = UIFactory.CreatePanel(tile, "DrainBg",
                    greyed ? new Color(0.14f, 0.08f, 0.08f, 0.95f) : new Color(0.42f, 0.08f, 0.10f, 0.98f));
                drainBg.anchorMin = new Vector2(1, 1);
                drainBg.anchorMax = new Vector2(1, 1);
                drainBg.pivot = new Vector2(1, 1);
                drainBg.anchoredPosition = new Vector2(-4, -4);
                drainBg.sizeDelta = new Vector2(30, 30);
                var drain = UIFactory.CreateText(drainBg, "Drain", $"-{card.maxPowerCost}", 16,
                    TextAnchor.MiddleCenter, greyed ? new Color(0.75f, 0.55f, 0.55f) : new Color(1f, 0.72f, 0.62f));
                StretchAll(drain.rectTransform);
            }

            // ime: rastegnuto, odmah ispod arta. BestFit smanjuje duga imena
            // ("Baelric the Lightbringer") da nikad ne iscure iz svoje trake.
            var nm = UIFactory.CreateText(tile, "Name", card.cardName, 14, TextAnchor.MiddleCenter, ink);
            nm.horizontalOverflow = HorizontalWrapMode.Wrap;
            nm.verticalOverflow = VerticalWrapMode.Truncate;
            nm.resizeTextForBestFit = true;
            nm.resizeTextMinSize = 9;
            nm.resizeTextMaxSize = 14;
            StretchTop(nm.rectTransform, topOffset: -88, height: 32, sideMargin: 6);

            switch (card)
            {
                case UnitCardData u: PaintUnitFooter(tile, u, dim, greyed); break;
                case HeroCardData h: PaintHeroFooter(tile, h, greyed); break;
                case SpellCardData s: PaintSpellFooter(tile, s, greyed); break;
            }

            // Mjedeni okvir ide ZADNJI da bude iznad arta i footera. Boja nosi rang:
            // zlatna = rasa s golden unlockom, svijetla = heroj, hladna = spell,
            // obicna mjed = unit.
            var fr = UIFactory.AddFrame(tile);
            fr.color = FrameTint(card, greyed);
        }

        // boja okvira po "rangu" karte
        private static Color FrameTint(CardData card, bool greyed)
        {
            if (greyed) return new Color(0.55f, 0.55f, 0.60f, 0.45f);
            if (GoldenFrames.Has(card.race)) return new Color(1f, 0.86f, 0.42f, 1f);
            return card switch
            {
                HeroCardData => new Color(1f, 0.93f, 0.72f, 1f),
                SpellCardData => new Color(0.74f, 0.87f, 1f, 1f),
                _ => Color.white,
            };
        }

        // unit: uokvireni box dolje s tipom, keyword oznakom i attack/health
        private static void PaintUnitFooter(RectTransform tile, UnitCardData u, Color dim, bool greyed)
        {
            var box = UIFactory.CreatePanel(tile, "StatBox",
                greyed ? new Color(0.12f, 0.12f, 0.14f, 0.9f) : new Color(0f, 0f, 0f, 0.4f));
            StretchBottom(box, bottomOffset: 6, height: 46, sideMargin: 14);

            // "Basic · Insight" kad jedinica ima efekt; tag odmah kaze DA nesto radi,
            // tooltip objasnjava sto tocno
            string kw = KeywordFor(u.ability);
            string typeLine = kw == null ? u.unitType.ToString() : $"{u.unitType} · {kw}";
            var type = UIFactory.CreateText(box, "Type", typeLine, 12,
                TextAnchor.UpperCenter,
                kw != null && !greyed ? new Color(1f, 0.86f, 0.55f) : dim);
            type.resizeTextForBestFit = true;
            type.resizeTextMinSize = 8;
            type.resizeTextMaxSize = 12;
            StretchTop(type.rectTransform, topOffset: -3, height: 18, sideMargin: 4);

            var stats = UIFactory.CreateText(box, "Stats", $"{u.attack} / {u.health}", 18,
                TextAnchor.LowerCenter, greyed ? new Color(0.6f, 0.6f, 0.6f) : new Color(1f, 0.97f, 0.85f));
            StretchBottom(stats.rectTransform, bottomOffset: 3, height: 22, sideMargin: 4);
        }

        // kratki keyword za ability, pise se na karti pored tipa jedinice
        public static string KeywordFor(CardAbility ab) => ab switch
        {
            CardAbility.Heal => "Mender",
            CardAbility.Draw => "Insight",
            CardAbility.PowerRamp => "Ritual",
            CardAbility.BuffAttack => "Inspire",
            CardAbility.Aoe => "Nova",
            CardAbility.ChargeFace => "Charge",
            CardAbility.Lifesteal => "Lifesteal",
            CardAbility.DivineShield => "Shield",
            CardAbility.HealAllUnits => "Renewal",
            _ => null
        };

        // heroj: passive ili on-enter linija, pa attack/health ispod nje
        private static void PaintHeroFooter(RectTransform tile, HeroCardData h, bool greyed)
        {
            var box = UIFactory.CreatePanel(tile, "HeroBox",
                greyed ? new Color(0.12f, 0.12f, 0.14f, 0.9f) : new Color(0.16f, 0.09f, 0.02f, 0.6f));
            StretchBottom(box, bottomOffset: 6, height: 62, sideMargin: 10);

            string modeTag = h.abilityMode == HeroAbilityMode.Passive ? "PASSIVE" : "ON ENTER";
            var power = UIFactory.CreateText(box, "Power",
                $"{modeTag}: {ShortAbility(h)}", 11, TextAnchor.UpperCenter,
                greyed ? new Color(0.55f, 0.55f, 0.55f) : new Color(1f, 0.88f, 0.6f));
            power.horizontalOverflow = HorizontalWrapMode.Wrap;
            power.verticalOverflow = VerticalWrapMode.Truncate;
            power.resizeTextForBestFit = true;
            power.resizeTextMinSize = 8;
            power.resizeTextMaxSize = 11;
            StretchTop(power.rectTransform, topOffset: -3, height: 36, sideMargin: 4);

            var stats = UIFactory.CreateText(box, "Stats", $"{h.attack} / {h.health}", 16,
                TextAnchor.LowerCenter, greyed ? new Color(0.6f, 0.6f, 0.6f) : new Color(1f, 0.97f, 0.85f));
            StretchBottom(stats.rectTransform, bottomOffset: 3, height: 20, sideMargin: 4);
        }

        // spell: tip i kratka linija o tome sto radi
        private static void PaintSpellFooter(RectTransform tile, SpellCardData s, bool greyed)
        {
            string typeWord = s.spellType.ToString();
            var body = UIFactory.CreateText(tile, "Body",
                $"{typeWord}:\n{SpellEffectText(s)}", 12, TextAnchor.UpperCenter,
                greyed ? new Color(0.55f, 0.55f, 0.55f) : new Color(0.93f, 0.93f, 0.83f));
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
            body.verticalOverflow = VerticalWrapMode.Truncate;
            body.resizeTextForBestFit = true;
            body.resizeTextMinSize = 9;
            body.resizeTextMaxSize = 12;
            StretchBottom(body.rectTransform, bottomOffset: 8, height: 72, sideMargin: 10);
        }

        // ---- anchor pomocnici (najvazniji dio) ----

        // puna sirina minus margine, visi s vrha parenta
        private static void StretchTop(RectTransform rt, float topOffset, float height, float sideMargin)
        {
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1);
            rt.anchoredPosition = new Vector2(0, topOffset);
            rt.sizeDelta = new Vector2(-sideMargin * 2, height);
        }

        // puna sirina minus margine, sjedi na dnu parenta
        private static void StretchBottom(RectTransform rt, float bottomOffset, float height, float sideMargin)
        {
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(1, 0);
            rt.pivot = new Vector2(0.5f, 0);
            rt.anchoredPosition = new Vector2(0, bottomOffset);
            rt.sizeDelta = new Vector2(-sideMargin * 2, height);
        }

        private static void StretchAll(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        // ---- text helpers ----

        private static string ShortAbility(HeroCardData h) => h.ability switch
        {
            CardAbility.PassiveBuffAllies => $"allies +{h.abilityValue} atk",
            CardAbility.PassiveRampEachTurn => $"+{h.abilityValue} POWER/turn",
            CardAbility.PassiveHealEachTurn => $"heal {h.abilityValue}/turn",
            CardAbility.PassiveTauntShield => $"allies take -{h.abilityValue} dmg",
            CardAbility.Heal => $"heal {h.abilityValue}",
            CardAbility.Aoe => $"{h.abilityValue} to all enemies",
            CardAbility.PowerRamp => $"+{h.abilityValue} POWER",
            CardAbility.ChargeFace => "attacks at once",
            CardAbility.Draw => $"draw {h.abilityValue}",
            _ => "-"
        };

        private static string SpellEffectText(SpellCardData s)
        {
            if (s.spellType == SpellType.Offensive)
            {
                string extra = s.ability == CardAbility.Aoe ? $" +{s.abilityValue} all" : "";
                return $"deal {s.power} dmg{extra}";
            }
            string ab = s.ability switch
            {
                CardAbility.Heal => $"restore {s.abilityValue} hp",
                CardAbility.Draw => $"draw {s.abilityValue}",
                CardAbility.PowerRamp => $"+{s.abilityValue} POWER",
                CardAbility.BuffAttack => $"units +{s.abilityValue} atk",
                CardAbility.DamageEqualsMaxPower => "dmg = max POWER",
                CardAbility.KillDraw => $"deal {s.power}, kill = draw",
                CardAbility.RefreshAttacks => "units attack again",
                CardAbility.HealAllUnits => "heal units to full",
                CardAbility.ExecuteStrongest => "execute strongest",
                CardAbility.SacrificeDraw => $"sacrifice, draw {s.abilityValue}",
                CardAbility.AoeAllDraw => $"{s.abilityValue} dmg ALL, draw 2",
                CardAbility.SummonTokens => $"summon {s.abilityValue}x 1/1",
                _ => "field effect"
            };
            return s.canBePlayedHidden ? ab + " (hidden trap)" : ab;
        }

        // ---- art ucitavanje (cache; null se pamti da ne trazimo svaki put) ----
        //
        // Tri putanje, po prioritetu:
        //   CardArt/{Race}_{Ime}          stara jednodijelna slika, ako je ostala
        //   CardArt/Banner/Banner_{Race}_{n}   pozadina, dijeli je vise karata
        //   CardArt/Sprite/{Race}_{Ime}        lik, jedan po karti

        private static readonly System.Collections.Generic.Dictionary<string, Sprite> _artCache = new();

        // koliko varijanti pozadine postoji po rasi
        public const int BannerVariants = 3;

        public static string BannerName(Race race, int variant) => $"Banner_{race}_{variant}";

        // Karta bira svoju pozadinu po imenu. Racuna se vlastitim hashom, jer
        // string.GetHashCode nije zajamceno isti u editoru i u buildu, pa bi karta
        // mogla dobiti jednu pozadinu pri generiranju, a drugu pri igranju.
        public static int StableHash(string s)
        {
            unchecked
            {
                int h = 23;
                foreach (char c in s) h = h * 31 + c;
                return h & 0x7FFFFFFF;
            }
        }

        private static Sprite Cached(string path)
        {
            if (_artCache.TryGetValue(path, out var hit)) return hit;
            var sp = Resources.Load<Sprite>(path);
            _artCache[path] = sp;
            return sp;
        }

        public static Sprite LoadArt(CardData card) =>
            Cached($"CardArt/{card.race}_{SafeName(card.cardName)}");

        public static Sprite LoadBanner(CardData card)
        {
            int v = StableHash(card.cardName) % BannerVariants;
            return Cached("CardArt/Banner/" + BannerName(card.race, v));
        }

        public static Sprite LoadSprite(CardData card) =>
            Cached($"CardArt/Sprite/{card.race}_{SafeName(card.cardName)}");

        // isto pravilo imena kao u ArtPlaceholderGeneratoru
        private static string SafeName(string n) =>
            n.Replace(",", "").Replace(" ", "_").Replace("'", "");

        public static Color RaceColor(Race race) => race switch
        {
            Race.Orcs => new Color(0.40f, 0.50f, 0.22f),
            Race.Elves => new Color(0.20f, 0.45f, 0.38f),
            Race.Humans => new Color(0.30f, 0.38f, 0.55f),
            Race.Demons => new Color(0.50f, 0.18f, 0.24f),
            _ => Color.gray
        };
    }
}
