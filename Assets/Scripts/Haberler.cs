using System;
using System.Collections.Generic;
using UnityEngine;

// Haber ve gazete sistemi: tarihî haberler ve sivillerin yaşadığı olaylar (Türk tarafının bakış açısından).
// İlke: oyuncu sivillere zarar veremez; siviller yalnızca haber olarak görünür ve öfke, gönüllü katılımı,
// moral gibi etkiler doğurur.
public static class Haberler
{
    class TarihiHaber
    {
        public DateTime tarih;
        public string baslik, metin, etki;
        public Action uygula;
    }

    static readonly List<TarihiHaber> tarihiler = new List<TarihiHaber>();
    static readonly HashSet<TarihiHaber> verilen = new HashSet<TarihiHaber>();
    static readonly Dictionary<Taraf, float> takviyeCarpani = new Dictionary<Taraf, float>();
    static int gun, sonSivilHaberGunu = -999;
    public static DateTime Tarih;

    public static float TakviyeCarpani(Taraf t) { return takviyeCarpani.TryGetValue(t, out float c) ? c : 1f; }

    public static void Sifirla()
    {
        tarihiler.Clear(); verilen.Clear(); takviyeCarpani.Clear();
        gun = 0; sonSivilHaberGunu = -999;

        Ekle(1919, 6, 22, "Amasya Genelgesi yayımlandı",
             "Mustafa Kemal Paşa Amasya'dan bütün ülkeye seslendi: \"Milletin istiklâlini yine milletin azim ve kararı kurtaracaktır.\" Sivas'ta bir kongre toplanması kararlaştırıldı.",
             "Yetki +5", () => Meclis.YetkiDegistir(5f));
        Ekle(1920, 3, 16, "İstanbul resmen işgal edildi",
             "İtilaf kuvvetleri İstanbul'u resmen işgal etti, Meclis-i Mebusan basıldı ve mebuslar tutuklandı. Pek çok aydın ve subay Anadolu'ya geçmenin yollarını arıyor.",
             "Yetki +5, Ankara'daki orduya 500 gönüllü, Muhafazakârların güveni +5",
             () => { Meclis.YetkiDegistir(5f); GonulluEkle(Sancak.Bul("Ankara"), 500); MeclisGuven(1, 5f); });
        Ekle(1920, 4, 6, "Anadolu Ajansı kuruldu",
             "Halide Edip ve Yunus Nadi'nin öncülüğünde kurulan Anadolu Ajansı, Millî Mücadele'nin sesini bütün yurda telgrafla duyurmaya başladı.",
             "Bütün ordularda moral +3", () => TurkMorali(3));
        Ekle(1920, 4, 11, "Şeyhülislam fetvası",
             "İstanbul hükümetinin Şeyhülislamı Millî Mücadele'ye katılanları asi ilan eden bir fetva yayımladı. Anadolu'da müftüler karşı fetvalarla cevap veriyor, ama kafalar karışık.",
             "Yetki −3, Muhafazakârların güveni −10", () => { Meclis.YetkiDegistir(-3f); MeclisGuven(1, -10f); });
        Ekle(1921, 3, 12, "İstiklal Marşı kabul edildi",
             "Mehmet Akif'in yazdığı İstiklal Marşı Büyük Millet Meclisi'nde ayakta alkışlarla kabul edildi.",
             "Bütün ordularda moral +5, Yetki +2", () => { TurkMorali(5); Meclis.YetkiDegistir(2f); });
        Ekle(1921, 5, 23, "Yalova-Gemlik raporu yayımlandı",
             "İtilaf devletlerinin soruşturma heyeti, Yalova ve Gemlik yarımadasında Türk köylerinin yakıldığını ve halka karşı işlenen suçları raporuna geçirdi. Avrupa kamuoyunda Yunan harekâtına destek azalıyor.",
             "İngiliz ve Fransız takviyeleri %25 azalır, Yetki +2",
             () => { takviyeCarpani[Taraf.Ingiliz] = 0.75f; takviyeCarpani[Taraf.Fransiz] = 0.75f; Meclis.YetkiDegistir(2f); });
    }

