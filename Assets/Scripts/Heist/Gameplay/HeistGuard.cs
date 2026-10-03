using UnityEngine;
using UnityEngine.Rendering;

public class HeistGuard : MonoBehaviour
{
    public Vector3 home;
    public Transform chase;
    public float speed = 2.6f;
    public float sightRange = 9f;
    public float sightHalfAngle = 55f;
    public bool downed;
    public float Health;
    public float MaxHealth;
    CharacterController controller;
    float attackCooldown;
    float stunRemaining;
    float staggerRemaining;
    float dwell;
    int waypointIndex;
    Vector3[] waypoints = System.Array.Empty<Vector3>();
    Vector3? investigate;
    HeistGameSession session;
    Color bodyColor = new Color(0.55f, 0.15f, 0.18f);

    public void Setup(HeistGameSession game, Vector3 spawn, float moveSpeed, Vector3[] route, float maxHealth)
    {
        session = game;
        home = spawn;
        speed = moveSpeed;
        MaxHealth = maxHealth;
        Health = maxHealth;
        waypoints = route != null && route.Length > 0 ? route : new[] { spawn };
        dwell = 0.5f;
        var col = GetComponent<CapsuleCollider>();
        if (col != null) Destroy(col);
        controller = gameObject.AddComponent<CharacterController>();
        controller.height = 1.6f;
        controller.radius = 0.32f;
        controller.center = new Vector3(0f, 0.2f, 0f);
        HeistPrims.Paint(gameObject, bodyColor);
        var nameLabel = HeistPrims.Label(transform, transform.position + Vector3.up * 1.4f, "GUARD", 0.045f);
        nameLabel.transform.localPosition = new Vector3(0f, 1.4f, 0f);
        var vision = gameObject.AddComponent<HeistGuardVision>();
        vision.Setup(this, sightRange, sightHalfAngle);
        DrawPatrol(waypoints);
    }

    public void Stun(float seconds)
    {
        stunRemaining = Mathf.Max(stunRemaining, seconds);
        attackCooldown = Mathf.Max(attackCooldown, seconds);
        chase = null;
    }

    public void Investigate(Vector3 point, float seconds)
    {
        Stun(Mathf.Min(0.45f, seconds));
        investigate = point;
    }

    public bool TakeHit(float damage, Transform attacker)
    {
        if (downed) return false;
        Health = Mathf.Max(0f, Health - damage);
        stunRemaining = 0f;
        staggerRemaining = 0.08f;
        if (attacker != null)
        {
            chase = attacker;
            investigate = null;
            Vector3 dir = attacker.position - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.01f) transform.forward = dir.normalized;
        }
        float t = MaxHealth <= 0f ? 1f : 1f - Health / MaxHealth;
        HeistPrims.Paint(gameObject, Color.Lerp(bodyColor, new Color(0.15f, 0.05f, 0.06f), t));
        if (Health <= 0f)
        {
            Die();
            return true;
        }
        return false;
    }

    void Die()
    {
        downed = true;
        chase = null;
        HeistPrims.Paint(gameObject, new Color(0.18f, 0.12f, 0.12f));
        if (controller != null) controller.enabled = false;
        var vision = GetComponent<HeistGuardVision>();
        if (vision != null) vision.enabled = false;
        transform.rotation = Quaternion.Euler(80f, transform.eulerAngles.y, 0f);
    }

    void Update()
    {
        if (downed || session == null || session.Ended) return;
        attackCooldown -= Time.deltaTime;
        stunRemaining -= Time.deltaTime;
        staggerRemaining -= Time.deltaTime;
        if (stunRemaining > 0f) return;

        HeistOperative nearest = session.NearestStanding(transform.position, 11f);
        if (nearest != null && CanSee(nearest.transform.position))
        {
            chase = nearest.transform;
            investigate = null;
            session.Heat.Add(3.5f * Time.deltaTime, "spotted");
        }

        Vector3 dest = PatrolDestination();
        Vector3 to = dest - transform.position;
        to.y = 0f;
        bool inMelee = chase != null && to.magnitude < 1.2f;
        float moveSpeed = chase != null ? speed : speed * 0.72f;
        if (!inMelee && to.magnitude > 0.35f && staggerRemaining <= 0f)
        {
            controller.Move(to.normalized * moveSpeed * Time.deltaTime + Vector3.down * 4f * Time.deltaTime);
            transform.forward = to.normalized;
        }
        else
        {
            controller.Move(Vector3.down * 4f * Time.deltaTime);
            if (to.sqrMagnitude > 0.01f) transform.forward = to.normalized;
            if (chase == null)
            {
                FaceNextWaypoint();
                AdvancePatrol();
            }
        }

        if (inMelee && attackCooldown <= 0f)
        {
            var op = chase.GetComponent<HeistOperative>();
            if (op != null && !op.downed)
            {
                float dmg = 6f + (session.Launch != null ? session.Launch.difficulty : 1);
                op.TakeDamage(dmg, "guard strike");
                session.Heat.Add(3f, "melee fight");
                attackCooldown = 1.05f;
            }
        }

        if (chase != null && Vector3.Distance(transform.position, chase.position) > 14f)
            chase = null;
    }

    Vector3 PatrolDestination()
    {
        if (chase != null) return chase.position;
        if (investigate.HasValue) return investigate.Value;
        if (waypoints.Length == 0) return home;
        return waypoints[waypointIndex];
    }

    void AdvancePatrol()
    {
        if (investigate.HasValue)
        {
            investigate = null;
            dwell = 0.8f;
            return;
        }

        dwell -= Time.deltaTime;
        if (dwell > 0f || waypoints.Length == 0) return;
        waypointIndex = (waypointIndex + 1) % waypoints.Length;
        dwell = 1.15f;
    }

    void FaceNextWaypoint()
    {
        if (investigate.HasValue || waypoints.Length < 2) return;
        int next = (waypointIndex + 1) % waypoints.Length;
        Vector3 dir = waypoints[next] - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.01f) return;
        transform.forward = Vector3.Slerp(transform.forward, dir.normalized, Time.deltaTime * 2.4f);
    }

    bool CanSee(Vector3 point)
    {
        Vector3 flat = point - transform.position;
        flat.y = 0f;
        if (flat.magnitude > sightRange) return false;
        float angle = Vector3.Angle(transform.forward, flat);
        if (angle >= sightHalfAngle) return false;
        return HeistSight.Clear(transform.position, point);
    }

    void DrawPatrol(Vector3[] route)
    {
        if (route == null || route.Length < 2) return;
        var go = new GameObject("PatrolRoute");
        go.transform.SetParent(transform.parent, false);
        var line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.loop = true;
        line.widthMultiplier = 0.05f;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
        var shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
        var mat = new Material(shader != null ? shader : Shader.Find("Standard"));
        var color = new Color(0.75f, 0.22f, 0.22f, 0.45f);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
        line.material = mat;
        line.startColor = color;
        line.endColor = color;
        line.positionCount = route.Length;
        for (int i = 0; i < route.Length; i++)
            line.SetPosition(i, route[i] + Vector3.up * 0.08f);
    }
}
