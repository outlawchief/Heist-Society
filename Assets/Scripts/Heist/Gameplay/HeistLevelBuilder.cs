using System.Collections.Generic;
using UnityEngine;

public class HeistLevelBuilder : MonoBehaviour
{
    public const float CellPitch = 26f;
    public const float HallWidth = 2.2f;
    public const float DoorWidth = 1.9f;

    public Transform Root { get; private set; }
    public Vector3 ExtractPoint { get; private set; }
    public Vector3 VaultPoint { get; private set; }
    public readonly List<HeistRoomPlan> Layout = new List<HeistRoomPlan>();
    public readonly List<Vector3> RoomCenters = new List<Vector3>();
    public readonly List<HeistInteractable> Interactables = new List<HeistInteractable>();
    public readonly List<BoxCollider> HiddenDoors = new List<BoxCollider>();
    public readonly List<HeistSecurityCamera> SecurityCameras = new List<HeistSecurityCamera>();

    public void Build(List<HeistRoomPlan> rooms, int seed = 0)
    {
        Clear();
        Root = new GameObject("HeistWorld").transform;
        Layout.AddRange(rooms);
        SetupCamera(rooms.Count);
        SetupLight();

        for (int i = 0; i < rooms.Count; i++)
        {
            RoomCenters.Add(Center(rooms[i]));
            BuildRoom(rooms[i], i);
        }

        var built = new HashSet<int>();
        for (int i = 0; i < rooms.Count; i++)
        {
            foreach (int j in rooms[i].links)
            {
                int key = Pair(i, j);
                if (!built.Add(key)) continue;
                BuildConnector(rooms[i], rooms[j], false);
            }
            foreach (int j in rooms[i].hiddenLinks)
            {
                int key = Pair(i, j) ^ unchecked((int)0x10000);
                if (!built.Add(key)) continue;
                BuildConnector(rooms[i], rooms[j], true);
            }
        }

        var start = rooms[0];
        ExtractPoint = Center(start) + new Vector3(-start.width * 0.22f, 0f, -start.depth * 0.18f);
        var extract = HeistPrims.Cube(Root, ExtractPoint + Vector3.up * 0.04f, new Vector3(2.8f, 0.08f, 2.8f), new Color(0.35f, 0.7f, 0.4f), "Extract");
        var extractCol = extract.GetComponent<Collider>();
        if (extractCol != null) extractCol.isTrigger = true;
        extract.AddComponent<HeistExtractZone>();
        HeistPrims.Label(Root, ExtractPoint + Vector3.up * 1.6f, "EXTRACT");

        var vaultRoom = rooms[rooms.Count - 1];
        VaultPoint = Center(vaultRoom) + new Vector3(vaultRoom.width * 0.18f, 0.5f, 0f);
        PlaceSecurityCameras(seed);
    }

    public Vector3 Center(HeistRoomPlan room)
    {
        return new Vector3(room.gx * CellPitch, 0f, room.gz * CellPitch);
    }

    static int Pair(int a, int b)
    {
        int lo = Mathf.Min(a, b);
        int hi = Mathf.Max(a, b);
        return lo * 256 + hi;
    }

    void BuildRoom(HeistRoomPlan plan, int index)
    {
        Vector3 c = Center(plan);
        HeistPrims.Cube(Root, c + new Vector3(0f, -0.05f, 0f), new Vector3(plan.width, 0.1f, plan.depth), new Color(0.1f, 0.11f, 0.13f), "Floor_" + plan.name);
        HeistPrims.Label(Root, c + new Vector3(0f, 0.2f, plan.depth * 0.42f), plan.name.ToUpperInvariant(), 0.07f);

        bool openN = false, openE = false, openS = false, openW = false;
        CollectOpenings(plan, index, false, ref openN, ref openE, ref openS, ref openW);
        bool hidN = false, hidE = false, hidS = false, hidW = false;
        CollectOpenings(plan, index, true, ref hidN, ref hidE, ref hidS, ref hidW);

        var wall = new Color(0.16f, 0.08f, 0.09f);
        BuildWall(c, plan, 0, openN, hidN, wall);
        BuildWall(c, plan, 1, openE, hidE, wall);
        BuildWall(c, plan, 2, openS, hidS, wall);
        BuildWall(c, plan, 3, openW, hidW, wall);

        int slot = 0;
        foreach (var challenge in plan.challenges)
        {
            float z = (slot - (plan.challenges.Count - 1) * 0.5f) * 2.2f;
            Vector3 pos = c + new Vector3(plan.width * 0.18f, 0.7f, z);
            if (challenge.type == "vault" || plan.vault && challenge.type == "vault")
            {
                pos = c + new Vector3(plan.width * 0.22f, 0.7f, 0f);
                VaultPoint = pos;
            }
            if (challenge.type == "bypass")
                pos = c + new Vector3(plan.width * 0.32f, 0.7f, plan.depth * 0.32f);

            var block = HeistPrims.Cube(Root, pos, new Vector3(1.1f, 1.4f, 1.1f), ColorFor(challenge.type), challenge.name);
            var interact = block.AddComponent<HeistInteractable>();
            interact.Setup(challenge, plan.name);
            Interactables.Add(interact);
            HeistPrims.Label(Root, pos + Vector3.up * 1.15f, challenge.name, 0.045f);
            slot++;
        }
    }

