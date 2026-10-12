using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Savaş ekranında bir birlik: piyade alayı, süvari alayı, topçu taburu, makineli tüfek bölüğü ya da karargâh.
// Figürler nizam halinde dizilir; kayıp verdikçe askerler yere düşer (toplar ve makineliler yerinde kalır).
public class Alay : MonoBehaviour
{
    public enum Durum { Hazir, Kacti, Bitti }
    public enum Tur { Piyade, Suvari, Topcu, Makineli, Karargah }

    // Birlik türlerinin özellikleri
    public class Ozellik
    {
        public string tip;
        public int sutun, sira;
        public float araX, araZ;
        public float yuruyus, kosu;
        public float menzil, minMenzil;
        public float atis;           // asker başına, yarım saniyelik atış etkisi
        public float gogus;          // asker başına, yarım saniyelik süngü/kılıç etkisi
        public float hucumCarpani;   // hücumda göğüs göğüse çarpanı
        public float cephane;
        public float atesAlma;       // tüfek ateşinden aldığı kayıp çarpanı
    }

    static readonly Dictionary<Tur, Ozellik> ozellikler = new Dictionary<Tur, Ozellik>
    {
        { Tur.Piyade,   new Ozellik { tip = "Piyade",   sutun = 8, sira = 4, araX = 0.75f, araZ = 0.85f, yuruyus = 2.2f, kosu = 4.2f,
                                      menzil = 16f, atis = 0.0022f, gogus = 0.006f, hucumCarpani = 1.3f, cephane = 140f, atesAlma = 1f } },
        { Tur.Suvari,   new Ozellik { tip = "Süvari",   sutun = 6, sira = 2, araX = 1.0f, araZ = 1.8f, yuruyus = 3.4f, kosu = 7.0f,
                                      menzil = 10f, atis = 0.0009f, gogus = 0.009f, hucumCarpani = 2.0f, cephane = 60f, atesAlma = 1.25f } },
        { Tur.Topcu,    new Ozellik { tip = "Topçu",    sutun = 4, sira = 3, araX = 2.0f, araZ = 1.1f, yuruyus = 1.3f, kosu = 1.7f,
                                      menzil = 48f, minMenzil = 6f, atis = 0f, gogus = 0.002f, hucumCarpani = 1f, cephane = 60f, atesAlma = 0.9f } },
        { Tur.Makineli, new Ozellik { tip = "Makineli", sutun = 4, sira = 2, araX = 1.1f, araZ = 0.9f, yuruyus = 1.8f, kosu = 3.0f,
                                      menzil = 20f, atis = 0.010f, gogus = 0.003f, hucumCarpani = 1f, cephane = 120f, atesAlma = 0.9f } },
        { Tur.Karargah, new Ozellik { tip = "Karargâh", sutun = 3, sira = 1, araX = 1.2f, araZ = 1.8f, yuruyus = 3.4f, kosu = 7.0f,
                                      menzil = 8f, atis = 0.001f, gogus = 0.010f, hucumCarpani = 1.8f, cephane = 40f, atesAlma = 1.25f } },
    };

    public static Ozellik Ozellikleri(Tur t) { return ozellikler[t]; }

    public string ad;
    public Tur tur;
    public Ozellik oz;
    public Taraf taraf;
    public bool oyuncunun;
    public bool savunan;            // savunan taraf mevzisinde siper avantajı alır
    public int baslangic;
    public float adam;              // kalan asker (kesirli; kayıplar yavaş yavaş düşer)
    public float moral = 80f;
    public float cephane;
    public float verim = 1f;        // haritadaki moral ve ikmal durumundan gelen etkinlik
    public float siper = 1f;        // savunanın mevzi çarpanı (arazi + tahkimat)
    public Durum durum = Durum.Hazir;
    public bool duzenliCekildi;     // emirle çekilen birlik kaçan kadar dağılmaz

