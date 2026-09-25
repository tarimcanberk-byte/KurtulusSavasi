using System.Collections.Generic;
using UnityEngine;

// Düşman taraflarının her tur ne yapacağına karar verir.
// Basit kurallar: her taraf farklı derecede saldırgandır; yalnızca kazanabileceği
// savaşlara girer; boş Türk sancaklarını küçük müfrezelerle işgal eder; her tur takviye alır.
public static class DusmanYapayZeka
{
    // Her tur saldırıya geçme ihtimali (0 = hiç, 1 = her zaman)
    static float Saldirganlik(Taraf t)
    {
        switch (t)
        {
            case Taraf.Yunan:   return 0.6f;   // 1919-22 boyunca ilerleyen ana düşman
            case Taraf.Ermeni:  return 0.3f;
            case Taraf.Fransiz: return 0.25f;
            case Taraf.Ingiliz: return 0.4f;   // savaş ilan edilirse
            default:            return 0.3f;   // İtalya, savaş ilan edilirse
        }
    }

    // Her tur gelen takviye asker
    static int Takviye(Taraf t)
    {
        switch (t)
        {
            case Taraf.Yunan:   return 500;    // + tarihî taarruz dalgaları (TarihselOlaylar)
            case Taraf.Fransiz: return 150;
            case Taraf.Ermeni:  return 150;
            case Taraf.Ingiliz: return 1500;   // İngiltere ile savaş çok pahalıya patlar
            default:            return 500;
        }
    }

    public static List<string> TurOyna()
    {
        List<string> rapor = new List<string>();
        TakviyeleriDagit();

        // Listenin bir kopyası üzerinde dönüyoruz; tur içinde yeni müfrezeler oluşabilir
        Ordu[] ordular = Object.FindObjectsByType<Ordu>(FindObjectsSortMode.None);
        foreach (Ordu o in ordular)
        {
            if (o == null || !o.Yasiyor || o.OyuncununMu) continue;
            if (!Diplomasi.SavastaMi(o.taraf)) continue;   // barıştaki taraflar saldırmaz
            if (Random.value > Saldirganlik(o.taraf)) continue;

            Sancak hedef = HedefSec(o);
            if (hedef == null) continue;

            // Büyük bir ordu boş bir sancağa bütün gücüyle değil, küçük bir müfrezeyle gider
            Ordu yuruyen = o;
            if (TurkGucu(hedef) == 0f && o.askerSayisi >= 6000)
            {
                yuruyen = Ordu.Olustur(TarafBilgi.Ad(o.taraf) + " Müfrezesi", o.taraf, 2000, o.bulunduguSancak);
                o.askerSayisi -= 2000;
            }

            string sonuc = Savas.Ilerle(yuruyen, hedef);
            rapor.Add(sonuc.Replace("\n", " "));
        }
        return rapor;
    }

    // Saldırılabilecek en cazip komşu Türk sancağını seçer (yoksa null)
    static Sancak HedefSec(Ordu o)
    {
        Sancak enIyi = null;
        float enIyiPuan = 0f;
        foreach (Sancak k in o.bulunduguSancak.komsular)
        {
            bool turkBolgesi = k.sahip == Taraf.Turk || TurkGucu(k) > 0f;
            if (!turkBolgesi) continue;

            // Sadece açıkça daha güçlüyse saldır (savunma bonusu + güvenlik payı)
            float savunma = TurkGucu(k) * 1.2f;
            if (o.Guc < savunma * 1.3f) continue;

            float puan = k.paraUretimi + k.erzakUretimi + (savunma == 0f ? 20f : 0f) + Random.value * 10f;
            if (puan > enIyiPuan) { enIyiPuan = puan; enIyi = k; }
        }
        return enIyi;
    }

    static float TurkGucu(Sancak s) { return Savas.TarafGucu(s, Taraf.Turk); }

    // Her düşman tarafı takviyesini en büyük ordusuna ekler
    static void TakviyeleriDagit()
    {
        Dictionary<Taraf, Ordu> enBuyuk = new Dictionary<Taraf, Ordu>();
        foreach (Ordu o in Object.FindObjectsByType<Ordu>(FindObjectsSortMode.None))
        {
            if (o.OyuncununMu || !o.Yasiyor) continue;
            if (!enBuyuk.ContainsKey(o.taraf) || o.askerSayisi > enBuyuk[o.taraf].askerSayisi)
                enBuyuk[o.taraf] = o;
        }
        foreach (var cift in enBuyuk)
            if (Diplomasi.SavastaMi(cift.Key))
                cift.Value.askerSayisi += Takviye(cift.Key);
    }
}