    void CollectOpenings(HeistRoomPlan plan, int index, bool hidden, ref bool n, ref bool e, ref bool s, ref bool w)
    {
        var ids = hidden ? plan.hiddenLinks : plan.links;
        foreach (int other in ids)
        {
            if (other < 0 || other >= Layout.Count) continue;
            var o = Layout[other];
            if (o.gx > plan.gx) e = true;
            else if (o.gx < plan.gx) w = true;
            else if (o.gz > plan.gz) n = true;
            else if (o.gz < plan.gz) s = true;
        }
    }

    void BuildWall(Vector3 c, HeistRoomPlan plan, int dir, bool open, bool hidden, Color wall)
    {
        float hw = plan.width * 0.5f;
        float hd = plan.depth * 0.5f;
        float hiddenAlong = dir % 2 == 0 ? plan.width * 0.28f : plan.depth * 0.28f;
        var doors = new List<float>();
        if (open) doors.Add(0f);
        if (hidden) doors.Add(hiddenAlong);

        if (dir == 0) SegmentWall(c + new Vector3(0f, 1.2f, hd), Vector3.right, plan.width, doors, wall, "WallN");
        if (dir == 2) SegmentWall(c + new Vector3(0f, 1.2f, -hd), Vector3.right, plan.width, doors, wall, "WallS");
        if (dir == 1) SegmentWall(c + new Vector3(hw, 1.2f, 0f), Vector3.forward, plan.depth, doors, wall, "WallE");
        if (dir == 3) SegmentWall(c + new Vector3(-hw, 1.2f, 0f), Vector3.forward, plan.depth, doors, wall, "WallW");
    }

