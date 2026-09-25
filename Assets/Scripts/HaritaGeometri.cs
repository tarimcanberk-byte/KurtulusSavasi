using System;

// Resources/HaritaGeometri.json dosyasının yapısı.
// Bu dosya gerçek coğrafi veriden (Natural Earth) üretildi: her sancağın bölge şekli,
// komşu ülkelerin kara parçaları ve ortak sınırı olan sancak çiftleri.
// Koordinatlar doğrudan Unity birimidir (x = doğu-batı, z = kuzey-güney).
[Serializable]
public class GeoParca
{
    public float[] v;       // köşeler: x0, z0, x1, z1, ...
    public int[] t;         // üçgenler: köşe indeksleri, üçer üçer
    public float[] sinir;   // dış sınır çizgisi: x0, z0, x1, z1, ...
}

[Serializable]
public class GeoSancak
{
    public string ad;
    public float etiketX, etiketZ;   // isim etiketinin konumu
    public GeoParca[] parcalar;      // ana kara + varsa adalar
}

[Serializable]
public class GeoKomsuluk { public string a, b; }

[Serializable]
public class GeoHarita
{
    public GeoSancak[] sancaklar;
    public GeoParca[] yabanci;          // Yunanistan, Bulgaristan, Suriye, İran... (oynanamaz kara)
    public GeoKomsuluk[] komsuluklar;
}
