using System.Collections.Generic;
using UnityEngine;

public class HeistRoom : MonoBehaviour
{
    public const float DoorWidth = 1.9f;
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
            int neighbor = NeighborOn(layout, dir);
            if (neighbor >= 0 && Index > neighbor) continue;

            var cuts = new List<float>();
            CollectAlongs(layout, Plan.links, dir, false, cuts);
            var hidden = new List<float>();
            CollectAlongs(layout, Plan.hiddenLinks, dir, true, hidden);
            cuts.AddRange(hidden);
            BuildWall(c, dir, cuts);
            foreach (float along in hidden)
                SpawnSeal(c, dir, along, seals);
        }
    }

    int NeighborOn(List<HeistRoomPlan> layout, int dir)
    {
        int found = -1;
        Scan(layout, Plan.links, dir, ref found);
        if (found < 0) Scan(layout, Plan.hiddenLinks, dir, ref found);
        return found;
    }

    void Scan(List<HeistRoomPlan> layout, List<int> ids, int dir, ref int found)
    {
        foreach (int other in ids)
        {
            if (other < 0 || other >= layout.Count) continue;
            if (HeistLevelGenerator.DirFrom(Plan, layout[other]) != dir) continue;
            found = other;
            return;
        }
    }

    void CollectAlongs(List<HeistRoomPlan> layout, List<int> ids, int dir, bool hidden, List<float> into)
    {
        foreach (int other in ids)
        {
            if (other < 0 || other >= layout.Count) continue;
            var o = layout[other];
            if (HeistLevelGenerator.DirFrom(Plan, o) != dir) continue;
            float along = HeistLevelGenerator.DoorAlong(Plan, o);
            if (hidden && Plan.links.Contains(other)) along += 2.2f;
            into.Add(along);
        }
    }

    void BuildWall(Vector3 c, int dir, List<float> doors)
    {
        float hw = Plan.width * 0.5f;
        float hd = Plan.depth * 0.5f;
        if (dir == 0) SegmentWall(c + new Vector3(0f, 1.2f, hd), Vector3.right, Plan.width, doors, "WallN");
        if (dir == 2) SegmentWall(c + new Vector3(0f, 1.2f, -hd), Vector3.right, Plan.width, doors, "WallS");
        if (dir == 1) SegmentWall(c + new Vector3(hw, 1.2f, 0f), Vector3.forward, Plan.depth, doors, "WallE");
        if (dir == 3) SegmentWall(c + new Vector3(-hw, 1.2f, 0f), Vector3.forward, Plan.depth, doors, "WallW");
    }

    void SegmentWall(Vector3 mid, Vector3 axis, float length, List<float> doorAlongs, string name)
    {
        var cuts = new List<float> { -length * 0.5f, length * 0.5f };
        foreach (float along in doorAlongs)
        {
            cuts.Add(along - DoorWidth * 0.5f);
            cuts.Add(along + DoorWidth * 0.5f);
        }
        cuts.Sort();
        for (int i = 0; i + 1 < cuts.Count; i += 2)
        {
            float a = cuts[i];
            float b = cuts[i + 1];
            float seg = b - a;
            if (seg < 0.2f) continue;
            Vector3 pos = mid + axis * ((a + b) * 0.5f);
            Vector3 scale = axis == Vector3.right
                ? new Vector3(seg, 2.4f, 0.25f)
                : new Vector3(0.25f, 2.4f, seg);
            HeistPrims.Cube(transform, pos, scale, WallColor, name);
        }
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
