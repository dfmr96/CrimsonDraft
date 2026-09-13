#nullable enable

using UnityEngine;

namespace CrimsonDraft.UI.MainMenu
{
    public sealed class MainMenuCameraSway : MonoBehaviour
    {
        [Tooltip("Cuanto se desplaza la camara arriba/abajo, en unidades de mundo.")]
        [SerializeField] private float amplitude = 0.15f;

        [Tooltip("Velocidad del oleaje, en radianes por segundo.")]
        [SerializeField] private float speed = 1f;

        private Vector3 basePosition;
        private float   phase;

        private void OnEnable()
        {
            this.basePosition = this.transform.localPosition;
            this.phase = Random.Range(0f, Mathf.PI * 2f);
        }

        private void Update()
        {
            float offsetY = Mathf.Sin(Time.time * this.speed + this.phase) * this.amplitude;
            this.transform.localPosition = this.basePosition + new Vector3(0f, offsetY, 0f);
        }

        private void OnDisable()
        {
            this.transform.localPosition = this.basePosition;
        }
    }
}
