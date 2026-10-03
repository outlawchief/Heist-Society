using System.Collections.Generic;
using UnityEngine;

public class HeistLevelBuilder : MonoBehaviour
{
    public const float RoomWidth = 14f;
    public const float RoomDepth = 12f;
    public const float RoomSpacing = 18f;

    public Transform Root { get; private set; }
    public Vector3 ExtractPoint { get; private set; }
    public Vector3 VaultPoint { get; private set; }
    public readonly List<Vector3> RoomCenters = new List<Vector3>();
    public readonly List<HeistInteractable> Interactables = new List<HeistInteractable>();
    public readonly List<BoxCollider> HiddenDoors = new List<BoxCollider>();

    public void Build(List<HeistRoomPlan> rooms)
    {
        Clear();
        Root = new GameObject("HeistWorld").transform;
        SetupCamera(rooms.Count);
        SetupLight();

        for (int i = 0; i < rooms.Count; i++)
        {
            float x = i * RoomSpacing;
            RoomCenters.Add(new Vector3(x, 0f, 0f));
            BuildRoom(rooms[i], x, i, i == rooms.Count - 1);
            if (i < rooms.Count - 1)
            {
                bool hidden = HasBypass(rooms[i]);
                BuildCorridor(x, hidden);
            }
        }

        ExtractPoint = new Vector3(-RoomWidth * 0.35f, 0.5f, 0f);
        var extract = HeistPrims.Cube(Root, ExtractPoint + Vector3.up * 0.05f, new Vector3(2.4f, 0.1f, 2.4f), new Color(0.35f, 0.7f, 0.4f), "Extract");
        var extractZone = extract.AddComponent<HeistExtractZone>();
        HeistPrims.Label(Root, ExtractPoint + Vector3.up * 1.6f, "EXTRACT");
        VaultPoint = RoomCenters[RoomCenters.Count - 1] + new Vector3(3.2f, 0.5f, 0f);
    }

    bool HasBypass(HeistRoomPlan room)
    {
        foreach (var challenge in room.challenges)
        {
            if (challenge.isBypass || challenge.type == "bypass") return true;
        }
        return false;
    }

    void BuildRoom(HeistRoomPlan plan, float x, int index, bool last)
    {
        HeistPrims.Cube(Root, new Vector3(x, -0.05f, 0f), new Vector3(RoomWidth, 0.1f, RoomDepth), new Color(0.1f, 0.11f, 0.13f), "Floor_" + plan.name);
        HeistPrims.Label(Root, new Vector3(x, 0.2f, RoomDepth * 0.42f), plan.name.ToUpperInvariant(), 0.07f);

        float hw = RoomWidth * 0.5f;
        float hd = RoomDepth * 0.5f;
        var wall = new Color(0.16f, 0.08f, 0.09f);
        HeistPrims.Cube(Root, new Vector3(x, 1.2f, hd), new Vector3(RoomWidth, 2.4f, 0.25f), wall, "WallN");
        HeistPrims.Cube(Root, new Vector3(x, 1.2f, -hd), new Vector3(RoomWidth, 2.4f, 0.25f), wall, "WallS");
        HeistPrims.Cube(Root, new Vector3(x - hw, 1.2f, hd * 0.55f), new Vector3(0.25f, 2.4f, RoomDepth * 0.45f), wall, "WallW1");
        HeistPrims.Cube(Root, new Vector3(x - hw, 1.2f, -hd * 0.55f), new Vector3(0.25f, 2.4f, RoomDepth * 0.45f), wall, "WallW2");
        HeistPrims.Cube(Root, new Vector3(x + hw, 1.2f, hd * 0.55f), new Vector3(0.25f, 2.4f, RoomDepth * 0.45f), wall, "WallE1");
        HeistPrims.Cube(Root, new Vector3(x + hw, 1.2f, -hd * 0.55f), new Vector3(0.25f, 2.4f, RoomDepth * 0.45f), wall, "WallE2");

        int slot = 0;
        foreach (var challenge in plan.challenges)
        {
            float z = (slot - (plan.challenges.Count - 1) * 0.5f) * 2.4f;
            Vector3 pos = new Vector3(x + 2.2f, 0.7f, z);
            if (challenge.type == "vault" || last && challenge.type == "vault")
            {
                pos = new Vector3(x + 3.2f, 0.7f, 0f);
                VaultPoint = pos;
            }
            if (challenge.type == "bypass")
            {
                pos = new Vector3(x + hw - 0.6f, 0.7f, hd - 1.2f);
            }

            var block = HeistPrims.Cube(Root, pos, new Vector3(1.1f, 1.4f, 1.1f), ColorFor(challenge.type), challenge.name);
            var interact = block.AddComponent<HeistInteractable>();
            interact.Setup(challenge, plan.name);
            Interactables.Add(interact);
            HeistPrims.Label(Root, pos + Vector3.up * 1.15f, challenge.name, 0.045f);
            slot++;
        }

        if (!last)
        {
            var door = HeistPrims.Cube(Root, new Vector3(x + hw, 1.1f, 0f), new Vector3(0.35f, 2.2f, 1.8f), new Color(0.45f, 0.32f, 0.18f), "Door");
            var interact = door.AddComponent<HeistInteractable>();
            interact.Setup(new HeistChallengeResult
            {
                name = "Connecting Door",
                type = "door",
                skill = "str",
                threshold = 5,
                isBypass = false
            }, plan.name);
            interact.blocksPath = true;
            Interactables.Add(interact);
        }
    }

