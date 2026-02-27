# VContainer + MessagePipe — Guía DI para Crimson Draft

> **Versión:** 2026-02-27
> **Stack:** Unity 2D URP · VContainer · MessagePipe · UniTask
> **Referencia oficial:** [VContainer](https://vcontainer.hadashikick.jp/) · [MessagePipe](https://github.com/Cysharp/MessagePipe)

---

## Instalación

### VContainer
Package Manager → Add package by name:
```
com.hadashikick.vcontainer
```
O via OpenUPM:
```
openupm add com.hadashikick.vcontainer
```

### MessagePipe + VContainer bridge
```
openupm add com.cysharp.messagepipe
openupm add com.cysharp.messagepipe.vcontainer
```

---

## 1. VContainer — Project Setup

### Conceptos clave de lifetime

| Lifetime | Comportamiento |
|---|---|
| `Singleton` | Una sola instancia para toda la aplicación. |
| `Scoped` | Una instancia por `LifetimeScope`. Al destruir el scope se llama `Dispose()`. |
| `Transient` | Nueva instancia en cada resolución. Nunca se reutiliza. |

### Boot Scene — GameLifetimeScope

Crea una escena vacía llamada `Boot`. Aquí vive todo lo que debe persistir durante toda la partida.

```csharp
// Assets/Scripts/Scopes/GameLifetimeScope.cs
using VContainer;
using VContainer.Unity;
using MessagePipe;
using UnityEngine;

public class GameLifetimeScope : LifetimeScope
{
    // Exponer referencias de escena arrastrando desde el inspector
    [SerializeField] AudioSource globalAudioSource;

    protected override void Configure(IContainerBuilder builder)
    {
        // --- MessagePipe (DEBE registrarse primero) ---
        var messagePipeOptions = builder.RegisterMessagePipe();

        // Habilita la ventana de diagnóstico en el editor y GlobalMessagePipe
        builder.RegisterBuildCallback(c =>
            GlobalMessagePipe.SetProvider(c.AsServiceProvider()));

        // --- Registrar todos los brokers de eventos del juego ---
        RegisterGameEvents(builder, messagePipeOptions);

        // --- Servicios globales (singleton para toda la partida) ---
        builder.Register<SaveSystem>(Lifetime.Singleton);
        builder.Register<AudioManager>(Lifetime.Singleton);
        builder.Register<GameSettingsService>(Lifetime.Singleton);

        // --- MonoBehaviours globales ---
        builder.RegisterComponent(globalAudioSource);

        // --- Entry point global (recibe IInitializable, ITickable, etc.) ---
        builder.RegisterEntryPoint<GameBootstrap>(Lifetime.Singleton);
    }

    void RegisterGameEvents(IContainerBuilder builder, MessagePipeOptions options)
    {
        // Combate
        builder.RegisterMessageBroker<OnCharacterDamagedEvent>(options);
        builder.RegisterMessageBroker<OnCharacterDiedEvent>(options);
        builder.RegisterMessageBroker<OnCombatStartedEvent>(options);
        builder.RegisterMessageBroker<OnCombatEndedEvent>(options);
        builder.RegisterMessageBroker<OnQTECompletedEvent>(options);

        // Items
        builder.RegisterMessageBroker<OnItemUsedEvent>(options);
        builder.RegisterMessageBroker<OnKrokoniлDoseAppliedEvent>(options);

        // Navegación / guardias
        builder.RegisterMessageBroker<OnGuardAlertChangedEvent>(options);

        // Guardado
        builder.RegisterMessageBroker<OnGameSavedEvent>(options);
    }
}
```

**Configuración en Unity:**
1. Crear `GameObject` vacío en la escena Boot → `Add Component` → `GameLifetimeScope`.
2. En el componente, activar **DontDestroyOnLoad** en el inspector de `LifetimeScope`.
3. Marcar esta escena como la primera en Build Settings.

```csharp
// Assets/Scripts/Bootstrap/GameBootstrap.cs
using VContainer.Unity;
using UnityEngine;

public class GameBootstrap : IInitializable
{
    readonly SaveSystem saveSystem;
    readonly AudioManager audioManager;

    public GameBootstrap(SaveSystem saveSystem, AudioManager audioManager)
    {
        this.saveSystem = saveSystem;
        this.audioManager = audioManager;
    }

    // Se ejecuta antes del primer Start de cualquier MonoBehaviour en la escena
    public void Initialize()
    {
        saveSystem.LoadProfile();
        audioManager.Initialize();
        Debug.Log("[Bootstrap] Crimson Draft initialized.");
    }
}
```

---

### Child Scope por escena

Cada escena de juego crea su propio `LifetimeScope` hijo. El hijo puede resolver todo lo registrado en el padre (GameLifetimeScope) pero el padre nunca ve los servicios del hijo.

```
GameLifetimeScope (DontDestroyOnLoad)
    └── NavigationScope  (escena: Ship_Interior)
            └── CombatScope  (escena: Combat_Room, aditiva)
```

#### NavigationScope

```csharp
// Assets/Scripts/Scopes/NavigationScope.cs
using VContainer;
using VContainer.Unity;

public class NavigationScope : LifetimeScope
{
    // En el inspector del componente LifetimeScope:
    // Parent → asignar el prefab GameLifetimeScope (o dejarlo en auto-detect)

    protected override void Configure(IContainerBuilder builder)
    {
        // Servicios scoped a la escena de navegación
        builder.Register<INavigationService, ShipNavigationService>(Lifetime.Scoped);
        builder.Register<IGuardAlertSystem, GuardAlertSystem>(Lifetime.Scoped);
        builder.Register<IInteractionSystem, InteractionSystem>(Lifetime.Scoped);

        // MonoBehaviours de la escena
        builder.RegisterComponentInHierarchy<ShipMapView>();
        builder.RegisterComponentInHierarchy<GuardPatrolView>();

        // Entry points de la escena de navegación
        builder.RegisterEntryPoint<NavigationPresenter>(Lifetime.Scoped);
        builder.RegisterEntryPoint<GuardAlertPresenter>(Lifetime.Scoped);
    }
}
```

#### Carga de CombatScope como escena aditiva

El `CombatScope` se carga aditivamente sobre la navegación. Usa `EnqueueParent` para que el scope de la escena de combate sea hijo del NavigationScope actual.

```csharp
// Assets/Scripts/Navigation/CombatSceneLoader.cs
using VContainer;
using VContainer.Unity;
using UnityEngine.SceneManagement;
using Cysharp.Threading.Tasks;

public class CombatSceneLoader : IDisposable
{
    readonly LifetimeScope parentScope;

    public CombatSceneLoader(LifetimeScope lifetimeScope)
    {
        parentScope = lifetimeScope;
    }

    public async UniTask LoadCombatAsync(CombatSetupData setupData)
    {
        // Inyectar datos específicos del combate en el scope hijo
        using (LifetimeScope.EnqueueParent(parentScope))
        using (LifetimeScope.Enqueue(builder =>
        {
            // Registrar datos de contexto de este combate concreto
            builder.RegisterInstance(setupData);
        }))
        {
            await SceneManager.LoadSceneAsync("Combat_Room", LoadSceneMode.Additive);
        }
    }

    public async UniTask UnloadCombatAsync()
    {
        await SceneManager.UnloadSceneAsync("Combat_Room");
        // Al destruirse CombatScope, VContainer llama Dispose()
        // en todos los IDisposable registrados en ese scope
    }

    public void Dispose() { }
}
```

---

## 2. VContainer — Registration Examples

### Servicios core del juego

```csharp
// En NavigationScope o GameLifetimeScope según si son globales o por escena

// Interfaz → implementación concreta, singleton durante la partida
builder.Register<IHealthService, CharacterHealthService>(Lifetime.Singleton);
builder.Register<ICombatService, CombatService>(Lifetime.Singleton);
builder.Register<IInventoryService, InventoryService>(Lifetime.Singleton);

// Scoped: una instancia por escena de navegación
builder.Register<IGuardAlertSystem, GuardAlertSystem>(Lifetime.Scoped);
```

```csharp
// Assets/Scripts/Services/CharacterHealthService.cs
public interface IHealthService
{
    void ApplyDamage(CharacterId character, DamageInfo damage);
    HealthState GetState(CharacterId character);
    void ApplyKrokoniлDose(CharacterId character, float dose);
}

public class CharacterHealthService : IHealthService
{
    readonly IPublisher<OnCharacterDamagedEvent> damagedPublisher;
    readonly IPublisher<OnCharacterDiedEvent> diedPublisher;
    readonly IPublisher<OnKrokoniлDoseAppliedEvent> krokoniлPublisher;

    // VContainer inyecta por constructor automáticamente
    public CharacterHealthService(
        IPublisher<OnCharacterDamagedEvent> damagedPublisher,
        IPublisher<OnCharacterDiedEvent> diedPublisher,
        IPublisher<OnKrokoniлDoseAppliedEvent> krokoniлPublisher)
    {
        this.damagedPublisher = damagedPublisher;
        this.diedPublisher = diedPublisher;
        this.krokoniлPublisher = krokoniлPublisher;
    }

    public void ApplyDamage(CharacterId character, DamageInfo damage)
    {
        // ... lógica de HP + blood pressure
        damagedPublisher.Publish(new OnCharacterDamagedEvent(character, damage));

        if (IsDead(character))
            diedPublisher.Publish(new OnCharacterDiedEvent(character));
    }

    public void ApplyKrokoniлDose(CharacterId character, float dose)
    {
        // Krokonil enmascara síntomas pero acumula degradación oculta
        krokoniлPublisher.Publish(new OnKrokoniлDoseAppliedEvent(character, dose));
    }

    public HealthState GetState(CharacterId character) { /* ... */ return default; }
    bool IsDead(CharacterId character) { /* ... */ return false; }
}
```

### Enemigos como Transient

Los enemigos son transient: cada instancia resuelve sus dependencias frescas.

```csharp
// En CombatScope
builder.Register<IEnemy, KrokodilEnemy>(Lifetime.Transient);
builder.Register<IEnemy, GuardEnemy>(Lifetime.Transient);
```

Para instanciar desde código usando DI, inyecta `IObjectResolver`:

```csharp
public class EnemySpawner
{
    readonly IObjectResolver resolver;

    public EnemySpawner(IObjectResolver resolver)
    {
        this.resolver = resolver;
    }

    public IEnemy SpawnEnemy(EnemyType type)
    {
        return type switch
        {
            EnemyType.Guard    => resolver.Resolve<GuardEnemy>(),
            EnemyType.Krokodil => resolver.Resolve<KrokodilEnemy>(),
            _ => throw new System.ArgumentOutOfRangeException()
        };
    }
}
```

### Inyectar dependencias en MonoBehaviours

MonoBehaviours no tienen constructor, por lo que VContainer usa **method injection** vía `[Inject]`.

```csharp
// Assets/Scripts/UI/CombatHUDView.cs
using UnityEngine;
using VContainer;
using MessagePipe;

public class CombatHUDView : MonoBehaviour
{
    // Campos normales de Unity (desde inspector)
    [SerializeField] UnityEngine.UI.Slider healthBar;
    [SerializeField] TMPro.TMP_Text ammoLabel;

    ISubscriber<OnCharacterDamagedEvent> damagedSubscriber;
    DisposableBag subscriptions;

    // VContainer llama este método automáticamente después de Awake
    [Inject]
    public void Construct(ISubscriber<OnCharacterDamagedEvent> damagedSubscriber)
    {
        this.damagedSubscriber = damagedSubscriber;
    }

    void Start()
    {
        var bag = DisposableBag.CreateBuilder();
        damagedSubscriber
            .Subscribe(OnCharacterDamaged)
            .AddTo(bag);
        subscriptions = bag.Build();
    }

    void OnCharacterDamaged(OnCharacterDamagedEvent e)
    {
        // Actualizar UI para el personaje dañado
        if (e.Character == CharacterId.Alpha)
            healthBar.value = e.RemainingHpNormalized;
    }

    void OnDestroy()
    {
        subscriptions.Dispose();
    }
}
```

Registrar el MonoBehaviour en el scope correspondiente:

```csharp
// En CombatScope.Configure()
builder.RegisterComponentInHierarchy<CombatHUDView>();
```

---

## 3. MessagePipe — Setup con VContainer

### Registro (una sola vez, en GameLifetimeScope)

```csharp
protected override void Configure(IContainerBuilder builder)
{
    // 1. Registrar MessagePipe y obtener options
    var options = builder.RegisterMessagePipe(opt =>
    {
        // Modo diagnóstico solo en Editor
        opt.EnableCaptureStackTrace = UnityEngine.Debug.isDebugBuild;
    });

    // 2. Exponer GlobalMessagePipe (útil para acceso desde código legacy o estático)
    builder.RegisterBuildCallback(container =>
        GlobalMessagePipe.SetProvider(container.AsServiceProvider()));

    // 3. Registrar un broker por cada tipo de evento
    builder.RegisterMessageBroker<OnCharacterDamagedEvent>(options);
    builder.RegisterMessageBroker<OnCharacterDiedEvent>(options);
    builder.RegisterMessageBroker<OnCombatStartedEvent>(options);
    builder.RegisterMessageBroker<OnCombatEndedEvent>(options);
    builder.RegisterMessageBroker<OnQTECompletedEvent>(options);
    builder.RegisterMessageBroker<OnItemUsedEvent>(options);
    builder.RegisterMessageBroker<OnKrokoniлDoseAppliedEvent>(options);
    builder.RegisterMessageBroker<OnGuardAlertChangedEvent>(options);
    builder.RegisterMessageBroker<OnGameSavedEvent>(options);
}
```

> **Nota IL2CPP:** Unity con IL2CPP no admite genéricos abiertos. Cada tipo de mensaje debe registrarse explícitamente con `RegisterMessageBroker<T>`. No existe auto-registro.

### Definición de eventos

```csharp
// Assets/Scripts/Events/GameEvents.cs

// Structs livianos — cero GC overhead al publicar
public readonly struct OnCharacterDamagedEvent
{
    public readonly CharacterId Character;
    public readonly DamageInfo Damage;
    public readonly float RemainingHpNormalized;

    public OnCharacterDamagedEvent(CharacterId character, DamageInfo damage, float hp)
    {
        Character = character;
        Damage = damage;
        RemainingHpNormalized = hp;
    }
}

public readonly struct OnCharacterDiedEvent
{
    public readonly CharacterId Character;
    public readonly CauseOfDeath Cause;

    public OnCharacterDiedEvent(CharacterId character, CauseOfDeath cause)
    {
        Character = character;
        Cause = cause;
    }
}

public readonly struct OnCombatStartedEvent
{
    public readonly EnemyGroupData Enemies;
    public readonly RoomId Room;

    public OnCombatStartedEvent(EnemyGroupData enemies, RoomId room)
    {
        Enemies = enemies;
        Room = room;
    }
}

public readonly struct OnQTECompletedEvent
{
    public readonly CharacterId Shooter;
    public readonly WeaponId Weapon;
    public readonly QTEResult Result;   // Hit, Miss, CriticalHit
    public readonly float ReactionTime; // segundos

    public OnQTECompletedEvent(CharacterId shooter, WeaponId weapon, QTEResult result, float reactionTime)
    {
        Shooter = shooter;
        Weapon = weapon;
        Result = result;
        ReactionTime = reactionTime;
    }
}

public readonly struct OnGuardAlertChangedEvent
{
    public readonly GuardId Guard;
    public readonly AlertState Previous;
    public readonly AlertState Current;

    public OnGuardAlertChangedEvent(GuardId guard, AlertState previous, AlertState current)
    {
        Guard = guard;
        Previous = previous;
        Current = current;
    }
}

public readonly struct OnItemUsedEvent
{
    public readonly CharacterId Character;
    public readonly ItemId Item;

    public OnItemUsedEvent(CharacterId character, ItemId item)
    {
        Character = character;
        Item = item;
    }
}

public readonly struct OnKrokoniлDoseAppliedEvent
{
    public readonly CharacterId Character;
    public readonly float Dose;
    public readonly float AccumulatedExposure; // exposición total acumulada oculta

    public OnKrokoniлDoseAppliedEvent(CharacterId character, float dose, float accumulated)
    {
        Character = character;
        Dose = dose;
        AccumulatedExposure = accumulated;
    }
}
```

### Publicar un evento (en un servicio)

```csharp
public class CombatService : ICombatService, IDisposable
{
    readonly IPublisher<OnCombatStartedEvent> combatStartedPublisher;
    readonly IPublisher<OnCombatEndedEvent>   combatEndedPublisher;
    readonly IHealthService healthService;

    public CombatService(
        IPublisher<OnCombatStartedEvent> combatStartedPublisher,
        IPublisher<OnCombatEndedEvent>   combatEndedPublisher,
        IHealthService healthService)
    {
        this.combatStartedPublisher = combatStartedPublisher;
        this.combatEndedPublisher   = combatEndedPublisher;
        this.healthService          = healthService;
    }

    public void StartCombat(EnemyGroupData enemies, RoomId room)
    {
        // Preparar estado interno...
        combatStartedPublisher.Publish(new OnCombatStartedEvent(enemies, room));
    }

    public void Dispose() { }
}
```

### Suscribirse a un evento (en un entry point)

```csharp
// Assets/Scripts/Combat/CombatPresenter.cs
using VContainer.Unity;
using MessagePipe;

public class CombatPresenter : IStartable, IDisposable
{
    readonly ISubscriber<OnCombatStartedEvent>   combatStartedSub;
    readonly ISubscriber<OnCharacterDamagedEvent> damagedSub;
    readonly ISubscriber<OnCharacterDiedEvent>    diedSub;
    readonly ISubscriber<OnQTECompletedEvent>     qteCompletedSub;

    DisposableBag subscriptions;

    public CombatPresenter(
        ISubscriber<OnCombatStartedEvent>   combatStartedSub,
        ISubscriber<OnCharacterDamagedEvent> damagedSub,
        ISubscriber<OnCharacterDiedEvent>    diedSub,
        ISubscriber<OnQTECompletedEvent>     qteCompletedSub)
    {
        this.combatStartedSub  = combatStartedSub;
        this.damagedSub        = damagedSub;
        this.diedSub           = diedSub;
        this.qteCompletedSub   = qteCompletedSub;
    }

    // Se ejecuta en timing equivalente a Start()
    public void Start()
    {
        var bag = DisposableBag.CreateBuilder();

        combatStartedSub
            .Subscribe(OnCombatStarted)
            .AddTo(bag);

        damagedSub
            .Subscribe(OnCharacterDamaged)
            .AddTo(bag);

        diedSub
            .Subscribe(OnCharacterDied)
            .AddTo(bag);

        qteCompletedSub
            .Subscribe(OnQTECompleted)
            .AddTo(bag);

        subscriptions = bag.Build();
    }

    void OnCombatStarted(OnCombatStartedEvent e)
    {
        UnityEngine.Debug.Log($"[Combat] Started in room {e.Room} vs {e.Enemies.Count} enemies");
    }

    void OnCharacterDamaged(OnCharacterDamagedEvent e)
    {
        UnityEngine.Debug.Log($"[Combat] {e.Character} took {e.Damage.Amount} dmg");
    }

    void OnCharacterDied(OnCharacterDiedEvent e)
    {
        UnityEngine.Debug.Log($"[Combat] {e.Character} died ({e.Cause})");
        // Trigger game over o activar lógica de party death
    }

    void OnQTECompleted(OnQTECompletedEvent e)
    {
        UnityEngine.Debug.Log($"[QTE] {e.Shooter} fired {e.Weapon}: {e.Result} in {e.ReactionTime:F2}s");
    }

    // Al destruirse CombatScope, VContainer llama Dispose() automáticamente
    public void Dispose()
    {
        subscriptions.Dispose();
    }
}
```

Registrar en CombatScope:

```csharp
builder.RegisterEntryPoint<CombatPresenter>(Lifetime.Scoped);
```

---

## 4. Scope Architecture para Crimson Draft

```
┌─────────────────────────────────────────────────────┐
│  GameLifetimeScope  (Boot scene, DontDestroyOnLoad) │
│                                                     │
│  SERVICIOS:                                         │
│  · SaveSystem (Singleton)                           │
│  · AudioManager (Singleton)                         │
│  · GameSettingsService (Singleton)                  │
│                                                     │
│  MESSAGEPIPE BROKERS (todos los eventos del juego): │
│  · OnCharacterDamagedEvent                          │
│  · OnCharacterDiedEvent                             │
│  · OnCombatStartedEvent / OnCombatEndedEvent        │
│  · OnQTECompletedEvent                              │
│  · OnItemUsedEvent                                  │
│  · OnKrokoniлDoseAppliedEvent                       │
│  · OnGuardAlertChangedEvent                         │
│  · OnGameSavedEvent                                 │
│                                                     │
│  ENTRY POINTS:                                      │
│  · GameBootstrap (IInitializable)                   │
│                                                     │
│  └───────────────────────────────────────────────┐  │
│  │  NavigationScope  (Ship_Interior scene)        │  │
│  │                                               │  │
│  │  SERVICIOS:                                   │  │
│  │  · INavigationService (Scoped)                │  │
│  │  · IGuardAlertSystem  (Scoped)                │  │
│  │  · IInteractionSystem (Scoped)                │  │
│  │  · IInventoryService  (Scoped)                │  │
│  │  · IHealthService     (Scoped)                │  │
│  │  · CombatSceneLoader  (Scoped)                │  │
│  │                                               │  │
│  │  MONOBEHAVIOURS:                              │  │
│  │  · ShipMapView (RegisterComponentInHierarchy) │  │
│  │  · GuardPatrolView                            │  │
│  │                                               │  │
│  │  ENTRY POINTS:                                │  │
│  │  · NavigationPresenter (IStartable, ITick)    │  │
│  │  · GuardAlertPresenter (IStartable, ITick)    │  │
│  │                                               │  │
│  │  └─────────────────────────────────────────┐ │  │
│  │  │  CombatScope  (Combat_Room, additive)    │ │  │
│  │  │                                         │ │  │
│  │  │  SERVICIOS:                             │ │  │
│  │  │  · ICombatService   (Scoped)            │ │  │
│  │  │  · IQTESystem       (Scoped)            │ │  │
│  │  │  · EnemySpawner     (Scoped)            │ │  │
│  │  │  · IEnemy (Transient — instanciado por  │ │  │
│  │  │            EnemySpawner vía IResolver)  │ │  │
│  │  │                                         │ │  │
│  │  │  CONTEXT DATA (RegisterInstance):       │ │  │
│  │  │  · CombatSetupData (inyectado al cargar)│ │  │
│  │  │                                         │ │  │
│  │  │  MONOBEHAVIOURS:                        │ │  │
│  │  │  · CombatHUDView                        │ │  │
│  │  │  · QTEView                              │ │  │
│  │  │                                         │ │  │
│  │  │  ENTRY POINTS:                          │ │  │
│  │  │  · CombatPresenter (IStartable,         │ │  │
│  │  │                     IDisposable)        │ │  │
│  │  │  · QTEPresenter    (IStartable,         │ │  │
│  │  │                     IDisposable)        │ │  │
│  │  └─────────────────────────────────────────┘ │  │
│  └───────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────┘
```

### Qué vive dónde y por qué

| Sistema | Scope | Razón |
|---|---|---|
| SaveSystem | Game | Necesita persistir toda la partida; guarda/carga entre escenas |
| AudioManager | Game | Continuidad de audio entre escenas; gestión de canales global |
| MessagePipe brokers | Game | Los eventos cruzan scope boundaries (guardia muere → combat termina → nav reactiva) |
| IHealthService | Navigation | Los personajes existen durante la exploración; se recrean si el juego carga de nuevo |
| IInventoryService | Navigation | El inventario es accesible durante exploración y combate (mismo scope) |
| IGuardAlertSystem | Navigation | Los guardias son entidades de la escena de navegación |
| ICombatService | Combat | Solo existe durante la pelea; se destruye al volver a exploración |
| IQTESystem | Combat | Idem |
| IEnemy | Combat (Transient) | Cada enemigo es una instancia independiente |
| CombatSetupData | Combat (Instance) | Datos de contexto específicos de este combate |

### Ciclo de vida al salir del combate

```
1. CombatSceneLoader.UnloadCombatAsync() llama SceneManager.UnloadSceneAsync("Combat_Room")
2. Unity destruye el CombatScope MonoBehaviour
3. VContainer llama IDisposable.Dispose() en orden inverso al registro:
   - QTEPresenter.Dispose() → cancela subscripciones MessagePipe
   - CombatPresenter.Dispose() → cancela subscripciones
   - CombatService.Dispose()
   - IQTESystem.Dispose()
4. NavigationScope continúa vivo — IHealthService, IInventoryService siguen disponibles
5. GuardAlertSystem reactiva patrullas (estado guardado durante el combate)
```

---

## 5. Practical Patterns

### 5.1 Sistema QTE con MessagePipe

El QTE tiene tres fases: aparece el prompt, el jugador presiona el botón, se evalúa el resultado. MessagePipe coordina sin acoplamiento entre el sistema de input, el evaluador y la animación/sfx.

```csharp
// Assets/Scripts/Combat/QTE/QTESystem.cs
using VContainer.Unity;
using MessagePipe;
using Cysharp.Threading.Tasks;
using System.Threading;

public class QTESystem : IQTESystem, IStartable, IDisposable
{
    readonly IPublisher<OnQTECompletedEvent> qtePublisher;
    readonly ISubscriber<OnCombatStartedEvent> combatStartedSub;
    DisposableBag subscriptions;

    // Datos de la ventana QTE actual
    CancellationTokenSource currentQTECts;
    bool qteActive;

    public QTESystem(
        IPublisher<OnQTECompletedEvent> qtePublisher,
        ISubscriber<OnCombatStartedEvent> combatStartedSub)
    {
        this.qtePublisher      = qtePublisher;
        this.combatStartedSub  = combatStartedSub;
    }

    public void Start()
    {
        var bag = DisposableBag.CreateBuilder();
        combatStartedSub.Subscribe(OnCombatStarted).AddTo(bag);
        subscriptions = bag.Build();
    }

    void OnCombatStarted(OnCombatStartedEvent e)
    {
        // Al iniciar combate, comenzar el loop de QTE
        RunQTELoopAsync(UnityEngine.Application.exitCancellationToken).Forget();
    }

    async UniTaskVoid RunQTELoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            await UniTask.WaitForSeconds(GetNextQTEDelay(), cancellationToken: ct);
            await TriggerQTEAsync(GetNextShooter(), GetNextWeapon(), ct);
        }
    }

    async UniTask TriggerQTEAsync(CharacterId shooter, WeaponId weapon, CancellationToken ct)
    {
        qteActive = true;
        currentQTECts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        float startTime = UnityEngine.Time.time;
        float window = GetQTEWindow(weapon); // ventana más corta para armas más rápidas

        // Esperar input del jugador o timeout
        bool pressed = await WaitForPlayerInputAsync(window, currentQTECts.Token);

        float reactionTime = UnityEngine.Time.time - startTime;
        QTEResult result = EvaluateQTE(pressed, reactionTime, window);

        qteActive = false;
        qtePublisher.Publish(new OnQTECompletedEvent(shooter, weapon, result, reactionTime));
    }

    async UniTask<bool> WaitForPlayerInputAsync(float window, CancellationToken ct)
    {
        float elapsed = 0f;
        while (elapsed < window && !ct.IsCancellationRequested)
        {
            // Input sin mouse — solo joystick/teclado
            if (UnityEngine.Input.GetButtonDown("Fire1"))
                return true;
            elapsed += UnityEngine.Time.deltaTime;
            await UniTask.NextFrame(ct);
        }
        return false;
    }

    QTEResult EvaluateQTE(bool pressed, float reactionTime, float window)
    {
        if (!pressed) return QTEResult.Miss;
        if (reactionTime < window * 0.3f) return QTEResult.CriticalHit;
        return QTEResult.Hit;
    }

    float GetQTEWindow(WeaponId weapon) => weapon switch
    {
        WeaponId.P229     => 0.8f,
        WeaponId.MP5      => 0.5f,
        WeaponId.BenelliM4 => 1.2f,
        WeaponId.Mk18     => 0.6f,
        _                 => 1.0f
    };

    float GetNextQTEDelay()       => UnityEngine.Random.Range(1.5f, 3.0f);
    CharacterId GetNextShooter()  => CharacterId.Alpha; // TODO: lógica de turno
    WeaponId GetNextWeapon()      => WeaponId.P229;     // TODO: arma equipada

    public void Dispose()
    {
        currentQTECts?.Cancel();
        currentQTECts?.Dispose();
        subscriptions.Dispose();
    }
}
```

El `CombatService` consume el resultado sin saber nada del input:

```csharp
// En CombatService
readonly ISubscriber<OnQTECompletedEvent> qteCompletedSub;
DisposableBag subscriptions;

public void Start()
{
    var bag = DisposableBag.CreateBuilder();
    qteCompletedSub.Subscribe(OnQTECompleted).AddTo(bag);
    subscriptions = bag.Build();
}

void OnQTECompleted(OnQTECompletedEvent e)
{
    var damage = CalculateDamage(e.Weapon, e.Result);
    // Aplicar daño al enemigo objetivo actual
    currentEnemy.ApplyDamage(damage, e.Result);
}
```

### 5.2 Sistema de alerta de guardias con pub/sub

Los guardias publican cambios de estado. La IA de otros guardias y el sistema de música reactiva son suscriptores independientes.

```csharp
// Assets/Scripts/Navigation/GuardController.cs
using UnityEngine;
using VContainer;
using MessagePipe;

public class GuardController : MonoBehaviour
{
    [SerializeField] float detectionRadius = 5f;

    IPublisher<OnGuardAlertChangedEvent> alertPublisher;
    AlertState currentState = AlertState.Patrol;
    GuardId guardId;

    [Inject]
    public void Construct(
        IPublisher<OnGuardAlertChangedEvent> alertPublisher,
        GuardId id)  // GuardId inyectado como instancia única por guardia
    {
        this.alertPublisher = alertPublisher;
        this.guardId        = id;
    }

    void Update()
    {
        AlertState newState = EvaluateAlertState();
        if (newState != currentState)
            TransitionTo(newState);
    }

    void TransitionTo(AlertState newState)
    {
        var previous = currentState;
        currentState = newState;
        alertPublisher.Publish(new OnGuardAlertChangedEvent(guardId, previous, newState));
    }

    AlertState EvaluateAlertState()
    {
        // Lógica de detección sin acoplamiento con otros sistemas
        float distToPlayer = Vector3.Distance(transform.position, GetPlayerPosition());
        if (distToPlayer < detectionRadius * 0.3f) return AlertState.Alert;
        if (distToPlayer < detectionRadius)         return AlertState.Suspicious;
        return AlertState.Patrol;
    }

    Vector3 GetPlayerPosition() => Vector3.zero; // TODO: inyectar IPlayerLocator
}
```

```csharp
// Assets/Scripts/Navigation/GuardAlertSystem.cs — coordina reacciones globales
using VContainer.Unity;
using MessagePipe;

public class GuardAlertSystem : IStartable, IDisposable
{
    readonly ISubscriber<OnGuardAlertChangedEvent> alertSub;
    readonly IPublisher<OnCombatStartedEvent>      combatStartedPublisher;
    readonly Dictionary<GuardId, AlertState>       guardStates = new();
    DisposableBag subscriptions;

    public GuardAlertSystem(
        ISubscriber<OnGuardAlertChangedEvent> alertSub,
        IPublisher<OnCombatStartedEvent> combatStartedPublisher)
    {
        this.alertSub               = alertSub;
        this.combatStartedPublisher = combatStartedPublisher;
    }

    public void Start()
    {
        var bag = DisposableBag.CreateBuilder();
        alertSub.Subscribe(OnGuardAlertChanged).AddTo(bag);
        subscriptions = bag.Build();
    }

    void OnGuardAlertChanged(OnGuardAlertChangedEvent e)
    {
        guardStates[e.Guard] = e.Current;

        UnityEngine.Debug.Log($"[Guard {e.Guard}] {e.Previous} → {e.Current}");

        // Si algún guardia llega a Alert, iniciar combate
        if (e.Current == AlertState.Alert)
            TriggerCombat(e.Guard);

        // Propagar alerta a guardias cercanos (chain reaction)
        if (e.Current >= AlertState.Suspicious)
            AlertNearbyGuards(e.Guard);
    }

    void TriggerCombat(GuardId triggeringGuard)
    {
        var enemies = BuildEnemyGroup(triggeringGuard);
        combatStartedPublisher.Publish(new OnCombatStartedEvent(enemies, GetCurrentRoom()));
    }

    void AlertNearbyGuards(GuardId sourceGuard)
    {
        // Los otros GuardControllers recibirán la señal vía otro canal
        // o mediante un método directo en el GuardPatrolView
    }

    EnemyGroupData BuildEnemyGroup(GuardId g) => new EnemyGroupData(); // TODO
    RoomId GetCurrentRoom()                   => RoomId.Corridor_A;    // TODO

    public void Dispose() => subscriptions.Dispose();
}
```

Registrar `GuardController` (MonoBehaviour) en NavigationScope:

```csharp
// NavigationScope.cs
// Para guardias que ya existen en la escena:
builder.RegisterComponentInHierarchy<GuardController>();

// Si hay múltiples guardias con prefabs:
// Usar IObjectResolver.Instantiate al spawnear cada guardia
```

### 5.3 Krokonil — suscriptor de efectos encubiertos

Krokonil enmascara síntomas pero acumula exposición oculta. El sistema de salud publica el evento; un servicio separado lleva la cuenta oculta.

```csharp
// Assets/Scripts/Health/KrokoniлExposureTracker.cs
using VContainer.Unity;
using MessagePipe;

public class KrokoniлExposureTracker : IStartable, IDisposable
{
    readonly ISubscriber<OnKrokoniлDoseAppliedEvent> doseSub;
    readonly ISubscriber<OnCharacterDiedEvent>       diedSub;
    readonly Dictionary<CharacterId, float> hiddenExposure = new();

    DisposableBag subscriptions;

    public KrokoniлExposureTracker(
        ISubscriber<OnKrokoniлDoseAppliedEvent> doseSub,
        ISubscriber<OnCharacterDiedEvent> diedSub)
    {
        this.doseSub = doseSub;
        this.diedSub = diedSub;
    }

    public void Start()
    {
        var bag = DisposableBag.CreateBuilder();
        doseSub.Subscribe(OnDoseApplied).AddTo(bag);
        diedSub.Subscribe(OnCharacterDied).AddTo(bag);
        subscriptions = bag.Build();
    }

    void OnDoseApplied(OnKrokoniлDoseAppliedEvent e)
    {
        // Acumular exposición real — NO visible en UI salvo indicador sutil
        hiddenExposure.TryGetValue(e.Character, out float current);
        hiddenExposure[e.Character] = current + e.Dose;

        float total = hiddenExposure[e.Character];

        if (total > 80f)
            UnityEngine.Debug.LogWarning($"[Krokonil] {e.Character} approaching critical exposure ({total:F1})");
    }

    void OnCharacterDied(OnCharacterDiedEvent e)
    {
        // Si el personaje murió con alta exposición a Krokonil,
        // revelar que el anti-permadeath item no funcionará en el próximo intento
        if (hiddenExposure.TryGetValue(e.Character, out float exposure) && exposure > 60f)
        {
            UnityEngine.Debug.Log($"[Krokonil] {e.Character} died with {exposure:F1} exposure — revival compromised");
            // Marcar degradación para el sistema de anti-permadeath
        }
    }

    public float GetExposure(CharacterId character) =>
        hiddenExposure.TryGetValue(character, out float v) ? v : 0f;

    public void Dispose() => subscriptions.Dispose();
}
```

---

## 6. Anti-Patterns a Evitar

### No usar `FindObjectOfType` dentro de servicios DI

```csharp
// MAL — rompe el grafo de dependencias
public class CombatService
{
    void BadMethod()
    {
        var hud = UnityEngine.Object.FindObjectOfType<CombatHUDView>(); // NUNCA
    }
}

// BIEN — inyectar la interfaz directamente
public class CombatService
{
    readonly ICombatHUD hud;
    public CombatService(ICombatHUD hud) { this.hud = hud; }
}
```

### No suscribirse sin deregistrar

```csharp
// MAL — memory leak si el GameObject se destruye antes que el scope
void Start()
{
    subscriber.Subscribe(OnEvent); // sin guardar el IDisposable
}

// BIEN
DisposableBag subscriptions;
void Start()
{
    var bag = DisposableBag.CreateBuilder();
    subscriber.Subscribe(OnEvent).AddTo(bag);
    subscriptions = bag.Build();
}
void OnDestroy() => subscriptions.Dispose();
```

### No registrar MonoBehaviours con Lifetime.Singleton desde un child scope

```csharp
// MAL — el singleton sobrevive al scope que lo creó; el MonoBehaviour será destruido
// pero la referencia seguirá en el contenedor padre
builder.RegisterComponentInHierarchy<GuardController>(Lifetime.Singleton);

// BIEN — usar Scoped para MonoBehaviours en escenas
// RegisterComponentInHierarchy siempre es Scoped por diseño de VContainer
builder.RegisterComponentInHierarchy<GuardController>(); // implícitamente Scoped
```

### No publicar desde el constructor

```csharp
// MAL — el contenedor puede no haber terminado de construirse
public class BadService
{
    public BadService(IPublisher<SomeEvent> pub)
    {
        pub.Publish(new SomeEvent()); // potencial NullReference o estado inconsistente
    }
}

// BIEN — publicar en IStartable.Start() o IInitializable.Initialize()
public class GoodService : IStartable
{
    readonly IPublisher<SomeEvent> pub;
    public GoodService(IPublisher<SomeEvent> pub) { this.pub = pub; }
    public void Start() { pub.Publish(new SomeEvent()); }
}
```

### No resolver servicios del scope padre desde código estático

```csharp
// MAL — GlobalMessagePipe es un escape hatch; usarlo como primera opción
// acopla código que debería ser inyectado
var pub = GlobalMessagePipe.GetPublisher<SomeEvent>(); // solo para código legacy

// BIEN — inyectar IPublisher<T> directamente por constructor o [Inject]
```

---

## Enums y tipos de soporte

```csharp
// Assets/Scripts/Core/GameTypes.cs

public enum CharacterId { Alpha, Bravo, Charlie, CIAOperative }
public enum WeaponId    { P229, MP5, BenelliM4, Mk18 }
public enum AlertState  { Patrol, Suspicious, Alert }
public enum QTEResult   { Miss, Hit, CriticalHit }
public enum CauseOfDeath { BleedOut, CardiacArrest, KrokoniлOverdose, Executed }

public readonly struct GuardId
{
    public readonly int Value;
    public GuardId(int value) { Value = value; }
    public override string ToString() => $"Guard_{Value}";
}

public readonly struct RoomId
{
    public readonly string Name;
    public RoomId(string name) { Name = name; }
    public static readonly RoomId Corridor_A = new RoomId("Corridor_A");
}

public readonly struct DamageInfo
{
    public readonly float Amount;
    public readonly float BloodPressureImpact;
    public readonly bool  CausesHemorrhage;

    public DamageInfo(float amount, float bpImpact, bool hemorrhage)
    {
        Amount              = amount;
        BloodPressureImpact = bpImpact;
        CausesHemorrhage    = hemorrhage;
    }
}

public class EnemyGroupData
{
    public int Count;
    public EnemyType[] Types;
}

public class CombatSetupData
{
    public EnemyGroupData Enemies;
    public RoomId Room;
    public bool IsAmbush;
}

public enum EnemyType { Guard, ArmoredGuard, Krokodil }
public struct HealthState
{
    public float HP;
    public float BloodPressure;
    public float KrokoniлExposure;
    public bool IsAlive;
}
```

---

## Referencias

- [VContainer — About](https://vcontainer.hadashikick.jp/)
- [VContainer — Hello World](https://vcontainer.hadashikick.jp/getting-started/hello-world)
- [VContainer — Scoping: generate child via scene](https://vcontainer.hadashikick.jp/scoping/generate-child-via-scene)
- [VContainer — Lifetime Overview](https://vcontainer.hadashikick.jp/scoping/lifetime-overview)
- [VContainer — Register MonoBehaviour](https://vcontainer.hadashikick.jp/registering/register-monobehaviour)
- [VContainer — Injecting into GameObjects](https://vcontainer.hadashikick.jp/resolving/gameobject-injection)
- [MessagePipe — GitHub](https://github.com/Cysharp/MessagePipe)
- [MessagePipe.VContainer — OpenUPM](https://openupm.com/packages/com.cysharp.messagepipe.vcontainer/)
- [MessagePipe Unity Integration — DeepWiki](https://deepwiki.com/Cysharp/MessagePipe/5.2-unity-integration)
- [VContainer — DeepWiki](https://deepwiki.com/hadashiA/VContainer)
