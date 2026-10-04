using System.Collections;
using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.Networking;
using Hashtable = ExitGames.Client.Photon.Hashtable;

public class HeistPhotonSession : MonoBehaviourPunCallbacks
{
    public const string LaunchKey = "launch";
    public const string CrewKey = "crew";
    const byte ClaimEvent = 10;
    const byte ClaimResultEvent = 11;
    const byte PoseEvent = 12;
    const byte SnapshotEvent = 13;

    public static HeistPhotonSession Instance { get; private set; }

    public string Status = "Idle";
    public bool RoomMissing;
    public HeistLaunch ActiveLaunch;
    public HeistGameSession Game { get; private set; }

    bool wantHost;
    bool abandoned;
    string wantCode = "";
    float sendTimer;

    public static void StartEditor(HeistBootstrap boot)
    {
        if (boot == null) return;
        Ensure(boot).ConnectFromSettings();
    }

    public static void HostFromWeb(HeistBootstrap boot, HeistLaunch launch)
    {
        if (boot == null || launch == null) return;
        Ensure(boot).HostLaunch(launch);
    }

    static HeistPhotonSession Ensure(HeistBootstrap boot)
    {
        var current = boot.GetComponent<HeistPhotonSession>();
        if (current == null) current = boot.gameObject.AddComponent<HeistPhotonSession>();
        return current;
    }

    void HostLaunch(HeistLaunch launch)
    {
        abandoned = false;
        RoomMissing = false;
        Game = null;
        ActiveLaunch = launch;
        wantHost = true;
        wantCode = !string.IsNullOrEmpty(launch.joinCode) ? launch.joinCode.Trim().ToUpperInvariant() : HeistBootstrap.MakeCode();
        StopAllCoroutines();
        StartCoroutine(GiveUpPhotonIfNeeded());
        ConnectPhotonOrLocal(null);
    }

