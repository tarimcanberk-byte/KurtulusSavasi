using System;
using System.Collections.Generic;
using UnityEngine;

// Gerçek yükseklik verisiyle 3 boyutlu arazi.
// Resources klasöründeki dosyalar (Claude'un ürettiği):
//   arazi_bilgi.json      : ızgara boyutu, harita sınırları, bölge sırası
//   arazi_yukseklik.bytes : her ızgara noktasının yüksekliği (metre, int16; deniz eksi)
//   arazi_bolge.bytes     : her ızgara hücresinin hangi sancağa ait olduğu (254 = su, 255 = yabancı kara)
//   arazi_renk.bytes      : arazinin renk dokusu (JPEG)
public static class Arazi
{
    [Serializable]
    class Bilgi { public int nx, nz; public float xmin, xmax, zmin, zmax; public string[] bolgeSirasi; }

    public const float MetreBasinaBirim = 0.00035f;   // yükseklik abartması (Ağrı Dağı ≈ 1.8 birim)
    public const float DenizY = -0.03f;
    public const byte Su = 254, Yabanci = 255, SinirHucresi = 253;

    // Sınır hücrelerinin sancak poligonuna tam oturan parçaları (arazi_kenar.json)
    [Serializable] class KenarParca { public string ad; public float[] v; public int[] t; }
    [Serializable] class KenarDosya { public KenarParca[] sancaklar; }
    static Dictionary<string, KenarParca> kenarlar;

    public static bool Yuklu { get; private set; }
    public static int NX, NZ;
    public static float XMin, XMax, ZMin, ZMax;
    public static string[] BolgeSirasi;
    static short[] yukseklik;
    static byte[] bolge;

    public static bool Yukle()
    {
        Yuklu = false;
        TextAsset b = Resources.Load<TextAsset>("arazi_bilgi");
        TextAsset y = Resources.Load<TextAsset>("arazi_yukseklik");
        TextAsset g = Resources.Load<TextAsset>("arazi_bolge");
        if (b == null || y == null || g == null) return false;

        Bilgi bilgi = JsonUtility.FromJson<Bilgi>(b.text);
        NX = bilgi.nx; NZ = bilgi.nz;
        XMin = bilgi.xmin; XMax = bilgi.xmax; ZMin = bilgi.zmin; ZMax = bilgi.zmax;
        BolgeSirasi = bilgi.bolgeSirasi;

        byte[] ham = y.bytes;
        yukseklik = new short[NX * NZ];
        Buffer.BlockCopy(ham, 0, yukseklik, 0, Mathf.Min(ham.Length, yukseklik.Length * 2));
        bolge = g.bytes;

        kenarlar = new Dictionary<string, KenarParca>();
        TextAsset k = Resources.Load<TextAsset>("arazi_kenar");
        if (k != null)
            foreach (KenarParca p in JsonUtility.FromJson<KenarDosya>(k.text).sancaklar) kenarlar[p.ad] = p;
        Yuklu = true;
        return true;
    }

    static Texture2D renkDokusu;   // bir kez yüklenir (harita ve savaş ekranı paylaşır)
    public static Texture2D RenkDokusu()
    {
        if (renkDokusu != null) return renkDokusu;
        TextAsset r = Resources.Load<TextAsset>("arazi_renk");
        if (r == null) return null;
        Texture2D t = new Texture2D(2, 2, TextureFormat.RGBA32, true);
        t.LoadImage(r.bytes);
        t.wrapMode = TextureWrapMode.Clamp;
        t.anisoLevel = 8;
        t.filterMode = FilterMode.Trilinear;
        renkDokusu = t;
        return t;
    }

    static float Dx { get { return (XMax - XMin) / (NX - 1); } }
    static float Dz { get { return (ZMax - ZMin) / (NZ - 1); } }

    // Izgara noktasının Unity yüksekliği (i: batı→doğu, j: kuzey→güney)
    static float NoktaY(int i, int j)
    {
        i = Mathf.Clamp(i, 0, NX - 1); j = Mathf.Clamp(j, 0, NZ - 1);
        float m = yukseklik[j * NX + i];
        return m > 0f ? m * MetreBasinaBirim : DenizY;
    }

    static Vector3 Nokta(int i, int j) { return new Vector3(XMin + i * Dx, NoktaY(i, j), ZMax - j * Dz); }

