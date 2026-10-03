using UnityEngine;

public class HeistHeatDirector : MonoBehaviour
{
    public float Value;
    public int Notch;
    public int Lockdowns;
    public bool LockdownActive;
    float lockdownTimer;
    HeistGameSession session;

    public void Setup(HeistGameSession game)
    {
        session = game;
        Value = 0f;
        Notch = 0;
    }

    public void Add(float amount, string reason)
    {
        if (session == null || session.Ended) return;
        if (!session.IsHost) return;
        Value = Mathf.Clamp(Value + amount, 0f, 100f);
        int next = Mathf.FloorToInt(Value / 25f);
        if (next > Notch)
        {
            Notch = next;
            session.SpawnGuardWave(Notch);
            session.SetCaption($"Heat notch {Notch}. More guards inbound.");
        }

        if (Value >= 100f && !LockdownActive)
        {
            LockdownActive = true;
            lockdownTimer = 8f;
            Lockdowns++;
            session.SetCaption("LOCKDOWN. Cameras stay hot.");
            if (Lockdowns >= 3) session.FailHeist("repeated lockdowns");
        }
    }

    public bool InCameraView(Vector3 point)
    {
        foreach (var interactable in session.Level.Interactables)
        {
            if (interactable.completed) continue;
            if (interactable.challenge == null) continue;
            if (interactable.challenge.type != "cameras") continue;
            if (Vector3.Distance(point, interactable.transform.position) < 5.5f) return true;
        }
        return LockdownActive;
    }

    void Update()
    {
        if (session == null || !session.IsHost) return;
        if (LockdownActive)
        {
            lockdownTimer -= Time.deltaTime;
            if (lockdownTimer <= 0f)
            {
                LockdownActive = false;
                Value = 82f;
            }
        }
        else
        {
            Value = Mathf.Max(0f, Value - Time.deltaTime * 0.35f);
        }
    }
}
