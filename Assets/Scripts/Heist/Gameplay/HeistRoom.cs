using System.Collections.Generic;
using UnityEngine;

public class HeistRoom : MonoBehaviour
{
    public const float DoorWidth = 1.9f;
    public const float WallThickness = 0.25f;
    public const float WallHeight = 2.4f;
    public const float WallCenterY = 1.2f;
    public const float JambOverlap = 0.12f;
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
            CollectSide(layout, dir, openings, omitted, hidden);
            BuildWall(c, dir, openings, omitted);
            foreach (float along in hidden)
                SpawnSeal(c, dir, along, seals);
        }
    }

    public bool TryVentMount(List<HeistRoomPlan> layout, System.Random rng, out Vector3 pos, out Quaternion rotation)
    {
        pos = Vector3.zero;
        rotation = Quaternion.identity;
        var options = new List<Vector3>();
        for (int dir = 0; dir < 4; dir++)
        {
            var openings = new List<Vector2>();
            var omitted = new List<Vector2>();
            var hidden = new List<float>();
            CollectSide(layout, dir, openings, omitted, hidden);
            float limit = SideLength(dir) * 0.5f;
            foreach (var span in SolidSpans(-limit, limit, openings, omitted))
            {
                if (span.y - span.x < 1.2f) continue;
                options.Add(new Vector3(dir, span.x, span.y));
            }
        }

        if (options.Count == 0) return false;
        var pick = options[rng.Next(0, options.Count)];
        int wall = Mathf.RoundToInt(pick.x);
        float margin = 0.55f;
        float along = Mathf.Lerp(pick.y + margin, pick.z - margin, (float)rng.NextDouble());
        pos = VentPosition(wall, along);
        rotation = Quaternion.LookRotation(Inward(wall), Vector3.up);
        return true;
    }

    void CollectSide(List<HeistRoomPlan> layout, int dir, List<Vector2> openings, List<Vector2> omitted, List<float> hidden)
    {
        for (int other = 0; other < layout.Count; other++)
        {
            if (other == Index) continue;
            if (!SharesSide(layout[other], dir, out float lo, out float hi)) continue;

            if (other < Index)
            {
                omitted.Add(new Vector2(lo - 0.02f, hi + 0.02f));
                continue;
            }

            bool linked = Plan.links.Contains(other);
            bool secret = Plan.hiddenLinks.Contains(other);
            if (!linked && !secret) continue;

            if (linked && TryResolveOpening(lo, hi, true, false, false, out Vector2 door))
                openings.Add(InsetForWall(door));
            if (!secret) continue;
            if (!TryResolveOpening(lo, hi, linked, true, true, out Vector2 hatch)) continue;
            openings.Add(InsetForWall(hatch));
            hidden.Add((hatch.x + hatch.y) * 0.5f);
        }
    }

    public static bool TryConnectorPose(
        HeistRoomPlan from,
        HeistRoomPlan to,
        List<HeistRoomPlan> layout,
        bool hidden,
        out Vector3 pos,
        out Vector3 scale)
    {
        pos = default;
        scale = default;
        if (from == null || to == null || layout == null) return false;
        int toIndex = layout.IndexOf(to);
        if (toIndex < 0) return false;

        int dir = HeistLevelGenerator.DirFrom(from, to);
        if (!SharesSide(from, to, dir, out float lo, out float hi)) return false;

        bool linked = from.links != null && from.links.Contains(toIndex);
        bool secret = from.hiddenLinks != null && from.hiddenLinks.Contains(toIndex);
        if (!TryResolveOpening(lo, hi, linked, secret, hidden, out Vector2 span)) return false;

        float along = (span.x + span.y) * 0.5f;
        float width = (span.y - span.x) + JambOverlap * 2f;
        float thick = WallThickness + JambOverlap;
        Vector3 c = new Vector3(from.cx, 0f, from.cz);
        if (dir == 1 || dir == 3)
        {
            pos = new Vector3(c.x + (dir == 1 ? from.width * 0.5f : -from.width * 0.5f), WallCenterY, c.z + along);
            scale = new Vector3(thick, WallHeight, width);
        }
        else
        {
            pos = new Vector3(c.x + along, WallCenterY, c.z + (dir == 0 ? from.depth * 0.5f : -from.depth * 0.5f));
            scale = new Vector3(width, WallHeight, thick);
        }
        return true;
    }

    static bool TryResolveOpening(float lo, float hi, bool linked, bool secret, bool wantHidden, out Vector2 span)
    {
        span = default;
        float mid = (lo + hi) * 0.5f;
        Vector2 door = default;
        bool hasDoor = linked && TryOpening(mid, lo, hi, DoorWidth, out door);
        if (!wantHidden)
        {
            if (!hasDoor) return false;
            span = door;
            return true;
        }
        if (!secret) return false;

        float secretAt = linked ? mid + 2.2f : mid;
        if (!TryOpening(secretAt, lo, hi, DoorWidth, out Vector2 hatch)) return false;
        if (linked && hasDoor && IntervalsOverlap(hatch, door))
        {
            float beside = door.y + 0.4f + DoorWidth * 0.5f;
            if (!TryOpening(beside, lo, hi, DoorWidth, out hatch) || IntervalsOverlap(hatch, door))
            {
                beside = door.x - 0.4f - DoorWidth * 0.5f;
                if (!TryOpening(beside, lo, hi, DoorWidth, out hatch) || IntervalsOverlap(hatch, door))
                    return false;
            }
        }
        span = hatch;
        return true;
    }

    static Vector2 InsetForWall(Vector2 span)
    {
        float a = span.x + JambOverlap;
        float b = span.y - JambOverlap;
        if (b - a < 0.85f) return span;
        return new Vector2(a, b);
    }

    static bool TryOpening(float desired, float lo, float hi, float preferWidth, out Vector2 span)
    {
        float width = Mathf.Min(preferWidth, hi - lo);
        if (width < 0.9f)
        {
            span = default;
            return false;
        }
        float half = width * 0.5f;
        float center = Mathf.Clamp(desired, lo + half, hi - half);
        span = new Vector2(center - half, center + half);
        return true;
    }

    static bool IntervalsOverlap(Vector2 a, Vector2 b)
    {
        return a.x < b.y - 0.05f && b.x < a.y - 0.05f;
    }

    bool SharesSide(HeistRoomPlan other, int dir, out float along0, out float along1)
    {
        return SharesSide(Plan, other, dir, out along0, out along1);
    }

    static bool SharesSide(HeistRoomPlan self, HeistRoomPlan other, int dir, out float along0, out float along1)
    {
        along0 = 0f;
        along1 = 0f;
        if (self == null || other == null) return false;
        const float gapMax = 0.2f;
        float gap;
        if (dir == 0 || dir == 2)
        {
            float ourEdge = self.cz + (dir == 0 ? self.depth * 0.5f : -self.depth * 0.5f);
            float theirEdge = other.cz + (dir == 0 ? -other.depth * 0.5f : other.depth * 0.5f);
            gap = Mathf.Abs(ourEdge - theirEdge);
            float lo = Mathf.Max(self.cx - self.width * 0.5f, other.cx - other.width * 0.5f);
            float hi = Mathf.Min(self.cx + self.width * 0.5f, other.cx + other.width * 0.5f);
            along0 = lo - self.cx;
            along1 = hi - self.cx;
        }
        else
        {
            float ourEdge = self.cx + (dir == 1 ? self.width * 0.5f : -self.width * 0.5f);
            float theirEdge = other.cx + (dir == 1 ? -other.width * 0.5f : other.width * 0.5f);
            gap = Mathf.Abs(ourEdge - theirEdge);
            float lo = Mathf.Max(self.cz - self.depth * 0.5f, other.cz - other.depth * 0.5f);
            float hi = Mathf.Min(self.cz + self.depth * 0.5f, other.cz + other.depth * 0.5f);
            along0 = lo - self.cz;
            along1 = hi - self.cz;
        }

        return gap < gapMax && along1 - along0 > 0.4f;
    }

    float SideLength(int dir) => dir == 0 || dir == 2 ? Plan.width : Plan.depth;

    static Vector3 Inward(int dir)
    {
        if (dir == 0) return Vector3.back;
        if (dir == 2) return Vector3.forward;
        if (dir == 1) return Vector3.left;
        return Vector3.right;
    }

    Vector3 VentPosition(int dir, float along)
    {
        const float grateDepth = 0.12f;
        float hw = Plan.width * 0.5f;
        float hd = Plan.depth * 0.5f;
        Vector3 edge;
        if (dir == 0) edge = Center + new Vector3(along, WallCenterY, hd);
        else if (dir == 2) edge = Center + new Vector3(along, WallCenterY, -hd);
        else if (dir == 1) edge = Center + new Vector3(hw, WallCenterY, along);
        else edge = Center + new Vector3(-hw, WallCenterY, along);

        Vector3 inward = Inward(dir);
        Vector3 innerFace = edge + inward * (WallThickness * 0.5f);
        return innerFace + inward * (grateDepth * 0.5f - 0.02f);
    }

    void BuildWall(Vector3 c, int dir, List<Vector2> openings, List<Vector2> omitted)
    {
        float hw = Plan.width * 0.5f;
        float hd = Plan.depth * 0.5f;
        if (dir == 0) SegmentWall(c + new Vector3(0f, WallCenterY, hd), Vector3.right, Plan.width, openings, omitted, "WallN");
        if (dir == 2) SegmentWall(c + new Vector3(0f, WallCenterY, -hd), Vector3.right, Plan.width, openings, omitted, "WallS");
        if (dir == 1) SegmentWall(c + new Vector3(hw, WallCenterY, 0f), Vector3.forward, Plan.depth, openings, omitted, "WallE");
        if (dir == 3) SegmentWall(c + new Vector3(-hw, WallCenterY, 0f), Vector3.forward, Plan.depth, openings, omitted, "WallW");
    }

    void SegmentWall(Vector3 mid, Vector3 axis, float length, List<Vector2> openings, List<Vector2> omitted, string name)
    {
        float start = -length * 0.5f;
        foreach (var span in SolidSpans(start, start + length, openings, omitted))
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
                if (b - a > 0.02f) gaps.Add(new Vector2(a, b));
            }
        }
        if (omitted != null)
        {
            foreach (var span in omitted)
            {
                float a = Mathf.Max(start, span.x);
                float b = Mathf.Min(end, span.y);
                if (b - a > 0.02f) gaps.Add(new Vector2(a, b));
            }
        }

        gaps.Sort((p, q) => p.x.CompareTo(q.x));
        var merged = new List<Vector2>();
        foreach (var gap in gaps)
        {
            if (merged.Count == 0 || gap.x > merged[merged.Count - 1].y)
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
            if (gap.x - cursor >= 0.05f) solid.Add(new Vector2(cursor, gap.x));
            cursor = Mathf.Max(cursor, gap.y);
        }
        if (end - cursor >= 0.05f) solid.Add(new Vector2(cursor, end));
        return solid;
    }

    void SpawnSeal(Vector3 c, int dir, float along, List<HeistHiddenSeal> seals)
    {
        Vector3 pos;
        Vector3 scale;
        if (dir == 1 || dir == 3)
        {
            pos = new Vector3(c.x + (dir == 1 ? Plan.width * 0.5f : -Plan.width * 0.5f), WallCenterY, c.z + along);
            scale = new Vector3(WallThickness + JambOverlap, WallHeight, DoorWidth + JambOverlap * 2f);
        }
        else
        {
            pos = new Vector3(c.x + along, WallCenterY, c.z + (dir == 0 ? Plan.depth * 0.5f : -Plan.depth * 0.5f));
            scale = new Vector3(DoorWidth + JambOverlap * 2f, WallHeight, WallThickness + JambOverlap);
        }
        var slab = HeistPrims.Cube(transform, pos, scale, WallColor, "HiddenSeal");
        seals.Add(slab.AddComponent<HeistHiddenSeal>());
    }
}
