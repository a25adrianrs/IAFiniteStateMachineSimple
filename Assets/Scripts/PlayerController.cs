using System.Collections;

using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// ============================================================================
// PlayerController
//  - Controla el movimiento del jugador en el mundo 3D usando WASD / flechas.
//  - Permite emitir un "knock" con Space y una "explosión" con E.
//  - Cuando se activa un knock o una explosión, notifica a los GuardController cercanos.
//  - Dibuja ayudas visuales en la escena para el radio de acción y para depuración.
//  - Usa AudioSource para reproducir efectos de sonido.
// ============================================================================
//[RequireComponent(typeof(AudioSource))] // Si quieres forzar que haya un AudioSource, descomenta esta línea.
public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 5f;           // Velocidad de movimiento hacia delante/atrás.
    public float rotationSpeed = 180f;     // Velocidad de rotación en grados por segundo.

    [Header("Input System Selection")]
    public bool useNewInputSystem = true;  // True = usa el nuevo Input System; False = usa el Input Manager clásico.

    [Header("Knock Settings")]
    public float knockRadius = 20.0f;      // Radio de influencia del knock.
    [Header("Explosion Settings")]
    public float explosionRadius = 50.0f;  // Radio de influencia de la explosión.

    [Header("Debug/Visualization")]
    public bool showKnockGizmos = true;            // Mostrar el gizmo de knock en la escena.
    public bool showExplosionGizmos = true;        // Mostrar el gizmo de explosión en la escena.
    public float knockGizmoDuration = 1.5f;        // Duración en segundos del gizmo del knock.
    public float explosionGizmoDuration = 1.5f;    // Duración en segundos del gizmo de la explosión.

    public Color knockGizmoColor = new Color(0f, 1f, 1f, 0.85f); // Color del gizmo del knock.
    public Color explosionGizmoColor = new Color(1f, 0.3f, 0f, 0.5f); // Color del gizmo de la explosión.

    [Header("Audio Sources")]
    [SerializeField] private AudioSource knockAudio;      // AudioSource para reproducir el sonido del knock.
    [SerializeField] private AudioSource explosionAudio;  // AudioSource para reproducir el sonido de la explosión.

    [Header("Punto Seguro")]
    [SerializeField] private Transform safePoint;         // Punto al que los guardias huyen tras la explosión.

    private Vector3 lastKnockPoint;   // Posición donde se realizó el último knock.
    private float lastKnockTime = -999f; // Momento del último knock; usado para dibujar gizmos.

    private Vector3 lastExplosionPoint; // Posición de la última explosión.
    private float lastExplosionTime = -999f; // Momento de la última explosión; usado para dibujar gizmos.

    private Vector2 moveInput; // Entrada de movimiento actual del jugador.

    //=========================================================================
    // Update se ejecuta una vez por frame.
    // Lee la entrada del jugador y mueve el personaje en cada frame.
    //=========================================================================
    void Update()
    {
        if (useNewInputSystem)
        {
            GetNewInput(); // Lee el input con el nuevo Input System (Keyboard.current).
        }
        else
        {
            GetOldInput(); // Lee el input con el Input Manager clásico (Input.GetAxis, GetKeyDown).
        }

        MovePlayer(); // Aplica la rotación y el avance/retroceso en base al input leído.
    }

    //=========================================================================
    // Lee el input usando el sistema clásico de Unity.
    // Utiliza Input.GetAxis para el movimiento y Input.GetKeyDown para acciones.
    //=========================================================================
    void GetOldInput()
    {
        float horizontal = Input.GetAxis("Horizontal"); // Eje horizontal: A/D o flechas.
        float vertical = Input.GetAxis("Vertical");     // Eje vertical: W/S o flechas.

        // Guardamos el valor del input en moveInput para usarlo en MovePlayer().
        moveInput = new Vector2(horizontal, vertical);

        // Detecta el botón Space para realizar el knock.
        if (Input.GetKeyDown(KeyCode.Space))
        {
            HandleSpaceAction();
        }

        // Detecta la tecla E para realizar la acción de explosión.
        if (Input.GetKeyDown(KeyCode.E))
        {
            Debug.Log("OldInput E pressed");
            HandleEAction();
        }
    }

    //=========================================================================
    // Lee el input usando el nuevo Input System desde Keyboard.current.
    // Si el nuevo sistema no está activado, recurre al antiguo.
    //=========================================================================
    void GetNewInput()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            float horizontal = 0f;
            float vertical = 0f;

            if (Keyboard.current.aKey.isPressed) horizontal -= 1f; // A = izquierda.
            if (Keyboard.current.dKey.isPressed) horizontal += 1f; // D = derecha.
            if (Keyboard.current.sKey.isPressed) vertical -= 1f;   // S = atrás.
            if (Keyboard.current.wKey.isPressed) vertical += 1f;   // W = adelante.

            moveInput = new Vector2(horizontal, vertical); // Input de movimiento guardado.

            // Detecta la pulsación de Space en este frame.
            if (Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                HandleSpaceAction();
            }

            // Detecta la pulsación de E en este frame.
            if (Keyboard.current.eKey.wasPressedThisFrame)
            {
                Debug.Log("NewInput E pressed");
                HandleEAction();
            }
        }
