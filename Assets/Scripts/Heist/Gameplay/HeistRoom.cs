using System.Collections.Generic;
using UnityEngine;

public class HeistRoom : MonoBehaviour
{
    public const float DoorWidth = 1.9f;
    public const float WallThickness = 0.25f;
    public const float WallHeight = 2.4f;
    public const float WallCenterY = 1.2f;
    static readonly Color WallColor = new Color(0.16f, 0.08f, 0.09f);

    public HeistRoomPlan Plan;
    public int Index;

    public Vector3 Center => new Vector3(Plan.cx, 0f, Plan.cz);

    public static HeistRoom Spawn(
        Transform world,
        HeistRoomPlan plan,
        int index,
        List<HeistRoomPlan> layout,
        List<HeistHiddenSeal> seals)
    {
        var go = new GameObject("Room_" + plan.name);
        go.transform.SetParent(world, false);
        var room = go.AddComponent<HeistRoom>();
        room.Plan = plan;
        room.Index = index;
        room.BuildShell(layout, seals);
        return room;
    }

    void BuildShell(List<HeistRoomPlan> layout, List<HeistHiddenSeal> seals)
    {
        Vector3 c = Center;
        HeistPrims.Cube(transform, c + new Vector3(0f, -0.05f, 0f), new Vector3(Plan.width, 0.1f, Plan.depth), new Color(0.1f, 0.11f, 0.13f), "Floor");
        HeistPrims.Label(transform, c + new Vector3(0f, 0.2f, Plan.depth * 0.42f), Plan.name.ToUpperInvariant(), 0.07f);

        for (int dir = 0; dir < 4; dir++)
        {
            var openings = new List<Vector2>();
            var omitted = new List<Vector2>();
            var hidden = new List<float>();
            var ownedByNeighbor = new List<Vector2>();
            CollectSide(layout, dir, openings, omitted, hidden, ownedByNeighbor, true);
            BuildWall(c, dir, openings, omitted);
            foreach (float along in hidden)
                SpawnSeal(c, dir, along, seals);
        }
    }

    void CollectSide(List<HeistRoomPlan> layout, int dir, List<Vector2> openings, List<Vector2> omitted, List<float> hidden, List<Vector2> ownedByNeighbor, bool openingsOnlyForOwner)
    {
        for (int other = 0; other < layout.Count; other++)
        {
            if (other == Index) continue;
            if (!SharesSide(layout[other], dir, out float a, out float b)) continue;

            if (other < Index)
            {
                omitted.Add(new Vector2(a - 0.02f, b + 0.02f));
            }
            if (ownedByNeighbor != null && other < Index)
                ownedByNeighbor.Add(new Vector2(a, b));
            if (openingsOnlyForOwner && other < Index) continue;

            bool linked = Plan.links.Contains(other);
            bool secret = Plan.hiddenLinks.Contains(other);
            if (!linked && !secret) continue;

            float mid = (a + b) * 0.5f;
            if (linked && TryOpening(mid, a, b, DoorWidth, out Vector2 door))
                openings.Add(door);
            if (!secret) continue;

            float secretAt = linked ? mid + 2.2f : mid;
            if (!TryOpening(secretAt, a, b, DoorWidth, out Vector2 hatch)) continue;
            if (linked && openings.Count > 0 && IntervalsOverlap(hatch, openings[openings.Count - 1]))
            {
                var main = openings[openings.Count - 1];
                float beside = main.y + 0.4f + DoorWidth * 0.5f;
                if (!TryOpening(beside, a, b, DoorWidth, out hatch) || IntervalsOverlap(hatch, main))
                {
                    beside = main.x - 0.4f - DoorWidth * 0.5f;
                    if (!TryOpening(beside, a, b, DoorWidth, out hatch) || IntervalsOverlap(hatch, main))
                        continue;
                }
            }
            openings.Add(hatch);
            hidden.Add((hatch.x + hatch.y) * 0.5f);
        }
    }

