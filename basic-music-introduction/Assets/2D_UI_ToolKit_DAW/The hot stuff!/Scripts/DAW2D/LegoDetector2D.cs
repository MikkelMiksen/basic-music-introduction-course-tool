using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using OpenCvSharp;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using CvRect = OpenCvSharp.Rect;
using DAW2D;

public class LegoDetector2D : MonoBehaviour
{
    public Button nextSceneButton;
    [Header("Camera")]
    public CameraFeed cameraSource;

    [Header("Preview Renderers")]
    public Renderer cameraRenderer;
    public Renderer warpedRenderer;

    [Header("Scene")]
    public string nextSceneName = "MainScene";

    [Header("Piano Roll")]
    public PianoRollController pianoRollController;

    [Header("Grid")]
    public int gridCols = 64;
    public int gridRows = 44;

    [Header("Warped Board Size")]
    public int warpedWidth = 640;
    public int warpedHeight = 440;

    [Header("Blue Corner Detection")]
    public Scalar blueLower = new Scalar(100, 120, 80);
    public Scalar blueUpper = new Scalar(130, 255, 255);

    [SerializeField] private float blueRectMinSize = 10f;

    [Header("Brick Colors")]

    // Quarter Notes
    public Scalar yellowLower = new Scalar(20, 100, 100);
    public Scalar yellowUpper = new Scalar(35, 255, 255);

    // Eighth Notes
    public Scalar whiteLower = new Scalar(0, 0, 200);
    public Scalar whiteUpper = new Scalar(180, 25, 255);

    // Sixteenth Notes
    public Scalar pinkLower = new Scalar(120, 50, 100);
    public Scalar pinkUpper = new Scalar(175, 255, 255);

    [HideInInspector]
    public bool buildingPlateFound = false;

    private Texture2D cameraTexture;
    private Texture2D warpedTexture;

    private Point2f[] corners = new Point2f[4];

    private readonly Point2f[] destinationCorners =
    {
        new Point2f(0, 0),       // TL
        new Point2f(640, 0),     // TR
        new Point2f(640, 440),   // BR
        new Point2f(0, 440)      // BL
    };

    private List<NoteData> currentDetectedNotes = new();

    IEnumerator Start()
    {
        while (!cameraSource.IsReady)
            yield return null;

        nextSceneButton.onClick.AddListener(NextScene);

        int w = cameraSource.GetWidth();
        int h = cameraSource.GetHeight();

        cameraTexture = new Texture2D(w, h, TextureFormat.RGBA32, false);
        warpedTexture = new Texture2D(warpedWidth, warpedHeight, TextureFormat.RGBA32, false);

        if (cameraRenderer != null)
            cameraRenderer.material.mainTexture = cameraTexture;

        if (warpedRenderer != null)
            warpedRenderer.material.mainTexture = warpedTexture;
    }

    void Update()
    {
        if (!cameraSource.IsReady)
            return;

        ProcessFrame();
    }

    void ProcessFrame()
    {
        Color32[] pixels = cameraSource.GetPixels();

        int w = cameraSource.GetWidth();
        int h = cameraSource.GetHeight();

        Mat frame = new Mat(h, w, MatType.CV_8UC4, pixels);

        // RGBA -> BGR
        Cv2.CvtColor(frame, frame, ColorConversionCodes.RGBA2BGR);
        Cv2.Flip(frame, frame, FlipMode.X);

        Mat hsv = new Mat();
        Cv2.CvtColor(frame, hsv, ColorConversionCodes.BGR2HSV);

        buildingPlateFound = DetectCorners(frame, hsv);

        currentDetectedNotes.Clear();

        if (buildingPlateFound)
        {
            DrawBoardOutline(frame);

            Mat warped = WarpBoard(frame);

            DetectBlocks(warped);

            if (pianoRollController != null)
            {
                pianoRollController.SaveCurrentInput(currentDetectedNotes);
            }

            ShowFrame(warped, warpedTexture);

            warped.Dispose();
        }

        ShowFrame(frame, cameraTexture);

        hsv.Dispose();
        frame.Dispose();
    }

    bool DetectCorners(Mat frame, Mat hsv)
    {
        Mat mask = new Mat();

        Cv2.InRange(hsv, blueLower, blueUpper, mask);

        Mat kernel = Cv2.GetStructuringElement(
            MorphShapes.Rect,
            new Size(5, 5));

        Cv2.MorphologyEx(mask, mask, MorphTypes.Open, kernel);
        Cv2.MorphologyEx(mask, mask, MorphTypes.Close, kernel);

        Cv2.FindContours(
            mask,
            out Point[][] contours,
            out _,
            RetrievalModes.External,
            ContourApproximationModes.ApproxSimple);

        List<Point> foundPoints = new();

        foreach (var contour in contours)
        {
            CvRect rect = Cv2.BoundingRect(contour);

            if (rect.Width < blueRectMinSize ||
                rect.Height < blueRectMinSize)
                continue;

            Point center = new Point(
                rect.X + rect.Width / 2,
                rect.Y + rect.Height / 2);

            foundPoints.Add(center);

            // Draw blue rectangles
            Cv2.Rectangle(
                frame,
                rect,
                new Scalar(255, 0, 0),
                3);

            Cv2.Circle(
                frame,
                center,
                5,
                new Scalar(0, 255, 255),
                -1);
        }

        if (foundPoints.Count < 4)
        {
            mask.Dispose();
            return false;
        }

        Point[] pts = foundPoints.Take(4).ToArray();

        Point2f[] sorted = new Point2f[4];

        // Top-left
        sorted[0] = pts.OrderBy(p => p.X + p.Y).First();

        // Bottom-right
        sorted[2] = pts.OrderByDescending(p => p.X + p.Y).First();

        // Top-right
        sorted[1] = pts.OrderBy(p => p.X - p.Y).First();

        // Bottom-left
        sorted[3] = pts.OrderByDescending(p => p.X - p.Y).First();

        corners = sorted;

        mask.Dispose();

        return ValidateBoard();
    }

