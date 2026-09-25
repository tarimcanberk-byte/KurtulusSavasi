using System.Collections.Generic;
using UnityEngine;

// İkmal (lojistik) hesabı: bir ordu kendi ikmal kaynağından ne kadar uzakta?
// Türk orduları kendi topraklarında her yerden beslenir.
// Yabancı ordular denizden (kendi elindeki kıyı sancaklarından) beslenir; Ermeni ordusu Kars'tan.
// Kaynaktan uzaklaştıkça ordu zayıflar ve her tur asker kaybeder. Yolu kesilen ordu "kuşatılmış" sayılır.
public static class Ikmal
{
    public const int Kusatilmis = 99;
    public const int GuvenliMesafe = 2;   // bu kadar adıma kadar ceza yok

    static bool KaynakMi(Sancak s, Taraf t)
    {
        if (s.sahip != t) return false;
        if (t == Taraf.Turk) return true;
        if (t == Taraf.Ermeni) return s.sancakAdi == "Kars";
        return s.kiyiMi;
    }

    // Ordunun bulunduğu yerden en yakın ikmal kaynağına, kendi (ya da dost) topraklarından geçen en kısa yol
    public static int Mesafe(Ordu o)
    {
        Sancak baslangic = o.bulunduguSancak;
        if (baslangic == null) return 0;
        if (KaynakMi(baslangic, o.taraf)) return 0;

        Dictionary<Sancak, int> uzaklik = new Dictionary<Sancak, int> { { baslangic, 0 } };
        Queue<Sancak> kuyruk = new Queue<Sancak>();
        kuyruk.Enqueue(baslangic);
        while (kuyruk.Count > 0)
        {
            Sancak s = kuyruk.Dequeue();
            foreach (Sancak k in s.komsular)
            {
                if (uzaklik.ContainsKey(k)) continue;
                int d = uzaklik[s] + 1;
                if (KaynakMi(k, o.taraf)) return d;
                if (k.sahip != o.taraf) continue;        // düşman toprağından ikmal geçmez
                uzaklik[k] = d;
                kuyruk.Enqueue(k);
            }
        }
        return Kusatilmis;
    }

    // Savaş gücüne çarpan: 2 adıma kadar tam, sonra her adım %15 düşük, kuşatılmışsa yarı yarıya
    public static float Carpan(int mesafe)
    {
        if (mesafe >= Kusatilmis) return 0.5f;
        if (mesafe <= GuvenliMesafe) return 1f;
        return Mathf.Max(0.4f, 1f - 0.15f * (mesafe - GuvenliMesafe));
    }

    // Her tur ikmalsizlikten kaybedilen asker oranı
    public static float YipranmaOrani(int mesafe)
    {
        if (mesafe >= Kusatilmis) return 0.10f;
        if (mesafe <= GuvenliMesafe) return 0f;
        return 0.03f * (mesafe - GuvenliMesafe);
    }

    public static string Aciklama(int mesafe)
    {
        if (mesafe >= Kusatilmis) return "<color=#f66>KUŞATILMIŞ (ikmal yok)</color>";
        if (mesafe <= GuvenliMesafe) return "<color=#9f9>iyi</color>";
        return "<color=#fc8>zayıf (" + mesafe + " adım, güç %" + Mathf.RoundToInt(Carpan(mesafe) * 100) + ")</color>";
    }
}
