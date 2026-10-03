using UnityEngine;

public class HeistBootstrap : MonoBehaviour
{
    HeistGameSession session;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (FindFirstObjectByType<HeistBootstrap>() != null) return;
        var go = new GameObject("HeistBootstrap");
        go.AddComponent<HeistBootstrap>();
        DontDestroyOnLoad(go);
    }

    void Start()
    {
#if UNITY_EDITOR
        if (session == null) StartHeist(JsonUtility.ToJson(EditorLaunch()));
#endif
    }

    public void StartHeist(string json)
    {
        json = HeistJson.NormalizeInbound(json);
        var launch = JsonUtility.FromJson<HeistLaunch>(json);
        if (launch == null)
            launch = EditorLaunch();
        if (launch.seed == 0 && !launch.testTuning)
        {
            launch.seed = Mathf.Abs((launch.targetValue + launch.difficulty * 17) | 1);
        }
        if (launch.crew == null) 
        {
            launch.crew = new HeistCrewMember[0];
        }
        
        if (string.IsNullOrEmpty(launch.origin)) launch.origin = "http://127.0.0.1:8765";
        bool host = string.IsNullOrEmpty(launch.joinCode);
        string code = host ? MakeCode() : launch.joinCode;
        string possess = string.IsNullOrEmpty(launch.possessId) && launch.crew.Length > 0
            ? launch.crew[0].id
            : launch.possessId;
        BeginSession(launch, possess, host, code);
    }

    public void JoinHeist(string json)
    {
        json = HeistJson.NormalizeInbound(json);
        var launch = JsonUtility.FromJson<HeistLaunch>(json);
        if (launch == null || string.IsNullOrEmpty(launch.joinCode)) return;
        if (launch.crew == null) return;
        string possess = launch.possessId;
        BeginSession(launch, possess, false, launch.joinCode);
    }

    void BeginSession(HeistLaunch launch, string possess, bool host, string code)
    {
        foreach (var component in GetComponents<HeistCoopRelay>()) DestroyImmediate(component);
        foreach (var component in GetComponents<HeistGameSession>()) DestroyImmediate(component);
        foreach (var component in GetComponents<HeistHeatDirector>()) DestroyImmediate(component);
        foreach (var component in GetComponents<HeistHud>()) DestroyImmediate(component);
        var builder = GetComponent<HeistLevelBuilder>();
        if (builder != null)
        {
            builder.Clear();
            DestroyImmediate(builder);
        }

        launch.joinCode = code;
        session = gameObject.AddComponent<HeistGameSession>();
        session.Begin(launch, possess, host, code);
        var relay = gameObject.AddComponent<HeistCoopRelay>();
        relay.StartRelay(session, code, host);
        HeistJs.JoinCode(code);
    }

    public static void NotifyResult(HeistResult result)
    {
        HeistJs.Complete(JsonUtility.ToJson(result));
    }

    static HeistLaunch EditorLaunch()
    {
        var settings = Resources.Load<HeistTestSettings>("HeistTestSettings");
        if (settings != null) return settings.BuildLaunch();
        return JsonUtility.FromJson<HeistLaunch>(SamplePayload());
    }

    static string MakeCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var c = new char[4];
        for (int i = 0; i < 4; i++) c[i] = chars[Random.Range(0, chars.Length)];
        return new string(c);
    }

    static string SamplePayload()
    {
        return "{\"difficulty\":3,\"targetValue\":500000,\"seed\":42,\"organizerId\":\"organizer\",\"origin\":\"http://127.0.0.1:8765\",\"crew\":[" +
               "{\"id\":\"organizer\",\"name\":\"The Locksmith\",\"className\":\"Safecracker\",\"level\":1,\"isOrganizer\":true,\"gear\":\"Lockpick Set\",\"stats\":{\"str\":4,\"agi\":3,\"intel\":3,\"dex\":5,\"cha\":2,\"per\":3}}," +
               "{\"id\":\"ghost\",\"name\":\"Ghost\",\"className\":\"Stealth\",\"level\":1,\"isOrganizer\":false,\"gear\":\"Disguise Kit\",\"stats\":{\"str\":2,\"agi\":8,\"intel\":4,\"dex\":5,\"cha\":3,\"per\":7}}" +
               "]}";
    }
}
