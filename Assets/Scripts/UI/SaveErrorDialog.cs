using UnityEngine;

/// <summary>Save/load errors remain visible even when startup returns to the menu.</summary>
public sealed class SaveErrorDialog : MonoBehaviour
{
    private static SaveErrorDialog instance;
    private string message;

    public static void Show(string text)
    {
        if (!Application.isPlaying) return;
        if (instance == null)
        {
            instance = new GameObject("Save error").AddComponent<SaveErrorDialog>();
            DontDestroyOnLoad(instance.gameObject);
        }
        instance.message = text;
    }

    private void OnGUI()
    {
        GUI.depth = -1000;
        float width = Mathf.Min(560, Screen.width - 20);
        GUILayout.BeginArea(new Rect((Screen.width - width) / 2, 60, width, 180), GUI.skin.box);
        GUILayout.Label(message, new GUIStyle(GUI.skin.label) { wordWrap = true });
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("확인")) Destroy(gameObject);
        GUILayout.EndArea();
    }
}
