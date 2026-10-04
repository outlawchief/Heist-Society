using System.Collections.Generic;
using UnityEngine;

public static class HeistLevelGenerator
{
    const float DoorNeed = 2.4f;

    static readonly string[] RoomNames =
    {
        "Service Alley", "Lobby", "Security Wing", "Archives",
        "Executive Floor", "Vault Approach", "Cash Room", "Inner Vault"
    };

    static readonly string[][] Catalog =
    {
        new[] { "Forced Door", "door", "str" },
        new[] { "Vent Crawl", "vent", "agi" },
        new[] { "Laser Grid", "door", "agi" },
        new[] { "Camera Grid", "cameras", "intel" },
        new[] { "Alarm Panel", "cameras", "intel" },
        new[] { "Guard Desk", "social", "cha" },
        new[] { "Bluff the Patrol", "social", "cha" },
        new[] { "Vault Lock", "vault", "dex" },
        new[] { "Hidden Passage", "bypass", "per" }
    };

    public static List<HeistRoomPlan> Generate(int difficulty, int seed)
    {
        var rng = new System.Random(seed);
        int roomCount = Mathf.Clamp(difficulty + 1, 2, 8);
        var rooms = new List<HeistRoomPlan>();

        for (int i = 0; i < roomCount; i++)
        {
            var room = new HeistRoomPlan
            {
                name = RoomNames[i],
                extract = i == 0,
                vault = i == roomCount - 1,
                hallway = i == 1 && roomCount >= 3
            };

            if (room.extract)
            {
                room.width = 12f;
                room.depth = 10f;
            }
            else if (room.hallway)
            {
                if (rng.Next(0, 2) == 0)
                {
                    room.width = 6f;
                    room.depth = 18f + rng.Next(0, 3) * 4f;
                }
                else
                {
                    room.depth = 6f;
                    room.width = 18f + rng.Next(0, 3) * 4f;
                }
                room.name = "Corridor";
            }
            else if (room.vault)
            {
                room.width = 10f + rng.Next(0, 3) * 2f;
                room.depth = 10f + rng.Next(0, 2) * 2f;
            }
            else
            {
                room.width = 10f + rng.Next(0, 5) * 2f;
                room.depth = 8f + rng.Next(0, 4) * 2f;
            }

            int extra = 0;
            if (difficulty >= 3 && rng.NextDouble() < 0.4 + difficulty * 0.05) extra++;
            if (difficulty >= 6 && rng.NextDouble() < 0.35) extra++;
            int count = 1 + extra;
            bool lastRoom = i == roomCount - 1;

            for (int c = 0; c < count; c++)
            {
                bool vault = lastRoom && c == count - 1;
                bool bypass = !vault && !lastRoom && rng.NextDouble() < 0.28;
                room.challenges.Add(MakeChallenge(rng, difficulty, vault, bypass));
            }

            rooms.Add(room);
        }

        PlaceAbutting(rooms, rng);
        AddTouchLoops(rooms, rng);
        AddHiddenPassages(rooms, rng);
        return rooms;
    }

    static void PlaceAbutting(List<HeistRoomPlan> rooms, System.Random rng)
    {
        rooms[0].cx = 0f;
        rooms[0].cz = 0f;

        for (int i = 1; i < rooms.Count; i++)
        {
            bool placed = false;
            for (int attempt = 0; attempt < 80 && !placed; attempt++)
            {
                int parent = PickParent(rooms, i, rng);
                int dir = rng.Next(0, 4);
                float t = (float)rng.NextDouble();
                if (!TryAttach(rooms, parent, i, dir, t)) continue;
                Link(rooms, parent, i, false);
                placed = true;
            }

            if (placed) continue;

            for (int parent = 0; parent < i && !placed; parent++)
            {
                for (int dir = 0; dir < 4 && !placed; dir++)
                {
                    if (!TryAttach(rooms, parent, i, dir, 0.5f)) continue;
                    Link(rooms, parent, i, false);
                    placed = true;
                }
            }
        }
    }

