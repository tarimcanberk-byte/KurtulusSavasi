using System.Collections.Generic;
using UnityEngine;

// Bir ordunun başka bir sancağa girmesini ve gerekirse savaşı hesaplar.
// Otomatik çözüm burada; oyuncu "Bizzat komuta et" derse MuharebeYoneticisi savaş ekranını açar.
public static class Savas
{
    // Sonucu anlatan bir yazı döndürür
    public static string Ilerle(Ordu saldiran, Sancak hedef)
    {

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
        float savunmaGucu = savunan.SavunmaGucu * Random.Range(0.8f, 1.2f);   // arazi + siper dahil
        bool kazandi = saldiriGucu > savunmaGucu;
        float oran = Mathf.Min(saldiriGucu, savunmaGucu) / Mathf.Max(saldiriGucu, savunmaGucu);

        int saldiranKayip = Mathf.RoundToInt(saldiran.askerSayisi * (kazandi ? 0.20f * oran : 0.40f));
        int savunanKayip  = Mathf.RoundToInt(savunan.askerSayisi  * (kazandi ? 0.40f : 0.20f * oran));
        string baslik = "<b>Muharebe: " + hedef.sancakAdi + "</b>   <size=80%>(güç " + Mathf.RoundToInt(saldiriGucu) + " / " + Mathf.RoundToInt(savunmaGucu) + ")</size>";
        return SonucUygula(saldiran, savunan, hedef, kazandi, saldiranKayip, savunanKayip, baslik);
    }

    // Bir muharebenin sonucunu haritaya işler (otomatik çözümde de, savaş ekranında da kullanılır):
    // kayıplar, moral, yenilenin çekilmesi ya da dağılması, kazananın sancağa girmesi.
    public static string SonucUygula(Ordu saldiran, Ordu savunan, Sancak hedef, bool kazandi,
                                     int saldiranKayip, int savunanKayip, string baslik)
    {
        Ordu kazanan = kazandi ? saldiran : savunan;
        Ordu kaybeden = kazandi ? savunan : saldiran;
        int kazananKayip  = kazandi ? saldiranKayip : savunanKayip;
        int kaybedenKayip = kazandi ? savunanKayip : saldiranKayip;
        string kazananAd = kazanan.orduAdi, kaybedenAd = kaybeden.orduAdi;
        bool oyuncuSaldiriyor = saldiran.OyuncununMu;

        kazanan.KayipVer(kazananKayip);
        kaybeden.KayipVer(kaybedenKayip);
        if (kazanan.Yasiyor) kazanan.MoralDegistir(+10);
        if (kaybeden.Yasiyor) kaybeden.MoralDegistir(-25);

        string rapor = baslik + "\n"
                     + kazananAd + " kazandı (−" + kazananKayip + " asker). "
                     + kaybedenAd + " yenildi (−" + kaybedenKayip + " asker).";
        if (!kaybeden.Yasiyor) rapor += "\n" + kaybedenAd + " dağıldı!";

        if (kazandi)
        {
            // Yenilen savunan geri çekilir; çekilecek yeri yoksa dağılır
            if (savunan.Yasiyor)
            {
                Sancak cekilme = CekilmeYeri(savunan, saldiran.taraf);
                if (cekilme != null) { savunan.Yerles(cekilme); savunan.YurumeBaslat(cekilme); rapor += "\n" + kaybedenAd + " " + cekilme.sancakAdi + " yönüne çekildi."; }
                else { savunan.YokOl(); rapor += "\n" + kaybedenAd + " çekilecek yer bulamadı ve dağıldı!"; }
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
        bool bizimIcinIyi = (kazandi == oyuncuSaldiriyor);
        return Renkli(rapor, bizimIcinIyi);
    }

    // Bir sancaktaki belli bir tarafın toplam savunma gücü (arazi ve siper dahil)
    public static float SavunmaGucu(Sancak s, Taraf t)
    {
        float g = 0f;
        foreach (Ordu o in s.Ordular()) if (o.taraf == t) g += o.SavunmaGucu;
        return g;
    }

    // Baskın: tam bir muharebeye girmeden düşmanı yıpratmak (Kuva-yi Milliye usulü).
    // Hedefteki en güçlü düşmana %4-8 kayıp verdirir; ikmali zayıf düşmana daha etkilidir.
    public static string Baskin(Ordu baskinci, Sancak hedef)
    {
        baskinci.HareketHakkiniKullan();
        Ordu hedefOrdu = null;
        foreach (Ordu o in hedef.Ordular())
            if (o.taraf != baskinci.taraf && (hedefOrdu == null || o.askerSayisi > hedefOrdu.askerSayisi)) hedefOrdu = o;
        if (hedefOrdu == null) return baskinci.orduAdi + ": " + hedef.sancakAdi + "'da baskın yapılacak düşman yok.";

        float carpan = 1f;
        if (baskinci.orduAdi.Contains("Kuva-yi Milliye")) carpan *= 1.5f;      // gerilla ustaları
        if (hedefOrdu.IkmalMesafesi > Ikmal.GuvenliMesafe) carpan *= 1.5f;    // uzun ikmal hattı savunmasız

        int dusmanKayip = Mathf.RoundToInt(hedefOrdu.askerSayisi * Random.Range(0.04f, 0.08f) * carpan);
        int bizimKayip = Mathf.RoundToInt(baskinci.askerSayisi * Random.Range(0.01f, 0.04f));
        string dusmanAd = hedefOrdu.orduAdi;
        hedefOrdu.MoralDegistir(-5);
        hedefOrdu.KayipVer(dusmanKayip);
        baskinci.KayipVer(bizimKayip);
        return Renkli("<b>Baskın: " + hedef.sancakAdi + "</b>\n" + dusmanAd + " −" + dusmanKayip + " asker (moral −5). "
                      + baskinci.orduAdi + " −" + bizimKayip + " asker.", true);
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