#else
        GetOldInput(); // Fallback al sistema clásico si no hay nuevo Input System.
#endif
    }

    //=========================================================================
    // Mueve al jugador y rota en función del input guardado en moveInput.
    // Se llama desde Update(), una vez por frame.
    //=========================================================================
    void MovePlayer()
    {
        // Rotación alrededor del eje Y cuando hay input horizontal.
        if (Mathf.Abs(moveInput.x) > 0.01f)
        {
            transform.Rotate(0f, moveInput.x * rotationSpeed * Time.deltaTime, 0f);
        }

        // Movimiento hacia delante/atrás en la dirección forward local del transform.
        if (Mathf.Abs(moveInput.y) > 0.01f)
        {
            transform.position += transform.forward * moveInput.y * moveSpeed * Time.deltaTime;
        }
    }

    //=========================================================================
    // Maneja el knock cuando el jugador pulsa Space.
    // Reproduce sonido, dibuja debug y alerta a los guardias cercanos.
    //=========================================================================
    void HandleSpaceAction()
    {
        StartCoroutine(PlayKnock()); // Reproduce el sonido del knock de forma no bloqueante.

        // Busca todos los objetos GuardController en la escena.
        GuardController[] guards = FindObjectsByType<GuardController>(FindObjectsSortMode.None);
        Vector3 point = transform.position; // Posición actual del jugador.

        // Guardamos la posición y el tiempo del knock para los gizmos.
        lastKnockPoint = point;
        lastKnockTime = Time.time;

        // Dibuja un círculo de debug en el plano XZ para visualizar el radio del knock.
        DrawKnockCircleDebug(lastKnockPoint, knockRadius, knockGizmoDuration, knockGizmoColor);

        foreach (var guard in guards)
        {
            float dist = Vector3.Distance(guard.transform.position, point); // Distancia del guardia al knock.

            if (dist <= knockRadius)
            {
                // Si el guardia está dentro del radio, le pedimos que investigue el punto.
                guard.InvestigatePoint(point);
            }
        }
    }

    //=========================================================================
    // Maneja la acción con la tecla E: explosión y huida de los guardias.
    //=========================================================================
    void HandleEAction()
    {
        StartCoroutine(PlayExplosion()); // Reproduce el sonido de explosión.

        GuardController[] guards = FindObjectsByType<GuardController>(FindObjectsSortMode.None);
        Vector3 explosionPoint = transform.position; // Posición del jugador en el momento de la explosión.

        lastExplosionPoint = explosionPoint;
        lastExplosionTime = Time.time;

        DrawnExplosionCircleDebug(lastExplosionPoint, explosionRadius, explosionGizmoDuration, explosionGizmoColor);

        foreach (var guard in guards)
        {
            float dist = Vector3.Distance(guard.transform.position, explosionPoint);

            if (dist <= explosionRadius)
            {
                // Pide al guardia que se vaya al punto seguro.
                guard.RunAwayPoint(safePoint.position);
            }
        }
    }

    //=========================================================================
    // Dibuja un círculo en el plano XZ usando Debug.DrawLine.
    // Sirve solo para visualización en el modo Play/Scene.
    //=========================================================================
    void DrawKnockCircleDebug(Vector3 center, float radius, float duration, Color color)
    {
        int segments = 36; // Número de segmentos para aproximar el círculo.
        float step = Mathf.PI * 2f / segments;
        Vector3 prev = center + new Vector3(radius, 0f, 0f); // Punto inicial en el extremo derecho.

        for (int i = 1; i <= segments; i++)
        {
            float angle = i * step;
            Vector3 next = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            Debug.DrawLine(prev, next, color, duration); // Dibuja cada segmento del círculo.
            prev = next;
        }
    }

    //=========================================================================
    // Dibuja gizmos en el editor para el radio del knock.
    // Se ejecuta en el editor y en Play a través de OnDrawGizmos.
    //=========================================================================
    void OnDrawGizmos()
    {
        if (!showKnockGizmos)
            return;

        if (!Application.isPlaying)
            return; // Solo dibuja durante la ejecución del juego para evitar valores falsos en edición.

        if (Time.time - lastKnockTime <= knockGizmoDuration)
        {
            Color prev = Gizmos.color;
            Gizmos.color = knockGizmoColor;
            Gizmos.DrawWireSphere(lastKnockPoint, knockRadius); // Dibuja una esfera alrededor del knock.
            Gizmos.color = prev;
        }
    }

    //=========================================================================
    // Dibuja un círculo en el plano XZ para la explosión.
    // Similar a DrawKnockCircleDebug pero con parámetros de explosión.
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

    //=========================================================================
    // Reproduce el sonido del knock y espera a que termine el clip.
    // Se usa StartCoroutine para no bloquear el juego.
    //=========================================================================
    IEnumerator PlayKnock()
    {
        if (knockAudio != null)
        {
            knockAudio.Play();
            yield return new WaitForSeconds(knockAudio.clip.length); // Espera hasta que el sonido termine.
        }
    }

    //=========================================================================
    // Reproduce el sonido de la explosión y espera a que termine el clip.
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

