# Unity Game Programming Patterns Reference

> Structured reference based on Unity's "Level Up Your Code with Game Programming Patterns" (2022 LTS Edition).
> Adapted for projects using **VContainer** (DI) + **MessagePipe** (events) instead of raw singletons or Zenject.

---

# SOLID Principles

## Single Responsibility Principle (SRP)

**Rule:** A class should have one reason to change -- just its single responsibility.

Build your projects from many smaller components instead of monolithic classes. Shorter classes and methods are easier to explain, understand, and implement.

```csharp
// BAD: One class handles input, movement, and audio
public class UnrefactoredPlayer : MonoBehaviour
{
    private AudioSource bounceSfx;
    private void Update()
    {
        float delta = Input.GetAxis("Horizontal") * Time.deltaTime;
        transform.position += new Vector3(0, delta, 0);
    }
    private void OnTriggerEnter(Collider other) { bounceSfx.Play(); }
}

// GOOD: Separate concerns into focused components
[RequireComponent(typeof(PlayerAudio), typeof(PlayerInput), typeof(PlayerMovement))]
public class Player : MonoBehaviour
{
    [SerializeField] private PlayerAudio playerAudio;
    [SerializeField] private PlayerInput playerInput;
    [SerializeField] private PlayerMovement playerMovement;
}
```

**Guidelines:**
- Keep classes under 200-300 lines
- Design classes to be small, modular, and reusable
- Don't oversimplify to the extreme by creating classes with just one method

**VContainer note:** VContainer's container registration naturally encourages SRP. Each service is a focused, injectable class. MonoBehaviour components remain small by delegating logic to injected services.

---

## Open-Closed Principle (OCP)

**Rule:** Classes must be open for extension but closed for modification.

Use abstractions (abstract classes/interfaces) so new behavior can be added without modifying existing code.

```csharp
// Define abstraction
public abstract class Shape
{
    public abstract float CalculateArea();
}

// Extend without modifying AreaCalculator
public class Rectangle : Shape
{
    public float width, height;
    public override float CalculateArea() => width * height;
}

public class AreaCalculator
{
    public float GetArea(Shape shape) => shape.CalculateArea();
}
```

**VContainer note:** Register abstractions in the container. New implementations can be swapped by changing registration, not by modifying consumers.

---

## Liskov Substitution Principle (LSP)

**Rule:** Derived classes must be substitutable for their base class without breaking the application.

If removing features when subclassing (throwing `NotImplementedException` or leaving methods blank), you're violating LSP.

```csharp
// BAD: Train inherits from Vehicle but can't TurnLeft/TurnRight
// GOOD: Use interfaces to compose capabilities
public interface ITurnable { void TurnRight(); void TurnLeft(); }
public interface IMovable { void GoForward(); void Reverse(); }

public class RoadVehicle : IMovable, ITurnable { /* implements all */ }
public class RailVehicle : IMovable { /* only forward/reverse */ }
```

**Key insight:** Favor composition over inheritance. Classifications in reality don't always translate into class hierarchy. Let software design drive your hierarchy.

**VContainer note:** Register by interface (`builder.Register<IMovable, RoadVehicle>(...)`) so consumers depend on abstractions. Substituting implementations becomes a one-line registration change.

---

## Interface Segregation Principle (ISP)

**Rule:** No client should be forced to depend on methods it does not use. Keep interfaces compact and focused.

```csharp
// BAD: One monolithic interface
public interface IUnitStats
{
    float Health { get; set; }
    void Die();
    float MoveSpeed { get; set; }
    void GoForward();
    int Strength { get; set; }
    // ... 15+ members
}

// GOOD: Small, composable interfaces
public interface IDamageable { float Health { get; set; } void TakeDamage(); void Die(); }
public interface IMovable { float MoveSpeed { get; set; } void GoForward(); }
public interface IExplodable { void Explode(); }

// Compose only what's needed
public class ExplodingBarrel : MonoBehaviour, IDamageable, IExplodable { }
public class EnemyUnit : MonoBehaviour, IDamageable, IMovable { }
```

**VContainer note:** Small interfaces map directly to focused VContainer registrations. A service can implement multiple small interfaces and be registered as each one.