    // Emirler
    public Vector3 hedefNokta;
    public Alay hedefAlay;
    public bool hucum;
    public bool yuruyor;
    public float duranSure;         // kaç saniyedir yerinde (top ve makineli kurulumu için)
    public float atisSayaci;        // topçu yaylım ateşi zamanlayıcısı
    public Vector3 mevzi;           // başladığı yer (savunan için siper)
    public Vector3 sonBakis;        // varınca döneceği yön (sağ tıkla sürükleyerek verilir; sıfırsa yürüyüş yönü)

    public const float TemasMesafesi = 2.6f;

    readonly List<Transform> figurler = new List<Transform>();
    readonly List<Vector3> daginiklik = new List<Vector3>();
    readonly List<bool> dustu = new List<bool>();
    readonly List<bool> kalici = new List<bool>();     // toplar ve makineliler: düşmez, yerinde kalır
    int askerFigurSayisi;
    Transform bayrak;
    TextMeshPro yazi;
    LineRenderer cerceve;
    LineRenderer emirCizgisi;      // emir oku: yürürken beyaz, saldırırken kırmızı
    bool secili;
    float adimSaati;

    public bool Savasabilir { get { return durum == Durum.Hazir && adam > 0.5f; } }
    public int GorunenAdam { get { return Mathf.CeilToInt(adam); } }
    public float Genislik { get { return oz.sutun * oz.araX; } }
    public float Derinlik { get { return oz.sira * oz.araZ; } }
    public float Menzil { get { return oz.menzil; } }
    public bool Atli { get { return tur == Tur.Suvari || tur == Tur.Karargah; } }
    // Ağır silahlar ancak durduktan bir süre sonra ateş edebilir
    public bool Kurulu { get { return tur == Tur.Topcu ? duranSure > 4f : tur == Tur.Makineli ? duranSure > 1.5f : true; } }

    static readonly Dictionary<string, Material> malzemeler = new Dictionary<string, Material>();

    public static Alay Olustur(Transform ebeveyn, string ad, Tur tur, Taraf taraf, bool oyuncunun, int asker,
                               Vector3 yer, Vector3 bakis, float moral, float verim)
    {
        GameObject g = new GameObject(ad);
        g.transform.SetParent(ebeveyn, true);
        g.transform.position = MuharebeAlani.Uzerinde(yer);
        g.transform.rotation = Quaternion.LookRotation(bakis, Vector3.up);
        Alay a = g.AddComponent<Alay>();
        a.ad = ad; a.tur = tur; a.oz = ozellikler[tur];
        a.taraf = taraf; a.oyuncunun = oyuncunun;
        a.baslangic = asker; a.adam = asker;
        a.moral = Mathf.Clamp(tur == Tur.Karargah ? moral + 10f : moral, 20f, 100f);
        a.verim = verim;
        a.cephane = a.oz.cephane;
        a.duranSure = 10f;          // savaş başında mevzide kurulu
        a.hedefNokta = g.transform.position;
        a.mevzi = g.transform.position;
        a.Kur();
        return a;
    }

    public static void Sifirla() { malzemeler.Clear(); }

    static Material Malzeme(Color c)
    {
        string anahtar = ColorUtility.ToHtmlStringRGB(c);
        if (malzemeler.TryGetValue(anahtar, out Material m) && m != null) return m;
        m = new Material(MuharebeAlani.LitShader());
        m.SetColor("_BaseColor", c);
        m.SetFloat("_Smoothness", 0.1f);
        m.enableInstancing = true;
        malzemeler[anahtar] = m;
        return m;
    }

    // n. figür ne: 0 yaya asker, 1 atlı, 2 top, 3 makineli tüfek
    int FigurTuru(int n)
    {
        int s = n / oz.sutun, c = n % oz.sutun;
        switch (tur)
        {
            case Tur.Suvari: case Tur.Karargah: return 1;
            case Tur.Topcu: return s == 0 ? 2 : 0;
            case Tur.Makineli: return s == 0 && (c == 1 || c == 2) ? 3 : 0;
            default: return 0;
        }
    }

