using UnityEngine;

public static class HeistWorldNet
{
    public static string BuildJson(HeistGameSession session) => JsonUtility.ToJson(Build(session));

    public static CoopSnapshot Build(HeistGameSession session)
    {
        var snap = new CoopSnapshot
        {
            heat = session.Heat != null ? session.Heat.Value : 0f,
            vaultOpen = session.VaultOpen,
            lootX = session.Loot != null ? session.Loot.position.x : 0f,
            lootZ = session.Loot != null ? session.Loot.position.z : 0f,
            ended = session.Ended,
            success = session.Success,
            caption = session.Caption,
            resultJson = session.Result != null ? JsonUtility.ToJson(session.Result) : "",
            ops = new CoopOpState[session.Operatives.Count],
            props = BuildProps(session),
            guards = BuildGuards(session),
            cameras = BuildCameras(session)
        };
        for (int i = 0; i < session.Operatives.Count; i++)
        {
            var op = session.Operatives[i];
            if (op == null || op.Member == null) continue;
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
        return snap;
    }

    public static void Apply(HeistGameSession session, string json)
    {
        if (session == null || string.IsNullOrEmpty(json)) return;
        Apply(session, JsonUtility.FromJson<CoopSnapshot>(json));
    }

    public static void Apply(HeistGameSession session, CoopSnapshot snap)
    {
        if (session == null || snap == null) return;
        if (session.Heat != null) session.Heat.Value = snap.heat;
        if (!string.IsNullOrEmpty(snap.caption)) session.Caption = snap.caption;
        session.VaultOpen = snap.vaultOpen;
        ApplyProps(session, snap.props);
        ApplyGuards(session, snap.guards);
        ApplyCameras(session, snap.cameras);
        if (snap.ops != null)
        {
            foreach (var state in snap.ops)
            {
                if (state == null) continue;
                foreach (var op in session.Operatives)
                {
                    if (op == null || op.Member == null || op.Member.id != state.id) continue;
                    if (op.isLocal)
                    {
                        op.Health = state.hp;
                        if (state.downed && !op.downed) op.Down("downed");
                        continue;
                    }
                    op.ApplyRemote(new Vector3(state.x, op.transform.position.y, state.z), state.downed, state.loot, state.hp);
                }
            }
        }
        ApplyLoot(session, snap);
        if (snap.ended && !session.Ended)
        {
            if (snap.success) session.WinHeist();
            else session.FailHeist("session ended");
        }
        if (!string.IsNullOrEmpty(snap.resultJson))
            session.Result = JsonUtility.FromJson<HeistResult>(snap.resultJson);
    }

    public static void ApplyGuestInput(HeistGameSession session, CoopInput input)
    {
        if (session == null || input == null || string.IsNullOrEmpty(input.id)) return;
        foreach (var op in session.Operatives)
        {
            if (op == null || op.Member == null || op.isLocal || op.Member.id != input.id) continue;
            op.SetRole(false, false);
            op.ApplyRemote(new Vector3(input.x, op.transform.position.y, input.z), op.downed, input.loot, op.Health);
            ApplyGuestInteract(session, op, input);
            ApplyGuestCombat(session, op, input);
        }
    }

    public static CoopInput CaptureInput(HeistOperative op)
    {
        if (op == null || op.Member == null) return null;
        var p = op.transform.position;
        var input = new CoopInput
        {
            id = op.Member.id,
            x = p.x,
            z = p.z,
            loot = op.carryingLoot,
            interact = op.pendingProp,
            done = op.pendingProp >= 0,
            hit = op.pendingHit,
            distract = op.pendingDistract
        };
        op.pendingHit = -1;
        op.pendingDistract = false;
        return input;
    }

    static CoopPropState[] BuildProps(HeistGameSession session)
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
                x = item != null ? item.transform.position.x : 0f,
                z = item != null ? item.transform.position.z : 0f,
                kind = Kind(item),
                on = item != null && item.gameObject.activeSelf,
                done = item != null && item.completed
            };
        }
        return props;
    }

    static void ApplyProps(HeistGameSession session, CoopPropState[] props)
    {
        if (props == null || session.Level == null) return;
        foreach (var prop in props)
            ApplyProp(session, prop);
    }

    public static void ApplyProp(HeistGameSession session, CoopPropState prop)
    {
        if (session == null || prop == null || session.Level == null) return;
        var item = FindProp(session, prop);
        if (item == null) return;
        if (prop.on && !item.gameObject.activeSelf)
            session.RevealNetworked(item);
        if (prop.done && !item.completed)
        {
            item.Complete(null);
            session.OnInteractSuccess(null, item);
        }
        if (session.LocalOperative != null && session.Level.IndexOf(item) == session.LocalOperative.pendingProp && prop.done)
            session.LocalOperative.pendingProp = -1;
    }

    public static CoopPropState CaptureProp(HeistInteractable item, int id = -1)
    {
        if (item == null) return null;
        return new CoopPropState
        {
            id = id,
            x = item.transform.position.x,
            z = item.transform.position.z,
            kind = Kind(item),
            on = item.gameObject.activeSelf,
            done = item.completed
        };
    }

    static HeistInteractable FindProp(HeistGameSession session, CoopPropState prop)
    {
        var list = session.Level.Interactables;
        if (list == null) return null;
        string kind = prop.kind ?? "";
        if (prop.id >= 0 && prop.id < list.Count)
        {
            var byId = list[prop.id];
            if (byId != null && (string.IsNullOrEmpty(kind) || Kind(byId) == kind))
            {
                float d = Flat(byId.transform.position, prop.x, prop.z);
                if (d < 3.5f || (prop.x == 0f && prop.z == 0f)) return byId;
            }
        }

        HeistInteractable best = null;
        float bestD = 3.5f;
        foreach (var item in list)
        {
            if (item == null) continue;
            if (!string.IsNullOrEmpty(kind) && Kind(item) != kind) continue;
            float d = Flat(item.transform.position, prop.x, prop.z);
            if (d < bestD)
            {
                bestD = d;
                best = item;
            }
        }
        return best;
    }

    static string Kind(HeistInteractable item)
    {
        if (item == null || item.challenge == null || string.IsNullOrEmpty(item.challenge.type)) return "";
        return item.challenge.type;
    }

    static float Flat(Vector3 pos, float x, float z)
    {
        float dx = pos.x - x;
        float dz = pos.z - z;
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    static CoopGuardState[] BuildGuards(HeistGameSession session)
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

    static void ApplyGuards(HeistGameSession session, CoopGuardState[] guards)
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

    static CoopCameraState[] BuildCameras(HeistGameSession session)
    {
        if (session.Level == null) return new CoopCameraState[0];
        var list = session.Level.SecurityCameras;
        var cameras = new CoopCameraState[list.Count];
        for (int i = 0; i < list.Count; i++)
        {
            var cam = list[i];
            cameras[i] = new CoopCameraState
            {
                id = i,
                on = cam != null && cam.revealed,
                jammed = cam != null && cam.jammed,
                tracking = cam != null && cam.tracking
            };
        }
        return cameras;
    }

    static void ApplyCameras(HeistGameSession session, CoopCameraState[] cameras)
    {
        if (cameras == null || session.Level == null) return;
        var list = session.Level.SecurityCameras;
        foreach (var state in cameras)
        {
            if (state == null || state.id < 0 || state.id >= list.Count) continue;
            var cam = list[state.id];
            if (cam == null) continue;
            cam.ApplyRemote(state.on, state.jammed, state.tracking);
        }
    }

    static void ApplyLoot(HeistGameSession session, CoopSnapshot snap)
    {
        if (snap == null || !snap.vaultOpen) return;
        Vector3 atVault = session.Level != null
            ? session.Level.VaultPoint + Vector3.up * 0.6f
            : new Vector3(snap.lootX, 0.9f, snap.lootZ);
        if (session.Loot == null)
            session.SpawnLoot(atVault);

        HeistOperative carrier = null;
        foreach (var op in session.Operatives)
        {
            if (op != null && op.carryingLoot) carrier = op;
        }
        if (session.Loot == null) return;
        if (carrier != null && !carrier.isLocal)
            session.Loot.position = carrier.transform.position + Vector3.up * 1.3f + carrier.transform.forward * 0.4f;
        else if (carrier == null)
            session.Loot.position = new Vector3(snap.lootX, session.Loot.position.y, snap.lootZ);
    }

    static void ApplyGuestInteract(HeistGameSession session, HeistOperative op, CoopInput input)
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

    static void ApplyGuestCombat(HeistGameSession session, HeistOperative op, CoopInput input)
    {
        if (op == null || input == null) return;
        if (input.hit >= 0 && input.hit < session.Guards.Count)
        {
            var guard = session.Guards[input.hit];
            if (guard != null && !guard.downed)
            {
                int damage = op.MeleeDamage;
                bool dropped = guard.TakeHit(damage, op.transform);
                if (session.Heat != null)
                    session.Heat.Add(session.Heat.InCameraView(op.transform.position) ? 8f : 3f, "melee");
                if (dropped)
                {
                    if (session.Heat != null) session.Heat.Add(6f, "guard down");
                    session.SetCaption($"{op.Member.name} drops a guard ({damage} dmg).");
                }
            }
        }
        if (input.distract) session.OnDistract(op);
    }
}
