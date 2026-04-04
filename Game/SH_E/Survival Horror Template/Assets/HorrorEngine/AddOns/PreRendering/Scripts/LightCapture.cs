using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PreRenderBackgrounds
{
    public class LightCapture : MonoBehaviour
    {
        // Start is called before the first frame update
        void Start()
        {

        }

        // Update is called once per frame
        void Update()
        {

        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(1, 1, 0));
        }
    }
}