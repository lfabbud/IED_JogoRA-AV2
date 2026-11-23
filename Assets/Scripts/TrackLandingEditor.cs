using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Inspector do TrackLanding: mostra a queda e a poeira na janela Scene, sem Play.
// Pasta obrigatória: Assets/Scripts/Editor.
[CustomEditor(typeof(TrackLanding))]
public class TrackLandingEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var landing = (TrackLanding)target;
        EditorGUILayout.Space();

        using (new EditorGUI.DisabledScope(Application.isPlaying))
        {
            if (TrackLandingPreview.Target == landing)
            {
                if (GUILayout.Button("Parar pré-visualização")) TrackLandingPreview.Stop();
            }
            else if (GUILayout.Button("Pré-visualizar aterrissagem"))
            {
                TrackLandingPreview.Begin(landing);
            }
        }

        EditorGUILayout.HelpBox(
            Application.isPlaying
                ? "A pré-visualização funciona só fora do Play."
                : "A pré-visualização mostra a queda e a poeira na janela Scene. O som toca só no Play.",
            MessageType.None);
    }

    void OnDisable()
    {
        var landing = target as TrackLanding;
        if (landing != null && TrackLandingPreview.Target == landing) TrackLandingPreview.Stop();
    }
}

// Controla a pré-visualização: avança o tempo pelo EditorApplication.update e devolve a esteira ao lugar original ao parar.
static class TrackLandingPreview
{
    public static TrackLanding Target;
    static Vector3 savedPosition;
    static double startTime;
    static double lastTime;
    static bool hooked;

    public static void Begin(TrackLanding landing)
    {
        Stop();
        if (landing == null) return;
        Target = landing;
        savedPosition = landing.transform.position;
        landing.Begin(savedPosition);
        startTime = EditorApplication.timeSinceStartup;
        lastTime = startTime;
        Hook();
        SceneView.RepaintAll();
    }

    public static void Stop()
    {
        var landing = Target;
        Target = null;
        Unhook();
        if (landing == null) return;
        landing.End();
        landing.transform.position = savedPosition;
        landing.DestroyDust();
        SceneView.RepaintAll();
    }

    static void Hook()
    {
        if (hooked) return;
        hooked = true;
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += OnPlayMode;
        EditorSceneManager.sceneSaving += OnSceneSaving;
        PrefabStage.prefabSaving += OnPrefabSaving;
        AssemblyReloadEvents.beforeAssemblyReload += Stop;
    }

    static void Unhook()
    {
        if (!hooked) return;
        hooked = false;
        EditorApplication.update -= Tick;
        EditorApplication.playModeStateChanged -= OnPlayMode;
        EditorSceneManager.sceneSaving -= OnSceneSaving;
        PrefabStage.prefabSaving -= OnPrefabSaving;
        AssemblyReloadEvents.beforeAssemblyReload -= Stop;
    }

    static void OnPlayMode(PlayModeStateChange change)
    {
        Stop();
    }

    // Salvar a cena ou o prefab no meio da pré-visualização gravaria a esteira no alto: para antes.
    static void OnSceneSaving(UnityEngine.SceneManagement.Scene scene, string path)
    {
        Stop();
    }

    static void OnPrefabSaving(GameObject root)
    {
        Stop();
    }

    static void Tick()
    {
        if (Target == null)
        {
            Stop();
            return;
        }
        double now = EditorApplication.timeSinceStartup;
        float t = (float)(now - startTime);
        float dt = (float)(now - lastTime);
        lastTime = now;

        Target.Step(t);
        Target.AdvanceDust(dt);
        SceneView.RepaintAll();

        if (t >= Target.TotalWithDustSeconds) Stop();
    }
}
