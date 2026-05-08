using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using Data_holders.instruments;
using System;
using System.Linq;

namespace DAW2D
{
    public class PianoRollController : MonoBehaviour
    {
        public UIDocument uiDocument;
        
        [Header("Settings")]
        public int gridWidth = 64;
        public int gridHeight = 44;
        
        [Header("Data")]
        public List<Pattern> patterns = new();
        public int selectedPatternIndex;
        public Instruments selectedInstrument = Instruments.PluckSynth;
        
        public enum PlayMode { Pattern, Playlist }
        public PlayMode currentMode = PlayMode.Pattern;
        
        private DropdownField instrumentDropdown;
        private DropdownField patternDropdown;
        private DropdownField modeDropdown;
        private Button playPauseButton;
        private VisualElement pianoGrid;
        private VisualElement pianoKeyboard;
        private VisualElement playhead;
        private VisualElement timeline;
        
        private bool isPlaying = false;
        private NoteManager2D noteManager;

        void OnEnable()
        {
            
            Debug.Log("displays connected: " + Display.displays.Length);
            if (Display.displays.Length > 1)
            {
                Display.displays[1].Activate();
            }

            // Force UI to render on Display 2 (index 1) so Unity editor doesn't reset it
            if (uiDocument != null && uiDocument.panelSettings != null)
            {
                uiDocument.panelSettings.targetDisplay = 0; // 0 = Display 1, 1 = Display 2
            }
            
            var root = uiDocument.rootVisualElement;
            if (root == null)
            {
                Debug.LogError("[DEBUG_LOG] UIDocument root is null!");
                return;
            }
            
            instrumentDropdown = root.Q<DropdownField>("InstrumentDropdown");
            patternDropdown = root.Q<DropdownField>("PatternDropdown");
            modeDropdown = root.Q<DropdownField>("ModeDropdown");
            playPauseButton = root.Q<Button>("PlayPauseButton");
            pianoGrid = root.Q<VisualElement>("PianoGrid");
            pianoKeyboard = root.Q<VisualElement>("PianoKeyboard");
            playhead = root.Q<VisualElement>("Playhead");
            timeline = root.Q<VisualElement>("Timeline");

            if (instrumentDropdown != null) instrumentDropdown.style.width = 400;
            if (patternDropdown != null) patternDropdown.style.width = 400;
            if (modeDropdown != null) modeDropdown.style.width = 300;

            if (instrumentDropdown == null || patternDropdown == null || modeDropdown == null || 
                playPauseButton == null || pianoGrid == null || pianoKeyboard == null || 
                playhead == null || timeline == null)
            {
                // Detailed logging for which one is missing
                if (instrumentDropdown == null) Debug.LogError("[DEBUG_LOG] InstrumentDropdown not found!");
                if (patternDropdown == null) Debug.LogError("[DEBUG_LOG] PatternDropdown not found!");
                if (modeDropdown == null) Debug.LogError("[DEBUG_LOG] ModeDropdown not found!");
                if (playPauseButton == null) Debug.LogError("[DEBUG_LOG] PlayPauseButton not found!");
                if (pianoGrid == null) Debug.LogError("[DEBUG_LOG] PianoGrid not found!");
                if (pianoKeyboard == null) Debug.LogError("[DEBUG_LOG] PianoKeyboard not found!");
                if (playhead == null) Debug.LogError("[DEBUG_LOG] Playhead not found!");
                if (timeline == null) Debug.LogError("[DEBUG_LOG] Timeline not found!");
                return;
            }

            noteManager = FindFirstObjectByType<NoteManager2D>();
            
            // Initialize Dropdowns
            instrumentDropdown.choices = Enum.GetNames(typeof(Instruments)).ToList();
            instrumentDropdown.value = selectedInstrument.ToString();
            instrumentDropdown.RegisterValueChangedCallback(evt => {
                selectedInstrument = (Instruments)Enum.Parse(typeof(Instruments), evt.newValue);
            });
            
            RefreshPatternChoices();
            patternDropdown.RegisterValueChangedCallback(evt => {
                selectedPatternIndex = patterns.FindIndex(p => p.name == evt.newValue);
            });
            
            modeDropdown.choices = Enum.GetNames(typeof(PlayMode)).ToList();
            modeDropdown.value = currentMode.ToString();
            modeDropdown.RegisterValueChangedCallback(evt => {
                currentMode = (PlayMode)Enum.Parse(typeof(PlayMode), evt.newValue);
                UpdateModeVisibility();
            });
            
            playPauseButton.clicked += TogglePlayPause;
            
            // Generate Visual Grid (64x48)
            GenerateGrid();
            GeneratePianoKeys();
            UpdateModeVisibility();
            
            // Create initial patterns if empty
            if (patterns.Count == 0)
            {
                patterns.Add(new Pattern { name = "Pattern 1" });
                RefreshPatternChoices();
            }
        }

        private void Update()
        {
            if (noteManager != null && playhead != null)
            {
                float progress = (float)noteManager.CurrentTick / gridWidth;
                playhead.style.left = Length.Percent(progress * 100f);
            }
        }

        private void UpdateModeVisibility()
        {
            if (currentMode == PlayMode.Pattern)
            {
                pianoKeyboard.style.display = DisplayStyle.Flex;
                instrumentDropdown.style.display = DisplayStyle.Flex;
                timeline.style.marginLeft = 60; // Align with keyboard
            }
            else
            {
                pianoKeyboard.style.display = DisplayStyle.None;
                instrumentDropdown.style.display = DisplayStyle.None;
                timeline.style.marginLeft = 0; // Full width in playlist
            }
            
            pianoGrid.Clear();
            if (currentMode == PlayMode.Pattern)
            {
                // Re-draw current pattern if in pattern mode
                if (selectedPatternIndex >= 0 && selectedPatternIndex < patterns.Count)
                {
                    var pattern = patterns[selectedPatternIndex];
                    var sequence = pattern.instrumentSequences.Find(s => s.instrument == selectedInstrument);
                    if (sequence != null) UpdateVisualGrid(sequence.notes);
                }
            }
        }