    void Kur()
    {
        int toplam = oz.sutun * oz.sira;
        for (int n = 0; n < toplam; n++)
        {
            int ft = FigurTuru(n);
            GameObject f = new GameObject(ft == 2 ? "Top" : ft == 3 ? "Makineli" : "Asker");
            f.transform.SetParent(transform.parent, true);   // figürler serbest dolaşır, birliğe yavaşça yetişir
            if (ft == 0) YayaKur(f.transform);
            else if (ft == 1) AtliKur(f.transform, n);
            else if (ft == 2) TopKur(f.transform);
            else MakineliKur(f.transform);
            figurler.Add(f.transform);
            float dag = ft >= 2 ? 0.05f : 0.12f;
            daginiklik.Add(new Vector3(Random.Range(-dag, dag), 0f, Random.Range(-dag, dag)));
            dustu.Add(false);
            kalici.Add(ft >= 2);
            if (ft < 2) askerFigurSayisi++;
            f.transform.position = SlotDunya(n);
            f.transform.rotation = transform.rotation;
        }

        // Bayrak (uzaktan birliği tanımak için); karargâhın sancağı daha büyük
        Material direkM = Malzeme(new Color(0.22f, 0.16f, 0.10f));
        float b = tur == Tur.Karargah ? 1.5f : 1f;
        GameObject direk = new GameObject("Bayrak");
        direk.transform.SetParent(transform, false);
        direk.transform.localPosition = new Vector3(0f, 0f, -Derinlik * 0.5f - 0.2f);
        Parca(PrimitiveType.Cylinder, direk.transform, new Vector3(0f, 1.4f * b, 0f), new Vector3(0.05f, 1.4f * b, 0.05f), direkM);
        Transform bez = Parca(PrimitiveType.Cube, direk.transform, new Vector3(0.45f * b, 2.45f * b, 0f), new Vector3(0.85f * b, 0.55f * b, 0.03f),
                              Malzeme(TarafBilgi.OrduRengi(taraf)));
        if (taraf == Taraf.Turk)
            Parca(PrimitiveType.Cylinder, bez, new Vector3(-0.1f, 0f, -0.6f), new Vector3(0.35f, 0.02f, 0.5f), Malzeme(Color.white))
                .localRotation = Quaternion.Euler(90f, 0f, 0f);
        bayrak = direk.transform;

        // Bilgi yazısı
        GameObject y = new GameObject("Yazi");
        y.transform.SetParent(transform, false);
        yazi = y.AddComponent<TextMeshPro>();
        yazi.fontSize = 6f;
        yazi.alignment = TextAlignmentOptions.Center;
        yazi.rectTransform.sizeDelta = new Vector2(14f, 3f);
        yazi.outlineWidth = 0.25f;
        yazi.outlineColor = new Color32(0, 0, 0, 200);

        // Seçim / taraf çerçevesi
        cerceve = gameObject.AddComponent<LineRenderer>();
        cerceve.material = new Material(Shader.Find("Sprites/Default"));
        cerceve.loop = true;
        cerceve.positionCount = 4;
        cerceve.useWorldSpace = true;
        cerceve.widthMultiplier = 0.12f;

        BoxCollider kutu = gameObject.AddComponent<BoxCollider>();
        kutu.size = new Vector3(Genislik + 0.6f, Atli ? 3f : 2.4f, Derinlik + 0.8f);
        kutu.center = new Vector3(0f, Atli ? 1.4f : 1.1f, 0f);

        // Emir oku
        GameObject ok = new GameObject("EmirOku");
        ok.transform.SetParent(transform.parent, false);
        emirCizgisi = ok.AddComponent<LineRenderer>();
        emirCizgisi.material = cerceve.material;
        emirCizgisi.useWorldSpace = true;
        emirCizgisi.numCapVertices = 2;
        emirCizgisi.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        emirCizgisi.enabled = false;

        SecimGoster(false);
    }

    // ---------- Figürler ----------

