using Serenegiant.UVC;
using System;
using UnityEngine;
using UnityEngine.UI;
using ZXing;

public class Scan : MonoBehaviour
{
    [SerializeField]
    RawImage rawImage;
    [SerializeField]
    TMPro.TextMeshProUGUI label;
    [SerializeField]
    TMPro.TextMeshProUGUI Log;
    
    float scanInterval = 0.5f;

    private IBarcodeReader reader;
    private float timer = 0;

    
   

    void Start()
    {
       
        reader = new BarcodeReader
        {
            AutoRotate = true,
            TryInverted = true,
            Options = new ZXing.Common.DecodingOptions
            {
                TryHarder = true
            }
        };
    }

  

   

    void Update()
    {
        timer += Time.deltaTime;
        if (timer >= scanInterval)
        {
            timer = 0f;
            ScanQRcode();
        }
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
    void ScanQRcode()
    {
        if (Local.Tex == null)
        {
            Log.text = "NULL";
            return;
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

        // ZXing
        var pixels = cpuTex.GetPixels32();
        var result = reader.Decode(pixels, cpuTex.width, cpuTex.height);

        if (result != null)
            Log.text = result.Text;
        else
            SaveImg(cpuTex);
    }

    void SaveImg(Texture2D t2d)
    {
        try
        {
            string path = Application.persistentDataPath + "/debug_camera1.png";

            byte[] png = t2d.EncodeToPNG();

            System.IO.File.WriteAllBytes(path,png);

            Log.text = ($"[QRScanner] Image saved at: {path}");
        }
        catch (System.Exception ex)
        {

            Log.text = ex.Message;
        }
      
    }

}
