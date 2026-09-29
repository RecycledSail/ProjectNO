using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class SaveDeleteConfirmationUI : MonoBehaviour
{
    private const string PrefabPath = "UI/SaveDeleteConfirmPanel";

    [SerializeField] private TMP_Text messageText;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;

    private static SaveDeleteConfirmationUI activeDialog;
    private Action confirmAction;

    private void Awake()
    {
        confirmButton.onClick.AddListener(Confirm);
        cancelButton.onClick.AddListener(Cancel);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
            Cancel();
    }

    public static void Show(Component requester, string saveName, Action onConfirm)
    {
        Canvas canvas = requester.GetComponentInParent<Canvas>()?.rootCanvas;
        SaveDeleteConfirmationUI prefab = Resources.Load<SaveDeleteConfirmationUI>(PrefabPath);

        if (canvas == null || prefab == null)
        {
            Debug.LogError("Save delete confirmation panel could not be opened.");
            return;
        }

        if (activeDialog != null)
            Destroy(activeDialog.gameObject);

        activeDialog = Instantiate(prefab, canvas.transform, false);
        activeDialog.transform.SetAsLastSibling();
        activeDialog.confirmAction = onConfirm;
        activeDialog.messageText.text = $"Delete save '{saveName}'?\nThis cannot be undone.";
    }

    private void Confirm()
    {
        Action action = confirmAction;
        Close();
        action?.Invoke();
    }

    private void Cancel() => Close();

    private void Close()
    {
        confirmAction = null;
        if (activeDialog == this)
            activeDialog = null;
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (activeDialog == this)
            activeDialog = null;
    }
}
