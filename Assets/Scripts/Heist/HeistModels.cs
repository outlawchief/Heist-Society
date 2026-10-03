using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class HeistStats
{
    public int str = 1;
    public int agi = 1;
    public int intel = 1;
    public int dex = 1;
    public int cha = 1;
    public int per = 1;

    public int Get(string skill)
    {
        switch (skill)
        {
            case "str": return str;
            case "agi": return agi;
            case "intel": return intel;
            case "dex": return dex;
            case "cha": return cha;
            case "per": return per;
            default: return 1;
        }
    }
}

[Serializable]
public class HeistCrewMember
{
    public string id;
    public string name;
    public string className;
    public bool isOrganizer;
    public string gear;
    public HeistStats stats = new HeistStats();
}

[Serializable]
public class HeistLaunch
{
    public int difficulty = 1;
    public int targetValue;
    public int seed;
    public string organizerId;
    public HeistCrewMember[] crew;
}

[Serializable]
public class HeistChallengeResult
{
    public string name;
    public string type;
    public string skill;
    public int threshold;
    public bool isBypass;
    public bool skipped;
    public bool passed;
    public string actorId;
    public string actorName;
    public int roll;
    public int bonus;
    public string narration;
}

[Serializable]
public class HeistRoomResult
{
    public string name;
    public HeistChallengeResult[] challenges;
}

[Serializable]
public class HeistCrewOutcome
{
    public string id;
    public string name;
    public bool isOrganizer;
    public string status;
    public int share;
}

[Serializable]
public class HeistResult
{
    public bool success;
    public int heat;
    public int targetValue;
    public int recoveredValue;
    public int organizerShare;
    public HeistCrewOutcome[] crewOutcomes;
    public HeistRoomResult[] rooms;
}

public class HeistRoomPlan
{
    public string name;
    public List<HeistChallengeResult> challenges = new List<HeistChallengeResult>();
}

public static class HeistJson
{
    public static string NormalizeInbound(string json)
    {
        if (string.IsNullOrEmpty(json)) return json;
        return json.Replace("\"int\":", "\"intel\":");
    }
}
