using System.Collections.Generic;
using UnityEngine;

// Savaş ekranının arazisi: muharebenin yapıldığı sancağın gerçek yükseklik verisinden,
// haritadan uzakta (Merkez) kurulan 84 x 84 birimlik bir meydan.
public static class MuharebeAlani
{
    public static readonly Vector3 Merkez = new Vector3(3000f, 0f, 3000f);
    public const float Yari = 42f;            // meydanın yarı genişliği
    const int N = 97;                         // ızgara nokta sayısı (her kenarda)
    const float HaritaYaricapi = 0.40f;       // haritada kaç birimlik alan örneklenir (~22 km)

    static float[] yukseklik;                 // N x N (yerel)
    static float Adim { get { return 2f * Yari / (N - 1); } }

    // Meydanı kurar ve kök nesnesini döndürür
    public static GameObject Kur(Sancak sancak, Transform ebeveyn)
    {
        GameObject kok = new GameObject("MuharebeAlani");
        kok.transform.SetParent(ebeveyn, false);
        kok.transform.position = Merkez;

        Vector3 c = KaraMerkezi(sancak.transform.position);
        YukseklikleriHesapla(c);

        // Arazi ağı
        Vector3[] v = new Vector3[N * N];
        Vector2[] uv = new Vector2[N * N];
        for (int j = 0; j < N; j++)
            for (int i = 0; i < N; i++)
            {
                v[j * N + i] = new Vector3(-Yari + i * Adim, yukseklik[j * N + i], -Yari + j * Adim);
                uv[j * N + i] = new Vector2(i / (float)(N - 1), j / (float)(N - 1));
            }
        int[] t = new int[(N - 1) * (N - 1) * 6];
        int k = 0;
        for (int j = 0; j < N - 1; j++)
            for (int i = 0; i < N - 1; i++)
            {
                int a = j * N + i, b = a + 1, cc = a + N, d = cc + 1;   // cc: bir kuzey sıra
                t[k++] = a; t[k++] = cc; t[k++] = b;
                t[k++] = b; t[k++] = cc; t[k++] = d;
            }
        Mesh m = new Mesh();
        m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        m.vertices = v; m.uv = uv; m.triangles = t;
        m.RecalculateNormals();
        m.RecalculateBounds();

        GameObject zemin = new GameObject("Zemin");
        zemin.transform.SetParent(kok.transform, false);
        zemin.AddComponent<MeshFilter>().sharedMesh = m;
        Material zm = new Material(LitShader());
        zm.SetTexture("_BaseMap", DokuUret(c));
        zm.SetFloat("_Smoothness", 0.05f);
        zemin.AddComponent<MeshRenderer>().material = zm;
        zemin.AddComponent<MeshCollider>().sharedMesh = m;

        // Meydanın dışında boşluk görünmesin: geniş, alçak bir etek
        float minY = float.MaxValue;
        foreach (float y in yukseklik) minY = Mathf.Min(minY, y);
        GameObject etek = GameObject.CreatePrimitive(PrimitiveType.Quad);
        etek.name = "Etek";
        Object.Destroy(etek.GetComponent<Collider>());
        etek.transform.SetParent(kok.transform, false);
        etek.transform.localPosition = new Vector3(0f, minY - 0.4f, 0f);
        etek.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        etek.transform.localScale = new Vector3(900f, 900f, 1f);
        Material em = new Material(LitShader());
        em.SetColor("_BaseColor", OrtalamaRenk(c) * 0.8f);
        em.SetFloat("_Smoothness", 0f);
        etek.GetComponent<Renderer>().material = em;

        AgaclariDik(kok.transform);
        return kok;
    }

    // Dünya konumundaki meydan yüksekliği (askerleri yere oturtmak için)
    public static float Yukseklik(float x, float z)
    {
        if (yukseklik == null) return Merkez.y;
        float fi = (x - Merkez.x + Yari) / Adim, fj = (z - Merkez.z + Yari) / Adim;
        fi = Mathf.Clamp(fi, 0f, N - 1.001f); fj = Mathf.Clamp(fj, 0f, N - 1.001f);
        int i = (int)fi, j = (int)fj;
        float tx = fi - i, tz = fj - j;
        float a = Mathf.Lerp(yukseklik[j * N + i], yukseklik[j * N + i + 1], tx);
        float b = Mathf.Lerp(yukseklik[(j + 1) * N + i], yukseklik[(j + 1) * N + i + 1], tx);
        return Merkez.y + Mathf.Lerp(a, b, tz);
    }

    public static Vector3 Uzerinde(Vector3 p, float ek = 0f)
    {
        return new Vector3(p.x, Yukseklik(p.x, p.z) + ek, p.z);
    }

