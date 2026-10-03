using System.Collections.Generic;
using UnityEngine;

public static class HeistLevelGenerator
{
    static readonly string[] RoomNames =
    {
        "Service Alley", "Lobby", "Security Wing", "Archives",
        "Executive Floor", "Vault Approach", "Cash Room", "Inner Vault"
    };

    static readonly string[][] Catalog =
    {
        new[] { "Forced Door", "door", "str" },
        new[] { "Vent Crawl", "door", "agi" },
        new[] { "Laser Grid", "door", "agi" },
        new[] { "Camera Grid", "cameras", "intel" },
        new[] { "Alarm Panel", "cameras", "intel" },
        new[] { "Guard Desk", "social", "cha" },
        new[] { "Bluff the Patrol", "social", "cha" },
        new[] { "Vault Lock", "vault", "dex" },
        new[] { "Hidden Passage", "bypass", "per" }
    };

    static readonly int[] Dx = { 1, -1, 0, 0 };
    static readonly int[] Dz = { 0, 0, 1, -1 };

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
                width = 10f + rng.Next(0, 5) * 2f,
                depth = 8f + rng.Next(0, 4) * 2f,
                extract = i == 0,
                vault = i == roomCount - 1
            };
            if (room.extract)
            {
                room.width = 12f;
                room.depth = 10f;
            }
            if (room.vault)
            {
                room.width = 10f + rng.Next(0, 3) * 2f;
                room.depth = 10f + rng.Next(0, 2) * 2f;
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

        PlaceOnGrid(rooms, rng);
        AddLoops(rooms, rng);
        AddHiddenPassages(rooms, rng);
        return rooms;
    }

    static void PlaceOnGrid(List<HeistRoomPlan> rooms, System.Random rng)
    {
        var occupied = new HashSet<int> { Pack(0, 0) };
        rooms[0].gx = 0;
        rooms[0].gz = 0;

        for (int i = 1; i < rooms.Count; i++)
        {
            bool placed = false;
            for (int attempt = 0; attempt < 48 && !placed; attempt++)
            {
                int parent = rng.Next(0, i);
                int dir = rng.Next(0, 4);
                int nx = rooms[parent].gx + Dx[dir];
                int nz = rooms[parent].gz + Dz[dir];
                int key = Pack(nx, nz);
                if (occupied.Contains(key)) continue;
                rooms[i].gx = nx;
                rooms[i].gz = nz;
                occupied.Add(key);
                Link(rooms, parent, i, false);
                placed = true;
            }

            if (placed) continue;

            foreach (var cell in new List<int>(occupied))
            {
                Unpack(cell, out int x, out int z);
                for (int dir = 0; dir < 4 && !placed; dir++)
                {
                    int nx = x + Dx[dir];
                    int nz = z + Dz[dir];
                    int key = Pack(nx, nz);
                    if (occupied.Contains(key)) continue;
                    rooms[i].gx = nx;
                    rooms[i].gz = nz;
                    occupied.Add(key);
                    int parent = IndexAt(rooms, i, x, z);
                    if (parent >= 0) Link(rooms, parent, i, false);
                    placed = true;
                }
            }
        }
    }

    static void AddLoops(List<HeistRoomPlan> rooms, System.Random rng)
    {
        for (int i = 0; i < rooms.Count; i++)
        {
            for (int j = i + 1; j < rooms.Count; j++)
            {
                if (!Adjacent(rooms[i], rooms[j])) continue;
                if (rooms[i].links.Contains(j)) continue;
                if (rng.NextDouble() < 0.55) Link(rooms, i, j, false);
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

    static bool Adjacent(HeistRoomPlan a, HeistRoomPlan b)
    {
        return Mathf.Abs(a.gx - b.gx) + Mathf.Abs(a.gz - b.gz) == 1;
    }

    static int IndexAt(List<HeistRoomPlan> rooms, int limit, int x, int z)
    {
        for (int i = 0; i < limit; i++)
        {
            if (rooms[i].gx == x && rooms[i].gz == z) return i;
        }
        return -1;
    }

    static int Pack(int x, int z) => (x + 16) * 64 + (z + 16);

    static void Unpack(int key, out int x, out int z)
    {
        x = key / 64 - 16;
        z = key % 64 - 16;
    }

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
