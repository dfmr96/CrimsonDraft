#nullable enable

using UnityEngine;

namespace CrimsonDraft.Operators
{
    public enum Caliber
    {
        None    = 0,
        [InspectorName("9mm")]    _9mm    = 1,
        [InspectorName("12ga")]   _12ga   = 2,
        [InspectorName("5.56x45")] _556x45 = 3,
        [InspectorName("4.6x30")] _4_6x30 = 4,
        [InspectorName("5.7x28")] _5_7x28 = 5,
    }
}
