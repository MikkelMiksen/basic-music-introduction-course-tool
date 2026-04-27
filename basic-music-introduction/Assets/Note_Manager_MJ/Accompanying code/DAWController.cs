using UnityEngine;
using TMPro;
using System.Collections.Generic;
using Data_holders.instruments;
using UnityEngine.UI;
using System.Linq;
using UnityEngine.EventSystems;

public class DAWController : MonoBehaviour
{
    public enum DAWMode { PatternEditor, Playlist }

    [Header("UI References")]
    [SerializeField] private TMP_Dropdown modeDropdown;
    [SerializeField] private TMP_Dropdown instrumentDropdown;
    [SerializeField] private TMP_Dropdown savedPatternsDropdown;
    [SerializeField] private Button selectPlaceButton;
    [SerializeField] private TextMeshProUGUI selectPlaceButtonText;
    [SerializeField] private Button savePatternButton;
    [SerializeField] private Button playPauseButton;
    [SerializeField] private TextMeshProUGUI playPauseButtonText;
    [SerializeField] private Button duration1_4;
    [SerializeField] private Button duration1_8;
    [SerializeField] private Button duration1_16;

    [Header("Prefabs & Parents")]
    [SerializeField] private GameObject notePrefab;
    [SerializeField] private Transform patternParent;
    [SerializeField] private Transform playlistParent;

    [Header("Settings")]
    [SerializeField] private LayerMask gridLayer;
    
    private DAWMode currentMode = DAWMode.PatternEditor;
    private int currentDuration = 1; // 1, 2, or 4 cubes
    private List<GameObject> savedPatternPrefabs = new List<GameObject>();
    private GameObject currentEditingPattern;
    private GameObject tmpPattern;

    private List<GameObject> lastPlacedNotes = new List<GameObject>();
    private List<float> instrumentZValues = new List<float>();

