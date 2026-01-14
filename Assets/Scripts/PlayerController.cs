using System.Collections;

using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// ============================================================================
// PlayerController
//  - Movemento: controla o xogador con teclas WASD (novo Input System ou clásico)
//  - Acción: ao premer Space fai un "knock" (reproduce un son) e alerta gardas próximos
//  - Depuración: debuxa unha esfera/círculo co radio do knock en Play e na Scene
//  - Requisitos: precisa un AudioSource no mesmo GameObject
// ============================================================================
//[RequireComponent(typeof(AudioSource))] // Require un AudioSource para o son
public class PlayerController : MonoBehaviour // Controlador sinxelo para o xogador con soporte para ambos sistemas de input de Unity
{
    [Header("Movement Settings")]
    public float moveSpeed = 5f;           // Velocidade de movemento cara adiante/atrás
    public float rotationSpeed = 180f;     // Velocidade de rotación (graos por segundo)

    [Header("Input System Selection")]
    public bool useNewInputSystem = true;  // True = novo Input System, False = Input Manager clásico

    [Header("Knock Settings")]
    public float knockRadius = 20.0f;
    [Header("Explosion Settings")]
    public float explosionRadius = 50.0f;

    [Header("Debug/Visualization")]
    public bool showKnockGizmos = true;            // Mostrar a esfera do son
    public bool showExplosionGizmos = true;        // Esfera de la explosión
    public float knockGizmoDuration = 1.5f;        // Tempo que permanece visible a esfera
    public float explosionGizmoDuration = 1.5f;        // Tempo que permanece visible a esfera

    public Color knockGizmoColor = new Color(0f, 1f, 1f, 0.85f); // Cor da esfera (cian)
    public Color explosionGizmoColor = new Color(1f, 0.3f, 0f, 0.5f); // Cor da esfera de la explosion (naranja)


    // Sonido
    [Header("Audio Sources")]
    [SerializeField] private AudioSource knockAudio;
    [SerializeField] private AudioSource explosionAudio;

    // Punto Seguro para la huda
    [Header("Punto Seguro")]
    [SerializeField] private Transform safePoint;

    // Estado do último knock
    private Vector3 lastKnockPoint;
    private float lastKnockTime = -999f;

    private Vector3 lastExplosionPoint;
    private float lastExplosionTime = -999f;

    private Vector2 moveInput; // Input actual do xogador

    //=========================================================================
    // Le o input segundo o sistema seleccionado (novo ou clásico) e move o xogador cada frame
    //=========================================================================
    void Update()
    {
        if (useNewInputSystem) // Obtén o input do sistema seleccionado
        {
            GetNewInput();
        }
        else
        {
            GetOldInput();
        }

        MovePlayer(); // Move o xogador baseándose no input
    }
    //=========================================================================
    // Le o input usando o Input Manager clásico (Input.GetAxis): Horizontal (A/D) e Vertical (W/S)
    //=========================================================================
    void GetOldInput() // Le o input usando o Input Manager clásico (Input.GetAxis)
    {
        float horizontal = Input.GetAxis("Horizontal"); // A/D ou frechas esquerda/dereita (eixos configurados en Edit > Project Settings > Input Manager)
        float vertical = Input.GetAxis("Vertical");     // W/S ou frechas arriba/abaixo
        moveInput = new Vector2(horizontal, vertical);

        // Detecta Space para executar unha acción (knock)
        if (Input.GetKeyDown(KeyCode.Space))
        {
            HandleSpaceAction();
        }

        if (Input.GetKeyDown(KeyCode.E))
        {
            Debug.Log(" OldInput E pressed");
            HandleEAction();
        }
    }

    //=========================================================================
    // Le o input usando o novo Input System consultando Keyboard.current (A, D, W, S)
    // Se o novo sistema non está dispoñible, recorre ao sistema antigo
    //=========================================================================
    void GetNewInput() // Le o input usando o novo Input System (Keyboard.current)
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null) // Comproba se hai un teclado conectado
        {
            float horizontal = 0f;
            float vertical = 0f;

            if (Keyboard.current.aKey.isPressed) horizontal -= 1f;  // A = esquerda
            if (Keyboard.current.dKey.isPressed) horizontal += 1f;  // D = dereita
            if (Keyboard.current.sKey.isPressed) vertical -= 1f;    // S = atrás
            if (Keyboard.current.wKey.isPressed) vertical += 1f;    // W = adiante

            moveInput = new Vector2(horizontal, vertical);

            // Detecta Space para executar unha acción (knock)
            if (Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                HandleSpaceAction();
            }
            if (Keyboard.current.eKey.wasPressedThisFrame)
            {
                Debug.Log("NewInput E pressed");
                HandleEAction();
            }
        }
#else
        GetOldInput(); // Se o novo sistema non está dispoñible, usa o antigo como fallback
