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

    public static List<HeistRoomPlan> Generate(int difficulty, int seed)
    {
        var rng = new System.Random(seed);
        int roomCount = Mathf.Clamp(difficulty + 1, 2, 8);
        var rooms = new List<HeistRoomPlan>();

        for (int i = 0; i < roomCount; i++)
        {
            var room = new HeistRoomPlan { name = RoomNames[i] };
            int extra = 0;
            if (difficulty >= 3 && rng.NextDouble() < 0.4 + difficulty * 0.05) extra++;
            if (difficulty >= 6 && rng.NextDouble() < 0.35) extra++;
            int count = 1 + extra;
            bool lastRoom = i == roomCount - 1;

            for (int c = 0; c < count; c++)
            {
                bool vault = lastRoom && c == count - 1;
                bool bypass = !vault && c == 0 && !lastRoom && rng.NextDouble() < 0.28;
                room.challenges.Add(MakeChallenge(rng, difficulty, vault, bypass));
            }

            rooms.Add(room);
        }

        return rooms;
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
