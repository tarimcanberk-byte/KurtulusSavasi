// Haritanın bütün verisi burada. Yeni sancak ya da ordu eklemek için
// sadece bu listelere bir satır eklemek yeterli.
//
// NOT: Koordinatlar sancak merkezlerinin yaklaşık enlem/boylamıdır.
// Üretim değerleri ve asker sayıları prototip içindir, tarihî doğrulama sonra yapılacak.
public static class HaritaVerisi
{
    public struct SancakTanimi
    {
        public string ad; public float enlem, boylam; public Taraf sahip; public int para, erzak;
        public SancakTanimi(string ad, float enlem, float boylam, Taraf sahip, int para, int erzak)
        { this.ad = ad; this.enlem = enlem; this.boylam = boylam; this.sahip = sahip; this.para = para; this.erzak = erzak; }
    }

    public struct OrduTanimi
    {
        public string ad, sancak; public Taraf taraf; public int asker;
        public OrduTanimi(string ad, string sancak, Taraf taraf, int asker)
        { this.ad = ad; this.sancak = sancak; this.taraf = taraf; this.asker = asker; }
    }

    // 19 Mayıs 1919 itibarıyla (yaklaşık) durum
    public static readonly SancakTanimi[] Sancaklar =
    {
        //                 ad            enlem   boylam  sahip           para erzak
        new SancakTanimi("İstanbul",    41.01f, 28.98f, Taraf.Ingiliz,   40, 10),
        new SancakTanimi("İzmit",       40.77f, 29.94f, Taraf.Ingiliz,   15, 10),
        new SancakTanimi("Çanakkale",   40.15f, 26.41f, Taraf.Ingiliz,   10, 10),
        new SancakTanimi("Bursa",       40.19f, 29.06f, Taraf.Turk,      20, 20),
        new SancakTanimi("Balıkesir",   39.65f, 27.88f, Taraf.Turk,      10, 20),
        new SancakTanimi("İzmir",       38.42f, 27.14f, Taraf.Yunan,     35, 15),
        new SancakTanimi("Manisa",      38.61f, 27.43f, Taraf.Turk,      10, 25),
        new SancakTanimi("Aydın",       37.85f, 27.84f, Taraf.Turk,      10, 25),
        new SancakTanimi("Menteşe",     37.22f, 28.36f, Taraf.Italyan,    5, 10),
        new SancakTanimi("Denizli",     37.78f, 29.09f, Taraf.Turk,      10, 15),
        new SancakTanimi("Kütahya",     39.42f, 29.98f, Taraf.Turk,      10, 15),
        new SancakTanimi("Eskişehir",   39.78f, 30.52f, Taraf.Turk,      10, 25),
        new SancakTanimi("Afyon",       38.76f, 30.54f, Taraf.Turk,      10, 15),
        new SancakTanimi("Isparta",     37.76f, 30.55f, Taraf.Turk,       5, 15),
        new SancakTanimi("Antalya",     36.90f, 30.70f, Taraf.Italyan,   10, 10),
        new SancakTanimi("Bolu",        40.74f, 31.61f, Taraf.Turk,       5, 15),
        new SancakTanimi("Ankara",      39.93f, 32.86f, Taraf.Turk,      20,  5),
        new SancakTanimi("Konya",       37.87f, 32.48f, Taraf.Turk,      10, 30),
        new SancakTanimi("Kastamonu",   41.38f, 33.78f, Taraf.Turk,       5, 10),
        new SancakTanimi("Kırşehir",    39.15f, 34.16f, Taraf.Turk,       5, 15),
        new SancakTanimi("Yozgat",      39.82f, 34.81f, Taraf.Turk,       5, 15),
        new SancakTanimi("Kayseri",     38.73f, 35.49f, Taraf.Turk,      15, 15),
        new SancakTanimi("Niğde",       37.97f, 34.68f, Taraf.Turk,       5, 15),
        new SancakTanimi("Adana",       37.00f, 35.32f, Taraf.Fransiz,   25, 25),
        new SancakTanimi("Maraş",       37.58f, 36.94f, Taraf.Ingiliz,   10, 15),
        new SancakTanimi("Antep",       37.07f, 37.38f, Taraf.Ingiliz,   15, 15),
        new SancakTanimi("Urfa",        37.16f, 38.79f, Taraf.Ingiliz,   10, 15),
        new SancakTanimi("Samsun",      41.29f, 36.33f, Taraf.Turk,      15, 10),
        new SancakTanimi("Amasya",      40.65f, 35.83f, Taraf.Turk,       5, 15),
        new SancakTanimi("Sivas",       39.75f, 37.02f, Taraf.Turk,      10, 15),
        new SancakTanimi("Malatya",     38.35f, 38.31f, Taraf.Turk,       5, 15),
        new SancakTanimi("Diyarbakır",  37.91f, 40.24f, Taraf.Turk,      10, 15),
        new SancakTanimi("Erzincan",    39.75f, 39.49f, Taraf.Turk,       5, 10),
        new SancakTanimi("Trabzon",     41.00f, 39.72f, Taraf.Turk,      15, 10),
        new SancakTanimi("Erzurum",     39.90f, 41.27f, Taraf.Turk,      10, 10),
        new SancakTanimi("Kars",        40.60f, 43.10f, Taraf.Ermeni,     5, 10),
        new SancakTanimi("Van",         38.50f, 43.38f, Taraf.Turk,       5, 10),
    };

