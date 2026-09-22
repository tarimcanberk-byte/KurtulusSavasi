using UnityEngine;
using UnityEngine.InputSystem;

// Fareyle tıklanan sancağı bulur ve seçer.
// Bu scripti sahnede tek bir nesneye (örneğin Main Camera'ya) ekleyeceğiz.
public class HaritaSecici : MonoBehaviour
{
    private Sancak seciliSancak;

    // Her karede (saniyede onlarca kez) çalışır
    void Update()
    {
        // Sol fare tuşuna bu karede basılmadıysa hiçbir şey yapma
        if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame)
            return;

        // Kameradan, farenin olduğu noktaya doğru görünmez bir ışın gönder
        Ray isin = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());

        if (Physics.Raycast(isin, out RaycastHit carpma))
        {
            Sancak tiklanan = carpma.collider.GetComponent<Sancak>();
            if (tiklanan != null)
            {
                if (seciliSancak != null)
                    seciliSancak.SecimiKaldir();

                seciliSancak = tiklanan;
                seciliSancak.Sec();
                return;
            }
        }

        // Boş bir yere tıklandıysa seçimi kaldır
        if (seciliSancak != null)
        {
            seciliSancak.SecimiKaldir();
            seciliSancak = null;
        }
    }
}
