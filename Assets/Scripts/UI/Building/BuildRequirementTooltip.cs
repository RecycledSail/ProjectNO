using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class BuildRequirementTooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private const float PaddingX = 14f;
    private const float PaddingY = 10f;
    private const float MaxWidth = 360f;
    private const float CursorOffset = 14f;

    private static GameObject tooltipObject;
    private static RectTransform tooltipRect;
    private static TMP_Text tooltipText;
    private static Canvas tooltipCanvas;
    private static RectTransform canvasRect;

    private string message = "";
    private bool hovering;

    public void SetMessage(string tooltipMessage)
    {
        message = tooltipMessage ?? "";

        if (hovering)
            ShowOrHide();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        hovering = true;
        ShowOrHide();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hovering = false;
        Hide();
    }

    private void Update()
    {
        if (hovering && tooltipObject != null && tooltipObject.activeSelf)
            PositionTooltip();
    }

    private void ShowOrHide()
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            Hide();
            return;
        }

        EnsureTooltip();
        if (tooltipObject == null)
            return;

        tooltipText.text = message;

        Vector2 preferred = tooltipText.GetPreferredValues(message, MaxWidth, 0f);
        tooltipRect.sizeDelta = new Vector2(preferred.x + PaddingX * 2f, preferred.y + PaddingY * 2f);
        PositionTooltip();
        tooltipObject.SetActive(true);
        tooltipObject.transform.SetAsLastSibling();
    }

    private static void Hide()
    {
        if (tooltipObject != null)
            tooltipObject.SetActive(false);
    }

    private void EnsureTooltip()
    {
        Canvas canvas = GetComponentInParent<Canvas>()?.rootCanvas;
        if (canvas == null)
            return;

        if (tooltipObject != null && tooltipCanvas == canvas)
            return;

        if (tooltipObject != null)
            Destroy(tooltipObject);

        tooltipCanvas = canvas;
        canvasRect = canvas.GetComponent<RectTransform>();

        tooltipObject = new GameObject("BuildRequirementTooltip", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        tooltipObject.transform.SetParent(tooltipCanvas.transform, false);

        tooltipRect = tooltipObject.GetComponent<RectTransform>();
        tooltipRect.anchorMin = new Vector2(0.5f, 0.5f);
        tooltipRect.anchorMax = new Vector2(0.5f, 0.5f);
        tooltipRect.pivot = new Vector2(0f, 1f);
        tooltipRect.sizeDelta = new Vector2(280f, 78f);
        tooltipRect.localScale = Vector3.one;

        Image background = tooltipObject.GetComponent<Image>();
        background.color = new Color(0.05f, 0.05f, 0.05f, 0.92f);
        background.raycastTarget = false;

        GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(tooltipObject.transform, false);

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(PaddingX, PaddingY);
        textRect.offsetMax = new Vector2(-PaddingX, -PaddingY);

        tooltipText = textObject.GetComponent<TMP_Text>();
        tooltipText.fontSize = 20f;
        tooltipText.color = Color.white;
        tooltipText.textWrappingMode = TextWrappingModes.NoWrap;
        tooltipText.raycastTarget = false;

        tooltipObject.SetActive(false);
    }

    private void PositionTooltip()
    {
        if (tooltipCanvas == null || canvasRect == null)
            return;

        Camera camera = tooltipCanvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : tooltipCanvas.worldCamera;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, Input.mousePosition, camera, out Vector2 localPoint))
            return;

        Vector2 size = tooltipRect.sizeDelta;
        Rect rect = canvasRect.rect;

        float x = Mathf.Clamp(localPoint.x + CursorOffset, rect.xMin, rect.xMax - size.x);
        float y = Mathf.Clamp(localPoint.y - CursorOffset, rect.yMin + size.y, rect.yMax);

        tooltipRect.anchoredPosition = new Vector2(x, y);
    }
}
