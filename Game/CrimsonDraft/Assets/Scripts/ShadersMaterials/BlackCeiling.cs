using UnityEngine;
using System.Collections;

public class BlackCeiling : MonoBehaviour
{
        [Header("Se vuelven TRANSPARENTES al entrar")]
        public Renderer[] toTransparent;

        [Header("Se vuelven OSCUROS al entrar")]
        public Renderer[] toDark;

        public float fadeDuration = 1f;

        private Material[] matsTransparent;
        private Material[] matsDark;

        private Coroutine routine;

        void Start()
        {
            // Instanciar materiales
            matsTransparent = InitMaterials(toTransparent);
            matsDark = InitMaterials(toDark);
        }

        Material[] InitMaterials(Renderer[] renderers)
        {
            Material[] mats = new Material[renderers.Length];

            for (int i = 0; i < renderers.Length; i++)
            {
                mats[i] = renderers[i].material;
            }

            return mats;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player"))
                StartSwitch(0f, 1f); // transparente / oscuro
        }

        //private void OnTriggerExit(Collider other)
        //{
        //    if (other.CompareTag("Player"))
        //        StartSwitch(1f, 0f); // revertir
        //}

        void StartSwitch(float transparentTarget, float darkTarget)
        {
            if (routine != null)
                StopCoroutine(routine);

            routine = StartCoroutine(FadeRoutine(transparentTarget, darkTarget));
        }

        IEnumerator FadeRoutine(float transparentTarget, float darkTarget)
        {
            float t = 0f;

            float[] startTransparent = GetAlphas(matsTransparent);
            float[] startDark = GetAlphas(matsDark);

            while (t < fadeDuration)
            {
                t += Time.deltaTime;
                float lerp = Mathf.SmoothStep(0f, 1f, t / fadeDuration);

                // Grupo transparente
                for (int i = 0; i < matsTransparent.Length; i++)
                {
                    float a = Mathf.Lerp(startTransparent[i], transparentTarget, lerp);
                    SetAlpha(matsTransparent[i], a);
                }

                // Grupo oscuro
                for (int i = 0; i < matsDark.Length; i++)
                {
                    float a = Mathf.Lerp(startDark[i], darkTarget, lerp);
                    SetAlpha(matsDark[i], a);
                }

                yield return null;
            }

            // asegurar valores finales
            SetFinal(matsTransparent, transparentTarget);
            SetFinal(matsDark, darkTarget);
        }

        float[] GetAlphas(Material[] mats)
        {
            float[] values = new float[mats.Length];

            for (int i = 0; i < mats.Length; i++)
            {
                values[i] = mats[i].color.a;
            }

            return values;
        }

        void SetAlpha(Material mat, float a)
        {
            Color c = mat.color;
            c.a = a;
            mat.color = c;
        }

        void SetFinal(Material[] mats, float target)
        {
            for (int i = 0; i < mats.Length; i++)
            {
                SetAlpha(mats[i], target);
            }
        }
    }
