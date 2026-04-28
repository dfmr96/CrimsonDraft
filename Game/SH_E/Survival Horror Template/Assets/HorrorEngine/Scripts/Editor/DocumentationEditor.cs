using UnityEngine;
using UnityEditor;

namespace HorrorEngine
{
    public class DocumentationEditor : MonoBehaviour
    {
        [MenuItem("Horror Engine/Documentation")]
        static void OpenDocs()
        {
            string docsLinks = "https://www.notion.so/Retro-Horror-Template-6c9c55d8a82e4b858906eca17f3edaf9?pvs=4";
            Application.OpenURL(docsLinks);
        }

        [MenuItem("Horror Engine/Video Tutorials")]
        static void OpenVideos()
        {
            string ytLinks = "https://www.youtube.com/watch?v=DpU-mC3akAU&list=PLKmllWWdyHrwbtl9y8nHL4JbvEGwulSrA";
            Application.OpenURL(ytLinks);
        }

        
    }
}