    public static readonly OrduTanimi[] Ordular =
    {
        new OrduTanimi("20. Kolordu",          "Ankara",    Taraf.Turk,     5000),
        new OrduTanimi("15. Kolordu",          "Erzurum",   Taraf.Turk,    15000),
        new OrduTanimi("3. Kolordu",           "Sivas",     Taraf.Turk,     6000),
        new OrduTanimi("57. Tümen",            "Aydın",     Taraf.Turk,     3000),
        new OrduTanimi("61. Tümen",            "Balıkesir", Taraf.Turk,     3000),

        new OrduTanimi("Yunan İzmir Tümeni",   "İzmir",     Taraf.Yunan,   12000),
        new OrduTanimi("İngiliz İstanbul Garnizonu", "İstanbul", Taraf.Ingiliz, 10000),
        new OrduTanimi("İngiliz İzmit Birliği", "İzmit",    Taraf.Ingiliz,  3000),
        new OrduTanimi("İngiliz Antep Birliği", "Antep",    Taraf.Ingiliz,  3000),
        new OrduTanimi("İtalyan Antalya Birliği", "Antalya", Taraf.Italyan, 4000),
        new OrduTanimi("Fransız Kilikya Birliği", "Adana",  Taraf.Fransiz,  6000),
        new OrduTanimi("Ermeni Kars Ordusu",   "Kars",      Taraf.Ermeni,   8000),
    };

    // Komşuluk otomatik hesaplanır: merkezleri arası mesafe bu değerin altındaysa komşudur (km)
    public const float KomsulukMesafesiKm = 200f;

    // Otomatik hesabın gözden kaçırdığı komşuluklar
    public static readonly string[,] EkKomsuluklar =
    {
        { "Sivas", "Erzincan" },
        { "Eskişehir", "Ankara" },    // Sakarya hattı: Eskişehir-Polatlı-Ankara demiryolu
        { "Afyon", "Ankara" },        // Sivrihisar-Polatlı üzerinden
        { "Konya", "Ankara" },
        { "Diyarbakır", "Van" },
        { "Erzurum", "Van" },
    };

    // Yakın olsa da gerçekte doğrudan geçilemeyen çiftler
    public static readonly string[,] KomsuDegil =
    {
        { "İstanbul", "Balıkesir" }, { "İstanbul", "Eskişehir" }, { "İstanbul", "Kütahya" },
        { "İzmit", "Kütahya" }, { "Menteşe", "Manisa" }, { "Menteşe", "İzmir" },
        { "Denizli", "İzmir" }, { "Denizli", "Manisa" }, { "Balıkesir", "Aydın" },
        { "Kars", "Van" },
    };
}
