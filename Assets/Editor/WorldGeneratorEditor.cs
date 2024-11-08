using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(WorldGenerator))]
public class WorldGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector(); // Draws the default convertor

        WorldGenerator script = (WorldGenerator)target;

        if (script._autoUpdate)
            script.DrawMapInInspector();

        if (GUILayout.Button("Generate map"))
            script.DrawMapInInspector();

        if (GUILayout.Button("Save Texture"))
        {
            SaveTextureAsAsset(script.GetWorldTexture());
        }

    }
    private void SaveTextureAsAsset(Texture2D texture)
    {
        string path = EditorUtility.SaveFilePanelInProject("Save Texture", "Maps", "png", "Please enter a file name to save the texture to");
        if (path.Length != 0)
        {
            // Convert the texture to PNG
            byte[] pngData = texture.EncodeToPNG();
            if (pngData != null)
            {
                System.IO.File.WriteAllBytes(path, pngData);
                AssetDatabase.ImportAsset(path); // Refresh the AssetDatabase containing the new asset
                AssetDatabase.Refresh();
                Debug.Log("Texture saved as new asset at " + path);
            }
        }
    }
}
