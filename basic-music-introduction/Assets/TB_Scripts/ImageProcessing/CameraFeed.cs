using Unity.VisualScripting;
using UnityEngine;

public class CameraFeed : MonoBehaviour
{
    public WebCamTexture webcam;

    public int width = 640;
    public int height = 480;

    public bool IsReady => webcam != null && webcam.width > 100;

    void Start()
    {
        // Log all devices for debugging
        WebCamDevice[] devices = WebCamTexture.devices;
        Debug.Log($"[DEBUG_LOG] Found {devices.Length} cameras.");
        for (int i = 0; i < devices.Length; i++)
        {
            Debug.Log($"[DEBUG_LOG] Camera {i}: {devices[i].name}");
        }

        string targetDevice = "Tobiass S24 (Virtuelt Windows-kamera)";
        bool deviceFound = false;

        foreach (var device in devices)
        {
            if (device.name == targetDevice)
            {
                deviceFound = true;
                break;
            }
        }

        if (!deviceFound && devices.Length > 0)
        {
            Debug.LogWarning($"[DEBUG_LOG] Target camera '{targetDevice}' not found. Falling back to first available: {devices[0].name}");
            targetDevice = devices[0].name;
        }
        else if (devices.Length == 0)
        {
            Debug.LogError("[DEBUG_LOG] No cameras found at all! Check connections and permissions.");
            return;
        }

        webcam = new WebCamTexture(targetDevice, width, height);
        
        try 
        {
            webcam.Play();
            Debug.Log($"[DEBUG_LOG] Started webcam: {targetDevice}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[DEBUG_LOG] Failed to play webcam {targetDevice}: {e.Message}");
        }
    }

    public Color32[] GetPixels()
    {
        return webcam.GetPixels32();
    }

    public int GetWidth() => webcam.width;
    public int GetHeight() => webcam.height;
}