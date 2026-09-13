using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace LordOfTheRealms
{
    // Test Ground: sandbox scena za rucnu provjeru pravila karata.
    //  - Ploca protivnika (gore) i igraca (dolje), svaka s jednom od svake vrste
    //    jedinice (Basic/Taunt/Assassin/Scout), herojem i spell placeholderima.
    //  - Svaka strana ima "hero face" traku koju takoder mozes gadati.
    // Targeting test: klikni svoju kartu (napadac) -> legalne mete svijetle zeleno
    // -> klikni metu za stetu. Scout test: skriveni spellovi svijetle cyan (reveal),
    // klik ih otkrije i potrosi Scoutov napad (ne unistava ih).
    public class TestGroundController : MonoBehaviour
    {
        private Canvas _canvas;
        private Text _logText;
        private Text _hintText;

        private readonly List<CardView> _playerViews = new();
        private readonly List<CardView> _opponentViews = new();

        private PlayerEntity _player;
        private PlayerEntity _opponent;
        private PlayerFaceView _playerFace;
        private PlayerFaceView _opponentFace;

        private CardView _selectedAttacker;

        private readonly List<string> _log = new();

        private static readonly Color GreenTarget = new Color(0.3f, 1f, 0.4f);
        private static readonly Color CyanReveal = new Color(0.3f, 0.85f, 1f);
        private static readonly Color YellowAttacker = new Color(1f, 0.9f, 0.3f);
        private static readonly Color BlueUsable = new Color(0.35f, 0.5f, 1f);

        private void Start()
        {
            AudioManager.EnsureExists();
            _player = new PlayerEntity(Owner.Player, Race.Humans);
            _opponent = new PlayerEntity(Owner.Opponent, Race.Orcs);
            BuildUI();
            Log("Test Ground ready. Click one of YOUR cards (bottom) to select an attacker.");
        }

        // ---------------------------------------------------------------- build

        private void BuildUI()
        {
            var canvasGO = new GameObject("TestCanvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _canvas = canvasGO.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            EnsureEventSystem();
            BuildBoardContentsOnly();
        }

        private PlayerFaceView CreateFace(Transform parent, PlayerEntity entity, string name,
            Vector2 anchor, Vector2 pos)
        {
            var panel = UIFactory.CreatePanel(parent, name, new Color(0.15f, 0.13f, 0.16f, 0.95f));
            UIFactory.Anchor(panel, anchor, anchor, anchor, pos, new Vector2(360, 60));
            var face = panel.gameObject.AddComponent<PlayerFaceView>();
            face.Bind(entity);
            face.OnClicked += HandleFaceClicked;
            return face;
        }

        private void BuildRow(Transform parent, Owner side, List<CardView> views, Race race, float yAnchor)
        {
            // Po jedan od svakog tipa unita, plus heroj i dva spell placeholdera.
            var cards = new List<CardInstance>
            {
                new CardInstance(MakeUnit("Grunt",   race, 2, 1, 3, UnitType.Basic),    side),
                new CardInstance(MakeUnit("Wall",    race, 2, 0, 4, UnitType.Taunt),    side),
                new CardInstance(MakeUnit("Stalker", race, 3, 3, 2, UnitType.Assassin), side),
                new CardInstance(MakeUnit("Ranger",  race, 2, 2, 2, UnitType.Scout),    side),
                new CardInstance(MakeHero("Champion",race, 4, 4, 6),                     side),
                new CardInstance(MakeSpell("Fireball", race, 3, SpellType.Offensive, 3, false), side),
                new CardInstance(MakeSpell("Ward",     race, 2, SpellType.Defensive, 3, true),  side),
            };

            const int count = 7;
            const float cardW = 150f, cardH = 210f, gap = 16f;
            float totalW = count * cardW + (count - 1) * gap;
            float startX = -totalW / 2f + cardW / 2f;

            for (int i = 0; i < cards.Count; i++)
            {
                var go = new GameObject($"{side}_Card_{i}", typeof(RectTransform));
                go.transform.SetParent(parent, false);
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, yAnchor);
                rt.anchorMax = new Vector2(0.5f, yAnchor);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(cardW, cardH);
                rt.anchoredPosition = new Vector2(startX + i * (cardW + gap), 0);

                var view = go.AddComponent<CardView>();
                view.Bind(cards[i], side);
                view.OnClicked += HandleCardClicked;

                // Spellovi koji se mogu sakriti krecu skriveni na protivnickoj strani, da mozes testirati Scout reveal.
                if (side == Owner.Opponent && cards[i].Data is SpellCardData sp && sp.canBePlayedHidden)
                    cards[i].IsHidden = true;

                // Igracevi uniti dobiju "ready" da mozes odmah napasti u sandboxu.
                if (side == Owner.Player && cards[i].Data.Category == CardCategory.Unit)
                    cards[i].CanAttack = true;

                view.Refresh();
                views.Add(view);
            }
        }

        // ---------------------------------------------------------------- data helpers

        private UnitCardData MakeUnit(string n, Race r, int cost, int atk, int hp, UnitType type)
        {
            var d = ScriptableObject.CreateInstance<UnitCardData>();
            d.cardName = n; d.race = r; d.cost = cost;
            d.attack = atk; d.health = hp; d.unitType = type;
            return d;
        }

        private HeroCardData MakeHero(string n, Race r, int atk, int hp, int cost)
        {
            var d = ScriptableObject.CreateInstance<HeroCardData>();
            d.cardName = n; d.race = r; d.cost = cost; d.attack = atk; d.health = hp;
            return d;
        }

        private SpellCardData MakeSpell(string n, Race r, int cost, SpellType type, int power, bool hidden)
        {
            var d = ScriptableObject.CreateInstance<SpellCardData>();
            d.cardName = n; d.race = r; d.cost = cost;
            d.spellType = type; d.power = power; d.canBePlayedHidden = hidden;
            return d;
        }

        // ---------------------------------------------------------------- interaction

        private void HandleCardClicked(CardView view)
        {
            // Klik na vlastitu card je odabire kao napadaca (ako moze djelovati).
            if (view.Side == Owner.Player)
            {
                TrySelectAttacker(view);
                return;
            }

            // Klik na protivnicku card = pokusaj je gadati.
            if (_selectedAttacker != null)
                TryResolveOnTarget(view.Card.AsTargetable(), view);
            else
                Log("Select one of YOUR cards first.");
        }

        private void HandleFaceClicked(PlayerFaceView face)
        {
            if (face.Entity.Owner == Owner.Opponent && _selectedAttacker != null)
                TryResolveOnTarget(face.Entity, null, face);
            else if (face.Entity.Owner == Owner.Player)
                Log("That's your own hero. Click an enemy target.");
        }

        private void TrySelectAttacker(CardView view)
        {
            var card = view.Card;

            bool canAct =
                card.Data.Category == CardCategory.Spell
                    ? ((SpellCardData)card.Data).spellType == SpellType.Offensive
                    : card.CanAttack && card.IsAlive;

            if (!canAct)
            {
                if (card.Data is SpellCardData)
                    Log($"{card.Data.cardName}: only Offensive spells can target from hand in this test.");
                else if (!card.IsAlive)
                    Log($"{card.Data.cardName} is dead.");
                else
                    Log($"{card.Data.cardName} has summoning sickness (can't attack yet).");
                return;
            }

            _selectedAttacker = view;
            HighlightTargets();

            if (card.IsScout)
                Log($"Selected Scout {card.Data.cardName}. CYAN = hidden spells you can REVEAL; GREEN = attack targets.");
            else
            {
                string mode = card.AttackTargeting == TargetingMode.Precise ? "PRECISE (ignores taunt vs units)" : "normal";
                Log($"Selected {card.Data.cardName} [{mode}]. Green = legal targets. Click one.");
            }
        }

        private void TryResolveOnTarget(ITargetable target, CardView targetView, PlayerFaceView faceView = null)
        {
            var attacker = _selectedAttacker.Card;

            // --- Scout otkriva skriveni spell ---
            if (attacker.IsScout && targetView != null && targetView.Card.IsHiddenSpell)
            {
                targetView.Card.Reveal();
                Log($"Scout {attacker.Data.cardName} reveals hidden spell: {targetView.Card.Data.cardName} (it stays in play).");
                attacker.CanAttack = false; // Scout spends its action to scout
                targetView.Refresh();
                ClearSelection();
                RefreshAll();
                return;
            }

            var enemyUnits = _opponentViews.Select(v => v.Card).ToList();

            bool legal = TargetingRules.IsLegalTarget(
                attacker.AttackTargeting, target, enemyUnits, _opponent);

            if (!legal)
            {
                Log($"Illegal target: taunt rules forbid hitting {target.TargetName} with {attacker.Data.cardName}.");
                return;
            }

            int dmg = attacker.Data.Category == CardCategory.Spell
                ? ((SpellCardData)attacker.Data).power
                : attacker.Attack;

            target.ReceiveDamage(dmg);
            Log($"{attacker.Data.cardName} hits {target.TargetName} for {dmg}.");

            if (attacker.Data.Category == CardCategory.Unit || attacker.Data.Category == CardCategory.Hero)
                attacker.CanAttack = false;

            targetView?.Refresh();
            faceView?.Refresh();
            ClearSelection();
            RefreshAll();
        }

        private void HighlightTargets()
        {
            var attacker = _selectedAttacker.Card;
            var enemyUnits = _opponentViews.Select(v => v.Card).ToList();
            var legal = TargetingRules.GetLegalTargets(attacker.AttackTargeting, enemyUnits, _opponent);

            foreach (var v in _opponentViews)
            {
                // Scout: hidden spells become cyan "reveal" targets, ignoring taunt.
                if (attacker.IsScout && v.Card.IsHiddenSpell)
                {
                    v.SetHighlight(true, CyanReveal);
                    continue;
                }

                bool isLegal = legal.Contains(v.Card.AsTargetable());
                v.SetHighlight(isLegal, GreenTarget);
            }

            bool faceLegal = legal.Contains(_opponent);
            _opponentFace.SetHighlight(faceLegal, GreenTarget);

            _selectedAttacker.SetHighlight(true, YellowAttacker);
        }

        private void ClearSelection()
        {
            if (_selectedAttacker != null)
                _selectedAttacker.SetHighlight(false);
            _selectedAttacker = null;
            foreach (var v in _opponentViews) v.SetHighlight(false);
            _opponentFace.SetHighlight(false);
        }

        private void RefreshAll()
        {
            foreach (var v in _playerViews) v.Refresh();
            foreach (var v in _opponentViews) v.Refresh();
            _playerFace.Refresh();
            _opponentFace.Refresh();
            RefreshSelectableHints();
        }

        private void RefreshSelectableHints()
        {
            foreach (var v in _playerViews)
            {
                if (_selectedAttacker != null) continue;
                bool usable = v.Card.IsAlive &&
                    (v.Card.Data.Category == CardCategory.Spell
                        ? ((SpellCardData)v.Card.Data).spellType == SpellType.Offensive
                        : v.Card.CanAttack);
                v.SetHighlight(usable, BlueUsable);
            }
        }

        // ---------------------------------------------------------------- control panel

        private void BuildControlPanel(Transform parent)
        {
            var panel = UIFactory.CreatePanel(parent, "ControlPanel", UIFactory.Panel);
            UIFactory.Anchor(panel, new Vector2(1, 0.5f), new Vector2(1, 0.5f),
                new Vector2(1, 0.5f), new Vector2(-16, 0), new Vector2(230, 380));

            var header = UIFactory.CreateText(panel, "CtrlHeader", "TEST TOOLS", 20,
                TextAnchor.UpperCenter, UIFactory.Ink);
            UIFactory.Anchor(header.rectTransform, new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0.5f, 1), new Vector2(0, -12), new Vector2(-16, 30));

            AddCtrlButton(panel, "Reset Board", -60, ResetBoard);
            AddCtrlButton(panel, "Ready Player Units", -120, ReadyPlayerUnits);
            AddCtrlButton(panel, "Hide Opp. Spells", -180, HideOpponentSpells);
            AddCtrlButton(panel, "Toggle Opp. Taunt", -240, ToggleOpponentTaunt);
            AddCtrlButton(panel, "Clear Selection", -300, () => { ClearSelection(); RefreshSelectableHints(); Log("Selection cleared."); });
        }

        private void AddCtrlButton(RectTransform panel, string label, float y, System.Action action)
        {
            var b = UIFactory.CreateButton(panel, label.Replace(" ", ""), label, 16);
            UIFactory.Anchor(b.GetComponent<RectTransform>(), new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(0.5f, 1), new Vector2(0, y), new Vector2(200, 46));
            b.onClick.AddListener(() => action());
        }

        // ---------------------------------------------------------------- test tools

        private void ResetBoard()
        {
            foreach (Transform child in _canvas.transform) Destroy(child.gameObject);
            _playerViews.Clear();
            _opponentViews.Clear();
            _log.Clear();
            _selectedAttacker = null;
            _player = new PlayerEntity(Owner.Player, Race.Humans);
            _opponent = new PlayerEntity(Owner.Opponent, Race.Orcs);

            BuildBoardContentsOnly();
            Log("Board reset.");
        }

        private void BuildBoardContentsOnly()
        {
            var bg = UIFactory.CreatePanel(_canvas.transform, "Background",
                new Color(0.07f, 0.06f, 0.08f, 1f));
            UIFactory.Stretch(bg);

            var back = UIFactory.CreateButton(_canvas.transform, "BackButton", "MENU", 20);
            UIFactory.Anchor(back.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(0, 1), new Vector2(20, -20), new Vector2(140, 54));
            back.onClick.AddListener(GameFlow.LoadMainMenu);

            var titleText = UIFactory.CreateText(_canvas.transform, "TitleText",
                "TEST GROUND", 30, TextAnchor.UpperCenter, UIFactory.Ink);
            UIFactory.Anchor(titleText.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(0.5f, 1), new Vector2(0, -22), new Vector2(500, 44));

            _opponentFace = CreateFace(_canvas.transform, _opponent, "OpponentFace",
                new Vector2(0.5f, 1), new Vector2(0, -90));
            _playerFace = CreateFace(_canvas.transform, _player, "PlayerFace",
                new Vector2(0.5f, 0), new Vector2(0, 70));

            BuildRow(_canvas.transform, Owner.Opponent, _opponentViews, _opponent.Race, 0.63f);
            BuildRow(_canvas.transform, Owner.Player, _playerViews, _player.Race, 0.30f);

            BuildControlPanel(_canvas.transform);
            BuildLogPanel(_canvas.transform);
            RefreshSelectableHints();
        }

        private void ReadyPlayerUnits()
        {
            foreach (var v in _playerViews)
                if (v.Card.Data.Category == CardCategory.Unit || v.Card.Data.Category == CardCategory.Hero)
                    v.Card.CanAttack = true;
            RefreshAll();
            Log("All your units are READY to attack.");
        }

        private void HideOpponentSpells()
        {
            int hidden = 0;
            foreach (var v in _opponentViews)
                if (v.Card.Data is SpellCardData sp && sp.canBePlayedHidden && !v.Card.IsHidden)
                { v.Card.IsHidden = true; hidden++; v.Refresh(); }
            Log(hidden > 0 ? $"Re-hid {hidden} opponent spell(s). Select your Scout to reveal them."
                           : "No hideable opponent spells to hide.");
            if (_selectedAttacker != null) HighlightTargets();
        }

        private void ToggleOpponentTaunt()
        {
            var taunt = _opponentViews.FirstOrDefault(v => v.Card.IsTaunt);
            if (taunt == null) { Log("No taunt unit on opponent board."); return; }

            if (taunt.Card.IsAlive)
            {
                taunt.Card.TakeDamage(taunt.Card.CurrentHealth);
                Log("Opponent taunt removed. Precise vs normal targeting now differs.");
            }
            else
            {
                taunt.Card.Heal(taunt.Card.MaxHealth);
                Log("Opponent taunt restored.");
            }
            if (_selectedAttacker != null) HighlightTargets();
            RefreshAll();
        }

        // ---------------------------------------------------------------- log

        private void BuildLogPanel(Transform parent)
        {
            var panel = UIFactory.CreatePanel(parent, "LogPanel", new Color(0.05f, 0.05f, 0.06f, 0.95f));
            UIFactory.Anchor(panel, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0),
                new Vector2(16, 16), new Vector2(560, 150));

            _logText = UIFactory.CreateText(panel, "LogText", "", 16, TextAnchor.LowerLeft,
                new Color(0.8f, 0.85f, 0.8f));
            UIFactory.Stretch(_logText.rectTransform);
            _logText.rectTransform.offsetMin = new Vector2(12, 10);
            _logText.rectTransform.offsetMax = new Vector2(-12, -10);
            _logText.alignment = TextAnchor.LowerLeft;

            _hintText = UIFactory.CreateText(parent, "HintText",
                "Blue = your usable cards · Green = attack targets · Cyan = Scout reveal · Click attacker then target",
                15, TextAnchor.LowerLeft, new Color(0.55f, 0.55f, 0.6f));
            UIFactory.Anchor(_hintText.rectTransform, new Vector2(0, 0), new Vector2(0, 0),
                new Vector2(0, 0), new Vector2(16, 174), new Vector2(760, 24));

            RenderLog();
        }

        private void Log(string msg)
        {
            _log.Add(msg);
            while (_log.Count > 6) _log.RemoveAt(0);
            RenderLog();
        }

        private void RenderLog()
        {
            if (_logText != null) _logText.text = string.Join("\n", _log);
        }

        private void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() != null)
                return;
            new GameObject("EventSystem",
                typeof(UnityEngine.EventSystems.EventSystem),
                typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
        }
    }
}
