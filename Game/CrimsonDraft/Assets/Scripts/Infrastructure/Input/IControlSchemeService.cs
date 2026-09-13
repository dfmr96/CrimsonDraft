#nullable enable

namespace CrimsonDraft.Infrastructure.Input
{
    public enum ControlScheme { Modern, Classic }

    public interface IControlSchemeService
    {
        ControlScheme CurrentScheme { get; }
        void SetScheme(ControlScheme scheme);
    }
}
