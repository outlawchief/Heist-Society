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
        }
#endif
        return cached;
    }

    public bool HasWall => wall != null;
    public bool HasDoorWall => doorWall != null;
    public bool HasDoor => door != null;
    public bool HasFloor => floor != null;
    public bool HasVaultFrame => vaultFrame != null;
}
