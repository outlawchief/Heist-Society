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
    public bool inVent;
    public float Health;
    public float MaxHealth;

    CharacterController controller;
    float meleeCooldown;
    float distractTimer;
    float attackTilt;
    Vector3 attackFacing;
    Vector3 workFacing;
    bool workJiggling;
    Color baseColor;
    HeistGameSession session;

    public HeistGameSession Session => session;

    public void Setup(HeistCrewMember member, bool local, bool ai, Color color, HeistGameSession game)
    {
        Member = member;
        if (Member.level < 1) Member.level = 1;
        isLocal = local;
        isAi = ai;
        session = game;
        baseColor = color;
        MaxHealth = 30f + Member.level * 15f;
        Health = MaxHealth;
        HeistPrims.Paint(gameObject, color);
        var existing = GetComponent<CapsuleCollider>();
        if (existing != null) Destroy(existing);
        controller = gameObject.AddComponent<CharacterController>();
        controller.height = 1.6f;
        controller.radius = 0.32f;
        controller.center = new Vector3(0f, 0.2f, 0f);
        controller.enabled = local || ai;
        var nameLabel = HeistPrims.Label(transform, transform.position + Vector3.up * 1.5f, member.name, 0.05f);
        nameLabel.transform.localPosition = new Vector3(0f, 1.5f, 0f);
    }

    public void SetRole(bool local, bool ai)
    {
        isLocal = local;
        isAi = ai;
        if (controller != null) controller.enabled = (local || ai) && !downed;
    }

    public float MoveSpeed => 3.2f + Member.stats.agi * 0.22f;
    public float MeleeRange => 1.45f + Member.stats.str * 0.06f;
    public int MeleeDamage => 5 + Member.stats.str * 3;
    public float AttackCooldown => Mathf.Max(0.22f, 0.92f - Member.stats.agi * 0.07f);
    public float Noise => carryingLoot ? 1.2f : 0.55f + (Member.stats.agi < 5 ? 0.35f : 0f);

    void Update()
    {
        if (downed) return;
        meleeCooldown -= Time.deltaTime;
        distractTimer -= Time.deltaTime;
        if (inVent)
        {
            TickVent();
            return;
        }

        if (isAi) TickAi();
        else if (isLocal) TickLocal();

        if (carryingLoot && session != null && session.Loot != null)
        {
            session.Loot.transform.position = transform.position + Vector3.up * 1.3f + transform.forward * 0.4f;
        }

        TickAttackTilt();
        TickWorkJiggle();
    }

    void TickWorkJiggle()
    {
        if (attackTilt > 0f) return;
        if (interactFill <= 0f)
        {
            if (!workJiggling) return;
            workJiggling = false;
            Vector3 rest = workFacing.sqrMagnitude > 0.01f ? workFacing : transform.forward;
            rest.y = 0f;
            if (rest.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.LookRotation(rest.normalized, Vector3.up);
            return;
        }

        if (!workJiggling)
        {
            workFacing = transform.forward;
            workFacing.y = 0f;
            workJiggling = true;
        }
        if (workFacing.sqrMagnitude < 0.01f) return;
        float shimmy = Mathf.Sin(Time.time * 16f);
        float nod = Mathf.Abs(Mathf.Sin(Time.time * 8f)) * 6f;
        transform.rotation = Quaternion.LookRotation(workFacing.normalized, Vector3.up)
            * Quaternion.Euler(nod, shimmy * 4f, shimmy * 8f);
    }

    void TickAttackTilt()
    {
        if (attackTilt <= 0f) return;
        attackTilt = Mathf.Max(0f, attackTilt - Time.deltaTime / 0.2f);
        Vector3 face = attackFacing.sqrMagnitude > 0.01f ? attackFacing : transform.forward;
        face.y = 0f;
        if (face.sqrMagnitude < 0.01f) return;
        transform.rotation = Quaternion.LookRotation(face.normalized, Vector3.up)
            * Quaternion.Euler(28f * attackTilt, 0f, 0f);
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
        if (input.sqrMagnitude > 0.01f && attackTilt <= 0f)
            transform.forward = new Vector3(input.x, 0f, input.z);

        if (sprint && session.Heat.InCameraView(transform.position))
            session.Heat.Add(6f * Time.deltaTime, "sprinting on camera");

        FindInteractable();
        if (kb.eKey.wasPressedThisFrame && currentVent != null)
        {
            session.TryEnterVent(this, currentVent);
            interactFill = 0f;
        }
        else if (kb.eKey.isPressed && current != null && !current.completed)
            HoldInteract();
        else
            interactFill = 0f;

        if (kb.spaceKey.wasPressedThisFrame) Melee();
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

    HeistVent currentVent;

    void TickVent()
    {
        var kb = Keyboard.current;
        if (kb == null || session == null) return;
        if (kb.aKey.wasPressedThisFrame || kb.leftArrowKey.wasPressedThisFrame)
            session.CycleVent(-1);
        if (kb.dKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame)
            session.CycleVent(1);
        if (kb.eKey.wasPressedThisFrame)
            session.ExitVent();
    }

    void FindInteractable()
    {
        current = null;
        currentVent = null;
        prompt = carryingLoot ? "Carry loot to EXTRACT" : "";
        float best = 1.8f;
        foreach (var interactable in session.Level.Interactables)
        {
            if (interactable == null || interactable.completed || !interactable.gameObject.activeInHierarchy) continue;
            float dist = Vector3.Distance(transform.position, interactable.transform.position);
            if (dist < best)
            {
                best = dist;
                current = interactable;
                prompt = $"Hold E: {interactable.challenge.name} ({interactable.challenge.skill.ToUpperInvariant()})";
            }
        }
        foreach (var vent in session.Level.Vents)
        {
            if (vent == null || !vent.revealed) continue;
            float dist = Vector3.Distance(transform.position, vent.transform.position);
            if (dist < 1.7f && dist < best)
            {
                best = dist;
                current = null;
                currentVent = vent;
                prompt = CanUseVent(vent)
                    ? "E: Enter vents"
                    : $"Need AGI {HeistVent.AgilityNeed}+ to use vents";
            }
        }
    }

    bool CanUseVent(HeistVent vent) => vent != null && vent.CanEnter(this);

    public void SetHidden(bool hidden)
    {
        foreach (var r in GetComponentsInChildren<Renderer>(true))
            r.enabled = !hidden;
        if (controller != null) controller.enabled = !hidden;
    }

    public void Teleport(Vector3 pos)
    {
        if (controller != null) controller.enabled = false;
        transform.position = pos;
        if (controller != null && !inVent) controller.enabled = true;
    }

    void HoldInteract()
    {
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
        meleeCooldown = AttackCooldown;
        attackTilt = 1f;
        HeistAudio.PlayOperativePunch();
        var target = session != null ? session.NearestGuard(transform.position, MeleeRange + 0.35f) : null;
        if (target != null)
        {
            Vector3 dir = target.transform.position - transform.position;
            dir.y = 0f;
            attackFacing = dir.sqrMagnitude > 0.01f ? dir.normalized : transform.forward;
        }
        else
            attackFacing = transform.forward;

        session?.TryMelee(this);
    }

    public void TakeDamage(float amount, string source)
    {
        if (downed) return;
        Health = Mathf.Max(0f, Health - amount);
        if (Health <= 0f)
            Down(source);
        else
            prompt = $"{Member.name} HP {Health:0}/{MaxHealth:0}";
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

    public void ApplyRemote(Vector3 pos, bool down, bool loot, float health)
    {
        if (isLocal && !isAi)
        {
            Health = health;
            if (down && !downed) Down("downed");
            return;
        }
        transform.position = pos;
        Health = health;
        carryingLoot = loot;
        if (down && !downed) Down("downed");
        downed = down;
    }
}
