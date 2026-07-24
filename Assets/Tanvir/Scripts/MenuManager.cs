using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuManager : MonoBehaviour
{
    [Header("Scene Configuration")]
    [Tooltip("Drag your GamePlay scene here, or whatever scene you want to come after pressing PLAY button.")]
    [SerializeField] private string gameplaySceneName = "SampleScene";

    [Header("UI Panels")]
    [Tooltip("Are there any Settings Panel on the hierarchy yet? If there is one, drag it here!")]
    [SerializeField] private GameObject settingsPanel;

    public void PlayGame()
    {
        // Please change the scene name according to gameplay scene in the project.
        // For now it's the default "Sample Scene"
        SceneManager.LoadScene(gameplaySceneName);
    }

    // To be implemented. Pressing the button won't do shit.
    public void OpenSettings()
    {
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(true);
        }
        else
        {
            Debug.LogWarning("Settings Panel is not assigned in the Inspector.");
        }
    }

    // To be implemented. There ain't no button to press!
    public void CloseSettings()
    {
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
        }
    }

    public void QuitGame()
    {
        // Will work when the build is played on phone.
        Debug.Log("Quit Game requested.");
        Application.Quit();

        // Will work for UnityEditor.
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #endif
    }
}