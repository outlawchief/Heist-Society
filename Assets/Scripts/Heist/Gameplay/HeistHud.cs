using UnityEngine;

public class HeistHud : MonoBehaviour
{
    HeistGameSession session;
    GUIStyle titleStyle;
    GUIStyle subtitleStyle;
    GUIStyle bodyStyle;
    GUIStyle moneyStyle;
    GUIStyle dimStyle;
    Texture2D panelTex;
    Texture2D accentTex;

    void Awake() => session = GetComponent<HeistGameSession>();

    void EnsureStyles()
    {
        if (titleStyle != null) return;
        titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 28,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            wordWrap = true
        };
        titleStyle.normal.textColor = new Color(0.96f, 0.93f, 0.82f);
        subtitleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 16,
            alignment = TextAnchor.MiddleCenter,
            wordWrap = true
        };
        subtitleStyle.normal.textColor = new Color(0.78f, 0.74f, 0.64f);
        bodyStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 15,
            alignment = TextAnchor.MiddleLeft,
            wordWrap = true
        };
        bodyStyle.normal.textColor = new Color(0.9f, 0.88f, 0.82f);
        moneyStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 22,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleRight
        };
        moneyStyle.normal.textColor = new Color(0.93f, 0.78f, 0.28f);
        dimStyle = new GUIStyle(bodyStyle);
        dimStyle.normal.textColor = new Color(0.62f, 0.6f, 0.54f);
        panelTex = Pixel(new Color(0.07f, 0.075f, 0.08f, 0.94f));
        accentTex = Pixel(new Color(0.72f, 0.58f, 0.22f, 1f));
    }

    static Texture2D Pixel(Color color)
    {
        var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.SetPixel(0, 0, color);
        tex.Apply();
        return tex;
    }

    void OnGUI()
    {
        if (session == null) return;
        EnsureStyles();
        if (session.Ended && session.Result != null)
        {
            DrawDebrief();
            return;
        }
        DrawPlayHud();
    }

    void DrawPlayHud()
    {
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
            float hpT = op.MaxHealth <= 0f ? 0f : op.Health / op.MaxHealth;
            GUI.Box(new Rect(18, 108, 220, 18), "");
            GUI.Box(new Rect(18, 108, 220f * hpT, 18), "");
            GUI.Label(new Rect(18, 126, 320, 22), $"HP {op.Health:0}/{op.MaxHealth:0}   LVL {Mathf.Max(1, op.Member.level)}");
            GUI.Label(new Rect(18, 148, 720, 24), op.prompt);
            if (op.interactFill > 0f)
            {
                GUI.Box(new Rect(18, 172, 200, 14), "");
                GUI.Box(new Rect(18, 172, 200f * op.interactFill, 14), " ");
            }
            GUI.Label(new Rect(18, 192, 720, 40),
                $"{op.Member.name}  STR{op.Member.stats.str} AGI{op.Member.stats.agi} INT{op.Member.stats.intel} DEX{op.Member.stats.dex} CHA{op.Member.stats.cha} PER{op.Member.stats.per}  {op.Member.gear}");
        }
    }

    void DrawDebrief()
    {
        var result = session.Result;
        GUI.color = new Color(0f, 0f, 0f, 0.72f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;

        float w = Mathf.Min(760f, Screen.width - 48f);
        float h = Mathf.Min(620f, Screen.height - 40f);
        var panel = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
        GUI.DrawTexture(panel, panelTex);
        GUI.DrawTexture(new Rect(panel.x, panel.y, panel.width, 6f), accentTex);

        float y = panel.y + 22f;
        titleStyle.normal.textColor = result.success
            ? new Color(0.78f, 0.9f, 0.55f)
            : new Color(0.92f, 0.42f, 0.38f);
        GUI.Label(new Rect(panel.x + 24f, y, panel.width - 48f, 36f),
            result.success ? "SCORE SECURED" : "HEIST COLLAPSED", titleStyle);
        y += 36f;
        GUI.Label(new Rect(panel.x + 24f, y, panel.width - 48f, 24f),
            result.success ? "Returning to lobby with the take." : session.Caption, subtitleStyle);
        y += 40f;

        DrawStat(panel.x + 36f, y, "Target value", Money(result.targetValue));
        DrawStat(panel.x + 36f + (panel.width - 72f) * 0.5f, y, result.success ? "Recovered" : "Consolation", Money(result.recoveredValue));
        y += 58f;
        DrawStat(panel.x + 36f, y, "Organizer cut", Money(result.organizerShare));
        DrawStat(panel.x + 36f + (panel.width - 72f) * 0.5f, y, "Heat", result.heat.ToString());
        y += 64f;

        GUI.Label(new Rect(panel.x + 36f, y, 300f, 22f), "CREW PAYOUTS", dimStyle);
        y += 26f;
        if (result.crewOutcomes != null)
        {
            foreach (var outcome in result.crewOutcomes)
            {
                string who = outcome.name + (outcome.isOrganizer ? "  ·  Organizer" : "");
                string status = string.IsNullOrEmpty(outcome.status) ? "ok" : outcome.status.ToUpperInvariant();
                GUI.Label(new Rect(panel.x + 36f, y, panel.width * 0.55f, 24f), who, bodyStyle);
                GUI.Label(new Rect(panel.x + panel.width * 0.52f, y, 90f, 24f), status, dimStyle);
                GUI.Label(new Rect(panel.x + panel.width - 200f, y, 160f, 24f), Money(outcome.share), moneyStyle);
                y += 28f;
            }
        }

        y += 10f;
        GUI.Label(new Rect(panel.x + 36f, y, 300f, 22f), "ROOMS", dimStyle);
        y += 24f;
        if (result.rooms != null)
        {
            foreach (var room in result.rooms)
            {
                int cleared = 0;
                int total = room.challenges == null ? 0 : room.challenges.Length;
                if (room.challenges != null)
                {
                    foreach (var challenge in room.challenges)
                    {
                        if (challenge.passed) cleared++;
                    }
                }
                GUI.Label(new Rect(panel.x + 36f, y, panel.width - 72f, 22f),
                    $"{room.name}   {cleared}/{total} cleared", bodyStyle);
                y += 22f;
                if (y > panel.yMax - 70f) break;
            }
        }

        var button = new Rect(panel.x + panel.width * 0.5f - 110f, panel.yMax - 52f, 220f, 34f);
        if (GUI.Button(button, "Return to lobby"))
            HeistBootstrap.NotifyResult(result);
    }

    void DrawStat(float x, float y, string label, string value)
    {
        GUI.Label(new Rect(x, y, 280f, 18f), label.ToUpperInvariant(), dimStyle);
        moneyStyle.alignment = TextAnchor.MiddleLeft;
        GUI.Label(new Rect(x, y + 16f, 280f, 28f), value, moneyStyle);
        moneyStyle.alignment = TextAnchor.MiddleRight;
    }

    static string Money(int value)
    {
        return "$" + value.ToString("N0");
    }
}