    static void Ekle(int y, int a, int g, string baslik, string metin, string etki, Action uygula)
    {
        tarihiler.Add(new TarihiHaber { tarih = new DateTime(y, a, g), baslik = baslik, metin = metin, etki = etki, uygula = uygula });
    }

    // ---------- Gün gün ----------

    public static void GunIlerle(DateTime tarih)
    {
        Tarih = tarih;
        gun++;
        foreach (TarihiHaber h in tarihiler)
        {
            if (verilen.Contains(h) || tarih < h.tarih) continue;
            verilen.Add(h);
            h.uygula?.Invoke();
            Yayinla(h.baslik, h.metin, h.etki);
        }
        if (gun % 7 == 0) SivilHaberleri();
    }

    // Haftada bir: işgal altındaki bölgelerden haberler
    static void SivilHaberleri()
    {
        if (gun - sonSivilHaberGunu < 21) return;   // haber yağmuru olmasın
        List<Sancak> yunanda = new List<Sancak>(), ermeniYakini = new List<Sancak>();
        foreach (Sancak s in UnityEngine.Object.FindObjectsByType<Sancak>(FindObjectsSortMode.None))
        {
            if (s.sahip == Taraf.Yunan) yunanda.Add(s);
            if (s.sahip == Taraf.Turk)
                foreach (Sancak k in s.komsular) if (k.sahip == Taraf.Ermeni) { ermeniYakini.Add(s); break; }
        }

        if (yunanda.Count > 0 && UnityEngine.Random.value < 0.03f * yunanda.Count)
        {
            Sancak s = yunanda[UnityEngine.Random.Range(0, yunanda.Count)];
            int gonullu = UnityEngine.Random.Range(300, 801);
            string ordu = GonulluEkle(s, gonullu);
            BolgeMorali(s, 5);
            Yayinla("Yunan işgalindeki " + s.sancakAdi + "'da köyler yakıldı",
                    s.sancakAdi + " civarından kaçabilen köylüler yaşananları anlatıyor: evleri yakılan aileler dağlara sığınmış durumda. "
                    + "Haber yayıldıkça öfke büyüyor; gençler cepheye koşuyor. Bölge ne kadar çabuk kurtarılırsa o kadar az köy yanacak.",
                    gonullu + " gönüllü" + (ordu != null ? " (" + ordu + ")" : "") + ", yakındaki ordularda moral +5");
            sonSivilHaberGunu = gun;
            return;
        }
        if (ermeniYakini.Count > 0 && UnityEngine.Random.value < 0.08f * ermeniYakini.Count)
        {
            Sancak s = ermeniYakini[UnityEngine.Random.Range(0, ermeniYakini.Count)];
            int gonullu = UnityEngine.Random.Range(200, 501);
            string ordu = GonulluEkle(s, gonullu);
            BolgeMorali(s, 4);
            Yayinla("Ermeni çeteleri " + s.sancakAdi + " köylerine baskın yaptı",
                    "Sınır boyundaki silahlı Ermeni grupları " + s.sancakAdi + " civarında Türk köylerine saldırdı. Köylüler yardım bekliyor; "
                    + "Doğu cephesinden müdahale çağrıları artıyor.",
                    gonullu + " gönüllü" + (ordu != null ? " (" + ordu + ")" : "") + ", yakındaki ordularda moral +4");
            sonSivilHaberGunu = gun;
        }
    }

