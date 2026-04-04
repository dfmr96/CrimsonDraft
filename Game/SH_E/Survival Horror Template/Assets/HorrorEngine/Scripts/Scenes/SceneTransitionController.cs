using System.Collections;
using UnityEngine;
using UnityEngine.ResourceManagement.Util;

namespace HorrorEngine
{
    public class SceneTransitionPreMessage : BaseMessage
    {

    }
    public class SceneTransitionPostMessage : BaseMessage
    {

    }

    public class SceneTransitionEndMessage : BaseMessage
    {

    }

    public class SceneTransitionController : ComponentSingleton<SceneTransitionController>
    {
        public void Trigger(SceneTransition transition)
        {
            StartCoroutine(StartTransitionRoutine(transition));
        }

        // --------------------------------------------------------------------

        private IEnumerator StartTransitionRoutine(SceneTransition transition)
        {
            PauseController.Instance.Pause(this);

            // Fade Out - always get a fresh UIFade in case it got recreated
            UIFade uiFade = UIManager.Get<UIFade>(true);
            if (transition.FadeOut && uiFade)
                yield return uiFade.Fade(0f, 1f, transition.FadeOutDuration);

            yield return transition.StartSceneTransition();

            PauseController.Instance.Resume(this);

            // Fade In - always get a fresh UIFade in case it got recreated
            uiFade = UIManager.Get<UIFade>(true);
            if (transition.FadeIn && uiFade)
                yield return uiFade.Fade(1f, 0f, transition.FadeInDuration);

        }
    }
}