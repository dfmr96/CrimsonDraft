using UnityEngine;
using UnityEditor;

namespace HorrorEngine
{
    public class HorrorEngineDebug : MonoBehaviour
    {
        [MenuItem("Horror Engine/Debug/Kill Player")]
        public static void KillPlayer()
        {
            if (Application.isPlaying)
                GameManager.Instance.Player.GetComponent<Health>().Kill();
        }
    }
}
