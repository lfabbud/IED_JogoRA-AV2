using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// Painel do Orientation Layout no Inspector: a chave de leiaute, os tamanhos em pixels e o botão de salvar.
[CustomEditor(typeof(OrientationLayout))]
public class OrientationLayoutEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var o = (OrientationLayout)target;

        var lay = (OrientationLayout.Layout)EditorGUILayout.EnumPopup("Leiaute", o.layout);
        if (lay != o.layout)
        {
            if (o.HasUnsavedChanges())
            {
                string nome = o.layout == OrientationLayout.Layout.Horizontal ? "horizontal" : "vertical";
                int r = EditorUtility.DisplayDialogComplex("Orientation Layout",
                    "Há posições modificadas e não salvas no leiaute " + nome + ".", "Salvar", "Cancelar", "Descartar");
                if (r == 1) return; // cancelar: continua no leiaute atual
                if (r == 0) o.SaveCurrent();
                else o.RevertCurrent();
            }
            o.SelectLayout(lay);
            Vector2 sz = lay == OrientationLayout.Layout.Horizontal ? o.horizontalSize : o.verticalSize;
            ApplyToGameView(Mathf.RoundToInt(sz.x), Mathf.RoundToInt(sz.y));
            GUIUtility.ExitGUI();
        }

        EditorGUI.BeginChangeCheck();
        var v = EditorGUILayout.Vector2Field("Vertical (pixels)", o.verticalSize);
        var h = EditorGUILayout.Vector2Field("Horizontal (pixels)", o.horizontalSize);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(o, "Orientation Layout");
            o.verticalSize = v;
            o.horizontalSize = h;
            EditorUtility.SetDirty(o);
            if (o.editing) o.BeginEdit();
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Posição vertical salva: " + o.CountSaved(false) + " elementos");
        EditorGUILayout.LabelField("Posição horizontal salva: " + o.CountSaved(true) + " elementos");
        Vector2 game = Handles.GetMainGameViewSize();
        EditorGUILayout.LabelField("Janela Game agora: " + Mathf.RoundToInt(game.x) + " x " + Mathf.RoundToInt(game.y));
        bool gameLandscape = game.x > game.y;
        bool wantLandscape = o.layout == OrientationLayout.Layout.Horizontal;
        if (gameLandscape != wantLandscape)
            EditorGUILayout.HelpBox("A janela Game não está na proporção do leiaute escolhido. Escolha a proporção à mão no menu da janela Game, ou clique em Aplicar abaixo.", MessageType.Warning);
        EditorGUILayout.Space();
        if (GUILayout.Button("Salvar posição deste leiaute")) o.SaveCurrent();
        if (GUILayout.Button("Aplicar a proporção deste leiaute ao Game (Incerto)"))
        {
            Vector2 sz = wantLandscape ? o.horizontalSize : o.verticalSize;
            ApplyToGameView(Mathf.RoundToInt(sz.x), Mathf.RoundToInt(sz.y));
        }
    }

    // INCERTO: usa partes internas do Editor (GameViewSizes / GameView), sem API oficial; pode não funcionar no Unity 6.
    static void ApplyToGameView(int w, int h)
    {
        try
        {
            var asm = typeof(Editor).Assembly;
            var sizesType = asm.GetType("UnityEditor.GameViewSizes");
            var singleton = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
            var instance = singleton.GetProperty("instance").GetValue(null, null);
            var group = sizesType.GetProperty("currentGroup").GetValue(instance, null);
            var groupType = group.GetType();

            string label = "Orientation Layout " + w + "x" + h;
            var texts = (string[])groupType.GetMethod("GetDisplayTexts").Invoke(group, null);
            int index = -1;
            for (int i = 0; i < texts.Length; i++)
            {
                if (texts[i].Contains(label)) { index = i; break; }
            }
            if (index < 0)
            {
                var sizeType = asm.GetType("UnityEditor.GameViewSize");
                var enumType = asm.GetType("UnityEditor.GameViewSizeType");
                object fixedRes = Enum.Parse(enumType, "FixedResolution");
                object size = Activator.CreateInstance(sizeType, new object[] { fixedRes, w, h, label });
                groupType.GetMethod("AddCustomSize").Invoke(group, new object[] { size });
                index = (int)groupType.GetMethod("GetTotalCount").Invoke(group, null) - 1;
            }

            var gvType = asm.GetType("UnityEditor.GameView");
            var win = EditorWindow.GetWindow(gvType);
            var cb = gvType.GetMethod("SizeSelectionCallback", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            cb.Invoke(win, new object[] { index, null });
            win.Repaint();
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Lu] Não consegui mudar a proporção da janela Game por script (" + e.Message + "). Escolha a proporção à mão no menu da janela Game.");
        }
    }
}
