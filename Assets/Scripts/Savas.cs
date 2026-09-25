using System.Collections.Generic;
using UnityEngine;

// Bir ordunun başka bir sancağa girmesini ve gerekirse savaşı hesaplar.
// Şimdilik savaşlar otomatik çözülür; ileride burada savaş ekranı açılacak.
public static class Savas
{
    // Sonucu anlatan bir yazı döndürür
    public static string Ilerle(Ordu saldiran, Sancak hedef)
    {
        saldiran.HareketHakkiniKullan();

        // Hedefteki düşman ordularından en güçlüsünü bul
        Ordu savunan = null;
        foreach (Ordu o in hedef.Ordular())
            if (o.taraf != saldiran.taraf && (savunan == null || o.Guc > savunan.Guc))
                savunan = o;

        // Düşman ordusu yoksa: yürü, sancak düşmandaysa ele geçir
        if (savunan == null)
        {
            saldiran.Yerles(hedef);
            if (hedef.sahip != saldiran.taraf)
            {
                Taraf eski = hedef.sahip;
                hedef.SahipDegistir(saldiran.taraf);
                return Renkli(hedef.sancakAdi + ", " + TarafBilgi.Ad(eski) + " elinden alındı! (" + saldiran.orduAdi + ")", saldiran.OyuncununMu);
            }
            return saldiran.orduAdi + " yürüdü → " + hedef.sancakAdi;
        }

        // Savaş: iki tarafın gücü + şans. Savunan arazi avantajıyla %20 bonus alır.
        float saldiriGucu = saldiran.Guc * Random.Range(0.8f, 1.2f);
        float savunmaGucu = savunan.Guc * 1.2f * Random.Range(0.8f, 1.2f);
        bool kazandi = saldiriGucu > savunmaGucu;
        float oran = Mathf.Min(saldiriGucu, savunmaGucu) / Mathf.Max(saldiriGucu, savunmaGucu);

        Ordu kazanan = kazandi ? saldiran : savunan;
        Ordu kaybeden = kazandi ? savunan : saldiran;

        int kazananKayip  = Mathf.RoundToInt(kazanan.askerSayisi * 0.20f * oran);
        int kaybedenKayip = Mathf.RoundToInt(kaybeden.askerSayisi * 0.40f);
        string kazananAd = kazanan.orduAdi, kaybedenAd = kaybeden.orduAdi;

        kazanan.KayipVer(kazananKayip);
        kaybeden.KayipVer(kaybedenKayip);
        kazanan.MoralDegistir(+10);
        kaybeden.MoralDegistir(-25);

        string rapor = "<b>Muharebe: " + hedef.sancakAdi + "</b>   <size=80%>(güç " + Mathf.RoundToInt(saldiriGucu) + " / " + Mathf.RoundToInt(savunmaGucu) + ")</size>\n"
                     + kazananAd + " kazandı (−" + kazananKayip + " asker). "
                     + kaybedenAd + " yenildi (−" + kaybedenKayip + " asker).";

        if (kazandi)
        {
            // Yenilen savunan geri çekilir; çekilecek yeri yoksa dağılır
            if (savunan.Yasiyor)
            {
                Sancak cekilme = CekilmeYeri(savunan, saldiran.taraf);
                if (cekilme != null) { savunan.Yerles(cekilme); rapor += "\n" + kaybedenAd + " " + cekilme.sancakAdi + " yönüne çekildi."; }
                else { savunan.YokOl(); rapor += "\n" + kaybedenAd + " dağıldı!"; }
            }

            // Sancakta başka düşman kalmadıysa saldıran içeri girer
            bool dusmanKaldi = false;
            foreach (Ordu o in hedef.Ordular()) if (o.taraf != saldiran.taraf) dusmanKaldi = true;
            if (!dusmanKaldi && saldiran.Yasiyor)
            {
                saldiran.Yerles(hedef);
                if (hedef.sahip != saldiran.taraf)
                {
                    hedef.SahipDegistir(saldiran.taraf);
                    rapor += "\n" + hedef.sancakAdi + " " + TarafBilgi.Ad(saldiran.taraf) + " eline geçti!";
                }
            }
        }
        // Bizim için iyi sonuç yeşil, kötü sonuç kırmızı
        bool bizimIcinIyi = (kazandi == saldiran.OyuncununMu);
        return Renkli(rapor, bizimIcinIyi);
    }

    // Bir sancaktaki belli bir tarafın toplam savaş gücü
    public static float TarafGucu(Sancak s, Taraf t)
    {
        float g = 0f;
        foreach (Ordu o in s.Ordular()) if (o.taraf == t) g += o.Guc;
        return g;
    }

    static string Renkli(string metin, bool iyi)
    {
        return (iyi ? "<color=#9f9>" : "<color=#f99>") + metin + "</color>";
    }

    // Yenilen ordunun çekilebileceği, kendi tarafına ait ve düşmansız bir komşu sancak
    static Sancak CekilmeYeri(Ordu ordu, Taraf dusman)
    {
        foreach (Sancak k in ordu.bulunduguSancak.komsular)
        {
            if (k.sahip != ordu.taraf) continue;
            bool dusmanVar = false;
            foreach (Ordu o in k.Ordular()) if (o.taraf == dusman) dusmanVar = true;
            if (!dusmanVar) return k;
        }
        return null;
    }
}
