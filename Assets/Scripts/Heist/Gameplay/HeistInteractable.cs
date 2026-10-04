using UnityEngine;

public class HeistInteractable : MonoBehaviour
{
    public HeistChallengeResult challenge;
    public string roomName;
    public bool blocksPath;
    public bool completed;
    public float fill;

    Collider obstacle;

    public void Setup(HeistChallengeResult data, string room)
    {
        challenge = data;
        roomName = room;
        obstacle = GetComponent<Collider>();
        if (obstacle == null) obstacle = GetComponentInChildren<Collider>();
    }

    public float HoldTime(HeistOperative operative)
    {
        int skill = operative.Member.stats.Get(challenge.skill);
        int gear = HeistResolver.GearBonus(operative.Member.gear, challenge);
        float time = Mathf.Max(0.55f, 2.4f - skill * 0.14f - gear * 0.35f);
        if (challenge.type == "vault") time += 0.4f;
        return time;
    }

    public bool Matches(HeistOperative operative)
    {
        return true;
    }

    public void Complete(HeistOperative operative)
    {
        completed = true;
        fill = 1f;
        challenge.passed = true;
        challenge.actorId = operative.Member.id;
        challenge.actorName = operative.Member.name;
        challenge.narration = $"{operative.Member.name} cleared {challenge.name}.";
        HeistPrims.Paint(gameObject, new Color(0.3f, 0.7f, 0.35f));
        if (challenge != null && challenge.type == "lasers")
        {
            foreach (var grid in FindObjectsByType<HeistLaserGrid>(FindObjectsSortMode.None))
            {
                if (grid == null) continue;
                if (grid != null && grid.panel == this)
                    grid.Disarm();
            }
        }
        else if (challenge != null && challenge.type == "vault")
        {
            var lid = transform.name == "SM_Env_VaultDoor_Lid_01" ? transform : transform.Find("SM_Env_VaultDoor_Lid_01");
            if (lid != null) lid.gameObject.SetActive(false);
        }
        else if (blocksPath)
        {
            foreach (var col in GetComponentsInChildren<Collider>(true))
                col.enabled = false;
            transform.Rotate(0f, 80f, 0f, Space.Self);
        }
        else
        {
            if (obstacle != null) obstacle.enabled = false;
            transform.localScale = Vector3.Scale(transform.localScale, new Vector3(1f, 0.15f, 1f));
            transform.position += Vector3.down * 0.6f;
        }
        var level = FindFirstObjectByType<HeistLevelBuilder>();
        if (level != null) level.BakeNavMesh();
    }

    public void Fail(HeistOperative operative)
    {
        challenge.passed = false;
        challenge.actorId = operative.Member.id;
        challenge.actorName = operative.Member.name;
        challenge.narration = $"{operative.Member.name} failed {challenge.name}.";
        HeistPrims.Paint(gameObject, new Color(0.75f, 0.2f, 0.22f));
    }
}
