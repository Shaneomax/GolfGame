using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private GameObject pauseButton;
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private GameObject shootButton;
    [SerializeField] private GameObject equipmentPanel;
    [SerializeField] private GameObject onAirButton;

    [Header("Scene Names")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    public static bool IsPaused { get; private set; } = false;

    private void Start()
    {
        if (pausePanel != null) pausePanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (pauseButton != null) pauseButton.SetActive(true);

        Time.timeScale = 1f;
    }

    public void PauseGame()
    {
        IsPaused = true;
        Time.timeScale = 0f;

        if (pauseButton != null) pauseButton.SetActive(false);
        if (pausePanel != null) pausePanel.SetActive(true);
        if (shootButton != null) shootButton.SetActive(false);
        if (equipmentPanel != null) equipmentPanel.SetActive(false);
        if (onAirButton != null) onAirButton.SetActive(false);
    }

    public void ResumeGame()
    {
        IsPaused = false;
        Time.timeScale = 1f;

        if (pausePanel != null) pausePanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (pauseButton != null) pauseButton.SetActive(true);
        if (equipmentPanel != null) equipmentPanel.SetActive(true);
        
        // Let the GameplayUIController handle the shoot button and onAirButton if possible
        if (GolfGame.Controllers.GameStateManager.Instance != null)
        {
            var currentState = GolfGame.Controllers.GameStateManager.Instance.CurrentState;
            if (shootButton != null) shootButton.SetActive(currentState == GolfGame.Controllers.GameStateManager.GameState.Setup);
            if (onAirButton != null) onAirButton.SetActive(currentState == GolfGame.Controllers.GameStateManager.GameState.Aiming);
        }
        else 
        {
            if (shootButton != null) shootButton.SetActive(true);
            if (onAirButton != null) onAirButton.SetActive(false);
        }
    }

    // To be implemented.
    public void OpenSettings()
    {
        if (settingsPanel != null) settingsPanel.SetActive(true);
    }

    // To be implemented
    public void CloseSettings()
    {
        if (settingsPanel != null) settingsPanel.SetActive(false);
    }

    public void LoadMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(mainMenuSceneName);
    }

    public void QuitGame()
    {
        Debug.Log("Quitting game from pause menu...");
        Application.Quit();

        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #endif
    }
}