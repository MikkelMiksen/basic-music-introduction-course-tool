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

    // Brick Setup
    public enum BlockType
    {
        Quarter,   // Yellow
        Eighth,    // White
        Sixteenth  // Black
    }

    public struct Block
    {
        public int gridX;
        public int gridY;
        public CvRect rect;
        public BlockType type;
    }

    public List<Block> detectedBlocks = new List<Block>();

    [Header("Corner Detection (Blue)")]

    // Blue corners
    public Scalar blueLower = new Scalar(100, 120, 150);
    public Scalar blueUpper = new Scalar(130, 255, 255);
    
    // Color Ranges (HSV)
    [Header("Color Ranges")]

    // Yellow
    public Scalar yellowLower = new Scalar(20, 100, 100);
    public Scalar yellowUpper = new Scalar(35, 255, 255);

    // White
    public Scalar whiteLower = new Scalar(0, 0, 200);
    public Scalar whiteUpper = new Scalar(180, 40, 255);

    // Black
    public Scalar blackLower = new Scalar(0, 0, 0);
    public Scalar blackUpper = new Scalar(180, 255, 40);

    // Pink
    public Scalar pinkLower = new Scalar(130, 140, 140);
    public Scalar pinkUpper = new Scalar(170, 255, 255);

    // Init
    IEnumerator Start()
    {
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

    // Processing
    void ProcessFrame()
    {
        Color32[] pixels = cameraSource.GetPixels();
        int w = cameraSource.GetWidth();
        int h = cameraSource.GetHeight();

        Mat frame = new Mat(h, w, MatType.CV_8UC4, pixels);

        // Convert RGBA → BGR (OpenCV uses BGR)
        Cv2.CvtColor(frame, frame, ColorConversionCodes.RGBA2BGR);

        detectedBlocks.Clear();

        Mat hsv = new Mat();
        Cv2.CvtColor(frame, hsv, ColorConversionCodes.BGR2HSV);

        //Process corners
        ProcessCorners(frame, hsv);

        // Process each color
        ProcessColor(frame, hsv, pinkLower, pinkUpper, BlockType.Quarter, new Scalar(0, 255, 0));
        ProcessColor(frame, hsv, yellowLower, yellowUpper, BlockType.Eighth, new Scalar(255, 0, 255));
        ProcessColor(frame, hsv, whiteLower, whiteUpper, BlockType.Sixteenth, new Scalar(255, 255, 0));
        // ProcessColor(frame, hsv, blackLower, blackUpper, BlockType.Eighth, new Scalar(255, 255, 255));

        // Show result
        ShowFrame(frame);

        Vector2 center = new Vector2(w / 2, h / 2);
        Vec3b hsvPixel = hsv.At<Vec3b>((int)center.y, (int)center.x);
        Debug.Log($"HSV at center: H={hsvPixel.Item0}, S={hsvPixel.Item1}, V={hsvPixel.Item2}");
    }

    // Color Detection
    void ProcessColor(Mat frame, Mat hsv, Scalar lower, Scalar upper, BlockType type, Scalar drawColor)
    {
        Mat mask = new Mat();
        Cv2.InRange(hsv, lower, upper, mask);

        // Clean noise
        Mat kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(5, 5));
        Cv2.MorphologyEx(mask, mask, MorphTypes.Open, kernel);
        Cv2.MorphologyEx(mask, mask, MorphTypes.Close, kernel);

        Cv2.FindContours(mask, out Point[][] contours,
            out HierarchyIndex[] hierarchy,
            RetrievalModes.External,
            ContourApproximationModes.ApproxSimple);

        foreach (var contour in contours)
        {
            CvRect rect = Cv2.BoundingRect(contour);

            if (rect.Width < 15 || rect.Height < 15)
                continue;

            int gridX = (int)((float)rect.X / frame.Width * 64);
            int gridY = (int)((float)rect.Y / frame.Height * 48);

            detectedBlocks.Add(new Block
            {
                gridX = gridX,
                gridY = gridY,
                rect = rect,
                type = type
            });

            // Draw rectangle
            Cv2.Rectangle(frame, rect, drawColor, 2);

            // Label
            Cv2.PutText(
                frame,
                type.ToString(),
                new Point(rect.X, rect.Y - 5),
                HersheyFonts.HersheySimplex,
                0.5,
                drawColor,
                1
            );
        }
    }

    void ProcessCorners(Mat frame, Mat hsv)
    {
        Mat mask = new Mat();
        Cv2.InRange(hsv, blueLower, blueUpper, mask);

        Mat kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(5, 5));
        Cv2.MorphologyEx(mask, mask, MorphTypes.Open, kernel);
        Cv2.MorphologyEx(mask, mask, MorphTypes.Close, kernel);

        Cv2.FindContours(mask, out Point[][] contours,
            out HierarchyIndex[] hierarchy,
            RetrievalModes.External,
            ContourApproximationModes.ApproxSimple);

        foreach (var contour in contours)
        {
            OpenCvSharp.Rect rect = Cv2.BoundingRect(contour);

            if (rect.Width < 20 || rect.Height < 20)
                continue;

            // 🔵 Draw BLUE corners
            Cv2.Rectangle(frame, rect, new Scalar(255, 0, 0), 3);

            Cv2.PutText(
                frame,
                "CORNER",
                new Point(rect.X, rect.Y - 5),
                HersheyFonts.HersheySimplex,
                0.6,
                new Scalar(255, 0, 0),
                2
            );
        }
    }

    // Display
    void ShowFrame(Mat frame)
    {
        Mat rgba = new Mat();
        Cv2.CvtColor(frame, rgba, ColorConversionCodes.BGR2RGBA);

        if (!rgba.IsContinuous())
            rgba = rgba.Clone();

        byte[] data = new byte[rgba.Total() * rgba.ElemSize()];
        Marshal.Copy(rgba.Data, data, 0, data.Length);

        outputTexture.LoadRawTextureData(data);
        outputTexture.Apply();

        rgba.Dispose();
    }
}