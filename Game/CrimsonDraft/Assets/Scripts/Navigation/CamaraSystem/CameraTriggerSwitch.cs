using UnityEngine;
using Unity.Cinemachine;  

using CrimsonDraft.Navigation.Player;

namespace CrimsonDraft.Navigation
{
    public class CameraTriggerSwitch : MonoBehaviour
    {
        [Header("Camaras")]
        [SerializeField] private CinemachineCamera camA;
        [SerializeField] private CinemachineCamera camB;

        private void OnTriggerEnter(Collider other)
        {
            if (other.GetComponent<PlayerController>() != null)
            {
                ActivateCameraB();
            }
        }

        private void ActivateCameraB()
        {
            if (camA == null || camB == null)
            {
                Debug.LogWarning($"{nameof(CameraTriggerSwitch)} requiere dos camaras asignadas.", this);
                return;
            }

            camA.gameObject.SetActive(false);
            camB.gameObject.SetActive(true);
        }
    }
}
