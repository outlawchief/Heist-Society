using System.Collections.Generic;
using UnityEngine;

public class HeistRoom : MonoBehaviour
{
    public const float Module = HeistShellCatalog.Module;
    public const float DoorWidth = HeistShellCatalog.Module;
    public const float WallThickness = HeistShellCatalog.WallThickness;
    public const float WallHeight = HeistShellCatalog.WallHeight;
    public const float WallCenterY = HeistShellCatalog.WallHeight * 0.5f;
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
        TileFloors();
        HeistPrims.Label(transform, c + new Vector3(0f, 0.2f, Plan.depth * 0.42f), Plan.name.ToUpperInvariant(), 0.07f);

        for (int dir = 0; dir < 4; dir++)
        {
            var openings = new List<Vector2>();
            var omitted = new List<Vector2>();
            var hidden = new List<float>();
            CollectSide(layout, dir, openings, omitted, hidden);
            BuildWall(dir, openings, omitted, hidden, seals);
        }
    }

    void TileFloors()
    {
        var catalog = HeistShellCatalog.Load();
        float minX = Plan.cx - Plan.width * 0.5f;
        float minZ = Plan.cz - Plan.depth * 0.5f;
        if (catalog == null || !catalog.HasFloor)
        {
            HeistPrims.Cube(transform, Center + new Vector3(0f, -0.05f, 0f), new Vector3(Plan.width, 0.1f, Plan.depth), new Color(0.1f, 0.11f, 0.13f), "Floor");
            return;
        }

        int nx = Mathf.Max(1, Mathf.RoundToInt(Plan.width / Module));
        int nz = Mathf.Max(1, Mathf.RoundToInt(Plan.depth / Module));
        for (int ix = 0; ix < nx; ix++)
        {
            for (int iz = 0; iz < nz; iz++)
            {
                float x0 = minX + ix * Module;
                float z0 = minZ + iz * Module;
                var tile = Instantiate(catalog.floor, new Vector3(x0 + Module, 0f, z0), Quaternion.identity, transform);
                tile.name = "Floor";
            }
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
                openings.Add(door);
            if (!secret) continue;
            if (!TryResolveOpening(lo, hi, linked, true, true, out Vector2 hatch)) continue;
            openings.Add(hatch);
            hidden.Add((hatch.x + hatch.y) * 0.5f);
        }
    }

    public static bool TryConnectorPose(
        HeistRoomPlan from,
        HeistRoomPlan to,
        List<HeistRoomPlan> layout,
        bool hidden,
        out Vector3 leafPos,
        out Quaternion rotation)
    {
        leafPos = default;
        rotation = Quaternion.identity;
        if (from == null || to == null || layout == null) return false;
        int toIndex = layout.IndexOf(to);
        if (toIndex < 0) return false;

        int dir = HeistLevelGenerator.DirFrom(from, to);
        if (!SharesSide(from, to, dir, out float lo, out float hi)) return false;

        bool linked = from.links != null && from.links.Contains(toIndex);
        bool secret = from.hiddenLinks != null && from.hiddenLinks.Contains(toIndex);
        if (!TryResolveOpening(lo, hi, linked, secret, hidden, out Vector2 span)) return false;

        WallModulePose(from, dir, span.x, out Vector3 pivot, out rotation);
        leafPos = pivot + rotation * new Vector3(HeistShellCatalog.DoorLeafLocalX, 0f, 0f);
        return true;
    }

    static bool TryResolveOpening(float lo, float hi, bool linked, bool secret, bool wantHidden, out Vector2 span)
    {
        span = default;
        var cells = CellsIn(lo, hi);
        if (cells.Count == 0) return false;
        float mid = (lo + hi) * 0.5f;
        int doorIdx = ClosestCell(cells, mid);
        if (!wantHidden)
        {
            if (!linked) return false;
            span = cells[doorIdx];
            return true;
        }
        if (!secret) return false;
        if (!linked)
        {
            span = cells[doorIdx];
            return true;
        }

        int hiddenIdx = ClosestCell(cells, mid + Module);
        if (hiddenIdx == doorIdx)
        {
            if (doorIdx + 1 < cells.Count) hiddenIdx = doorIdx + 1;
            else if (doorIdx > 0) hiddenIdx = doorIdx - 1;
            else return false;
        }
        span = cells[hiddenIdx];
        return true;
    }

    static List<Vector2> CellsIn(float lo, float hi)
    {
        var cells = new List<Vector2>();
        float x = lo;
        while (x + Module <= hi + 0.05f)
        {
            cells.Add(new Vector2(x, x + Module));
            x += Module;
        }
        return cells;
    }

    static int ClosestCell(List<Vector2> cells, float desired)
    {
        int best = 0;
        float bestDist = float.MaxValue;
        for (int i = 0; i < cells.Count; i++)
        {
            float mid = (cells[i].x + cells[i].y) * 0.5f;
            float dist = Mathf.Abs(mid - desired);
            if (dist >= bestDist) continue;
            bestDist = dist;
            best = i;
        }
        return best;
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

    bool SharesSide(HeistRoomPlan other, int dir, out float along0, out float along1)
    {
        return SharesSide(Plan, other, dir, out along0, out along1);
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

    void BuildWall(int dir, List<Vector2> openings, List<Vector2> omitted, List<float> hidden, List<HeistHiddenSeal> seals)
    {
        float half = SideLength(dir) * 0.5f;
        int cells = Mathf.Max(1, Mathf.RoundToInt(SideLength(dir) / Module));
        for (int i = 0; i < cells; i++)
        {
            float start = -half + i * Module;
            float end = start + Module;
            float mid = (start + end) * 0.5f;
            if (CellCovered(omitted, start, end)) continue;
            bool isDoor = CellCovered(openings, start, end);
            bool isHidden = IsHiddenCell(hidden, mid);
            if (isDoor)
            {
                var doorWall = PlaceWallModule(dir, start, true, isHidden ? "HiddenDoorWall" : "DoorWall");
                if (isHidden && doorWall != null) doorWall.SetActive(false);
                if (isHidden) SpawnSeal(dir, start, seals);
                continue;
            }

            PlaceWallModule(dir, start, false, WallName(dir));
        }
    }

    static bool IsHiddenCell(List<float> hidden, float mid)
    {
        if (hidden == null) return false;
        foreach (float along in hidden)
        {
            if (Mathf.Abs(along - mid) < 0.2f) return true;
        }
        return false;
    }

    static bool CellCovered(List<Vector2> spans, float start, float end)
    {
        if (spans == null) return false;
        foreach (var span in spans)
        {
            float a = Mathf.Max(start, span.x);
            float b = Mathf.Min(end, span.y);
            if (b - a > Module * 0.5f) return true;
        }
        return false;
    }

    static string WallName(int dir)
    {
        if (dir == 0) return "WallN";
        if (dir == 2) return "WallS";
        if (dir == 1) return "WallE";
        return "WallW";
    }

    GameObject PlaceWallModule(int dir, float cellStart, bool doorway, string name)
    {
        WallModulePose(Plan, dir, cellStart, out Vector3 pivot, out Quaternion rot);
        var catalog = HeistShellCatalog.Load();
        GameObject prefab = null;
        if (catalog != null) prefab = doorway ? catalog.doorWall : catalog.wall;
        if (prefab != null)
        {
            var go = Instantiate(prefab, pivot, rot, transform);
            go.name = name;
            if (doorway)
                PlaceBackface(prefab, go, pivot, rot, Module);
            return go;
        }

        Vector3 alongAxis = dir == 0 || dir == 2 ? Vector3.right : Vector3.forward;
        Vector3 mid = WallLineOrigin(Plan, dir) + alongAxis * (cellStart + Module * 0.5f) + Vector3.up * WallCenterY;
        Vector3 scale = dir == 0 || dir == 2
            ? new Vector3(Module, WallHeight, WallThickness)
            : new Vector3(WallThickness, WallHeight, Module);
        return HeistPrims.Cube(transform, mid, scale, WallColor, name);
    }

    static void PlaceBackface(GameObject prefab, GameObject front, Vector3 pivot, Quaternion rot, float along)
    {
        Quaternion backRot = rot * Quaternion.Euler(0f, 180f, 0f);
        Vector3 backPivot = pivot + rot * Vector3.left * along;
        var back = Object.Instantiate(prefab, backPivot, backRot, front.transform);
        back.name = front.name + "_Back";
        foreach (var col in back.GetComponentsInChildren<Collider>(true))
            col.enabled = false;
    }

    void SpawnSeal(int dir, float cellStart, List<HeistHiddenSeal> seals)
    {
        var slab = PlaceWallModule(dir, cellStart, false, "HiddenSeal");
        if (slab == null) return;
        seals.Add(slab.AddComponent<HeistHiddenSeal>());
    }

    public static float VaultCellStart(HeistRoomPlan plan)
    {
        float half = plan.depth * 0.5f;
        int cells = Mathf.Max(1, Mathf.RoundToInt(plan.depth / Module));
        return -half + (cells / 2) * Module;
    }

    public static void WallModulePose(HeistRoomPlan plan, int dir, float cellStart, out Vector3 pivot, out Quaternion rotation)
    {
        rotation = Quaternion.LookRotation(Inward(dir), Vector3.up);
        Vector3 origin = WallLineOrigin(plan, dir);
        Vector3 alongAxis = dir == 0 || dir == 2 ? Vector3.right : Vector3.forward;
        Vector3 p0 = origin + alongAxis * cellStart;
        Vector3 p1 = origin + alongAxis * (cellStart + Module);
        Vector3 localX = rotation * Vector3.right;
        pivot = Vector3.Dot(p1 - p0, localX) > 0f ? p1 : p0;
    }

    static Vector3 WallLineOrigin(HeistRoomPlan plan, int dir)
    {
        float hw = plan.width * 0.5f;
        float hd = plan.depth * 0.5f;
        if (dir == 0) return new Vector3(plan.cx, 0f, plan.cz + hd);
        if (dir == 2) return new Vector3(plan.cx, 0f, plan.cz - hd);
        if (dir == 1) return new Vector3(plan.cx + hw, 0f, plan.cz);
        return new Vector3(plan.cx - hw, 0f, plan.cz);
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
}