    void Kafa(Transform f, float y)
    {
        Color uniforma = TarafBilgi.UniformaRengi(taraf);
        Parca(PrimitiveType.Sphere, f, new Vector3(0f, y, 0f), new Vector3(0.17f, 0.17f, 0.17f), Malzeme(new Color(0.78f, 0.60f, 0.46f)));
        if (taraf == Taraf.Turk)   // kalpak
            Parca(PrimitiveType.Cylinder, f, new Vector3(0f, y + 0.12f, 0f), new Vector3(0.19f, 0.07f, 0.19f), Malzeme(new Color(0.20f, 0.16f, 0.12f)));
        else                       // kasket
            Parca(PrimitiveType.Cylinder, f, new Vector3(0f, y + 0.08f, 0.02f), new Vector3(0.20f, 0.03f, 0.22f), Malzeme(uniforma * 0.8f));
        if (tur == Tur.Karargah)   // subay: altın şerit
            Parca(PrimitiveType.Cylinder, f, new Vector3(0f, y + 0.07f, 0f), new Vector3(0.2f, 0.015f, 0.2f), Malzeme(new Color(0.85f, 0.68f, 0.25f)));
    }

    void YayaKur(Transform f)
    {
        Parca(PrimitiveType.Capsule, f, new Vector3(0f, 0.42f, 0f), new Vector3(0.28f, 0.40f, 0.22f), Malzeme(TarafBilgi.UniformaRengi(taraf)));
        Kafa(f, 0.92f);
        Parca(PrimitiveType.Cube, f, new Vector3(0.12f, 0.62f, 0.05f), new Vector3(0.035f, 0.75f, 0.035f), Malzeme(new Color(0.22f, 0.16f, 0.10f)))
            .localRotation = Quaternion.Euler(12f, 0f, 0f);
    }

    static readonly Color[] atRenkleri = { new Color(0.36f, 0.22f, 0.12f), new Color(0.18f, 0.12f, 0.08f), new Color(0.50f, 0.36f, 0.22f), new Color(0.12f, 0.11f, 0.10f) };

    void AtliKur(Transform f, int n)
    {
        Material at = Malzeme(atRenkleri[(n * 7 + 3) % atRenkleri.Length]);
        Parca(PrimitiveType.Cube, f, new Vector3(0f, 0.78f, 0f), new Vector3(0.32f, 0.36f, 0.95f), at);                 // gövde
        Parca(PrimitiveType.Cube, f, new Vector3(0f, 1.05f, 0.52f), new Vector3(0.16f, 0.42f, 0.22f), at)
            .localRotation = Quaternion.Euler(30f, 0f, 0f);                                                                   // boyun
        Parca(PrimitiveType.Cube, f, new Vector3(0f, 1.22f, 0.70f), new Vector3(0.14f, 0.16f, 0.32f), at);              // baş
        for (int i = 0; i < 4; i++)
            Parca(PrimitiveType.Cube, f, new Vector3(i % 2 == 0 ? -0.11f : 0.11f, 0.32f, i < 2 ? 0.36f : -0.36f), new Vector3(0.07f, 0.62f, 0.07f), at);
        Parca(PrimitiveType.Cube, f, new Vector3(0f, 0.85f, -0.5f), new Vector3(0.05f, 0.4f, 0.05f), at)
            .localRotation = Quaternion.Euler(-25f, 0f, 0f);                                                                  // kuyruk
        // Süvari
        Parca(PrimitiveType.Capsule, f, new Vector3(0f, 1.32f, -0.05f), new Vector3(0.26f, 0.32f, 0.2f), Malzeme(TarafBilgi.UniformaRengi(taraf)));
        Kafa(f, 1.72f);
        Parca(PrimitiveType.Cube, f, new Vector3(0.18f, 1.5f, 0.15f), new Vector3(0.03f, 0.03f, 0.8f), Malzeme(new Color(0.75f, 0.76f, 0.78f)))
            .localRotation = Quaternion.Euler(-35f, 0f, 0f);                                                                  // kılıç
    }

