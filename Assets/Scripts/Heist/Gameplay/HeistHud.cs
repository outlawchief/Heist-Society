using UnityEngine;

public class HeistHud : MonoBehaviour
{
    HeistGameSession session;

    void Awake() => session = GetComponent<HeistGameSession>();

    void OnGUI()
    {
        if (session == null) return;
        var op = session.LocalOperative;
        GUI.skin.label.fontSize = 16;
        GUI.Label(new Rect(18, 12, 900, 28), session.Caption);
        if (!string.IsNullOrEmpty(session.JoinCode))
            GUI.Label(new Rect(18, 36, 500, 24), "Join code: " + session.JoinCode);

        float heat = session.Heat != null ? session.Heat.Value : 0f;
        GUI.Box(new Rect(18, 64, 220, 18), "");
        GUI.Box(new Rect(18, 64, 220f * (heat / 100f), 18), "");
        GUI.Label(new Rect(18, 82, 260, 22), $"HEAT {heat:0}   lockdowns {session.Heat?.Lockdowns ?? 0}");

        if (op != null)
        {
            GUI.Label(new Rect(18, 108, 720, 24), op.prompt);
            if (op.interactFill > 0f)
            {
                GUI.Box(new Rect(18, 132, 200, 14), "");
                GUI.Box(new Rect(18, 132, 200f * op.interactFill, 14), " ");
            }
            GUI.Label(new Rect(18, 152, 720, 40),
                $"{op.Member.name}  STR{op.Member.stats.str} AGI{op.Member.stats.agi} INT{op.Member.stats.intel} DEX{op.Member.stats.dex} CHA{op.Member.stats.cha} PER{op.Member.stats.per}  {op.Member.gear}");
        }
    }
}
