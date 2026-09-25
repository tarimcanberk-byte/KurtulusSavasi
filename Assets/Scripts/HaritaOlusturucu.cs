using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Oyun başlarken haritayı kurar:
//   deniz → komşu ülkeler → sancak bölgeleri (gerçek şekilleriyle) → sınır çizgileri → isimler → yollar → ordular.
// Bölge şekilleri Resources/HaritaGeometri.json dosyasından, oyun verisi HaritaVerisi.cs'den gelir.
public class HaritaOlusturucu : MonoBehaviour
{
    public float olcek = 4f;                 // 1 derece = kaç Unity birimi (JSON ile aynı olmalı)
    public float merkezEnlem = 39f;
    public float merkezBoylam = 35f;

    public Color denizRengi   = new Color(0.20f, 0.33f, 0.45f);
    public Color yabanciRengi = new Color(0.50f, 0.48f, 0.43f);
    public Color sinirRengi   = new Color(0.10f, 0.10f, 0.08f, 0.9f);

    private readonly Dictionary<string, Sancak> sancaklar = new Dictionary<string, Sancak>();
    private GeoHarita geo;
    private Material duzMalzeme;     // ışıktan etkilenmeyen düz renk malzeme
    private Material cizgiMalzeme;
    private bool arazi3B = false;           // gerçek yükseklik verisi yüklendi mi

    void Awake()
    {
        // Önceki oyundan kalan durumu temizle
        Diplomasi.Sifirla();
        TarihselOlaylar.Sifirla();

        Shader duz = Shader.Find("Universal Render Pipeline/Unlit");
        if (duz == null) duz = Shader.Find("Universal Render Pipeline/Lit");
        duzMalzeme = new Material(duz);
        cizgiMalzeme = new Material(Shader.Find("Sprites/Default"));

        arazi3B = Arazi.Yukle();

        TextAsset json = Resources.Load<TextAsset>("HaritaGeometri");
        if (json != null) geo = JsonUtility.FromJson<GeoHarita>(json.text);
        else Debug.LogWarning("HaritaGeometri.json bulunamadı, kare sancaklar kullanılacak.");

        DenizVeKomsuUlkeler();
        SancaklariKur();
        KomsulariKur();
        YollariCiz();
        OrdulariKur();
    }

    void Start()
    {
        KameraKontrol kamera = FindAnyObjectByType<KameraKontrol>();
        if (kamera != null && sancaklar.ContainsKey("Ankara"))
            kamera.Odakla(sancaklar["Ankara"].transform.position, 22f);
    }

    Vector3 Konum(float enlem, float boylam)
    {
        float x = (boylam - merkezBoylam) * Mathf.Cos(merkezEnlem * Mathf.Deg2Rad) * olcek;
        float z = (enlem - merkezEnlem) * olcek;
        return new Vector3(x, 0f, z);
    }

    // ---------- Deniz ve komşu ülkeler ----------

    void DenizVeKomsuUlkeler()
    {
        if (arazi3B) { AraziyiKur(); return; }
        GameObject deniz = GameObject.CreatePrimitive(PrimitiveType.Quad);
        deniz.name = "Deniz";
        Destroy(deniz.GetComponent<Collider>());
        deniz.transform.position = new Vector3(0f, -0.05f, 0f);
        deniz.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        deniz.transform.localScale = new Vector3(160f, 80f, 1f);
        deniz.GetComponent<Renderer>().material = RenkliMalzeme(denizRengi);

        if (geo == null || geo.yabanci == null) return;
        GameObject yabanci = new GameObject("KomsuUlkeler");
        yabanci.transform.position = new Vector3(0f, -0.02f, 0f);
        yabanci.AddComponent<MeshFilter>().mesh = MeshOlustur(geo.yabanci, Vector3.zero);
        yabanci.AddComponent<MeshRenderer>().material = RenkliMalzeme(yabanciRengi);
    }

    // Gerçek yükseklikli arazi + uzak deniz + yabancı toprakların soluk örtüsü
    void AraziyiKur()
    {
        // Alçak açılı, sıcak güneş: dağların gölgesi belirgin olsun
        Light gunes = null;
        foreach (Light l in FindObjectsByType<Light>(FindObjectsSortMode.None))
            if (l.type == LightType.Directional) { gunes = l; break; }
        if (gunes != null)
        {
            gunes.transform.rotation = Quaternion.Euler(32f, -35f, 0f);
            gunes.color = new Color(1f, 0.95f, 0.85f);
            gunes.intensity = 1.35f;
            gunes.shadows = LightShadows.Soft;
        }

        GameObject arazi = new GameObject("Arazi");
        arazi.AddComponent<MeshFilter>().mesh = Arazi.AraziMeshi();
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        Material m = new Material(lit != null ? lit : duzMalzeme.shader);
        Texture2D doku = Arazi.RenkDokusu();
        if (doku != null) { m.SetTexture("_BaseMap", doku); m.mainTexture = doku; }
        m.color = Color.white;
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.05f);
        arazi.AddComponent<MeshRenderer>().material = m;