    IEnumerator GiveUpPhotonIfNeeded()
    {
        yield return new WaitForSeconds(6f);
        if (Game != null || abandoned) yield break;
        Status = "Photon did not connect. Starting a local heist.";
        StartLocalHeist();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public override void OnEnable()
    {
        base.OnEnable();
        if (PhotonNetwork.NetworkingClient != null)
            PhotonNetwork.NetworkingClient.EventReceived += HandleEvent;
    }

    public override void OnDisable()
    {
        if (PhotonNetwork.NetworkingClient != null)
            PhotonNetwork.NetworkingClient.EventReceived -= HandleEvent;
        base.OnDisable();
    }

    public void ConnectFromSettings()
    {
        RoomMissing = false;
        var settings = Resources.Load<HeistTestSettings>("HeistTestSettings");
        if (settings == null)
        {
            Status = "Heist Test Settings are missing.";
            return;
        }

        ActiveLaunch = settings.BuildLaunch();
        wantHost = string.IsNullOrWhiteSpace(settings.roomCode);
        wantCode = wantHost ? HeistBootstrap.MakeCode() : settings.roomCode.Trim().ToUpperInvariant();

        if (!wantHost)
        {
            Status = "Looking for LAN session " + wantCode + "...";
            StopAllCoroutines();
            StartCoroutine(JoinExisting(settings));
            return;
        }

        ConnectPhotonOrLocal(settings);
    }

    IEnumerator JoinExisting(HeistTestSettings settings)
    {
        string origin = ActiveLaunch != null && !string.IsNullOrEmpty(ActiveLaunch.origin)
            ? ActiveLaunch.origin.TrimEnd('/')
            : "http://127.0.0.1:8765";

        string lastDetail = "";
        for (int attempt = 0; attempt < 12; attempt++)
        {
            using (var req = UnityWebRequest.Get(origin + "/coop/" + wantCode + "/launch"))
            {
                yield return req.SendWebRequest();
                lastDetail = req.responseCode + " " + req.error;
                if (req.result == UnityWebRequest.Result.Success)
                {
                    var launch = JsonUtility.FromJson<HeistLaunch>(HeistJson.NormalizeInbound(req.downloadHandler.text));
                    if (launch != null && launch.crew != null && launch.crew.Length > 0)
                    {
                        JoinLanSession(launch, origin, settings);
                        yield break;
                    }
                    lastDetail = "empty crew in " + req.downloadHandler.text;
                }
            }
            Status = "Waiting for LAN session " + wantCode + " at " + origin + "…";
            yield return new WaitForSeconds(0.4f);
        }

        RoomMissing = true;
        Status = "No LAN session " + wantCode + " at " + origin + " (" + lastDetail + "). Hard-refresh the website after Launch Heist, keep serve.py running, then Restart.";
        Debug.LogWarning(Status);
    }

    void JoinLanSession(HeistLaunch launch, string origin, HeistTestSettings settings)
    {
        launch.joinCode = wantCode;
        launch.origin = origin;
        launch.possessId = GuestCrewId(launch, settings);
        ActiveLaunch = launch;
        RoomMissing = false;
        Status = "Joined LAN session " + wantCode + " as " + launch.possessId + ".";
        var boot = GetComponent<HeistBootstrap>();
        if (boot == null) return;
        boot.JoinHeist(JsonUtility.ToJson(launch));
        Game = boot.Session;
    }

    static string GuestCrewId(HeistLaunch launch, HeistTestSettings settings)
    {
        if (launch == null || launch.crew == null) return "";
        if (settings != null)
        {
            foreach (var member in launch.crew)
            {
                if (member == null || member.isOrganizer) continue;
                if (member.id == settings.codeName || member.name == settings.codeName) return member.id;
            }
        }
        foreach (var member in launch.crew)
        {
            if (member != null && !member.isOrganizer && !string.IsNullOrEmpty(member.id)) return member.id;
        }
        if (launch.crew.Length > 1 && launch.crew[1] != null) return launch.crew[1].id;
        return launch.crew[0] != null ? launch.crew[0].id : "";
    }

    void ConnectPhotonOrLocal(HeistTestSettings settings, bool allowLocalHostFallback = true)
    {
        string appId = PhotonNetwork.PhotonServerSettings != null
            ? PhotonNetwork.PhotonServerSettings.AppSettings.AppIdRealtime
            : "";
        if (string.IsNullOrEmpty(appId))
        {
            if (!allowLocalHostFallback)
            {
                RoomMissing = true;
                Status = "No LAN session for " + wantCode + ". Launch Heist in the website first (same serve.py / join code).";
                return;
            }
            Status = "Photon App Id is missing. Starting a local heist.";
            StartLocalHeist();
            return;
        }

        PhotonNetwork.AutomaticallySyncScene = false;
        PhotonNetwork.NickName = settings != null && !string.IsNullOrWhiteSpace(settings.codeName)
            ? settings.codeName
            : "Operative";
        if (PhotonNetwork.InRoom)
        {
            Status = "Leaving the current room...";
            PhotonNetwork.LeaveRoom();
            return;
        }
        if (PhotonNetwork.IsConnectedAndReady)
        {
            JoinOrCreate();
            return;
        }

        Status = "Connecting to Photon...";
        PhotonNetwork.ConnectUsingSettings();
    }

    public override void OnConnectedToMaster()
    {
        if (abandoned || Game != null)
        {
            PhotonNetwork.Disconnect();
            return;
        }
        JoinOrCreate();
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        if (Game != null || abandoned) return;
        Status = "Photon unavailable (" + cause + "). Starting a local heist.";
        StartLocalHeist();
    }

    void JoinOrCreate()
    {
        if (ActiveLaunch == null) return;
        if (wantHost)
        {
            Status = "Creating room " + wantCode + "...";
            var options = new RoomOptions
            {
                MaxPlayers = (byte)Mathf.Clamp(ActiveLaunch != null && ActiveLaunch.crew != null ? ActiveLaunch.crew.Length : 2, 1, 20),
                IsVisible = false,
                IsOpen = true,
                CustomRoomProperties = new Hashtable { { LaunchKey, JsonUtility.ToJson(ActiveLaunch) } }
            };
            PhotonNetwork.CreateRoom(wantCode, options);
            return;
        }

        Status = "Joining room " + wantCode + "...";
        PhotonNetwork.JoinRoom(wantCode);
    }

    public override void OnJoinedRoom()
    {
        if (abandoned)
        {
            PhotonNetwork.Disconnect();
            return;
        }
        RoomMissing = false;
        string code = PhotonNetwork.CurrentRoom != null ? PhotonNetwork.CurrentRoom.Name : wantCode;
        if (PhotonNetwork.IsMasterClient)
        {
            string crewId = HostCrewId(ActiveLaunch);
            PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { CrewKey, crewId } });
            Status = "Hosting room " + code;
            StartLevel(ActiveLaunch, crewId, true, false, code);
            return;
        }

        object launchJson = null;
        if (PhotonNetwork.CurrentRoom != null)
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(LaunchKey, out launchJson);
        var launch = JsonUtility.FromJson<HeistLaunch>(launchJson as string);
        if (launch == null || launch.crew == null || launch.crew.Length == 0)
        {
            Status = "Joined " + code + ", but that room has no heist.";
            return;
        }