    void TopKur(Transform f)
    {
        Material demir = Malzeme(new Color(0.16f, 0.17f, 0.16f));
        Material ahsap = Malzeme(new Color(0.33f, 0.26f, 0.17f));
        Material kalkan = Malzeme(TarafBilgi.UniformaRengi(taraf) * 0.75f);
        for (int i = -1; i <= 1; i += 2)
            Parca(PrimitiveType.Cylinder, f, new Vector3(0.38f * i, 0.32f, 0f), new Vector3(0.64f, 0.04f, 0.64f), ahsap)
                .localRotation = Quaternion.Euler(0f, 0f, 90f);                                                               // tekerlek
        Parca(PrimitiveType.Cylinder, f, new Vector3(0f, 0.32f, 0f), new Vector3(0.06f, 0.4f, 0.06f), demir)
            .localRotation = Quaternion.Euler(0f, 0f, 90f);                                                                   // dingil
        Parca(PrimitiveType.Cylinder, f, new Vector3(0f, 0.5f, 0.45f), new Vector3(0.11f, 0.55f, 0.11f), demir)
            .localRotation = Quaternion.Euler(80f, 0f, 0f);                                                                   // namlu
        Parca(PrimitiveType.Cube, f, new Vector3(0f, 0.55f, 0.08f), new Vector3(0.8f, 0.5f, 0.04f), kalkan);              // zırh kalkanı
        Parca(PrimitiveType.Cube, f, new Vector3(0f, 0.18f, -0.55f), new Vector3(0.1f, 0.08f, 1.0f), ahsap)
            .localRotation = Quaternion.Euler(-12f, 0f, 0f);                                                                  // kundak
    }

    void MakineliKur(Transform f)
    {
        Material demir = Malzeme(new Color(0.15f, 0.16f, 0.15f));
        for (int i = 0; i < 3; i++)
            Parca(PrimitiveType.Cube, f, Quaternion.Euler(0f, i * 120f, 0f) * new Vector3(0f, 0.15f, 0.18f), new Vector3(0.03f, 0.32f, 0.03f), demir)
                .localRotation = Quaternion.Euler(25f, i * 120f, 0f);                                                         // sehpa
        Parca(PrimitiveType.Cylinder, f, new Vector3(0f, 0.36f, 0.12f), new Vector3(0.1f, 0.28f, 0.1f), demir)
            .localRotation = Quaternion.Euler(90f, 0f, 0f);                                                                   // su gömleği
        Parca(PrimitiveType.Cylinder, f, new Vector3(0f, 0.36f, 0.48f), new Vector3(0.03f, 0.1f, 0.03f), demir)
            .localRotation = Quaternion.Euler(90f, 0f, 0f);                                                                   // namlu ağzı
        Parca(PrimitiveType.Cube, f, new Vector3(0f, 0.4f, 0.1f), new Vector3(0.36f, 0.24f, 0.02f), Malzeme(TarafBilgi.UniformaRengi(taraf) * 0.75f));
        Parca(PrimitiveType.Cube, f, new Vector3(0f, 0.34f, -0.12f), new Vector3(0.1f, 0.12f, 0.22f), demir);           // gövde
    }

    static Transform Parca(PrimitiveType tur, Transform ebeveyn, Vector3 yer, Vector3 olcek, Material m)
    {
        GameObject g = GameObject.CreatePrimitive(tur);
        Object.Destroy(g.GetComponent<Collider>());
        g.transform.SetParent(ebeveyn, false);
        g.transform.localPosition = yer;
        g.transform.localScale = olcek;
        Renderer r = g.GetComponent<Renderer>();
        r.sharedMaterial = m;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        return g.transform;
    }

    // n. figürün nizamdaki yeri (dünya)
    Vector3 SlotDunya(int n)
    {
        int s = n / oz.sutun, c = n % oz.sutun;
        Vector3 yerel = new Vector3((c - (oz.sutun - 1) / 2f) * oz.araX, 0f, -(s - (oz.sira - 1) / 2f) * oz.araZ);
        if (n < daginiklik.Count) yerel += daginiklik[n];
        return MuharebeAlani.Uzerinde(transform.position + transform.rotation * yerel);
    }

