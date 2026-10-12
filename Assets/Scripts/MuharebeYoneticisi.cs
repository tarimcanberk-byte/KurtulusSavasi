using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Savaş ekranı (Total War usulü), 1. aşama.
// Oyuncunun katıldığı her muharebede zaman durur ve sorulur: "Bizzat komuta et" ya da "Otomatik çöz".
// Bizzat komuta: ordular alaylara bölünür, gerçek arazide karşılaşır; sol tık seçer, sağ tık emir verir.
public class MuharebeYoneticisi : MonoBehaviour
{
    public static MuharebeYoneticisi Ornek;

    // Harita zamanı bu doğruyken durur
    public static bool Mesgul { get { return Ornek != null && (Ornek.kuyruk.Count > 0 || Ornek.karar || Ornek.aktif); } }

    struct Istek { public Ordu saldiran; public Sancak hedef; }
    readonly Queue<Istek> kuyruk = new Queue<Istek>();
    bool karar, aktif, devamEdecekti, elleSavasildi, acilacak;
    TurYoneticisi tur;

    // Karar penceresi
    GameObject kararPaneli;
    Istek simdiki;
    Ordu simdikiSavunan;

    // Savaş sahnesi
    Ordu saldiran, savunan;
    Sancak hedef;
    bool oyuncuSaldiriyor;
    GameObject kok;
    readonly List<Alay> alaylar = new List<Alay>();
    readonly List<Alay> secili = new List<Alay>();
    Canvas haritaCanvas, savasCanvas;
    Camera kam;
    Vector3 kamYer; Quaternion kamDonus; float kamUzak, sisYogunluk;
    KameraKontrol haritaKamerasi;
    MuharebeKamera savasKamerasi;
    TMP_Text ustYazi, seciliYazi, hizYazisi, mesajYazisi;
    float mesajSure;
    RectTransform secimKutusu;
    ParticleSystem duman;
    bool bitti, savasDuraklatildi;
    bool dizilis;                       // savaş öncesi: oyuncu birliklerini yerleştirir
    GameObject baslatDugmesi;
    LineRenderer dizilisCizgisi;
    int savasHizi = 1;
    float sure, carpismaSayaci, zekaSayaci;
    const float SureSiniri = 600f;      // 10 dakika: saldıran sonuç alamazsa savunan kazanır

    // Fare
    Vector2 surukleBaslangic;
    bool sagSurukle;                    // sağ tık basılı: bırakınca yürüyüş, sürüklenirse yön de verilir
    Vector3 sagBaslangic;
    bool sagCift;
    readonly List<LineRenderer> hayaletler = new List<LineRenderer>();   // yerleşme önizlemesi
    LineRenderer yonOku;
    bool surukleniyor;
    float sonSagTik;
    Alay sonSagHedef;

    // ---------- Harita tarafı ----------

    // Bu çarpışmada oyuncu var mı ve gerçekten karşısında bir ordu var mı?
    public static bool OyuncuKatilimli(Ordu gelen, Sancak hedef)
    {
        bool dusmanOrdusu = false, turkVar = gelen.OyuncununMu;
        foreach (Ordu o in hedef.Ordular())
        {
            if (!o.Yasiyor) continue;
            if (o.taraf != gelen.taraf) dusmanOrdusu = true;
            if (o.OyuncununMu) turkVar = true;
        }
        return dusmanOrdusu && turkVar;
    }

    public static void Iste(Ordu saldiran, Sancak hedef)
    {
        if (Ornek == null)
        {
            GameObject g = new GameObject("MuharebeYoneticisi");
            Ornek = g.AddComponent<MuharebeYoneticisi>();
        }
        Ornek.IstekEkle(saldiran, hedef);
    }

    void IstekEkle(Ordu s, Sancak h)
    {
        if (tur == null) tur = FindAnyObjectByType<TurYoneticisi>();
        if (!karar && !aktif && kuyruk.Count == 0)
        {
            devamEdecekti = tur != null && !tur.Duraklatildi;
            elleSavasildi = false;
            if (tur != null) tur.Duraklat();
        }
        kuyruk.Enqueue(new Istek { saldiran = s, hedef = h });
        karar = true;
        acilacak = true;     // pencere bir sonraki karede açılır (gün döngüsü bitsin)
    }

    void SiradakiniAc()
    {
        acilacak = false;
        while (kuyruk.Count > 0)
        {
            Istek i = kuyruk.Dequeue();
            if (i.saldiran == null || !i.saldiran.Yasiyor) continue;
            Ordu sv = EnGucluDusman(i.saldiran, i.hedef);
            if (sv == null || !OyuncuKatilimli(i.saldiran, i.hedef))
            {
                // Artık ortada muharebe yok (ör. düşman çekildi): eski usulle ilerle
                string r = Savas.Ilerle(i.saldiran, i.hedef);
                i.saldiran.MuharebeBitti(i.hedef);
                if (r != null) Gunluk.Ekle(r.Replace("\n", " "));
                continue;
            }
            simdiki = i; simdikiSavunan = sv;
            KararPenceresiAc();
            return;
        }
        // Hepsi bitti
        karar = false;
        HaritayiYenile();
        if (devamEdecekti && !elleSavasildi && tur != null) tur.Devam();
        devamEdecekti = false;
    }

    static Ordu EnGucluDusman(Ordu saldiran, Sancak hedef)
    {
        Ordu sv = null;
        foreach (Ordu o in hedef.Ordular())
            if (o.Yasiyor && o.taraf != saldiran.taraf && (sv == null || o.Guc > sv.Guc)) sv = o;
        return sv;
    }

    void HaritayiYenile()
    {
        if (tur != null) tur.EkraniGuncelle();
        HaritaSecici secici = FindAnyObjectByType<HaritaSecici>();
        if (secici != null) secici.BilgiyiYenile();
    }

    void KararPenceresiAc()
    {
        haritaCanvas = FindAnyObjectByType<Canvas>();
        KameraKontrol kk = FindAnyObjectByType<KameraKontrol>();
        if (kk != null) kk.Odakla(simdiki.hedef.transform.position, 16f);
        if (haritaCanvas == null) { OtomatikCoz(); return; }

        Ordu s = simdiki.saldiran, d = simdikiSavunan;
        kararPaneli = TamEkranKarartma(haritaCanvas.transform);
        RectTransform kutu = Panel(kararPaneli.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 480f), ArayuzYardimci.PanelRengi);
        kutu.GetComponent<Image>().color = new Color(0.10f, 0.08f, 0.06f, 0.96f);

        string siper = d.tahkimat > 0 ? ", siper " + d.tahkimat + ". kademe" : "";
        string metin = "<size=130%><b>Muharebe: " + simdiki.hedef.sancakAdi + "</b></size>\n\n"
            + "<b>Saldıran:</b> " + s.orduAdi + " <size=80%>(" + TarafBilgi.Ad(s.taraf) + ")</size> — " + s.askerSayisi + " asker, moral " + s.moral + "\n"
            + "<b>Savunan:</b> " + d.orduAdi + " <size=80%>(" + TarafBilgi.Ad(d.taraf) + ")</size> — " + d.askerSayisi + " asker, moral " + d.moral + siper + "\n\n"
            + "<size=85%>Tahmini güç: saldırı " + Mathf.RoundToInt(s.Guc) + "  /  savunma " + Mathf.RoundToInt(d.SavunmaGucu)
            + "  <color=#bbb>(savunan arazi x" + d.bulunduguSancak.araziBonusu.ToString("0.0#") + ")</color></size>\n\n"
            + "<size=85%>Bizzat komuta edersen savaş ekranı açılır. Otomatik çözümde sonuç güç ve şansa bağlıdır.</size>";
        TMP_Text t = Yazi(kutu, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(840f, 330f), 28f, TextAlignmentOptions.Top);
        t.text = metin;