    void SegmentWall(Vector3 mid, Vector3 axis, float length, List<float> doorAlongs, Color wall, string name)
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
            HeistPrims.Cube(Root, pos, scale, wall, name);
        }
    }

    void BuildConnector(HeistRoomPlan a, HeistRoomPlan b, bool hidden)
    {
        Vector3 ca = Center(a);
        Vector3 cb = Center(b);
        bool eastWest = a.gx != b.gx;
        float offset = 0f;
        if (hidden)
            offset = eastWest ? Mathf.Min(a.depth, b.depth) * 0.28f : Mathf.Min(a.width, b.width) * 0.28f;

        if (eastWest)
        {
            float sign = Mathf.Sign(cb.x - ca.x);
            float x0 = ca.x + sign * a.width * 0.5f;
            float x1 = cb.x - sign * b.width * 0.5f;
            float z = (ca.z + cb.z) * 0.5f + offset;
            float mid = (x0 + x1) * 0.5f;
            float len = Mathf.Max(0.6f, Mathf.Abs(x1 - x0));
            HeistPrims.Cube(Root, new Vector3(mid, -0.05f, z), new Vector3(len, 0.1f, HallWidth), HallColor(hidden), hidden ? "HiddenHall" : "Hall");
            PlaceDoor(new Vector3(x0, 1.1f, z), new Vector3(0.35f, 2.2f, 1.8f), a.name, hidden);
        }
        else
        {
            float sign = Mathf.Sign(cb.z - ca.z);
            float z0 = ca.z + sign * a.depth * 0.5f;
            float z1 = cb.z - sign * b.depth * 0.5f;
            float x = (ca.x + cb.x) * 0.5f + offset;
            float mid = (z0 + z1) * 0.5f;
            float len = Mathf.Max(0.6f, Mathf.Abs(z1 - z0));
            HeistPrims.Cube(Root, new Vector3(x, -0.05f, mid), new Vector3(HallWidth, 0.1f, len), HallColor(hidden), hidden ? "HiddenHall" : "Hall");
            PlaceDoor(new Vector3(x, 1.1f, z0), new Vector3(1.8f, 2.2f, 0.35f), a.name, hidden);
        }
    }

    static Color HallColor(bool hidden)
    {
        return hidden ? new Color(0.12f, 0.11f, 0.08f) : new Color(0.08f, 0.08f, 0.09f);
    }

    void PlaceDoor(Vector3 pos, Vector3 scale, string roomName, bool hidden)
    {
        var door = HeistPrims.Cube(Root, pos, scale, hidden ? new Color(0.3f, 0.25f, 0.12f) : new Color(0.45f, 0.32f, 0.18f), hidden ? "HiddenDoor" : "Door");
        var interact = door.AddComponent<HeistInteractable>();
        interact.Setup(new HeistChallengeResult
        {
            name = hidden ? "Hidden Passage" : "Connecting Door",
            type = hidden ? "bypass" : "door",
            skill = hidden ? "per" : "str",
            threshold = hidden ? 6 : 5,
            isBypass = hidden
        }, roomName);
        interact.blocksPath = true;
        Interactables.Add(interact);
        if (hidden)
        {
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

    public Vector3[] PatrolRoute(int roomIndex, int variant)
    {
        if (Layout.Count == 0) return new[] { Vector3.zero };
        roomIndex = Mathf.Clamp(roomIndex, 0, Layout.Count - 1);
        var room = Layout[roomIndex];
        Vector3 c = Center(room);
        float insetX = room.width * (0.34f - (variant % 3) * 0.05f);
        float insetZ = room.depth * (0.34f - (variant % 2) * 0.06f);
        bool flip = variant % 2 == 1;
        var loop = new[]
        {
            c + new Vector3(-insetX, 0f, -insetZ),
            c + new Vector3(insetX, 0f, -insetZ),
            c + new Vector3(insetX, 0f, insetZ),
            c + new Vector3(-insetX, 0f, insetZ)
        };
        if (flip) System.Array.Reverse(loop);
        return loop;
    }

    public void PlaceSecurityCameras(int seed)
    {
        if (Layout.Count == 0) return;
        var rng = new System.Random(seed ^ 0x5EC4);
        int count = 2 + Layout.Count;
        for (int i = 0; i < count; i++)
        {
            var room = Layout[rng.Next(0, Layout.Count)];
            Vector3 c = Center(room);
            float along = (float)(rng.NextDouble() * 0.7 - 0.35);
            int wall = rng.Next(0, 4);
            float hw = room.width * 0.48f;
            float hd = room.depth * 0.48f;
            Vector3 pos;
            Vector3 look;
            switch (wall)
            {
                case 0:
                    pos = c + new Vector3(along * room.width, 2.15f, hd);
                    look = Vector3.back;
                    break;
                case 1:
                    pos = c + new Vector3(along * room.width, 2.15f, -hd);
                    look = Vector3.forward;
                    break;
                case 2:
                    pos = c + new Vector3(-hw, 2.15f, along * room.depth);
                    look = Vector3.right;
                    break;
                default:
                    pos = c + new Vector3(hw, 2.15f, along * room.depth);
                    look = Vector3.left;
                    break;
            }

            var root = new GameObject("SecurityCamera");
            root.transform.SetParent(Root, false);
            root.transform.position = pos;
            root.transform.rotation = Quaternion.LookRotation(look, Vector3.up);
            var body = HeistPrims.Cube(root.transform, pos, new Vector3(0.28f, 0.2f, 0.36f), new Color(0.12f, 0.14f, 0.18f), "CamBody");
            body.transform.localPosition = Vector3.zero;
            var lens = HeistPrims.Cube(root.transform, pos, new Vector3(0.14f, 0.12f, 0.14f), new Color(0.45f, 0.12f, 0.12f), "CamLens");
            lens.transform.localPosition = new Vector3(0f, 0f, 0.18f);
            HeistPrims.Label(root.transform, pos + Vector3.up * 0.35f, "CAM", 0.04f);
            var cam = root.AddComponent<HeistSecurityCamera>();
            SecurityCameras.Add(cam);
        }
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
        SecurityCameras.Clear();
        Layout.Clear();
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
