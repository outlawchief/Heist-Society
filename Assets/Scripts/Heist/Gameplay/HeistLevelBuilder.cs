using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

public class HeistLevelBuilder : MonoBehaviour
{
    public const float DoorWidth = HeistShellCatalog.Module;

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
    public readonly List<HeistLaserGrid> LaserGrids = new List<HeistLaserGrid>();
    public readonly List<HeistRoom> Rooms = new List<HeistRoom>();
    public readonly List<Vector3> DoorPoints = new List<Vector3>();

    struct PendingLaserDesk
    {
        public HeistRoomPlan laserRoom;
        public HeistChallengeResult challenge;
        public HeistLaserGrid grid;
    }

    readonly List<PendingLaserDesk> pendingLaserDesks = new List<PendingLaserDesk>();
    System.Random laserRng;

    NavMeshSurface navSurface;

    public void Build(List<HeistRoomPlan> rooms, int seed = 0, float cameraSpawnRate = 1f)
    {
        Clear();
        laserRng = new System.Random(seed ^ 0x1A5E);
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
        PlacePendingLaserDesks();

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
        HeistFurniturePlacer.Place(this, seed);
        BakeNavMesh();
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
            if (challenge.type == "vent")
                continue;

            if (challenge.type == "lasers")
            {
                if (!plan.vault) PlaceLaserTrap(plan, c, challenge);
                slot++;
                continue;
            }

            if (challenge.type == "vault" || plan.vault && challenge.type == "vault")
            {
                PlaceVault(plan, c, challenge);
                slot++;
                continue;
            }

            Vector3 pos = WallPropPosition(plan, c, slot, challenge);
            var block = HeistPrims.Cube(Root, pos, new Vector3(1.1f, 1.4f, 1.1f), ColorFor(challenge.type), challenge.name);
            var interact = block.AddComponent<HeistInteractable>();
            interact.Setup(challenge, plan.name);
            Interactables.Add(interact);
            HeistPrims.Label(Root, pos + Vector3.up * 1.15f, challenge.name, 0.045f);
            slot++;
        }
    }

    void PlaceVault(HeistRoomPlan plan, Vector3 c, HeistChallengeResult challenge)
    {
        var catalog = HeistShellCatalog.Load();
        HeistRoom.WallModulePose(plan, 1, HeistRoom.VaultCellStart(plan), out Vector3 pivot, out Quaternion rot);
        GameObject vault;
        if (catalog != null && catalog.HasVaultFrame)
        {
            vault = Instantiate(catalog.vaultFrame, pivot, rot, Root);
            vault.name = "Vault";
        }
        else
        {
            Vector3 pos = c + new Vector3(plan.width * 0.5f - 0.9f, 0.7f, 0f);
            vault = HeistPrims.Cube(Root, pos, new Vector3(1.1f, 1.4f, 1.1f), ColorFor("vault"), "Vault");
        }

        Transform interactOn = vault.transform.Find("SM_Env_VaultDoor_Lid_01");
        if (interactOn == null) interactOn = vault.transform;
        VaultPoint = interactOn.position;
        var interact = interactOn.gameObject.AddComponent<HeistInteractable>();
        interact.Setup(challenge, plan.name);
        Interactables.Add(interact);
        HeistPrims.Label(Root, VaultPoint + Vector3.up * 1.15f, challenge.name, 0.045f);
        PlaceVaultLasers(plan, c, LaserChallenge(plan));
    }

    static HeistChallengeResult LaserChallenge(HeistRoomPlan plan)
    {
        if (plan?.challenges == null) return null;
        foreach (var challenge in plan.challenges)
        {
            if (challenge != null && challenge.type == "lasers") return challenge;
        }
        return null;
    }

    void PlaceVaultLasers(HeistRoomPlan plan, Vector3 c, HeistChallengeResult challenge)
    {
        var grid = SpawnWallLaser(plan, 1, 2.2f);
        QueueLaserDesk(plan, challenge ?? new HeistChallengeResult
        {
            name = "Laser Grid",
            type = "lasers",
            skill = "agi",
            threshold = 5
        }, grid);
    }

    void PlaceLaserTrap(HeistRoomPlan plan, Vector3 c, HeistChallengeResult challenge)
    {
        int wall = EntranceWall(plan);
        var grid = SpawnWallLaser(plan, wall, 1.6f);
        QueueLaserDesk(plan, challenge, grid);
    }

    HeistLaserGrid SpawnWallLaser(HeistRoomPlan plan, int wallDir, float insetFromWall)
    {
        float hw = plan.width * 0.5f;
        float hd = plan.depth * 0.5f;
        float wallPad = HeistRoom.WallThickness;
        Vector3 along;
        Vector3 center;
        float width;
        if (wallDir == 0 || wallDir == 2)
        {
            along = Vector3.right;
            width = Mathf.Max(1.5f, plan.width - wallPad);
            float z = plan.cz + (wallDir == 0 ? hd - insetFromWall : -hd + insetFromWall);
            center = new Vector3(plan.cx, 1.15f, z);
        }
        else
        {
            along = Vector3.forward;
            width = Mathf.Max(1.5f, plan.depth - wallPad);
            float x = plan.cx + (wallDir == 1 ? hw - insetFromWall : -hw + insetFromWall);
            center = new Vector3(x, 1.15f, plan.cz);
        }

        var grid = HeistLaserGrid.Spawn(Root, center, along, width, 2.3f, 5);
        LaserGrids.Add(grid);
        return grid;
    }

    int EntranceWall(HeistRoomPlan plan)
    {
        int best = -1;
        foreach (int link in plan.links)
        {
            if (best < 0 || link < best) best = link;
        }
        if (best < 0 || best >= Layout.Count) return 3;
        return HeistLevelGenerator.DirFrom(plan, Layout[best]);
    }

    void QueueLaserDesk(HeistRoomPlan laserRoom, HeistChallengeResult challenge, HeistLaserGrid grid)
    {
        pendingLaserDesks.Add(new PendingLaserDesk
        {
            laserRoom = laserRoom,
            challenge = challenge,
            grid = grid
        });
    }

    void PlacePendingLaserDesks()
    {
        if (laserRng == null) laserRng = new System.Random(7);
        foreach (var pending in pendingLaserDesks)
        {
            var room = PickDeskRoom(pending.laserRoom);
            Vector3 c = Center(room);
            int wall = laserRng.Next(0, 4);
            float inset = 1.15f;
            float hw = room.width * 0.5f - inset;
            float hd = room.depth * 0.5f - inset;
            Vector3 pos;
            Vector3 inward;
            switch (wall)
            {
                case 0:
                    pos = new Vector3(c.x, 0f, c.z + hd);
                    inward = Vector3.back;
                    break;
                case 1:
                    pos = new Vector3(c.x + hw, 0f, c.z);
                    inward = Vector3.left;
                    break;
                case 2:
                    pos = new Vector3(c.x, 0f, c.z - hd);
                    inward = Vector3.forward;
                    break;
                default:
                    pos = new Vector3(c.x - hw, 0f, c.z);
                    inward = Vector3.right;
                    break;
            }
            PlaceLaserPanel(pos, inward, pending.challenge, room.name, pending.grid);
        }
        pendingLaserDesks.Clear();
    }

    HeistRoomPlan PickDeskRoom(HeistRoomPlan avoid)
    {
        var options = new List<HeistRoomPlan>();
        foreach (var room in Layout)
        {
            if (room != null && room != avoid) options.Add(room);
        }
        if (options.Count == 0) return avoid;
        return options[laserRng.Next(0, options.Count)];
    }

    void PlaceLaserPanel(Vector3 pos, Vector3 inward, HeistChallengeResult challenge, string roomName, HeistLaserGrid grid)
    {
        pos.y = 0f;
        if (inward.sqrMagnitude < 0.01f) inward = Vector3.left;
        inward.y = 0f;
        Quaternion rot = Quaternion.LookRotation(inward.normalized, Vector3.up);

        var catalog = HeistShellCatalog.Load();
        GameObject root;
        if (catalog != null && catalog.HasLaserConsole)
        {
            root = Instantiate(catalog.desk, pos, rot, Root);
            root.name = "LaserDesk";
            PlaceDeskKit(root, catalog);
        }
        else
        {
            root = HeistPrims.Cube(Root, pos + Vector3.up * 0.7f, new Vector3(0.55f, 1.05f, 0.28f), ColorFor("lasers"), "LaserPanel");
        }

        var hook = new GameObject("LaserConsole");
        hook.transform.SetParent(root.transform, false);
        Bounds bounds = WorldBounds(root);
        hook.transform.position = new Vector3(bounds.center.x, Mathf.Max(0.9f, bounds.max.y), bounds.center.z);
        var interact = hook.AddComponent<HeistInteractable>();
        interact.Setup(challenge, roomName);
        Interactables.Add(interact);
        if (grid != null) grid.panel = interact;
        HeistPrims.Label(Root, hook.transform.position + Vector3.up * 0.45f, "LASERS", 0.04f);
    }

    static void PlaceDeskKit(GameObject desk, HeistShellCatalog catalog)
    {
        Bounds deskBounds = WorldBounds(desk);
        Vector3 top = new Vector3(deskBounds.center.x, deskBounds.max.y, deskBounds.center.z);
        Vector3 forward = desk.transform.forward;
        Vector3 right = desk.transform.right;
        SpawnOnDesk(desk.transform, catalog.screen, top + forward * 0.08f);
        SpawnOnDesk(desk.transform, catalog.keyboard, top - forward * 0.22f - right * 0.08f);
        SpawnOnDesk(desk.transform, catalog.mouse, top - forward * 0.18f + right * 0.28f);
    }

    static void SpawnOnDesk(Transform desk, GameObject prefab, Vector3 worldPos)
    {
        if (prefab == null) return;
        var go = Object.Instantiate(prefab, desk);
        go.transform.position = worldPos;
        go.transform.rotation = desk.rotation;
    }

    static Bounds WorldBounds(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers == null || renderers.Length == 0)
            return new Bounds(go.transform.position, Vector3.one);
        var bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    Vector3 WallPropPosition(HeistRoomPlan plan, Vector3 c, int slot, HeistChallengeResult challenge)
    {
        float hw = plan.width * 0.5f - 0.9f;
        float hd = plan.depth * 0.5f - 0.9f;
        if (challenge != null && (challenge.type == "vault" || plan.vault && challenge.type == "vault"))
            return c + new Vector3(hw, 0.7f, 0f);
        if (challenge != null && (challenge.type == "bypass" || challenge.isBypass))
            return c + new Vector3(hw * 0.92f, 0.7f, hd * 0.92f);

        int wall = slot % 4;
        float spread = ((slot / 4) - 0.5f) * 1.7f;
        switch (wall)
        {
            case 0: return c + new Vector3(Mathf.Clamp(spread, -hw + 0.5f, hw - 0.5f), 0.7f, hd);
            case 1: return c + new Vector3(Mathf.Clamp(spread, -hw + 0.5f, hw - 0.5f), 0.7f, -hd);
            case 2: return c + new Vector3(-hw, 0.7f, Mathf.Clamp(spread, -hd + 0.5f, hd - 0.5f));
            default: return c + new Vector3(hw, 0.7f, Mathf.Clamp(spread, -hd + 0.5f, hd - 0.5f));
        }
    }

    void BuildConnector(HeistRoomPlan a, HeistRoomPlan b, bool hidden)
    {
        if (!HeistLevelGenerator.Touches(a, b) && !hidden) return;
        if (HeistRoom.TryConnectorPose(a, b, Layout, hidden, out Vector3 leafPos, out Quaternion rot))
            PlaceDoor(leafPos, rot, a.name, hidden);
    }

    void PlaceDoor(Vector3 pos, Quaternion rot, string roomName, bool hidden)
    {
        var catalog = HeistShellCatalog.Load();
        GameObject door;
        if (catalog != null && catalog.HasDoor)
        {
            door = Instantiate(catalog.door, pos, rot, Root);
            door.name = hidden ? "HiddenDoor" : "Door";
            var back = Instantiate(catalog.door, door.transform);
            back.name = "Door_Back";
            back.transform.localPosition = new Vector3(-HeistShellCatalog.DoorLeaf, 0f, 0f);
            back.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            foreach (var col in back.GetComponentsInChildren<Collider>(true))
                col.enabled = false;
        }
        else
        {
            door = HeistPrims.Cube(Root, pos + Vector3.up * HeistRoom.WallCenterY, new Vector3(HeistShellCatalog.DoorLeaf, 2.2f, 0.12f), hidden ? new Color(0.3f, 0.25f, 0.12f) : new Color(0.45f, 0.32f, 0.18f), hidden ? "HiddenDoor" : "Door");
            door.transform.rotation = rot;
        }
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
        DoorPoints.Add(pos);
        if (hidden)
        {
            var box = door.GetComponent<BoxCollider>();
            if (box == null) box = door.GetComponentInChildren<BoxCollider>();
            if (box != null) HiddenDoors.Add(box);
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
            case "lasers": return new Color(0.65f, 0.12f, 0.12f);
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
        cam.orthographic = false;
        cam.fieldOfView = 40f;
        cam.nearClipPlane = 0.3f;
        cam.farClipPlane = 120f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.03f, 0.035f, 0.04f);
        var follow = cam.GetComponent<HeistCameraFollow>();
        if (follow == null) follow = cam.gameObject.AddComponent<HeistCameraFollow>();
        follow.offset = new Vector3(0f, 13.5f, -12.5f);
        follow.tilt = 46f;
        cam.transform.rotation = Quaternion.Euler(follow.tilt, 0f, 0f);
        cam.transform.position = follow.offset;
        if (cam.GetComponent<AudioListener>() == null && FindFirstObjectByType<AudioListener>() == null)
            cam.gameObject.AddComponent<AudioListener>();
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

    public Vector3[] PatrolLoop(HeistRoomPlan room)
    {
        if (room == null) return System.Array.Empty<Vector3>();
        return PatrolRoute(IndexOf(room), 0);
    }

    public void BakeNavMesh()
    {
        if (Root == null) return;
        if (navSurface == null)
        {
            navSurface = Root.gameObject.GetComponent<NavMeshSurface>();
            if (navSurface == null) navSurface = Root.gameObject.AddComponent<NavMeshSurface>();
            navSurface.collectObjects = CollectObjects.Children;
            navSurface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            navSurface.layerMask = ~0;
            navSurface.overrideVoxelSize = true;
            navSurface.voxelSize = 0.08f;
            navSurface.agentTypeID = 0;
            navSurface.minRegionArea = 0.05f;
        }
        navSurface.minRegionArea = 0.05f;

        var unread = new List<MeshCollider>();
        foreach (var col in Root.GetComponentsInChildren<MeshCollider>(true))
        {
            if (col == null || !col.enabled || col.sharedMesh == null || col.sharedMesh.isReadable) continue;
            col.enabled = false;
            unread.Add(col);
        }
        navSurface.BuildNavMesh();
        for (int i = 0; i < unread.Count; i++)
        {
            if (unread[i] != null) unread[i].enabled = true;
        }
    }

    public Vector3 SnapToNav(Vector3 point, float range = 2.4f)
    {
        if (NavMesh.SamplePosition(point, out NavMeshHit hit, range, NavMesh.AllAreas))
            return hit.position;
        if (NavMesh.SamplePosition(point, out hit, range * 2.5f, NavMesh.AllAreas))
            return hit.position;
        return point;
    }

    public void PlaceSecurityCameras(int seed, float spawnRate = 1f)
    {
        if (Layout.Count == 0) return;
        var rng = new System.Random(seed ^ 0x5EC4);
        int count = Mathf.RoundToInt((2 + Layout.Count) * Mathf.Max(0f, spawnRate));
        if (count <= 0) return;
        Vector3 spawn = ExtractPoint + Vector3.up * 0.9f;
        int placed = 0;
        int attempts = 0;
        int want = count;
        while (placed < want && attempts < want * 14)
        {
            attempts++;
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

            if (SeesExtract(pos, look, spawn))
                continue;

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
            foreach (var col in root.GetComponentsInChildren<Collider>())
                col.isTrigger = true;
            placed++;
        }
    }

    bool SeesExtract(Vector3 camPos, Vector3 look, Vector3 spawn)
    {
        float range = 6.4f;
        float half = 42f;
        if (HeistSecurityCamera.ConeSees(camPos, look, spawn, range, half)) return true;
        if (HeistSecurityCamera.ConeSees(camPos, look, ExtractPoint, range, half)) return true;
        Vector3[] pad =
        {
            ExtractPoint + new Vector3(1.2f, 0.9f, 1.2f),
            ExtractPoint + new Vector3(-1.2f, 0.9f, 1.2f),
            ExtractPoint + new Vector3(1.2f, 0.9f, -1.2f),
            ExtractPoint + new Vector3(-1.2f, 0.9f, -1.2f)
        };
        foreach (var p in pad)
        {
            if (HeistSecurityCamera.ConeSees(camPos, look, p, range, half)) return true;
        }
        return false;
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
            foreach (var col in root.GetComponentsInChildren<Collider>())
                col.isTrigger = true;
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
        BakeNavMesh();
    }

    public int IndexOf(HeistInteractable item)
    {
        if (item == null) return -1;
        for (int i = 0; i < Interactables.Count; i++)
        {
            if (Interactables[i] == item) return i;
        }
        return -1;
    }

    public void Clear()
    {
        navSurface = null;
        if (Root != null) Destroy(Root.gameObject);
        Interactables.Clear();
        RoomCenters.Clear();
        HiddenDoors.Clear();
        HiddenSeals.Clear();
        SecurityCameras.Clear();
        Vents.Clear();
        Rooms.Clear();
        Layout.Clear();
        DoorPoints.Clear();
        LaserGrids.Clear();
        pendingLaserDesks.Clear();
    }
}

public class HeistExtractZone : MonoBehaviour { }

public class HeistCameraFollow : MonoBehaviour
{
    public Transform target;
    public Vector3 offset = new Vector3(0f, 13.5f, -12.5f);
    public float tilt = 46f;

    void LateUpdate()
    {
        Quaternion look = Quaternion.Euler(tilt, 0f, 0f);
        transform.rotation = look;
        if (target == null)
        {
            transform.position = offset;
            return;
        }
        Vector3 desired = target.position + offset;
        transform.position = Vector3.Lerp(transform.position, desired, Time.deltaTime * 6f);
    }
}
