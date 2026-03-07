#nullable enable

using TMPro;
using UnityEngine;

namespace CrimsonDraft.Navigation.UI
{
    public sealed class RosterOperatorRow : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI nameLabel   = null!;
        [SerializeField] private TextMeshProUGUI weaponLabel = null!;

        public void Setup(string operatorName, string equippedWeapon)
        {
            this.nameLabel.text   = operatorName;
            this.weaponLabel.text = equippedWeapon;
        }
    }
}
