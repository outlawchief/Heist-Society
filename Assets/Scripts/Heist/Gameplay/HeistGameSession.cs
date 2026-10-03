using System.Collections.Generic;
using UnityEngine;

public class HeistGameSession : MonoBehaviour
{
    public HeistLaunch Launch;
    public List<HeistRoomPlan> Rooms;
    public HeistLevelBuilder Level;
    public HeistHeatDirector Heat;
    public readonly List<HeistOperative> Operatives = new List<HeistOperative>();
    public readonly List<HeistGuard> Guards = new List<HeistGuard>();
    public HeistOperative LocalOperative;
    public Transform Loot;
    public bool VaultOpen;
    public bool Ended;
    public bool Success;
    public string Caption = "Infiltrate. Hit the vault. Extract.";
    public string JoinCode = "";
    public bool IsHost = true;

    public void Begin(HeistLaunch launch, string possessCrewId, bool host, string code)
    {
        Launch = launch;
        IsHost = host;
        JoinCode = code;
        Rooms = HeistLevelGenerator.Generate(Mathf.Clamp(launch.difficulty, 1, 7), launch.seed);
        Level = gameObject.AddComponent<HeistLevelBuilder>();
        Level.Build(Rooms);
        Heat = gameObject.AddComponent<HeistHeatDirector>();
        Heat.Setup(this);
        if (GetComponent<HeistHud>() == null) gameObject.AddComponent<HeistHud>();

        var colors = new[]
        {
            new Color(0.85f, 0.22f, 0.26f),
            new Color(0.95f, 0.9f, 0.72f),
            new Color(0.45f, 0.7f, 0.85f),
            new Color(0.6f, 0.85f, 0.5f)
        };

        string possess = string.IsNullOrEmpty(possessCrewId) && launch.crew.Length > 0 ? launch.crew[0].id : possessCrewId;
        Vector3 start = Level.ExtractPoint + Vector3.up * 0.2f;
        for (int i = 0; i < launch.crew.Length; i++)
        {
            var member = launch.crew[i];
            var body = HeistPrims.Capsule(Level.Root, start + Vector3.right * (i * 0.85f), colors[i % colors.Length], member.name);
            var op = body.AddComponent<HeistOperative>();
            bool local = member.id == possess;
            bool ai = host && !local;
            op.Setup(member, local, ai, colors[i % colors.Length], this);
            if (!host && !local)
            {
                op.isLocal = false;
                op.isAi = false;
            }
            Operatives.Add(op);
            if (local) LocalOperative = op;
        }

        if (LocalOperative == null && Operatives.Count > 0)
        {
            LocalOperative = Operatives[0];
            LocalOperative.isLocal = true;
            LocalOperative.isAi = false;
        }

        var follow = FindFirstObjectByType<HeistCameraFollow>();
        if (follow != null && LocalOperative != null) follow.target = LocalOperative.transform;

        if (host) SpawnGuardWave(0);
        Caption = "WASD move   E hold interact   Space melee   Q perceive   F distract   Shift sprint";
        if (!host)
        {
            foreach (var op in Operatives)
            {
                if (!op.isLocal) op.isAi = false;
            }
            Caption = "Joined " + code + ". You control " + (LocalOperative != null ? LocalOperative.Member.name : "an operative") + ".";
        }
    }

    public void SetCaption(string text) => Caption = text;

    public HeistOperative NearestStanding(Vector3 from, float range)
    {
        HeistOperative best = null;
        float bestD = range;
        foreach (var op in Operatives)
        {
            if (op == null || op.downed) continue;
            float d = Vector3.Distance(from, op.transform.position);
            if (d < bestD)
            {
                bestD = d;
                best = op;
            }
        }
        return best;
    }

    public void TryMelee(HeistOperative attacker)
    {
        foreach (var guard in Guards)
        {
            if (guard == null) continue;
            float d = Vector3.Distance(attacker.transform.position, guard.transform.position);
            if (d <= attacker.MeleeRange)
            {
                guard.Stun(0.6f + attacker.Member.stats.str * 0.12f);
                bool seen = Heat.InCameraView(attacker.transform.position);
                Heat.Add(seen ? 10f : 4f, "melee");
                SetCaption($"{attacker.Member.name} stuns a guard.");
                return;
            }
        }
    }