        private void GeneratePianoKeys()
        {
            pianoKeyboard.Clear();
            float keyHeight = 100f / gridHeight;
            
            // Map gridHeight to MIDI. Let's assume bottom is C2 (36) and top is C6 (84) for 48 keys
            int baseMidi = 36;

            for (int i = 0; i < gridHeight; i++)
            {
                int rowIdx = i;
                int midiNote = baseMidi + (gridHeight - 1 - i); // Flip so high notes are at the top

                var key = new VisualElement();
                key.style.height = Length.Percent(keyHeight);
                key.style.width = Length.Percent(100f);
                
                int noteInOctave = midiNote % 12;
                bool isBlack = noteInOctave == 1 || noteInOctave == 3 || noteInOctave == 6 || noteInOctave == 8 || noteInOctave == 10;
                
                key.style.backgroundColor = isBlack ? Color.black : Color.white;
                key.style.borderBottomWidth = 1;
                key.style.borderBottomColor = new Color(0.2f, 0.2f, 0.2f);
                
                // Audio Preview on Click
                key.RegisterCallback<MouseDownEvent>(evt => {
                    if (noteManager != null)
                    {
                        noteManager.PlayPreviewNote(midiNote, 0.7f);
                        key.style.backgroundColor = Color.yellow; // Visual feedback
                    }
                });

                // Add label for drum keys
                string drumLabel = GetDrumLabel(midiNote);
                if (!string.IsNullOrEmpty(drumLabel))
                {
                    var label = new Label(drumLabel);
                    label.style.color = isBlack ? Color.white : Color.black;
                    label.style.fontSize = 8;
                    label.style.unityTextAlign = TextAnchor.MiddleRight;
                    label.pickingMode = PickingMode.Ignore;
                    key.Add(label);
                }

                key.RegisterCallback<MouseUpEvent>(evt => {
                    key.style.backgroundColor = isBlack ? Color.black : Color.white;
                });
                
                key.RegisterCallback<MouseLeaveEvent>(evt => {
                    key.style.backgroundColor = isBlack ? Color.black : Color.white;
                });
                
                pianoKeyboard.Add(key);
            }
        }

        private void RefreshPatternChoices()
        {
            patternDropdown.choices = patterns.Select(p => p.name).ToList();
            if (patterns.Count > 0) patternDropdown.value = patterns[selectedPatternIndex].name;
        }

        private void TogglePlayPause()
        {
            isPlaying = !isPlaying;
            playPauseButton.text = isPlaying ? "Pause" : "Play";
            // Integrate with NoteManager here
            var noteManager = FindFirstObjectByType<NoteManager2D>();
            if (noteManager != null) noteManager.SetPlaying(isPlaying);
            
            PlayheadController.instance.PlayPause_Button();
        }

        private void GenerateGrid()
        {
            pianoGrid.Clear();
            // We use a high-performance approach or just containers
            // For 64x48, individual elements might be heavy, but let's try
            // Alternatively, use a custom painter or a background texture
            pianoGrid.style.flexWrap = Wrap.Wrap;
            pianoGrid.style.flexDirection = FlexDirection.Row;
            
            // To make it look like a grid, we need fixed sizes or percentages
            float cellWidth = 100f / gridWidth;
            float cellHeight = 100f / gridHeight;

            // Instead of 3072 elements, let's just make sure the container is set up
            // and we can overlay notes.
            pianoGrid.style.position = Position.Relative;
        }

        public void SaveCurrentInput(List<NoteData> detectedNotes)
        {
            if (selectedPatternIndex < 0 || selectedPatternIndex >= patterns.Count) return;
            
            var pattern = patterns[selectedPatternIndex];
            var sequence = pattern.GetOrCreateSequence(selectedInstrument);
            
            sequence.notes = new List<NoteData>(detectedNotes);
            
            // Visual Update
            UpdateVisualGrid(detectedNotes);
            
            Debug.Log($"Saved {detectedNotes.Count} notes to {selectedInstrument} in {pattern.name}");
        }

        private void UpdateVisualGrid(List<NoteData> notes)
        {
            pianoGrid.Clear();
            foreach (var note in notes)
            {
                var visualNote = new VisualElement();
                visualNote.style.position = Position.Absolute;
                // Tick maps left to right across the grid width
                visualNote.style.left = Length.Percent((float)note.tick / gridWidth * 100f);
                // Pitch: mirrored on horizontal axis (0 = top of grid)
                visualNote.style.top = Length.Percent((float)note.pitch / gridHeight * 100f);
                visualNote.style.width = Length.Percent((float)note.duration / gridWidth * 100f);
                visualNote.style.height = Length.Percent(100f / gridHeight);
                visualNote.style.backgroundColor = Color.green;
                visualNote.style.borderLeftWidth = 1;
                visualNote.style.borderLeftColor = Color.black;
                pianoGrid.Add(visualNote);
            }
        }
        private string GetDrumLabel(int midiNote)
        {
            // Simplified mapping based on user request
            if (midiNote == 36) return "KICK";
            if (midiNote == 37) return "SNARE";
            if (midiNote == 38) return "CHH";
            if (midiNote == 39) return "OHH";
            return "";
        }
    }
}
