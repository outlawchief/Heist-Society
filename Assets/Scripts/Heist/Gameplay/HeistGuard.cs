using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

public class HeistGuard : MonoBehaviour
{
    public Vector3 home;
    public Transform chase;
    public float speed = 2.6f;
    public float sightRange = 9f;
    public float sightHalfAngle = 55f;
    public const float NoticeNeed = 1.7f;
    public bool downed;
    public float Health;
    public float MaxHealth;
    NavMeshAgent agent;
    float attackCooldown;
    float stunRemaining;
    float staggerRemaining;
    float dwell;
    float stuck;
    float notice;
    Vector3 lastPos;
    int waypointIndex;
    Vector3[] waypoints = System.Array.Empty<Vector3>();
    Vector3? investigate;
    HeistGameSession session;
    Color bodyColor = new Color(0.55f, 0.15f, 0.18f);

    public void Setup(HeistGameSession game, Vector3 spawn, float moveSpeed, Vector3[] route, float maxHealth)
    {
        session = game;
        speed = moveSpeed;
        MaxHealth = maxHealth;
        Health = maxHealth;
        dwell = 0.5f;
        var col = GetComponent<CapsuleCollider>();
        if (col != null) Destroy(col);
        var cc = GetComponent<CharacterController>();
        if (cc != null) Destroy(cc);
        agent = gameObject.GetComponent<NavMeshAgent>();
        if (agent == null) agent = gameObject.AddComponent<NavMeshAgent>();
        agent.radius = 0.28f;
        agent.height = 1.6f;
        agent.speed = moveSpeed;
        agent.acceleration = 14f;
        agent.angularSpeed = 220f;
        agent.stoppingDistance = 0.28f;
        agent.autoBraking = true;
        agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
        waypoints = SnapRoute(route, spawn);
        home = waypoints.Length > 0 ? waypoints[0] : Snap(spawn);
        if (agent.isOnNavMesh) agent.Warp(home);
        else transform.position = home;
        HeistPrims.Paint(gameObject, bodyColor);
        var nameLabel = HeistPrims.Label(transform, transform.position + Vector3.up * 1.4f, "GUARD", 0.045f);
        nameLabel.transform.localPosition = new Vector3(0f, 1.4f, 0f);
        var vision = gameObject.AddComponent<HeistGuardVision>();
        vision.Setup(this, sightRange, sightHalfAngle);
        DrawPatrol(waypoints);
        lastPos = transform.position;
        GoTo(PatrolDestination());
    }

    public void SetupRemote(HeistGameSession game, Vector3 spawn)
    {
        session = game;
        MaxHealth = 40f;
        Health = 40f;
        home = spawn;
        transform.position = spawn;
        HeistPrims.Paint(gameObject, bodyColor);
        var nameLabel = HeistPrims.Label(transform, transform.position + Vector3.up * 1.4f, "GUARD", 0.045f);
        nameLabel.transform.localPosition = new Vector3(0f, 1.4f, 0f);
        var vision = gameObject.AddComponent<HeistGuardVision>();
        vision.Setup(this, sightRange, sightHalfAngle);
        agent = gameObject.GetComponent<NavMeshAgent>();
        if (agent != null) agent.enabled = false;
    }

