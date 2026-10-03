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
        if (blocksPath && obstacle != null) obstacle.enabled = false;
        transform.localScale = Vector3.Scale(transform.localScale, new Vector3(1f, 0.15f, 1f));
        transform.position += Vector3.down * 0.6f;
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
