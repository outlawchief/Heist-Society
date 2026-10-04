using UnityEngine;

public static class HeistSight
{
    const float Skin = 0.22f;

    public static float PerceptionRange(HeistOperative op)
    {
        if (op == null || op.Member == null) return 3.2f;
        return 3.2f + op.Member.stats.per * 0.85f;
    }

    public static float IntelligenceRange(HeistOperative op)
    {
        if (op == null || op.Member == null || op.Member.stats == null) return 0f;
        if (op.Member.stats.intel < 3) return 0f;
        return 3.2f + op.Member.stats.intel * 0.85f;
    }

    public static bool Notices(HeistOperative op, Vector3 worldPoint, Transform target = null)
    {
        if (op == null || op.downed || op.inVent) return false;
        if (Vector3.Distance(op.transform.position, worldPoint) > PerceptionRange(op)) return false;
        return Clear(op.transform.position, worldPoint, target, 0.85f, 0f);
    }

    public static bool Clear(Vector3 from, Vector3 to, Transform target = null, float fromLift = 0.85f, float toLift = 0.85f)
    {
        Vector3 origin = from + Vector3.up * fromLift;
        Vector3 dest = to + Vector3.up * toLift;
        Vector3 delta = dest - origin;
        float dist = delta.magnitude;
        if (dist < 0.05f) return true;
        Vector3 dir = delta / dist;
        origin += dir * Skin;
        if (!Physics.Linecast(origin, dest, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore))
            return true;
        if (target != null && (hit.transform == target || hit.transform.IsChildOf(target)))
            return true;
        return (hit.point - dest).sqrMagnitude < 0.45f;
    }

    public static float Range(Vector3 origin, Vector3 direction, float maxRange)
    {
        if (maxRange <= 0.01f) return 0f;
        Vector3 start = origin + direction.normalized * 0.2f;
        if (Physics.Raycast(start, direction, out RaycastHit hit, maxRange, ~0, QueryTriggerInteraction.Ignore))
            return Mathf.Max(0.15f, hit.distance);
        return maxRange;
    }
}
