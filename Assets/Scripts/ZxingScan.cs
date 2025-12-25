using UnityEngine;
using UnityEngine.UI;
using ZXing;

public class ZxingScan : MonoBehaviour
{
    [Header("Image Source")]
   

    [Header("Scan Settings")]
     float scanInterval = 0.5f; // Scan every 0.5 seconds
    private float nextScanTime = 0f;
     bool autoScan = true; // Continuously scan
     bool downscaleImage = false; // Downscale for faster scanning
     int maxImageSize = 1024; // Max size if downscaling

    [Header("Result Display")]
    [SerializeField] TMPro.TextMeshProUGUI resultText; // Display scan result (optional)
    [SerializeField] TMPro.TextMeshProUGUI Log; // Display scan result (optional)

    private IBarcodeReader barcodeReader;
    private bool isScanning = false;
    private Texture2D texture2D;

    void Start()
    {
        InitializeBarcodeReader();

        if (autoScan)
        {
            // Wait 1 second for camera to fully initialize
            Invoke("StartScanning", 1f);
        }
    }

    void InitializeBarcodeReader()
    {
        // Initialize ZXing barcode reader - QR CODE ONLY
        barcodeReader = new BarcodeReader
        {
            AutoRotate = true,
            TryInverted = true,
            Options = new ZXing.Common.DecodingOptions
            {
                TryHarder = true, // Important: more thorough scanning
                PureBarcode = false, // Allow scanning with noise
                CharacterSet = "UTF-8",
                PossibleFormats = new[]
                {
                    BarcodeFormat.QR_CODE,      // QR CODE 
                     BarcodeFormat.EAN_13,        // Most common product barcode (13 digits)
                    BarcodeFormat.EAN_8,         // Shorter product barcode (8 digits)
                    BarcodeFormat.UPC_A,         // North American product barcode
                    BarcodeFormat.UPC_E,         // Compact version of UPC-A
                    
                    // Industrial/Logistics Barcodes
                    BarcodeFormat.CODE_128,      // Very versatile, high-density
                    BarcodeFormat.CODE_39,       // Alphanumeric, widely used
                    BarcodeFormat.CODE_93,       // Similar to Code 39, more compact
                    
                    // Other 2D Codes
                    BarcodeFormat.DATA_MATRIX,   // Small 2D codes
                    BarcodeFormat.PDF_417,       // 2D barcode for IDs, tickets
                    BarcodeFormat.AZTEC,         // 2D code used in transport tickets
                    
                    // Specialized Barcodes
                    BarcodeFormat.ITF,           // Interleaved 2 of 5 (shipping/logistics)
                    BarcodeFormat.CODABAR
                }
            }
        };

        Log.text=("[QRScanner] ZXing QR Code Reader initialized");
    }

