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
    }
}