    bool ValidateBoard()
    {
        float widthTop = Vector2.Distance(
            ToVec2(corners[0]),
            ToVec2(corners[1]));

        float widthBottom = Vector2.Distance(
            ToVec2(corners[3]),
            ToVec2(corners[2]));

        float heightLeft = Vector2.Distance(
            ToVec2(corners[0]),
            ToVec2(corners[3]));

        float heightRight = Vector2.Distance(
            ToVec2(corners[1]),
            ToVec2(corners[2]));

        bool widthsMatch =
            Mathf.Abs(widthTop - widthBottom) < 100;

        bool heightsMatch =
            Mathf.Abs(heightLeft - heightRight) < 100;

        bool largeEnough =
            widthTop > 100 &&
            heightLeft > 100;

        return widthsMatch && heightsMatch && largeEnough;
    }

    void DrawBoardOutline(Mat frame)
    {
        Point[] poly =
        {
            (Point)corners[0],
            (Point)corners[1],
            (Point)corners[2],
            (Point)corners[3]
        };

        Cv2.Polylines(
            frame,
            new[] { poly },
            true,
            new Scalar(0, 255, 0),
            4);
    }

    Mat WarpBoard(Mat frame)
    {
        Mat warped = new Mat();

        Mat transform = Cv2.GetPerspectiveTransform(
            corners,
            destinationCorners);

        Cv2.WarpPerspective(
            frame,
            warped,
            transform,
            new Size(warpedWidth, warpedHeight));

        return warped;
    }

    void DetectBlocks(Mat warped)
    {
        Mat hsv = new Mat();

        Cv2.CvtColor(
            warped,
            hsv,
            ColorConversionCodes.BGR2HSV);

        // Quarter Notes
        ProcessColor(
            warped,
            hsv,
            yellowLower,
            yellowUpper,
            4,
            new Scalar(255, 0, 255));

        // Eighth Notes
        ProcessColor(
            warped,
            hsv,
            whiteLower,
            whiteUpper,
            2,
            new Scalar(255, 255, 0));

        // Sixteenth Notes
        ProcessColor(
            warped,
            hsv,
            pinkLower,
            pinkUpper,
            1,
            new Scalar(0, 255, 0));

        hsv.Dispose();
    }

    void ProcessColor(
        Mat frame,
        Mat hsv,
        Scalar lower,
        Scalar upper,
        int duration,
        Scalar drawColor)
    {
        Mat mask = new Mat();

        Cv2.InRange(hsv, lower, upper, mask);

        Mat kernel = Cv2.GetStructuringElement(
            MorphShapes.Rect,
            new Size(5, 5));

        Cv2.MorphologyEx(mask, mask, MorphTypes.Open, kernel);
        Cv2.MorphologyEx(mask, mask, MorphTypes.Close, kernel);

        Cv2.FindContours(
            mask,
            out Point[][] contours,
            out _,
            RetrievalModes.External,
            ContourApproximationModes.ApproxSimple);

        foreach (var contour in contours)
        {
            CvRect rect = Cv2.BoundingRect(contour);

            if (rect.Width < 10 || rect.Height < 10)
                continue;

            float centerX = rect.X + rect.Width * 0.5f;
            float centerY = rect.Y + rect.Height * 0.5f;

            int gx = Mathf.Clamp(
                Mathf.FloorToInt((centerX / warpedWidth) * gridCols),
                0,
                gridCols - 1);

            int gy = Mathf.Clamp(
                Mathf.FloorToInt((centerY / warpedHeight) * gridRows),
                0,
                gridRows - 1);

            // Flip vertically so high notes are top
            gy = (gridRows - 1) - gy;

            currentDetectedNotes.Add(new NoteData
            {
                tick = gx,
                pitch = gy,
                duration = duration,
                velocity = 0.8f
            });

            Cv2.Rectangle(
                frame,
                rect,
                drawColor,
                2);

            Cv2.PutText(
                frame,
                $"{gx},{gy}",
                new Point(rect.X, rect.Y - 5),
                HersheyFonts.HersheySimplex,
                0.5,
                drawColor,
                1);
        }

        mask.Dispose();
    }

    void ShowFrame(Mat frame, Texture2D tex)
    {
        Mat rgba = new Mat();

        Cv2.CvtColor(
            frame,
            rgba,
            ColorConversionCodes.BGR2RGBA);

        if (!rgba.IsContinuous())
            rgba = rgba.Clone();

        byte[] data = new byte[
            rgba.Total() * rgba.ElemSize()];

        Marshal.Copy(
            rgba.Data,
            data,
            0,
            data.Length);

        tex.LoadRawTextureData(data);
        tex.Apply();

        rgba.Dispose();
    }

    Vector2 ToVec2(Point2f p)
    {
        return new Vector2(p.X, p.Y);
    }

    void NextScene() {if (buildingPlateFound) SceneManager.LoadScene(nextSceneName);}
}