---

## Dependency Inversion Principle (DIP)

**Rule:** High-level modules should not depend on low-level modules. Both should depend on abstractions.

```csharp
// BAD: Switch depends directly on Door
public class Switch : MonoBehaviour
{
    public Door door; // tight coupling
    public void Toggle() { door.Open(); }
}

// GOOD: Abstraction in between
public interface ISwitchable
{
    bool IsActive { get; }
    void Activate();
    void Deactivate();
}

public class Switch : MonoBehaviour
{
    public ISwitchable client; // depends on abstraction
    public void Toggle()
    {
        if (client.IsActive) client.Deactivate();
        else client.Activate();
    }
}

public class Door : MonoBehaviour, ISwitchable { /* implements */ }
public class TrapDoor : MonoBehaviour, ISwitchable { /* implements */ }
```

**VContainer note:** This is DIP's natural home. VContainer IS dependency inversion -- the container resolves abstractions to concrete types. `builder.Register<ISwitchable, Door>(Lifetime.Scoped)` is the textbook implementation.

---

## Interfaces vs Abstract Classes (Quick Reference)

| Feature | Abstract Class | Interface |
|---|---|---|
| Method implementation | Fully or partially | Declarations only (C# 8+ allows defaults) |
| Fields & constants | Yes | No (properties only) |
| Static members | Yes | No |
| Constructors | Yes | No |
| Access modifiers | All (protected, private, etc.) | All public implicitly |
| Multiple inheritance | One base class max | Multiple interfaces |
| Best for | Shared core functionality ("is a") | Composable capabilities ("has a") |

---

# Design Patterns

## Factory Pattern

**Category:** Creational
**When to use:** When you need to spawn many different product types at runtime, each with its own initialization logic.
**When NOT to use:** When you have few product types or simple instantiation with no custom setup.

### Problem it solves
Creating objects at runtime without cluttering the caller with product-specific creation details. Adding new product types without modifying existing creation code.

### How it works (Unity)

Define an `IProduct` interface and an abstract `Factory`. Each concrete factory knows how to instantiate its specific product Prefab.

```csharp
public interface IProduct
{
    string ProductName { get; set; }
    void Initialize();
}

public abstract class Factory : MonoBehaviour
{
    public abstract IProduct GetProduct(Vector3 position);
}

public class ConcreteFactoryA : Factory
{
    [SerializeField] private ProductA productPrefab;

    public override IProduct GetProduct(Vector3 position)
    {
        GameObject instance = Instantiate(productPrefab.gameObject, position, Quaternion.identity);
        ProductA product = instance.GetComponent<ProductA>();
        product.Initialize();
        return product;
    }
}
```

### Pros
- Adding new product types doesn't change existing code (OCP)
- Each product's internal logic is self-contained (SRP)
- Factories can be swapped at runtime

### Cons
- Introduces many classes and subclasses
- Overhead may be unnecessary for a small variety of products

### VContainer/MessagePipe adaptation
VContainer can replace or complement factories:
- **Simple case:** Register a factory method: `builder.Register<IProduct>(container => new ProductA(...), Lifetime.Transient)`
- **Complex case:** Create a factory class that receives dependencies via constructor injection, then register the factory itself: `builder.Register<IProductFactory, ProductFactory>(Lifetime.Singleton)`
- The factory pattern **complements** DI. VContainer handles wiring; the factory handles runtime creation decisions.

---

## Object Pool

**Category:** Creational / Optimization
**When to use:** When instantiating and destroying many GameObjects causes GC spikes (projectiles, particles, enemies).
**When NOT to use:** When you have few objects or infrequent creation/destruction.

### Problem it solves
Frequent `Instantiate`/`Destroy` calls cause garbage collection spikes that stutter gameplay. Pre-allocating and recycling objects eliminates this.

### How it works (Unity)

Unity 2021+ provides `UnityEngine.Pool.ObjectPool<T>` built-in:

```csharp
using UnityEngine.Pool;

public class Gun : MonoBehaviour
{
    [SerializeField] private Projectile projectilePrefab;
    private IObjectPool<Projectile> objectPool;

    private void Awake()
    {
        objectPool = new ObjectPool<Projectile>(
            createFunc: () => { var p = Instantiate(projectilePrefab); p.Pool = objectPool; return p; },
            actionOnGet: p => p.gameObject.SetActive(true),
            actionOnRelease: p => p.gameObject.SetActive(false),
            actionOnDestroy: p => Destroy(p.gameObject),
            defaultCapacity: 20,
            maxSize: 100
        );
    }

    public void Fire() { var p = objectPool.Get(); /* position, aim */ }
}
```

Call `objectPool.Get()` instead of `Instantiate`. Call `objectPool.Release(obj)` instead of `Destroy`.

### Pros
- Eliminates GC spikes from frequent allocation/deallocation
- Built-in `UnityEngine.Pool` API removes need for custom implementation
- Can set max size to cap memory usage

### Cons
- Pre-allocated objects consume memory even when idle
- Requires careful lifecycle management (don't release objects still in use)
- Pool sizing needs tuning per use case

### VContainer/MessagePipe adaptation
- Object pools **complement** DI. Register the pool itself as a singleton service: `builder.Register<IObjectPool<Projectile>>(container => new ObjectPool<Projectile>(...), Lifetime.Singleton)`
- Consumers receive the pool via constructor injection instead of finding it via static access
- MessagePipe can publish events when pools run low or objects are returned

---

## Singleton Pattern

**Category:** Creational
**When to use:** When you need exactly one instance of a manager-level object with global access.
**When NOT to use:** In almost all cases when using VContainer. DI replaces singleton's two purposes (single instance + global access).

### Problem it solves
Ensures only one instance of a class exists and provides global access to it.

### How it works (Unity)

```csharp
public class Singleton<T> : MonoBehaviour where T : Component
{
    private static T instance;
    public static T Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<T>();
                if (instance == null) SetupInstance();
            }
            return instance;
        }
    }

    private void Awake()
    {
        if (instance == null) { instance = this as T; DontDestroyOnLoad(gameObject); }
        else Destroy(gameObject);
    }
}
```

### Pros
- Quick to learn and implement
- User-friendly: access via `MyManager.Instance` from anywhere

### Cons
- Hides dependencies, making bugs hard to trace
- Makes unit testing difficult (shared mutable state)
- Encourages tight coupling, violating DIP
- Considered an anti-pattern by many

### VContainer/MessagePipe adaptation
**VContainer fully replaces the Singleton pattern:**

```csharp
// Instead of: GameManager.Instance.DoThing()
// Register as singleton in a LifetimeScope:
builder.Register<GameManager>(Lifetime.Singleton);

// Or for MonoBehaviours already in scene:
builder.RegisterComponentInHierarchy<AudioManager>();

// Consumers receive it via constructor injection:
public class MyService
{
    private readonly GameManager gameManager;
    public MyService(GameManager gameManager) { this.gameManager = gameManager; }
}
```

- **Single instance:** `Lifetime.Singleton` guarantees one instance per container
- **Global access:** Replaced by constructor injection -- dependencies are explicit, testable, and decoupled
- **Persistence:** `GameLifetimeScope` with `DontDestroyOnLoad` handles cross-scene lifetime
- **DO NOT use raw singletons** in a VContainer project

---

## Command Pattern

**Category:** Behavioral
**When to use:** When you need undo/redo, action replay, input buffering, combo detection, or turn-based action queuing.
**When NOT to use:** When actions are fire-and-forget with no need for history or replay.

### Problem it solves
Encapsulates method calls as objects, enabling them to be stored, queued, undone, or replayed. Decouples the invoker from the receiver.

### How it works (Unity)

```csharp
public interface ICommand
{
    void Execute();
    void Undo();
}

public class MoveCommand : ICommand
{
    private PlayerMover playerMover;
    private Vector3 movement;

    public MoveCommand(PlayerMover player, Vector3 moveVector)
    {
        playerMover = player;
        movement = moveVector;
    }

    public void Execute() => playerMover.Move(movement);
    public void Undo() => playerMover.Move(-movement);
}

public class CommandInvoker
{
    private Stack<ICommand> undoStack = new Stack<ICommand>();

    public void ExecuteCommand(ICommand command)
    {
        command.Execute();
        undoStack.Push(command);
    }

    public void UndoCommand()
    {
        if (undoStack.Count > 0) undoStack.Pop().Undo();
    }
}
```

### Pros
- Enables undo/redo by storing command history
- Useful for replay systems and combo detection
- Adding new commands doesn't affect existing ones (OCP)

### Cons
- Introduces extra classes and interfaces per action type
- Overhead for simple fire-and-forget actions

### VContainer/MessagePipe adaptation
The Command pattern **complements** DI:
- Register `CommandInvoker` as a singleton service: `builder.Register<CommandInvoker>(Lifetime.Singleton)`
- Inject it into services that need to execute or undo commands
- Commands themselves are transient objects created at runtime (not registered in the container)
- MessagePipe can publish events when commands execute/undo for UI updates

---

## State Pattern

**Category:** Behavioral
**When to use:** When an object has many states with distinct behaviors and transitions (player controller, AI, animation).
**When NOT to use:** When you have only 2-3 simple states -- a basic enum + switch may suffice.

### Problem it solves
Eliminates growing switch/if chains for state-dependent behavior. Each state is an independent class with its own Enter/Update/Exit logic.

### How it works (Unity)

```csharp
public interface IState
{
    void Enter();
    void Update();
    void Exit();
}

[Serializable]
public class StateMachine
{
    public IState CurrentState { get; private set; }

    public void Initialize(IState startingState)
    {
        CurrentState = startingState;
        startingState.Enter();
    }

    public void TransitionTo(IState nextState)
    {
        CurrentState.Exit();
        CurrentState = nextState;
        nextState.Enter();
    }

    public void Update() => CurrentState?.Update();
}

public class IdleState : IState
{
    private PlayerController player;
    public IdleState(PlayerController player) { this.player = player; }

    public void Enter() { /* set idle animation */ }
    public void Update() { /* check transition conditions */ }
    public void Exit() { /* cleanup */ }
}
```

### Pros
- Each state is self-contained (SRP) and testable in isolation
- Adding new states doesn't affect existing ones (OCP)
- Pairs naturally with Unity's Animator
- Useful for AI behavior (patrol/chase/attack/flee)

### Cons
- Overkill for objects with few states
- Each state is a separate class, increasing file count

### VContainer/MessagePipe adaptation
The State pattern **complements** DI:
- The `StateMachine` itself can be registered in VContainer if it needs to be shared
- Use MessagePipe to publish state-change events: `publisher.Publish(new StateChangedEvent(oldState, newState))`
- Observers (UI, audio, animation) subscribe to state changes without the StateMachine knowing about them

---

## Observer Pattern

**Category:** Behavioral
**When to use:** When objects need to react to events without tight coupling (UI updates, achievements, audio triggers).
**When NOT to use:** When only one object ever needs to know about a change -- direct method calls are simpler.

### Problem it solves
Allows objects to communicate with a one-to-many dependency while staying loosely coupled. The subject broadcasts; observers listen and respond independently.

### How it works (Unity)

Using C# events (built-in observer pattern):

```csharp
// Subject (publisher)
public class Subject : MonoBehaviour
{
    public event Action ThingHappened;
    public void DoThing() => ThingHappened?.Invoke();
}

// Observer (subscriber)
public class Observer : MonoBehaviour
{
    [SerializeField] private Subject subjectToObserve;

    private void Awake() => subjectToObserve.ThingHappened += OnThingHappened;
    private void OnDestroy() => subjectToObserve.ThingHappened -= OnThingHappened;

    private void OnThingHappened() { Debug.Log("Observer responds"); }
}
```

**Critical:** Always unsubscribe in `OnDestroy` to avoid null reference errors.

### Pros
- Decouples subject from observers
- Built into C# (`event`, `Action`, `Action<T>`)

### Cons
- Observers still need a reference to the subject's class
- Must manage subscription lifecycle carefully (memory leaks)

### VContainer/MessagePipe adaptation
**MessagePipe fully replaces the Observer pattern:**

```csharp
// Define the event as a struct
public struct EnemyDestroyedEvent
{
    public int EnemyId;
    public Vector3 Position;
}

// Publisher (inject IPublisher<T>)
public class EnemyService
{
    private readonly IPublisher<EnemyDestroyedEvent> publisher;

    public EnemyService(IPublisher<EnemyDestroyedEvent> publisher)
    {
        this.publisher = publisher;
    }

    public void DestroyEnemy(int id, Vector3 pos)
    {
        publisher.Publish(new EnemyDestroyedEvent { EnemyId = id, Position = pos });
    }
}

// Subscriber (inject ISubscriber<T>)
public class ScoreService : IDisposable
{
    private readonly IDisposable subscription;

    public ScoreService(ISubscriber<EnemyDestroyedEvent> subscriber)
    {
        subscription = subscriber.Subscribe(e => AddScore(e.EnemyId));
    }

    public void Dispose() => subscription.Dispose();
}
```

**Advantages over raw C# events:**
- No direct reference between publisher and subscriber (fully decoupled)
- Automatic lifecycle management when using VContainer scopes
- Type-safe event routing via generics
- Register: `builder.RegisterMessageBroker<EnemyDestroyedEvent>(options)`

---

## Model View Presenter (MVP)

**Category:** Architectural
**When to use:** When building UI systems that need separation of data, logic, and presentation.
**When NOT to use:** For simple scripts or non-UI systems where the overhead of three layers isn't justified.

### Problem it solves
Separates application data (Model), display (View), and logic (Presenter) to avoid spaghetti code.

### How it works (Unity)

```csharp
// Model: pure data + change notification
public class Health : MonoBehaviour
{
    public event Action HealthChanged;
    private int currentHealth;
    public int CurrentHealth => currentHealth;
    public int MaxHealth => 100;

    public void Decrement(int amount)
    {
        currentHealth = Mathf.Clamp(currentHealth - amount, 0, MaxHealth);
        HealthChanged?.Invoke();
    }
}

// Presenter: mediates between Model and View
public class HealthPresenter : MonoBehaviour
{
    [SerializeField] private Health health;
    [SerializeField] private Slider healthSlider;

    private void Start() => health.HealthChanged += UpdateView;
    private void OnDestroy() => health.HealthChanged -= UpdateView;

    public void Damage(int amount) => health.Decrement(amount);

    private void UpdateView()
    {
        healthSlider.value = (float)health.CurrentHealth / health.MaxHealth;
    }
}
```

**Flow:** User input -> View -> Presenter -> Model -> (event) -> Presenter -> View

### Pros
- Smooth division of work (UI devs vs gameplay devs)
- Simplified unit testing (test Presenter without Play mode)
- Readable, maintainable code

### Cons
- Requires planning -- splitting classes by responsibility takes organization
- Overhead not justified for simple scripts

### VContainer/MessagePipe adaptation
MVP **pairs naturally** with VContainer + MessagePipe:
- **Model:** Plain C# class registered in VContainer
- **Presenter:** Registered in VContainer, receives Model and View via injection
- **View:** MonoBehaviour on UI GameObject, registered via `RegisterComponentInHierarchy<T>`
- **Events:** Replace `event Action` with MessagePipe `IPublisher<T>`/`ISubscriber<T>`

---

# Other Patterns (Brief Reference)

## Adapter
**Category:** Structural
**Use case:** Wrapping an incompatible interface so two systems work together.
**VContainer note:** Register the adapter as the abstraction: `builder.Register<IMyService, ThirdPartyAdapter>(Lifetime.Singleton)`

## Flyweight
**Category:** Structural
**Use case:** Sharing common data across many similar objects to save memory.
**VContainer note:** Register shared data as `Lifetime.Singleton`; individual instances reference it.

## Decorator
**Category:** Structural
**Use case:** Adding responsibilities to an object at runtime without changing its class (weapon perks, buff stacking).
**VContainer note:** Chain decorators via registration.

## Facade
**Category:** Structural
**Use case:** Providing a simplified interface to a complex subsystem.
**VContainer note:** The facade receives its internal components via constructor injection.

## Strategy
**Category:** Behavioral
**Use case:** Swapping algorithms at runtime (pathfinding: A*, Dijkstra).
**VContainer note:** Register strategy interface; inject or resolve the right implementation.

## Type Object
**Category:** Behavioral
**Use case:** Differentiating objects via data (ScriptableObjects) instead of subclasses.
**VContainer note:** ScriptableObjects live outside the container as pure data assets. The processing service is injected.

## Double Buffer
**Category:** Sequencing
**Use case:** Maintaining two data sets -- display one while computing the other.

## Dirty Flag
**Category:** Optimization
**Use case:** Tracking whether an expensive operation needs to re-run.

## Spatial Partitioning
**Category:** Optimization
**Use case:** Organizing GameObjects by position for efficient queries (Grid, Quadtree, Octree).

## Subclass Sandbox
**Category:** Behavioral
**Use case:** Defining behaviors as protected methods in a parent class for subclass mixing.

## Data Locality
**Category:** Optimization
**Use case:** Storing data efficiently in memory for cache-friendly access (ECS/DOTS).

---

# Patterns Already Built Into Unity

| Pattern | Unity Implementation |
|---|---|
| **Game Loop** | MonoBehaviour lifecycle: `Update`, `FixedUpdate`, `LateUpdate` |
| **Update** | MonoBehaviour's `Update` method runs per-frame logic automatically |
| **Prototype** | Prefab system -- duplicate template objects with component overrides |
| **Component** | GameObject + Component architecture -- compose behavior from small parts |

---

# Pattern Selection Guide

| I need to... | Use this pattern | In VContainer/MessagePipe, this means... |
|---|---|---|
| Ensure only one instance of a manager | ~~Singleton~~ | `builder.Register<T>(Lifetime.Singleton)` -- never use raw singletons |
| Create objects at runtime with custom init | Factory | Register a factory class in VContainer; inject it where needed |
| Spawn/destroy many objects without GC spikes | Object Pool | Use `UnityEngine.Pool.ObjectPool<T>`; register pool as singleton service |
| Track and undo/redo player actions | Command | Register `CommandInvoker` in VContainer; commands are runtime objects |
| Manage complex object states (player, AI) | State | StateMachine as injectable service; publish state changes via MessagePipe |
| Notify many objects when something happens | ~~Observer~~ | `IPublisher<T>` / `ISubscriber<T>` via MessagePipe -- replaces C# events |
| Separate UI from game logic | MVP | Model + Presenter as VContainer services; View as registered MonoBehaviour |
| Swap algorithms at runtime | Strategy | Register strategy interface; inject or resolve the right implementation |
| Add abilities/modifiers without changing base | Decorator | Chain decorators via VContainer registration |
| Wrap incompatible third-party APIs | Adapter | Register adapter as the expected interface in VContainer |
| Define many item/enemy variants via data | Type Object | ScriptableObjects for data; processing service injected via VContainer |
| Simplify access to complex subsystems | Facade | Facade class receives subsystem components via constructor injection |
| Share data across many similar objects | Flyweight | Register shared data as Singleton; individual instances reference it |

---

# Key Takeaways for VContainer + MessagePipe Projects

1. **Singleton pattern is obsolete.** VContainer's `Lifetime.Singleton` + constructor injection gives you single-instance guarantees with explicit, testable dependencies. Never use `static Instance`.

2. **Observer pattern is replaced by MessagePipe.** Instead of C# `event` + `+=`/`-=`, use `IPublisher<T>`/`ISubscriber<T>`. Benefits: full decoupling, automatic lifecycle via scopes, type-safe routing.

3. **Factory, Command, State, Object Pool, and MVP complement DI.** These patterns solve problems that DI doesn't address (runtime creation, action history, state transitions, memory optimization, UI architecture). Register their orchestrator classes in VContainer and inject dependencies.

4. **KISS still applies.** Don't use a pattern just because it exists. If a simple `if` statement works and the code is unlikely to grow, skip the pattern. Add complexity only when it solves a real problem.

5. **Composition over inheritance.** Use interfaces (ISP) to compose capabilities. Register implementations in VContainer. This is more flexible than deep inheritance hierarchies and avoids LSP violations.
