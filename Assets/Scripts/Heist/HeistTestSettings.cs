using UnityEngine;

[CreateAssetMenu(fileName = "HeistTestSettings", menuName = "Heist Society/Test Settings", order = 0)]
public class HeistTestSettings : ScriptableObject
{
    [Header("Heist")]
    public int seed = 42;
    public int targetValue = 500000;
    [Range(1, 7)] public int difficulty = 3;

    [Header("Spawns")]
    [Range(0f, 4f)] public float guardSpawnRate = 1f;
    [Range(0f, 4f)] public float cameraSpawnRate = 1f;

    [Header("Main character")]
    public string codeName = "The Locksmith";
    public string gear = "Lockpick Set";
    [Range(1, 10)] public int level = 1;
    public HeistStats skills = new HeistStats
    {
        str = 4,
        agi = 3,
        intel = 3,
        dex = 5,
        cha = 2,
        per = 3
    };

    public HeistLaunch BuildLaunch()
    {
        var stats = skills ?? new HeistStats();
        return new HeistLaunch
        {
            difficulty = Mathf.Clamp(difficulty, 1, 7),
            targetValue = Mathf.Max(0, targetValue),
            seed = seed,
            organizerId = "organizer",
            origin = "http://127.0.0.1:8765",
            testTuning = true,
            guardSpawnRate = Mathf.Max(0f, guardSpawnRate),
            cameraSpawnRate = Mathf.Max(0f, cameraSpawnRate),
            crew = new[]
            {
                new HeistCrewMember
                {
                    id = "organizer",
                    name = string.IsNullOrWhiteSpace(codeName) ? "Operative" : codeName,
                    className = "Tester",
                    level = Mathf.Max(1, level),
                    isOrganizer = true,
                    gear = gear,
                    stats = new HeistStats
                    {
                        str = Mathf.Clamp(stats.str, 1, 10),
                        agi = Mathf.Clamp(stats.agi, 1, 10),
                        intel = Mathf.Clamp(stats.intel, 1, 10),
                        dex = Mathf.Clamp(stats.dex, 1, 10),
                        cha = Mathf.Clamp(stats.cha, 1, 10),
                        per = Mathf.Clamp(stats.per, 1, 10)
                    }
                },
                new HeistCrewMember
                {
                    id = "ghost",
                    name = "Ghost",
                    className = "Stealth",
                    level = 1,
                    gear = "Disguise Kit",
                    stats = new HeistStats { str = 2, agi = 8, intel = 4, dex = 5, cha = 3, per = 7 }
                }
            }
        };
    }
}
