using UnityEngine;
using UnityEngine.Rendering;

public class HeistVisionArc : MonoBehaviour
{
    const int Segments = 20;
    MeshRenderer meshRenderer;
    LineRenderer outline;
    Material fillMat;
    Material lineMat;
    Color painted;

    public static HeistVisionArc Create(Transform parent, Vector3 localPos, float range, float halfAngle, Color color)
    {
        var go = new GameObject("VisionArc");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.identity;
        var arc = go.AddComponent<HeistVisionArc>();
        arc.Build(range, halfAngle, color);
        return arc;
    }

    public void Build(float range, float halfAngle, Color color)
    {
        var filter = gameObject.AddComponent<MeshFilter>();
        filter.mesh = BuildFan(range, halfAngle);
        meshRenderer = gameObject.AddComponent<MeshRenderer>();
        meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        fillMat = MakeTransparent(color);
        meshRenderer.material = fillMat;
        outline = BuildOutline(transform, range, halfAngle, color);
        lineMat = outline.material;
        painted = color;
        ApplyColor(color);
    }

    public void SetShown(bool on)
    {
        if (meshRenderer != null) meshRenderer.enabled = on;
        if (outline != null) outline.enabled = on;
    }

    public void SetColor(Color color)
    {
        if (color == painted) return;
        painted = color;
        ApplyColor(color);
    }

    void ApplyColor(Color color)
    {
        Apply(fillMat, color);
        if (outline != null)
        {
            Color edge = color;
            edge.a = Mathf.Clamp01(color.a + 0.55f);
            outline.startColor = edge;
            outline.endColor = edge;
            Apply(lineMat, edge);
        }
    }

    static Mesh BuildFan(float range, float halfAngle)
    {
        var mesh = new Mesh { name = "VisionFan" };
        int vertCount = Segments + 2;
        var verts = new Vector3[vertCount];
        verts[0] = Vector3.zero;
        for (int i = 0; i <= Segments; i++)
        {
            float t = i / (float)Segments;
            float angle = Mathf.Lerp(-halfAngle, halfAngle, t) * Mathf.Deg2Rad;
            verts[i + 1] = new Vector3(Mathf.Sin(angle) * range, 0.02f, Mathf.Cos(angle) * range);
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
        mesh.triangles = tris;
        mesh.RecalculateBounds();
        return mesh;
    }

    static LineRenderer BuildOutline(Transform parent, float range, float halfAngle, Color color)
    {
        var go = new GameObject("VisionOutline");
        go.transform.SetParent(parent, false);
        var line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.loop = true;
        line.widthMultiplier = 0.06f;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.material = MakeTransparent(color);
        int count = Segments + 2;
        line.positionCount = count;
        line.SetPosition(0, Vector3.zero);
        for (int i = 0; i <= Segments; i++)
        {
            float t = i / (float)Segments;
            float angle = Mathf.Lerp(-halfAngle, halfAngle, t) * Mathf.Deg2Rad;
            line.SetPosition(i + 1, new Vector3(Mathf.Sin(angle) * range, 0.03f, Mathf.Cos(angle) * range));
        }
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
        if (mat == null) return;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
    }
}
