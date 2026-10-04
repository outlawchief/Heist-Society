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

    string BuildSnapshot()
    {
        var snap = new CoopSnapshot
        {
            heat = session.Heat.Value,
            vaultOpen = session.VaultOpen,
            ended = session.Ended,
            success = session.Success,
            caption = session.Caption,
            resultJson = session.Result != null ? JsonUtility.ToJson(session.Result) : "",
            ops = new CoopOpState[session.Operatives.Count],
            props = BuildProps(),
            guards = BuildGuards()
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
                    ApplyGuestInteract(op, input);
                    ApplyGuestCombat(op, input);
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
            ApplyProps(snap.props);
            ApplyGuards(snap.guards);
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
            if (!string.IsNullOrEmpty(snap.resultJson))
                session.Result = JsonUtility.FromJson<HeistResult>(snap.resultJson);
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
            loot = session.LocalOperative.carryingLoot,
            interact = session.LocalOperative.pendingProp,
            done = session.LocalOperative.pendingProp >= 0,
            hit = session.LocalOperative.pendingHit,
            distract = session.LocalOperative.pendingDistract
        };
        yield return Post("/coop/" + code + "/input", JsonUtility.ToJson(input));
        session.LocalOperative.pendingHit = -1;
        session.LocalOperative.pendingDistract = false;
    }

    CoopPropState[] BuildProps()
    {
        if (session.Level == null) return new CoopPropState[0];
        var list = session.Level.Interactables;
        var props = new CoopPropState[list.Count];
        for (int i = 0; i < list.Count; i++)
        {
            var item = list[i];
            props[i] = new CoopPropState
            {
                id = i,
                on = item != null && item.gameObject.activeSelf,
                done = item != null && item.completed
            };
        }
        return props;
    }

    void ApplyProps(CoopPropState[] props)
    {
        if (props == null || session.Level == null) return;
        var list = session.Level.Interactables;
        foreach (var prop in props)
        {
            if (prop == null || prop.id < 0 || prop.id >= list.Count) continue;
            var item = list[prop.id];
            if (item == null) continue;
            if (prop.on && !item.gameObject.activeSelf)
                session.RevealNetworked(item);
            if (prop.done && !item.completed)
                item.Complete(null);
            if (session.LocalOperative != null && session.LocalOperative.pendingProp == prop.id && prop.done)
                session.LocalOperative.pendingProp = -1;
        }
    }

    void ApplyGuestInteract(HeistOperative op, CoopInput input)
    {
        if (op == null || input == null || !input.done || input.interact < 0) return;
        if (session.Level == null) return;
        var list = session.Level.Interactables;
        if (input.interact >= list.Count) return;
        var item = list[input.interact];
        if (item == null || item.completed) return;
        if (!item.gameObject.activeSelf) session.RevealNetworked(item);
        item.Complete(op);
        session.OnInteractSuccess(op, item);
    }

    void ApplyGuestCombat(HeistOperative op, CoopInput input)
    {
        if (op == null || input == null) return;
        if (input.hit >= 0 && input.hit < session.Guards.Count)
        {
            var guard = session.Guards[input.hit];
            if (guard != null && !guard.downed)
            {
                int damage = op.MeleeDamage;
                bool dropped = guard.TakeHit(damage, op.transform);
                session.Heat.Add(session.Heat.InCameraView(op.transform.position) ? 8f : 3f, "melee");
                if (dropped)
                {
                    session.Heat.Add(6f, "guard down");
                    session.SetCaption($"{op.Member.name} drops a guard ({damage} dmg).");
                }
            }
        }
        if (input.distract) session.OnDistract(op);
    }

    CoopGuardState[] BuildGuards()
    {
        var list = session.Guards;
        var guards = new CoopGuardState[list.Count];
        for (int i = 0; i < list.Count; i++)
        {
            var g = list[i];
            var p = g != null ? g.transform.position : Vector3.zero;
            guards[i] = new CoopGuardState
            {
                id = i,
                x = p.x,
                z = p.z,
                yaw = g != null ? g.transform.eulerAngles.y : 0f,
                hp = g != null ? g.Health : 0f,
                downed = g != null && g.downed
            };
        }
        return guards;
    }

    void ApplyGuards(CoopGuardState[] guards)
    {
        if (guards == null) return;
        for (int i = 0; i < guards.Length; i++)
        {
            var state = guards[i];
            if (state == null) continue;
            var guard = session.EnsureRemoteGuard(i, new Vector3(state.x, 0.9f, state.z));
            if (guard == null) continue;
            guard.ApplyRemote(state.x, state.z, state.yaw, state.hp, state.downed);
        }
        for (int i = guards.Length; i < session.Guards.Count; i++)
        {
            if (session.Guards[i] != null) session.Guards[i].gameObject.SetActive(false);
        }
    }

    IEnumerator Post(string path, string json)
    {
        var req = new UnityWebRequest(Origin() + path, "POST");
        req.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json ?? "{}"));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        yield return req.SendWebRequest();
        if (req.result != UnityWebRequest.Result.Success)
            Debug.LogWarning("Heist coop POST " + path + " failed: " + req.responseCode + " " + req.error);
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
    public bool ended;
    public bool success;
    public string caption;
    public string resultJson;
    public CoopOpState[] ops;
    public CoopPropState[] props;
    public CoopGuardState[] guards;
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
    public bool on;
    public bool done;
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
