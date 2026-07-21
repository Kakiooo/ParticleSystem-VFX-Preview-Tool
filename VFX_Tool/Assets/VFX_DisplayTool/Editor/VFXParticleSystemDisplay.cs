using UnityEditor;
using UnityEngine;

public class VFXParticleSystemDisplay : EditorWindow
{
    PreviewRenderUtility _previewRenderUtility;
    GameObject _instance;
    ParticleSystem _source;       // the asset the user picks in the field
    ParticleSystem _instancePS;   // the live clone's PS that we actually simulate
    double _startTime;

    float _normalizedTime;    // slider value: 0 = start, 1 = end
    bool _isPlaying = true;   // true = auto-advance, false = held on the slider frame

    //Camera control
    float _yaw = -25f;      
    float _pitch = 15f;     
    float _camDistance = 6f;
    bool _orbiting;

    public bool expanded;
    public Editor editor;   // cached embedded inspector for particle system

    float _listHeight = 140f;   // height of the element list; preview takes the rest
    bool _draggingSplitter;
    class VfxElement
    {
        public ParticleSystem system;
        public string name;
        public bool enabled;
        public bool expanded;
        public Editor editor;
    }

    System.Collections.Generic.List<VfxElement> _elements = new System.Collections.Generic.List<VfxElement>();
    Vector2 _scroll;

    [MenuItem("VFXTool/vfx Particle System preview")]
    public static void ShowWindow()
    {
        GetWindow(typeof(VFXParticleSystemDisplay));
    }

    void OnEnable()
    {
        EditorApplication.update += Repaint;
    }

    void OnDisable()
    {
        EditorApplication.update -= Repaint;
        Cleanup();
    }

