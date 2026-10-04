using System.Collections.Generic;
using UnityEngine;

public static class HeistFurniturePlacer
{
    public static void Place(HeistLevelBuilder level, int seed)
    {
        if (level == null || level.Root == null) return;
        var catalog = Resources.Load<HeistFurnitureCatalog>("HeistFurniture");
        var rng = new System.Random(seed ^ 0xF17E);
        var occupied = new List<Vector3>();
        foreach (var interactable in level.Interactables)
        {
            if (interactable != null) occupied.Add(interactable.transform.position);
        }

        for (int i = 0; i < level.Layout.Count; i++)
        {
            var plan = level.Layout[i];
            var kind = HeistRoomKinds.Of(plan);
            int want = CountFor(kind, rng);
            var prefabs = catalog != null ? catalog.For(kind) : null;
            var slots = WallSlots(plan, level.Center(plan));
            Shuffle(slots, rng);
            int placed = 0;
            for (int s = 0; s < slots.Count && placed < want; s++)
            {
                Vector3 pos = slots[s];
                if (!IsClear(level, plan, pos, occupied)) continue;
                Quaternion rot = FaceInward(plan, pos);
                if (!TryPrefab(level.Root, prefabs, rng, pos, rot))
                    SpawnPrimitive(level.Root, kind, rng, pos, rot);
                occupied.Add(pos);
                placed++;
            }
        }
    }

    static int CountFor(HeistRoomKind kind, System.Random rng)
    {
        switch (kind)
        {
            case HeistRoomKind.Corridor: return rng.Next(1, 3);
            case HeistRoomKind.Extract: return rng.Next(2, 4);
            case HeistRoomKind.Vault: return rng.Next(3, 5);
            case HeistRoomKind.Lobby: return rng.Next(4, 7);
            case HeistRoomKind.Office: return rng.Next(6, 9);
            case HeistRoomKind.Archives: return rng.Next(6, 9);
            case HeistRoomKind.Security: return rng.Next(4, 6);
            default: return rng.Next(4, 7);
        }
    }

    static List<Vector3> WallSlots(HeistRoomPlan plan, Vector3 c)
    {
        var slots = new List<Vector3>();
        float hw = plan.width * 0.5f - 0.7f;
        float hd = plan.depth * 0.5f - 0.7f;
        int across = Mathf.Max(3, Mathf.RoundToInt(plan.width / 2.4f));
        int along = Mathf.Max(3, Mathf.RoundToInt(plan.depth / 2.4f));
        for (int i = 0; i < across; i++)
        {
            float t = (i + 0.5f) / across * 2f - 1f;
            float x = t * (hw - 0.3f);
            slots.Add(c + new Vector3(x, 0f, hd));
            slots.Add(c + new Vector3(x, 0f, -hd));
        }
        for (int i = 0; i < along; i++)
        {
            float t = (i + 0.5f) / along * 2f - 1f;
            float z = t * (hd - 0.3f);
            slots.Add(c + new Vector3(-hw, 0f, z));
            slots.Add(c + new Vector3(hw, 0f, z));
        }
        return slots;
    }

    static bool IsClear(HeistLevelBuilder level, HeistRoomPlan plan, Vector3 pos, List<Vector3> occupied)
    {
        Vector3 flat = pos;
        flat.y = 0f;
        if (Flat(flat, level.ExtractPoint) < 2.2f) return false;
        if (level.VaultPoint.sqrMagnitude > 0.01f && Flat(flat, level.VaultPoint) < 1.8f) return false;
        foreach (var door in level.DoorPoints)
        {
            if (Flat(flat, door) < 2.4f) return false;
        }
        foreach (var other in occupied)
        {
            if (Flat(flat, other) < 1.15f) return false;
        }
        var loop = level.PatrolLoop(plan);
        for (int i = 0; i < loop.Length; i++)
        {
            Vector3 a = loop[i];
            Vector3 b = loop[(i + 1) % loop.Length];
            if (DistToSegment(flat, a, b) < 1.2f) return false;
        }
        return true;
    }

    static float Flat(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    static float DistToSegment(Vector3 p, Vector3 a, Vector3 b)
    {
        p.y = 0f;
        a.y = 0f;
        b.y = 0f;
        Vector3 ab = b - a;
        float len = ab.sqrMagnitude;
        if (len < 0.0001f) return Vector3.Distance(p, a);
        float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / len);
        return Vector3.Distance(p, a + ab * t);
    }

    static Quaternion FaceInward(HeistRoomPlan plan, Vector3 pos)
    {
        Vector3 c = new Vector3(plan.cx, 0f, plan.cz);
        Vector3 to = c - pos;
        to.y = 0f;
        if (to.sqrMagnitude < 0.01f) to = Vector3.forward;
        return Quaternion.LookRotation(to.normalized, Vector3.up);
    }

    static bool TryPrefab(Transform parent, GameObject[] prefabs, System.Random rng, Vector3 pos, Quaternion rot)
    {
        if (prefabs == null || prefabs.Length == 0) return false;
        var pick = prefabs[rng.Next(0, prefabs.Length)];
        if (pick == null) return false;
        var go = Object.Instantiate(pick, pos, rot, parent);
        go.name = pick.name;
        return true;
    }

    static void SpawnPrimitive(Transform parent, HeistRoomKind kind, System.Random rng, Vector3 pos, Quaternion rot)
    {
        Color tone = Tone(kind);
        int roll = rng.Next(0, 5);
        GameObject go;
        switch (roll)
        {
            case 0:
                go = HeistPrims.Cube(parent, pos + Vector3.up * 0.4f, new Vector3(1.6f, 0.8f, 0.72f), tone, "Desk");
                break;
            case 1:
                go = HeistPrims.Cube(parent, pos + Vector3.up * 0.85f, new Vector3(1.7f, 1.7f, 0.38f), tone * 0.85f, "Shelf");
                break;
            case 2:
                go = HeistPrims.Cube(parent, pos + Vector3.up * 0.35f, new Vector3(0.7f, 0.7f, 0.7f), new Color(0.42f, 0.32f, 0.22f), "Crate");
                break;
            case 3:
                go = HeistPrims.Cube(parent, pos + Vector3.up * 0.28f, new Vector3(1.4f, 0.45f, 0.42f), tone, "Bench");
                break;
            default:
                go = HeistPrims.Capsule(parent, pos + Vector3.up * 0.35f, new Color(0.18f, 0.32f, 0.2f), "Planter");
                go.transform.localScale = new Vector3(0.55f, 0.4f, 0.55f);
                break;
        }
        go.transform.rotation = rot;
    }

    static Color Tone(HeistRoomKind kind)
    {
        switch (kind)
        {
            case HeistRoomKind.Extract: return new Color(0.28f, 0.32f, 0.3f);
            case HeistRoomKind.Corridor: return new Color(0.3f, 0.26f, 0.24f);
            case HeistRoomKind.Lobby: return new Color(0.38f, 0.3f, 0.26f);
            case HeistRoomKind.Office: return new Color(0.34f, 0.28f, 0.22f);
            case HeistRoomKind.Security: return new Color(0.22f, 0.24f, 0.3f);
            case HeistRoomKind.Archives: return new Color(0.4f, 0.32f, 0.22f);
            case HeistRoomKind.Vault: return new Color(0.45f, 0.4f, 0.28f);
            default: return new Color(0.32f, 0.26f, 0.22f);
        }
    }

    static void Shuffle(List<Vector3> list, System.Random rng)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
