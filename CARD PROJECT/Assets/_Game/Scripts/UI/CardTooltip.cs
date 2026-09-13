using UnityEngine;
using UnityEngine.UI;

namespace LordOfTheRealms
{
    // Jedan lebdeci tooltip koji na hover pokaze pune detalje karte. Jedna
    // instanca zivi na canvasu; CardView trazi prikaz ili skrivanje. Prati mis i
    // ispisuje ime, tip, cijenu, statove i pravila da igrac uvijek zna
    // sto karta radi, pogotovo za spellove ciji efekt nije vidljiv na plodi.
    //
    // Napomena o nazivlju: efekti na *igraca* (onih 40 HP koje napadas da pobijedis)
    // kazu "player"; "hero" je rezervirano za herojske *karte* poput Kragmawa.
    public class CardTooltip : MonoBehaviour
    {
        private static CardTooltip _instance;
        private RectTransform _rt;
        private Text _title;
        private Text _body;
        private CanvasGroup _group;
        private Canvas _canvas;

        public static CardTooltip Ensure(Canvas canvas)
        {
            if (_instance != null) return _instance;
            var go = new GameObject("CardTooltip", typeof(RectTransform));
            go.transform.SetParent(canvas.transform, false);
            _instance = go.AddComponent<CardTooltip>();
            _instance._canvas = canvas;
            _instance.Build();
            return _instance;
        }

        private void Build()
        {
            _rt = GetComponent<RectTransform>();
            _rt.sizeDelta = new Vector2(320, 200);
            _rt.pivot = new Vector2(0, 1);

            _group = gameObject.AddComponent<CanvasGroup>();
            _group.blocksRaycasts = false; // never eat clicks
            _group.interactable = false;

            var bg = UIFactory.CreatePanel(transform, "Bg", new Color(0.06f, 0.05f, 0.08f, 0.97f));
            UIFactory.Stretch(bg);
            var border = bg.gameObject.AddComponent<Outline>();
            border.effectColor = new Color(0.8f, 0.7f, 0.4f, 0.8f);
            border.effectDistance = new Vector2(2, -2);

            _title = UIFactory.CreateText(transform, "Title", "", 20,
                TextAnchor.UpperLeft, new Color(1f, 0.95f, 0.8f));
            var trt = _title.rectTransform;
            trt.anchorMin = new Vector2(0, 1); trt.anchorMax = new Vector2(1, 1);
            trt.pivot = new Vector2(0.5f, 1);
            trt.anchoredPosition = new Vector2(0, -10);
            trt.sizeDelta = new Vector2(-24, 52);

            _body = UIFactory.CreateText(transform, "Body", "", 16,
                TextAnchor.UpperLeft, new Color(0.9f, 0.9f, 0.85f));
            _body.horizontalOverflow = HorizontalWrapMode.Wrap;
            var brt = _body.rectTransform;
            brt.anchorMin = new Vector2(0, 0); brt.anchorMax = new Vector2(1, 1);
            brt.offsetMin = new Vector2(14, 14);
            brt.offsetMax = new Vector2(-14, -62);

            Hide();
        }

        public void Show(CardData data)
        {
            if (data == null) { Hide(); return; }
            _group.alpha = 1f;

            string type = data.Category.ToString();
            if (data is UnitCardData u) type = string.Format(Localization.T("tt.type_unit"), UnitTypeName(u.unitType));
            else if (data is SpellCardData s) type = string.Format(Localization.T("tt.type_spell"), SpellTypeName(s.spellType));
            else if (data is HeroCardData) type = Localization.T("tt.type_hero");

            _title.text = data.maxPowerCost > 0
                ? $"{data.cardName}  ({data.cost}◆  -{data.maxPowerCost} max)"
                : $"{data.cardName}  ({data.cost}◆)";

            string stats = "";
            if (data is UnitCardData uu) stats = string.Format(Localization.T("tt.stats_unit"), uu.attack, uu.health) + "\n";
            else if (data is HeroCardData hh) stats = string.Format(Localization.T("tt.stats_unit"), hh.attack, hh.health) + "\n";
            else if (data is SpellCardData ss && ss.spellType == SpellType.Offensive)
                stats = string.Format(Localization.T("tt.stats_spell"), ss.power) + "\n";

            string rules = BuildRules(data);

            _body.text = $"{type}\n{stats}\n{rules}".Trim();

            // rastegni panel da stane tekst, da duga pravila i lore nikad ne iscure.
            // Izmjeri prirodnu visinu tijela na fiksnoj sirini omatanja, pa dodaj
            // prostor za naslov (62) i donju marginu (14).
            float wrapWidth = 320f - 28f;
            var settings = _body.GetGenerationSettings(new Vector2(wrapWidth, 0));
            float bodyH = _body.cachedTextGeneratorForLayout.GetPreferredHeight(_body.text, settings)
                          / _body.pixelsPerUnit;
            _rt.sizeDelta = new Vector2(320, Mathf.Max(160, bodyH + 62 + 18));
        }

