using UnityEngine;
using UnityEngine.AI;

public class HeistLaserGrid : MonoBehaviour
{
    public const float BurstHeat = 32f;
    public const float SustainHeat = 16f;

    public bool armed = true;
    public HeistInteractable panel;

    BoxCollider volume;
    NavMeshObstacle obstacle;
    readonly System.Collections.Generic.List<LineRenderer> beams = new System.Collections.Generic.List<LineRenderer>();
    float burstCooldown;
    bool warned;

    public static HeistLaserGrid Spawn(
        Transform parent,
        Vector3 center,
        Vector3 along,
        float width,
        float height,
        int beamCount)
    {
        along.y = 0f;
        if (along.sqrMagnitude < 0.01f) along = Vector3.forward;
        along.Normalize();
        var root = new GameObject("LaserGrid");
        root.transform.SetParent(parent, false);
        root.transform.position = center;
        root.transform.rotation = Quaternion.LookRotation(Vector3.Cross(Vector3.up, along), Vector3.up);
        var grid = root.AddComponent<HeistLaserGrid>();
        grid.Build(width, height, Mathf.Clamp(beamCount, 3, 8));
        return grid;
    }

    void Build(float width, float height, int beamCount)
    {
        volume = gameObject.AddComponent<BoxCollider>();
        volume.isTrigger = true;
        volume.size = new Vector3(width, height, 0.32f);
        volume.center = Vector3.zero;

        obstacle = gameObject.AddComponent<NavMeshObstacle>();
        obstacle.carving = true;
        obstacle.size = new Vector3(width, height, 0.4f);
        obstacle.center = Vector3.zero;

        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        var mat = new Material(shader);
        var color = new Color(1f, 0.12f, 0.08f);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);

        float half = width * 0.5f;
        float top = height * 0.5f - 0.18f;
        float bot = -height * 0.5f + 0.18f;
        for (int i = 0; i < beamCount; i++)
        {
            float t = beamCount == 1 ? 0.5f : i / (float)(beamCount - 1);
            float y = Mathf.Lerp(bot, top, t);
            var beam = new GameObject("Beam").AddComponent<LineRenderer>();
            beam.transform.SetParent(transform, false);
            beam.positionCount = 2;
            beam.useWorldSpace = false;
            beam.startWidth = 0.04f;
            beam.endWidth = 0.04f;
            beam.material = mat;
            beam.startColor = color;
            beam.endColor = color;
            beam.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            beam.SetPosition(0, new Vector3(-half, y, 0f));
            beam.SetPosition(1, new Vector3(half, y, 0f));
            beams.Add(beam);
        }

        var postA = HeistPrims.Cube(transform, transform.TransformPoint(new Vector3(-half, 0f, 0f)), new Vector3(0.12f, height, 0.12f), new Color(0.18f, 0.06f, 0.06f), "LaserPost");
        postA.transform.SetParent(transform, true);
        var postB = HeistPrims.Cube(transform, transform.TransformPoint(new Vector3(half, 0f, 0f)), new Vector3(0.12f, height, 0.12f), new Color(0.18f, 0.06f, 0.06f), "LaserPost");
        postB.transform.SetParent(transform, true);
        foreach (var col in GetComponentsInChildren<Collider>())
        {
            if (col != volume) Destroy(col);
        }
    }

    public void Disarm()
    {
        armed = false;
        if (volume != null) volume.enabled = false;
        if (obstacle != null) obstacle.enabled = false;
        foreach (var beam in beams)
        {
            if (beam != null) beam.enabled = false;
        }
    }

    void Update()
    {
        burstCooldown = Mathf.Max(0f, burstCooldown - Time.deltaTime);
    }

    void OnTriggerStay(Collider other)
    {
        if (!armed) return;
        var op = other.GetComponent<HeistOperative>() ?? other.GetComponentInParent<HeistOperative>();
        if (op == null || op.downed || op.inVent) return;
        var session = op.Session;
        if (session == null || session.Ended) return;
        if (burstCooldown <= 0f)
        {
            burstCooldown = 1.4f;
            if (session.IsHost) session.Heat.Add(BurstHeat, "laser trip");
            session.SetCaption($"{op.Member.name} tripped a laser grid.");
            warned = true;
        }
        else if (session.IsHost)
        {
            session.Heat.Add(SustainHeat * Time.deltaTime, "laser field");
            if (!warned)
            {
                session.SetCaption($"{op.Member.name} is standing in a laser grid.");
                warned = true;
            }
        }
    }
}
