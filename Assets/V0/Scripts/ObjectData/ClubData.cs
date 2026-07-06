using UnityEngine;

[CreateAssetMenu(fileName = "NewClub", menuName = "Golf/Club Data")]
public class ClubData : ScriptableObject
{
    [Header("Club Identification")]
    public string ClubName;
    public string ClubDescription;

    [Header("Core Stats")]
    [Tooltip("Determines the maximum physical force applied to the ball.")]
    public float Power; 
    
    [Tooltip("Determines how fast the swing meter needle moves.")]
    public float Accuracy; 
    
    [Tooltip("Maximum allowed topspin for rolling.")]
    public float TopSpin; 
    
    [Tooltip("Maximum allowed backspin for stopping on the green.")]
    public float BackSpin; 
    
    [Tooltip("How much the player can hook/slice the ball in the air.")]
    public float Curl; 
}
