using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using OpenCvSharp;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using CvRect = OpenCvSharp.Rect;

public class BoardSetupManager : MonoBehaviour
{
    public static Point2f[] SavedCorners;

    [Header("Camera")]
    public CameraFeed cameraSource;

    [Header("Preview")]
    public Renderer cameraRenderer;
    public Renderer warpedRenderer;

    [Header("Continue")]
    public Button continueButton;
    public string nextSceneName = "MainScene";

    [Header("Warp")]
    public int warpedWidth = 640;
    public int warpedHeight = 440;

    [Header("Blue Detection")]
    public Scalar blueLower = new Scalar(100, 120, 80);
    public Scalar blueUpper = new Scalar(130, 255, 255);

    public float minCornerSize = 10f;

    private Texture2D cameraTexture;
    private Texture2D warpedTexture;

    private Point2f[] corners = new Point2f[4];

    private readonly Point2f[] destinationCorners =
    {
        new Point2f(0,0),
        new Point2f(640,0),
        new Point2f(640,440),
        new Point2f(0,440)
    };

    private bool boardFound = false;

    IEnumerator Start()
    {
        continueButton.interactable = false;

        while (!cameraSource.IsReady)
            yield return null;

        int w = cameraSource.GetWidth();
        int h = cameraSource.GetHeight();

        cameraTexture = new Texture2D(w, h, TextureFormat.RGBA32, false);
        warpedTexture = new Texture2D(warpedWidth, warpedHeight, TextureFormat.RGBA32, false);

        cameraRenderer.material.mainTexture = cameraTexture;
        warpedRenderer.material.mainTexture = warpedTexture;
    }

    void Update()
    {
        if (!cameraSource.IsReady)
            return;

        ProcessFrame();

        continueButton.interactable = boardFound;
    }

    void ProcessFrame()
    {
        Color32[] pixels = cameraSource.GetPixels();

        int w = cameraSource.GetWidth();
        int h = cameraSource.GetHeight();

        Mat frame = new Mat(h, w, MatType.CV_8UC4, pixels);

        Cv2.CvtColor(frame, frame, ColorConversionCodes.RGBA2BGR);
        Cv2.Flip(frame, frame, FlipMode.Y);

        Mat hsv = new Mat();
        Cv2.CvtColor(frame, hsv, ColorConversionCodes.BGR2HSV);

        boardFound = DetectCorners(frame, hsv);

        if (!boardFound)
        {
            DrawOutline(frame);

            Mat warped = WarpBoard(frame);

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

        Cv2.FindContours(
            mask,
            out Point[][] contours,
            out _,
            RetrievalModes.External,
            ContourApproximationModes.ApproxSimple);

        List<Point> found = new();

        foreach (var contour in contours)
        {
            CvRect rect = Cv2.BoundingRect(contour);

            if (rect.Width < minCornerSize ||
                rect.Height < minCornerSize)
                continue;

            Point center = new Point(
                rect.X + rect.Width / 2,
                rect.Y + rect.Height / 2);

            found.Add(center);

            Cv2.Rectangle(frame, rect, new Scalar(255,0,0), 3);
        }

        if (found.Count < 4)
            return false;

        Point[] pts = found.ToArray();

        corners = new Point2f[]
        {
            pts.OrderBy(p => p.X + p.Y).First(),              // Top Left
            pts.OrderByDescending(p => p.X - p.Y).First(),   // Top Right
            pts.OrderByDescending(p => p.X + p.Y).First(),   // Bottom Right
            pts.OrderBy(p => p.X - p.Y).First()              // Bottom Left
        };

        Cv2.Circle(frame, (Point)corners[0], 12, new Scalar(0,255,0), -1);
        Cv2.Circle(frame, (Point)corners[1], 12, new Scalar(0,0,255), -1);
        Cv2.Circle(frame, (Point)corners[2], 12, new Scalar(255,0,0), -1);
        Cv2.Circle(frame, (Point)corners[3], 12, new Scalar(0,255,255), -1);

        mask.Dispose();

        return true;
    }

    void DrawOutline(Mat frame)
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
            new Scalar(0,255,0),
            4);
    }

    Mat WarpBoard(Mat frame)
    {
        Mat warped = new Mat();

        Mat matrix = Cv2.GetPerspectiveTransform(
            corners,
            destinationCorners);

        Cv2.WarpPerspective(
            frame,
            warped,
            matrix,
            new Size(warpedWidth, warpedHeight));

        Debug.Log("Warping...");
        Debug.Log("Corners:");
        Debug.Log(corners[0]);
        Debug.Log(corners[1]);
        Debug.Log(corners[2]);
        Debug.Log(corners[3]);

        matrix.Dispose();

        return warped;
    }

    public void Continue()
    {
        SavedCorners = corners;

        SceneManager.LoadScene(nextSceneName);
    }

    void ShowFrame(Mat frame, Texture2D tex)
    {
        Mat rgba = new Mat();

        Cv2.CvtColor(frame, rgba, ColorConversionCodes.BGR2RGBA);

        if (!rgba.IsContinuous())
            rgba = rgba.Clone();

        byte[] data = new byte[rgba.Total() * rgba.ElemSize()];

        Marshal.Copy(rgba.Data, data, 0, data.Length);

        tex.LoadRawTextureData(data);
        tex.Apply();

        rgba.Dispose();
    }
}