        private string BuildRules(CardData data)
        {
            var sb = new System.Text.StringBuilder();

            // drain karte: dodatna cijena ide PRVA da se odmah vidi u cemu je kvaka
            if (data.maxPowerCost > 0)
                sb.AppendLine(string.Format(Localization.T("tt.drain"), data.maxPowerCost));

            switch (data)
            {
                case UnitCardData u:
                    switch (u.unitType)
                    {
                        case UnitType.Taunt:
                            sb.AppendLine(Localization.T("tt.taunt")); break;
                        case UnitType.Assassin:
                            sb.AppendLine(Localization.T("tt.assassin")); break;
                        case UnitType.Scout:
                            sb.AppendLine(Localization.T("tt.scout")); break;
                    }
                    // vlastita sposobnost unita (dodaj samo ako je ima)
                    AppendAbility(sb, data);
                    break;
                case SpellCardData s:
                    switch (s.spellType)
                    {
                        case SpellType.Offensive:
                            sb.AppendLine(string.Format(Localization.T("tt.offensive"), s.power)); break;
                        case SpellType.Defensive:
                            if (s.canBePlayedHidden)
                                sb.AppendLine(string.Format(Localization.T("tt.hidden"), s.power));
                            break;
                        case SpellType.Field:
                            sb.AppendLine(Localization.T("tt.field_effect")); break;
                    }
                    AppendAbility(sb, data);
                    break;
                case HeroCardData h:
                    // heroji imaju rucno pisanu liniju pravila, pa koristi SAMO nju;
                    // ne dodaji jos i genericki ability tekst jer se onda cita dvaput
                    string heroRule = CardText.HeroRule(h);
                    if (!string.IsNullOrEmpty(heroRule))
                        sb.AppendLine(heroRule);
                    else
                        AppendAbility(sb, data);
                    break;
            }

            // lore ide zadnji, odvojen praznim redom
            string lore = CardText.Lore(data);
            if (!string.IsNullOrEmpty(lore))
            {
                sb.AppendLine();
                sb.AppendLine(lore);
            }

            return sb.ToString().Trim();
        }

        // dodaje liniju s efektom, ali kod offensive spellova
        // sama steta to vec pokriva, pa preskoci offensive ponavljanja bez Aoe
        private void AppendAbility(System.Text.StringBuilder sb, CardData data)
        {
            string ab = AbilityText(data.ability, data.abilityValue);
            if (!string.IsNullOrEmpty(ab)) sb.AppendLine(ab);
        }

        public static string AbilityText(CardAbility ab, int v) => ab switch
        {
            CardAbility.PowerRamp => string.Format(Localization.T("ab.powerramp"), v),
            CardAbility.Draw => string.Format(Localization.T("ab.draw"), v),
            CardAbility.Heal => string.Format(Localization.T("ab.heal"), v),
            CardAbility.BuffAttack => string.Format(Localization.T("ab.buffattack"), v),
            CardAbility.Aoe => string.Format(Localization.T("ab.aoe"), v),
            CardAbility.ChargeFace => Localization.T("ab.chargeface"),
            CardAbility.PassiveBuffAllies => string.Format(Localization.T("ab.passivebuff"), v),
            CardAbility.PassiveRampEachTurn => string.Format(Localization.T("ab.passiveramp"), v),
            CardAbility.PassiveHealEachTurn => string.Format(Localization.T("ab.passiveheal"), v),
            CardAbility.PassiveTauntShield => string.Format(Localization.T("ab.passiveshield"), v),
            CardAbility.Lifesteal => Localization.T("ab.lifesteal"),
            CardAbility.DivineShield => Localization.T("ab.divineshield"),
            CardAbility.DamageEqualsMaxPower => Localization.T("ab.dmgmaxpower"),
            CardAbility.KillDraw => Localization.T("ab.killdraw"),
            CardAbility.RefreshAttacks => Localization.T("ab.refresh"),
            CardAbility.HealAllUnits => Localization.T("ab.healall"),
            CardAbility.ExecuteStrongest => Localization.T("ab.execute"),
            CardAbility.SacrificeDraw => string.Format(Localization.T("ab.sacrifice"), v),
            CardAbility.AoeAllDraw => string.Format(Localization.T("ab.aoealldraw"), v),
            CardAbility.SummonTokens => string.Format(Localization.T("ab.summon"), v),
            _ => ""
        };

        // opisni naziv tipa jedinice; keyword tipovi (Taunt/Assassin/Scout) ostaju termini
        private static string UnitTypeName(UnitType t) => t switch
        {
            UnitType.Taunt => "Taunt",
            UnitType.Assassin => "Assassin",
            UnitType.Scout => "Scout",
            _ => Localization.T("ut.basic")
        };

        // opisni naziv tipa spella
        private static string SpellTypeName(SpellType t) => t switch
        {
            SpellType.Offensive => Localization.T("st.offensive"),
            SpellType.Defensive => Localization.T("st.defensive"),
            SpellType.Field => Localization.T("st.field"),
            _ => t.ToString()
        };

        public void Hide()
        {
            if (_group != null) _group.alpha = 0f;
        }

        // sakrij tooltip koji je trenutno vidljiv, s bilo kojeg mjesta i bez
        // reference na canvas (koristi se kad se card unisti dok je mis na njoj)
        public static void HideActive()
        {
            if (_instance != null) _instance.Hide();
        }

        private void Update()
        {
            if (_group == null || _group.alpha <= 0f) return;
            // prati mis, s pomakom da ne prekrije card
            Vector2 mouse = GetMouse();
            Vector2 local;
            var canvasRect = _canvas.transform as RectTransform;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, mouse, _canvas.worldCamera, out local);
            local += new Vector2(24, -24);

            // ogranici da cijeli tooltip ostane na ekranu (pivot je gore lijevo)
            Vector2 half = canvasRect.rect.size * 0.5f;
            float w = _rt.sizeDelta.x, h = _rt.sizeDelta.y;
            local.x = Mathf.Clamp(local.x, -half.x + 4, half.x - w - 4);
            local.y = Mathf.Clamp(local.y, -half.y + h + 4, half.y - 4);
            _rt.anchoredPosition = local;
        }

        private Vector2 GetMouse()
        {
#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Mouse.current != null)
                return UnityEngine.InputSystem.Mouse.current.position.ReadValue();
#endif
            return Input.mousePosition;
        }
    }
}
