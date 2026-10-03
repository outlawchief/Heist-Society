using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(HeistTestSettings))]
public class HeistTestSettingsEditor : Editor
{
    const string AssetPath = "Assets/Resources/HeistTestSettings.asset";

    [MenuItem("Heist Society/Test Settings")]
    public static void OpenTestSettings()
    {
        var settings = AssetDatabase.LoadAssetAtPath<HeistTestSettings>(AssetPath);
        if (settings == null) settings = Resources.Load<HeistTestSettings>("HeistTestSettings");
        if (settings == null)
        {
            Debug.LogWarning("HeistTestSettings asset was not found at " + AssetPath);
            return;
        }

        Selection.activeObject = settings;
        EditorGUIUtility.PingObject(settings);
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var settings = (HeistTestSettings)target;
        EditorGUILayout.Space();
        if (GUILayout.Button("Randomize seed"))
        {
            Undo.RecordObject(settings, "Randomize heist seed");
            settings.seed = Random.Range(1, 999999);
            EditorUtility.SetDirty(settings);
        }

        EditorGUI.BeginDisabledGroup(!Application.isPlaying);
        if (GUILayout.Button("Restart heist with these settings"))
        {
            var boot = Object.FindFirstObjectByType<HeistBootstrap>();
            if (boot != null) boot.StartHeist(JsonUtility.ToJson(settings.BuildLaunch()));
            else Debug.LogWarning("HeistBootstrap is not in the scene yet.");
        }
        EditorGUI.EndDisabledGroup();
        EditorGUILayout.HelpBox("Edit these fields, then press Play. During Play, Restart rebuilds the heist without stopping.", MessageType.Info);
    }
}
