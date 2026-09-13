#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;
using System.Linq;

namespace LordOfTheRealms.EditorTools
{
    // Kopira svaki generirani CardData asset u Assets/_Game/Resources/CardData da se
    // moze ucitati u runtimeu (Resources.LoadAll). Pokreni nakon generiranja/izmjene
    // karata. Menu: Lord of the Realms > Sync Cards To Resources
    public static class ResourceSync
    {
        private const string Source = "Assets/_Game/CardData";
        private const string ResourcesRoot = "Assets/_Game/Resources";
        private const string Dest = "Assets/_Game/Resources/CardData";

        [MenuItem("Lord of the Realms/Sync Cards To Resources")]
        public static void Sync()
        {
            if (!AssetDatabase.IsValidFolder(Source))
            {
                EditorUtility.DisplayDialog("Lord of the Realms",
                    "No CardData folder found. Run 'Generate Card Assets' first.", "OK");
                return;
            }

            if (!AssetDatabase.IsValidFolder(ResourcesRoot))
                AssetDatabase.CreateFolder("Assets/_Game", "Resources");
            if (AssetDatabase.IsValidFolder(Dest))
                AssetDatabase.DeleteAsset(Dest);
            AssetDatabase.CreateFolder(ResourcesRoot, "CardData");

            var guids = AssetDatabase.FindAssets("t:CardData", new[] { Source });
            int n = 0;
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string file = Path.GetFileName(path);
                string destPath = $"{Dest}/{file}";
                if (AssetDatabase.CopyAsset(path, destPath)) n++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Lord of the Realms",
                $"{n} cards synced.",
                "OK");
        }
    }
}
#endif
