#nullable enable

using System;

namespace CrimsonDraft.Navigation.Milestones
{
    public interface IMilestoneService
    {
        bool IsCompleted(MilestoneRef milestone);
        bool IsCompleted(string milestoneId);
        event Action<MilestoneRef>? Completed;
    }
}
