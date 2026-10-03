using System.Collections.Generic;
using UnityEngine;

public static class HeistResolver
{
    public const float OrganizerCut = 0.25f;
    public const float ConsolationRate = 0.02f;
    public const int HeatPerFailure = 2;
    public const float CaptureBase = 0.12f;
    public const float CapturePerHeat = 0.07f;
    public const float KillChance = 0.22f;

    public static HeistResult Resolve(HeistLaunch launch, List<HeistRoomPlan> rooms)
    {
        var rng = new System.Random(launch.seed == 0 ? 1 : launch.seed + 91);
        var crew = launch.crew ?? new HeistCrewMember[0];
        int heat = 0;
        int heatCap = launch.difficulty + 4;
        int challengeCount = 0;
        int failedCount = 0;
        bool vaultFailed = false;

        foreach (var room in rooms)
        {
            bool skipNext = false;
            foreach (var challenge in room.challenges)
            {
                challengeCount++;
                if (skipNext)
                {
                    challenge.skipped = true;
                    challenge.passed = true;
                    challenge.narration = "Bypassed via the hidden path.";
                    skipNext = false;
                    continue;
                }

                ResolveChallenge(challenge, crew, rng);
                if (!challenge.passed)
                {
                    failedCount++;
                    heat += HeatPerFailure;
                    if (challenge.type == "vault") vaultFailed = true;
                }
                else if (challenge.isBypass)
                {
                    skipNext = true;
                    heat = Mathf.Max(0, heat - 1);
                }
            }
        }

        bool success = !vaultFailed && heat < heatCap;
        float failRatio = challengeCount == 0 ? 0f : failedCount / (float)challengeCount;
        int recovered = 0;
        if (success)
        {
            recovered = Mathf.RoundToInt(launch.targetValue * 0.76f * (1f - 0.12f * failRatio));
        }

        int consolation = Mathf.Max(1, Mathf.RoundToInt(launch.targetValue * ConsolationRate));
        int organizerShare = success
            ? Mathf.RoundToInt(recovered * OrganizerCut)
            : consolation;

        if (!success) recovered = organizerShare;

        var statuses = new Dictionary<string, string>();
        foreach (var member in crew)
        {
            string status = "ok";
            if (!success)
            {
                double captureChance = CaptureBase + heat * CapturePerHeat;
                if (rng.NextDouble() < captureChance) status = "captured";
                if (heat >= heatCap && rng.NextDouble() < KillChance) status = "killed";
            }
            statuses[member.id] = status;
        }

        int remainder = success ? Mathf.Max(0, recovered - organizerShare) : 0;
        var survivors = new List<HeistCrewMember>();
        foreach (var member in crew)
        {
            if (statuses[member.id] == "ok") survivors.Add(member);
        }

        int split = survivors.Count == 0 ? 0 : remainder / survivors.Count;
        var outcomes = new HeistCrewOutcome[crew.Length];
        for (int i = 0; i < crew.Length; i++)
        {
            var member = crew[i];
            int share = statuses[member.id] == "ok" ? split : 0;
            if (member.isOrganizer) share += organizerShare;
            outcomes[i] = new HeistCrewOutcome
            {
                id = member.id,
                name = member.name,
                isOrganizer = member.isOrganizer,
                status = statuses[member.id],
                share = share
            };
        }

        var roomResults = new HeistRoomResult[rooms.Count];
        for (int i = 0; i < rooms.Count; i++)
        {
            roomResults[i] = new HeistRoomResult
            {
                name = rooms[i].name,
                challenges = rooms[i].challenges.ToArray()
            };
        }

        return new HeistResult
        {
            success = success,
            heat = heat,
            targetValue = launch.targetValue,
            recoveredValue = recovered,
            organizerShare = organizerShare,
            crewOutcomes = outcomes,
            rooms = roomResults
        };
    }

    static void ResolveChallenge(HeistChallengeResult challenge, HeistCrewMember[] crew, System.Random rng)
    {
        if (crew.Length == 0)
        {
            challenge.passed = false;
            challenge.narration = "No crew was sent. The challenge stands unanswered.";
            return;
        }

        HeistCrewMember best = crew[0];
        int bestScore = -1;
        foreach (var member in crew)
        {
            int score = member.stats.Get(challenge.skill) + GearBonus(member.gear, challenge);
            if (score > bestScore)
            {
                bestScore = score;
                best = member;
            }
        }

        int bonus = GearBonus(best.gear, challenge);
        int roll = rng.Next(1, 7);
        int skillValue = best.stats.Get(challenge.skill);
        int total = skillValue + bonus + roll;
        challenge.actorId = best.id;
        challenge.actorName = best.name;
        challenge.roll = roll;
        challenge.bonus = bonus;
        challenge.passed = total >= challenge.threshold;

        string math = FormatCheck(skillValue, bonus, roll, total, challenge.threshold);
        if (challenge.passed)
        {
            challenge.narration = challenge.isBypass
                ? $"{best.name} spots a hidden route ({math})."
                : $"{best.name} clears {challenge.name} ({math}).";
        }
        else
        {
            challenge.narration = $"{best.name} fails {challenge.name} ({math}). Heat rises.";
        }
    }

    static string FormatCheck(int skill, int gear, int roll, int total, int threshold)
    {
        string gearBit = gear > 0 ? $" + {gear} gear" : "";
        return $"{skill}{gearBit} + roll {roll} = {total} vs {threshold}";
    }

    public static int GearBonus(string gear, HeistChallengeResult challenge)
    {
        if (string.IsNullOrEmpty(gear) || challenge == null) return 0;
        if (gear == "Lockpick Set" && (challenge.skill == "dex" || challenge.type == "vault")) return 1;
        if (gear == "Signal Jammer" && challenge.skill == "intel") return 1;
        if (gear == "Breaching Kit" && challenge.skill == "str") return 1;
        if (gear == "Disguise Kit" && challenge.skill == "cha") return 1;
        return 0;
    }
}
