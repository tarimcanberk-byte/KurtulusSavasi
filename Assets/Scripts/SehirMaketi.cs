using UnityEngine;

// Sancak merkezlerine küçük 3 boyutlu şehir maketleri: beyaz badanalı evler, kiremit çatılar,
// kurşun kaplı kubbeli bir cami ve minare. Büyük şehirler daha kalabalık.
public static class SehirMaketi
{
    static readonly Color Badana  = new Color(0.90f, 0.87f, 0.80f);
    static readonly Color Kiremit = new Color(0.62f, 0.28f, 0.18f);
    static readonly Color Kursun  = new Color(0.55f, 0.58f, 0.62f);

    // 0: kasaba, 1: şehir, 2: büyük şehir, 3: başkent/liman
    public static int Buyukluk(string ad, int para)
    {
        switch (ad)
        {
            case "İstanbul": case "İzmir": return 3;
            case "Ankara": case "Bursa": case "Konya": case "Erzurum": case "Adana": case "Trabzon": return 2;
        }
        return para >= 15 ? 1 : 0;
    }

    public const float Boyut = 2.2f;   // maketlerin genel büyüklüğü

    public static void Kur(Transform ebeveyn, Vector3 merkez, string ad, int buyukluk)
    {
        GameObject sehir = new GameObject("Sehir");
        sehir.transform.SetParent(ebeveyn, true);
        Vector3 orta = merkez + new Vector3(0f, 0f, -0.3f);   // ordular merkezin kuzeyinde durur, şehir güneyinde

        // Adından türeyen sabit rastgelelik: her oyunda aynı şehir aynı görünür
        int tohum = 17;
        foreach (char c in ad) tohum = tohum * 31 + c;
        System.Random r = new System.Random(tohum);
        float S() { return (float)r.NextDouble(); }

        int evSayisi = 5 + buyukluk * 4;
        float yaricap = (0.12f + buyukluk * 0.05f) * Boyut;
        for (int i = 0; i < evSayisi; i++)
        {
            float aci = S() * Mathf.PI * 2f, uzaklik = Mathf.Sqrt(S()) * yaricap;
            Vector3 yer = orta + new Vector3(Mathf.Cos(aci), 0f, Mathf.Sin(aci)) * uzaklik;
            float g = (0.035f + S() * 0.025f) * Boyut, d = (0.035f + S() * 0.025f) * Boyut, h = (0.03f + S() * 0.02f) * Boyut;
            float don = S() * 90f;
            Parca(sehir, PrimitiveType.Cube, Yere(yer, h * 0.5f), new Vector3(g, h, d), don, Color.Lerp(Badana, new Color(0.8f, 0.72f, 0.6f), S() * 0.5f));
            Parca(sehir, PrimitiveType.Cube, Yere(yer, h + 0.006f * Boyut), new Vector3(g * 1.1f, 0.012f * Boyut, d * 1.1f), don, Kiremit);
        }

        // Cami: gövde + kubbe + minare(ler)
        int camiSayisi = buyukluk >= 2 ? 2 : 1;
        for (int c = 0; c < camiSayisi; c++)
        {
            Vector3 yer = orta + new Vector3((c == 0 ? 0f : 0.09f), 0f, (c == 0 ? 0f : 0.07f)) * Boyut;
            Parca(sehir, PrimitiveType.Cube,     Yere(yer, 0.025f * Boyut), new Vector3(0.07f, 0.05f, 0.07f) * Boyut, 0f, Badana);
            Parca(sehir, PrimitiveType.Sphere,   Yere(yer, 0.05f * Boyut),  new Vector3(0.065f, 0.05f, 0.065f) * Boyut, 0f, Kursun);
            Vector3 minare = yer + new Vector3(0.045f, 0f, 0.035f) * Boyut;
            Parca(sehir, PrimitiveType.Cylinder, Yere(minare, 0.07f * Boyut),  new Vector3(0.012f, 0.07f, 0.012f) * Boyut, 0f, Badana);
            Parca(sehir, PrimitiveType.Sphere,   Yere(minare, 0.145f * Boyut), new Vector3(0.016f, 0.02f, 0.016f) * Boyut, 0f, Kursun);
        }
    }

    static readonly System.Collections.Generic.Dictionary<Color, Material> malzemeler = new System.Collections.Generic.Dictionary<Color, Material>();

    static Vector3 Yere(Vector3 p, float ek)
    {
        return Arazi.Yuklu ? Arazi.Uzerinde(p, ek) : p + new Vector3(0f, ek, 0f);
    }

    static void Parca(GameObject ebeveyn, PrimitiveType tur, Vector3 yer, Vector3 boyut, float don, Color renk)
    {
        GameObject p = GameObject.CreatePrimitive(tur);
        Object.Destroy(p.GetComponent<Collider>());
        p.transform.SetParent(ebeveyn.transform, false);
        p.transform.position = yer;
        p.transform.rotation = Quaternion.Euler(0f, don, 0f);
        p.transform.localScale = boyut;
        Renderer rd = p.GetComponent<Renderer>();
        // Aynı renkteki parçalar aynı malzemeyi paylaşsın (yüzlerce kopya oluşmasın)
        if (!malzemeler.TryGetValue(renk, out Material m))
        {
            m = new Material(rd.sharedMaterial);
            m.color = renk;
            malzemeler[renk] = m;
        }
        rd.sharedMaterial = m;
        rd.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
    }
}
