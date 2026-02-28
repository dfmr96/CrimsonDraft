#nullable enable

using VContainer;
using VContainer.Unity;
using UnityEngine;
using UnityEngine.InputSystem;
using CrimsonDraft.Infrastructure.Input;

namespace CrimsonDraft.Infrastructure
{
    public sealed class GameLifetimeScope : LifetimeScope
    {
        [SerializeField] private InputActionAsset inputActions = null!;

        protected override void Awake()
        {
            base.Awake();
            DontDestroyOnLoad(gameObject);
        }

        protected override void Configure(IContainerBuilder builder)
        {
            if (this.inputActions == null)
                throw new System.InvalidOperationException(
                    $"{nameof(this.inputActions)} is not assigned in {nameof(GameLifetimeScope)}.");

            builder.RegisterInstance(this.inputActions);
            builder.Register<InputService>(Lifetime.Singleton).AsImplementedInterfaces();
        }
    }
}
