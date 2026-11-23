using UnityEditor;
using UnityEngine;

// Seletor de fase no Inspector dos scripts que têm pré-visualização na Scene.
public static class PhasePreviewGUI
{
    public static void Draw()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Pré-visualização da fase", EditorStyles.boldLabel);
        var catalog = PhasePreview.Catalog();
        if (catalog == null || catalog.levels == null)
        {
            EditorGUILayout.HelpBox("Não encontrei um Level Catalog com fases no projeto.", MessageType.Warning);
            return;
        }
        var current = PhasePreview.Current;
        EditorGUILayout.LabelField("Fase mostrada", current == null ? "Tema neutro" : current.displayName);
        if (GUILayout.Button("Tema neutro")) PhasePreview.Select(null);
        foreach (var level in catalog.levels)
        {
            if (level != null && GUILayout.Button(level.displayName)) PhasePreview.Select(level);
        }
        if (GUILayout.Button("Limpar pré-visualização")) PhasePreview.ClearAll();
    }
}

public class PhasePreviewEditorBase : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        PhasePreviewGUI.Draw();
    }
}

[CustomEditor(typeof(LevelThemeApplier))]
public class LevelThemeApplierEditor : PhasePreviewEditorBase { }

[CustomEditor(typeof(TrackRoot))]
public class TrackRootEditor : PhasePreviewEditorBase { }

[CustomEditor(typeof(LuWardrobe))]
public class LuWardrobeEditor : PhasePreviewEditorBase { }

[CustomEditor(typeof(ResultScreen))]
public class ResultScreenEditor : PhasePreviewEditorBase { }

[CustomEditor(typeof(HudController))]
public class HudControllerEditor : PhasePreviewEditorBase { }

[CustomEditor(typeof(MenuController))]
public class MenuControllerEditor : PhasePreviewEditorBase { }

[CustomEditor(typeof(PosterScanner))]
public class PosterScannerEditor : PhasePreviewEditorBase { }

[CustomEditor(typeof(PhasePicker))]
public class PhasePickerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var picker = (PhasePicker)target;
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Pré-visualização da lista de fases", EditorStyles.boldLabel);
        if (GUILayout.Button("Mostrar a lista de fases")) picker.ShowPreview(PhasePreview.Current);
        if (GUILayout.Button("Ocultar a lista de fases")) picker.ClearPreview();
    }
}

[InitializeOnLoad]
public static class PhasePreviewHooks
{
    static PhasePreviewHooks()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.ExitingEditMode) PhasePreview.ClearAll();
        };
    }
}
