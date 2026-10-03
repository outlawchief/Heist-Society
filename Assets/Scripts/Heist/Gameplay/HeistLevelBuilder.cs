using System.Collections.Generic;
using UnityEngine;

public class HeistLevelBuilder : MonoBehaviour
{
    public const float DoorWidth = 1.9f;

    public Transform Root { get; private set; }
    public Vector3 ExtractPoint { get; private set; }
    public Vector3 VaultPoint { get; private set; }
    public readonly List<HeistRoomPlan> Layout = new List<HeistRoomPlan>();
    public readonly List<Vector3> RoomCenters = new List<Vector3>();
    public readonly List<HeistInteractable> Interactables = new List<HeistInteractable>();
    public readonly List<BoxCollider> HiddenDoors = new List<BoxCollider>();
    public readonly List<HeistHiddenSeal> HiddenSeals = new List<HeistHiddenSeal>();
    public readonly List<HeistSecurityCamera> SecurityCameras = new List<HeistSecurityCamera>();
    public readonly List<HeistVent> Vents = new List<HeistVent>();
    public readonly List<HeistRoom> Rooms = new List<HeistRoom>();

    public void Build(List<HeistRoomPlan> rooms, int seed = 0, float cameraSpawnRate = 1f)
    {
        Clear();
        Root = new GameObject("HeistWorld").transform;
        Layout.AddRange(rooms);
        SetupCamera(rooms.Count);
        SetupLight();

        for (int i = 0; i < rooms.Count; i++)
        {
            RoomCenters.Add(Center(rooms[i]));
            var room = HeistRoom.Spawn(Root, rooms[i], i, Layout, HiddenSeals);
            Rooms.Add(room);
            PlaceProps(rooms[i], room.Center);
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
        PlaceSecurityCameras(seed, cameraSpawnRate);
        PlaceVents(seed);
    }

    public Vector3 Center(HeistRoomPlan room)
    {
        return new Vector3(room.cx, 0f, room.cz);
    }

    static int Pair(int a, int b)
    {
        int lo = Mathf.Min(a, b);
        int hi = Mathf.Max(a, b);
        return lo * 256 + hi;
    }

    void PlaceProps(HeistRoomPlan plan, Vector3 c)
    {
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
            if (challenge.type == "vent")
                continue;

            var block = HeistPrims.Cube(Root, pos, new Vector3(1.1f, 1.4f, 1.1f), ColorFor(challenge.type), challenge.name);
            var interact = block.AddComponent<HeistInteractable>();
            interact.Setup(challenge, plan.name);
            Interactables.Add(interact);
            HeistPrims.Label(Root, pos + Vector3.up * 1.15f, challenge.name, 0.045f);
            slot++;
        }
    }

    void BuildConnector(HeistRoomPlan a, HeistRoomPlan b, bool hidden)
    {
        if (!HeistLevelGenerator.Touches(a, b) && !hidden) return;
        Vector3 ca = Center(a);
        int dir = HeistLevelGenerator.DirFrom(a, b);
        float along = HeistLevelGenerator.DoorAlong(a, b);
        if (hidden && a.links.Contains(IndexOf(b))) along += 2.2f;

        if (dir == 1 || dir == 3)
        {
            float x = ca.x + (dir == 1 ? a.width * 0.5f : -a.width * 0.5f);
            float z = ca.z + along;
            PlaceDoor(new Vector3(x, 1.1f, z), new Vector3(0.35f, 2.2f, 1.8f), a.name, hidden);
        }
        else
        {
            float z = ca.z + (dir == 0 ? a.depth * 0.5f : -a.depth * 0.5f);
            float x = ca.x + along;
            PlaceDoor(new Vector3(x, 1.1f, z), new Vector3(1.8f, 2.2f, 0.35f), a.name, hidden);
        }
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

    int IndexOf(HeistRoomPlan room)
    {
        for (int i = 0; i < Layout.Count; i++)
        {
            if (Layout[i] == room) return i;
        }
        return -1;
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

    public void PlaceSecurityCameras(int seed, float spawnRate = 1f)
    {
        if (Layout.Count == 0) return;
        var rng = new System.Random(seed ^ 0x5EC4);
        int count = Mathf.RoundToInt((2 + Layout.Count) * Mathf.Max(0f, spawnRate));
        if (count <= 0) return;
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
            const float inset = 0.22f;
            switch (wall)
            {
                case 0:
                    pos = c + new Vector3(along * room.width, 2.15f, hd - inset);
                    look = Vector3.back;
                    break;
                case 1:
                    pos = c + new Vector3(along * room.width, 2.15f, -hd + inset);
                    look = Vector3.forward;
                    break;
                case 2:
                    pos = c + new Vector3(-hw + inset, 2.15f, along * room.depth);
                    look = Vector3.right;
                    break;
                default:
                    pos = c + new Vector3(hw - inset, 2.15f, along * room.depth);
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

    public void PlaceVents(int seed)
    {
        if (Layout.Count == 0) return;
        var rng = new System.Random(seed ^ 0x7E17);
        for (int i = 0; i < Rooms.Count; i++)
        {
            var room = Layout[i];
            if (!Rooms[i].TryVentMount(Layout, rng, out Vector3 pos, out Quaternion rotation)) continue;

            var root = new GameObject("Vent");
            root.transform.SetParent(Root, false);
            root.transform.SetPositionAndRotation(pos, rotation);
            var grate = HeistPrims.Cube(root.transform, pos, new Vector3(0.9f, 0.55f, 0.12f), new Color(0.22f, 0.24f, 0.26f), "VentGrate");
            grate.transform.localPosition = Vector3.zero;
            grate.transform.localRotation = Quaternion.identity;
            HeistPrims.Label(root.transform, pos + Vector3.up * 0.45f, "VENT", 0.035f);
            var vent = root.AddComponent<HeistVent>();
            vent.roomName = room.name;
            vent.roomIndex = i;
            vent.lookPoint = Center(room) + Vector3.up * 1.35f;
            Vents.Add(vent);
        }

        for (int i = 0; i < Vents.Count; i++)
        {
            int links = 1 + rng.Next(0, Mathf.Min(3, Vents.Count - 1));
            int guard = 0;
            while (Vents[i].Destinations.Count < links && guard++ < 24)
            {
                var other = Vents[rng.Next(0, Vents.Count)];
                if (other == Vents[i]) continue;
                if (Vents[i].Destinations.Contains(other)) continue;
                Vents[i].Destinations.Add(other);
            }
        }
    }

    public void RevealHidden()
    {
        foreach (var interactable in Interactables)
        {
            if (interactable.challenge != null && interactable.challenge.type == "bypass")
                interactable.gameObject.SetActive(true);
        }
        foreach (var seal in HiddenSeals)
        {
            if (seal != null) seal.gameObject.SetActive(false);
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
        HiddenSeals.Clear();
        SecurityCameras.Clear();
        Vents.Clear();
        Rooms.Clear();
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