    // Bir sancak sahip değiştirdiğinde (Sancak.SahipDegistir çağırır)
    public static void SahipDegisti(Sancak s, Taraf eski, Taraf yeni)
    {
        if (gun == 0) return;   // harita kurulurken değil
        if (eski == Taraf.Yunan && yeni == Taraf.Turk)
        {
            bool yakildi = UnityEngine.Random.value < 0.5f;
            if (yakildi)
            {
                int gonullu = UnityEngine.Random.Range(400, 1001);
                string ordu = GonulluEkle(s, gonullu);
                BolgeMorali(s, 6);
                Yayinla(s.sancakAdi + " kurtarıldı, ama yanmış köylerle",
                        "Ordumuz " + s.sancakAdi + "'ya girdi. Çekilen Yunan askerleri geride yanmış köyler ve yıkılmış kasabalar bıraktı. "
                        + "Halk askerimizi gözyaşlarıyla karşıladı; pek çok genç orduya katılıyor.",
                        gonullu + " gönüllü" + (ordu != null ? " (" + ordu + ")" : "") + ", yakındaki ordularda moral +6");
            }
            else
            {
                BolgeMorali(s, 4);
                Yayinla(s.sancakAdi + " düşman işgalinden kurtarıldı",
                        "Ordumuz " + s.sancakAdi + "'ya girdi. Halk sokaklara döküldü, camilerde şükür namazları kılındı.",
                        "Yakındaki ordularda moral +4");
            }
        }
        else if (eski == Taraf.Turk && yeni != Taraf.Turk)
        {
            Yayinla(s.sancakAdi + " düşman eline geçti",
                    s.sancakAdi + " " + TarafBilgi.Ad(yeni) + " kuvvetlerince işgal edildi. Halktan kaçabilenler iç bölgelere doğru göç ediyor.",
                    "Meclis'te endişe büyüyor");
        }
    }

    // ---------- Etki yardımcıları ----------

    static void Yayinla(string baslik, string metin, string etki)
    {
        Gazete.Ekle(baslik, metin, etki, Tarih);
        Gunluk.Ekle("<b>Gazete:</b> " + baslik + (etki != null ? " <size=80%>(" + etki + ")</size>" : ""));
    }

    static void MeclisGuven(int grup, float f)
    {
        if (grup < Meclis.Gruplar.Count) Meclis.Gruplar[grup].guven = Mathf.Clamp(Meclis.Gruplar[grup].guven + f, 0f, 100f);
    }

    static void TurkMorali(int m)
    {
        foreach (Ordu o in UnityEngine.Object.FindObjectsByType<Ordu>(FindObjectsSortMode.None))
            if (o.Yasiyor && o.OyuncununMu) o.MoralDegistir(m);
    }

    // Bölgenin 2 sancak çevresindeki Türk ordularına moral
    static void BolgeMorali(Sancak s, int m)
    {
        if (s == null) return;
        foreach (Ordu o in UnityEngine.Object.FindObjectsByType<Ordu>(FindObjectsSortMode.None))
            if (o.Yasiyor && o.OyuncununMu && o.bulunduguSancak != null
                && Vector3.Distance(o.bulunduguSancak.transform.position, s.transform.position) < 9f)
                o.MoralDegistir(m);
    }

    // Gönüllüler bölgeye en yakın Türk ordusuna katılır; ordu yoksa en yakın Türk sancağında yeni bir birlik kurulur
    public static string GonulluEkle(Sancak s, int n)
    {
        if (s == null || n <= 0) return null;
        Ordu en = null; float enMesafe = float.MaxValue;
        foreach (Ordu o in UnityEngine.Object.FindObjectsByType<Ordu>(FindObjectsSortMode.None))
        {
            if (!o.Yasiyor || !o.OyuncununMu || o.bulunduguSancak == null) continue;
            float d = Vector3.Distance(o.bulunduguSancak.transform.position, s.transform.position);
            if (d < enMesafe) { enMesafe = d; en = o; }
        }
        if (en != null && enMesafe < 12f) { en.askerSayisi += n; return en.orduAdi; }

        Sancak yakin = null; float ym = float.MaxValue;
        foreach (Sancak x in UnityEngine.Object.FindObjectsByType<Sancak>(FindObjectsSortMode.None))
        {
            if (x.sahip != Taraf.Turk) continue;
            float d = Vector3.Distance(x.transform.position, s.transform.position);
            if (d < ym) { ym = d; yakin = x; }
        }
        if (yakin == null) return null;
        Ordu yeni = Ordu.Olustur(yakin.sancakAdi + " Kuva-yi Milliyesi", Taraf.Turk, Mathf.Max(n, 300), yakin, 80);
        yakin.OrdulariDiz();
        return yeni.orduAdi;
    }
}