    public void SecimGoster(bool s) { secili = s; }

    // ---------- Hareket (her kare) ----------

    public void Hareket(float dt)
    {
        if (durum == Durum.Bitti) return;
        Vector3 p = transform.position;
        Vector3 hedef = p;
        float hiz = oz.yuruyus;
        yuruyor = false;

        if (durum == Durum.Kacti)
        {
            // Kendi tarafının kenarına doğru kaç
            Vector3 kacis = (mevzi - MuharebeAlani.Merkez); kacis.y = 0f;
            kacis = kacis.sqrMagnitude < 1f ? -transform.forward : kacis.normalized;
            hedef = p + kacis * 10f;
            hiz = Mathf.Max(oz.kosu, 3.5f) * 1.1f;
            if (!MuharebeAlani.Icinde(p, 1.5f)) { durum = Durum.Bitti; Gizle(); return; }
        }
        else if (hedefAlay != null && hedefAlay.Savasabilir)
        {
            float mesafe = Vector3.Distance(Duz(p), Duz(hedefAlay.transform.position));
            if (hucum) { if (mesafe > TemasMesafesi * 0.9f) hedef = hedefAlay.transform.position; hiz = oz.kosu; }
            else if (mesafe > Menzil * 0.9f) hedef = hedefAlay.transform.position;
            else Bak(hedefAlay.transform.position - p, dt);
        }
        else
        {
            if (hedefAlay != null && !hedefAlay.Savasabilir) { hedefAlay = null; hucum = false; hedefNokta = p; }
            hedef = hedefNokta;
            if (hucum) hiz = oz.kosu;
        }

        Vector3 fark = Duz(hedef) - Duz(p);
        if (fark.magnitude > 0.3f)
        {
            // Yokuş yukarı yavaşlar
            Vector3 adim = fark.normalized * hiz * dt;
            if (adim.magnitude > fark.magnitude) adim = fark;
            float egim = MuharebeAlani.Yukseklik(p.x + adim.x, p.z + adim.z) - MuharebeAlani.Yukseklik(p.x, p.z);
            if (egim > 0f) adim *= Mathf.Clamp(1f - egim / Mathf.Max(0.001f, adim.magnitude) * 0.8f, 0.45f, 1f);
            p += adim;
            Bak(fark, dt);
            yuruyor = adim.sqrMagnitude > 0f;
        }
        else if (durum == Durum.Hazir && hedefAlay == null && sonBakis.sqrMagnitude > 0.01f) Bak(sonBakis, dt);   // mevzide istenen yöne dön
        duranSure = yuruyor ? 0f : duranSure + dt;
        // Kaçanlar dışında kimse meydanın dışına çıkamaz
        if (durum == Durum.Hazir)
        {
            float s = MuharebeAlani.Yari - 2f;
            Vector3 yerel = p - MuharebeAlani.Merkez;
            p = MuharebeAlani.Merkez + new Vector3(Mathf.Clamp(yerel.x, -s, s), 0f, Mathf.Clamp(yerel.z, -s, s));
        }
        transform.position = MuharebeAlani.Uzerinde(p);
    }

    static Vector3 Duz(Vector3 v) { v.y = 0f; return v; }