    static int PickParent(List<HeistRoomPlan> rooms, int count, System.Random rng)
    {
        int total = 0;
        for (int i = 0; i < count; i++)
            total += rooms[i].hallway ? 6 : 2;
        int pick = rng.Next(total);
        for (int i = 0; i < count; i++)
        {
            pick -= rooms[i].hallway ? 6 : 2;
            if (pick < 0) return i;
        }
        return count - 1;
    }

    static bool TryAttach(List<HeistRoomPlan> rooms, int parent, int child, int dir, float t)
    {
        var p = rooms[parent];
        var c = rooms[child];
        float cx = c.cx;
        float cz = c.cz;
        switch (dir)
        {
            case 1:
                cx = p.cx + (p.width + c.width) * 0.5f;
                if (!Align(p.cz, p.depth, c.depth, t, out cz)) return false;
                break;
            case 3:
                cx = p.cx - (p.width + c.width) * 0.5f;
                if (!Align(p.cz, p.depth, c.depth, t, out cz)) return false;
                break;
            case 0:
                cz = p.cz + (p.depth + c.depth) * 0.5f;
                if (!Align(p.cx, p.width, c.width, t, out cx)) return false;
                break;
            default:
                cz = p.cz - (p.depth + c.depth) * 0.5f;
                if (!Align(p.cx, p.width, c.width, t, out cx)) return false;
                break;
        }

        float oldX = c.cx;
        float oldZ = c.cz;
        c.cx = cx;
        c.cz = cz;
        for (int i = 0; i < child; i++)
        {
            if (i == parent) continue;
            if (Overlaps(c, rooms[i]))
            {
                c.cx = oldX;
                c.cz = oldZ;
                return false;
            }
        }
        return true;
    }

    static bool Align(float parentMid, float parentLen, float childLen, float t, out float childMid)
    {
        float p0 = parentMid - parentLen * 0.5f;
        float p1 = parentMid + parentLen * 0.5f;
        float half = childLen * 0.5f;
        float min = p0 - half + DoorNeed;
        float max = p1 + half - DoorNeed;
        if (min > max)
        {
            childMid = parentMid;
            return Overlap1D(p0, p1, childMid - half, childMid + half) >= DoorNeed * 0.6f;
        }
        childMid = min + Mathf.Clamp01(t) * (max - min);
        return true;
    }

    static void AddTouchLoops(List<HeistRoomPlan> rooms, System.Random rng)
    {
        for (int i = 0; i < rooms.Count; i++)
        {
            for (int j = i + 1; j < rooms.Count; j++)
            {
                if (rooms[i].links.Contains(j)) continue;
                if (!Touches(rooms[i], rooms[j])) continue;
                if (rng.NextDouble() < 0.45) Link(rooms, i, j, false);
            }
        }
    }

    static void AddHiddenPassages(List<HeistRoomPlan> rooms, System.Random rng)
    {
        for (int i = 0; i < rooms.Count; i++)
        {
            bool bypass = false;
            foreach (var challenge in rooms[i].challenges)
            {
                if (challenge.isBypass || challenge.type == "bypass") bypass = true;
            }
            if (!bypass || rooms[i].links.Count == 0) continue;
            int other = rooms[i].links[rng.Next(0, rooms[i].links.Count)];
            if (SharedOverlap(rooms[i], rooms[other]) < DoorNeed * 2f) continue;
            Link(rooms, i, other, true);
        }
    }

    static void Link(List<HeistRoomPlan> rooms, int a, int b, bool hidden)
    {
        if (a == b) return;
        var listA = hidden ? rooms[a].hiddenLinks : rooms[a].links;
        var listB = hidden ? rooms[b].hiddenLinks : rooms[b].links;
        if (!listA.Contains(b)) listA.Add(b);
        if (!listB.Contains(a)) listB.Add(a);
    }

    public static bool Overlaps(HeistRoomPlan a, HeistRoomPlan b)
    {
        float ox = (a.width + b.width) * 0.5f - Mathf.Abs(a.cx - b.cx);
        float oz = (a.depth + b.depth) * 0.5f - Mathf.Abs(a.cz - b.cz);
        return ox > 0.18f && oz > 0.18f;
    }

