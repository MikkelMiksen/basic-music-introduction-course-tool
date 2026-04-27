using UnityEngine;
using OpenCvSharp;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using CvRect = OpenCvSharp.Rect;

public class LegoDetector : MonoBehaviour
{
    public CameraFeed cameraSource;
    public Renderer displayRenderer;

    private Texture2D outputTexture;

    // Example: BLUE detection (you'll expand later)
    public Scalar lowerColor = new Scalar(100, 150, 50);
    public Scalar upperColor = new Scalar(140, 255, 255);

    public struct Block
    {
        public int gridX;
        public int gridY;
        public CvRect rect;
        public BlockType type;
    }

    public enum BlockType
    {
        Quarter,   // Yellow 2x4
        Eighth,    // White 2x2
        Sixteenth  // Black 1x2
    }

    public List<Block> detectedBlocks = new List<Block>();

    IEnumerator Start()
    {
        // Wait until camera is ready (IMPORTANT)
        while (!cameraSource.IsReady)
            yield return null;

        int w = cameraSource.GetWidth();
        int h = cameraSource.GetHeight();

        Debug.Log($"Camera ready: {w} x {h}");

        outputTexture = new Texture2D(w, h, TextureFormat.RGBA32, false);

        if (displayRenderer != null)
            displayRenderer.material.mainTexture = outputTexture;
    }

    void Update()
    {
        if (!cameraSource.IsReady || outputTexture == null)
            return;

        ProcessFrame();
    }

    void ProcessFrame()
    {
        Color32[] pixels = cameraSource.GetPixels();
        int w = cameraSource.GetWidth();
        int h = cameraSource.GetHeight();

        // Create Mat from webcam
        Mat frame = new Mat(h, w, MatType.CV_8UC4, pixels);

        // Convert RGBA → BGR for OpenCV
        Cv2.CvtColor(frame, frame, ColorConversionCodes.RGBA2BGR);

        // --- DETECTION PIPELINE ---
        Mat hsv = new Mat();
        Cv2.CvtColor(frame, hsv, ColorConversionCodes.BGR2HSV);

        Mat mask = new Mat();
        Cv2.InRange(hsv, lowerColor, upperColor, mask);

        Cv2.FindContours(mask, out Point[][] contours,
            out HierarchyIndex[] hierarchy,
            RetrievalModes.External,
            ContourApproximationModes.ApproxSimple);

        detectedBlocks.Clear();

        foreach (var contour in contours)
        {
            CvRect rect = Cv2.BoundingRect(contour);

            if (rect.Width < 20 || rect.Height < 20)
                continue;

            // --- Determine block type (TEMP: based on size) ---
            BlockType type = BlockType.Quarter;

            if (rect.Width < 30)
                type = BlockType.Sixteenth;
            else if (rect.Width < 50)
                type = BlockType.Eighth;

            // --- Convert to grid ---
            int gridX = (int)((float)rect.X / frame.Width * 64);
            int gridY = (int)((float)rect.Y / frame.Height * 48);

            // --- Store block ---
            detectedBlocks.Add(new Block
            {
                gridX = gridX,
                gridY = gridY,
                rect = rect,
                type = type
            });

            // --- DRAW ---
            Cv2.Rectangle(frame, rect, new Scalar(0, 255, 0), 2);
        }

        // --- DISPLAY ---
        ShowFrame(frame);
    }

    void ShowFrame(Mat frame)
    {
        // Convert BGR → RGBA for Unity
        Mat rgba = new Mat();
        Cv2.CvtColor(frame, rgba, ColorConversionCodes.BGR2RGBA);

        // Ensure continuous memory (prevents split image bug)
        if (!rgba.IsContinuous())
            rgba = rgba.Clone();

        byte[] data = new byte[rgba.Total() * rgba.ElemSize()];
        Marshal.Copy(rgba.Data, data, 0, data.Length);

        outputTexture.LoadRawTextureData(data);
        outputTexture.Apply();

        rgba.Dispose();
    }
}