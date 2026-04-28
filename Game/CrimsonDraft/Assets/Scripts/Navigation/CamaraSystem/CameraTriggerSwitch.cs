using UnityEngine;
using Unity.Cinemachine;  

using CrimsonDraft.Navigation.Player;

namespace CrimsonDraft.Navigation
{
    public class CameraTriggerSwitch : MonoBehaviour
    {
        [Header("Camaras")]
        [SerializeField] private CinemachineCamera camOFF;
        [SerializeField] private CinemachineCamera camON;

        private void OnTriggerEnter(Collider other)
        {
            if (other.GetComponent<PlayerController>() != null)
            {
                ActivateCameraB();
            }
        }

        private void ActivateCameraB()
        {
            if (camOFF == null || camON == null)
            {
                Debug.LogWarning($"{nameof(CameraTriggerSwitch)} requiere dos camaras asignadas.", this);
                return;
            }

            camOFF.gameObject.SetActive(false);
            camON.gameObject.SetActive(true);
        }
    }
}
