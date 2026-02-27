using VContainer;
using VContainer.Unity;

namespace CrimsonDraft.Combat
{
    /// <summary>
    /// DI scope for combat encounters. Loaded additively on top of Navigation.
    /// Everything registered here is disposed when the combat scene unloads.
    /// Parent: NavigationScope → GameLifetimeScope.
    ///
    /// Assign to a GameObject in Combat.unity.
    /// </summary>
    public class CombatScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            // ── Combat services ───────────────────────────────────────────────
            // builder.Register<ICombatService, CombatService>(Lifetime.Scoped);
            // builder.Register<IQTESystem,      QTESystem    >(Lifetime.Scoped);
            // builder.Register<IEnemySpawner,   EnemySpawner >(Lifetime.Scoped);

            // ── Views (MonoBehaviours) ────────────────────────────────────────
            // builder.RegisterComponentInHierarchy<CombatHUDView>();
            // builder.RegisterComponentInHierarchy<QTEBarView>();

            // ── Setup data injected before scene load ─────────────────────────
            // Passed in via LifetimeScope.Enqueue() from CombatSceneLoader:
            //   using (LifetimeScope.EnqueueParent(navigationScope))
            //   using (LifetimeScope.Enqueue(b => b.RegisterInstance(setupData)))
            //   { await SceneManager.LoadSceneAsync("Combat", additive); }
        }
    }
}
