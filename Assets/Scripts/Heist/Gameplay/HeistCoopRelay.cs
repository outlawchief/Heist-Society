using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

public class HeistCoopRelay : MonoBehaviour
{
    string code;
    bool host;
    HeistGameSession session;

    public void StartRelay(HeistGameSession game, string joinCode, bool isHost)
    {
        session = game;
        code = joinCode;
        host = isHost;
        StartCoroutine(Loop());
    }

    IEnumerator Loop()
    {
        if (host)
        {
            yield return Post("/coop/" + code + "/launch", JsonUtility.ToJson(session.Launch));
        }

        while (session != null)
        {
            if (host)
            {
                yield return Post("/coop/" + code + "/state", BuildSnapshot());
                if (!session.Ended) yield return ApplyGuestInputs();
            }
            else
            {
                yield return PullAndApplyState();
                if (!session.Ended) yield return PostGuestInput();
            }
            if (session.Ended) yield break;
            yield return new WaitForSeconds(0.15f);
        }
    }

    string BuildSnapshot() => HeistWorldNet.BuildJson(session);

    IEnumerator ApplyGuestInputs()
    {
        using (var req = UnityWebRequest.Get(Origin() + "/coop/" + code + "/input"))
        {
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success) yield break;
            var bag = JsonUtility.FromJson<CoopInputBag>(req.downloadHandler.text);
            if (bag?.items == null) yield break;
            foreach (var input in bag.items)
                HeistWorldNet.ApplyGuestInput(session, input);
        }
    }

    IEnumerator PullAndApplyState()
    {
        using (var req = UnityWebRequest.Get(Origin() + "/coop/" + code + "/state"))
        {
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success) yield break;
            var snap = JsonUtility.FromJson<CoopSnapshot>(req.downloadHandler.text);
            HeistWorldNet.Apply(session, snap);
        }
    }

    IEnumerator PostGuestInput()
    {
        if (session.LocalOperative == null) yield break;
        var input = HeistWorldNet.CaptureInput(session.LocalOperative);
        if (input == null) yield break;
        yield return Post("/coop/" + code + "/input", JsonUtility.ToJson(input));
    }

    IEnumerator Post(string path, string json)
    {
        string origin = Origin();
        string payload = json ?? "{}";
        using (var req = new UnityWebRequest(origin + path, "POST"))
        {
            req.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(payload));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            yield return req.SendWebRequest();
            if (req.result == UnityWebRequest.Result.Success) yield break;
            long status = req.responseCode;
            if (status != 405 && status != 501 && status != 404)
            {
                Debug.LogWarning("Heist coop POST " + path + " failed: " + status + " " + req.error);
                yield break;
            }
        }

        using (var get = UnityWebRequest.Get(origin + path + "?body=" + UnityWebRequest.EscapeURL(payload)))
        {
            yield return get.SendWebRequest();
            if (get.result != UnityWebRequest.Result.Success)
                Debug.LogWarning("Heist coop GET-seed " + path + " failed: " + get.responseCode + " " + get.error);
        }
    }

    string Origin()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        string page = PageOrigin();
        if (!string.IsNullOrEmpty(page)) return page;
#endif
        if (session != null && session.Launch != null && !string.IsNullOrEmpty(session.Launch.origin))
            return session.Launch.origin.TrimEnd('/');
        return "http://127.0.0.1:8765";
    }

    static string PageOrigin()
    {
        string abs = Application.absoluteURL;
        if (string.IsNullOrEmpty(abs)) return "";
        try
        {
            var uri = new Uri(abs);
            return uri.GetLeftPart(UriPartial.Authority);
        }
        catch (Exception)
        {
            return "";
        }
    }
}

[System.Serializable]
public class CoopOpState
{
    public string id;
    public float x;
    public float z;
    public bool downed;
    public bool loot;
    public float hp;
}

[System.Serializable]
public class CoopSnapshot
{
    public float heat;
    public bool vaultOpen;
    public float lootX;
    public float lootZ;
    public bool ended;
    public bool success;
    public string caption;
    public string resultJson;
    public CoopOpState[] ops;
    public CoopPropState[] props;
    public CoopGuardState[] guards;
    public CoopCameraState[] cameras;
}

[System.Serializable]
public class CoopGuardState
{
    public int id;
    public float x;
    public float z;
    public float yaw;
    public float hp;
    public bool downed;
}

[System.Serializable]
public class CoopPropState
{
    public int id;
    public float x;
    public float z;
    public string kind;
    public bool on;
    public bool done;
}

[System.Serializable]
public class CoopCameraState
{
    public int id;
    public bool on;
    public bool jammed;
    public bool tracking;
}

[System.Serializable]
public class CoopInput
{
    public string id;
    public float x;
    public float z;
    public bool loot;
    public int interact = -1;
    public bool done;
    public int hit = -1;
    public bool distract;
}

[System.Serializable]
public class CoopInputBag
{
    public CoopInput[] items;
}
