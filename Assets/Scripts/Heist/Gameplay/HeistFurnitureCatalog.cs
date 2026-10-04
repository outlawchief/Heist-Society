using UnityEngine;

[CreateAssetMenu(menuName = "Heist Society/Furniture Catalog", fileName = "HeistFurniture")]
public class HeistFurnitureCatalog : ScriptableObject
{
    public GameObject[] extract;
    public GameObject[] corridor;
    public GameObject[] lobby;
    public GameObject[] office;
    public GameObject[] security;
    public GameObject[] archives;
    public GameObject[] vault;
    public GameObject[] generic;

    public GameObject[] For(HeistRoomKind kind)
    {
        switch (kind)
        {
            case HeistRoomKind.Extract: return extract;
            case HeistRoomKind.Corridor: return corridor;
            case HeistRoomKind.Lobby: return lobby;
            case HeistRoomKind.Office: return office;
            case HeistRoomKind.Security: return security;
            case HeistRoomKind.Archives: return archives;
            case HeistRoomKind.Vault: return vault;
            default: return generic;
        }
    }
}