    // Meydanın içinde mi (yerel sınır payıyla)
    public static bool Icinde(Vector3 p, float pay = 0f)
    {
        return Mathf.Abs(p.x - Merkez.x) < Yari - pay && Mathf.Abs(p.z - Merkez.z) < Yari - pay;
    }

    // ---------- İç işler ----------

    static bool Kara(float x, float z) { return Arazi.Yuklu && Arazi.Yukseklik(x, z) > -0.001f; }

    // Kıyı şehirlerinde meydan denize taşmasın: şehrin çevresinde en çok kara içeren noktayı seç
    static Vector3 KaraMerkezi(Vector3 p)
    {
        if (!Arazi.Yuklu) return p;
        Vector3 enIyi = p; int enIyiSayi = -1;
        for (int halka = 0; halka <= 2; halka++)
        {
            int yonSayisi = halka == 0 ? 1 : 8;
            for (int y = 0; y < yonSayisi; y++)
            {
                float aci = y * Mathf.PI / 4f;
                Vector3 aday = p + new Vector3(Mathf.Cos(aci), 0f, Mathf.Sin(aci)) * (halka * 0.3f);
                int sayi = 0;
                for (int a = -4; a <= 4; a++)
                    for (int b = -4; b <= 4; b++)
                        if (Kara(aday.x + a * HaritaYaricapi / 4f, aday.z + b * HaritaYaricapi / 4f)) sayi++;
                if (sayi > enIyiSayi + 2) { enIyiSayi = sayi; enIyi = aday; }
            }
            if (enIyiSayi >= 79) break;   // neredeyse tamamı kara: yeterli
        }
        return enIyi;
    }

    static void YukseklikleriHesapla(Vector3 c)
    {
        yukseklik = new float[N * N];
        float[] metre = new float[N * N];
        float mMin = float.MaxValue, mMax = float.MinValue;
        for (int j = 0; j < N; j++)
            for (int i = 0; i < N; i++)
            {
                float hx = c.x + (i / (float)(N - 1) * 2f - 1f) * HaritaYaricapi;
                float hz = c.z + (j / (float)(N - 1) * 2f - 1f) * HaritaYaricapi;
                float m = Arazi.Yuklu ? Mathf.Max(0f, Arazi.Yukseklik(hx, hz)) / Arazi.MetreBasinaBirim : 0f;
                metre[j * N + i] = m;
                mMin = Mathf.Min(mMin, m); mMax = Mathf.Max(mMax, m);
            }
        // Gerçek yükseklik farkı + küçük tepecikler; çok dağlıksa sıkıştır
        float olcek = (mMax - mMin) > 900f ? 13.5f / (mMax - mMin) : 0.015f;
        float ox = Random.Range(0f, 100f), oz = Random.Range(0f, 100f);
        for (int j = 0; j < N; j++)
            for (int i = 0; i < N; i++)
            {
                float px = i * 0.06f + ox, pz = j * 0.06f + oz;
                float tepe = (Mathf.PerlinNoise(px, pz) - 0.5f) * 2.5f
                           + (Mathf.PerlinNoise(px * 3f, pz * 3f) - 0.5f) * 0.5f;
                yukseklik[j * N + i] = (metre[j * N + i] - mMin) * olcek + tepe;
            }
    }

    // Haritadaki renkleri (bulanık) temel alıp üzerine çimen, toprak ve tarla lekeleri işler
    static Texture2D DokuUret(Vector3 c)
    {
        const int B = 512;
        Texture2D harita = Arazi.RenkDokusu();
        Texture2D d = new Texture2D(B, B, TextureFormat.RGBA32, true);
        Color[] p = new Color[B * B];
        Color cimen = new Color(0.36f, 0.40f, 0.20f), kuru = new Color(0.60f, 0.54f, 0.34f),
              toprak = new Color(0.45f, 0.36f, 0.24f), tarla = new Color(0.66f, 0.58f, 0.33f);
        float ox = Random.Range(0f, 50f);
        for (int y = 0; y < B; y++)
            for (int x = 0; x < B; x++)
            {
                float u = x / (float)(B - 1), v = y / (float)(B - 1);
                Color taban = HaritaRengi(harita, c.x + (u * 2f - 1f) * HaritaYaricapi, c.z + (v * 2f - 1f) * HaritaYaricapi);
                float n1 = Mathf.PerlinNoise(u * 6f + ox, v * 6f);
                float n2 = Mathf.PerlinNoise(u * 25f + ox, v * 25f);
                float n3 = Mathf.PerlinNoise(u * 90f, v * 90f + ox);
                Color yerel = Color.Lerp(cimen, kuru, n1);
                yerel = Color.Lerp(yerel, toprak, Mathf.Clamp01((n2 - 0.62f) * 4f));
                // Tarlalar: dikdörtgen parçalar
                float tx = Mathf.Floor(u * 9f + ox), tz = Mathf.Floor(v * 7f);
                if (Mathf.PerlinNoise(tx * 0.71f, tz * 0.53f) > 0.66f) yerel = Color.Lerp(yerel, tarla, 0.55f);
                Color son = Color.Lerp(taban, yerel, 0.55f) * (0.9f + n3 * 0.2f);
                // Kıyıda su: haritadaki su rengi korunur
                if (!Kara(c.x + (u * 2f - 1f) * HaritaYaricapi, c.z + (v * 2f - 1f) * HaritaYaricapi))
                    son = new Color(0.13f, 0.25f, 0.33f);
                son.a = 1f;
                p[y * B + x] = son;
            }
        d.SetPixels(p);
        d.Apply(true);
        d.wrapMode = TextureWrapMode.Clamp;
        d.anisoLevel = 8;
        return d;
    }

