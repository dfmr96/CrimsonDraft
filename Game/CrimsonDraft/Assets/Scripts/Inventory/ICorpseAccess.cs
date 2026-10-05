#nullable enable

namespace CrimsonDraft.Inventory
{
    public interface ICorpseAccess
    {
        bool CanAccess(int operatorSlot);
    }
}
