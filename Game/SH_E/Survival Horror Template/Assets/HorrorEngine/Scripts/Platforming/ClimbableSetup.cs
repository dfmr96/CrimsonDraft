using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HorrorEngine
{
    [CreateAssetMenu(menuName = "Horror Engine/Platforming/Climbable Setup")]
    public class ClimbableSetup : ScriptableObject
    {
        [Tooltip("Animation that will be played on climb")]
        public AnimatorStateHandle ClimbAnimation;
        [Tooltip("Animation that will be played on drop")]
        public AnimatorStateHandle DropAnimation;

        [Tooltip("Indicates if the animation should stay in a loop state between entry/exit. This state is automatically exited when the ExitDistance is reached")]
        public bool Loop;

        [Tooltip("Direction the character will rotate towards on climb entry. FaceEntryDirection needs to be selected")]
        public Vector3 ClimbEntryDirection = Vector3.forward;
        [Tooltip("Direction the character will rotate towards on exit entry. FaceEntryDirection needs to be selected")]
        public Vector3 DropEntryDirection = -Vector3.forward;

        [Tooltip("Indicates if the player will rotate towards the Climb/Drop EntryDirection")]
        public bool FaceEntryDirection = true;
        [Tooltip("Indicates if the player will rotate towards the Exit point forward direction")]
        public bool FaceExitDirection = true;

        [Tooltip("This is an offset from the top. Indicates the point at which the character will transition from the loop state to the exit state when climbing")]
        public float ClimbExitDistance = 0.1f;
        [Tooltip("This is an offset from the bottom. Indicates the point at which the character will transition from the loop state to the exit state when dropping")]
        public float DropExitDistance = 0f;

        [Tooltip("Indicates the speed of movement while climbing. This value is scaled by the animation ClimbProgress curve")]
        public float ClimbSpeed = 1f;
        [Tooltip("Indicates the speed of movement while dropping. This value is scaled by the animation ClimbProgress curve")]
        public float DropSpeed = 1f;
        public SurfaceType Surface;

        [Tooltip("Sets an extra Z separation for the displacement during climbing")]
        public float ClimbZOffset;
        [Tooltip("Sets an extra Z separation for the displacement during dropping")]
        public float DropZOffset;
    }
}