        // Haritanın dışında kalan boşluk görünmesin diye geniş, koyu bir deniz
        GameObject deniz = GameObject.CreatePrimitive(PrimitiveType.Quad);
        deniz.name = "UzakDeniz";
        Destroy(deniz.GetComponent<Collider>());
        deniz.transform.position = new Vector3(0f, Arazi.DenizY - 0.02f, 0f);
        deniz.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        deniz.transform.localScale = new Vector3(400f, 200f, 1f);
        deniz.GetComponent<Renderer>().material = RenkliMalzeme(new Color(0.07f, 0.17f, 0.29f));

        // Dalgalanan deniz yüzeyi ve atmosfer (pus, renk düzeltmesi)
        SuYuzeyi.Kur(-0.006f, new Vector2(Arazi.XMax - Arazi.XMin + 20f, Arazi.ZMax - Arazi.ZMin + 14f));
        Atmosfer.Kur();

        // Oynanamayan komşu ülkelerin üstüne hafif gri örtü
        GameObject yabanci = new GameObject("KomsuUlkeler");
        yabanci.AddComponent<MeshFilter>().mesh = Arazi.BolgeMeshi(Arazi.Yabanci, Vector3.zero, 0.01f);
        Material ym = SaydamMalzeme();
        ym.color = new Color(0.35f, 0.33f, 0.30f, 0.45f);
        yabanci.AddComponent<MeshRenderer>().material = ym;
    }

    // Arazinin üzerine serilen yarı saydam renk tabakası için malzeme (URP Unlit, saydam)
    Material SaydamMalzeme()
    {
        Material m = new Material(duzMalzeme);
        m.SetFloat("_Surface", 1f);                  // Transparent
        m.SetFloat("_Blend", 0f);                    // Alpha
        m.SetOverrideTag("RenderType", "Transparent");
        m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetInt("_ZWrite", 0);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        return m;
    }

    // Bir noktayı arazinin üstüne oturtur (arazi yoksa olduğu gibi bırakır)
    Vector3 Yere(Vector3 p, float ek)
    {
        return arazi3B ? Arazi.Uzerinde(p, ek) : p + new Vector3(0f, ek, 0f);
    }

    Material RenkliMalzeme(Color c)
    {
        Material m = new Material(duzMalzeme);
        m.color = c;
        return m;
    }

    // ---------- Sancaklar ----------

    void SancaklariKur()
    {
        GameObject klasor = new GameObject("Sancaklar");
        Dictionary<string, GeoSancak> sekiller = new Dictionary<string, GeoSancak>();
        if (geo != null && geo.sancaklar != null)
            foreach (GeoSancak g in geo.sancaklar) sekiller[g.ad] = g;

        foreach (var t in HaritaVerisi.Sancaklar)
        {
            Vector3 merkez = Konum(t.enlem, t.boylam);
            GameObject bolge;
            Vector3 etiketYeri;

            if (arazi3B && sekiller.TryGetValue(t.ad, out GeoSancak sekil3))
            {
                // Arazinin üzerine serilen yarı saydam bölge
                merkez = Arazi.Uzerinde(merkez);
                bolge = new GameObject(t.ad);
                bolge.transform.position = merkez;
                Mesh mesh = Arazi.BolgeMeshi(Arazi.BolgeNo(t.ad), merkez, 0.015f);
                bolge.AddComponent<MeshFilter>().mesh = mesh;
                bolge.AddComponent<MeshRenderer>().material = SaydamMalzeme();
                bolge.AddComponent<MeshCollider>().sharedMesh = mesh;
                SinirlariCiz(bolge.transform, sekil3.parcalar);
                etiketYeri = new Vector3(sekil3.etiketX, 0f, sekil3.etiketZ);
            }
            else if (sekiller.TryGetValue(t.ad, out GeoSancak sekil))
            {
                // Gerçek bölge şekli
                bolge = new GameObject(t.ad);
                bolge.transform.position = merkez;
                Mesh mesh = MeshOlustur(sekil.parcalar, merkez);
                bolge.AddComponent<MeshFilter>().mesh = mesh;
                bolge.AddComponent<MeshRenderer>().material = new Material(duzMalzeme);
                bolge.AddComponent<MeshCollider>().sharedMesh = mesh;
                SinirlariCiz(bolge.transform, sekil.parcalar);
                etiketYeri = new Vector3(sekil.etiketX, 0f, sekil.etiketZ);
            }
            else
            {
                // Şekil verisi yoksa eski usul kare
                bolge = GameObject.CreatePrimitive(PrimitiveType.Cube);
                bolge.name = t.ad;
                bolge.transform.position = merkez;
                bolge.transform.localScale = new Vector3(0.9f, 0.15f, 0.9f);
                etiketYeri = merkez + new Vector3(0f, 0f, -0.65f);
            }
            bolge.transform.SetParent(klasor.transform, true);

            Sancak s = bolge.AddComponent<Sancak>();
            s.saydam = arazi3B;
            s.sancakAdi = t.ad;
            s.sahip = t.sahip;
            s.paraUretimi = t.para;
            s.erzakUretimi = t.erzak;
            s.kiyiMi = System.Array.IndexOf(HaritaVerisi.KiyiSancaklari, t.ad) >= 0;
            s.araziBonusu = HaritaVerisi.AraziBonusu(t.ad);
            s.RengiGuncelle();
            sancaklar[t.ad] = s;

            if (arazi3B) SehirMaketi.Kur(bolge.transform, merkez, t.ad, SehirMaketi.Buyukluk(t.ad, t.para));
            else SehirIsaretiEkle(bolge.transform, merkez);
            EtiketEkle(bolge.transform, t.ad, etiketYeri);
        }
    }

    // JSON'daki parçalardan tek bir Mesh üretir. Köşeler "merkez"e göre yerel olur.
    Mesh MeshOlustur(GeoParca[] parcalar, Vector3 merkez)
    {
        List<Vector3> koseler = new List<Vector3>();
        List<int> ucgenler = new List<int>();
        foreach (GeoParca p in parcalar)
        {
            int baslangic = koseler.Count;
            for (int i = 0; i + 1 < p.v.Length; i += 2)
                koseler.Add(new Vector3(p.v[i], 0f, p.v[i + 1]) - merkez);

            for (int i = 0; i + 2 < p.t.Length; i += 3)
            {
                int a = baslangic + p.t[i], b = baslangic + p.t[i + 1], c = baslangic + p.t[i + 2];
                // Üçgenin yüzü yukarı bakmalı (yoksa kamera göremez ve tıklanamaz)
                Vector3 normal = Vector3.Cross(koseler[b] - koseler[a], koseler[c] - koseler[a]);
                if (normal.y < 0f) { int tmp = b; b = c; c = tmp; }
                ucgenler.Add(a); ucgenler.Add(b); ucgenler.Add(c);
            }
        }
        Mesh mesh = new Mesh();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(koseler);
        mesh.SetTriangles(ucgenler, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    void SinirlariCiz(Transform ebeveyn, GeoParca[] parcalar)
    {
        foreach (GeoParca p in parcalar)
        {
            if (p.sinir == null || p.sinir.Length < 4) continue;
            GameObject go = new GameObject("Sinir");
            go.transform.SetParent(ebeveyn, false);
            LineRenderer lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.material = cizgiMalzeme;
            lr.startColor = lr.endColor = sinirRengi;
            lr.startWidth = lr.endWidth = arazi3B ? 0.035f : 0.05f;
            lr.loop = true;
            int n = p.sinir.Length / 2;
            List<Vector3> noktalar = new List<Vector3>();
            for (int i = 0; i < n; i++)
            {
                Vector3 a = new Vector3(p.sinir[2 * i], 0f, p.sinir[2 * i + 1]);
                Vector3 b = new Vector3(p.sinir[2 * ((i + 1) % n)], 0f, p.sinir[2 * ((i + 1) % n) + 1]);
                // Arazinin kıvrımlarını izlesin diye uzun kenarları böl
                int parca = arazi3B ? Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(a, b) / 0.08f)) : 1;
                for (int s = 0; s < parca; s++)
                {
                    Vector3 q = Vector3.Lerp(a, b, s / (float)parca);
                    noktalar.Add(arazi3B ? Arazi.Uzerinde(q, 0.03f) : q + new Vector3(0f, 0.02f, 0f));
                }
            }
            lr.positionCount = noktalar.Count;
            lr.SetPositions(noktalar.ToArray());
        }
    }

    void SehirIsaretiEkle(Transform ebeveyn, Vector3 merkez)
    {
        GameObject nokta = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        nokta.name = "Sehir";
        Destroy(nokta.GetComponent<Collider>());
        nokta.transform.position = merkez + new Vector3(0f, 0.02f, 0f);   // merkez zaten arazinin üstünde
        nokta.transform.localScale = new Vector3(0.18f, 0.01f, 0.18f);
        nokta.GetComponent<Renderer>().material = RenkliMalzeme(new Color(0.15f, 0.12f, 0.1f));
        nokta.transform.SetParent(ebeveyn, true);
    }

    void EtiketEkle(Transform ebeveyn, string yazi, Vector3 konum)
    {
        GameObject etiket = new GameObject("Etiket");
        TextMeshPro tmp = etiket.AddComponent<TextMeshPro>();
        etiket.transform.position = Yere(konum, arazi3B ? 0.3f : 0.05f);
        etiket.transform.rotation = Quaternion.Euler(90f, 0f, 0f);   // yere yatık
        etiket.transform.SetParent(ebeveyn, true);

        tmp.text = yazi;
        tmp.fontSize = 4f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(1f, 0.97f, 0.9f);
        tmp.rectTransform.sizeDelta = new Vector2(5f, 0.8f);
        tmp.outlineWidth = 0.25f;
        tmp.outlineColor = new Color32(20, 15, 10, 255);
    }

    // ---------- Komşuluklar ----------

    static bool Yasakli(string a, string b)
    {
        var liste = HaritaVerisi.KomsuDegil;
        for (int i = 0; i < liste.GetLength(0); i++)
            if ((liste[i, 0] == a && liste[i, 1] == b) || (liste[i, 0] == b && liste[i, 1] == a)) return true;
        return false;
    }

    void Bagla(string a, string b)
    {
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return;
        if (!sancaklar.ContainsKey(a) || !sancaklar.ContainsKey(b)) { Debug.LogWarning("Bilinmeyen sancak: " + a + " / " + b); return; }
        if (Yasakli(a, b)) return;
        Sancak sa = sancaklar[a], sb = sancaklar[b];
        if (!sa.komsular.Contains(sb)) sa.komsular.Add(sb);
        if (!sb.komsular.Contains(sa)) sb.komsular.Add(sa);
    }

    static float MesafeKm(HaritaVerisi.SancakTanimi a, HaritaVerisi.SancakTanimi b)
    {
        float dy = (a.enlem - b.enlem) * 111f;
        float dx = (a.boylam - b.boylam) * 111f * Mathf.Cos((a.enlem + b.enlem) * 0.5f * Mathf.Deg2Rad);
        return Mathf.Sqrt(dx * dx + dy * dy);
    }

    void KomsulariKur()
    {
        if (geo != null && geo.komsuluklar != null && geo.komsuluklar.Length > 0)
        {
            // Ortak sınırı olan sancaklar komşudur
            foreach (GeoKomsuluk k in geo.komsuluklar) Bagla(k.a, k.b);
        }
        else
        {
            // Yedek yöntem: mesafeye göre
            var liste = HaritaVerisi.Sancaklar;
            for (int i = 0; i < liste.Length; i++)
                for (int j = i + 1; j < liste.Length; j++)
                    if (MesafeKm(liste[i], liste[j]) <= HaritaVerisi.KomsulukMesafesiKm)
                        Bagla(liste[i].ad, liste[j].ad);
        }

        var ek = HaritaVerisi.EkKomsuluklar;
        for (int i = 0; i < ek.GetLength(0); i++) Bagla(ek[i, 0], ek[i, 1]);
    }

    // Komşu sancak merkezleri arasına soluk yol çizgileri
    void YollariCiz()
    {
        GameObject klasor = new GameObject("Yollar");
        HashSet<string> cizilen = new HashSet<string>();
        foreach (Sancak s in sancaklar.Values)
            foreach (Sancak k in s.komsular)
            {
                string anahtar = string.CompareOrdinal(s.sancakAdi, k.sancakAdi) < 0 ? s.sancakAdi + "|" + k.sancakAdi : k.sancakAdi + "|" + s.sancakAdi;
                if (!cizilen.Add(anahtar)) continue;

                GameObject yol = new GameObject("Yol " + anahtar);
                yol.transform.SetParent(klasor.transform);
                LineRenderer cizgi = yol.AddComponent<LineRenderer>();
                cizgi.material = cizgiMalzeme;
                cizgi.startColor = cizgi.endColor = new Color(0.95f, 0.9f, 0.75f, arazi3B ? 0.16f : 0.35f);
                cizgi.startWidth = cizgi.endWidth = arazi3B ? 0.03f : 0.04f;
                int adim = arazi3B ? 16 : 1;
                cizgi.positionCount = adim + 1;
                for (int i = 0; i <= adim; i++)
                    cizgi.SetPosition(i, Yere(Vector3.Lerp(s.transform.position, k.transform.position, i / (float)adim), 0.04f));
            }
    }

    // ---------- Ordular ----------

    void OrdulariKur()
    {
        new GameObject("Ordular");
        foreach (var t in HaritaVerisi.Ordular)
        {
            if (!sancaklar.ContainsKey(t.sancak)) { Debug.LogWarning("Ordu için sancak bulunamadı: " + t.sancak); continue; }
            Ordu.Olustur(t.ad, t.taraf, t.asker, sancaklar[t.sancak]);
        }
    }
}
