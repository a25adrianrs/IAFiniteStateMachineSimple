# NUEVA ESCENA DE IAFINITESTATEMACHINESIMPLE

Para este ejercicio creé una escena a parte de la que sale en el ejemplo, y modifique los scripts 
de **PlayerController** y **GuardController**.

## Modificaciones y Añadidos realizados.


- Creé un EmptyGameObject llamado **SafePoint** el cúal posicione manualmente en un punto de la escena, el cúal sera el punto
  donde se reunirán los Guardias cuando se lanze la explosión.
- ## PlayerControler
  - Cree campos para dibujar en la Escena el radio de la explosión basandome en los previamente existentes de **knock**
    ``` [Header("Explosion Settings")]```
  
   ``` public float explosionRadius = 50.0f;```

  ```public bool showExplosionGizmos = true;```

  ```public float explosionGizmoDuration = 1.5f;```
  
    - Comente el **RequireComponent** para el AudioSource y los substituí por dos campos serializables dentro de la clase para poder disponer         tanto del Audio de **knock** como el de **explosion**.
      
      ```[Header("Audio Sources")]```
      
      ``` [SerializeField] private AudioSource knockAudio;```
  
      ``` [SerializeField] private AudioSource explosionAudio; ```
      
  - Tambien añadi otro campo serializable para obtener la posición del EmptyObject que voy a usar como punto seguro de reunión.
    ```[Header("Punto Seguro")]```
    
    ```[SerializeField] private Transform safePoint;```

    - Tanto en el **newInput** como en el **oldInput** añadi las dos opciones de teclado
        ```csharp
         if (Input.GetKeyDown(KeyCode.E))
        {
            Debug.Log(" OldInput E pressed");
            HandleEAction();
        }
        ```
        ```csharp
        if (Keyboard.current.eKey.wasPressedThisFrame)
            {
                Debug.Log("NewInput E pressed");
                HandleEAction();
            }
        ```

     - Creé un metodo **HandleEAction** y modificando el codigo del metodo ya existente **HandleSpaceAction**, al empezar ***llamo a la             corutina que lanza el sonido de explosion*** y en vez de pasar una posición para que los guardas busquen al jugador, se le pasa la              posición del **safePoint** al que iran todos cuando pulse la tecla **E** si estos estan dentro del radio de la explosión, si es así se lanzara el metodo **RunaWayPoint** que esta definido en el **GuardController** y se ocupara que los guardias se dirigan al punto segúro.
```csharp
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
```

 

- Hice una copia del metodo que se usa para dibujar el circulo del knock pero adaptado a la explosión y asi poder controlar que se ejecutaba correctamente.
```csharp
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
```

- Por ultimo debido a que añadi un nuevo sonido para la explosión y no queria eliminar el AudioSource que ya existia modifique ambas Corutinas para así disponer del sonido de ambos sonidos dependiendo de que tecla se pulsase pudiendo seleccionar entre los dos Audios.

```csharp
 IEnumerator PlayKnock()
    {
        if (knockAudio != null)
        {
            knockAudio.Play();
            yield return new WaitForSeconds(knockAudio.clip.length);
        }
    }

IEnumerator PlayExplosion()
    {
        if (explosionAudio != null)
        {
            explosionAudio.Play();
            yield return new WaitForSeconds(explosionAudio.clip.length);
        }
    }
  ```

- # GuardController
  - Añadi a los estados un estado nuevo llamado **RunaWay**.
  - Tambien creé una variable de Vector3 llamada **escapePoint**.
  - Por ultimo añadi otra variable **runaWayStartTime** la cual voy a usar para controlar el tiempo de los guardias cuando empiecen a escapar al punto , una vez pasado ese tiempo su estado volvera a ser **Patrol**.

    Dentro del Update añadi el nuevo estado:
    ```csharp
     case State.RunaWay:
                
                RunaWay();
                break;
    ```
    
- Creé el metodo **RunaWay()** el cual se lanza cuando los guardias cambian de estado, basicamente una vez entra en el estado se empieza a contar el tiempo , al pasar en este caso 10 segundos automaticamente los guardias vuelven a el estado **Patrol**, si no cambiaran hasta que llegen al punto seguro:
  ```csharp
  void RunaWay()
    {
        // Logica de huida
        var agent = GetComponent<UnityEngine.AI.NavMeshAgent>();

        if (Time.time - runaWayStartTime > 10f)
        {
            currentState = State.Patrol;
            return;
        }
        if (Vector3.Distance(transform.position, escapePoint) <= agent.stoppingDistance + 0.5f)
        {
            currentState = State.Patrol;
            return;
        }
        // Hacemos que el agente vaya al punto de escape
        agent.SetDestination(escapePoint);
    }
  ```
- Para entrar en el estado **RunaWay** se hace a traves del metodo **RunaWayPoint** el cual se ejecuta desde el PlayerController:
  ```csharp
   public void RunAwayPoint(Vector3 point) // Ordena a los guardias huir a un punto específico
    {
        escapePoint = point; // Establece o punto a huir
        currentState = State.RunaWay; // Cambia a estado RunaWay
        runaWayStartTime = Time.time; // Empieza a contar el tiempo de huida
    }
  ```
