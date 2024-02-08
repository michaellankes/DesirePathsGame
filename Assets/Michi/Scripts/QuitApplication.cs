using UnityEngine;

public class QuitApplication : MonoBehaviour
{
    public void Quit()
    {
        // Beim Drücken des Buttons wird diese Methode aufgerufen
#if UNITY_EDITOR
        // Wenn du im Unity Editor bist, wird dies ausgeführt
        UnityEditor.EditorApplication.isPlaying = false;
#else
        // Wenn du die gebaute Applikation ausführst, wird dies ausgeführt
        Application.Quit();
#endif
    }
}
