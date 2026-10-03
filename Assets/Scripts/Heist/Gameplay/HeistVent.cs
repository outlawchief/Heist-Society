using System.Collections.Generic;
using UnityEngine;

public class HeistVent : MonoBehaviour
{
    public const int AgilityNeed = 4;
    public string roomName;
    public int roomIndex;
    public bool revealed;
    public readonly List<HeistVent> Destinations = new List<HeistVent>();
    public Vector3 exitPoint;
    public Vector3 lookPoint;

    HeistGameSession session;
    Renderer[] renderers;
    TextMesh label;

    public void Setup(HeistGameSession game)
    {
        session = game;
        Vector3 inward = lookPoint - transform.position;
        inward.y = 0f;
        if (inward.sqrMagnitude < 0.01f) inward = Vector3.forward;
        exitPoint = transform.position + inward.normalized * 1.35f;
        exitPoint.y = lookPoint.y - 0.5f;
        renderers = GetComponentsInChildren<Renderer>(true);
        label = GetComponentInChildren<TextMesh>(true);
        foreach (var col in GetComponentsInChildren<Collider>())
            col.isTrigger = true;
        SetVisible(false);
    }

    public static float SpotRange(HeistOperative op) => HeistSight.PerceptionRange(op);

    public bool CanEnter(HeistOperative op)
    {
        return op != null && op.Member.stats.agi >= AgilityNeed;
    }

    public void Reveal()
    {
        if (revealed) return;
        revealed = true;
        SetVisible(true);
    }

    void Update()
    {
        if (revealed || session == null) return;
        foreach (var op in session.Operatives)
        {
            if (op == null || op.downed || op.inVent) continue;
            if (!CanBeSeenBy(op)) continue;
            Reveal();
            session.SetCaption($"{op.Member.name} finds a vent.");
            return;
        }
    }

    bool CanBeSeenBy(HeistOperative op) => HeistSight.Notices(op, transform.position, transform);

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
