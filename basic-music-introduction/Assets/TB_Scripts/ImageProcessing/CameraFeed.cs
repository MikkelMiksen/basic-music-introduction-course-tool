using UnityEngine;

public class CameraFeed : MonoBehaviour
{
    public WebCamTexture webcam;

    public int width = 640;
    public int height = 480;

    public bool IsReady => webcam != null && webcam.width > 100;

    void Start()
    {
        webcam = new WebCamTexture("Tobiass S24 (Virtuelt Windows-kamera)");
        webcam.Play();

        foreach (var device in WebCamTexture.devices)
        {
            Debug.Log(device.name);
        }
    }

    public Color32[] GetPixels()
    {
        return webcam.GetPixels32();
    }

    public int GetWidth() => webcam.width;
    public int GetHeight() => webcam.height;
}