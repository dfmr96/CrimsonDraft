
namespace HorrorEngine
{
    public class SceneAmbient : SceneAudio
    {
        protected override AudioStack GetStack()
        {
            return AmbientManager.Instance.GetStack();
        }
    }
}