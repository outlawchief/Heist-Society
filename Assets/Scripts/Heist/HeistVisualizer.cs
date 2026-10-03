using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HeistVisualizer : MonoBehaviour
{
    readonly List<GameObject> spawned = new List<GameObject>();
    readonly Dictionary<string, Transform> crewMarkers = new Dictionary<string, Transform>();
    TextMesh caption;
    Transform worldRoot;

    public IEnumerator Play(List<HeistRoomPlan> rooms, HeistCrewMember[] crew, HeistResult result)
    {
        Clear();
        worldRoot = new GameObject("HeistWorld").transform;
        spawned.Add(worldRoot.gameObject);

        SetupCamera(rooms.Count);
        CreateCaption();
        BuildRooms(rooms);
        SpawnCrew(crew);

        SetCaption("Crew is on site. Beginning the run.");
        yield return new WaitForSeconds(0.8f);

        for (int r = 0; r < rooms.Count; r++)
        {
            var room = rooms[r];
            float roomX = r * 9f;
            MoveCrewTo(roomX - 2.2f);
            SetCaption($"Entering {room.name}");
            yield return new WaitForSeconds(0.7f);

            foreach (var challenge in room.challenges)
            {
                var block = CreateChallengeBlock(roomX + 1.4f, challenge);
                if (!string.IsNullOrEmpty(challenge.actorId) && crewMarkers.TryGetValue(challenge.actorId, out var actor))
                {
                    yield return MoveTransform(actor, new Vector3(roomX + 0.4f, 0.6f, 0f), 0.35f);
                }

                SetCaption(challenge.narration);
                Flash(block, challenge.skipped ? new Color(0.85f, 0.8f, 0.55f) : challenge.passed ? new Color(0.35f, 0.75f, 0.4f) : new Color(0.85f, 0.2f, 0.25f));
                yield return new WaitForSeconds(1.05f);
            }
        }

        SetCaption(result.success
            ? $"Score secured. Organizer cut {result.organizerShare}."
            : $"Heist collapsed. Heat {result.heat}. Organizer consolation {result.organizerShare}.");
        yield return new WaitForSeconds(1.2f);
    }

    void SetupCamera(int roomCount)
    {
        var cam = Camera.main;
        if (cam == null)
        {
            var camGo = new GameObject("HeistCamera");
            cam = camGo.AddComponent<Camera>();
            cam.tag = "MainCamera";
            spawned.Add(camGo);
        }

        cam.orthographic = true;
        cam.orthographicSize = 5.2f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.03f, 0.035f, 0.04f);
        float mid = (roomCount - 1) * 4.5f;
        cam.transform.position = new Vector3(mid, 16f, -10f);
        cam.transform.rotation = Quaternion.Euler(55f, 0f, 0f);

        var light = FindFirstObjectByType<Light>();
        if (light == null)
        {
            var lightGo = new GameObject("HeistLight");
            light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            spawned.Add(lightGo);
        }
        light.color = new Color(1f, 0.92f, 0.8f);
        light.intensity = 1.1f;
        light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
    }

    void CreateCaption()
    {
        var go = new GameObject("Caption");
        go.transform.SetParent(worldRoot);
        spawned.Add(go);
        caption = go.AddComponent<TextMesh>();
        caption.fontSize = 48;
        caption.characterSize = 0.08f;
        caption.anchor = TextAnchor.MiddleCenter;
        caption.alignment = TextAlignment.Center;
        caption.color = new Color(0.96f, 0.94f, 0.88f);
        go.transform.position = new Vector3(0f, 4.4f, 3.2f);
    }

    void SetCaption(string text)
    {
        if (caption == null) return;
        caption.text = text;
        if (Camera.main != null)
        {
            var cam = Camera.main.transform;
            caption.transform.position = cam.position + cam.forward * 8f + cam.up * 2.6f;
            caption.transform.rotation = Quaternion.LookRotation(cam.forward, Vector3.up);
        }
    }

    void BuildRooms(List<HeistRoomPlan> rooms)
    {
        for (int i = 0; i < rooms.Count; i++)
        {
            float x = i * 9f;
            var floor = Quad(new Vector3(x, 0f, 0f), new Vector3(8.2f, 0.08f, 6f), new Color(0.09f, 0.1f, 0.12f));
            floor.name = "Room_" + rooms[i].name;
            var label = new GameObject("RoomLabel");
            label.transform.SetParent(worldRoot);
            spawned.Add(label);
            var tm = label.AddComponent<TextMesh>();
            tm.text = rooms[i].name.ToUpperInvariant();
            tm.fontSize = 28;
            tm.characterSize = 0.07f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.color = new Color(0.85f, 0.8f, 0.62f);
            label.transform.position = new Vector3(x, 0.2f, 2.6f);

            Quad(new Vector3(x, 1.1f, 3.1f), new Vector3(8.2f, 2.2f, 0.12f), new Color(0.16f, 0.08f, 0.09f));
        }
    }

    void SpawnCrew(HeistCrewMember[] crew)
    {
        crewMarkers.Clear();
        if (crew == null) return;
        for (int i = 0; i < crew.Length; i++)
        {
            var marker = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            marker.name = crew[i].name;
            marker.transform.SetParent(worldRoot);
            marker.transform.position = new Vector3(-2.4f + i * 0.7f, 0.6f, -1.6f);
            marker.transform.localScale = new Vector3(0.45f, 0.45f, 0.45f);
            Color color = crew[i].isOrganizer
                ? new Color(0.85f, 0.22f, 0.26f)
                : new Color(0.95f, 0.9f, 0.72f);
            ApplyColor(marker, color);
            spawned.Add(marker);
            crewMarkers[crew[i].id] = marker.transform;

            var nameGo = new GameObject("Name");
            nameGo.transform.SetParent(marker.transform);
            nameGo.transform.localPosition = new Vector3(0f, 1.5f, 0f);
            var tm = nameGo.AddComponent<TextMesh>();
            tm.text = crew[i].name;
            tm.fontSize = 20;
            tm.characterSize = 0.08f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.color = Color.white;
        }
    }

    GameObject CreateChallengeBlock(float x, HeistChallengeResult challenge)
    {
        var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
        block.transform.SetParent(worldRoot);
        block.transform.position = new Vector3(x, 0.7f, 0.4f);
        block.transform.localScale = new Vector3(1.1f, 1.4f, 1.1f);
        ApplyColor(block, new Color(0.25f, 0.18f, 0.12f));
        spawned.Add(block);

        var label = new GameObject("ChallengeLabel");
        label.transform.SetParent(block.transform);
        label.transform.localPosition = new Vector3(0f, 1.1f, 0f);
        var tm = label.AddComponent<TextMesh>();
        tm.text = challenge.name;
        tm.fontSize = 18;
        tm.characterSize = 0.06f;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.color = new Color(1f, 0.95f, 0.8f);
        return block;
    }

    void MoveCrewTo(float x)
    {
        int i = 0;
        foreach (var pair in crewMarkers)
        {
            pair.Value.position = new Vector3(x + i * 0.7f, 0.6f, -1.6f);
            i++;
        }
    }

    IEnumerator MoveTransform(Transform target, Vector3 dest, float duration)
    {
        Vector3 start = target.position;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            target.position = Vector3.Lerp(start, dest, t / duration);
            yield return null;
        }
        target.position = dest;
    }

    void Flash(GameObject block, Color color)
    {
        ApplyColor(block, color);
    }

    GameObject Quad(Vector3 position, Vector3 scale, Color color)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.transform.SetParent(worldRoot);
        go.transform.position = position;
        go.transform.localScale = scale;
        ApplyColor(go, color);
        spawned.Add(go);
        return go;
    }

    void ApplyColor(GameObject go, Color color)
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

    public void Clear()
    {
        foreach (var go in spawned)
        {
            if (go != null) Destroy(go);
        }
        spawned.Clear();
        crewMarkers.Clear();
        caption = null;
        worldRoot = null;
    }
}