    private void OnGUI()
    {
        GUILayout.Label("VFX Display", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        _source = EditorGUILayout.ObjectField("Preview VFX", _source, typeof(ParticleSystem), false) as ParticleSystem;//Assign GameObject

        if (EditorGUI.EndChangeCheck())
        {
            if (_source != null) SetTarget(_source.gameObject);
            else Cleanup();
        }
        if (GUILayout.Button("Replay"))
        {
            _startTime = EditorApplication.timeSinceStartup;
            _isPlaying = true;
        }
        EditorGUI.BeginChangeCheck();
        _normalizedTime = EditorGUILayout.Slider("Stage", _normalizedTime, 0f, 1f);
        if (EditorGUI.EndChangeCheck())
        {
            _isPlaying = false;   // grabbing the slider pauses auto-play so you can hold a frame
        }

        if (_elements.Count > 0)
        {
            GUILayout.Label("VFX Elements", EditorStyles.boldLabel);

            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.Height(_listHeight));
            foreach (var e in _elements)
            {
                EditorGUILayout.BeginHorizontal();
                e.expanded = EditorGUILayout.Foldout(e.expanded, e.name, true);
                bool now = EditorGUILayout.Toggle(e.enabled, GUILayout.Width(20));
                if (now != e.enabled)
                {
                    e.enabled = now;
                    e.system.gameObject.SetActive(now);
                }
                EditorGUILayout.EndHorizontal();

                if (e.expanded)
                {
                    if (e.editor == null)
                    {
                        e.editor = Editor.CreateEditor(e.system);   // create once, reuse
                    }
                    EditorGUI.indentLevel++;
                    e.editor.OnInspectorGUI();     // the real particle-system inspector
                    EditorGUI.indentLevel--;
                }
            }
            EditorGUILayout.EndScrollView();
        }

        //preview bar size changing
        Rect bar = GUILayoutUtility.GetRect(position.width, 20f, GUILayout.ExpandWidth(true));
        GUI.Box(bar, GUIContent.none, EditorStyles.toolbar);
        GUI.Label(new Rect(bar.x + 6, bar.y + 2, 100, 16), "Preview", EditorStyles.boldLabel);
        EditorGUIUtility.AddCursorRect(bar, MouseCursor.ResizeVertical);
        HandleSplitter(bar);

        Rect rect = GUILayoutUtility.GetRect(position.width, 10f,GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

        HandleCameraInput(rect);

        if (Event.current.type == EventType.Repaint)
        {
            Draw(rect);
        }

        if (GUILayout.Button("Export as New Prefab"))
        {
            ExportPrefab();
        }

    }

    public void SetTarget(GameObject prefab)//initial set up for the new vfx prefab
    {
        Cleanup();
        _previewRenderUtility = new PreviewRenderUtility();
        _previewRenderUtility.camera.transform.position = new Vector3(0, 0.5f, -30);
        _previewRenderUtility.camera.transform.rotation = Quaternion.Euler(0, 0, 0);
        _previewRenderUtility.camera.farClipPlane = 100f;

        _instance = Object.Instantiate(prefab);
        _previewRenderUtility.AddSingleGO(_instance);// move it into the preview scene

        _elements.Clear();
        foreach (ParticleSystem ps in _instance.GetComponentsInChildren<ParticleSystem>(true))
        {
            ps.useAutoRandomSeed = false;                // seed pin, now covers inactive ones too
            _elements.Add(new VfxElement{system = ps,name = ps.name,enabled = ps.gameObject.activeSelf});
        }

        _instancePS = _instance.GetComponentInChildren<ParticleSystem>();

        _startTime = EditorApplication.timeSinceStartup;
    }

    public void Draw(Rect rect)
    {
        if (_previewRenderUtility == null || _instancePS == null) return;

        ParticleSystem.MainModule main = _instancePS.main;
        float duration = main.duration;  // approx total run time

        float t;
        if (_isPlaying)
        {
            t = (float)(EditorApplication.timeSinceStartup - _startTime);
            _normalizedTime = (duration > 0f) ? Mathf.Clamp01(t / duration) : 0f;
            if (t >= duration) _isPlaying = false;
        }
        else
        {
            t = _normalizedTime * duration;
        }
        _instancePS.Simulate(t, true, true);

        _previewRenderUtility.BeginPreview(rect, GUIStyle.none);
        _previewRenderUtility.Render(true);         
        var tex = _previewRenderUtility.EndPreview();
        GUI.DrawTexture(rect, tex, ScaleMode.StretchToFill, false);

        //camera control
        Quaternion rot = Quaternion.Euler(_pitch, _yaw, 0f);
        Vector3 pivot = (_instance != null) ? _instance.transform.position : Vector3.zero;
        _previewRenderUtility.camera.transform.rotation = rot;
        _previewRenderUtility.camera.transform.position = pivot + rot * new Vector3(0f, 0f, -_camDistance);
        _previewRenderUtility.camera.farClipPlane = 100f;
    }

    public void Cleanup()
    {
        if (_instance != null) Object.DestroyImmediate(_instance);
        _previewRenderUtility?.Cleanup();
        _previewRenderUtility = null;
        _instancePS = null;
    }

    void HandleCameraInput(Rect rect)
    {
        Event e = Event.current;
        switch (e.type)
        {
            case EventType.MouseDown:
                if (e.button == 1 && rect.Contains(e.mousePosition)) { _orbiting = true; e.Use(); }
                break;
            case EventType.MouseUp:
                if (e.button == 1 && _orbiting) { _orbiting = false; e.Use(); }
                break;
            case EventType.MouseDrag:
                if (_orbiting)
                {
                    _yaw += e.delta.x * 0.5f;
                    _pitch = Mathf.Clamp(_pitch + e.delta.y * 0.5f, -89f, 89f);
                    e.Use();
                    Repaint();
                }
                break;
            case EventType.ScrollWheel:
                if (rect.Contains(e.mousePosition))
                {
                    _camDistance = Mathf.Clamp(_camDistance * (1f + e.delta.y * 0.03f), 0.5f, 100f);
                    e.Use();
                    Repaint();
                }
                break;
        }
    }

    void HandleSplitter(Rect bar)
    {
        Event e = Event.current;
        switch (e.type)
        {
            case EventType.MouseDown:
                if (bar.Contains(e.mousePosition)) { _draggingSplitter = true; e.Use(); }
                break;
            case EventType.MouseUp:
                if (_draggingSplitter) { _draggingSplitter = false; e.Use(); }
                break;
            case EventType.MouseDrag:
                if (_draggingSplitter)
                {
                    _listHeight = Mathf.Clamp(_listHeight + e.delta.y, 60f, position.height - 150f);
                    e.Use();
                    Repaint();
                }
                break;
        }
    }

    void ExportPrefab()
    {
        if (_instance == null || _source == null) return;

        string srcPath = AssetDatabase.GetAssetPath(_source);
        string dir = System.IO.Path.GetDirectoryName(srcPath);
        string name = System.IO.Path.GetFileNameWithoutExtension(srcPath);
        string newPath = AssetDatabase.GenerateUniqueAssetPath($"{dir}/{name}_edited.prefab");

        // Save from a plain copy, not the preview instance itself:
        GameObject temp = Object.Instantiate(_instance);
        temp.hideFlags = HideFlags.None;
        foreach (Transform t in temp.GetComponentsInChildren<Transform>(true))
        {
            t.gameObject.hideFlags = HideFlags.None;      // strip preview-scene hide flags
        }

        bool ok;
        PrefabUtility.SaveAsPrefabAsset(temp, newPath, out ok);
        Object.DestroyImmediate(temp);

        if (ok)
        {
            var saved = AssetDatabase.LoadAssetAtPath<GameObject>(newPath);
            EditorGUIUtility.PingObject(saved);
            Debug.Log($"Exported: {newPath}");
        }
        else Debug.LogError("Prefab export failed.");
    }
}