#endif
    }

    //=========================================================================
    // Aplica rotación sobre o eixo Y co input horizontal e movemento adiante/atrás co input vertical
    //=========================================================================
    void MovePlayer() // Move e rota o xogador baseándose no moveInput
    {
        if (Mathf.Abs(moveInput.x) > 0.01f) // Rota con A/D (input horizontal)
        {
            transform.Rotate(0f, moveInput.x * rotationSpeed * Time.deltaTime, 0f);
        }

        if (Mathf.Abs(moveInput.y) > 0.01f) // Move cara adiante/atrás con W/S (input vertical)
        {
            transform.position += transform.forward * moveInput.y * moveSpeed * Time.deltaTime;
        }
    }

    //=========================================================================
    // Acción de Space: xera un 'knock' (son) e alerta a gardas próximos
    //=========================================================================
    void HandleSpaceAction()
    {
        // Reproduce o son de knock (se hai AudioSource e clip)
        StartCoroutine(PlayKnock());

        // Notifica aos gardas próximos para investigar a posición actual do xogador
        GuardController[] guards = FindObjectsByType<GuardController>(FindObjectsSortMode.None);
        Vector3 point = transform.position;

        // Gardar estado para debuxar a esfera
        lastKnockPoint = point;
        lastKnockTime = Time.time;

        // Debuxo en runtime (Game View) con Debug.DrawLine como un círculo de segmentos
        DrawKnockCircleDebug(lastKnockPoint, knockRadius, knockGizmoDuration, knockGizmoColor);

        foreach (var guard in guards)
        {
            float dist = Vector3.Distance(guard.transform.position, point);
            if (dist <= knockRadius)
            {
                guard.InvestigatePoint(point);
            }
        }
    }


    void HandleEAction()
    {
        StartCoroutine(PlayExplosion());
        GuardController[] guards = FindObjectsByType<GuardController>(FindObjectsSortMode.None);
        Vector3 explosionPoint = transform.position;

        // Guardamos el estado de la explosión para dibujar la esfera
        lastExplosionPoint = explosionPoint;
        lastExplosionTime = Time.time;

        DrawnExplosionCircleDebug(lastExplosionPoint, explosionRadius, explosionGizmoDuration, explosionGizmoColor);

        foreach (var guard in guards)
        {
            float dist = Vector3.Distance(guard.transform.position, explosionPoint);

            if (dist <= explosionRadius)
            {
                guard.RunAwayPoint(safePoint.position);
            }
        }
    }


    //=========================================================================
    // Debuxa un círculo no plano XZ usando segmentos con Debug.DrawLine (visible en Game/Scene)
    //=========================================================================
    void DrawKnockCircleDebug(Vector3 center, float radius, float duration, Color color)
    {
        int segments = 36;
        float step = Mathf.PI * 2f / segments;
        Vector3 prev = center + new Vector3(radius, 0f, 0f);
        for (int i = 1; i <= segments; i++)
        {
            float angle = i * step;
            Vector3 next = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            Debug.DrawLine(prev, next, color, duration);
            prev = next;
        }
    }

    //=========================================================================
    // Gizmos en Scene View: debuxa a esfera do knock durante un tempo tras premer Space
    //=========================================================================
    void OnDrawGizmos()
    {
        if (!showKnockGizmos)
            return;

        // Só en Play: evita debuxar valores por defecto en modo edición
        if (!Application.isPlaying)
            return;

        if (Time.time - lastKnockTime <= knockGizmoDuration)
        {
            Color prev = Gizmos.color;
            Gizmos.color = knockGizmoColor;
            Gizmos.DrawWireSphere(lastKnockPoint, knockRadius);
            Gizmos.color = prev;
        }
    }



    //=========================================================================
    // Debuxa un círculo no plano XZ usando segmentos con Debug.DrawLine (visible en Game/Scene)
    //=========================================================================
    void DrawnExplosionCircleDebug(Vector3 center, float radius, float duration, Color color)
    {
        int segments = 36;
        float step = Mathf.PI * 2f / segments;
        Vector3 prev = center + new Vector3(radius, 0f, 0f);
        for (int i = 1; i <= segments; i++)
        {
            float angle = i * step;
            Vector3 next = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            Debug.DrawLine(prev, next, color, duration);
            prev = next;
        }
    }


    // HandleEAction

    //=========================================================================
    // Reproduce o son de knock e espera a que remate
    //=========================================================================
    IEnumerator PlayKnock()
    {
        if (knockAudio != null)
        {
            knockAudio.Play();
            yield return new WaitForSeconds(knockAudio.clip.length);
        }
    }

    //=========================================================================
    // Reproduce o son de explosion e espera a que remate
    //=========================================================================

    IEnumerator PlayExplosion()
    {
        if (explosionAudio != null)
        {
            explosionAudio.Play();
            yield return new WaitForSeconds(explosionAudio.clip.length);
        }
    }
}

