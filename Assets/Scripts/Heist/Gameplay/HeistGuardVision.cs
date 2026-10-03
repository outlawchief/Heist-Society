using UnityEngine;
using UnityEngine.Rendering;

public class HeistGuardVision : MonoBehaviour
{
    const int Segments = 20;
    static readonly Color Idle = new Color(1f, 0.82f, 0.22f, 0.28f);
    static readonly Color Alert = new Color(0.95f, 0.12f, 0.1f, 0.38f);

    HeistGuard guard;
    MeshRenderer meshRenderer;
    LineRenderer outline;
    Material mat;
    Color painted;

    public void Setup(HeistGuard owner, float sightRange, float sightHalfAngle)
    {
        guard = owner;

        var go = new GameObject("SightCone");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, -0.62f, 0f);
        go.transform.localRotation = Quaternion.identity;

        var filter = go.AddComponent<MeshFilter>();
        filter.mesh = BuildFan(sightRange, sightHalfAngle);
        meshRenderer = go.AddComponent<MeshRenderer>();
        meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        mat = MakeTransparent(Idle);
        meshRenderer.material = mat;
        painted = Idle;
        outline = BuildOutline(go.transform, sightRange, sightHalfAngle);
    }

    void LateUpdate()
    {
        if (mat == null || guard == null) return;
        Color next = guard.chase != null ? Alert : Idle;
        if (next == painted) return;
        painted = next;
        Apply(mat, next);
        if (outline != null)
        {
            Color edge = next;
            edge.a = 0.9f;
            outline.startColor = edge;
            outline.endColor = edge;
        }
    }

    static Mesh BuildFan(float range, float halfAngle)
    {
        var mesh = new Mesh { name = "GuardSightFan" };
        int vertCount = Segments + 2;
        var verts = new Vector3[vertCount];
        var colors = new Color[vertCount];
        verts[0] = Vector3.zero;
        colors[0] = Color.white;
        for (int i = 0; i <= Segments; i++)
        {
            float t = i / (float)Segments;
            float angle = Mathf.Lerp(-halfAngle, halfAngle, t) * Mathf.Deg2Rad;
            verts[i + 1] = new Vector3(Mathf.Sin(angle) * range, 0.02f, Mathf.Cos(angle) * range);
            colors[i + 1] = new Color(1f, 1f, 1f, 0.35f);
        }

        var tris = new int[Segments * 6];
        int tIndex = 0;
        for (int i = 0; i < Segments; i++)
        {
            tris[tIndex++] = 0;
            tris[tIndex++] = i + 1;
            tris[tIndex++] = i + 2;
            tris[tIndex++] = 0;
            tris[tIndex++] = i + 2;
            tris[tIndex++] = i + 1;
        }

        mesh.vertices = verts;
        mesh.colors = colors;
        mesh.triangles = tris;
        mesh.RecalculateBounds();
        return mesh;
    }

    static LineRenderer BuildOutline(Transform parent, float range, float halfAngle)
    {
        var go = new GameObject("SightOutline");
        go.transform.SetParent(parent, false);
        var line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.loop = true;
        line.widthMultiplier = 0.06f;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.material = MakeTransparent(Idle);
        int count = Segments + 2;
        line.positionCount = count;
        line.SetPosition(0, Vector3.zero);
        for (int i = 0; i <= Segments; i++)
        {
            float t = i / (float)Segments;
            float angle = Mathf.Lerp(-halfAngle, halfAngle, t) * Mathf.Deg2Rad;
            line.SetPosition(i + 1, new Vector3(Mathf.Sin(angle) * range, 0.03f, Mathf.Cos(angle) * range));
        }
        Color edge = Idle;
        edge.a = 0.9f;
        line.startColor = edge;
        line.endColor = edge;
        return line;
    }

    static Material MakeTransparent(Color color)
    {
        var shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Standard");
        var mat = new Material(shader);
        mat.renderQueue = 3000;
        mat.SetInt("_ZWrite", 0);
        mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_Cull", (int)CullMode.Off);
        Apply(mat, color);
        return mat;
    }

    static void Apply(Material mat, Color color)
    {
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
    }
}
