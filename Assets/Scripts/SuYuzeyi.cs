using UnityEngine;

// Denizlerin üstünde hafifçe dalgalanan, güneşi yansıtan yarı saydam su yüzeyi.
// Dalga dokusu (normal haritası) kodla üretilir ve yavaşça kaydırılarak hareket ettirilir.
public class SuYuzeyi : MonoBehaviour
{
    private Material malzeme;
    private readonly Vector2 akis = new Vector2(0.006f, 0.003f);

    public static void Kur(float y, Vector2 boyut)
    {
        GameObject su = GameObject.CreatePrimitive(PrimitiveType.Quad);
        su.name = "SuYuzeyi";
        Destroy(su.GetComponent<Collider>());
        su.transform.position = new Vector3(0f, y, 0f);
        su.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        su.transform.localScale = new Vector3(boyut.x, boyut.y, 1f);

        Material m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        // Saydam yüzey
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 0f);
        m.SetOverrideTag("RenderType", "Transparent");
        m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetInt("_ZWrite", 0);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        // Parlak, hafif mavi su
        m.color = new Color(0.16f, 0.33f, 0.46f, 0.42f);
        m.SetFloat("_Smoothness", 0.92f);
        m.SetFloat("_Metallic", 0f);
        // Dalgalar
        m.SetTexture("_BumpMap", DalgaDokusu());
        m.SetFloat("_BumpScale", 0.35f);
        m.EnableKeyword("_NORMALMAP");
        m.SetTextureScale("_BumpMap", new Vector2(boyut.x * 0.8f, boyut.y * 0.8f));
        su.GetComponent<Renderer>().material = m;

        su.AddComponent<SuYuzeyi>().malzeme = m;
    }

    void Update()
    {
        if (malzeme != null) malzeme.SetTextureOffset("_BumpMap", akis * Time.time);
    }

    // Kendini tekrar eden dalga deseni: birkaç sinüs dalgasının toplamından normal haritası
    static Texture2D DalgaDokusu()
    {
        const int N = 256;
        Texture2D t = new Texture2D(N, N, TextureFormat.RGBA32, true, true);
        t.wrapMode = TextureWrapMode.Repeat;
        Vector3[] dalgalar = { new Vector3(3, 1, 0.3f), new Vector3(-2, 4, 1.7f), new Vector3(5, -3, 2.9f), new Vector3(1, 7, 4.1f), new Vector3(-7, -2, 5.3f) };
        Color[] piksel = new Color[N * N];
        for (int j = 0; j < N; j++)
            for (int i = 0; i < N; i++)
            {
                float u = i / (float)N, v = j / (float)N, dx = 0f, dy = 0f;
                foreach (Vector3 d in dalgalar)
                {
                    float genlik = 1f / Mathf.Sqrt(d.x * d.x + d.y * d.y);
                    float faz = 2f * Mathf.PI * (d.x * u + d.y * v) + d.z;
                    float c = Mathf.Cos(faz) * genlik * 2f * Mathf.PI;
                    dx += c * d.x; dy += c * d.y;
                }
                Vector3 n = new Vector3(-dx * 0.08f, -dy * 0.08f, 1f).normalized;
                piksel[j * N + i] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
            }
        t.SetPixels(piksel);
        t.Apply(true);
        return t;
    }
}
