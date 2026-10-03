using UnityEngine;

public class HeistGuard : MonoBehaviour
{
    public Vector3 home;
    public Transform chase;
    public float speed = 2.6f;
    CharacterController controller;
    float attackCooldown;
    HeistGameSession session;

    public void Setup(HeistGameSession game, Vector3 spawn, float moveSpeed)
    {
        session = game;
        home = spawn;
        speed = moveSpeed;
        var col = GetComponent<CapsuleCollider>();
        if (col != null) Destroy(col);
        controller = gameObject.AddComponent<CharacterController>();
        controller.height = 1.6f;
        controller.radius = 0.32f;
        controller.center = new Vector3(0f, 0.2f, 0f);
        HeistPrims.Paint(gameObject, new Color(0.55f, 0.15f, 0.18f));
        var nameLabel = HeistPrims.Label(transform, transform.position + Vector3.up * 1.4f, "GUARD", 0.045f);
        nameLabel.transform.localPosition = new Vector3(0f, 1.4f, 0f);
    }

    public void Stun(float seconds)
    {
        attackCooldown = Mathf.Max(attackCooldown, seconds);
        chase = null;
    }

    void Update()
    {
        if (session == null || session.Ended) return;
        attackCooldown -= Time.deltaTime;
        HeistOperative nearest = session.NearestStanding(transform.position, 11f);
        if (nearest != null && CanSee(nearest.transform.position))
        {
            chase = nearest.transform;
            session.Heat.Add(3.5f * Time.deltaTime, "spotted");
        }

        Vector3 dest = chase != null ? chase.position : home;
        Vector3 to = dest - transform.position;
        to.y = 0f;
        if (to.magnitude > 0.4f)
        {
            controller.Move(to.normalized * speed * Time.deltaTime + Vector3.down * 4f * Time.deltaTime);
            transform.forward = to.normalized;
        }

        if (chase != null && to.magnitude < 1.15f && attackCooldown <= 0f)
        {
            var op = chase.GetComponent<HeistOperative>();
            if (op != null && !op.downed)
            {
                op.Down("captured by a guard");
                session.Heat.Add(12f, "operative captured");
                attackCooldown = 1.2f;
            }
        }

        if (chase != null && Vector3.Distance(transform.position, chase.position) > 14f)
            chase = null;
    }

    bool CanSee(Vector3 point)
    {
        Vector3 flat = point - transform.position;
        flat.y = 0f;
        if (flat.magnitude > 9f) return false;
        float angle = Vector3.Angle(transform.forward, flat);
        return angle < 55f;
    }
}
