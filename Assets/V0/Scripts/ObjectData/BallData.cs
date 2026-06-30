using UnityEngine;

[CreateAssetMenu(fileName = "NewBall", menuName = "Golf/Ball Data")]
public class BallData : ScriptableObject
{
    [Header("Ball Identification")]
    public string BallName;

    [Header("Physics Properties")]
    [Tooltip("Standard Unity mass. Usually 0.045 kg for a golf ball.")]
    public float Mass = 0.045f;
    
    [Tooltip("How much the wind affects this specific ball (lower is better).")]
    public float WindResistance = 1.0f; 
    
    [Tooltip("How much the ball bounces (combines with terrain friction later).")]
    public float Bounciness = 0.6f;

    [Header("Friction & Stopping")]
    [Tooltip("Air/Ground sliding resistance (Linear Drag).")]
    public float LinearDrag = 0.5f;
    
    [Tooltip("Rolling resistance to stop infinite rolling (Angular Drag).")]
    public float AngularDrag = 1.5f; 
    
    [Tooltip("Velocity below which the ball is forcefully stopped.")]
    public float StopThreshold = 0.1f;
}