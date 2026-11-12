using System;
using UnityEngine;
using UnityEngine.InputSystem;

// Fica no objeto "Lu". Lu não anda para a frente: troca de pista (1, 2 ou 3), pula (e pode pular duas vezes,
// se doubleJumpEnabled) e detecta contato com obstáculos e moedas por um collider-gatilho criado aqui.
// As pistas 0 e 4 são de cenário e não são jogáveis. Lu pode trocar de pista durante o pulo.
public class LaneController : MonoBehaviour
{
    public bool CanControl { get; set; }
    public bool SensorActive { get; set; }
    public float CurrentX { get { return transform.localPosition.x; } }
    public float Height { get; private set; }

    public event Action Jumped;
    public event Action DoubleJumped;
    public event Action<BeltItem> ObstacleTouched;
    public event Action<BeltItem> CoinTouched;

    GameSettings s;
    int lane = 2;
    float velocityY;
    bool airborne;
    bool usedDouble;

    public void Init(GameSettings settings)
    {
        s = settings;
        lane = 2;
        velocityY = 0f;
        airborne = false;
        usedDouble = false;
        Height = 0f;
        CanControl = false;
        SensorActive = true;
        var p = transform.localPosition;
        p.x = 0f;
        p.y = 0f;
        transform.localPosition = p;

        var rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        var box = GetComponent<BoxCollider>();
        if (box == null) box = gameObject.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = s.luHitboxSize;
        box.center = new Vector3(0f, s.luHitboxCenterY, 0f);
    }

    public void MoveLeft()
    {
        if (!CanControl) return;
        lane = Mathf.Max(1, lane - 1);
    }

    public void MoveRight()
    {
        if (!CanControl) return;
        lane = Mathf.Min(3, lane + 1);
    }

    public void Jump()
    {
        if (!CanControl || s == null) return;
        float T = Mathf.Max(0.05f, s.jumpSeconds);
        if (!airborne)
        {
            airborne = true;
            usedDouble = false;
            velocityY = 4f * s.jumpHeight / T;
            if (Jumped != null) Jumped();
        }
        else if (s.doubleJumpEnabled && !usedDouble)
        {
            usedDouble = true;
            float g = Gravity();
            velocityY = Mathf.Sqrt(2f * g * Mathf.Max(0.01f, s.doubleJumpHeight));
            if (DoubleJumped != null) DoubleJumped();
        }
    }

    float Gravity()
    {
        float T = Mathf.Max(0.05f, s.jumpSeconds);
        return 8f * s.jumpHeight / (T * T);
    }

    void Update()
    {
        if (s == null) return;

        // Teclado só para testar no Editor (Input System).
        if (CanControl)
        {
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) MoveLeft();
                if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) MoveRight();
                if (kb.spaceKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame) Jump();
            }
        }

        var p = transform.localPosition;
        float targetX = (lane - 2) * s.laneWidth;
        float speed = s.laneWidth / Mathf.Max(0.01f, s.laneChangeSeconds);
        p.x = Mathf.MoveTowards(p.x, targetX, speed * Time.deltaTime);

        if (airborne)
        {
            float dt = Time.deltaTime;
            velocityY -= Gravity() * dt;
            Height += velocityY * dt;
            if (Height <= 0f)
            {
                Height = 0f;
                velocityY = 0f;
                airborne = false;
                usedDouble = false;
            }
        }
        p.y = Height;
        transform.localPosition = p;
    }

    public bool IsAirborne { get { return airborne; } }

    void OnTriggerEnter(Collider other)
    {
        Touch(other);
    }

    void OnTriggerStay(Collider other)
    {
        Touch(other);
    }

    void Touch(Collider other)
    {
        if (!SensorActive) return;
        var item = other.GetComponentInParent<BeltItem>();
        if (item == null) return;
        if (item.kind == BeltKind.Obstacle)
        {
            if (ObstacleTouched != null) ObstacleTouched(item);
        }
        else if (item.kind == BeltKind.Coin)
        {
            if (CoinTouched != null) CoinTouched(item);
        }
    }
}
