// using UnityEngine;
// using Unity.Cinemachine;

// public class CinemachineAimController : MonoBehaviour
// {
//     [Header("Cinemachine Reference")]
//     public CinemachineCamera AimCamera;
    
//     // Reference to the OrbitalFollow component where the axes actually live
//     private CinemachineOrbitalFollow orbitalFollow;

//     [Header("Sensitivity Settings")]
//     public float DragSensitivityX = 0.2f;
//     public float DragSensitivityY = 0.01f;

//     private bool isAimingActive = false;
//     private Vector3 lastInputPosition;

//     private void Start()
//     {
//         GameStateManager.Instance.OnStateEnter += OnGameStateChanged;
        
//         // Retrieve the OrbitalFollow component from the camera
//         orbitalFollow = AimCamera.GetComponent<CinemachineOrbitalFollow>();
        
//         if (orbitalFollow != null)
//         {
//             // FIX: In Cinemachine 3, the property is just "Name"
//             orbitalFollow.HorizontalAxis.Name = "";
//             orbitalFollow.VerticalAxis.Name = "";
//         }
//     }

//     private void OnDestroy()
//     {
//         if (GameStateManager.Instance != null)
//             GameStateManager.Instance.OnStateEnter -= OnGameStateChanged;
//     }

//     private void OnGameStateChanged(GameStateManager.GameState newState)
//     {
//         if (newState == GameStateManager.GameState.Aiming)
//         {
//             GameObject ball = GameObject.FindWithTag("Player");
//             if (ball != null)
//             {
//                 AimCamera.LookAt = ball.transform;
//                 AimCamera.Follow = ball.transform;
//                 AimCamera.Priority = 10; 
//                 isAimingActive = true;
//             }
//         }
//         else
//         {
//             AimCamera.Priority = 0; 
//             isAimingActive = false;
//         }
//     }

//     private void Update()
//     {
//         if (!isAimingActive || orbitalFollow == null) return;

//         HandleInput();
//     }

//     private void HandleInput()
//     {
//         if (Input.GetMouseButtonDown(0))
//         {
//             lastInputPosition = Input.mousePosition;
//         }
//         else if (Input.GetMouseButton(0))
//         {
//             Vector3 delta = Input.mousePosition - lastInputPosition;
//             lastInputPosition = Input.mousePosition;

//             // Apply to the specific axes inside the OrbitalFollow component
//             orbitalFollow.HorizontalAxis.Value += delta.x * DragSensitivityX;
//             orbitalFollow.VerticalAxis.Value -= delta.y * DragSensitivityY;
//         }
//     }

//     public Vector3 GetAimDirection()
//     {
//         Vector3 forward = Camera.main.transform.forward;
//         forward.y = 0;
//         return forward.normalized;
//     }
// }