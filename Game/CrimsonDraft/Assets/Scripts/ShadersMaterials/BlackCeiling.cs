using UnityEngine;
using System.Collections;

public class BlackCeiling : MonoBehaviour
{
    public Renderer ceilingRenderer;
    public float fadeDuration = 1f;

    private Material mat;
    private Coroutine routine;

    void Start()
    {
        mat = ceilingRenderer.material;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
            FadeTo(0f); // transparente
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
            FadeTo(1f); // oscuro
    }

    void FadeTo(float targetAlpha)
    {
        if (routine != null)
            StopCoroutine(routine);

        routine = StartCoroutine(FadeRoutine(targetAlpha));
    }

    IEnumerator FadeRoutine(float target)
    {
        float start = mat.color.a;
        float t = 0f;

        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            float a = Mathf.SmoothStep(start, target, t / fadeDuration);

            Color c = mat.color;
            c.a = a;
            mat.color = c;

            yield return null;
        }

        Color final = mat.color;
        final.a = target;
        mat.color = final;
    }
}