    // Haritadaki herhangi bir (x, z) noktasının arazi yüksekliği
    public static float Yukseklik(float x, float z)
    {
        if (!Yuklu) return 0f;
        float fi = (x - XMin) / Dx, fj = (ZMax - z) / Dz;
        int i = Mathf.FloorToInt(fi), j = Mathf.FloorToInt(fj);
        float tx = fi - i, tz = fj - j;
        float a = Mathf.Lerp(NoktaY(i, j), NoktaY(i + 1, j), tx);
        float c = Mathf.Lerp(NoktaY(i, j + 1), NoktaY(i + 1, j + 1), tx);
        return Mathf.Max(Mathf.Lerp(a, c, tz), DenizY);
    }

    public static Vector3 Uzerinde(Vector3 p, float ek = 0f)
    {
        return new Vector3(p.x, Yukseklik(p.x, p.z) + ek, p.z);
    }

    // Bütün arazinin ağı
    public static Mesh AraziMeshi()
    {
        Vector3[] v = new Vector3[NX * NZ];
        Vector2[] uv = new Vector2[NX * NZ];
        for (int j = 0; j < NZ; j++)
            for (int i = 0; i < NX; i++)
            {
                v[j * NX + i] = Nokta(i, j);
                uv[j * NX + i] = new Vector2(i / (float)(NX - 1), 1f - j / (float)(NZ - 1));
            }
        int[] t = new int[(NX - 1) * (NZ - 1) * 6];
        int k = 0;
        for (int j = 0; j < NZ - 1; j++)
            for (int i = 0; i < NX - 1; i++)
            {
                int a = j * NX + i, b = a + 1, c = a + NX, d = c + 1;
                t[k++] = a; t[k++] = b; t[k++] = d;
                t[k++] = a; t[k++] = d; t[k++] = c;
            }
        Mesh m = new Mesh();
        m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        m.vertices = v; m.uv = uv; m.triangles = t;
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }

    // Bir bölgenin (sancak ya da yabancı kara) arazinin üzerine serilen ağı. Köşeler "merkez"e göre yerel.
    public static Mesh BolgeMeshi(byte id, Vector3 merkez, float ek)
    {
        Dictionary<int, int> indeks = new Dictionary<int, int>();
        List<Vector3> v = new List<Vector3>();
        List<int> t = new List<int>();
        int cw = NX - 1;
        int Kose(int i, int j)
        {
            int anahtar = j * NX + i;
            if (!indeks.TryGetValue(anahtar, out int n))
            {
                n = v.Count;
                indeks[anahtar] = n;
                Vector3 p = Nokta(i, j);
                p.y = Mathf.Max(p.y, 0f) + ek;
                v.Add(p - merkez);
            }
            return n;
        }
        for (int j = 0; j < NZ - 1; j++)
            for (int i = 0; i < cw; i++)
            {
                if (bolge[j * cw + i] != id) continue;
                int a = Kose(i, j), b = Kose(i + 1, j), c = Kose(i, j + 1), d = Kose(i + 1, j + 1);
                t.Add(a); t.Add(b); t.Add(d);
                t.Add(a); t.Add(d); t.Add(c);
            }
        // Sınır hücreleri: poligona tam oturan kesik parçalar (tırtıklı kenar olmasın)
        if (id < SinirHucresi && BolgeSirasi != null && id < BolgeSirasi.Length
            && kenarlar != null && kenarlar.TryGetValue(BolgeSirasi[id], out KenarParca kp))
        {
            int taban = v.Count;
            for (int n = 0; n + 1 < kp.v.Length; n += 2)
            {
                Vector3 p = new Vector3(kp.v[n], 0f, kp.v[n + 1]);
                p.y = Mathf.Max(Yukseklik(p.x, p.z), 0f) + ek;
                v.Add(p - merkez);
            }
            for (int n = 0; n + 2 < kp.t.Length; n += 3)
            {
                int a = taban + kp.t[n], b = taban + kp.t[n + 1], c = taban + kp.t[n + 2];
                Vector3 normal = Vector3.Cross(v[b] - v[a], v[c] - v[a]);
                if (normal.y < 0f) { int tmp = b; b = c; c = tmp; }
                t.Add(a); t.Add(b); t.Add(c);
            }
        }

        Mesh m = new Mesh();
        m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        m.SetVertices(v);
        m.SetTriangles(t, 0);
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }

    public static byte BolgeNo(string sancakAdi)
    {
        if (BolgeSirasi == null) return Su;
        int i = Array.IndexOf(BolgeSirasi, sancakAdi);
        return i < 0 ? Su : (byte)i;
    }
}
