using UnityEngine;
using UnityEngine.EventSystems;

public class UIPlacementUndoer : MonoBehaviour, IPointerDownHandler
{
    public void OnPointerDown(PointerEventData eventData)
    {
        // When this UI element is clicked, tell the DAWController to undo any note that just spawned.
        DAWController controller = Object.FindFirstObjectByType<DAWController>();
        if (controller != null)
        {
            controller.UndoLastPlacement();
        }
    }
}
