using UnityEngine;

// Mostra uma tela e esconde as outras da lista.
public class UiScreens : MonoBehaviour
{
    public GameObject[] screens;

    public void Show(GameObject target)
    {
        if (screens == null) return;
        foreach (var s in screens)
        {
            if (s != null) s.SetActive(s == target);
        }
    }
}