    void Start()
    {
        Debug.Log("[DEBUG_LOG] DAWController Start called.");
        // Setup dropdowns
        modeDropdown.onValueChanged.AddListener(OnModeChanged);
        duration1_4.onClick.AddListener(() => SetDuration(4));
        duration1_8.onClick.AddListener(() => SetDuration(2));
        duration1_16.onClick.AddListener(() => SetDuration(1));
        savePatternButton.onClick.AddListener(SaveCurrentPattern);
        playPauseButton.onClick.AddListener(TogglePlayback);
        selectPlaceButton.onClick.AddListener(OnSelectPlacePressed);

        UpdateModeUI();
        ToggleViews();

        PopulateInstrumentDropdown();

        // Assuming NoteManager has the mapping
        NoteManager nm = Object.FindFirstObjectByType<NoteManager>();
        if (nm != null)
        {
            // Use values from NoteManager or hardcode if needed
            // For now let's assume default integer Zs as per NoteManager script
        }
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            if (currentMode == DAWMode.PatternEditor)
            {
                // If we are over a blockable UI, we DON'T place a note.
                // This replaces the previous logic of placing and then undoing.
                if (!IsPointerOverBlockableUI())
                {
                    TryPlaceNote();
                }
                else
                {
                    Debug.Log("[DEBUG_LOG] Note placement blocked by UI.");
                }
            }
            else if (currentMode == DAWMode.Playlist)
            {
                // In Playlist mode, clicking on the grid places a pattern
                if (!IsPointerOverBlockableUI())
                {
                    PlacePatternInPlaylist();
                }
            }
        }
        else if (Input.GetMouseButtonDown(1))
        {
            if (currentMode == DAWMode.PatternEditor)
            {
                TryDeleteNote();
            }
            else if (currentMode == DAWMode.Playlist)
            {
                TryDeletePatternFromPlaylist();
            }
        }
    }

    private void TryDeletePatternFromPlaylist()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 100f))
        {
            // Check if we hit a pattern instance (child of playlistParent)
            Transform t = hit.collider.transform;
            while (t != null && t.parent != playlistParent)
            {
                t = t.parent;
            }

            if (t != null && t.parent == playlistParent)
            {
                Destroy(t.gameObject);
                Invoke(nameof(Rescan), 0.05f);
            }
        }
    }

    private bool IsPointerOverBlockableUI()
    {
        if (UnityEngine.EventSystems.EventSystem.current == null) return false;

        var eventData = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current);
        eventData.position = Input.mousePosition;

        var results = new List<UnityEngine.EventSystems.RaycastResult>();
        UnityEngine.EventSystems.EventSystem.current.RaycastAll(eventData, results);

        foreach (var result in results)
        {
            // If the UI object is on the 'Ignore Raycast' layer (Layer 2), skip it
            if (result.gameObject.layer == 2) continue;

            // Check if it's an actual interactive component
            bool isInteractive = result.gameObject.GetComponent<Selectable>() != null || 
                                 result.gameObject.GetComponent<IPointerClickHandler>() != null;

            // If it's a Graphic, check if it's a raycastTarget and has some opacity/relevance
            var graphic = result.gameObject.GetComponent<UnityEngine.UI.Graphic>();
            bool isBlockingGraphic = graphic != null && graphic.raycastTarget && graphic.color.a > 0.01f;

            if (isInteractive || isBlockingGraphic) 
            {
                Debug.Log($"[DEBUG_LOG] Placement blocked by UI object: {result.gameObject.name} (Interactive: {isInteractive}, BlockingGraphic: {isBlockingGraphic})");
                return true; 
            }
        }
        return false;
    }

    public void UndoLastPlacement()
    {
        if (lastPlacedNotes.Count > 0)
        {
            foreach (var note in lastPlacedNotes)
            {
                if (note != null) Destroy(note);
            }
            lastPlacedNotes.Clear();
            // Rescan after a short delay to ensure objects are destroyed
            Invoke(nameof(Rescan), 0.05f);
            Debug.Log("[DEBUG_LOG] Last placement undone because UI was clicked.");
        }
    }

    private void TryDeleteNote()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        // We use a shorter distance or infinite, but we don't want to use the gridLayer mask here
        // because we want to hit the Note objects, not the grid.
        if (Physics.Raycast(ray, out RaycastHit hit, 100f))
        {
            if (hit.collider.CompareTag("Note") || hit.collider.GetComponent<DragAndSnap>() != null)
            {
                Destroy(hit.collider.gameObject);
                // Call ScanNotes in the next frame to ensure the object is gone
                Invoke(nameof(Rescan), 0.05f);
            }
        }
    }

    private void Rescan()
    {
        Object.FindFirstObjectByType<NoteManager>()?.ScanNotes();
    }

    private void SetDuration(int cubes)
    {
        currentDuration = cubes;
    }

    private void OnModeChanged(int index)
    {
        DAWMode newMode = (DAWMode)index;
        if (currentMode == DAWMode.PatternEditor && newMode == DAWMode.Playlist)
        {
            SaveTmpPattern();
        }
        else if (currentMode == DAWMode.Playlist && newMode == DAWMode.PatternEditor)
        {
            LoadTmpPattern();
        }
        
        currentMode = newMode;
        UpdateModeUI();
        ToggleViews();
    }

    private void LoadTmpPattern()
    {
        if (tmpPattern == null) return;
        
        // Clear current editor
        foreach (Transform child in patternParent)
        {
            Destroy(child.gameObject);
        }

        foreach (Transform child in tmpPattern.transform)
        {
            GameObject note = Instantiate(child.gameObject, patternParent);
            note.transform.localPosition = child.localPosition;
            note.SetActive(true);
        }
        Object.FindFirstObjectByType<NoteManager>()?.ScanNotes();
    }

    private void UpdateModeUI()
    {
        if (modeDropdown != null) modeDropdown.SetValueWithoutNotify((int)currentMode);

        if (currentMode == DAWMode.PatternEditor)
        {
            selectPlaceButtonText.text = "Select";
        }
        else
        {
            selectPlaceButtonText.text = "Place";
        }
    }

    private void ToggleViews()
    {
        patternParent.gameObject.SetActive(currentMode == DAWMode.PatternEditor);
        playlistParent.gameObject.SetActive(currentMode == DAWMode.Playlist);
    }

    private void PopulateInstrumentDropdown()
    {
        NoteManager nm = Object.FindFirstObjectByType<NoteManager>();
        if (nm == null || instrumentDropdown == null) return;

        instrumentDropdown.ClearOptions();
        List<string> options = new List<string>();
        instrumentZValues.Clear();

        // We can hardcode the mapping based on NoteManager's public fields
        // Order: Kick, Snare, Closed HiHat, Open HiHat
        options.Add("Kick");
        instrumentZValues.Add(nm.kickZ);

        options.Add("Snare");
        instrumentZValues.Add(nm.snareZ);

        options.Add("Closed HiHat");
        instrumentZValues.Add(nm.closedHiHatZ);

        options.Add("Open HiHat");
        instrumentZValues.Add(nm.openHiHatZ);

        instrumentDropdown.AddOptions(options);
    }

    private void TryPlaceNote()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        
        // We use the gridLayer mask. If the transparent panel is on 'Ignore Raycast' layer, 
        // and 'Ignore Raycast' is NOT in the gridLayer mask, the raycast will pass through it.
        bool hitSomething = Physics.Raycast(ray, out RaycastHit hit, 100f, gridLayer);
        
        if (hitSomething)
        {
            Vector3 gridPos = hit.point;
            int startX = Mathf.RoundToInt(gridPos.x);
            int startY = Mathf.RoundToInt(gridPos.y);
            
            float z = GetZForSelectedInstrument();

            // FL Studio Style: If a note already exists at this exact position, don't double stack.
            // Check for existing note at this coordinate
            Collider[] existing = Physics.OverlapSphere(new Vector3(startX, startY, z), 0.1f);
            foreach (var col in existing)
            {
                if (col.CompareTag("Note"))
                {
                    Debug.Log("[DEBUG_LOG] Note already exists at this position. Skipping placement.");
                    return;
                }
            }

            Debug.Log($"[DEBUG_LOG] Placement Success! X:{startX}, Y:{startY}, Z:{z}");

            lastPlacedNotes.Clear();
            for (int i = 0; i < currentDuration; i++)
            {
                GameObject note = Instantiate(notePrefab, new Vector3(startX + i, startY, z), Quaternion.identity, patternParent);
                note.tag = "Note";
                note.name = $"Note_{startX + i}_{startY}_{z}";
                if (note.GetComponent<DragAndSnap>() == null) note.AddComponent<DragAndSnap>();
                note.SetActive(true);
                lastPlacedNotes.Add(note);
            }
            
            Object.FindFirstObjectByType<NoteManager>()?.ScanNotes();
        }
        else
        {
            // If we missed the grid, check if we hit the blocking UI layer
            if (Physics.Raycast(ray, out RaycastHit hitAny, 100f))
            {
                Debug.LogWarning($"[DEBUG_LOG] Raycast hit '{hitAny.collider.name}' on layer '{LayerMask.LayerToName(hitAny.collider.gameObject.layer)}'. This is NOT your Grid Layer.");
            }
        }
    }

    private float GetZForSelectedInstrument()
    {
        if (instrumentDropdown != null && instrumentDropdown.value < instrumentZValues.Count)
        {
            return instrumentZValues[instrumentDropdown.value];
        }
        return 0f;
    }

    private void SaveCurrentPattern()
    {
        GameObject pattern;
        if (currentEditingPattern != null)
        {
            pattern = currentEditingPattern;
            // Clear existing children
            foreach (Transform child in pattern.transform)
            {
                Destroy(child.gameObject);
            }
        }
        else
        {
            pattern = new GameObject("Pattern_" + savedPatternPrefabs.Count);
            pattern.transform.SetParent(null);
            savedPatternPrefabs.Add(pattern);
        }
        
        foreach (Transform child in patternParent)
        {
            GameObject noteClone = Instantiate(child.gameObject, pattern.transform);
            noteClone.transform.localPosition = child.localPosition;
        }

        pattern.SetActive(false);
        UpdateSavedPatternsDropdown();
        Debug.Log("Pattern Saved!");
    }

    private void SaveTmpPattern()
    {
        if (tmpPattern != null) Destroy(tmpPattern);
        tmpPattern = new GameObject("TmpPattern");
        tmpPattern.transform.SetParent(null);
        foreach (Transform child in patternParent)
        {
            GameObject noteClone = Instantiate(child.gameObject, tmpPattern.transform);
            noteClone.transform.localPosition = child.localPosition;
        }
        tmpPattern.SetActive(false);
    }

    private void UpdateSavedPatternsDropdown()
    {
        savedPatternsDropdown.ClearOptions();
        List<string> options = savedPatternPrefabs.Select(p => p.name).ToList();
        savedPatternsDropdown.AddOptions(options);
    }

    private void OnSelectPlacePressed()
    {
        if (currentMode == DAWMode.PatternEditor)
        {
            // Open/Edit pattern
            LoadPatternToEditor();
        }
        else
        {
            // Place pattern in playlist
            PlacePatternInPlaylist();
        }
    }

    private void TogglePlayback()
    {
        NoteManager nm = Object.FindFirstObjectByType<NoteManager>();
        if (nm != null)
        {
            bool isPlaying = nm.TogglePlayback();
            playPauseButtonText.text = isPlaying ? "Pause" : "Play";
        }
    }

    private void LoadPatternToEditor()
    {
        if (savedPatternsDropdown.options.Count == 0) return;
        
        // Clear current editor
        foreach (Transform child in patternParent)
        {
            Destroy(child.gameObject);
        }

        GameObject sourcePattern = savedPatternPrefabs[savedPatternsDropdown.value];
        currentEditingPattern = sourcePattern; // Track which pattern we are editing
        foreach (Transform child in sourcePattern.transform)
        {
            GameObject note = Instantiate(child.gameObject, patternParent);
            note.transform.localPosition = child.localPosition;
            note.SetActive(true);
        }
        Object.FindFirstObjectByType<NoteManager>()?.ScanNotes();
    }

    private void PlacePatternInPlaylist()
    {
        if (savedPatternsDropdown.options.Count == 0) return;

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 100f, gridLayer))
        {
            GameObject sourcePattern = savedPatternPrefabs[savedPatternsDropdown.value];
            
            // FL Studio Style: If a pattern already exists at this exact position, don't double stack.
            Vector3 targetPos = new Vector3(Mathf.Round(hit.point.x), Mathf.Round(hit.point.y), 0);
            
            foreach (Transform child in playlistParent)
            {
                if (Vector3.Distance(child.position, targetPos) < 0.1f)
                {
                    Debug.Log("[DEBUG_LOG] Pattern already exists at this position in Playlist. Skipping.");
                    return;
                }
            }

            GameObject instance = Instantiate(sourcePattern, playlistParent);
            instance.transform.position = targetPos;
            instance.SetActive(true);
            
            // Ensure child notes have "Note" tag so NoteManager sees them
            foreach (Transform child in instance.transform)
            {
                child.tag = "Note";
            }
            
            Invoke(nameof(Rescan), 0.05f);
        }
    }
}
