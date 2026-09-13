using UnityEngine;
using UnityEngine.SceneManagement;

namespace LordOfTheRealms
{
    // Imena scena i prijelazi izmedu njih; sve promjene scene idu odavde.
    public static class GameFlow
    {
        public const string MainMenuScene = "MainMenu";
        public const string TestGroundScene = "TestGround";
        public const string MatchSetupScene = "MatchSetup";
        public const string BattleScene = "Battle";
        public const string CollectionScene = "Collection";
        public const string StoryMapScene = "StoryMap";
        public const string MultiplayerScene = "Multiplayer";
        public const string GauntletScene = "Gauntlet";
        public const string TutorialScene = "Tutorial";
        public const string LobbyScene = "Lobby";

        public static void LoadMainMenu() => SceneManager.LoadScene(MainMenuScene);
        public static void LoadTestGround() => SceneManager.LoadScene(TestGroundScene);
        public static void LoadMatchSetup() => SceneManager.LoadScene(MatchSetupScene);
        public static void LoadBattle() => SceneManager.LoadScene(BattleScene);
        public static void LoadCollection() => SceneManager.LoadScene(CollectionScene);
        public static void LoadMultiplayer() => SceneManager.LoadScene(MultiplayerScene);
        public static void LoadGauntlet() => SceneManager.LoadScene(GauntletScene);
        public static void LoadTutorial() => SceneManager.LoadScene(TutorialScene);
        public static void LoadLobby() => SceneManager.LoadScene(LobbyScene);

        // Tocno kad se story mapa otvori svjeze iz glavnog menija, da mapa moze
        // pokazati Continue / New Game skocni prozor jednom, umjesto svaki put kad
        // se scena ponovno ucita (npr. povratkom iz deck editora).
        public static bool StoryEnteredFromMenu;

        // MatchSetup je dostupan iz Free Playa (Custom Match) i s kraja obicnog meca;
        // BACK se vraca tamo odakle si dosao
        public static bool MatchSetupFromFreePlay;

        public static void LoadStoryMap(bool fromMenu = false)
        {
            StoryEnteredFromMenu = fromMenu;
            SceneManager.LoadScene(StoryMapScene);
        }

        public static void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
