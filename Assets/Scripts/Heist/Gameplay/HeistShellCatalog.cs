using UnityEngine;

[CreateAssetMenu(menuName = "Heist Society/Shell Catalog", fileName = "HeistShell")]
public class HeistShellCatalog : ScriptableObject
{
    public const float Module = 5f;
    public const float WallHeight = 3f;
    public const float WallThickness = 0.16f;
    public const float DoorLeaf = 1.13f;
    public const float DoorLeafLocalX = -1.934f;

    public GameObject wall;
    public GameObject doorWall;
    public GameObject door;
    public GameObject floor;
    public GameObject vaultFrame;
    public GameObject desk;
    public GameObject keyboard;
    public GameObject mouse;
    public GameObject screen;

    static HeistShellCatalog cached;

    public static HeistShellCatalog Load()
    {
        if (cached == null) cached = Resources.Load<HeistShellCatalog>("HeistShell");
#if UNITY_EDITOR
        if (cached != null)
        {
            if (cached.wall == null)
                cached.wall = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonHeist/Prefab/Environment/SM_Env_Wall_Interior_01.prefab");
            if (cached.doorWall == null)
                cached.doorWall = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonHeist/Prefab/Environment/SM_Env_Wall_Interior_Door_01.prefab");
            if (cached.door == null)
                cached.door = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonHeist/Prefab/Environment/SM_Env_Door_03.prefab");
            if (cached.floor == null)
                cached.floor = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonHeist/Prefab/Environment/SM_Env_Floor_01.prefab");
            if (cached.vaultFrame == null)
                cached.vaultFrame = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonHeist/Prefab/Environment/SM_Env_VaultDoor_Frame_01.prefab");
            if (cached.desk == null)
                cached.desk = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonHeist/Prefab/Props/SM_Prop_Desk_02.prefab");
            if (cached.keyboard == null)
                cached.keyboard = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonHeist/Prefab/Props/SM_Prop_Computer_Keyboard_01.prefab");
            if (cached.mouse == null)
                cached.mouse = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonHeist/Prefab/Props/SM_Prop_Computer_Mouse_01.prefab");
            if (cached.screen == null)
                cached.screen = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonHeist/Prefab/Props/SM_Prop_Computer_Screen_01.prefab");
        }
#endif
        return cached;
    }

    public bool HasWall => wall != null;
    public bool HasDoorWall => doorWall != null;
    public bool HasDoor => door != null;
    public bool HasFloor => floor != null;
    public bool HasVaultFrame => vaultFrame != null;
    public bool HasLaserConsole => desk != null && keyboard != null && mouse != null && screen != null;
}
