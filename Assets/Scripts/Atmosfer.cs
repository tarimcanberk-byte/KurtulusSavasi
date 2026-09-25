using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Haritanın havası: uzaklarda hafif pus, sinematik renk düzeltmesi, yumuşak parlama ve kenar kararması.
public static class Atmosfer
{
    public static void Kur()
    {
        // Pus: uzaktaki dağlar hafifçe maviye çalsın
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogDensity = 0.006f;
        RenderSettings.fogColor = new Color(0.70f, 0.76f, 0.82f);

        Camera kamera = Camera.main;
        if (kamera != null)
        {
            var veri = kamera.GetUniversalAdditionalCameraData();
            veri.renderPostProcessing = true;
            veri.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        }

        // Görüntü efektleri (sahnede zaten olan Global Volume'un üstüne, daha yüksek öncelikle)
        GameObject go = new GameObject("Atmosfer");
        Volume hacim = go.AddComponent<Volume>();
        hacim.isGlobal = true;
        hacim.priority = 10f;
        VolumeProfile profil = ScriptableObject.CreateInstance<VolumeProfile>();

        Tonemapping ton = profil.Add<Tonemapping>(true);
        ton.mode.Override(TonemappingMode.ACES);

        ColorAdjustments renk = profil.Add<ColorAdjustments>(true);
        renk.postExposure.Override(0.2f);
        renk.contrast.Override(8f);
        renk.saturation.Override(-5f);

        WhiteBalance beyaz = profil.Add<WhiteBalance>(true);
        beyaz.temperature.Override(2f);   // çok hafif sıcak

        Bloom parlama = profil.Add<Bloom>(true);
        parlama.intensity.Override(0.35f);
        parlama.threshold.Override(1.1f);

        Vignette kenar = profil.Add<Vignette>(true);
        kenar.intensity.Override(0.22f);
        kenar.smoothness.Override(0.5f);

        hacim.profile = profil;
    }
}
