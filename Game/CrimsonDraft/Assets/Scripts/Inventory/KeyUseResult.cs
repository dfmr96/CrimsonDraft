#nullable enable

namespace CrimsonDraft.Inventory
{
    public enum KeyUseResult
    {
        Success,          // use registered; key has remaining uses
        DepletedAfterUse, // use registered; key reached 0 uses — caller shows discard prompt
        AlreadyDepleted,  // key found but already at 0 uses; use not registered
        NotFound          // no slot contains a KeyItem with the given itemId
    }
}
