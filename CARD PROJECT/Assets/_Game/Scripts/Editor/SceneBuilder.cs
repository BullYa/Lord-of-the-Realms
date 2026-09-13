#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LordOfTheRealms.EditorTools
{
    // Setup scena u jednom kliku: kreira sve scene (MainMenu, TestGround, MatchSetup,
    // Battle, Collection), ubaci pravi controller u svaku, snimi ih pod Assets/Scenes
    // i registrira u Build Settings. Menu: Lord of the Realms > Build Scenes
    public static class SceneBuilder
    {
        private const string ScenesFolder = "Assets/Scenes";

        [MenuItem("Lord of the Realms/Build Scenes")]
        public static void BuildScenes()
        {
            Directory.CreateDirectory(ScenesFolder);

            string menuPath = $"{ScenesFolder}/MainMenu.unity";
            string testPath = $"{ScenesFolder}/TestGround.unity";
            string setupPath = $"{ScenesFolder}/MatchSetup.unity";
            string battlePath = $"{ScenesFolder}/Battle.unity";
            string collectionPath = $"{ScenesFolder}/Collection.unity";
            string storyPath = $"{ScenesFolder}/StoryMap.unity";
            string mpPath = $"{ScenesFolder}/Multiplayer.unity";
            string gauntletPath = $"{ScenesFolder}/Gauntlet.unity";
            string tutorialPath = $"{ScenesFolder}/Tutorial.unity";
            string lobbyPath = $"{ScenesFolder}/Lobby.unity";

            CreateSceneWith<MainMenuController>(menuPath, "MainMenuRoot");
            CreateSceneWith<TestGroundController>(testPath, "TestGroundRoot");
            CreateSceneWith<MatchSetupController>(setupPath, "MatchSetupRoot");
            CreateSceneWith<BattleController>(battlePath, "BattleRoot");
            CreateSceneWith<CollectionController>(collectionPath, "CollectionRoot");
            CreateSceneWith<StoryMapController>(storyPath, "StoryMapRoot");
            CreateSceneWith<MultiplayerController>(mpPath, "MultiplayerRoot");
            CreateSceneWith<GauntletController>(gauntletPath, "GauntletRoot");
            CreateSceneWith<TutorialController>(tutorialPath, "TutorialRoot");
            CreateSceneWith<LobbyController>(lobbyPath, "LobbyRoot");

            AddToBuildSettings(menuPath, testPath, setupPath, battlePath, collectionPath, storyPath, mpPath, gauntletPath, tutorialPath, lobbyPath);

            // vrati editor u glavni izbornik: svaka scena se gradi u Single modu, pa bi
            // inace ostao otvoren Tutorial (zadnji u nizu) i Play bi krenuo iz njega
            EditorSceneManager.OpenScene(menuPath, OpenSceneMode.Single);

            EditorUtility.DisplayDialog("Lord of the Realms",
                "Scenes built.", "OK");
        }

        private static void CreateSceneWith<T>(string path, string rootName) where T : Component
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGO = new GameObject("Main Camera", typeof(Camera));
            camGO.tag = "MainCamera";
            var cam = camGO.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.04f, 0.06f);
            cam.orthographic = true;

            var root = new GameObject(rootName);
            root.AddComponent<T>();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, path);
        }

        private static void AddToBuildSettings(params string[] paths)
        {
            var list = new System.Collections.Generic.List<EditorBuildSettingsScene>();
            foreach (var p in paths)
                list.Add(new EditorBuildSettingsScene(p, true));
            EditorBuildSettings.scenes = list.ToArray();
        }
    }
}
#endif