    void Update()
    {
        if (!isScanning )
            return;

        // Scan at intervals for performance optimization
        if (Time.time >= nextScanTime)
        {
            nextScanTime = Time.time + scanInterval;
            ScanFromRawImage();
        }
    }
    Texture2D GPUToCPU()
    {
        if (Local.Tex == null)
        {
            Log.text = "NULL";
            return null;
        }

        // GPU -> CPU
        RenderTexture rt = new RenderTexture(
                                                Local.Tex.width,
                                                Local.Tex.height,
                                                0,
                                                RenderTextureFormat.ARGB32
                                            );

        Graphics.Blit(Local.Tex, rt);
        RenderTexture.active = rt;

        Texture2D cpuTex = new Texture2D(
            rt.width,
            rt.height,
            TextureFormat.ARGB32,
            false
        );

        cpuTex.ReadPixels(
            new Rect(0, 0, rt.width, rt.height),
            0, 0
        );
        cpuTex.Apply();

        RenderTexture.active = null;
        rt.Release();
        return cpuTex;
    }
    void ScanFromRawImage()
    {
        try
        {
            // Convert texture from GPU to CPU
            Texture texture = GPUToCPU();

            if (texture == null)
            {
                Log.text=("[QRScanner] RawImage has no texture!");
                return;
            }

            Log.text=($"[QRScanner] Texture info - Type: {texture.GetType().Name}, Size: {texture.width}x{texture.height}");

            // IMPORTANT: For WebCamTexture, we must use it directly
            Texture2D texture2D = null;

            if (texture is WebCamTexture)
            {
                // Special handling for WebCamTexture
                WebCamTexture webCamTexture = (WebCamTexture)texture;
                
                // Check if webcam is playing
                if (!webCamTexture.isPlaying)
                {
                    Log.text=("[QRScanner] WebCamTexture is not playing!");
                    return;
                }

                // Check if webcam has valid dimensions
                if (webCamTexture.width < 100)
                {
                    Log.text=("[QRScanner] WebCamTexture not ready yet (width < 100)");
                    return;
                }

                Log.text=($"[QRScanner] WebCamTexture - Size: {webCamTexture.width}x{webCamTexture.height}, FPS: {webCamTexture.requestedFPS}");

                // Create new Texture2D with same dimensions
                texture2D = new Texture2D(webCamTexture.width, webCamTexture.height, TextureFormat.RGB24, false);

                // Get pixels directly from WebCamTexture
                Color[] pixels = webCamTexture.GetPixels();
                texture2D.SetPixels(pixels);
                texture2D.Apply();

                Log.text=($"[QRScanner] Created Texture2D from WebCamTexture successfully");
            }
            else
            {
                // For other texture types
                texture2D = ConvertToTexture2D(texture);
            }

            if (texture2D == null)
            {
                Log.text=("[QRScanner] Failed to convert texture!");
                return;
            }

            Log.text=($"[QRScanner] Texture2D ready - Size: {texture2D.width}x{texture2D.height}, Format: {texture2D.format}");

            // Downscale if needed (faster scanning)
            if (downscaleImage && (texture2D.width > maxImageSize || texture2D.height > maxImageSize))
            {
                texture2D = DownscaleTexture(texture2D, maxImageSize);
                Log.text=($"[QRScanner] Downscaled to: {texture2D.width}x{texture2D.height}");
            }

            // Get pixels
            Color32[] pixels32 = null;
            try
            {
                pixels32 = texture2D.GetPixels32();
                Log.text=($"[QRScanner] Successfully got pixels array length: {pixels32.Length}");

                // Check if pixels has data
                if (pixels32.Length == 0)
                {
                    Log.text=("[QRScanner] Pixels array is empty!");
                    return;
                }

                // Check pixel samples
                CheckPixelSample(pixels32);
            }
            catch (System.Exception ex)
            {
                Log.text=($"[QRScanner] Error getting pixels: {ex.Message}");
                return;
            }

            // Decode barcode from Texture2D
            Log.text=("[QRScanner] Starting decode process...");
            var result = barcodeReader.Decode(pixels32, texture2D.width, texture2D.height);

            if (result != null)
            {
                Log.text=($"[QRScanner] SUCCESS! QR Code detected!");
                OnQRCodeScanned(result);
            }
            else
            {
                Log.text=("[QRScanner] First attempt failed. Trying flipped images...");

                // Try flipping the image
                result = TryDecodeWithFlip(pixels32, texture2D.width, texture2D.height);

                if (result != null)
                {
                    Log.text=($"[QRScanner] SUCCESS after flipping image!");
                    OnQRCodeScanned(result);
                }
                else
                {
                    Log.text=("[QRScanner] No QR Code detected in this frame");
                    Log.text=("[QRScanner] Tip: Move QR Code closer, ensure good lighting, QR should occupy 30-50% of frame");
                }
            }

            // Clean up temporary texture
            if (texture is WebCamTexture && texture2D != null)
            {
                Destroy(texture2D);
            }
        }
        catch (System.Exception ex)
        {
            Log.text=($"[QRScanner] Scan error: {ex.Message}\nStackTrace: {ex.StackTrace}");
        }
    }

