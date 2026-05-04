using UnityEngine;
using OpenCvSharp;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using CvRect = OpenCvSharp.Rect;
using DAW2D;

public class LegoDetector2D : MonoBehaviour
{
    public CameraFeed cameraSource;
    public Renderer displayRenderer;
    public DAW2D.PianoRollController pianoRollController;
    [SerializeField] private float blueREctMinSize = 5;

    private Texture2D outputTexture;

    [Header("Grid Setup")]
    public int gridCols = 64;
    public int gridRows = 42;

    [Header("Color Ranges (HSV)")]
    // Yellow
    public Scalar yellowLower = new Scalar(20, 100, 100);
    public Scalar yellowUpper = new Scalar(35, 255, 255);
    // White
    public Scalar whiteLower = new Scalar(0, 0, 200);
    public Scalar whiteUpper = new Scalar(180, 40, 255);
    // Black
    // public Scalar blackLower = new Scalar(0, 0, 0);
    // public Scalar blackUpper = new Scalar(180, 255, 100);
    // Pink
    public Scalar pinkLower = new Scalar(130, 80, 140);
    public Scalar pinkUpper = new Scalar(170, 255, 255);
    public Scalar blueLower = new Scalar(100, 120, 80);
    public Scalar blueUpper = new Scalar(130, 255, 255);

    private List<NoteData> currentDetectedNotes = new List<NoteData>();
    private Point2f[] corners = new Point2f[4];
    private bool cornersDetected = false;

    IEnumerator Start()
    {
        while (!cameraSource.IsReady)
            yield return null;

        int w = cameraSource.GetWidth();
        int h = cameraSource.GetHeight();
        outputTexture = new Texture2D(w, h, TextureFormat.RGBA32, false);

        if (displayRenderer != null)
            displayRenderer.material.mainTexture = outputTexture;
    }

    void Update()
    {
        if (!cameraSource.IsReady || outputTexture == null)
            return;

        ProcessFrame();
        
        // Auto-save to PianoRollController (could be triggered by a button too)
        if (pianoRollController != null && currentDetectedNotes.Count > 0)
        {
            pianoRollController.SaveCurrentInput(currentDetectedNotes);
        }
        
        if(cornersDetected)
            Debug.Log("Corners detected: " + corners[0].ToString() + ", " + corners[1].ToString() + ", " + corners[2].ToString() + ", " + corners[3].ToString());
    }

    void ProcessFrame()
    {
        Color32[] pixels = cameraSource.GetPixels();
        int w = cameraSource.GetWidth();
        int h = cameraSource.GetHeight();

        Mat frame = new Mat(h, w, MatType.CV_8UC4, pixels);
        Cv2.CvtColor(frame, frame, ColorConversionCodes.RGBA2BGR);

        Mat hsv = new Mat();
        Cv2.CvtColor(frame, hsv, ColorConversionCodes.BGR2HSV);

        DetectCorners(frame, hsv);

        currentDetectedNotes.Clear();
        if (cornersDetected)
        {
            // Perspective transform would be ideal here, but for now we use simple grid if corners are bounding the area
            ProcessColor(frame, hsv, yellowLower, yellowUpper, 2, new Scalar(255, 0, 255)); // Quarter
            ProcessColor(frame, hsv, whiteLower, whiteUpper, 4, new Scalar(255, 255, 0));  // Eighth
            ProcessColor(frame, hsv, pinkLower, pinkUpper, 1, new Scalar(0, 255, 0));    // Sixteenth
            // ProcessColor(frame, hsv, blackLower, blackUpper, 1, new Scalar(255, 255, 255)); // Sixteenth
        }

        ShowFrame(frame);
    }

    void DetectCorners(Mat frame, Mat hsv)
    {
        Mat mask = new Mat();
        Cv2.InRange(hsv, blueLower, blueUpper, mask);
        
        Cv2.FindContours(mask, out Point[][] contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);
        
        List<Point> cornerPoints = new List<Point>();
        foreach (var contour in contours)
        {
            CvRect rect = Cv2.BoundingRect(contour);
            if (rect.Width > blueREctMinSize && rect.Height > blueREctMinSize)
            {
                cornerPoints.Add(new Point(rect.X + rect.Width/2, rect.Y + rect.Height/2));
                Cv2.Rectangle(frame, rect, new Scalar(255, 0, 0), 2);
            }
        }

        if (cornerPoints.Count >= 4)
        {
            // Convert to list of exactly 4 points (you may want smarter selection later)
            var pts = cornerPoints.Take(4).ToArray();

            Point2f[] sorted = new Point2f[4];

            // Sum and difference method
            // top-left = smallest (x + y)
            // bottom-right = largest (x + y)
            // top-right = smallest (x - y)
            // bottom-left = largest (x - y)

            sorted[0] = pts.OrderBy(p => p.X + p.Y).First(); // top-left
            sorted[2] = pts.OrderByDescending(p => p.X + p.Y).First(); // bottom-right
            sorted[1] = pts.OrderBy(p => p.X - p.Y).First(); // top-right
            sorted[3] = pts.OrderByDescending(p => p.X - p.Y).First(); // bottom-left

            corners = sorted;
            cornersDetected = true;
        }
    }

    void ProcessColor(Mat frame, Mat hsv, Scalar lower, Scalar upper, int duration, Scalar drawColor)
    {
        Mat mask = new Mat();
        Cv2.InRange(hsv, lower, upper, mask);

        Cv2.FindContours(mask, out Point[][] contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);

        foreach (var contour in contours)
        {
            CvRect rect = Cv2.BoundingRect(contour);
            if (rect.Width < 10 || rect.Height < 10) continue;

            // Map rect center to 64x48 grid
            int gx = (int)((float)rect.X / frame.Width * gridCols);
            int gy = (int)((float)rect.Y / frame.Height * gridRows);

            currentDetectedNotes.Add(new NoteData
            {
                tick = gx,
                pitch = gy,
                duration = duration
            });

            Cv2.Rectangle(frame, rect, drawColor, 2);
        }
    }

    void ShowFrame(Mat frame)
    {
        Mat rgba = new Mat();
        Cv2.CvtColor(frame, rgba, ColorConversionCodes.BGR2RGBA);
        byte[] data = new byte[rgba.Total() * rgba.ElemSize()];
        Marshal.Copy(rgba.Data, data, 0, data.Length);
        outputTexture.LoadRawTextureData(data);
        outputTexture.Apply();
        rgba.Dispose();
    }
}
