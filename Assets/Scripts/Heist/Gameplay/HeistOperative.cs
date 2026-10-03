using UnityEngine;
using UnityEngine.InputSystem;

public class HeistOperative : MonoBehaviour
{
    public HeistCrewMember Member;
    public bool isLocal = true;
    public bool isAi;
    public bool downed;
    public bool carryingLoot;
    public float interactFill;
    public string prompt = "";
    public HeistInteractable current;

    CharacterController controller;
    float meleeCooldown;
    float perceptionTimer;
    float distractTimer;
    Color baseColor;
    HeistGameSession session;

    public void Setup(HeistCrewMember member, bool local, bool ai, Color color, HeistGameSession game)
    {
        Member = member;
        isLocal = local;
        isAi = ai;
        session = game;
        baseColor = color;
        HeistPrims.Paint(gameObject, color);
        var existing = GetComponent<CapsuleCollider>();
        if (existing != null) Destroy(existing);
        controller = gameObject.AddComponent<CharacterController>();
        controller.height = 1.6f;
        controller.radius = 0.32f;
        controller.center = new Vector3(0f, 0.2f, 0f);
        var nameLabel = HeistPrims.Label(transform, transform.position + Vector3.up * 1.5f, member.name, 0.05f);
        nameLabel.transform.localPosition = new Vector3(0f, 1.5f, 0f);
    }

    public float MoveSpeed => 3.2f + Member.stats.agi * 0.22f;
    public float MeleeRange => 1.4f + Member.stats.str * 0.06f;
    public float Noise => carryingLoot ? 1.2f : 0.55f + (Member.stats.agi < 5 ? 0.35f : 0f);

    void Update()
    {
        if (downed) return;
        meleeCooldown -= Time.deltaTime;
        perceptionTimer -= Time.deltaTime;
        distractTimer -= Time.deltaTime;

        if (isAi) TickAi();
        else if (isLocal) TickLocal();

        if (carryingLoot && session != null && session.Loot != null)
        {
            session.Loot.transform.position = transform.position + Vector3.up * 1.3f + transform.forward * 0.4f;
        }
    }

    void TickLocal()
    {
        var kb = Keyboard.current;
        if (kb == null) return;
        Vector3 input = Vector3.zero;
        if (kb.wKey.isPressed || kb.upArrowKey.isPressed) input.z += 1f;
        if (kb.sKey.isPressed || kb.downArrowKey.isPressed) input.z -= 1f;
        if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) input.x += 1f;
        if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) input.x -= 1f;
        bool sprint = kb.leftShiftKey.isPressed;
        float speed = MoveSpeed * (sprint ? 1.35f : 1f) * (carryingLoot ? 0.72f + Member.stats.str * 0.03f : 1f);
        Vector3 motion = input.normalized * speed * Time.deltaTime;
        motion.y = -4f * Time.deltaTime;
        controller.Move(motion);
        if (input.sqrMagnitude > 0.01f)
            transform.forward = new Vector3(input.x, 0f, input.z);

        if (sprint && session.Heat.InCameraView(transform.position))
            session.Heat.Add(6f * Time.deltaTime, "sprinting on camera");

        FindInteractable();
        if (kb.eKey.isPressed && current != null && !current.completed)
            HoldInteract();
        else
            interactFill = 0f;

        if (kb.spaceKey.wasPressedThisFrame) Melee();
        if (kb.qKey.wasPressedThisFrame) PerceptionPulse();
        if (kb.fKey.wasPressedThisFrame) Distract();
    }

    void TickAi()
    {
        var leader = session.LocalOperative;
        if (leader == null) return;
        Vector3 to = leader.transform.position - transform.position;
        to.y = 0f;
        if (to.magnitude > 2.2f)
        {
            controller.Move(to.normalized * MoveSpeed * 0.85f * Time.deltaTime + Vector3.down * 4f * Time.deltaTime);
            transform.forward = to.normalized;
        }

        FindInteractable();
        if (current != null && !current.completed && to.magnitude < 4f)
            HoldInteract();
    }

    void FindInteractable()
    {
        current = null;
        prompt = carryingLoot ? "Carry loot to EXTRACT" : "";
        float best = 1.6f;
        foreach (var interactable in session.Level.Interactables)
        {
            if (interactable == null || interactable.completed) continue;
            float dist = Vector3.Distance(transform.position, interactable.transform.position);
            if (dist < best)
            {
                best = dist;
                current = interactable;
                prompt = $"Hold E: {interactable.challenge.name} ({interactable.challenge.skill.ToUpperInvariant()})";
            }
        }
    }

    void HoldInteract()
    {
        if (current.challenge.type == "bypass" && Member.stats.per < 4 && HeistResolver.GearBonus(Member.gear, current.challenge) == 0)
        {
            prompt = "Need more PER to notice this";
            return;
        }

        float need = current.HoldTime(this);
        interactFill += Time.deltaTime / need;
        prompt = $"Working {current.challenge.name} {Mathf.Clamp01(interactFill) * 100f:0}%";
        if (interactFill >= 1f)
        {
            current.Complete(this);
            session.OnInteractSuccess(this, current);
            interactFill = 0f;
        }
    }

    public void CancelInteract(bool failed)
    {
        if (failed && current != null)
        {
            current.Fail(this);
            session.OnInteractFail(this, current);
        }
        interactFill = 0f;
    }

    public void Melee()
    {
        if (meleeCooldown > 0f) return;
        meleeCooldown = Mathf.Max(0.28f, 0.9f - Member.stats.str * 0.06f);
        session.TryMelee(this);
    }

    public void PerceptionPulse()
    {
        if (perceptionTimer > 0f) return;
        perceptionTimer = Mathf.Max(4f, 12f - Member.stats.per);
        session.OnPerception(this);
    }

    public void Distract()
    {
        if (distractTimer > 0f) return;
        distractTimer = Mathf.Max(5f, 14f - Member.stats.cha);
        session.OnDistract(this);
    }

    public void Down(string reason)
    {
        downed = true;
        carryingLoot = false;
        HeistPrims.Paint(gameObject, new Color(0.25f, 0.25f, 0.25f));
        prompt = "Downed: " + reason;
    }

    public void ApplyRemote(Vector3 pos, bool down, bool loot)
    {
        if (isLocal && !isAi) return;
        transform.position = pos;
        downed = down;
        carryingLoot = loot;
    }
}
