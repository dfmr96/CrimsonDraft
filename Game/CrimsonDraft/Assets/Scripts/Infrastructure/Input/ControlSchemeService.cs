#nullable enable

using UnityEngine;
using UnityEngine.Scripting;
using VContainer.Unity;

namespace CrimsonDraft.Infrastructure.Input
{
    public sealed class ControlSchemeService : IControlSchemeService, IInitializable
    {
        private const string        SchemeKey     = "Control.Scheme";
        private const ControlScheme DefaultScheme = ControlScheme.Modern;

        public ControlScheme CurrentScheme { get; private set; }

        [Preserve]
        public ControlSchemeService() { }

        void IInitializable.Initialize()
        {
            this.CurrentScheme = (ControlScheme)PlayerPrefs.GetInt(SchemeKey, (int)DefaultScheme);
        }

        public void SetScheme(ControlScheme scheme)
        {
            this.CurrentScheme = scheme;
            PlayerPrefs.SetInt(SchemeKey, (int)scheme);
        }
    }
}
