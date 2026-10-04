using UnityEngine;

public class HeistGuardVision : MonoBehaviour
{
    static readonly Color Idle = new Color(1f, 0.82f, 0.22f, 0.28f);
    static readonly Color Suspect = new Color(0.95f, 0.55f, 0.12f, 0.34f);
    static readonly Color Alert = new Color(0.95f, 0.12f, 0.1f, 0.38f);

    HeistGuard guard;
    HeistVisionArc arc;
    Color painted;

    public void Setup(HeistGuard owner, float sightRange, float sightHalfAngle)
    {
        guard = owner;
        arc = HeistVisionArc.Create(transform, new Vector3(0f, -0.62f, 0f), sightRange, sightHalfAngle, Idle);
        painted = Idle;
    }

    void LateUpdate()
    {
        if (arc == null || guard == null) return;
        if (guard.downed)
        {
            arc.SetShown(false);
            return;
        }
        arc.SetShown(true);
        Color next = guard.chase != null ? Alert : guard.Suspecting ? Suspect : Idle;
        if (next == painted) return;
        painted = next;
        arc.SetColor(next);
    }
}