    public static bool Touches(HeistRoomPlan a, HeistRoomPlan b)
    {
        return SharedOverlap(a, b) >= DoorNeed && TouchGap(a, b) < 0.2f;
    }

    public static int DirFrom(HeistRoomPlan from, HeistRoomPlan to)
    {
        float best = float.MaxValue;
        int dir = -1;
        Consider(1, Mathf.Abs(X1(from) - X0(to)), Overlap1D(Z0(from), Z1(from), Z0(to), Z1(to)));
        Consider(3, Mathf.Abs(X0(from) - X1(to)), Overlap1D(Z0(from), Z1(from), Z0(to), Z1(to)));
        Consider(0, Mathf.Abs(Z1(from) - Z0(to)), Overlap1D(X0(from), X1(from), X0(to), X1(to)));
        Consider(2, Mathf.Abs(Z0(from) - Z1(to)), Overlap1D(X0(from), X1(from), X0(to), X1(to)));
        if (dir >= 0) return dir;

        float dx = to.cx - from.cx;
        float dz = to.cz - from.cz;
        if (Mathf.Abs(dx) >= Mathf.Abs(dz)) return dx > 0f ? 1 : 3;
        return dz > 0f ? 0 : 2;

        void Consider(int candidate, float gap, float overlap)
        {
            if (overlap < 0.35f) return;
            if (gap >= best) return;
            best = gap;
            dir = candidate;
        }
    }

    public static float DoorAlong(HeistRoomPlan from, HeistRoomPlan to)
    {
        int dir = DirFrom(from, to);
        if (dir == 1 || dir == 3)
            return OverlapMid(Z0(from), Z1(from), Z0(to), Z1(to)) - from.cz;
        return OverlapMid(X0(from), X1(from), X0(to), X1(to)) - from.cx;
    }

    public static float SharedOverlap(HeistRoomPlan a, HeistRoomPlan b)
    {
        int dir = DirFrom(a, b);
        if (dir == 1 || dir == 3)
            return Overlap1D(Z0(a), Z1(a), Z0(b), Z1(b));
        return Overlap1D(X0(a), X1(a), X0(b), X1(b));
    }

    static float TouchGap(HeistRoomPlan a, HeistRoomPlan b)
    {
        int dir = DirFrom(a, b);
        if (dir == 1) return Mathf.Abs(X1(a) - X0(b));
        if (dir == 3) return Mathf.Abs(X0(a) - X1(b));
        if (dir == 0) return Mathf.Abs(Z1(a) - Z0(b));
        return Mathf.Abs(Z0(a) - Z1(b));
    }

    static float Overlap1D(float a0, float a1, float b0, float b1)
    {
        return Mathf.Max(0f, Mathf.Min(a1, b1) - Mathf.Max(a0, b0));
    }

    static float OverlapMid(float a0, float a1, float b0, float b1)
    {
        float lo = Mathf.Max(a0, b0);
        float hi = Mathf.Min(a1, b1);
        return (lo + hi) * 0.5f;
    }

    static float X0(HeistRoomPlan r) => r.cx - r.width * 0.5f;
    static float X1(HeistRoomPlan r) => r.cx + r.width * 0.5f;
    static float Z0(HeistRoomPlan r) => r.cz - r.depth * 0.5f;
    static float Z1(HeistRoomPlan r) => r.cz + r.depth * 0.5f;

    static HeistChallengeResult MakeChallenge(System.Random rng, int difficulty, bool vault, bool bypass)
    {
        string[] pick;
        if (vault) pick = Catalog[7];
        else if (bypass) pick = Catalog[8];
        else pick = Catalog[rng.Next(0, 7)];

        int minT = Mathf.Clamp(3 + difficulty, 3, 10);
        int maxT = Mathf.Clamp(4 + difficulty, minT, 10);
        int threshold = rng.Next(minT, maxT + 1);

        return new HeistChallengeResult
        {
            name = pick[0],
            type = pick[1],
            skill = pick[2],
            threshold = threshold,
            isBypass = pick[1] == "bypass"
        };
    }
}
