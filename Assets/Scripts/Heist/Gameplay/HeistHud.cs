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
    Texture2D trackTex;

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
        panelTex = Pixel(new Color(0.07f, 0.075f, 0.08f, 0.92f));
        accentTex = Pixel(new Color(0.72f, 0.58f, 0.22f, 1f));
        trackTex = Pixel(new Color(0.16f, 0.16f, 0.17f, 1f));
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
        DrawStatusCard(op);
        DrawCaption();
        DrawPrompt(op);
        DrawCrewSelect();
        DrawControls();
    }

    void DrawStatusCard(HeistOperative op)
    {
        float heat = session.Heat != null ? session.Heat.Value : 0f;
        bool hasStats = op != null && op.Member != null && op.Member.stats != null;
        float cardW = 336f;
        float cardH = 92f;
        if (op != null) cardH += 52f;
        if (hasStats) cardH += 52f;
        if (!string.IsNullOrEmpty(session.JoinCode)) cardH += 22f;
        var card = new Rect(16f, 16f, cardW, cardH);
        GUI.DrawTexture(card, panelTex);
        GUI.DrawTexture(new Rect(card.x, card.y, card.width, 3f), accentTex);

        float y = card.y + 10f;
        float pad = 14f;
        float inner = card.width - pad * 2f;
        if (op != null && op.Member != null)
        {
            moneyStyle.alignment = TextAnchor.MiddleLeft;
            GUI.Label(new Rect(card.x + pad, y, inner - 56f, 24f), op.Member.name, moneyStyle);
            dimStyle.alignment = TextAnchor.MiddleRight;
            GUI.Label(new Rect(card.x + pad, y, inner, 24f), "LV " + Mathf.Max(1, op.Member.level), dimStyle);
            dimStyle.alignment = TextAnchor.MiddleLeft;
            moneyStyle.alignment = TextAnchor.MiddleRight;
            y += 26f;
            float hpT = op.MaxHealth <= 0f ? 0f : op.Health / op.MaxHealth;
            GUI.Label(new Rect(card.x + pad, y, inner, 16f), $"HP  {op.Health:0}/{op.MaxHealth:0}", dimStyle);
            y += 16f;
            DrawBar(new Rect(card.x + pad, y, inner, 8f), hpT, Color.Lerp(new Color(0.75f, 0.22f, 0.2f), new Color(0.45f, 0.72f, 0.38f), hpT));
            y += 16f;
        }

        GUI.Label(new Rect(card.x + pad, y, inner, 16f), $"HEAT  {heat:0}    LOCKDOWNS  {session.Heat?.Lockdowns ?? 0}", dimStyle);
        y += 16f;
        DrawBar(new Rect(card.x + pad, y, inner, 8f), heat / 100f, Color.Lerp(new Color(0.86f, 0.62f, 0.18f), new Color(0.85f, 0.2f, 0.16f), heat / 100f));
        y += 14f;

        if (hasStats)
        {
            var s = op.Member.stats;
            DrawStatGrid(card.x + pad, y, inner, new[]
            {
                ("STR", s.str), ("AGI", s.agi), ("INT", s.intel),
                ("DEX", s.dex), ("CHA", s.cha), ("PER", s.per)
            });
            y += 48f;
        }
        if (!string.IsNullOrEmpty(session.JoinCode))
            GUI.Label(new Rect(card.x + pad, y, inner, 18f), "JOIN  " + session.JoinCode, bodyStyle);
    }

    void DrawStatGrid(float x, float y, float width, (string label, int value)[] stats)
    {
        const int cols = 3;
        float gap = 6f;
        float cellW = (width - gap * (cols - 1)) / cols;
        float cellH = 22f;
        var labelStyle = new GUIStyle(dimStyle)
        {
            fontSize = 11,
            alignment = TextAnchor.MiddleLeft,
            clipping = TextClipping.Clip,
            wordWrap = false
        };
        var valueStyle = new GUIStyle(bodyStyle)
        {
            fontSize = 15,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleRight,
            clipping = TextClipping.Clip,
            wordWrap = false
        };
        for (int i = 0; i < stats.Length; i++)
        {
            int col = i % cols;
            int row = i / cols;
            var cell = new Rect(x + col * (cellW + gap), y + row * (cellH + 2f), cellW, cellH);
            GUI.Label(new Rect(cell.x, cell.y, cell.width * 0.58f, cell.height), stats[i].label, labelStyle);
            GUI.Label(new Rect(cell.x + cell.width * 0.4f, cell.y, cell.width * 0.6f, cell.height), stats[i].value.ToString(), valueStyle);
        }
    }

    void DrawCaption()
    {
        if (string.IsNullOrEmpty(session.Caption) || session.Caption.StartsWith("WASD")) return;
        float w = Mathf.Min(640f, Screen.width - 400f);
        var banner = new Rect((Screen.width - w) * 0.5f, 16f, w, 36f);
        GUI.DrawTexture(banner, panelTex);
        GUI.Label(banner, session.Caption, subtitleStyle);
    }

    void DrawPrompt(HeistOperative op)
    {
        if (op == null || string.IsNullOrEmpty(op.prompt)) return;
        bool working = op.interactFill > 0f;
        float w = 440f;
        float h = working ? 72f : 48f;
        var card = new Rect((Screen.width - w) * 0.5f, Screen.height - h - 56f, w, h);
        GUI.DrawTexture(card, panelTex);
        GUI.DrawTexture(new Rect(card.x, card.y, 4f, card.height), accentTex);
        GUI.Label(new Rect(card.x + 16f, card.y + 8f, card.width - 28f, 24f), op.prompt, bodyStyle);
        if (!working) return;
        DrawBar(new Rect(card.x + 16f, card.y + 40f, card.width - 32f, 10f), op.interactFill, new Color(0.86f, 0.68f, 0.28f));
    }

    void DrawCrewSelect()
    {
        if (session == null || session.Ended) return;
        if (session.Operatives == null || session.Operatives.Count == 0) return;
        var photon = HeistPhotonSession.Instance;

        float w = 220f;
        float h = 28f + session.Operatives.Count * 28f;
        var box = new Rect(Screen.width - w - 16f, 16f, w, h);
        GUI.DrawTexture(box, panelTex);
        GUI.Label(new Rect(box.x + 10f, box.y + 4f, box.width - 20f, 20f),
            session.LocalOperative == null ? "TAKE CONTROL" : "CREW", dimStyle);

        float y = box.y + 26f;
        foreach (var op in session.Operatives)
        {
            if (op == null || op.Member == null) continue;
            bool mine = session.LocalOperative == op;
            string label = (mine ? "> " : "") + op.Member.name;
            var row = new Rect(box.x + 8f, y, box.width - 16f, 24f);
            if (mine)
            {
                GUI.Label(row, label, bodyStyle);
            }
            else if (GUI.Button(row, label))
            {
                if (photon != null) photon.RequestClaim(op.Member.id);
                else session.Possess(op.Member.id);
            }
            y += 26f;
        }
    }

    void DrawControls()
    {
        var line = new Rect(0f, Screen.height - 28f, Screen.width, 20f);
        dimStyle.alignment = TextAnchor.MiddleCenter;
        GUI.Label(line, "WASD move     Shift sprint     E interact     Space melee     F distract", dimStyle);
        dimStyle.alignment = TextAnchor.MiddleLeft;
    }

    void DrawBar(Rect rect, float amount, Color fill)
    {
        GUI.DrawTexture(rect, trackTex);
        float width = rect.width * Mathf.Clamp01(amount);
        if (width <= 0f) return;
        GUI.color = fill;
        GUI.DrawTexture(new Rect(rect.x, rect.y, width, rect.height), Texture2D.whiteTexture);
        GUI.color = Color.white;
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
