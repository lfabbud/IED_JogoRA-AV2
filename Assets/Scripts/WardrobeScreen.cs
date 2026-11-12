using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Tela do vestiário (entre o posicionamento da esteira e o aquecimento da Lu).
// Uma linha por peça de roupa (meia, short, boné); as amostras de cor são criadas por script a partir de
// GameSettings.wardrobeColors. A Lu, já posicionada no mundo real, muda de cor na hora.
public class WardrobeScreen : MonoBehaviour
{
    [System.Serializable]
    public class Row
    {
        public TMP_Text label;
        public RectTransform swatches;
    }

    public Row[] rows;
    public Button randomButton;
    public Button doneButton;
    public float swatchSize = 96f;

    public event System.Action Done;

    LuWardrobe wardrobe;
    GameSettings settings;
    readonly List<List<Outline>> outlines = new List<List<Outline>>();

    void Awake()
    {
        randomButton.onClick.AddListener(Randomize);
        doneButton.onClick.AddListener(() => { if (Done != null) Done(); });
    }

    public void Open(LuWardrobe luWardrobe, GameSettings gameSettings)
    {
        wardrobe = luWardrobe;
        settings = gameSettings;
        outlines.Clear();

        for (int i = 0; i < rows.Length; i++)
        {
            var row = rows[i];
            bool used = i < wardrobe.PartCount;
            if (row.label) row.label.gameObject.SetActive(used);
            row.swatches.gameObject.SetActive(used);

            var list = new List<Outline>();
            outlines.Add(list);
            if (!used) continue;

            if (row.label) row.label.text = wardrobe.parts[i].label;
            for (int c = row.swatches.childCount - 1; c >= 0; c--) Destroy(row.swatches.GetChild(c).gameObject);
            for (int c = 0; c < settings.wardrobeColors.Length; c++) list.Add(CreateSwatch(row.swatches, i, c));
            Refresh(i);
        }
    }

    Outline CreateSwatch(RectTransform parent, int part, int colorIndex)
    {
        var go = new GameObject("Cor" + colorIndex, typeof(RectTransform), typeof(Image), typeof(Button),
            typeof(Outline), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = settings.wardrobeColors[colorIndex];
        var le = go.GetComponent<LayoutElement>();
        le.preferredWidth = swatchSize;
        le.preferredHeight = swatchSize;
        var outline = go.GetComponent<Outline>();
        outline.effectColor = Color.white;
        outline.effectDistance = new Vector2(6f, -6f);
        outline.enabled = false;
        go.GetComponent<Button>().onClick.AddListener(() => Pick(part, colorIndex));
        return outline;
    }

    void Pick(int part, int colorIndex)
    {
        SaveSystem.SetWardrobe(part, colorIndex);
        wardrobe.Apply(part, settings.wardrobeColors[colorIndex]);
        Refresh(part);
    }

    void Refresh(int part)
    {
        int saved = SaveSystem.GetWardrobe(part);
        var list = outlines[part];
        for (int k = 0; k < list.Count; k++) list[k].enabled = k == saved;
    }

    void Randomize()
    {
        if (wardrobe == null) return;
        for (int i = 0; i < wardrobe.PartCount; i++) Pick(i, Random.Range(0, settings.wardrobeColors.Length));
    }
}