    // Check pixel samples
    void CheckPixelSample(Color32[] pixels)
    {
        if (pixels.Length < 100) return;

        // Sample 10 random pixels
        int blackCount = 0;
        int whiteCount = 0;

        for (int i = 0; i < 10; i++)
        {
            int index = Random.Range(0, pixels.Length);
            Color32 p = pixels[index];
            float brightness = (p.r + p.g + p.b) / 3f;

            if (brightness < 50) blackCount++;
            if (brightness > 200) whiteCount++;
        }

        Log.text=($"[QRScanner] Pixel sample - Black: {blackCount}/10, White: {whiteCount}/10");

        if (blackCount == 10 || whiteCount == 10)
        {
            Log.text=("[QRScanner] Image appears to be all black or all white!");
        }
    }

    // Convert non-readable texture to readable
    Texture2D MakeTextureReadable(Texture2D source)
    {
        RenderTexture rt = RenderTexture.GetTemporary(source.width, source.height);
        Graphics.Blit(source, rt);

        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = rt;

        Texture2D readable = new Texture2D(source.width, source.height, TextureFormat.RGB24, false);
        readable.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        readable.Apply();

        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(rt);

        Log.text=("[QRScanner] Texture converted to readable");
        return readable;
    }

    // Try decoding with flipped images
    Result TryDecodeWithFlip(Color32[] pixels, int width, int height)
    {
        // Horizontal flip
        Color32[] flippedPixels = new Color32[pixels.Length];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                flippedPixels[y * width + x] = pixels[y * width + (width - 1 - x)];
            }
        }

        var result = barcodeReader.Decode(flippedPixels, width, height);
        if (result != null)
        {
            Log.text=("[QRScanner] Decode successful with horizontal flip");
            return result;
        }

        // Vertical flip
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                flippedPixels[y * width + x] = pixels[(height - 1 - y) * width + x];
            }
        }

        result = barcodeReader.Decode(flippedPixels, width, height);
        if (result != null)
        {
            Log.text=("[QRScanner] Decode successful with vertical flip");
            return result;
        }

        return null;
    }

    Texture2D ConvertToTexture2D(Texture texture)
    {
        // If texture is already Texture2D
        if (texture is Texture2D)
        {
            return (Texture2D)texture;
        }

        // If it's WebCamTexture
        if (texture is WebCamTexture)
        {
            WebCamTexture webCamTexture = (WebCamTexture)texture;
            Texture2D tex2D = new Texture2D(webCamTexture.width, webCamTexture.height);
            tex2D.SetPixels(webCamTexture.GetPixels());
            tex2D.Apply();
            return tex2D;
        }

        // If it's RenderTexture or other texture types
        RenderTexture renderTexture = texture as RenderTexture;
        if (renderTexture == null)
        {
            // Create RenderTexture from regular texture
            renderTexture = RenderTexture.GetTemporary(texture.width, texture.height);
            Graphics.Blit(texture, renderTexture);
        }

        // Read from RenderTexture
        RenderTexture currentRT = RenderTexture.active;
        RenderTexture.active = renderTexture;

        Texture2D tex = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
        tex.Apply();

        RenderTexture.active = currentRT;

        if (texture as RenderTexture == null)
        {
            RenderTexture.ReleaseTemporary(renderTexture);
        }

        return tex;
    }

    Texture2D DownscaleTexture(Texture2D source, int maxSize)
    {
        float scale = Mathf.Min((float)maxSize / source.width, (float)maxSize / source.height);
        if (scale >= 1f) return source;

        int newWidth = Mathf.RoundToInt(source.width * scale);
        int newHeight = Mathf.RoundToInt(source.height * scale);

        Texture2D result = new Texture2D(newWidth, newHeight, source.format, false);

        for (int y = 0; y < newHeight; y++)
        {
            for (int x = 0; x < newWidth; x++)
            {
                float u = (float)x / newWidth;
                float v = (float)y / newHeight;
                result.SetPixel(x, y, source.GetPixelBilinear(u, v));
            }
        }

        result.Apply();
        return result;
    }

    void OnQRCodeScanned(Result result)
    {
        Log.text=($"[QRScanner] QR Code detected: {result.Text}");
        Log.text=($"[QRScanner] Format: {result.BarcodeFormat}");

        // Display result
        if (resultText != null)
        {
            resultText.text = $"Result: {result.Text}\nType: {result.BarcodeFormat}";
        }
        else SaveCurrentImage();
        // Process scan result
        ProcessScanResult(result.Text, result.BarcodeFormat);

        // Stop scanning after detection (optional)
        // StopScanning();
    }

    void ProcessScanResult(string data, BarcodeFormat format)
    {
        // Process scanned data
        switch (format)
        {
            case BarcodeFormat.QR_CODE:
                Log.text=("[QRScanner] QR Code data: " + data);
                HandleQRCode(data);
                break;

            case BarcodeFormat.EAN_13:
            case BarcodeFormat.EAN_8:
                Log.text=("[QRScanner] Product barcode: " + data);
                HandleProductBarcode(data);
                break;

            default:
                Log.text=("[QRScanner] Other code type: " + data);
                break;
        }
    }

    void HandleQRCode(string data)
    {
        // Check if it's a URL
        if (data.StartsWith("http://") || data.StartsWith("https://"))
        {
            Log.text=("[QRScanner] This is a URL: " + data);
            // Application.OpenURL(data); // Open URL in browser
        }
        else
        {
            Log.text=("[QRScanner] QR text data: " + data);
            // Process regular text data
        }
    }

    void HandleProductBarcode(string data)
    {
        Log.text=("[QRScanner] Product code: " + data);
        // Look up product info from database
        // or call API to get product details
    }

    // Public methods to control scanning
    public void StartScanning()
    {
        isScanning = true;
        Log.text=("[QRScanner] Scanning started...");
    }

    public void StopScanning()
    {
        isScanning = false;
        Log.text=("[QRScanner] Scanning stopped.");
    }

    public void ScanOnce()
    {

        ScanFromRawImage();
    }

    void OnDestroy()
    {
        StopScanning();

        if (texture2D != null)
        {
            Destroy(texture2D);
        }
    }

    void OnApplicationPause(bool pause)
    {
        if (pause)
            StopScanning();
        else if (autoScan)
            StartScanning();
    }

    // Debug: Save image for inspection
    public void SaveCurrentImage()
    {
       

        Texture2D tex = GPUToCPU();
        if (tex != null)
        {
            byte[] bytes = tex.EncodeToPNG();
            string path = Application.persistentDataPath + "/debug_camera.png";
            System.IO.File.WriteAllBytes(path, bytes);
            Log.text=($"[QRScanner] Image saved at: {path}");
        }
    }

    // Debug: Check image brightness
    void CheckImageBrightness(Texture2D texture)
    {
        Color32[] pixels = texture.GetPixels32();
        float totalBrightness = 0;

        // Sample 1000 random pixels
        int sampleCount = Mathf.Min(1000, pixels.Length);
        for (int i = 0; i < sampleCount; i++)
        {
            int index = Random.Range(0, pixels.Length);
            Color32 pixel = pixels[index];
            totalBrightness += (pixel.r + pixel.g + pixel.b) / 3f;
        }

        float avgBrightness = totalBrightness / sampleCount;
        Log.text=($"[QRScanner] Average brightness: {avgBrightness}/255");

        if (avgBrightness < 30)
        {
            Log.text=("[QRScanner] Image too dark! Need more lighting.");
        }
        else if (avgBrightness > 225)
        {
            Log.text=("[QRScanner] Image too bright! Reduce lighting or exposure.");
        }
    }
}
