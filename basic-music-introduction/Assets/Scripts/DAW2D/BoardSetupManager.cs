using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using OpenCvSharp;
using System.Collections;
using System.Runtime.InteropServices;
using System.Linq;
using System.Collections.Generic;

public class BoardSetupManager : MonoBehaviour
{
    [Header("Camera")]
    public CameraFeed cameraSource;

    [Header("Display (Scene 1 debug)")]
    public Renderer cameraQuad;
    public Renderer warpedQuad;

    [Header("Scene")]
    public string nextSceneName = "UI_Toolkit";
    public Button nextSceneButton;

    [Header("Warp Settings")]
    public int warpedWidth = 640;
    public int warpedHeight = 440;

    [Header("Blue Detection")]
    public Scalar blueLower = new Scalar(100, 120, 80);
    public Scalar blueUpper = new Scalar(130, 255, 255);
    public float minRectSize = 10f;

    private Texture2D cameraTexture;
    private Texture2D warpedTexture;

    private Point2f[] corners = new Point2f[4];

    private readonly Point2f[] destinationCorners =
    {
        new Point2f(0, 0),
        new Point2f(640, 0),
        new Point2f(640, 440),
        new Point2f(0, 440)
    };

    public static Point2f[] LockedCorners;

    IEnumerator Start()
    {
        while (!cameraSource.IsReady)
            yield return null;

        nextSceneButton.onClick.AddListener(GoToNextScene);

        int w = cameraSource.GetWidth();
        int h = cameraSource.GetHeight();

        cameraTexture = new Texture2D(w, h, TextureFormat.RGBA32, false);
        warpedTexture = new Texture2D(warpedWidth, warpedHeight, TextureFormat.RGBA32, false);

        if (cameraQuad != null)
            cameraQuad.material.mainTexture = cameraTexture;

        if (warpedQuad != null)
            warpedQuad.material.mainTexture = warpedTexture;
    }

    void Update()
    {
        if (!cameraSource.IsReady)
            return;

        Process();
    }

    void Process()
    {
        Color32[] pixels = cameraSource.GetPixels();
        int w = cameraSource.GetWidth();
        int h = cameraSource.GetHeight();

        Mat frame = new Mat(h, w, MatType.CV_8UC4, pixels);

        Cv2.CvtColor(frame, frame, ColorConversionCodes.RGBA2BGR);
        Cv2.Flip(frame, frame, FlipMode.X);

        Mat hsv = new Mat();
        Cv2.CvtColor(frame, hsv, ColorConversionCodes.BGR2HSV);

        bool found = DetectCorners(frame, hsv);

        if (found)
        {
            Mat warp = Warp(frame);

            ShowFrame(warp, warpedTexture);
            warp.Dispose();

            // STORE GLOBAL RESULT FOR SCENE 2
            LockedCorners = corners;
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

        var points = new System.Collections.Generic.List<Point>();

        foreach (var c in contours)
        {
            var r = Cv2.BoundingRect(c);

            if (r.Width < minRectSize || r.Height < minRectSize)
                continue;

            // CENTER of blue corner brick
            Point center = new Point(
                r.X + r.Width / 2,
                r.Y + r.Height / 2);

            points.Add(center);

            // Debug rectangle
            Cv2.Rectangle(
                frame,
                r,
                new Scalar(255, 0, 0),
                2);

            // Debug center point
            Cv2.Circle(
                frame,
                center,
                6,
                new Scalar(0, 255, 255),
                -1);
        }

        if (points.Count < 4)
        {
            mask.Dispose();
            return false;
        }

        // ORIGINAL STABLE SORTING
        corners[0] = points.OrderBy(p => p.X + p.Y).First();              // TL
        corners[2] = points.OrderByDescending(p => p.X + p.Y).First();    // BR
        corners[1] = points.OrderBy(p => p.X - p.Y).First();              // TR
        corners[3] = points.OrderByDescending(p => p.X - p.Y).First();    // BL

        // ============================================
        // MOVE CORNERS INWARD
        // ============================================

        float inwardOffset = 20f;

        Vector2 tl = ToVec2(corners[0]);
        Vector2 tr = ToVec2(corners[1]);
        Vector2 br = ToVec2(corners[2]);
        Vector2 bl = ToVec2(corners[3]);

        corners[0] = ToPoint2f(
            tl +
            (tr - tl).normalized * inwardOffset +
            (bl - tl).normalized * inwardOffset);

        corners[1] = ToPoint2f(
            tr +
            (tl - tr).normalized * inwardOffset +
            (br - tr).normalized * inwardOffset);

        corners[2] = ToPoint2f(
            br +
            (tr - br).normalized * inwardOffset +
            (bl - br).normalized * inwardOffset);

        corners[3] = ToPoint2f(
            bl +
            (tl - bl).normalized * inwardOffset +
            (br - bl).normalized * inwardOffset);

        // ============================================
        // DEBUG DRAW FINAL INNER CORNERS
        // ============================================

        foreach (var p in corners)
        {
            Cv2.Circle(
                frame,
                (Point)p,
                8,
                new Scalar(0, 255, 0),
                -1);
        }

        mask.Dispose();

        return true;
    }

    Mat Warp(Mat frame)
    {
        Mat warped = new Mat();
        Mat transform = Cv2.GetPerspectiveTransform(corners, destinationCorners);

        Cv2.WarpPerspective(frame, warped, transform, new Size(warpedWidth, warpedHeight));

        return warped;
    }

    void ShowFrame(Mat frame, Texture2D tex)
    {
        if (tex == null || frame == null) return;

        Mat rgba = new Mat();
        Cv2.CvtColor(frame, rgba, ColorConversionCodes.BGR2RGBA);

        byte[] data = new byte[rgba.Total() * rgba.ElemSize()];
        Marshal.Copy(rgba.Data, data, 0, data.Length);

        tex.LoadRawTextureData(data);
        tex.Apply();

        rgba.Dispose();
    }

    Vector2 ToVec2(Point2f p)
    {
        return new Vector2(p.X, p.Y);
    }

    Point2f ToPoint2f(Vector2 v)
    {
        return new Point2f(v.x, v.y);
    }

    public void GoToNextScene()
    {
        if (LockedCorners != null)
            SceneManager.LoadScene(nextSceneName);
    }
}