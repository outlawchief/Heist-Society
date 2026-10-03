using UnityEngine;

public class HeistSecurityCamera : MonoBehaviour
{
    public float watchRange = 6.4f;
    public float watchHalfAngle = 42f;
    public bool revealed;
    public bool jammed;
    static readonly Color IdleArc = new Color(0.25f, 0.72f, 0.95f, 0.28f);
    static readonly Color AlertArc = new Color(0.95f, 0.18f, 0.14f, 0.38f);
    static readonly Color JammedArc = new Color(0.35f, 0.38f, 0.4f, 0.16f);

    HeistGameSession session;
    Renderer[] renderers;
    TextMesh label;
    HeistVisionArc vision;

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

    public static float SpotRange(HeistOperative op)
    {
        if (op == null || op.Member == null) return 2.5f;
        return 2.2f + op.Member.stats.per * 0.7f;
    }

    public bool Watches(Vector3 point)
    {
        if (jammed) return false;
        Vector3 flat = point - transform.position;
        flat.y = 0f;
        if (flat.magnitude > watchRange) return false;
        return Vector3.Angle(transform.forward, flat) < watchHalfAngle;
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
        if (jammed)
        {
            vision.SetColor(JammedArc);
            return;
        }
        bool watching = false;
        if (session != null)
        {
            foreach (var op in session.Operatives)
            {
                if (op == null || op.downed) continue;
                if (Watches(op.transform.position))
                {
                    watching = true;
                    break;
                }
            }
        }
        vision.SetColor(watching ? AlertArc : IdleArc);
    }

    void Update()
    {
        if (revealed || jammed || session == null) return;
        foreach (var op in session.Operatives)
        {
            if (op == null || op.downed) continue;
            if (!CanBeSeenBy(op)) continue;
            Reveal();
            session.SetCaption($"{op.Member.name} spots a security camera.");
            return;
        }
    }

    bool CanBeSeenBy(HeistOperative op)
    {
        Vector3 toCam = transform.position - op.transform.position;
        float dist = toCam.magnitude;
        if (dist > SpotRange(op)) return false;
        toCam.y = 0f;
        if (toCam.sqrMagnitude < 0.01f) return true;
        return Vector3.Angle(op.transform.forward, toCam) < 70f;
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