        ActiveLaunch = launch;
        Status = "In room " + code + ". Pick an operative below.";
        StartLevel(launch, "", false, true, code);
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        RoomMissing = true;
        Status = "Photon room " + wantCode + " does not exist. WebGL hosts use the site join code on serve.py, not Photon — keep the same Room code and Origin as the browser.";
        Debug.LogWarning("Heist join failed (" + returnCode + "): " + message);
    }

    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        RoomMissing = true;
        Status = returnCode == ErrorCode.PluginReportedError
            ? "Photon rejected this App Id (Unsupported Plugin). Put a Realtime / PUN App Id in Photon Server Settings, not a Fusion, Voice, or Chat id. Starting a local heist."
            : "Could not create the Photon room (" + message + "). Starting a local heist.";
        Debug.LogWarning("Heist create failed (" + returnCode + "): " + message);
        StartLocalHeist();
    }

    void StartLocalHeist()
    {
        if (Game != null || ActiveLaunch == null) return;
        abandoned = true;
        StopAllCoroutines();
        var boot = GetComponent<HeistBootstrap>();
        if (boot == null) return;
        boot.BeginOffline(ActiveLaunch);
        Game = boot.Session;
        if (PhotonNetwork.IsConnected) PhotonNetwork.Disconnect();
    }

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
    {
        if (!PhotonNetwork.IsMasterClient || Game == null || changedProps == null) return;
        if (!changedProps.ContainsKey(CrewKey)) return;
        string crewId = CrewOf(targetPlayer);
        if (!string.IsNullOrEmpty(crewId)) Game.ReleaseCrew(crewId);
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        if (!PhotonNetwork.IsMasterClient || Game == null) return;
        string crewId = CrewOf(otherPlayer);
        if (!string.IsNullOrEmpty(crewId)) Game.ReturnToAi(crewId);
    }

    public void RequestClaim(string crewId)
    {
        if (string.IsNullOrEmpty(crewId) || !PhotonNetwork.InRoom) return;
        if (PhotonNetwork.IsMasterClient)
        {
            TryAcceptClaim(PhotonNetwork.LocalPlayer.ActorNumber, crewId);
            return;
        }
        PhotonNetwork.RaiseEvent(ClaimEvent, crewId, new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient }, SendOptions.SendReliable);
    }

    public bool HasLocalClaim => !string.IsNullOrEmpty(CrewOf(PhotonNetwork.LocalPlayer));

    public List<HeistCrewMember> Unclaimed()
    {
        var open = new List<HeistCrewMember>();
        if (ActiveLaunch == null || ActiveLaunch.crew == null || !PhotonNetwork.InRoom) return open;
        var taken = new HashSet<string>();
        foreach (var player in PhotonNetwork.PlayerList)
        {
            string id = CrewOf(player);
            if (!string.IsNullOrEmpty(id)) taken.Add(id);
        }
        foreach (var member in ActiveLaunch.crew)
        {
            if (member != null && !string.IsNullOrEmpty(member.id) && !taken.Contains(member.id))
                open.Add(member);
        }
        return open;
    }

    public static string CrewOf(Player player)
    {
        if (player == null || player.CustomProperties == null) return "";
        if (player.CustomProperties.TryGetValue(CrewKey, out object value) && value is string id) return id;
        return "";
    }

    void Update()
    {
        if (!PhotonNetwork.InRoom || Game == null) return;
        sendTimer -= Time.deltaTime;
        if (sendTimer > 0f) return;
        sendTimer = 0.1f;
        if (PhotonNetwork.IsMasterClient) BroadcastSnapshot();
        else SendPose();
    }

    void StartLevel(HeistLaunch launch, string possess, bool host, bool claimLater, string code)
    {
        var boot = GetComponent<HeistBootstrap>();
        if (boot == null || launch == null) return;
        boot.BeginPhotonSession(launch, possess, host, code, claimLater);
        Game = boot.Session;
    }

    void HandleEvent(EventData data)
    {
        if (data == null) return;
        if (data.Code == ClaimEvent && PhotonNetwork.IsMasterClient)
            TryAcceptClaim(data.Sender, data.CustomData as string);
        else if (data.Code == ClaimResultEvent)
            AcceptLocalClaim(data.CustomData as string);
        else if (data.Code == PoseEvent && PhotonNetwork.IsMasterClient)
            ApplyPose(data.CustomData as string);
        else if (data.Code == SnapshotEvent && !PhotonNetwork.IsMasterClient)
            ApplySnapshot(data.CustomData as string);
    }

    void TryAcceptClaim(int actor, string crewId)
    {
        if (string.IsNullOrEmpty(crewId) || ActiveLaunch == null || ActiveLaunch.crew == null) return;
        bool exists = false;
        foreach (var member in ActiveLaunch.crew)
        {
            if (member != null && member.id == crewId) exists = true;
        }
        bool taken = !exists;
        foreach (var player in PhotonNetwork.PlayerList)
        {
            string owned = CrewOf(player);
            if (player.ActorNumber == actor && !string.IsNullOrEmpty(owned)) taken = true;
            if (player.ActorNumber != actor && owned == crewId) taken = true;
        }
        if (taken)
        {
            PhotonNetwork.RaiseEvent(ClaimResultEvent, "", new RaiseEventOptions { TargetActors = new[] { actor } }, SendOptions.SendReliable);
            return;
        }

        Game?.ReleaseCrew(crewId);
        PhotonNetwork.RaiseEvent(
            ClaimResultEvent,
            crewId,
            new RaiseEventOptions { TargetActors = new[] { actor } },
            SendOptions.SendReliable);
    }

    void AcceptLocalClaim(string crewId)
    {
        if (string.IsNullOrEmpty(crewId))
        {
            Status = "That operative is already taken.";
            return;
        }

        PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { CrewKey, crewId } });
        Game?.Possess(crewId);
        string name = crewId;
        if (ActiveLaunch != null && ActiveLaunch.crew != null)
        {
            foreach (var member in ActiveLaunch.crew)
            {
                if (member != null && member.id == crewId) name = member.name;
            }
        }
        Status = "Controlling " + name + ".";
    }

    void SendPose()
    {
        var op = Game != null ? Game.LocalOperative : null;
        if (op == null || op.Member == null) return;
        var pose = new HeistNetPose
        {
            id = op.Member.id,
            x = op.transform.position.x,
            z = op.transform.position.z,
            hp = op.Health,
            loot = op.carryingLoot,
            downed = op.downed
        };
        PhotonNetwork.RaiseEvent(PoseEvent, JsonUtility.ToJson(pose), new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient }, SendOptions.SendUnreliable);
    }

    void ApplyPose(string json)
    {
        if (Game == null || string.IsNullOrEmpty(json)) return;
        var pose = JsonUtility.FromJson<HeistNetPose>(json);
        if (pose == null || string.IsNullOrEmpty(pose.id)) return;
        foreach (var op in Game.Operatives)
        {
            if (op == null || op.Member == null || op.Member.id != pose.id || op.isLocal) continue;
            op.SetRole(false, false);
            op.ApplyRemote(new Vector3(pose.x, op.transform.position.y, pose.z), pose.downed, pose.loot, pose.hp);
        }
    }

    void BroadcastSnapshot()
    {
        if (Game == null) return;
        var poses = new HeistNetPose[Game.Operatives.Count];
        for (int i = 0; i < Game.Operatives.Count; i++)
        {
            var op = Game.Operatives[i];
            var p = op.transform.position;
            poses[i] = new HeistNetPose
            {
                id = op.Member.id,
                x = p.x,
                z = p.z,
                hp = op.Health,
                loot = op.carryingLoot,
                downed = op.downed
            };
        }
        var bag = new HeistNetPoseBag { ops = poses };
        PhotonNetwork.RaiseEvent(SnapshotEvent, JsonUtility.ToJson(bag), new RaiseEventOptions { Receivers = ReceiverGroup.Others }, SendOptions.SendUnreliable);
    }

    void ApplySnapshot(string json)
    {
        if (Game == null || string.IsNullOrEmpty(json)) return;
        var bag = JsonUtility.FromJson<HeistNetPoseBag>(json);
        if (bag == null || bag.ops == null) return;
        foreach (var pose in bag.ops)
        {
            if (pose == null) continue;
            foreach (var op in Game.Operatives)
            {
                if (op == null || op.Member == null || op.Member.id != pose.id || op.isLocal) continue;
                op.ApplyRemote(new Vector3(pose.x, op.transform.position.y, pose.z), pose.downed, pose.loot, pose.hp);
            }
        }
    }

    static string HostCrewId(HeistLaunch launch)
    {
        if (launch != null && launch.crew != null)
        {
            foreach (var member in launch.crew)
            {
                if (member != null && member.isOrganizer && !string.IsNullOrEmpty(member.id)) return member.id;
            }
            if (launch.crew.Length > 0 && launch.crew[0] != null) return launch.crew[0].id;
        }
        return launch != null ? launch.organizerId : "";
    }
}

[System.Serializable]
public class HeistNetPose
{
    public string id;
    public float x;
    public float z;
    public float hp;
    public bool loot;
    public bool downed;
}

[System.Serializable]
public class HeistNetPoseBag
{
    public HeistNetPose[] ops;
}