    public void OnInteractSuccess(HeistOperative op, HeistInteractable interactable)
    {
        SetCaption($"{op.Member.name} cleared {interactable.challenge.name}.");
        if (interactable.challenge.type == "vault")
        {
            VaultOpen = true;
            SpawnLoot(interactable.transform.position + Vector3.up * 0.6f);
            op.carryingLoot = true;
            SetCaption("Vault open. Carry the loot to EXTRACT.");
        }
        if (interactable.challenge.type == "bypass" || interactable.challenge.isBypass)
        {
            Level.RevealHidden();
            Heat.Add(-8f, "quiet path");
        }
        if (interactable.challenge.type == "cameras")
            SetCaption("Cameras jammed.");
    }

    public void OnInteractFail(HeistOperative op, HeistInteractable interactable)
    {
        Heat.Add(14f, "failed interact");
        SetCaption($"{op.Member.name} bungled {interactable.challenge.name}. Heat up.");
        if (interactable.challenge.type == "vault" && Heat.LockdownActive)
            FailHeist("vault attempt during lockdown");
    }

    public void OnPerception(HeistOperative op)
    {
        Level.RevealHidden();
        foreach (var interactable in Level.Interactables)
        {
            if (interactable.challenge != null && interactable.challenge.type == "cameras")
                HeistPrims.Paint(interactable.gameObject, new Color(0.7f, 0.85f, 1f));
        }
        SetCaption($"{op.Member.name} outlines threats and hidden routes.");
    }

    public void OnDistract(HeistOperative op)
    {
        float range = 3f + op.Member.stats.cha * 0.4f;
        foreach (var guard in Guards)
        {
            if (guard == null) continue;
            if (Vector3.Distance(guard.transform.position, op.transform.position) < range)
            {
                guard.Stun(2f + op.Member.stats.cha * 0.2f);
                Vector3 away = guard.transform.position + op.transform.right * 3f;
                guard.home = away;
            }
        }
        SetCaption($"{op.Member.name} pulls focus.");
    }

    public void SpawnLoot(Vector3 pos)
    {
        if (Loot != null) return;
        var go = HeistPrims.Cube(Level.Root, pos, new Vector3(0.5f, 0.5f, 0.5f), new Color(0.95f, 0.8f, 0.2f), "Loot");
        Loot = go.transform;
    }

    public void SpawnGuardWave(int notch)
    {
        int count = notch == 0 ? 1 : notch;
        for (int i = 0; i < count; i++)
        {
            Vector3 room = Level.RoomCenters[Mathf.Min(Level.RoomCenters.Count - 1, 1 + i % Level.RoomCenters.Count)];
            Vector3 spawn = room + new Vector3(Random.Range(-2f, 2f), 0.9f, Random.Range(-2f, 2f));
            var body = HeistPrims.Capsule(Level.Root, spawn, new Color(0.55f, 0.15f, 0.18f), "Guard");
            var guard = body.AddComponent<HeistGuard>();
            guard.Setup(this, spawn, 2.3f + notch * 0.25f);
            Guards.Add(guard);
        }
    }

    void Update()
    {
        if (Ended || !IsHost) return;
        foreach (var op in Operatives)
        {
            if (op != null && op.carryingLoot &&
                Vector3.Distance(op.transform.position, Level.ExtractPoint) < 1.8f)
            {
                WinHeist();
                return;
            }
        }

        if (AllDown()) FailHeist("crew captured");
    }

    bool AllDown()
    {
        if (Operatives.Count == 0) return false;
        foreach (var op in Operatives)
        {
            if (op != null && !op.downed) return false;
        }
        return true;
    }

    public void WinHeist()
    {
        if (Ended) return;
        Ended = true;
        Success = true;
        Caption = "Score secured. Returning to lobby.";
        Finish(true);
    }

    public void FailHeist(string reason)
    {
        if (Ended) return;
        Ended = true;
        Success = false;
        Caption = "Heist collapsed: " + reason;
        Finish(false);
    }

    void Finish(bool success)
    {
        if (!IsHost)
        {
            Ended = true;
            Success = success;
            return;
        }
        var statuses = new Dictionary<string, string>();
        foreach (var op in Operatives)
        {
            statuses[op.Member.id] = op.downed ? "captured" : "ok";
        }
        foreach (var interactable in Level.Interactables)
        {
            if (interactable.challenge != null && !interactable.completed && interactable.challenge.type == "vault")
                interactable.challenge.passed = false;
        }

        float loot = success ? 1f : 0f;
        var result = HeistResolver.FinalizeLive(Launch, Rooms, Mathf.RoundToInt(Heat.Value), success, statuses, loot);
        HeistBootstrap.NotifyResult(result);
    }
}
