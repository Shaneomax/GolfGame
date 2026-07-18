using UnityEngine;

namespace GolfGame.Data
{
    [CreateAssetMenu(fileName = "NewGroundData", menuName = "Golf/Ground Data")]
    public class GroundData : ScriptableObject
    {
        [Header("Identification")]
        public string GroundName;
        public string GroundTag = "Untagged";
        
        [Tooltip("True if this is a putting green/NiceOn surface.")]
        public bool IsNiceOn = false;

        [Tooltip("True if this is water. The ball will reset to its previous location.")]
        public bool IsWaterHazard = false;

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

        [Header("Extra Roll Settings")]
        [Tooltip("Enable to apply extra artificial roll logic (overriding natural friction).")]
        public bool UseExtraRoll = false;
        
        [Tooltip("0 = Ball stops instantly. 0.5 = Natural Unity friction. 1 = Ball rolls infinitely without stopping.")]
        [Range(0f, 1f)]
        public float ExtraRollFactor = 0.5f;

        [Header("Debuffs")]
        [Tooltip("Reduces the maximum launch power of the club. (e.g., 0.05 = 5% power reduction in bunkers).")]
        [Range(0f, 1f)]
        public float PowerDebuff = 0f;
    }
}