    static Color HaritaRengi(Texture2D harita, float x, float z)
    {
        if (harita == null || !Arazi.Yuklu) return new Color(0.5f, 0.48f, 0.32f);
        float u = (x - Arazi.XMin) / (Arazi.XMax - Arazi.XMin), v = (z - Arazi.ZMin) / (Arazi.ZMax - Arazi.ZMin);
        return harita.GetPixelBilinear(u, v);
    }

    static Color OrtalamaRenk(Vector3 c)
    {
        Texture2D harita = Arazi.RenkDokusu();
        Color t = Color.black;
        for (int a = -2; a <= 2; a++)
            for (int b = -2; b <= 2; b++)
                t += HaritaRengi(harita, c.x + a * 0.15f, c.z + b * 0.15f);
        t /= 25f; t.a = 1f;
        return Color.Lerp(t, new Color(0.45f, 0.44f, 0.27f), 0.4f);
    }

    static void AgaclariDik(Transform kok)
    {
        Material govde = new Material(LitShader()); govde.SetColor("_BaseColor", new Color(0.28f, 0.20f, 0.13f));
        Material yaprak1 = new Material(LitShader()); yaprak1.SetColor("_BaseColor", new Color(0.17f, 0.25f, 0.11f));
        Material yaprak2 = new Material(LitShader()); yaprak2.SetColor("_BaseColor", new Color(0.24f, 0.30f, 0.13f));
        foreach (Material mm in new[] { govde, yaprak1, yaprak2 }) { mm.SetFloat("_Smoothness", 0f); mm.enableInstancing = true; }

        // Ağaçlar kümeler halinde (koruluklar), ordu hatlarının ortasına daha az
        int kume = Random.Range(7, 12);
        for (int k = 0; k < kume; k++)
        {
            Vector2 merkez = new Vector2(Random.Range(-Yari + 4f, Yari - 4f), Random.Range(-Yari + 4f, Yari - 4f));
            if (Mathf.Abs(merkez.y) < 8f) merkez.y += Mathf.Sign(merkez.y + 0.01f) * 10f;
            int adet = Random.Range(8, 20);
            for (int a = 0; a < adet; a++)
            {
                Vector2 o = merkez + Random.insideUnitCircle * Random.Range(2f, 6f);
                Vector3 yer = Merkez + new Vector3(o.x, 0f, o.y);
                if (!Icinde(yer, 1f)) continue;
                yer = Uzerinde(yer);
                float boy = Random.Range(0.8f, 1.4f);
                GameObject agac = new GameObject("Agac");
                agac.transform.SetParent(kok, true);
                agac.transform.position = yer;
                Parca(PrimitiveType.Cylinder, agac.transform, new Vector3(0f, 0.6f * boy, 0f), new Vector3(0.18f, 0.6f * boy, 0.18f), govde);
                Parca(PrimitiveType.Sphere, agac.transform, new Vector3(0f, 1.6f * boy, 0f),
                      new Vector3(1.3f, 1.5f, 1.3f) * boy, Random.value < 0.5f ? yaprak1 : yaprak2);
            }
        }
    }

    static void Parca(PrimitiveType tur, Transform ebeveyn, Vector3 yer, Vector3 olcek, Material m)
    {
        GameObject g = GameObject.CreatePrimitive(tur);
        Object.Destroy(g.GetComponent<Collider>());
        g.transform.SetParent(ebeveyn, false);
        g.transform.localPosition = yer;
        g.transform.localScale = olcek;
        g.GetComponent<Renderer>().sharedMaterial = m;
    }

    public static Shader LitShader()
    {
        Shader s = Shader.Find("Universal Render Pipeline/Lit");
        return s != null ? s : Shader.Find("Standard");
    }
}
