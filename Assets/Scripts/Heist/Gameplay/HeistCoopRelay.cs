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

        while (session != null && !session.Ended)
        {
            if (host)
            {
                yield return Post("/coop/" + code + "/state", BuildSnapshot());
                yield return ApplyGuestInputs();
            }
            else
            {
                yield return PullAndApplyState();
                yield return PostGuestInput();
            }
            yield return new WaitForSeconds(0.15f);
        }
    }

    string BuildSnapshot()
    {
        var snap = new CoopSnapshot
        {
            heat = session.Heat.Value,
            vaultOpen = session.VaultOpen,
            ended = session.Ended,
            success = session.Success,
            caption = session.Caption,
            ops = new CoopOpState[session.Operatives.Count]
        };
        for (int i = 0; i < session.Operatives.Count; i++)
        {
            var op = session.Operatives[i];
            var p = op.transform.position;
            snap.ops[i] = new CoopOpState
            {
                id = op.Member.id,
                x = p.x,
                z = p.z,
                downed = op.downed,
                loot = op.carryingLoot,
                hp = op.Health
            };
        }
        return JsonUtility.ToJson(snap);
    }

    IEnumerator ApplyGuestInputs()
    {
        using (var req = UnityWebRequest.Get(Origin() + "/coop/" + code + "/input"))
        {
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success) yield break;
            var bag = JsonUtility.FromJson<CoopInputBag>(req.downloadHandler.text);
            if (bag?.items == null) yield break;
            foreach (var input in bag.items)
            {
                foreach (var op in session.Operatives)
                {
                    if (op.isLocal || op.Member.id != input.id) continue;
                    op.isAi = false;
                    op.ApplyRemote(new Vector3(input.x, op.transform.position.y, input.z), op.downed, input.loot, op.Health);
                }
            }
        }
    }

    IEnumerator PullAndApplyState()
    {
        using (var req = UnityWebRequest.Get(Origin() + "/coop/" + code + "/state"))
        {
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success) yield break;
            var snap = JsonUtility.FromJson<CoopSnapshot>(req.downloadHandler.text);
            if (snap?.ops == null) yield break;
            session.Heat.Value = snap.heat;
            session.Caption = snap.caption;
            session.VaultOpen = snap.vaultOpen;
            foreach (var state in snap.ops)
            {
                foreach (var op in session.Operatives)
                {
                    if (op.Member.id != state.id) continue;
                    if (op.isLocal)
                    {
                        op.Health = state.hp;
                        if (state.downed && !op.downed) op.Down("downed");
                        continue;
                    }
                    op.ApplyRemote(new Vector3(state.x, op.transform.position.y, state.z), state.downed, state.loot, state.hp);
                }
            }
            if (snap.ended && !session.Ended)
            {
                if (snap.success) session.WinHeist();
                else session.FailHeist("session ended");
            }
        }
    }

    IEnumerator PostGuestInput()
    {
        if (session.LocalOperative == null) yield break;
        var p = session.LocalOperative.transform.position;
        var input = new CoopInput
        {
            id = session.LocalOperative.Member.id,
            x = p.x,
            z = p.z,
            loot = session.LocalOperative.carryingLoot
        };
        yield return Post("/coop/" + code + "/input", JsonUtility.ToJson(input));
    }

    IEnumerator Post(string path, string json)
    {
        var req = new UnityWebRequest(Origin() + path, "POST");
        req.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        yield return req.SendWebRequest();
    }

    string Origin()
    {
        if (!string.IsNullOrEmpty(session.Launch.origin)) return session.Launch.origin.TrimEnd('/');
        return "http://127.0.0.1:8765";
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
    public bool ended;
    public bool success;
    public string caption;
    public CoopOpState[] ops;
}

[System.Serializable]
public class CoopInput
{
    public string id;
    public float x;
    public float z;
    public bool loot;
}

[System.Serializable]
public class CoopInputBag
{
    public CoopInput[] items;
}
