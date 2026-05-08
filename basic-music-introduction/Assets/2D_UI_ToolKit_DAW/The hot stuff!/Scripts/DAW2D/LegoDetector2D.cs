using UnityEngine;
using OpenCvSharp;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using DAW2D;

public class LegoDetector2D : MonoBehaviour
{
    [Header("Camera")]
    [SerializeField] private CameraFeed cameraSource;

    [Header("Grid")]
    public int gridCols = 64;
    public int gridRows = 44;

    [Header("Colors")]
    public Scalar yellowLower, yellowUpper;
    public Scalar whiteLower, whiteUpper;
    public Scalar pinkLower, pinkUpper;

    [Header("Warp")]
    public int warpedWidth = 640;
    public int warpedHeight = 440;

    private List<NoteData> detectedNotes = new();

    void Awake()
    {
        cameraSource = CameraFeed.Instance;
    }

    private void Update()
    {
        if (!cameraSource.IsReady)
            return;

        if (BoardSetupManager.LockedCorners == null)
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

        Mat warped = Warp(frame);

        detectedNotes.Clear();

        DetectColor(warped, yellowLower, yellowUpper, 4);
        DetectColor(warped, whiteLower, whiteUpper, 2);
        DetectColor(warped, pinkLower, pinkUpper, 1);

        warped.Dispose();
        frame.Dispose();
    }

    Mat Warp(Mat frame)
    {
        Mat warped = new Mat();

        Cv2.WarpPerspective(
            frame,
            warped,
            BoardSetupManager.LockedWarpMatrix,
            new Size(warpedWidth, warpedHeight));

        return warped;
    }

    void DetectColor(Mat warped, Scalar lower, Scalar upper, int duration)
    {
        Mat hsv = new Mat();
        Cv2.CvtColor(warped, hsv, ColorConversionCodes.BGR2HSV);

        Mat mask = new Mat();
        Cv2.InRange(hsv, lower, upper, mask);

        Cv2.FindContours(mask, out Point[][] contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);

        foreach (var c in contours)
        {
            var r = Cv2.BoundingRect(c);
            if (r.Width < 10 || r.Height < 10)
                continue;

            float cx = r.X + r.Width * 0.5f;
            float cy = r.Y + r.Height * 0.5f;

            int gx = Mathf.Clamp(Mathf.FloorToInt((cx / warpedWidth) * gridCols), 0, gridCols - 1);
            int gy = Mathf.Clamp(Mathf.FloorToInt((cy / warpedHeight) * gridRows), 0, gridRows - 1);

            gy = (gridRows - 1) - gy;

            detectedNotes.Add(new NoteData
            {
                tick = gx,
                pitch = gy,
                duration = duration,
                velocity = 0.8f
            });
        }

        hsv.Dispose();
        mask.Dispose();
    }
}