    void BuildCorridor(float roomX, bool hidden)
    {
        float start = roomX + RoomWidth * 0.5f;
        float end = roomX + RoomSpacing - RoomWidth * 0.5f;
        float mid = (start + end) * 0.5f;
        HeistPrims.Cube(Root, new Vector3(mid, -0.05f, 0f), new Vector3(end - start, 0.1f, 2.2f), new Color(0.08f, 0.08f, 0.09f), "Hall");
        if (hidden)
        {
            HeistPrims.Cube(Root, new Vector3(mid, -0.05f, 5.5f), new Vector3(end - start + 2f, 0.1f, 2f), new Color(0.12f, 0.11f, 0.08f), "HiddenHall");
            var door = HeistPrims.Cube(Root, new Vector3(start + 0.4f, 1f, 5.5f), new Vector3(0.4f, 2f, 1.6f), new Color(0.3f, 0.25f, 0.12f), "HiddenDoor");
            var interact = door.AddComponent<HeistInteractable>();
            interact.Setup(new HeistChallengeResult
            {
                name = "Hidden Passage",
                type = "bypass",
                skill = "per",
                threshold = 6,
                isBypass = true
            }, "Hidden");
            interact.blocksPath = true;
            Interactables.Add(interact);
            HiddenDoors.Add(door.GetComponent<BoxCollider>());
            door.SetActive(false);
        }
    }

    static Color ColorFor(string type)
    {
        switch (type)
        {
            case "cameras": return new Color(0.2f, 0.35f, 0.55f);
            case "vault": return new Color(0.72f, 0.62f, 0.28f);
            case "social": return new Color(0.45f, 0.28f, 0.5f);
            case "bypass": return new Color(0.55f, 0.5f, 0.3f);
            case "door": return new Color(0.4f, 0.25f, 0.15f);
            default: return new Color(0.28f, 0.2f, 0.16f);
        }
    }

    void SetupCamera(int roomCount)
    {
        var cam = Camera.main;
        if (cam == null)
        {
            var camGo = new GameObject("HeistCamera");
            cam = camGo.AddComponent<Camera>();
            cam.tag = "MainCamera";
        }
        cam.orthographic = true;
        cam.orthographicSize = 8f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.03f, 0.035f, 0.04f);
        cam.transform.position = new Vector3(4f, 18f, -12f);
        cam.transform.rotation = Quaternion.Euler(50f, 0f, 0f);
        if (cam.GetComponent<HeistCameraFollow>() == null)
            cam.gameObject.AddComponent<HeistCameraFollow>();
    }

    void SetupLight()
    {
        var light = FindFirstObjectByType<Light>();
        if (light == null)
        {
            var lightGo = new GameObject("HeistLight");
            light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
        }
        light.color = new Color(1f, 0.92f, 0.8f);
        light.intensity = 1.15f;
        light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
    }

    public void RevealHidden()
    {
        foreach (var interactable in Interactables)
        {
            if (interactable.challenge != null && interactable.challenge.type == "bypass")
                interactable.gameObject.SetActive(true);
        }
        foreach (var door in HiddenDoors)
        {
            if (door != null) door.gameObject.SetActive(true);
        }
    }

    public void Clear()
    {
        if (Root != null) Destroy(Root.gameObject);
        Interactables.Clear();
        RoomCenters.Clear();
        HiddenDoors.Clear();
    }
}

public class HeistExtractZone : MonoBehaviour { }

public class HeistCameraFollow : MonoBehaviour
{
    public Transform target;

    void LateUpdate()
    {
        if (target == null) return;
        Vector3 desired = target.position + new Vector3(0f, 18f, -12f);
        transform.position = Vector3.Lerp(transform.position, desired, Time.deltaTime * 6f);
    }
}
