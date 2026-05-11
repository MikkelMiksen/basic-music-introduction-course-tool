using UnityEngine;

public class DragAndSnap : MonoBehaviour
{
    private Vector3 offset;
    private Camera cam;
    private bool isDragging = false;

    void Start()
    {
        cam = Camera.main;
    }

    void OnMouseDown()
    {
        offset = transform.position - GetMouseWorldPos();
        isDragging = true;
    }

    void OnMouseDrag()
    {
        if (isDragging)
        {
            Vector3 targetPos = GetMouseWorldPos() + offset;
            transform.position = SnapToGrid(targetPos);
        }
    }

    void OnMouseUp()
    {
        isDragging = false;
        // Optionally trigger a rescan in NoteManager
        NoteManager manager = Object.FindFirstObjectByType<NoteManager>();
        if (manager != null) manager.ScanNotes();
    }

    private Vector3 GetMouseWorldPos()
    {
        Vector3 mousePoint = Input.mousePosition;
        mousePoint.z = cam.WorldToScreenPoint(transform.position).z;
        return cam.ScreenToWorldPoint(mousePoint);
    }

    private Vector3 SnapToGrid(Vector3 position)
    {
        return new Vector3(
            Mathf.Round(position.x),
            Mathf.Round(position.y),
            Mathf.Round(position.z)
        );
    }
}