    public void ApplyRemote(float x, float z, float yaw, float hp, bool isDown)
    {
        Vector3 pos = new Vector3(x, transform.position.y, z);
        transform.position = Vector3.Lerp(transform.position, pos, 0.65f);
        Health = hp;
        if (isDown)
        {
            if (!downed) Die();
            return;
        }
        float t = MaxHealth <= 0f ? 0f : 1f - Health / Mathf.Max(1f, MaxHealth);
        HeistPrims.Paint(gameObject, Color.Lerp(bodyColor, new Color(0.15f, 0.05f, 0.06f), t));
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(0f, yaw, 0f), 0.65f);
    }

    public bool Suspecting => chase == null && notice > 0.05f;

    public void Stun(float seconds)
    {
        stunRemaining = Mathf.Max(stunRemaining, seconds);
        attackCooldown = Mathf.Max(attackCooldown, seconds);
        chase = null;
        notice = 0f;
        if (agent != null && agent.isOnNavMesh) agent.isStopped = true;
    }

    public void Investigate(Vector3 point, float seconds)
    {
        Stun(Mathf.Min(0.45f, seconds));
        investigate = Snap(point);
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
            notice = NoticeNeed;
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
        if (agent != null) agent.enabled = false;
        var vision = GetComponent<HeistGuardVision>();
        if (vision != null) vision.enabled = false;
        transform.rotation = Quaternion.Euler(80f, transform.eulerAngles.y, 0f);
    }

    void Update()
    {
        if (downed || session == null || session.Ended || !session.IsHost) return;
        attackCooldown -= Time.deltaTime;
        stunRemaining -= Time.deltaTime;
        staggerRemaining -= Time.deltaTime;
        if (stunRemaining > 0f) return;
        if (agent != null && agent.enabled && !agent.isOnNavMesh)
            agent.Warp(Snap(transform.position));
        if (agent != null && agent.enabled && agent.isOnNavMesh) agent.isStopped = staggerRemaining > 0f;

        HeistOperative nearest = session.NearestStanding(transform.position, 11f);
        bool sees = nearest != null && CanSee(nearest.transform.position);
        if (sees)
        {
            if (chase != null)
            {
                chase = nearest.transform;
                investigate = null;
                session.Heat.Add(3.5f * Time.deltaTime, "spotted");
            }
            else
            {
                float need = NoticeTime(nearest);
                notice += Time.deltaTime;
                if (need <= 0f || notice >= need)
                    Recognize(nearest);
            }
        }
        else if (chase == null)
            notice = Mathf.Max(0f, notice - Time.deltaTime * 1.4f);

        Vector3 dest = PatrolDestination();
        Vector3 to = dest - transform.position;
        to.y = 0f;
        bool inMelee = chase != null && to.magnitude < 1.2f;
        if (agent != null)
        {
            agent.speed = chase != null ? speed : speed * 0.72f;
            agent.stoppingDistance = chase != null ? 1.05f : 0.28f;
        }

        if (!inMelee && staggerRemaining <= 0f)
            GoTo(dest);
        else if (agent != null && agent.isOnNavMesh)
            agent.ResetPath();

        if (inMelee && attackCooldown <= 0f && chase != null)
        {
            var op = chase.GetComponent<HeistOperative>();
            if (op != null && !op.downed)
            {
                float dmg = 6f + (session.Launch != null ? session.Launch.difficulty : 1);
                op.TakeDamage(dmg, "guard strike");
                HeistAudio.PlayGuardPunch();
                session.Heat.Add(3f, "melee fight");
                attackCooldown = 1.05f;
            }
        }

        if (chase == null)
        {
            FaceNextWaypoint();
            if (Arrived(dest) || IsStuck())
                AdvancePatrol();
        }

        if (chase != null && Vector3.Distance(transform.position, chase.position) > 14f)
        {
            chase = null;
            notice = 0f;
        }

        lastPos = transform.position;
    }

    void GoTo(Vector3 dest)
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh) return;
        dest = Snap(dest);
        if (!agent.hasPath || (agent.destination - dest).sqrMagnitude > 0.2f)
            agent.SetDestination(dest);
    }

    bool Arrived(Vector3 dest)
    {
        Vector3 to = dest - transform.position;
        to.y = 0f;
        if (to.magnitude <= 0.45f) return true;
        return agent != null && agent.isOnNavMesh && !agent.pathPending && agent.remainingDistance <= 0.45f;
    }

    bool IsStuck()
    {
        float moved = Vector3.Distance(transform.position, lastPos);
        bool wantsMove = agent != null && agent.isOnNavMesh && agent.hasPath && agent.remainingDistance > 0.5f;
        if (wantsMove && moved < 0.02f)
            stuck += Time.deltaTime;
        else
            stuck = 0f;
        if (stuck < 1f) return false;
        stuck = 0f;
        return true;
    }

    Vector3 PatrolDestination()
    {
        if (chase != null) return Snap(chase.position);
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
        GoTo(PatrolDestination());
    }

    void FaceNextWaypoint()
    {
        if (investigate.HasValue || waypoints.Length < 2) return;
        if (agent != null && agent.velocity.sqrMagnitude > 0.04f) return;
        int next = (waypointIndex + 1) % waypoints.Length;
        Vector3 dir = waypoints[next] - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.01f) return;
        transform.forward = Vector3.Slerp(transform.forward, dir.normalized, Time.deltaTime * 2.4f);
    }

    float NoticeTime(HeistOperative op)
    {
        if (session.Heat != null && session.Heat.LockdownActive) return 0f;
        int cha = 1;
        if (op != null && op.Member != null && op.Member.stats != null)
            cha = Mathf.Clamp(op.Member.stats.cha, 1, 10);
        float charm = 0.45f + cha * 0.25f;
        float heat = session.Heat != null ? session.Heat.Value : 0f;
        float pressure = Mathf.Lerp(1f, 0.2f, Mathf.Clamp01(heat / 100f));
        return charm * pressure;
    }

    void Recognize(HeistOperative op)
    {
        notice = NoticeNeed;
        chase = op.transform;
        investigate = null;
        string name = op.Member != null ? op.Member.name : "an operative";
        session.SetCaption($"Guard recognizes {name}.");
        session.Heat.Add(3.5f * Time.deltaTime, "spotted");
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

    Vector3[] SnapRoute(Vector3[] route, Vector3 spawn)
    {
        if (route == null || route.Length == 0)
            return new[] { Snap(spawn) };
        var snapped = new Vector3[route.Length];
        for (int i = 0; i < route.Length; i++)
            snapped[i] = Snap(route[i] + Vector3.up * 0.1f);
        return snapped;
    }

    Vector3 Snap(Vector3 point)
    {
        if (session != null && session.Level != null)
            return session.Level.SnapToNav(point);
        if (NavMesh.SamplePosition(point, out NavMeshHit hit, 2.4f, NavMesh.AllAreas))
            return hit.position;
        return point;
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
