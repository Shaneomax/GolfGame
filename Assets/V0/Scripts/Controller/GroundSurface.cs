using UnityEngine;
using GolfGame.Data;

namespace GolfGame.Environment
{
    public class GroundSurface : MonoBehaviour
    {
        [Tooltip("Assign the ScriptableObject for this terrain type (e.g., FairwayData, GreenData).")]
        public GroundData SurfaceData;
    }
}