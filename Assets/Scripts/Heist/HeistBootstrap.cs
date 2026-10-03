using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;

public class HeistBootstrap : MonoBehaviour
{
    HeistVisualizer visualizer;
    bool running;

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    static extern void HeistNotifyComplete(string json);
#endif

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (FindFirstObjectByType<HeistBootstrap>() != null) return;
        var go = new GameObject("HeistBootstrap");
        go.AddComponent<HeistBootstrap>();
        DontDestroyOnLoad(go);
    }

    void Awake()
    {
        visualizer = gameObject.AddComponent<HeistVisualizer>();
    }

    void Start()
    {
#if UNITY_EDITOR
        if (!running) StartHeist(SamplePayload());
#endif
    }

    public void StartHeist(string json)
    {
        if (running) StopAllCoroutines();
        StartCoroutine(RunHeist(json));
    }

    IEnumerator RunHeist(string json)
    {
        running = true;
        json = HeistJson.NormalizeInbound(json);
        var launch = JsonUtility.FromJson<HeistLaunch>(json);
        if (launch == null) launch = JsonUtility.FromJson<HeistLaunch>(SamplePayload());
        if (launch.seed == 0) launch.seed = Mathf.Abs((launch.targetValue + launch.difficulty * 17) | 1);
        if (launch.crew == null) launch.crew = new HeistCrewMember[0];

        var rooms = HeistLevelGenerator.Generate(Mathf.Clamp(launch.difficulty, 1, 7), launch.seed);
        var result = HeistResolver.Resolve(launch, rooms);
        yield return visualizer.Play(rooms, launch.crew, result);

        string outbound = JsonUtility.ToJson(result);
#if UNITY_WEBGL && !UNITY_EDITOR
        HeistNotifyComplete(outbound);
#else
        Debug.Log(outbound);
#endif
        running = false;
    }

    static string SamplePayload()
    {
        return "{\"difficulty\":3,\"targetValue\":500000,\"seed\":42,\"organizerId\":\"organizer\",\"crew\":[" +
               "{\"id\":\"organizer\",\"name\":\"The Locksmith\",\"className\":\"Safecracker\",\"isOrganizer\":true,\"gear\":\"Lockpick Set\",\"stats\":{\"str\":2,\"agi\":2,\"intel\":3,\"dex\":4,\"cha\":1,\"per\":2}}," +
               "{\"id\":\"ghost\",\"name\":\"Ghost\",\"className\":\"Stealth\",\"isOrganizer\":false,\"gear\":\"Disguise Kit\",\"stats\":{\"str\":2,\"agi\":8,\"intel\":4,\"dex\":5,\"cha\":3,\"per\":7}}" +
               "]}";
    }
}
