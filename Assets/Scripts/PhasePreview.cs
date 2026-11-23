using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
#endif

// Implementada pelos scripts que mostram na Scene (sem Play) o que depende da fase.
// level == null: tema neutro (menu e scanner).
public interface IPhasePreview
{
    void ApplyPreview(LevelConfig level);
    void ClearPreview();
}

// Fase escolhida para a pré-visualização na Scene. A escolha vale para o Editor inteiro, nas cenas e no Prefab Mode.
public static class PhasePreview
{
#if UNITY_EDITOR
    const string Key = "LuRunner.PreviewLevelGuid";

    // Guardados em memória: o Inspector redesenha várias vezes por segundo e a busca no projeto é pesada.
    static LevelCatalog cachedCatalog;
    static LevelConfig cachedLevel;
    static string cachedLevelGuid = "";
    static bool hooked;

    static void Hook()
    {
        Watch();
        if (hooked) return;
        hooked = true;
        EditorApplication.projectChanged += () => { cachedCatalog = null; cachedLevel = null; cachedLevelGuid = ""; };
    }

    public static LevelConfig Current
    {
        get
        {
            Hook();
            string guid = SessionState.GetString(Key, "");
            if (string.IsNullOrEmpty(guid)) return null;
            if (guid == cachedLevelGuid && cachedLevel != null) return cachedLevel;
            string path = AssetDatabase.GUIDToAssetPath(guid);
            cachedLevel = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<LevelConfig>(path);
            cachedLevelGuid = guid;
            return cachedLevel;
        }
    }

    public static LevelCatalog Catalog()
    {
        Hook();
        if (cachedCatalog != null) return cachedCatalog;
        foreach (string guid in AssetDatabase.FindAssets("t:LevelCatalog"))
        {
            var c = AssetDatabase.LoadAssetAtPath<LevelCatalog>(AssetDatabase.GUIDToAssetPath(guid));
            if (c != null) { cachedCatalog = c; return c; }
        }
        return null;
    }

    // Quando um Level Config, o Game Settings ou o Level Catalog é editado, reaplica a fase escolhida sozinho.
    static bool refreshPending;
    static bool watching;

    static void Watch()
    {
        if (watching) return;
        watching = true;
        ObjectChangeEvents.changesPublished += OnChanges;
    }

    static void OnChanges(ref ObjectChangeEventStream stream)
    {
        if (Application.isPlaying || refreshPending) return;
        for (int i = 0; i < stream.length; i++)
        {
            if (stream.GetEventType(i) != ObjectChangeKind.ChangeAssetObjectProperties) continue;
            stream.GetChangeAssetObjectPropertiesEvent(i, out var e);
            var obj = EditorUtility.InstanceIDToObject(e.instanceId);
            if (obj is LevelConfig || obj is GameSettings || obj is LevelCatalog)
            {
                refreshPending = true;
                EditorApplication.delayCall += () =>
                {
                    refreshPending = false;
                    if (!string.IsNullOrEmpty(SessionState.GetString(Key, ""))) ApplyAll();
                };
                return;
            }
        }
    }

    public static GameSettings Settings()
    {
        var c = Catalog();
        return c == null ? null : c.settings;
    }

    public static void Select(LevelConfig level)
    {
        string guid = level == null ? "" : AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(level));
        SessionState.SetString(Key, guid);
        ApplyAll();
    }

    public static void ApplyAll()
    {
        Watch();
        if (Application.isPlaying) return;
        var level = Current;
        foreach (var p in FindAll()) p.ApplyPreview(level);
        SceneView.RepaintAll();
    }

    public static void ClearAll()
    {
        foreach (var p in FindAll()) p.ClearPreview();
        SceneView.RepaintAll();
    }

    static List<IPhasePreview> FindAll()
    {
        var list = new List<IPhasePreview>();
        var roots = new List<GameObject>();
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var sc = SceneManager.GetSceneAt(i);
            if (sc.isLoaded) roots.AddRange(sc.GetRootGameObjects());
        }
        var stage = PrefabStageUtility.GetCurrentPrefabStage();
        if (stage != null) roots.AddRange(stage.scene.GetRootGameObjects());
        foreach (var r in roots)
        {
            foreach (var m in r.GetComponentsInChildren<MonoBehaviour>(true))
            {
                var p = m as IPhasePreview;
                if (p != null) list.Add(p);
            }
        }
        return list;
    }

    // Objetos criados só para a pré-visualização: não entram na cena nem no prefab ao salvar.
    public static void MarkTemporary(GameObject root)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            t.gameObject.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
    }
#else
    public static LevelConfig Current { get { return null; } }
    public static LevelCatalog Catalog() { return null; }
    public static GameSettings Settings() { return null; }
    public static void MarkTemporary(GameObject root) { }
#endif
}