        Dugme(kutu, "Bizzat Komuta Et", new Vector2(0.5f, 0f), new Vector2(-200f, 70f), new Vector2(360f, 84f), BizzatKomuta, 30f);
        Dugme(kutu, "Otomatik Çöz", new Vector2(0.5f, 0f), new Vector2(200f, 70f), new Vector2(360f, 84f), OtomatikCoz, 30f);
    }

    void KararPenceresiKapat()
    {
        if (kararPaneli != null) Destroy(kararPaneli);
        kararPaneli = null;
    }

    void OtomatikCoz()
    {
        KararPenceresiKapat();
        string r = Savas.Ilerle(simdiki.saldiran, simdiki.hedef);
        simdiki.saldiran.MuharebeBitti(simdiki.hedef);
        if (r != null) Gunluk.Ekle(r.Replace("\n", " "));
        SiradakiniAc();
    }

    void BizzatKomuta()
    {
        KararPenceresiKapat();
        elleSavasildi = true;
        SavasiKur(simdiki.saldiran, simdikiSavunan, simdiki.hedef);
    }

    // ---------- Savaşın kurulması ----------

    void SavasiKur(Ordu s, Ordu d, Sancak h)
    {
        aktif = true; bitti = false; savasDuraklatildi = false; savasHizi = 1;
        saldiran = s; savunan = d; hedef = h;
        oyuncuSaldiriyor = s.OyuncununMu;
        sure = 0f; carpismaSayaci = 0f; zekaSayaci = 0f;
        alaylar.Clear(); secili.Clear();
        hayaletler.Clear(); yonOku = null; sagSurukle = false;
        Alay.Sifirla();
        Time.timeScale = 1f;

        // Harita arayüzü ve kamerası kenara
        if (haritaCanvas != null) haritaCanvas.gameObject.SetActive(false);
        kam = Camera.main;
        kamYer = kam.transform.position; kamDonus = kam.transform.rotation; kamUzak = kam.farClipPlane;
        sisYogunluk = RenderSettings.fogDensity;
        RenderSettings.fogDensity = 0.006f;
        kam.farClipPlane = 600f;
        haritaKamerasi = kam.GetComponent<KameraKontrol>();
        if (haritaKamerasi != null) haritaKamerasi.enabled = false;
        savasKamerasi = kam.gameObject.AddComponent<MuharebeKamera>();

        kok = new GameObject("Muharebe");
        MuharebeAlani.Kur(h, kok.transform);
        DumanKur();

        // Saldıran güneyden, savunan kuzeyden
        AlaylariKur(s, true, -24f);
        AlaylariKur(d, false, 12f);

        Vector3 m = MuharebeAlani.Merkez;
        if (oyuncuSaldiriyor) savasKamerasi.Ayarla(m + new Vector3(0f, 0f, -12f), 0f, 30f);
        else savasKamerasi.Ayarla(m + new Vector3(0f, 0f, 6f), 180f, 30f);

        SavasArayuzunuKur();
        DizilisBaslat();
        Gunluk.Ekle("<b>" + h.sancakAdi + " meydan muharebesi başladı.</b> Komuta sende.");
    }

    // Haritadaki ordunun savaş düzeni: karargâh, topçu, makineli tüfek, süvari ve kalanı piyade
    static List<KeyValuePair<Alay.Tur, int>> Kompozisyon(Ordu o)
    {
        List<KeyValuePair<Alay.Tur, int>> liste = new List<KeyValuePair<Alay.Tur, int>>();
        int a = o.askerSayisi, kalan = a;
        bool turk = o.taraf == Taraf.Turk, ermeni = o.taraf == Taraf.Ermeni;
        bool donanimli = !turk && !ermeni;     // İtilaf orduları daha çok top ve makineliye sahip
        bool kuvayi = o.orduAdi.Contains("Kuva");

        int karargah = a >= 2000 ? 1 : 0;
        int topcu = turk ? (a >= 16000 ? 2 : a >= 5000 ? 1 : 0)
                  : donanimli ? (a >= 25000 ? 3 : a >= 12000 ? 2 : a >= 4000 ? 1 : 0)
                  : (a >= 6000 ? 1 : 0);
        int makineli = donanimli ? (a >= 20000 ? 3 : a >= 8000 ? 2 : a >= 3000 ? 1 : 0)
                     : (a >= 10000 ? 2 : a >= 3000 ? 1 : 0);
        int suvari = turk ? (a >= 15000 ? 2 : a >= 5000 ? 1 : 0) + (kuvayi && a >= 2000 ? 1 : 0)
                   : ermeni ? (a >= 6000 ? 1 : 0)
                   : (a >= 15000 ? 1 : 0);

        void Koy(Alay.Tur t, int sayi, int asker)
        {
            for (int i = 0; i < sayi; i++)
            {
                if (kalan - asker < 1000) return;   // piyadeye yer kalsın
                liste.Add(new KeyValuePair<Alay.Tur, int>(t, asker));
                kalan -= asker;
            }
        }
        Koy(Alay.Tur.Karargah, karargah, 150);
        Koy(Alay.Tur.Topcu, topcu, 500);
        Koy(Alay.Tur.Makineli, makineli, 400);
        Koy(Alay.Tur.Suvari, suvari, 900);

        int n = Mathf.Clamp(Mathf.RoundToInt(kalan / 1500f), 1, 8);
        for (int i = 0; i < n; i++)
        {
            int pay = i < n - 1 ? kalan / n : kalan - (kalan / n) * (n - 1);
            liste.Add(new KeyValuePair<Alay.Tur, int>(Alay.Tur.Piyade, pay));
        }
        return liste;
    }

    // Ön hatta makineli + piyade, arkasında topçu, en geride karargâh, kanatlarda süvari
    void AlaylariKur(Ordu o, bool saldiranMi, float z)
    {
        List<KeyValuePair<Alay.Tur, int>> komp = Kompozisyon(o);
        float verim = Mathf.Clamp(o.Guc / Mathf.Max(1f, o.askerSayisi), 0.3f, 1.5f);
        float siper = saldiranMi ? 1f : Mathf.Clamp(o.SavunmaGucu / Mathf.Max(1f, o.Guc), 1f, 2.2f);
        Vector3 ileri = saldiranMi ? Vector3.forward : Vector3.back;
        Vector3 sag = Vector3.Cross(Vector3.up, ileri);
        Vector3 merkez = MuharebeAlani.Merkez + new Vector3(0f, 0f, z);
        string onEk = o.OyuncununMu ? "" : TarafBilgi.Ad(o.taraf) + " ";

        List<KeyValuePair<Alay.Tur, int>> onHat = new List<KeyValuePair<Alay.Tur, int>>();
        List<KeyValuePair<Alay.Tur, int>> topcular = new List<KeyValuePair<Alay.Tur, int>>();
        List<KeyValuePair<Alay.Tur, int>> suvariler = new List<KeyValuePair<Alay.Tur, int>>();
        List<KeyValuePair<Alay.Tur, int>> makineliler = new List<KeyValuePair<Alay.Tur, int>>();
        KeyValuePair<Alay.Tur, int>? karargah = null;
        foreach (var k in komp)
        {
            switch (k.Key)
            {
                case Alay.Tur.Piyade: onHat.Add(k); break;
                case Alay.Tur.Topcu: topcular.Add(k); break;
                case Alay.Tur.Suvari: suvariler.Add(k); break;
                case Alay.Tur.Makineli: makineliler.Add(k); break;
                default: karargah = k; break;
            }
        }
        // Makineliler piyade hattının iki ucuna ve ortasına
        for (int i = 0; i < makineliler.Count; i++)
        {
            if (i == 0) onHat.Insert(0, makineliler[i]);
            else if (i == 1) onHat.Add(makineliler[i]);
            else onHat.Insert(onHat.Count / 2, makineliler[i]);
        }

        Dictionary<Alay.Tur, int> sayac = new Dictionary<Alay.Tur, int>();
        float hatYarisi = SiraDiz(onHat, merkez, sag, ileri, 1.2f, o, onEk, verim, siper, !saldiranMi, sayac);
        SiraDiz(topcular, merkez - ileri * 9f, sag, ileri, 3f, o, onEk, verim, siper, !saldiranMi, sayac);
        if (karargah.HasValue)
            SiraDiz(new List<KeyValuePair<Alay.Tur, int>> { karargah.Value }, merkez - ileri * 15f, sag, ileri, 0f, o, onEk, verim, siper, !saldiranMi, sayac);
        for (int i = 0; i < suvariler.Count; i++)
        {
            float yan = (hatYarisi + 6f + (i / 2) * 8f) * (i % 2 == 0 ? 1f : -1f);
            Vector3 yer = merkez + sag * yan - ileri * 3f;
            BirlikOlustur(suvariler[i], yer, ileri, o, onEk, verim, siper, !saldiranMi, sayac);
        }
    }

    // Birlikleri yan yana bir hat halinde dizer; hattın yarı genişliğini döndürür
    float SiraDiz(List<KeyValuePair<Alay.Tur, int>> birlikler, Vector3 merkez, Vector3 sag, Vector3 ileri, float ara,
                  Ordu o, string onEk, float verim, float siper, bool savunanMi, Dictionary<Alay.Tur, int> sayac)
    {
        if (birlikler.Count == 0) return 0f;
        float toplam = -ara;
        foreach (var b in birlikler) { Alay.Ozellik oz = Alay.Ozellikleri(b.Key); toplam += oz.sutun * oz.araX + ara; }
        float x = -toplam / 2f;
        foreach (var b in birlikler)
        {
            Alay.Ozellik oz = Alay.Ozellikleri(b.Key);
            float g = oz.sutun * oz.araX;
            BirlikOlustur(b, merkez + sag * (x + g / 2f), ileri, o, onEk, verim, siper, savunanMi, sayac);
            x += g + ara;
        }
        return toplam / 2f;
    }

    void BirlikOlustur(KeyValuePair<Alay.Tur, int> b, Vector3 yer, Vector3 ileri, Ordu o, string onEk, float verim, float siper,
                       bool savunanMi, Dictionary<Alay.Tur, int> sayac)
    {
        int no = (sayac.ContainsKey(b.Key) ? sayac[b.Key] : 0) + 1;
        sayac[b.Key] = no;
        string ad = b.Key == Alay.Tur.Piyade ? no + ". Alay"
                  : b.Key == Alay.Tur.Suvari ? no + ". Süvari"
                  : b.Key == Alay.Tur.Topcu ? no + ". Topçu"
                  : b.Key == Alay.Tur.Makineli ? no + ". Makineli"
                  : "Karargâh";
        Vector3 yerel = yer - MuharebeAlani.Merkez;
        float s = MuharebeAlani.Yari - 4f;
        yer = MuharebeAlani.Merkez + new Vector3(Mathf.Clamp(yerel.x, -s, s), 0f, Mathf.Clamp(yerel.z, -s, s));
        Alay a = Alay.Olustur(kok.transform, onEk + ad, b.Key, o.taraf, o.OyuncununMu, b.Value, yer, ileri, o.moral, verim);
        a.savunan = savunanMi;
        a.siper = siper;
        alaylar.Add(a);
    }

    // Tüfek dumanı ve namlu ateşi için tek bir parçacık sistemi
    void DumanKur()
    {
        GameObject g = new GameObject("Duman");
        g.transform.SetParent(kok.transform, false);
        duman = g.AddComponent<ParticleSystem>();
        duman.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var ana = duman.main;
        ana.playOnAwake = false;
        ana.simulationSpace = ParticleSystemSimulationSpace.World;
        ana.maxParticles = 4000;
        ana.startLifetime = 3f;
        ana.startSpeed = 0.25f;
        ana.gravityModifier = -0.015f;
        var emisyon = duman.emission; emisyon.enabled = false;
        var sekil = duman.shape; sekil.enabled = false;
        var renk = duman.colorOverLifetime; renk.enabled = true;
        Gradient gr = new Gradient();
        gr.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                   new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.6f, 0.3f), new GradientAlphaKey(0f, 1f) });
        renk.color = gr;
        var boy = duman.sizeOverLifetime; boy.enabled = true;
        boy.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.5f, 1f, 2.4f));

        Texture2D yumusak = new Texture2D(64, 64, TextureFormat.RGBA32, false);
        for (int y = 0; y < 64; y++)
            for (int x = 0; x < 64; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(31.5f, 31.5f)) / 32f;
                yumusak.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(1f - d) * Mathf.Clamp01(1f - d)));
            }
        yumusak.Apply();
        Material mm = new Material(Shader.Find("Sprites/Default"));
        mm.mainTexture = yumusak;
        g.GetComponent<ParticleSystemRenderer>().material = mm;
        duman.Play();
    }

    void Parcacik(Vector3 yer, Color renk, float boy, float omur, Vector3 hiz)
    {
        if (duman == null) return;
        var ep = new ParticleSystem.EmitParams();
        ep.position = yer; ep.startColor = renk; ep.startSize = boy; ep.startLifetime = omur; ep.velocity = hiz;
        duman.Emit(ep, 1);
    }

    // Tüfek ve makineli ateşi: namlu alevi ve barut dumanı
    void AtesEt(Alay a)
    {
        bool makineli = a.tur == Alay.Tur.Makineli;
        float yay = a.Genislik * 0.45f;
        int dumanSayi = makineli ? 1 : 2, alevSayi = makineli ? 4 : 3;
        for (int i = 0; i < dumanSayi; i++)
            Parcacik(a.transform.position + a.transform.forward * (a.Derinlik * 0.5f + 0.4f) + a.transform.right * Random.Range(-yay, yay) + Vector3.up * 0.9f,
                     new Color(0.86f, 0.86f, 0.82f, 0.45f), Random.Range(0.9f, 1.5f), Random.Range(2.2f, 3.5f),
                     a.transform.forward * 0.4f + Random.insideUnitSphere * 0.15f);
        for (int i = 0; i < alevSayi; i++)
            Parcacik(a.transform.position + a.transform.forward * (a.Derinlik * 0.5f + 0.2f) + a.transform.right * Random.Range(-yay, yay)
                     + Vector3.up * (makineli ? 0.4f : 0.75f), new Color(1f, 0.85f, 0.4f, 1f), makineli ? 0.22f : 0.3f, 0.12f, Vector3.zero);
    }

    // Top yaylım ateşi: bataryada kalın duman, hedefte toprak fışkırması
    void TopAtesi(Alay a, Alay h)
    {
        for (int i = 0; i < 4; i++)
        {
            Vector3 namlu = a.transform.position + a.transform.right * ((i - 1.5f) * a.oz.araX) + a.transform.forward * (a.Derinlik * 0.5f + 0.8f) + Vector3.up * 0.6f;
            Parcacik(namlu, new Color(0.9f, 0.88f, 0.84f, 0.6f), Random.Range(2.2f, 3.2f), Random.Range(3.5f, 5f), a.transform.forward * 0.6f + Vector3.up * 0.3f);
            Parcacik(namlu, new Color(1f, 0.75f, 0.35f, 1f), 1.2f, 0.15f, Vector3.zero);
        }
        for (int i = 0; i < 3; i++)
        {
            Vector3 yer = h.transform.position + h.transform.right * Random.Range(-h.Genislik * 0.6f, h.Genislik * 0.6f)
                        + h.transform.forward * Random.Range(-h.Derinlik, h.Derinlik);
            yer = MuharebeAlani.Uzerinde(yer, 0.3f);
            Parcacik(yer, new Color(1f, 0.6f, 0.25f, 1f), 1.6f, 0.18f, Vector3.zero);
            for (int k = 0; k < 3; k++)
                Parcacik(yer, new Color(0.42f, 0.34f, 0.24f, 0.75f), Random.Range(1.4f, 2.4f), Random.Range(1.5f, 2.5f),
                         Vector3.up * Random.Range(1.5f, 3f) + Random.insideUnitSphere * 0.8f);
        }
    }

    // Kısa süreli savaş haberi (ekranın üstünde)
    void Mesaj(string metin)
    {
        if (mesajYazisi == null) return;
        mesajYazisi.text = metin;
        mesajSure = 4f;
    }

    // ---------- Her kare ----------

    void Update()
    {
        if (acilacak && !aktif && kararPaneli == null) SiradakiniAc();
        if (!aktif) return;

        Girdi();
        if (mesajYazisi != null) { mesajSure -= Time.unscaledDeltaTime; mesajYazisi.alpha = Mathf.Clamp01(mesajSure); }
        if (bitti) return;

        if (dizilis)
        {
            // Diziliş: zaman akmaz, yalnızca yerleştirme
            Ayristir();
            foreach (Alay a in alaylar)
            {
                if (!a.oyuncunun) continue;
                Vector3 p = DizilisIcinde(a.transform.position, a);
                a.transform.position = p; a.hedefNokta = p; a.mevzi = p;
            }
            foreach (Alay a in alaylar) a.Gorsel(Time.deltaTime);
            ArayuzuGuncelle();
            return;
        }

        float dt = Time.deltaTime;
        foreach (Alay a in alaylar) a.Hareket(dt);
        Ayristir();
        foreach (Alay a in alaylar) a.Gorsel(dt);

        if (dt > 0f)
        {
            sure += dt;
            carpismaSayaci += dt; zekaSayaci += dt;
            if (carpismaSayaci >= 0.5f) { carpismaSayaci -= 0.5f; Carpisma(); }
            if (zekaSayaci >= 1f) { zekaSayaci -= 1f; YapayZeka(); }
        }
        ArayuzuGuncelle();
        BitisKontrol();
    }

    // Birlikler iç içe girmesin: aynı taraftakiler genişlikleri kadar aralık bırakır
    void Ayristir()
    {
        for (int i = 0; i < alaylar.Count; i++)
            for (int j = i + 1; j < alaylar.Count; j++)
            {
                Alay a = alaylar[i], b = alaylar[j];
                if (a.durum != Alay.Durum.Hazir || b.durum != Alay.Durum.Hazir) continue;
                float min = a.oyuncunun == b.oyuncunun
                          ? (Mathf.Max(a.Genislik, a.Derinlik) + Mathf.Max(b.Genislik, b.Derinlik)) * 0.5f + 0.8f
                          : Alay.TemasMesafesi * 0.85f;
                Vector3 f = b.transform.position - a.transform.position; f.y = 0f;
                float d = f.magnitude;
                if (d >= min) continue;
                Vector3 it = (d < 0.01f ? Vector3.right : f / d) * (min - d) * 0.5f;
                // Ağır toplar zor itilir; diğeri daha çok kayar
                float pa = a.tur == Alay.Tur.Topcu ? 0.2f : 1f, pb = b.tur == Alay.Tur.Topcu ? 0.2f : 1f;
                float top = pa + pb;
                a.transform.position = MuharebeAlani.Uzerinde(a.transform.position - it * (2f * pa / top));
                b.transform.position = MuharebeAlani.Uzerinde(b.transform.position + it * (2f * pb / top));
            }
    }

    // ---------- Çarpışma (yarım saniyede bir) ----------

    readonly HashSet<Alay> sonGogusGoguse = new HashSet<Alay>();

    void Carpisma()
    {
        Dictionary<Alay, float> kayip = new Dictionary<Alay, float>();
        HashSet<Alay> gogusGoguse = new HashSet<Alay>();

        foreach (Alay a in alaylar)
        {
            if (!a.Savasabilir) continue;
            Alay yakin = EnYakinDusman(a, out float yakinMesafe);
            if (yakin == null) continue;
            float moralCarpani = 0.5f + a.moral / 200f;

            if (yakinMesafe <= Alay.TemasMesafesi + 0.5f)
            {
                // Göğüs göğüse (süngü, kılıç)
                Alay h = (a.hedefAlay != null && a.hedefAlay.Savasabilir && Mesafe(a, a.hedefAlay) <= Alay.TemasMesafesi + 0.5f) ? a.hedefAlay : yakin;
                float kanat = Kanat(a, h);
                float carp = a.hucum ? a.oz.hucumCarpani : 1f;
                // Süvari, önden süngü duvarına çarpınca şok etkisini büyük ölçüde kaybeder
                if (a.Atli && a.hucum && kanat <= 1f && h.tur == Alay.Tur.Piyade && !h.yuruyor) carp *= 0.55f;
                // Topçu ve makineli mürettebatı yakın dövüşte çaresizdir
                if (h.tur == Alay.Tur.Topcu || h.tur == Alay.Tur.Makineli) carp *= 1.5f;
                float z = a.adam * a.oz.gogus * a.verim * moralCarpani * kanat * carp * Random.Range(0.75f, 1.25f) / SiperEtkisi(h);
                Ekle(kayip, h, z);
                gogusGoguse.Add(a); gogusGoguse.Add(h);
            }
            else if (a.tur == Alay.Tur.Topcu)
            {
                // Topçu kendi temposunda (3 saniyede bir) yaylım ateşi açar
                if (a.cephane <= 0f || !a.Kurulu) continue;
                a.atisSayaci += 0.5f;
                if (a.atisSayaci < 3f) continue;
                a.atisSayaci = 0f;
                TopAtisi(a, yakin, yakinMesafe, kayip);
            }
            else if (a.cephane > 0f && !a.hucum && a.Kurulu)
            {
                // Tüfek ve makineli ateşi
                Alay h = (a.hedefAlay != null && a.hedefAlay.Savasabilir && Mesafe(a, a.hedefAlay) <= a.Menzil) ? a.hedefAlay
                       : (yakinMesafe <= a.Menzil ? yakin : null);
                if (h == null) continue;
                float mesafe = Mesafe(a, h);
                float isabet = 1.3f - 0.6f * mesafe / a.Menzil;
                float tepe = a.transform.position.y > h.transform.position.y + 1f ? 1.25f : 1f;
                float hareket = a.yuruyor ? 0.35f : 1f;
                float turCarpani = h.oz.atesAlma * (a.tur == Alay.Tur.Makineli && h.Atli ? 1.3f : 1f);
                float z = a.adam * a.oz.atis * a.verim * moralCarpani * isabet * tepe * hareket * turCarpani
                          * Kanat(a, h) * Random.Range(0.75f, 1.25f) / SiperEtkisi(h);
                Ekle(kayip, h, z);
                a.cephane -= 1f;
                AtesEt(a);
            }
        }

        // Kaçanlar kovalanırken kayıp verir (süvari daha çok)
        foreach (Alay k in alaylar)
        {
            if (k.durum != Alay.Durum.Kacti) continue;
            foreach (Alay a in alaylar)
                if (a.Savasabilir && a.oyuncunun != k.oyuncunun && Mesafe(a, k) < (a.Atli ? 7f : 5f))
                { Ekle(kayip, k, k.adam * (a.Atli ? 0.05f : 0.02f)); break; }
        }

        // Kayıplar ve moral (karargâh yakınındaki birliklerin morali daha sağlam)
        foreach (Alay a in alaylar)
        {
            if (a.durum == Alay.Durum.Bitti) continue;
            float z = kayip.ContainsKey(a) ? Mathf.Min(kayip[a], a.adam) : 0f;
            a.adam -= z;
            if (a.durum != Alay.Durum.Hazir) continue;
            bool komutan = KomutanYakin(a);
            float dayanim = komutan ? 0.7f : 1f;
            if (z > 0f) a.moral -= z / Mathf.Max(1f, a.baslangic) * 100f * 0.9f * dayanim;
            if (gogusGoguse.Contains(a)) a.moral -= 0.3f * dayanim;
            if (z <= 0f && !gogusGoguse.Contains(a)) a.moral = Mathf.Min(100f, a.moral + (komutan ? 0.65f : 0.35f));
            if (a.moral < 15f || a.adam < a.baslangic * 0.2f) Bozul(a);
        }

        sonGogusGoguse.Clear();
        foreach (Alay a in gogusGoguse) sonGogusGoguse.Add(a);
    }

    void TopAtisi(Alay a, Alay yakin, float yakinMesafe, Dictionary<Alay, float> kayip)
    {
        bool uygun(Alay x, float m) { return x != null && x.Savasabilir && m >= a.oz.minMenzil && m <= a.Menzil; }
        Alay h = null;
        if (a.hedefAlay != null && uygun(a.hedefAlay, Mesafe(a, a.hedefAlay))) h = a.hedefAlay;
        else if (uygun(yakin, yakinMesafe)) h = yakin;
        else
        {
            // En yakın düşman çok yakınsa menzildeki başka bir hedefi dövebilir
            float en = float.MaxValue;
            foreach (Alay b in alaylar)
            {
                if (!b.Savasabilir || b.oyuncunun == a.oyuncunun) continue;
                float m = Mesafe(a, b);
                if (uygun(b, m) && m < en) { en = m; h = b; }
            }
        }
        if (h == null) return;

        float mesafe = Mesafe(a, h);
        float isleyen = Mathf.Clamp01(a.adam / Mathf.Max(1f, a.baslangic) * 1.3f);   // mürettebat azaldıkça daha az top işler
        float isabet = Mathf.Lerp(1.1f, 0.55f, mesafe / a.Menzil);
        float yogunluk = h.yuruyor ? 0.8f : 1f;
        float z = 26f * isleyen * a.verim * isabet * yogunluk * Random.Range(0.6f, 1.4f) / Mathf.Sqrt(SiperEtkisi(h));
        Ekle(kayip, h, z);
        h.moral -= 1.5f * isleyen;                         // top ateşinin sarsıntısı
        foreach (Alay b in alaylar)                         // yakındaki birliklere sıçrayan şarapnel
            if (b != h && b.Savasabilir && b.oyuncunun == h.oyuncunun && Mesafe(b, h) < 5f) Ekle(kayip, b, z * 0.3f);
        a.cephane -= 1f;
        TopAtesi(a, h);
    }

    bool KomutanYakin(Alay a)
    {
        if (a.tur == Alay.Tur.Karargah) return true;
        foreach (Alay k in alaylar)
            if (k.tur == Alay.Tur.Karargah && k.Savasabilir && k.oyuncunun == a.oyuncunun && Mesafe(a, k) < 14f) return true;
        return false;
    }

    void Bozul(Alay a)
    {
        a.Bozgun();
        if (a.oyuncunun) secili.Remove(a);
        a.SecimGoster(false);
        foreach (Alay b in alaylar)
        {
            if (b == a || !b.Savasabilir || Mesafe(a, b) > 12f) continue;
            if (b.oyuncunun == a.oyuncunun) b.moral -= 8f;       // yanındaki kaçınca panik
            else b.moral = Mathf.Min(100f, b.moral + 4f);
        }
        if (a.tur == Alay.Tur.Karargah)
        {
            // Komutanın düşmesi bütün orduyu sarsar
            foreach (Alay b in alaylar) if (b.Savasabilir && b.oyuncunun == a.oyuncunun) b.moral -= 15f;
            Mesaj(a.oyuncunun ? "<color=#f77><b>Karargâhımız dağıldı!</b> Bütün birliklerde moral sarsıldı.</color>"
                              : "<color=#9f9><b>Düşman karargâhı dağıldı!</b> Düşmanın morali çöktü.</color>");
        }
        else Mesaj((a.oyuncunun ? "<color=#f99>" : "<color=#9f9>") + a.ad + " bozuldu ve kaçıyor!</color>");
    }

    static void Ekle(Dictionary<Alay, float> d, Alay a, float z) { d[a] = (d.ContainsKey(a) ? d[a] : 0f) + z; }

    static float Mesafe(Alay a, Alay b)
    {
        Vector3 f = a.transform.position - b.transform.position; f.y = 0f;
        return f.magnitude;
    }

    Alay EnYakinDusman(Alay a, out float mesafe)
    {
        Alay en = null; mesafe = float.MaxValue;
        foreach (Alay b in alaylar)
        {
            if (!b.Savasabilir || b.oyuncunun == a.oyuncunun) continue;
            float d = Mesafe(a, b);
            if (d < mesafe) { mesafe = d; en = b; }
        }
        return en;
    }

    // Yandan ya da arkadan vurmak çok daha etkilidir
    static float Kanat(Alay vuran, Alay hedef)
    {
        Vector3 yon = vuran.transform.position - hedef.transform.position; yon.y = 0f;
        if (yon.sqrMagnitude < 0.01f) return 1f;
        float d = Vector3.Dot(hedef.transform.forward, yon.normalized);
        if (d < -0.35f) return 2f;      // arkadan
        if (d < 0.45f) return 1.5f;     // yandan
        return 1f;
    }

    // Savunan, mevzisinden ayrılmadıkça siper ve arazi avantajını korur
    static float SiperEtkisi(Alay a)
    {
        if (!a.savunan) return 1f;
        Vector3 f = a.transform.position - a.mevzi; f.y = 0f;
        return f.magnitude < 10f ? a.siper : 1f;
    }

    // ---------- Düşman komutanı (saniyede bir) ----------

    void YapayZeka()
    {
        bool dusmanSaldiriyor = !oyuncuSaldiriyor;
        Vector3 geri = dusmanSaldiriyor ? Vector3.back : Vector3.forward;   // düşmanın kendi gerisi
        Vector3 orta = Vector3.zero; int say = 0;
        foreach (Alay a in alaylar)
            if (!a.oyuncunun && a.Savasabilir && a.tur != Alay.Tur.Karargah) { orta += a.transform.position; say++; }
        if (say > 0) orta /= say;

        foreach (Alay a in alaylar)
        {
            if (a.oyuncunun || !a.Savasabilir) continue;
            Alay e = EnYakinDusman(a, out float d);
            if (e == null) continue;

            switch (a.tur)
            {
                case Alay.Tur.Karargah:
                    // Ordunun gerisinde durur; ancak saldırıya uğrarsa çarpışır
                    if (d < 5f) { a.hedefAlay = e; a.hucum = true; }
                    else if (say > 0) { a.hedefAlay = null; a.hucum = false; a.hedefNokta = MuharebeAlani.Uzerinde(orta + geri * 8f); }
                    break;

                case Alay.Tur.Topcu:
                    a.hucum = false;
                    if (d < a.oz.minMenzil + 3f) { a.hedefAlay = null; a.hedefNokta = MuharebeAlani.Uzerinde(a.transform.position + geri * 8f); }   // geri çekil
                    else if (dusmanSaldiriyor) a.hedefAlay = e;            // menzile girene dek ilerle
                    else if (d <= a.Menzil) a.hedefAlay = e;
                    else { a.hedefAlay = null; a.hedefNokta = a.mevzi; }
                    break;

                case Alay.Tur.Makineli:
                    a.hucum = false;
                    if (dusmanSaldiriyor) a.hedefAlay = e;
                    else if (d <= a.Menzil) a.hedefAlay = e;
                    else { a.hedefAlay = null; a.hedefNokta = a.mevzi; }
                    break;

                case Alay.Tur.Suvari:
                    SuvariKarari(a);
                    break;

                default:
                    if (dusmanSaldiriyor)
                    {
                        if (a.hedefAlay == null || !a.hedefAlay.Savasabilir || Mesafe(a, a.hedefAlay) > d + 8f) { a.hedefAlay = e; a.hucum = false; }
                        float h = Mesafe(a, a.hedefAlay);
                        bool ustun = a.adam * a.moral > a.hedefAlay.adam * a.hedefAlay.moral * 1.3f;
                        if (h < a.Menzil + 2f && (ustun || a.hedefAlay.moral < 40f || a.cephane <= 0f || sure > 240f)) a.hucum = true;
                    }
                    else
                    {
                        // Savunan mevzisini tutar; menzile gireni vurur, zayıflayana hücum eder
                        if (d <= a.Menzil) { if (a.hedefAlay == null || !a.hedefAlay.Savasabilir) a.hedefAlay = e; }
                        else if (!a.hucum) { a.hedefAlay = null; a.hedefNokta = a.mevzi; }
                        if (d < 10f && (e.moral < 35f || a.cephane <= 0f)) { a.hedefAlay = e; a.hucum = true; }
                        if (a.hucum && d > 18f) { a.hucum = false; a.hedefAlay = null; a.hedefNokta = a.mevzi; }   // fazla açılma, geri dön
                    }
                    break;
            }
        }
    }

    // Süvari fırsat kollar: topçu ve makineliye, çarpışmaya tutuşmuş birliklerin yanına, sarsılmış düşmana dalar
    void SuvariKarari(Alay a)
    {
        if (a.hucum && a.hedefAlay != null && a.hedefAlay.Savasabilir) return;   // hücum başladıysa sürsün
        Alay en = null; float enPuan = float.MaxValue;
        foreach (Alay b in alaylar)
        {
            if (!b.Savasabilir || b.oyuncunun == a.oyuncunun) continue;
            float puan = Mesafe(a, b);
            if (b.tur == Alay.Tur.Topcu || b.tur == Alay.Tur.Makineli) puan -= 15f;
            if (sonGogusGoguse.Contains(b)) puan -= 12f;
            if (b.moral < 40f) puan -= 8f;
            if (b.tur == Alay.Tur.Piyade && !sonGogusGoguse.Contains(b)) puan += 10f;   // dinç piyadeye önden dalma
            if (puan < enPuan) { enPuan = puan; en = b; }
        }
        if (en == null) return;
        bool firsat = en.tur == Alay.Tur.Topcu || en.tur == Alay.Tur.Makineli || sonGogusGoguse.Contains(en) || en.moral < 40f || sure > 150f;
        if (firsat && Mesafe(a, en) < 50f) { a.hedefAlay = en; a.hucum = true; }
        else { a.hedefAlay = null; a.hucum = false; a.hedefNokta = a.mevzi; }
    }

    // ---------- Oyuncunun girdisi ----------

    void Girdi()
    {
        Keyboard k = Keyboard.current;
        Mouse m = Mouse.current;
        if (bitti || m == null) return;

        if (k != null)
        {
            if (k.spaceKey.wasPressedThisFrame && !dizilis) DuraklatDevam();
            if (dizilis && (k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame)) SavasiBaslat();
            if (k.hKey.wasPressedThisFrame) Hucum();
            if (k.xKey.wasPressedThisFrame) Dur();
            if (k.tabKey.wasPressedThisFrame) TumunuSec();
        }

        bool arayuzde = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        Vector2 fp = m.position.ReadValue();

        // Seçili alay varken fare düşmanın üzerindeyse imleç kılıca döner
        bool dusmanUstunde = false;
        if (secili.Count > 0 && !arayuzde)
        {
            Alay alt = FaredekiAlay(fp, out _);
            dusmanUstunde = alt != null && !alt.oyuncunun && alt.Savasabilir;
        }
        KilicImleci(dusmanUstunde && !dizilis);

        // Sol tık: seç, sürükle: kutuyla seç
        if (m.leftButton.wasPressedThisFrame && !arayuzde) { surukleBaslangic = fp; surukleniyor = true; }
        if (surukleniyor)
        {
            Vector2 min = Vector2.Min(surukleBaslangic, fp), max = Vector2.Max(surukleBaslangic, fp);
            bool kutu = (max - min).magnitude > 12f;
            if (secimKutusu != null)
            {
                secimKutusu.gameObject.SetActive(kutu);
                float o = savasCanvas.scaleFactor;
                secimKutusu.anchoredPosition = min / o;
                secimKutusu.sizeDelta = (max - min) / o;
            }
            if (m.leftButton.wasReleasedThisFrame)
            {
                surukleniyor = false;
                if (secimKutusu != null) secimKutusu.gameObject.SetActive(false);
                bool ekle = k != null && (k.leftShiftKey.isPressed || k.rightShiftKey.isPressed);
                if (!ekle) SecimiTemizle();
                if (kutu)
                {
                    foreach (Alay a in alaylar)
                    {
                        if (!a.oyuncunun || !a.Savasabilir) continue;
                        Vector3 e = kam.WorldToScreenPoint(a.transform.position);
                        if (e.z > 0 && e.x >= min.x && e.x <= max.x && e.y >= min.y && e.y <= max.y) Sec(a);
                    }
                }
                else
                {
                    Alay a = FaredekiAlay(fp, out _);
                    if (a != null && a.oyuncunun && a.Savasabilir)
                    {
                        if (ekle && secili.Contains(a)) { secili.Remove(a); a.SecimGoster(false); }
                        else Sec(a);
                    }
                }
            }
        }

        // Sağ tık: düşmana saldır (iki kez: hücum) ya da yere bas, sürükleyerek yön ver, bırakınca yürü
        if (m.rightButton.wasPressedThisFrame && !arayuzde && secili.Count > 0)
        {
            Alay a = FaredekiAlay(fp, out Vector3 nokta);
            bool cift = Time.unscaledTime - sonSagTik < 0.4f;
            sonSagTik = Time.unscaledTime;
            if (!dizilis && a != null && !a.oyuncunun && a.Savasabilir)
            {
                bool hucum = cift && sonSagHedef == a;
                foreach (Alay s in secili) { s.hedefAlay = a; s.hucum = hucum && s.tur != Alay.Tur.Topcu && s.tur != Alay.Tur.Makineli; s.sonBakis = Vector3.zero; }
                sonSagHedef = a;
            }
            else if (nokta != Vector3.zero)
            {
                sagSurukle = true; sagBaslangic = nokta; sagCift = cift;
                sonSagHedef = null;
            }
        }
        if (sagSurukle)
        {
            Vector3 yon = SurukleYonu(fp);
            OnizlemeCiz(yon);
            if (!m.rightButton.isPressed)
            {
                sagSurukle = false;
                OnizlemeGizle();
                if (secili.Count > 0) YuruyusEmri(sagBaslangic, sagCift, yon);
            }
        }
    }

    Alay FaredekiAlay(Vector2 ekran, out Vector3 nokta)
    {
        nokta = Vector3.zero;
        Ray isin = kam.ScreenPointToRay(ekran);
        RaycastHit[] vuruslar = Physics.RaycastAll(isin, 800f);
        Alay en = null; float enYakin = float.MaxValue;
        foreach (RaycastHit v in vuruslar)
        {
            if (kok == null || !v.transform.IsChildOf(kok.transform)) continue;
            Alay a = v.collider.GetComponent<Alay>();
            if (a != null) { if (v.distance < enYakin) { enYakin = v.distance; en = a; } }
            else if (nokta == Vector3.zero || v.distance < Vector3.Distance(isin.origin, nokta)) nokta = v.point;
        }
        return en;
    }

    // Fareyle sürüklenen yön (yeterince sürüklenmediyse sıfır)
    Vector3 SurukleYonu(Vector2 fp)
    {
        Ray isin = kam.ScreenPointToRay(fp);
        Plane duz = new Plane(Vector3.up, sagBaslangic);
        if (!duz.Raycast(isin, out float t)) return Vector3.zero;
        Vector3 f = isin.GetPoint(t) - sagBaslangic; f.y = 0f;
        return f.magnitude > 2f ? f.normalized : Vector3.zero;
    }

    // Seçili birliklerin hattaki yerleri: yöne dik, genişliklerine göre yan yana
    List<KeyValuePair<Alay, Vector3>> HatYerleri(Vector3 nokta, Vector3 yon)
    {
        Vector3 yan = Vector3.Cross(Vector3.up, yon);
        List<Alay> sirali = new List<Alay>(secili);
        sirali.Sort((p, q) => Vector3.Dot(p.transform.position, yan).CompareTo(Vector3.Dot(q.transform.position, yan)));
        float toplam = -1.2f;
        foreach (Alay a in sirali) toplam += a.Genislik + 1.2f;
        float x = -toplam / 2f;
        List<KeyValuePair<Alay, Vector3>> sonuc = new List<KeyValuePair<Alay, Vector3>>();
        foreach (Alay a in sirali)
        {
            Vector3 yer = nokta + yan * (x + a.Genislik / 2f);
            x += a.Genislik + 1.2f;
            if (dizilis) yer = DizilisIcinde(yer, a);
            else
            {
                Vector3 yerel = yer - MuharebeAlani.Merkez;
                float s = MuharebeAlani.Yari - 3f;
                yer = MuharebeAlani.Uzerinde(MuharebeAlani.Merkez + new Vector3(Mathf.Clamp(yerel.x, -s, s), 0f, Mathf.Clamp(yerel.z, -s, s)));
            }
            sonuc.Add(new KeyValuePair<Alay, Vector3>(a, yer));
        }
        return sonuc;
    }

    // Seçili birlikler tıklanan yere bir hat halinde gider; yön verildiyse varınca o yöne döner
    void YuruyusEmri(Vector3 nokta, bool kos, Vector3 bakis)
    {
        Vector3 orta = Vector3.zero;
        foreach (Alay a in secili) orta += a.transform.position;
        orta /= secili.Count;
        Vector3 yon = bakis;
        if (yon == Vector3.zero)
        {
            if (dizilis) yon = oyuncuSaldiriyor ? Vector3.forward : Vector3.back;
            else
            {
                yon = nokta - orta; yon.y = 0f;
                yon = yon.sqrMagnitude < 0.01f ? secili[0].transform.forward : yon.normalized;
            }
        }
        foreach (var hy in HatYerleri(nokta, yon))
        {
            Alay a = hy.Key;
            a.hedefAlay = null;
            a.hedefNokta = hy.Value;
            a.sonBakis = bakis;
            if (dizilis)
            {
                // Dizilişte anında yerleşir
                a.transform.position = hy.Value;
                a.transform.rotation = Quaternion.LookRotation(yon);
                a.mevzi = hy.Value;
                a.hucum = false;
            }
            else a.hucum = kos;
        }
    }

    // Sağ tık sürüklenirken: yön oku ve birliklerin yerleşeceği yerlerin hayaleti
    void OnizlemeCiz(Vector3 yon)
    {
        if (yon == Vector3.zero) { OnizlemeGizle(); return; }
        if (yonOku == null) yonOku = YeniCizgi("YonOku", 0.35f);
        yonOku.enabled = true;
        Vector3 bas = sagBaslangic, son = sagBaslangic + yon * 6f;
        yonOku.loop = false;
        yonOku.positionCount = 8;
        for (int i = 0; i < 8; i++) yonOku.SetPosition(i, MuharebeAlani.Uzerinde(Vector3.Lerp(bas, son, i / 7f), 0.3f));
        yonOku.widthCurve = new AnimationCurve(new Keyframe(0f, 0.3f), new Keyframe(0.7f, 0.3f), new Keyframe(0.701f, 1.0f), new Keyframe(1f, 0f));

        List<KeyValuePair<Alay, Vector3>> yerler = HatYerleri(sagBaslangic, yon);
        while (hayaletler.Count < yerler.Count) hayaletler.Add(YeniCizgi("Hayalet", 0.15f));
        Vector3 yan = Vector3.Cross(Vector3.up, yon);
        for (int i = 0; i < hayaletler.Count; i++)
        {
            LineRenderer h = hayaletler[i];
            if (i >= yerler.Count) { h.enabled = false; continue; }
            Alay a = yerler[i].Key; Vector3 c = yerler[i].Value;
            Vector3 sg = yan * (a.Genislik * 0.5f + 0.3f), il = yon * (a.Derinlik * 0.5f + 0.4f);
            h.enabled = true;
            h.loop = true;
            h.positionCount = 4;
            h.SetPosition(0, MuharebeAlani.Uzerinde(c + sg + il, 0.12f));
            h.SetPosition(1, MuharebeAlani.Uzerinde(c + sg - il, 0.12f));
            h.SetPosition(2, MuharebeAlani.Uzerinde(c - sg - il, 0.12f));
            h.SetPosition(3, MuharebeAlani.Uzerinde(c - sg + il, 0.12f));
        }
    }

    void OnizlemeGizle()
    {
        if (yonOku != null) yonOku.enabled = false;
        foreach (LineRenderer h in hayaletler) if (h != null) h.enabled = false;
    }

    LineRenderer YeniCizgi(string ad, float kalinlik)
    {
        GameObject g = new GameObject(ad);
        g.transform.SetParent(kok.transform, false);
        LineRenderer lr = g.AddComponent<LineRenderer>();
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.useWorldSpace = true;
        lr.widthMultiplier = kalinlik;
        lr.startColor = lr.endColor = new Color(1f, 0.85f, 0.35f, 0.75f);
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.enabled = false;
        return lr;
    }

    void Sec(Alay a) { if (!secili.Contains(a)) secili.Add(a); a.SecimGoster(true); }

    void SecimiTemizle()
    {
        foreach (Alay a in secili) if (a != null) a.SecimGoster(false);
        secili.Clear();
    }

    void TumunuSec()
    {
        SecimiTemizle();
        foreach (Alay a in alaylar) if (a.oyuncunun && a.Savasabilir) Sec(a);
    }

    void Hucum()
    {
        foreach (Alay a in secili)
        {
            if (a.tur == Alay.Tur.Topcu || a.tur == Alay.Tur.Makineli) continue;   // ağır silahlar hücum etmez
            if (a.hedefAlay == null || !a.hedefAlay.Savasabilir) a.hedefAlay = EnYakinDusman(a, out _);
            a.hucum = a.hedefAlay != null;
        }
    }

    void Dur()
    {
        foreach (Alay a in secili) { a.hedefAlay = null; a.hucum = false; a.hedefNokta = a.transform.position; }
    }

    void DuraklatDevam()
    {
        savasDuraklatildi = !savasDuraklatildi;
        Time.timeScale = savasDuraklatildi ? 0f : savasHizi;
    }

    void HizDegistir()
    {
        savasHizi = savasHizi >= 3 ? 1 : savasHizi + 1;
        if (!savasDuraklatildi) Time.timeScale = savasHizi;
    }

    void GeriCekil()
    {
        if (bitti) return;
        foreach (Alay a in alaylar)
            if (a.oyuncunun && a.Savasabilir) { a.Bozgun(); a.duzenliCekildi = true; }
        SecimiTemizle();
        Bitir(!oyuncuSaldiriyor);    // çekilen taraf muharebeyi kaybeder
    }

    // Kalan kısmı güç ve şansla hesapla
    void OtomatikBitir()
    {
        if (bitti) return;
        float s = 0f, d = 0f;
        foreach (Alay a in alaylar)
        {
            if (!a.Savasabilir) continue;
            float g = a.adam * (0.5f + a.moral / 200f) * a.verim;
            if (a.savunan) d += g * a.siper; else s += g;
        }
        bool kazandi = s * Random.Range(0.85f, 1.15f) > d;
        float oran = Mathf.Min(s, d) / Mathf.Max(1f, Mathf.Max(s, d));
        foreach (Alay a in alaylar)
        {
            if (!a.Savasabilir) continue;
            bool kazanan = a.savunan != kazandi;
            a.adam *= kazanan ? 1f - 0.15f * oran : 0.7f;
            if (!kazanan) a.Bozgun();
        }
        Bitir(kazandi);
    }

    void BitisKontrol()
    {
        if (bitti) return;
        bool sVar = false, dVar = false;
        foreach (Alay a in alaylar)
        {
            if (!a.Savasabilir) continue;
            if (a.savunan) dVar = true; else sVar = true;
        }
        if (!dVar) Bitir(true);
        else if (!sVar) Bitir(false);
        else if (sure >= SureSiniri) Bitir(false);    // saldıran vaktinde sonuç alamadı
    }

    // ---------- Sonuç ----------

    void Bitir(bool saldiranKazandi)
    {
        bitti = true;
        Time.timeScale = 0f;
        SecimiTemizle();
        KilicImleci(false);

        float kalanS = 0f, kalanD = 0f;
        foreach (Alay a in alaylar)
        {
            float k = a.Savasabilir ? a.adam : a.adam * (a.duzenliCekildi ? 0.9f : 0.6f);
            if (a.savunan) kalanD += k; else kalanS += k;
        }
        int sKayip = Mathf.Clamp(saldiran.askerSayisi - Mathf.RoundToInt(kalanS), 0, saldiran.askerSayisi);
        int dKayip = Mathf.Clamp(savunan.askerSayisi - Mathf.RoundToInt(kalanD), 0, savunan.askerSayisi);
        int bizimKayip = oyuncuSaldiriyor ? sKayip : dKayip, onlarinKayip = oyuncuSaldiriyor ? dKayip : sKayip;

        string baslik = "<b>Meydan muharebesi: " + hedef.sancakAdi + "</b>  <size=80%>(bizzat komuta)</size>";
        string rapor = Savas.SonucUygula(saldiran, savunan, hedef, saldiranKazandi, sKayip, dKayip, baslik);
        saldiran.MuharebeBitti(hedef);
        Gunluk.Ekle(rapor.Replace("\n", " "));

        bool zafer = saldiranKazandi == oyuncuSaldiriyor;
        RectTransform p = Panel(savasCanvas.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000f, 560f),
                                new Color(0.10f, 0.08f, 0.06f, 0.95f));
        TMP_Text t = Yazi(p, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(940f, 400f), 28f, TextAlignmentOptions.Top);
        t.text = (zafer ? "<size=200%><color=#9f9><b>ZAFER</b></color></size>" : "<size=200%><color=#f88><b>YENİLGİ</b></color></size>")
               + "\n\nKayıplarımız: <b>" + bizimKayip + "</b>   ·   Düşman kayıpları: <b>" + onlarinKayip + "</b>\n\n"
               + "<size=85%>" + rapor + "</size>";
        Dugme(p, "Haritaya Dön", new Vector2(0.5f, 0f), new Vector2(0f, 70f), new Vector2(380f, 84f), Kapat, 32f);
    }

    // ---------- Savaş öncesi diziliş ----------

    // Oyuncunun birliklerini yerleştirebileceği alan (yerel z aralığı)
    float DizilisMinZ { get { return oyuncuSaldiriyor ? -MuharebeAlani.Yari + 2f : 2f; } }
    float DizilisMaxZ { get { return oyuncuSaldiriyor ? -12f : MuharebeAlani.Yari - 2f; } }

    Vector3 DizilisIcinde(Vector3 p, Alay a)
    {
        Vector3 yerel = p - MuharebeAlani.Merkez;
        float px = MuharebeAlani.Yari - 2f - a.Genislik * 0.5f, pz = a.Derinlik * 0.5f + 0.5f;
        yerel.x = Mathf.Clamp(yerel.x, -px, px);
        yerel.z = Mathf.Clamp(yerel.z, DizilisMinZ + pz, DizilisMaxZ - pz);
        return MuharebeAlani.Uzerinde(MuharebeAlani.Merkez + new Vector3(yerel.x, 0f, yerel.z));
    }

    void DizilisBaslat()
    {
        dizilis = true;
        // Yeşil diziliş alanı
        GameObject g = new GameObject("DizilisAlani");
        g.transform.SetParent(kok.transform, false);
        dizilisCizgisi = g.AddComponent<LineRenderer>();
        dizilisCizgisi.material = new Material(Shader.Find("Sprites/Default"));
        dizilisCizgisi.useWorldSpace = true;
        dizilisCizgisi.loop = true;
        dizilisCizgisi.widthMultiplier = 0.3f;
        dizilisCizgisi.startColor = dizilisCizgisi.endColor = new Color(0.45f, 1f, 0.45f, 0.7f);
        dizilisCizgisi.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        float x = MuharebeAlani.Yari - 2f;
        Vector2[] koseler = { new Vector2(-x, DizilisMinZ), new Vector2(x, DizilisMinZ), new Vector2(x, DizilisMaxZ), new Vector2(-x, DizilisMaxZ) };
        List<Vector3> n = new List<Vector3>();
        for (int k = 0; k < 4; k++)
        {
            Vector2 a = koseler[k], b = koseler[(k + 1) % 4];
            int adim = Mathf.Max(2, Mathf.CeilToInt(Vector2.Distance(a, b) / 1.5f));
            for (int i = 0; i < adim; i++)
            {
                Vector2 q = Vector2.Lerp(a, b, i / (float)adim);
                n.Add(MuharebeAlani.Uzerinde(MuharebeAlani.Merkez + new Vector3(q.x, 0f, q.y), 0.2f));
            }
        }
        dizilisCizgisi.positionCount = n.Count;
        dizilisCizgisi.SetPositions(n.ToArray());

        baslatDugmesi = Dugme(savasCanvas.transform, "Savaşı Başlat\n<size=60%>(Enter)</size>", new Vector2(0.5f, 0f), new Vector2(0f, 160f),
                              new Vector2(400f, 96f), SavasiBaslat, 34f).gameObject;
        baslatDugmesi.GetComponent<Image>().color = new Color(0.45f, 0.18f, 0.12f, 0.97f);
    }

    void SavasiBaslat()
    {
        if (!dizilis || bitti) return;
        dizilis = false;
        if (baslatDugmesi != null) Destroy(baslatDugmesi);
        if (dizilisCizgisi != null) Destroy(dizilisCizgisi.gameObject);
        foreach (Alay a in alaylar)
        {
            a.hedefNokta = a.transform.position;
            a.mevzi = a.transform.position;
            a.duranSure = 10f;
        }
        Mesaj("<color=#fc8><b>Savaş başladı!</b></color>");
    }

    // ---------- Kılıç imleci ----------

    static Texture2D kilicDokusu;
    bool kilicAcik;

    void KilicImleci(bool ac)
    {
        if (ac == kilicAcik) return;
        kilicAcik = ac;
        if (ac) Cursor.SetCursor(KilicDokusu(), new Vector2(2f, 2f), CursorMode.Auto);
        else Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
    }

    // 32x32 çapraz kılıç: ucu sol üstte (imlecin tıklayan noktası)
    static Texture2D KilicDokusu()
    {
        if (kilicDokusu != null) return kilicDokusu;
        const int B = 32;
        Texture2D t = new Texture2D(B, B, TextureFormat.RGBA32, false);
        t.filterMode = FilterMode.Point;
        Color bos = new Color(0f, 0f, 0f, 0f);
        Color[] p = new Color[B * B];
        for (int i = 0; i < p.Length; i++) p[i] = bos;
        Color siyah = new Color(0.05f, 0.04f, 0.03f, 1f), gumus = new Color(0.88f, 0.9f, 0.94f, 1f),
              altin = new Color(0.85f, 0.65f, 0.2f, 1f), kahve = new Color(0.4f, 0.22f, 0.1f, 1f);
        // Önce kalın siyah dış çizgi, sonra renkler
        Cizgi(p, B, 2, 2, 21, 21, 2.4f, siyah); Cizgi(p, B, 14, 25, 25, 14, 2.4f, siyah);
        Cizgi(p, B, 21, 21, 28, 28, 2.4f, siyah);
        Cizgi(p, B, 2, 2, 21, 21, 1.2f, gumus); Cizgi(p, B, 14, 25, 25, 14, 1.2f, altin);
        Cizgi(p, B, 21, 21, 27, 27, 1.1f, kahve); Cizgi(p, B, 28, 28, 28.5f, 28.5f, 1.4f, altin);
        t.SetPixels(p);
        t.Apply();
        kilicDokusu = t;
        return t;
    }

    // Sol üst köşeye göre (x sağa, y aşağı) kalın bir çizgi çizer
    static void Cizgi(Color[] p, int B, float x0, float y0, float x1, float y1, float kalinlik, Color c)
    {
        Vector2 a = new Vector2(x0, y0), b = new Vector2(x1, y1), ab = b - a;
        for (int y = 0; y < B; y++)
            for (int x = 0; x < B; x++)
            {
                Vector2 q = new Vector2(x + 0.5f, y + 0.5f);
                float u = ab.sqrMagnitude < 0.001f ? 0f : Mathf.Clamp01(Vector2.Dot(q - a, ab) / ab.sqrMagnitude);
                if (Vector2.Distance(q, a + ab * u) <= kalinlik) p[(B - 1 - y) * B + x] = c;
            }
    }

    void Kapat()
    {
        KilicImleci(false);
        Time.timeScale = 1f;
        if (kok != null) Destroy(kok);
        if (savasCanvas != null) Destroy(savasCanvas.gameObject);
        if (savasKamerasi != null) Destroy(savasKamerasi);
        if (kam != null)
        {
            kam.transform.position = kamYer; kam.transform.rotation = kamDonus; kam.farClipPlane = kamUzak;
        }
        if (haritaKamerasi != null) haritaKamerasi.enabled = true;
        RenderSettings.fogDensity = sisYogunluk;
        if (haritaCanvas != null) haritaCanvas.gameObject.SetActive(true);
        alaylar.Clear(); secili.Clear();
        aktif = false;
        HaritayiYenile();
        SiradakiniAc();
    }

    // ---------- Savaş arayüzü ----------

    void SavasArayuzunuKur()
    {
        GameObject c = new GameObject("SavasArayuzu", typeof(RectTransform));
        savasCanvas = c.AddComponent<Canvas>();
        savasCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        savasCanvas.sortingOrder = 10;
        CanvasScaler olcek = c.AddComponent<CanvasScaler>();
        olcek.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        olcek.referenceResolution = new Vector2(1920f, 1080f);
        olcek.matchWidthOrHeight = 0.5f;
        c.AddComponent<GraphicRaycaster>();
        c.transform.SetParent(kok.transform, false);

        ArayuzYardimci.UstSeritOlustur(savasCanvas, 90f);
        ustYazi = Yazi(c.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(1800f, 80f), 30f, TextAlignmentOptions.Center);
        mesajYazisi = Yazi(c.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -100f), new Vector2(1600f, 60f), 32f, TextAlignmentOptions.Center);
        mesajYazisi.outlineWidth = 0.2f;
        mesajSure = 0f;

        float y = 50f;
        Dugme(c.transform, "Hücum!\n<size=60%>(H)</size>", new Vector2(0.5f, 0f), new Vector2(-575f, y), new Vector2(210f, 80f), Hucum);
        Dugme(c.transform, "Dur\n<size=60%>(X)</size>", new Vector2(0.5f, 0f), new Vector2(-345f, y), new Vector2(210f, 80f), Dur);
        Dugme(c.transform, "Tümünü Seç\n<size=60%>(Tab)</size>", new Vector2(0.5f, 0f), new Vector2(-115f, y), new Vector2(210f, 80f), TumunuSec);
        Button hiz = Dugme(c.transform, "Hız x1", new Vector2(0.5f, 0f), new Vector2(115f, y), new Vector2(210f, 80f), HizDegistir);
        hizYazisi = hiz.GetComponentInChildren<TMP_Text>();
        Dugme(c.transform, "Geri Çekil", new Vector2(0.5f, 0f), new Vector2(345f, y), new Vector2(210f, 80f), GeriCekil);
        Dugme(c.transform, "Otomatik Bitir", new Vector2(0.5f, 0f), new Vector2(575f, y), new Vector2(210f, 80f), OtomatikBitir);

        seciliYazi = Yazi(c.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(24f, 110f), new Vector2(560f, 200f), 24f, TextAlignmentOptions.BottomLeft);
        TMP_Text yardim = Yazi(c.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-24f, 110f), new Vector2(600f, 260f), 20f, TextAlignmentOptions.BottomRight);
        yardim.text = "<color=#ccc>Sol tık: alay seç  ·  sürükle: kutu ile seç  ·  Shift: ekle\n"
                    + "Sağ tık: yürü / düşmana saldır  ·  çift sağ tık: koş / hücum\n"
                    + "Sağ tıkı basılı tutup sürükle: birliğin bakacağı yönü belirle\n"
                    + "WASD: kaydır  ·  Q/E ya da orta tuş: döndür  ·  tekerlek: yakınlaş\n"
                    + "Boşluk: duraklat  ·  Yandan ve arkadan vurmak çok daha etkili\n"
                    + "Topçu ve makineli durduktan sonra ateş eder  ·  Süvari yandan dalınca yıkıcıdır\n"
                    + "Karargâhın yakınındaki birliklerin morali daha sağlamdır; karargâhı koru!</color>";

        GameObject kutu = new GameObject("SecimKutusu", typeof(RectTransform), typeof(Image));
        kutu.transform.SetParent(c.transform, false);
        secimKutusu = kutu.GetComponent<RectTransform>();
        secimKutusu.anchorMin = secimKutusu.anchorMax = Vector2.zero;
        secimKutusu.pivot = Vector2.zero;
        Image ki = kutu.GetComponent<Image>();
        ki.color = new Color(1f, 0.85f, 0.3f, 0.18f);
        ki.raycastTarget = false;
        kutu.SetActive(false);
    }

    void ArayuzuGuncelle()
    {
        int bizAdam = 0, bizAlay = 0, onlarAdam = 0, onlarAlay = 0;
        foreach (Alay a in alaylar)
        {
            if (!a.Savasabilir) continue;
            if (a.oyuncunun) { bizAdam += a.GorunenAdam; bizAlay++; } else { onlarAdam += a.GorunenAdam; onlarAlay++; }
        }
        int kalan = Mathf.Max(0, Mathf.CeilToInt(SureSiniri - sure));
        string durum = savasDuraklatildi ? "<color=#fc8>DURAKLATILDI</color>" : "Hız x" + savasHizi;
        ustYazi.text = "<b>" + hedef.sancakAdi + " Muharebesi</b>   |   <color=#f99>Biz: " + bizAdam + " (" + bizAlay + " birlik)</color>"
                     + "   |   <color=#9bf>" + TarafBilgi.Ad(oyuncuSaldiriyor ? savunan.taraf : saldiran.taraf) + ": " + onlarAdam + " (" + onlarAlay + " birlik)</color>"
                     + "   |   " + (oyuncuSaldiriyor ? "Saldırı süresi " : "Dayanılacak süre ") + (kalan / 60) + ":" + (kalan % 60).ToString("00")
                     + "   |   " + durum;
        if (dizilis)
            ustYazi.text = "<b>" + hedef.sancakAdi + " Muharebesi</b>   |   <color=#fc8><b>DİZİLİŞ</b></color>: birliklerini seç, sağ tıkla yeşil alanda yerleştir"
                         + "   |   hazır olunca <b>Savaşı Başlat</b> (Enter)";
        if (hizYazisi != null) hizYazisi.text = "Hız x" + savasHizi;

        if (secili.Count == 0) { seciliYazi.text = "<color=#ccc>Alay seçilmedi.</color>"; return; }
        int adam = 0; float moral = 0f, cephane = 0f;
        foreach (Alay a in secili) { adam += a.GorunenAdam; moral += a.moral; cephane += a.cephane / a.oz.cephane; }
        string baslik = secili.Count == 1 ? secili[0].ad + "  <size=80%><color=#ccc>(" + secili[0].oz.tip + ", menzil " + Mathf.RoundToInt(secili[0].Menzil) + ")</color></size>"
                                          : secili.Count + " birlik seçili";
        seciliYazi.text = "<b>" + baslik + "</b>\n"
                        + adam + " asker  ·  moral " + Mathf.RoundToInt(moral / secili.Count)
                        + "  ·  cephane %" + Mathf.RoundToInt(cephane / secili.Count * 100f);
    }

    // ---------- Arayüz yardımcıları ----------

    static GameObject TamEkranKarartma(Transform ebeveyn)
    {
        GameObject g = new GameObject("MuharebeKarari", typeof(RectTransform), typeof(Image));
        g.transform.SetParent(ebeveyn, false);
        RectTransform r = g.GetComponent<RectTransform>();
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
        g.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);
        return g;
    }

    static RectTransform Panel(Transform ebeveyn, Vector2 capa, Vector2 yer, Vector2 boyut, Color renk)
    {
        GameObject g = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        g.transform.SetParent(ebeveyn, false);
        RectTransform r = g.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = capa; r.pivot = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = yer; r.sizeDelta = boyut;
        g.GetComponent<Image>().color = renk;
        return r;
    }

    static TMP_Text Yazi(Transform ebeveyn, Vector2 capa, Vector2 pivot, Vector2 yer, Vector2 boyut, float font, TextAlignmentOptions hiza)
    {
        GameObject g = new GameObject("Yazi", typeof(RectTransform));
        g.transform.SetParent(ebeveyn, false);
        TextMeshProUGUI t = g.AddComponent<TextMeshProUGUI>();
        RectTransform r = t.rectTransform;
        r.anchorMin = r.anchorMax = capa; r.pivot = pivot;
        r.anchoredPosition = yer; r.sizeDelta = boyut;
        t.fontSize = font;
        t.alignment = hiza;
        t.color = ArayuzYardimci.DugmeYazisi;
        t.raycastTarget = false;
        return t;
    }

    static Button Dugme(Transform ebeveyn, string yazi, Vector2 capa, Vector2 yer, Vector2 boyut, UnityAction islem, float font = 26f)
    {
        GameObject d = TMP_DefaultControls.CreateButton(new TMP_DefaultControls.Resources());
        d.transform.SetParent(ebeveyn, false);
        RectTransform r = d.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = capa; r.pivot = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = yer; r.sizeDelta = boyut;
        TMP_Text t = d.GetComponentInChildren<TMP_Text>();
        t.text = yazi; t.fontSize = font;
        Button b = d.GetComponent<Button>();
        b.onClick.AddListener(islem);
        ArayuzYardimci.DugmeStili(b);
        return b;
    }
}
