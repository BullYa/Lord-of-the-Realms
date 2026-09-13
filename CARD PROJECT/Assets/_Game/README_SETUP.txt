LORD OF THE REALMS (Vladar Svjetova) - SETUP & PREGLED PROJEKTA
================================================================
Unity 6000.3.19f1, URP 2D. Sav UI se gradi IZ KODA (nema prefaba).
Zadnji update: tutorial + EN/HR lokalizacija.


PRVO POKRETANJE - REDOSLIJED JE OBAVEZAN
-----------------------------------------
1. Otvori projekt u Unityju, pričekaj kompajliranje (nema crvenih grešaka u Console).

2. Lord of the Realms > Generate Card Assets
   -> generira sve karte (statovi žive u Editor/CardAssetGenerator.cs) + 2 heroja po rasi

3. Lord of the Realms > Sync Cards To Resources
   -> kopira karte u Resources/CardData (OBAVEZNO za meč i collection)

4. Lord of the Realms > Generate Card Art Placeholders
5. Lord of the Realms > Generate Audio Placeholders

6. Lord of the Realms > Build Scenes
   -> MainMenu, TestGround, MatchSetup, Battle, Collection, StoryMap,
      Multiplayer, Gauntlet, Tutorial, Lobby (+ upis u Build Settings)

7. Otvori Assets/Scenes/MainMenu.unity, pritisni PLAY.

KAD TREBA PONOVITI:
  - mijenjani statovi/imena karata -> koraci 2,3 (+ ostali generatori po potrebi)
  - dodana nova scena                -> korak 7
  - samo izmjena koda/komentara      -> dovoljan recompile
  - testiranje story rewarda         -> preporuka: New Game (StoryMap > slot)


GLAVNI MENI
-----------
FREE PLAY       - ranked 1v1 (ELO vs bot), infinite gauntlet, edit deck
STORY           - kampanja: 4 regije x 5 bitaka + finale (21 node), 4 save slota
CARD COLLECTION - pregled svih karata + deck builder (slotovi)
HOW TO PLAY     - tutorial: pravila kroz 8 stranica + probni meč
ACHIEVEMENTS    - 18 postignuća
QUIT

Gore desno: SOUND (volume slider) i LANGUAGE (EN | HR prekidač).


JEZIK (EN / HR)
---------------
Cijeli UI je dvojezičan. Prekidač je u glavnom meniju gore desno.
Izbor se pamti (PlayerPrefs "ui_language"); promjena reloada aktivnu scenu
da se UI ponovno izgradi.

Svi tekstovi žive u Core/Localization.cs (Loc tablica key -> {EN, HR}),
uz EN fallback ako HR nedostaje. Pomoćnici:
  Localization.T("key")     - dohvat teksta
  Localization.RaceName(r)  - naziv rase (Orci/Vilenjaci/Ljudi/Demoni)
  Localization.DiffName(d)  - naziv težine bota

Story lore (21 protivnik) ima HR prijevod u Core/StoryData.cs
(CommandersHr / TextsHr). Achievementi se prevode preko ach.<id>.title/.desc.

NAMJERNO OSTAJE ENGLESKI (termini):
  - imena karata i junaka (Kragmaw, Forest Avatar, ...)
  - keywordovi: Taunt, Assassin, Scout, Lifesteal, Shield
  - resurs POWER, rank imena, ELO

NAPOMENA O FONTU: HR koristi dijakritike (č ć š ž đ). Ugrađeni font ih crta.
Ako dodaš vlastiti TTF (Resources/Fonts/UIFont.ttf), mora imati te glifove.


PRAVILA IGRE (ukratko)
----------------------
- 40 HP po igraču; pobjeda kad protivnički junak padne na 0.
- POWER: +1 max po potezu (cap 10), puni se na početku poteza.
  Ramp karte dižu max brže (probijaju do HardMax 15); "drain" karte
  TRAJNO smanjuju max POWER (crveni -N badge na karti).