            bool linked = Plan.links.Contains(other);
            bool secret = Plan.hiddenLinks.Contains(other);
            if (!linked && !secret) continue;
            float mid = (a + b) * 0.5f;
            if (linked && TryOpening(mid, a, b, DoorWidth, out Vector2 door))
                openings.Add(door);

            if (!secret) continue;

            float secretAt = linked ? mid + 2.2f : mid;
            if (!TryOpening(secretAt, a, b, DoorWidth, out Vector2 hatch)) continue;
            if (linked && openings.Count > 0 && IntervalsOverlap(hatch, openings[openings.Count - 1]))
            {
                var main = openings[openings.Count - 1];
                float beside = main.y + 0.4f + DoorWidth * 0.5f;
                if (!TryOpening(beside, a, b, DoorWidth, out hatch) || IntervalsOverlap(hatch, main))
                {
                    beside = main.x - 0.4f - DoorWidth * 0.5f;
                    if (!TryOpening(beside, a, b, DoorWidth, out hatch) || IntervalsOverlap(hatch, main))
                        continue;
                }
            }
            openings.Add(hatch);
            hidden.Add((hatch.x + hatch.y) * 0.5f);
        {
            float seg = span.y - span.x;
            Vector3 pos = mid + axis * ((span.x + span.y) * 0.5f);
            Vector3 scale = axis == Vector3.right
                ? new Vector3(seg, WallHeight, WallThickness)
                : new Vector3(WallThickness, WallHeight, seg);
            HeistPrims.Cube(transform, pos, scale, WallColor, name);
        }
    }

    static List<Vector2> SolidSpans(float start, float end, List<Vector2> openings, List<Vector2> omitted)
    {
        var gaps = new List<Vector2>();
        if (openings != null)
        {
            foreach (var opening in openings)
            {
                float a = Mathf.Max(start, opening.x);
                float b = Mathf.Min(end, opening.y);
                if (b - a > 0.05f) gaps.Add(new Vector2(a, b));
            }
        }
        if (omitted != null)
            }
        }
        if (omitted != null)
        {
            foreach (var span in omitted)
            {
                float a = Mathf.Max(start, span.x);
                float b = Mathf.Min(end, span.y);
                if (b - a > 0.05f) gaps.Add(new Vector2(a, b));
            }
        }

        gaps.Sort((p, q) => p.x.CompareTo(q.x));
        var merged = new List<Vector2>();
        foreach (var gap in gaps)
        {
            if (merged.Count == 0 || gap.x > merged[merged.Count - 1].y + 0.001f)
            {
                merged.Add(gap);
                continue;
            }
            var last = merged[merged.Count - 1];
            last.y = Mathf.Max(last.y, gap.y);
            merged[merged.Count - 1] = last;
        }

        var solid = new List<Vector2>();
        float cursor = start;
        foreach (var gap in merged)
        {
            if (gap.x - cursor >= 0.2f) solid.Add(new Vector2(cursor, gap.x));
            cursor = Mathf.Max(cursor, gap.y);
        }
        if (end - cursor >= 0.2f) solid.Add(new Vector2(cursor, end));
        return solid;
    }

    void SpawnSeal(Vector3 c, int dir, float along, List<HeistHiddenSeal> seals)
    {
        Vector3 pos;
        Vector3 scale;
        if (dir == 1 || dir == 3)
        {
            pos = new Vector3(c.x + (dir == 1 ? Plan.width * 0.5f : -Plan.width * 0.5f), 1.2f, c.z + along);
            scale = new Vector3(0.26f, 2.4f, DoorWidth);
        }
        else
        {
            pos = new Vector3(c.x + along, 1.2f, c.z + (dir == 0 ? Plan.depth * 0.5f : -Plan.depth * 0.5f));
            scale = new Vector3(DoorWidth, 2.4f, 0.26f);
        }
        var slab = HeistPrims.Cube(transform, pos, scale, WallColor, "HiddenSeal");
        seals.Add(slab.AddComponent<HeistHiddenSeal>());
    }
}
