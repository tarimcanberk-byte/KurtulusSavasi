using System.Collections.Generic;

// Hangi tarafla savaşta olduğumuzu tutar.
// Başlangıçta Yunanistan, Fransa ve Ermenistan ile fiilen savaş var;
// İngiltere ve İtalya işgalci ama açık savaşta değil.
public static class Diplomasi
{
    static HashSet<Taraf> savastakiler = new HashSet<Taraf>();

    // Oyun her başladığında çağrılır
    public static void Sifirla()
    {
        savastakiler = new HashSet<Taraf> { Taraf.Yunan, Taraf.Fransiz, Taraf.Ermeni };
    }

    public static bool SavastaMi(Taraf t)
    {
        return t != Taraf.Turk && savastakiler.Contains(t);
    }

    // Barış halindeki bir tarafa saldırınca çağrılır. Karşı taraf sert tepki verir.
    public static List<string> SavasIlanEt(Taraf t)
    {
        List<string> rapor = new List<string>();
        if (SavastaMi(t) || t == Taraf.Turk) return rapor;
        savastakiler.Add(t);
        rapor.Add("<color=#f99><b>" + TarafBilgi.Ad(t) + " ile savaş başladı!</b></color>");

        // Büyük devletler karşılık olarak asker çıkarır
        if (t == Taraf.Ingiliz)
            rapor.Add(TarihselOlaylar.Cikarma(Taraf.Ingiliz, 12000, "İngiliz Seferi Kuvveti", new[] { "İstanbul", "İzmit", "Çanakkale" }));
        if (t == Taraf.Italyan)
            rapor.Add(TarihselOlaylar.Cikarma(Taraf.Italyan, 6000, "İtalyan Takviye Tümeni", new[] { "Antalya", "Menteşe" }));
        return rapor;
    }

    public static void BarisYap(Taraf t)
    {
        savastakiler.Remove(t);
    }
}
