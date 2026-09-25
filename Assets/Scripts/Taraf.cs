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
            case Taraf.Turk:    return new Color(0.78f, 0.30f, 0.22f);  // kiremit kırmızısı
            case Taraf.Yunan:   return new Color(0.35f, 0.50f, 0.85f);  // mavi
            case Taraf.Ingiliz: return new Color(0.88f, 0.76f, 0.30f);  // hardal sarısı
            case Taraf.Fransiz: return new Color(0.60f, 0.40f, 0.75f);  // mor
            case Taraf.Italyan: return new Color(0.30f, 0.70f, 0.65f);  // turkuaz
            default:            return new Color(0.75f, 0.45f, 0.50f);  // gül rengi
        }
    }

    // Asker figürlerinin üniforma rengi
    public static Color UniformaRengi(Taraf t)
    {
        switch (t)
        {
            case Taraf.Turk:    return new Color(0.46f, 0.43f, 0.31f);  // haki
            case Taraf.Yunan:   return new Color(0.40f, 0.41f, 0.32f);  // zeytin haki
            case Taraf.Ingiliz: return new Color(0.56f, 0.48f, 0.31f);  // açık haki
            case Taraf.Fransiz: return new Color(0.46f, 0.53f, 0.64f);  // ufuk mavisi
            case Taraf.Italyan: return new Color(0.45f, 0.48f, 0.42f);  // gri-yeşil
            default:            return new Color(0.38f, 0.35f, 0.29f);
        }
    }

    // Ordu küplerinin rengi
    public static Color OrduRengi(Taraf t)
    {
        if (t == Taraf.Turk) return new Color(0.80f, 0.06f, 0.06f);    // al bayrak kırmızısı
        return SancakRengi(t) * 0.55f;                                  // düşman: kendi renginin koyusu
    }
}
