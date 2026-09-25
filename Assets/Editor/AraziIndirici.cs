using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

// Unity menüsüne "Kurtuluş Savaşı > Arazi verisini indir" ekler.
// Açık kaynaklı yükseklik verisini (AWS Terrain Tiles / Terrarium, SRTM tabanlı) indirip
// proje klasöründeki HamVeri/terrarium klasörüne kaydeder. Bir kez çalıştırmak yeterli.
public static class AraziIndirici
{
    const int Z = 8;
    const int X0 = 145, X1 = 160;   // 24°D – 46°D
    const int Y0 = 93,  Y1 = 101;   // 43.5°K – 34.5°K

    [MenuItem("Kurtuluş Savaşı/Arazi verisini indir")]
    static void Indir()
    {
        string klasor = Path.GetFullPath(Path.Combine(Application.dataPath, "../HamVeri/terrarium"));
        Directory.CreateDirectory(klasor);

        int toplam = (X1 - X0 + 1) * (Y1 - Y0 + 1), sayac = 0, hata = 0;
        try
        {
            for (int x = X0; x <= X1; x++)
                for (int y = Y0; y <= Y1; y++)
                {
                    sayac++;
                    string dosya = Path.Combine(klasor, Z + "_" + x + "_" + y + ".png");
                    if (File.Exists(dosya)) continue;
                    if (EditorUtility.DisplayCancelableProgressBar("Arazi verisi indiriliyor",
                        sayac + " / " + toplam, sayac / (float)toplam)) return;

                    string adres = "https://s3.amazonaws.com/elevation-tiles-prod/terrarium/" + Z + "/" + x + "/" + y + ".png";
                    using (UnityWebRequest istek = UnityWebRequest.Get(adres))
                    {
                        var islem = istek.SendWebRequest();
                        while (!islem.isDone) { }
                        if (istek.result == UnityWebRequest.Result.Success)
                            File.WriteAllBytes(dosya, istek.downloadHandler.data);
                        else { hata++; Debug.LogError("İndirilemedi: " + adres + " → " + istek.error); }
                    }
                }
        }
        finally { EditorUtility.ClearProgressBar(); }

        Debug.Log("Arazi verisi hazır: " + klasor + "  (" + toplam + " parça, " + hata + " hata)");
        EditorUtility.DisplayDialog("Kurtuluş Savaşı", hata == 0
            ? "Arazi verisi indirildi. Claude'a haber verebilirsin."
            : hata + " parça indirilemedi. Tekrar dene.", "Tamam");
    }
}
