using CrimsonDraft.Audio;
using UnityEngine;

public class PickupPoints : MonoBehaviour
{
    private PickupPointAudio audio;

    private void Awake()
    {
        TryGetComponent(out audio);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        if (audio != null) audio.PlayPickup();

        ScoreManager.Instance.CollectKey();

        Destroy(gameObject);
    }
}