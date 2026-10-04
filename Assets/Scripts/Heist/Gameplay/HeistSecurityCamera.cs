using UnityEngine;

public class HeistSecurityCamera : MonoBehaviour
{
    public float watchRange = 6.4f;
    public float watchHalfAngle = 42f;
    public bool revealed;
    public bool jammed;
    public bool tracking;
    public const float NoticeNeed = 1.7f;
    public const float HeatPerSecond = 4.5f;

    static readonly Color IdleArc = new Color(0.25f, 0.72f, 0.95f, 0.28f);
    static readonly Color SuspectArc = new Color(0.95f, 0.72f, 0.18f, 0.34f);
    static readonly Color AlertArc = new Color(0.95f, 0.18f, 0.14f, 0.38f);
    static readonly Color JammedArc = new Color(0.35f, 0.38f, 0.4f, 0.16f);

    HeistGameSession session;
    Renderer[] renderers;
    TextMesh label;
    HeistVisionArc vision;
    float notice;
    string lastSpot;
    bool intelDark;

    public void Setup(HeistGameSession game)
    {
        session = game;
        renderers = GetComponentsInChildren<Renderer>(true);
        label = GetComponentInChildren<TextMesh>(true);
        vision = HeistVisionArc.Create(transform, new Vector3(0f, -2.07f, 0f), watchRange, watchHalfAngle, IdleArc);
        vision.SetShown(false);
        SetVisible(false);
        foreach (var col in GetComponentsInChildren<Collider>())
            col.isTrigger = true;
    }

    public static float SpotRange(HeistOperative op) => HeistSight.PerceptionRange(op);

    public bool Watches(Vector3 point)
    {
        if (jammed) return false;
        return ConeSees(transform.position, transform.forward, point, watchRange, watchHalfAngle);
    }

    public static bool ConeSees(Vector3 origin, Vector3 forward, Vector3 point, float range, float halfAngle)
    {
        Vector3 flat = point - origin;
        flat.y = 0f;
        if (flat.magnitude > range) return false;
        if (Vector3.Angle(forward, flat) >= halfAngle) return false;
        return HeistSight.Clear(origin, point, null, 0f, 0.85f);
    }

    public void Reveal()
    {
        if (revealed) return;
        revealed = true;
        SetVisible(true);
        if (vision != null) vision.SetShown(true);
    }

    public void Jam()
    {
        jammed = true;
        tracking = false;
        notice = 0f;
        if (!revealed) Reveal();
        HeistPrims.Paint(gameObject, new Color(0.18f, 0.2f, 0.22f));
        foreach (var r in renderers)
        {
            if (r != null) HeistPrims.Paint(r.gameObject, new Color(0.18f, 0.2f, 0.22f));
        }
        if (label != null) label.text = "JAMMED";
        if (vision != null)
        {
            vision.SetShown(true);
            vision.SetColor(JammedArc);
        }
    }

    void LateUpdate()
    {
        if (vision == null || !revealed) return;
        if (jammed || intelDark)
        {
            vision.SetColor(JammedArc);
            return;
        }
        if (tracking) vision.SetColor(AlertArc);
        else if (notice > 0.05f) vision.SetColor(SuspectArc);
        else vision.SetColor(IdleArc);
    }

    void Update()
    {
        if (session == null) return;
        bool dark = jammed || IntelDark();
        if (dark && !intelDark && !jammed)
            session.SetCaption("A nearby camera goes dark.");
        intelDark = dark && !jammed;
        if (dark)
        {
            notice = 0f;
            tracking = false;
        }
        else
            TickWatch();
        if (revealed) return;
        foreach (var op in session.Operatives)
        {
            if (op == null || op.downed || op.inVent) continue;
            if (!CanBeSeenBy(op)) continue;
            Reveal();
            session.SetCaption($"{op.Member.name} spots a security camera.");
            return;
        }
    }

    void TickWatch()
    {
        HeistOperative seen = null;
        if (session.Operatives != null)
        {
            foreach (var op in session.Operatives)
            {
                if (op == null || op.downed || op.inVent) continue;
                if (!Watches(op.transform.position)) continue;
                seen = op;
                break;
            }
        }

        if (seen == null)
        {
            notice = Mathf.Max(0f, notice - Time.deltaTime * 1.4f);
            if (notice <= 0.05f) tracking = false;
            return;
        }

        lastSpot = seen.Member != null ? seen.Member.name : "an operative";
        if (tracking)
        {
            if (session.IsHost) session.Heat.Add(HeatPerSecond * Time.deltaTime, "camera lock");
            return;
        }

        notice += Time.deltaTime;
        if (notice < NoticeNeed) return;
        tracking = true;
        if (!revealed) Reveal();
        session.SetCaption($"Camera locked onto {lastSpot}.");
    }

    bool CanBeSeenBy(HeistOperative op) => HeistSight.Notices(op, transform.position, transform);

    bool IntelDark()
    {
        if (session.Operatives == null) return false;
        foreach (var op in session.Operatives)
        {
            if (op == null || op.downed || op.inVent) continue;
            float range = HeistSight.IntelligenceRange(op);
            if (range <= 0f) continue;
            if (Vector3.Distance(op.transform.position, transform.position) <= range)
                return true;
        }
        return false;
    }

    void SetVisible(bool on)
    {
        if (renderers != null)
        {
            foreach (var r in renderers)
            {
                if (r != null) r.enabled = on;
            }
        }
        if (label != null) label.gameObject.SetActive(on);
    }
}
