using Photon.Pun;
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
        serializedObject.Update();
        var roomCode = serializedObject.FindProperty("roomCode");
        var origin = serializedObject.FindProperty("origin");
        EditorGUILayout.PropertyField(roomCode, new GUIContent("Room code"));
        EditorGUILayout.PropertyField(origin, new GUIContent("LAN origin"));
        if (string.IsNullOrWhiteSpace(roomCode.stringValue))
            EditorGUILayout.HelpBox("Leave Room code empty to host. Play shows a join code on the HUD.", MessageType.None);
        else
            EditorGUILayout.HelpBox("Play joins a WebGL / LAN heist on serve.py with this code (same as the site). If that session is missing, it tries Photon.", MessageType.None);

        string appId = PhotonNetwork.PhotonServerSettings != null
            ? PhotonNetwork.PhotonServerSettings.AppSettings.AppIdRealtime
            : "";
        if (string.IsNullOrEmpty(appId))
            EditorGUILayout.HelpBox("Paste the Realtime App Id into Window > Photon Unity Networking > Highlight Server Settings. Until then, Play starts a local heist.", MessageType.Warning);

        EditorGUILayout.Space();
        DrawPropertiesExcluding(serializedObject, "m_Script", "roomCode", "origin");
        serializedObject.ApplyModifiedProperties();

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
            var photon = Object.FindFirstObjectByType<HeistPhotonSession>();
            if (photon != null) photon.ConnectFromSettings();
            else
            {
                var boot = Object.FindFirstObjectByType<HeistBootstrap>();
                if (boot != null) boot.StartHeist(JsonUtility.ToJson(settings.BuildLaunch()));
                else Debug.LogWarning("HeistBootstrap is not in the scene yet.");
            }
        }
        EditorGUI.EndDisabledGroup();

        if (Application.isPlaying)
        {
            var photon = HeistPhotonSession.Instance;
            if (photon != null)
            {
                EditorGUILayout.HelpBox(photon.Status, photon.RoomMissing ? MessageType.Error : MessageType.Info);
                if (PhotonNetwork.InRoom && !PhotonNetwork.IsMasterClient && !photon.HasLocalClaim)
                {
                    EditorGUILayout.LabelField("Take control", EditorStyles.boldLabel);
                    foreach (var member in photon.Unclaimed())
                    {
                        if (GUILayout.Button(member.name))
                            photon.RequestClaim(member.id);
                    }
                }
            }
            Repaint();
        }
        else
        {
            EditorGUILayout.HelpBox("Edit these fields, then press Play. During Play, Restart rebuilds the heist without stopping.", MessageType.Info);
        }
    }
}
