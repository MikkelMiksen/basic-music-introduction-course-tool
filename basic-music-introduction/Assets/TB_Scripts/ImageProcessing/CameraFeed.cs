using UnityEngine;

public class CameraFeed : MonoBehaviour
{
    public static CameraFeed Instance;

    public WebCamTexture webcam;

    public int width = 640;
    public int height = 480;

    public bool IsReady => webcam != null && webcam.width > 16;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        if (webcam != null)
            return; // prevent double init after scene loads

        InitCamera();
    }

    void InitCamera()
    {
        WebCamDevice[] devices = WebCamTexture.devices;

        Debug.Log($"[CameraFeed] Found {devices.Length} cameras.");

        if (devices.Length == 0)
        {
            Debug.LogError("[CameraFeed] No cameras found!");
            return;
        }

        string targetDevice = "Tobiass S24 (Virtuelt Windows-kamera)";
        bool found = false;

        foreach (var d in devices)
        {
            if (d.name == targetDevice)
            {
                found = true;
                break;
            }
        }

        if (!found)
        {
            Debug.LogWarning($"[CameraFeed] Target not found, using fallback: {devices[0].name}");
            targetDevice = devices[0].name;
        }

        webcam = new WebCamTexture(targetDevice, width, height);
        webcam.Play();

        Debug.Log($"[CameraFeed] Started webcam: {targetDevice}");
    }

    void OnDestroy()
    {
        // optional cleanup (only if app quits or manually destroyed)
        if (webcam != null)
        {
            webcam.Stop();
        }
    }

    public Color32[] GetPixels()
    {
        if (!IsReady) return null;
        return webcam.GetPixels32();
    }

    public int GetWidth() => webcam != null ? webcam.width : 0;
    public int GetHeight() => webcam != null ? webcam.height : 0;
}