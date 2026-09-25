using UnityEngine;

// Savaştaki taraflar
public enum Taraf { Turk, Yunan, Ingiliz, Fransiz, Italyan, Ermeni }

// Taraflarla ilgili yardımcı bilgiler: adları ve haritadaki renkleri
public static class TarafBilgi
{
    public static string Ad(Taraf t)
    {
        switch (t)
        {
            case Taraf.Turk:    return "Ankara Hükümeti";
            case Taraf.Yunan:   return "Yunanistan";
            case Taraf.Ingiliz: return "İngiltere";
            case Taraf.Fransiz: return "Fransa";
            case Taraf.Italyan: return "İtalya";
            default:            return "Ermenistan";
        }
    }

    // Sancak karelerinin rengi
    public static Color SancakRengi(Taraf t)
    {
        switch (t)
        {
            case Taraf.Turk:    return new Color(0.45f, 0.62f, 0.35f);  // yeşil
            case Taraf.Yunan:   return new Color(0.35f, 0.50f, 0.85f);  // mavi
            case Taraf.Ingiliz: return new Color(0.80f, 0.55f, 0.35f);  // turuncu-kahve
            case Taraf.Fransiz: return new Color(0.60f, 0.40f, 0.75f);  // mor
            case Taraf.Italyan: return new Color(0.30f, 0.70f, 0.65f);  // turkuaz
            default:            return new Color(0.75f, 0.45f, 0.50f);  // gül rengi
        }
    }

    // Ordu küplerinin rengi
    public static Color OrduRengi(Taraf t)
    {
        if (t == Taraf.Turk) return new Color(0.75f, 0.08f, 0.08f);    // al bayrak kırmızısı
        return SancakRengi(t) * 0.55f;                                  // düşman: kendi renginin koyusu
    }
}
