using UnityEngine;

namespace GolfGame.Data
{
    [CreateAssetMenu(fileName = "NewGroundData", menuName = "Golf/Ground Data")]
    public class GroundData : ScriptableObject
    {
        [Header("Identification")]
        public string GroundName;
        public string GroundTag = "Untagged";

        [Header("Friction & Drag")]
        [Tooltip("Linear drag applied while rolling/moving through this ground type.")]
        public float LinearDrag = 0f;
        [Tooltip("Angular drag applied to slow down spin/rolling.")]
        public float AngularDrag = 0.05f;

        [Header("Bounce Settings")]
        [Tooltip("Max number of bounces allowed before forcing roll. Use 0 for sand/mud.")]
        public int MaxBounces = 3;
        
        [Tooltip("How much downward impact is converted to upward bounce on the 1st hit.")]
        [Range(0f, 2f)]
        public float FirstBounceImpactScale = 1.1f;
        
        [Tooltip("Extra forward kill on the very first bounce. 1 = no kill, 0 = dead stop.")]
        [Range(0f, 1f)]
        public float FirstBounceForwardKill = 0.6f;

        [Tooltip("How much horizontal speed converts to upward energy (simulates digging in).")]
        [Range(0f, 0.5f)]
        public float ForwardToBounceConversion = 0.25f;

        [Tooltip("Fraction of previous bounce height used for the next bounce.")]
        [Range(0f, 1f)]
        public float BounceDecayRatio = 0.5f;

        [Tooltip("How much forward speed is retained on subsequent bounces.")]
        [Range(0f, 1f)]
        public float ForwardRetentionPerBounce = 0.75f;

        [Header("Roll Settings")]
        [Tooltip("Roll deceleration factor. 1 = rolls perfectly, 0 = stops immediately.")]
        [Range(0f, 1f)]
        public float RollFactor = 0.9f;
    }
}