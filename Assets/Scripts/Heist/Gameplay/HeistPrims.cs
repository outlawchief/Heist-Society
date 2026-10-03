using UnityEngine;

public static class HeistPrims
{
    public static GameObject Cube(Transform parent, Vector3 pos, Vector3 scale, Color color, string name = "Cube")
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        go.transform.localScale = scale;
        Paint(go, color);
        return go;
    }

    public static GameObject Capsule(Transform parent, Vector3 pos, Color color, string name = "Capsule")
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        go.transform.localScale = new Vector3(0.7f, 0.7f, 0.7f);
        Paint(go, color);
        return go;
    }

    public static TextMesh Label(Transform parent, Vector3 pos, string text, float size = 0.08f)
    {
        var go = new GameObject("Label");
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        var tm = go.AddComponent<TextMesh>();
        tm.text = text;
        tm.fontSize = 32;
        tm.characterSize = size;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = new Color(0.96f, 0.94f, 0.88f);
        go.AddComponent<HeistBillboard>();
        return tm;
    }

    public static void Paint(GameObject go, Color color)
    {
        var renderer = go.GetComponent<Renderer>();
        if (renderer == null) return;
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Standard");
        var mat = new Material(shader);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
        renderer.material = mat;
    }
}