    void Bak(Vector3 yon, float dt)
    {
        yon.y = 0f;
        if (yon.sqrMagnitude < 0.001f) return;
        float donus = tur == Tur.Topcu ? 35f : Atli ? 120f : 90f;
        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(yon), donus * dt);
    }

    // Figürler nizamdaki yerlerine yetişir; kaçarken dağılır
    public void Gorsel(float dt)
    {
        if (durum == Durum.Bitti) return;
        int canli = Mathf.Clamp(Mathf.CeilToInt(askerFigurSayisi * adam / Mathf.Max(1f, baslangic)), 0, askerFigurSayisi);
        float hiz = Mathf.Max(oz.kosu, 3.5f);
        adimSaati += dt * (yuruyor ? (hucum || durum == Durum.Kacti ? 11f : 7f) : 0f);
        int sayac = 0;
        for (int n = 0; n < figurler.Count; n++)
        {
            Transform f = figurler[n];
            if (f == null || dustu[n]) continue;
            if (kalici[n])
            {
                // Toplar ve makineliler: kaçarken terk edilir
                if (durum == Durum.Kacti) continue;
            }
            else
            {
                if (sayac >= canli) { Dusur(n); continue; }
                sayac++;
            }
            Vector3 slot = SlotDunya(n);
            if (durum == Durum.Kacti) slot += new Vector3(Mathf.Sin(n * 1.7f) * 2.5f, 0f, Mathf.Cos(n * 2.3f) * 2.5f);
            Vector3 yeni = Vector3.MoveTowards(f.position, slot, (durum == Durum.Kacti ? hiz * 1.4f : hiz) * dt * 1.3f);
            float ziplama = yuruyor && !kalici[n] ? Mathf.Abs(Mathf.Sin(adimSaati + n)) * (Atli ? 0.12f : 0.06f) : 0f;
            yeni.y = MuharebeAlani.Yukseklik(yeni.x, yeni.z) + ziplama;
            Vector3 hareket = yeni - f.position; hareket.y = 0f;
            f.position = yeni;
            Quaternion bakis = hareket.sqrMagnitude > 0.0004f && durum == Durum.Kacti ? Quaternion.LookRotation(hareket) : transform.rotation;
            f.rotation = Quaternion.Slerp(f.rotation, bakis, dt * 6f);
        }

        // Çerçeve
        Vector3 sag = transform.right * (Genislik * 0.5f + 0.3f), ileri = transform.forward * (Derinlik * 0.5f + 0.4f);
        Vector3 c = transform.position;
        cerceve.SetPosition(0, MuharebeAlani.Uzerinde(c + sag + ileri, 0.08f));
        cerceve.SetPosition(1, MuharebeAlani.Uzerinde(c + sag - ileri, 0.08f));
        cerceve.SetPosition(2, MuharebeAlani.Uzerinde(c - sag - ileri, 0.08f));
        cerceve.SetPosition(3, MuharebeAlani.Uzerinde(c - sag + ileri, 0.08f));
        Color renk = secili ? new Color(1f, 0.85f, 0.3f, 0.95f)
                   : oyuncunun ? new Color(0.9f, 0.2f, 0.15f, 0.45f) : new Color(0.3f, 0.5f, 0.95f, 0.45f);
        cerceve.startColor = cerceve.endColor = renk;
        cerceve.widthMultiplier = secili ? 0.18f : 0.1f;

        EmirOkunuCiz();

        // Yazı: kameraya dönük
        Camera kam = Camera.main;
        yazi.transform.position = transform.position + Vector3.up * (Atli ? 4.2f : 3.6f);
        if (kam != null) yazi.transform.rotation = Quaternion.LookRotation(yazi.transform.position - kam.transform.position);
        string moralRengi = moral > 60f ? "#9f9" : moral > 30f ? "#fd6" : "#f77";
        string d = durum == Durum.Kacti ? " <color=#f77>KAÇIYOR</color>" : hucum ? " <color=#fc6>HÜCUM</color>"
                 : (tur == Tur.Topcu || tur == Tur.Makineli) && !Kurulu ? " <color=#ccc>kuruluyor</color>" : "";
        yazi.text = "<b>" + ad + "</b>" + d + "\n" + GorunenAdam + "  <color=" + moralRengi + ">moral " + Mathf.RoundToInt(moral) + "</color>"
                  + (cephane <= 0f ? "  <color=#f99>cephane yok</color>" : "");
        yazi.color = secili ? new Color(1f, 0.92f, 0.5f) : Color.white;
        bayrak.localRotation = Quaternion.Euler(0f, Mathf.Sin(Time.time * 2f + baslangic) * 8f, 0f);
    }

    // Seçili birliğin emrini gösteren, araziyi izleyen ok
    void EmirOkunuCiz()
    {
        if (emirCizgisi == null) return;
        bool saldiri = hedefAlay != null && hedefAlay.Savasabilir;
        Vector3 bas = transform.position, son = saldiri ? hedefAlay.transform.position : hedefNokta;
        Vector3 fark = son - bas; fark.y = 0f;
        float uzunluk = fark.magnitude;
        if (!secili || !oyuncunun || durum != Durum.Hazir || uzunluk < 1.5f) { emirCizgisi.enabled = false; return; }

        // Ok, birliğin önünden başlayıp hedefin biraz önünde biter
        Vector3 yon = fark / uzunluk;
        bas += yon * 1.8f;
        son -= yon * (saldiri ? 2.2f : 0.3f);
        uzunluk = Vector3.Distance(new Vector3(bas.x, 0f, bas.z), new Vector3(son.x, 0f, son.z));
        if (uzunluk < 0.5f) { emirCizgisi.enabled = false; return; }

        int n = Mathf.Clamp(Mathf.CeilToInt(uzunluk / 0.8f) + 1, 2, 120);
        emirCizgisi.positionCount = n;
        for (int i = 0; i < n; i++)
            emirCizgisi.SetPosition(i, MuharebeAlani.Uzerinde(Vector3.Lerp(bas, son, i / (float)(n - 1)), 0.25f));

        // Ucu ok başı: son 1,6 birimde genişleyip sivrilir
        float t = Mathf.Clamp01(1f - 1.6f / uzunluk);
        float govde = saldiri ? 0.35f : 0.25f;
        AnimationCurve egri = new AnimationCurve(
            new Keyframe(0f, govde), new Keyframe(t, govde), new Keyframe(Mathf.Min(0.999f, t + 0.001f), govde * 3.2f), new Keyframe(1f, 0f));
        emirCizgisi.widthCurve = egri;
        emirCizgisi.widthMultiplier = 1f;

        Color renk = saldiri ? (hucum ? new Color(1f, 0.15f, 0.1f, 0.95f) : new Color(0.9f, 0.12f, 0.1f, 0.8f))
                             : new Color(1f, 1f, 1f, 0.8f);
        emirCizgisi.startColor = new Color(renk.r, renk.g, renk.b, renk.a * 0.35f);
        emirCizgisi.endColor = renk;
        emirCizgisi.enabled = true;
    }

    // Bir figür vurulur: yere yatar ve öylece kalır (at yan yatar)
    void Dusur(int n)
    {
        dustu[n] = true;
        Transform f = figurler[n];
        Vector3 p = f.position;
        p.y = MuharebeAlani.Yukseklik(p.x, p.z) + (Atli ? 0.3f : 0.12f);
        f.position = p;
        Quaternion yat = Atli ? Quaternion.Euler(0f, 0f, Random.value < 0.5f ? -85f : 85f)
                              : Quaternion.Euler(Random.value < 0.5f ? -88f : 88f, 0f, 0f);
        f.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * yat;
    }

    // Meydandan çıkan birlik: ayakta kalan figürler kaybolur; düşenler ve terk edilen toplar yerde kalır
    void Gizle()
    {
        for (int n = 0; n < figurler.Count; n++)
            if (figurler[n] != null && !dustu[n] && !kalici[n]) figurler[n].gameObject.SetActive(false);
        if (emirCizgisi != null) emirCizgisi.enabled = false;
        gameObject.SetActive(false);
    }

    // Birliği derhal kaçış durumuna sok
    public void Bozgun()
    {
        if (durum != Durum.Hazir) return;
        durum = Durum.Kacti;
        hucum = false;
        hedefAlay = null;
    }

    public void Temizle()
    {
        foreach (Transform f in figurler) if (f != null) Destroy(f.gameObject);
        if (emirCizgisi != null) Destroy(emirCizgisi.gameObject);
        Destroy(gameObject);
    }
}
