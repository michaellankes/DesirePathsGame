using UnityEngine;

public class QuitApplication : MonoBehaviour
{
    public void Quit()
    {
        // Beim Dr�cken des Buttons wird diese Methode aufgerufen
#if UNITY_EDITOR
        // Wenn du im Unity Editor bist, wird dies ausgef�hrt
        UnityEditor.EditorApplication.isPlaying = false;
#else
        // Wenn du die gebaute Applikation ausf�hrst, wird dies ausgef�hrt
        Application.Quit();
#endif
    }
}
