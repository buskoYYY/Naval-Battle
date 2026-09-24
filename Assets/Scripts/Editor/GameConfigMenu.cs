using NavalBattle.Config;
using UnityEditor;
using UnityEngine;

namespace NavalBattle.EditorTools
{
    public static class GameConfigMenu
    {
        [MenuItem("Naval Battle/Create Default Game Config")]
        public static void CreateDefaultConfig()
        {
            const string folder = "Assets/Config";
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder("Assets", "Config");

            const string path = folder + "/DefaultGameConfig.asset";
            var existing = AssetDatabase.LoadAssetAtPath<GameConfig>(path);
            if (existing != null)
            {
                Selection.activeObject = existing;
                EditorGUIUtility.PingObject(existing);
                return;
            }

            var config = ScriptableObject.CreateInstance<GameConfig>();
            AssetDatabase.CreateAsset(config, path);
            AssetDatabase.SaveAssets();
            Selection.activeObject = config;
        }
    }
}