- Deck: 29 karata + 1 heroj, max 3 kopije po karti.
- Jedinice napadaju potez NAKON što su odigrane.
  Taunt (mora se prvi napasti), Assassin (ignorira Taunt, ne dira junaka
  dok Taunt stoji), Scout (može otkriti skrivenu čaroliju).
- Spellovi: jednokratni efekt; neki se postavljaju licem prema dolje
  kao hidden trap i okidaju kad neprijatelj napadne.
- Junak se bori kao jedinica; kad umre vraća se na VRH decka (ne gubi se).
- Mulligan: na početku meča biraš koje karte zamijeniti.


STORY MOD
---------
- 21 node: 4 regije (Orcs/Elves/Humans/Demons) x 5 + finale (Ashmount).
- Težina raste 1..5 unutar regije; finale je Nightmare (boss: 50 HP).
- Svaka bitka ima FIKSAN reward tvoje rase: borbe 1-19 karte,
  Demons_5 drugi heroj, finale golden okvir.
- 4 save slota: napredak mape je PO SLOTU, otključane karte su GLOBALNE
  (New Game u jednom slotu ne uništava rewarde iz drugih).
- Svaki protivnik ima ime i pred-borbeni lore tekst (StoryLore).


STRUKTURA KODA (Assets/_Game/Scripts)
-------------------------------------
Core/    GameMatch (motor pravila), PowerPool, Deck/DeckBuilder/DeckLists/
         DeckStorage, CardLibrary (+MatchConfig), BotPlayer, StoryData
         (+StoryLore, StoryProgress, StorySave, StorySlots, GoldenFrames),
         RankedSystem, PlayerStats, MatchHistory, Achievements, AudioManager,
         GameFlow (scene), GameEnums, CardAbility, TargetingRules, ITargetable,
         PlayerEntity, Localization
Cards/   CardData -> UnitCardData / SpellCardData / HeroCardData, CardInstance
UI/      BattleController (meč), CollectionController, StoryMapController,
         MainMenuController, MultiplayerController (=Free Play),
         GauntletController, TutorialController, CardArtView, CardTooltip,
         UIFactory, UISprites, Anim, ButtonHover, MatchSetupController, Toast
Editor/  CardAssetGenerator (SVI STATOVI KARATA), ResourceSync, SceneBuilder,
         ArtPlaceholderGenerator, AudioPlaceholderGenerator,
         BackgroundPlaceholderGenerator
TestGround/ CardView, PlayerFaceView, TestGroundController

VAŽNO: statovi i imena karata se NE mijenjaju u Resources. Mijenjaju se u
Editor/CardAssetGenerator.cs, pa se regeneriraju (koraci 2-3 gore).


PLACEHOLDER ASSETI
------------------
Art i zvukovi su generirani placeholderi. Prava slika/zvuk se ubacuje
prepisivanjem datoteke ISTOG IMENA u Resources/CardArt i Resources/Audio.
Glazba u borbi bira se po rasi PROTIVNIKA.

POZADINA BORBE: mec ne koristi sliku. Iz koda se crta univerzalno igrace
polje (playmat): uokvirena ploca preko obje zone, topli ton na tvojoj
strani, hladni na protivnickoj, sredisnji sav. Gradi ga
BattleController.BuildPlaymat(), pa generator pozadina vise ne postoji.


DODAVANJE NOVE SCENE (postupak)
-------------------------------
1. Core/GameFlow.cs      - dodaj const ime scene + Load metodu
2. Editor/SceneBuilder.cs- dodaj CreateSceneWith<TvojController> + u AddToBuildSettings
3. Lord of the Realms > Build Scenes


PREOSTALO / SLJEDEĆI KORACI
---------------------------
- Netcode multiplayer (hook postoji: FindRankedMatch, sad igra protiv bota)
- Zamjena placeholder arta/audia/pozadina pravim materijalom
- Post-victory defeat dijalozi po protivniku
- Dva završetka (zatvori Maw vs skuj krunu), vidi STORY.txt
- Lore varijacija po rasi igrača
- Kozmetika: golden karte / card-back varijante, globalni settings ekran
- Balans sim update (drain + nove karte)
