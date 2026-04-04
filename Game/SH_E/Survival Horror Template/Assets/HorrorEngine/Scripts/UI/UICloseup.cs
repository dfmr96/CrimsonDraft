using System.Collections;
using UnityEngine;

namespace HorrorEngine
{
    public class UICloseup : Closeup
    {
        [SerializeField] GameObject Content;

        protected override void Start()
        {
            base.Start();
            Content.SetActive(false);
        }
        public override IEnumerator ActivationRoutine()
        {
            Content.SetActive(true);
            yield return null;
        }

        public override IEnumerator DeactivationRoutine()
        {
            Content.SetActive(false);
            yield return null;
        }
    }
}