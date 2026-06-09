using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Yarn File Creator — Editor Window para generar archivos .yarn
/// Colocar en cualquier carpeta Editor/ dentro de Assets/
/// La ruta de destino se guarda por usuario en EditorPrefs (no afecta a otros colaboradores)
/// </summary>
public class YarnFileCreator : EditorWindow
{
    // ── Tipos de nodo ────────────────────────────────────────────────
    private static readonly string[] TypeCodes  = { "DR", "NT", "POI", "KI", "TR", "IA", "SZ", "IT" };
    private static readonly string[] TypeLabels = { "DR — Door", "NT — Note", "POI — Point of Interest",
                                                     "KI — Key Item", "TR — Transit", "IA — Interactable",
                                                     "SZ — Save Zone", "IT — Item" };

    // ── Preferencia de ruta (por usuario, no entra al repo) ──────────
    private const string PrefKey = "YarnFileCreator_OutputPath";

    // ── Estado del formulario ────────────────────────────────────────
    private string  _outputPath   = "";
    private string  _fileName     = "";
    private int     _typeIndex    = 0;

    private class NodeEntry
    {
        public string Suffix = "";
        public string Body   = "";
    }

    private List<NodeEntry> _nodes = new List<NodeEntry> { new NodeEntry() };

    // ── Scroll ───────────────────────────────────────────────────────
    private Vector2 _scroll;

    // ── Estilos (inicializados en OnGUI para evitar problemas de orden) ──
    private GUIStyle _headerStyle;
    private GUIStyle _sectionStyle;
    private GUIStyle _previewStyle;
    private bool     _stylesReady = false;

    // ── Preview ──────────────────────────────────────────────────────
    private bool    _showPreview  = false;
    private string  _previewText  = "";

    // ────────────────────────────────────────────────────────────────
    [MenuItem("Tools/Yarn File Creator")]
    public static void OpenWindow()
    {
        var win = GetWindow<YarnFileCreator>("Yarn File Creator");
        win.minSize = new Vector2(520, 600);
    }

    private void OnEnable()
    {
        _outputPath = EditorPrefs.GetString(PrefKey, "");
    }

    // ── Inicializar estilos una sola vez ─────────────────────────────
    private void InitStyles()
    {
        if (_stylesReady) return;

        _headerStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 13,
            margin   = new RectOffset(0, 0, 8, 4)
        };

        _sectionStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize  = 11,
            fontStyle = FontStyle.Bold
        };

        _previewStyle = new GUIStyle(EditorStyles.textArea)
        {
            fontStyle = FontStyle.Normal,
            wordWrap  = false,
            richText  = false
        };
        _previewStyle.font = Resources.Load<Font>("Fonts/RobotoMono") 
                             ?? EditorStyles.textArea.font;

        _stylesReady = true;
    }

    // ────────────────────────────────────────────────────────────────
    private void OnGUI()
    {
        InitStyles();

        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        // ── Header ──────────────────────────────────────────────────
        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Yarn File Creator", _headerStyle);
        DrawHRule();

        // ── Carpeta destino ──────────────────────────────────────────
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Output Folder", _sectionStyle);
        EditorGUILayout.BeginHorizontal();
        _outputPath = EditorGUILayout.TextField(_outputPath, GUILayout.ExpandWidth(true));
        if (GUILayout.Button("Browse", GUILayout.Width(64)))
        {
            string picked = EditorUtility.OpenFolderPanel("Select Dialogues folder", _outputPath, "");
            if (!string.IsNullOrEmpty(picked))
            {
                _outputPath = picked;
                EditorPrefs.SetString(PrefKey, _outputPath);
            }
        }
        EditorGUILayout.EndHorizontal();

        if (string.IsNullOrWhiteSpace(_outputPath))
            EditorGUILayout.HelpBox("Seleccioná la carpeta donde se guardarán los archivos .yarn.", MessageType.Info);
        else if (!Directory.Exists(_outputPath))
            EditorGUILayout.HelpBox("La carpeta no existe. Se creará al generar.", MessageType.Warning);

        EditorGUILayout.Space(8);

        // ── Nombre del archivo ───────────────────────────────────────
        EditorGUILayout.LabelField("File Name", _sectionStyle);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(GetPrefix() + "_", GUILayout.Width(GetPrefixLabelWidth()));
        _fileName = EditorGUILayout.TextField(_fileName).ToLower().Replace(" ", "_");
        EditorGUILayout.LabelField(".yarn", GUILayout.Width(40));
        EditorGUILayout.EndHorizontal();

        // ── Tipo ─────────────────────────────────────────────────────
        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Type", _sectionStyle);
        int newIndex = EditorGUILayout.Popup(_typeIndex, TypeLabels);
        if (newIndex != _typeIndex) { _typeIndex = newIndex; _showPreview = false; }

        EditorGUILayout.Space(10);

        // ── Nodos ────────────────────────────────────────────────────
        EditorGUILayout.LabelField("Nodes", _sectionStyle);
        DrawHRule();

        for (int i = 0; i < _nodes.Count; i++)
        {
            DrawNodeEntry(i);
            EditorGUILayout.Space(4);
        }

        EditorGUILayout.Space(4);
        if (GUILayout.Button("+ Add Node", GUILayout.Height(26)))
            _nodes.Add(new NodeEntry());

        EditorGUILayout.Space(10);
        DrawHRule();

        // ── Preview ──────────────────────────────────────────────────
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button(_showPreview ? "Hide Preview" : "Preview .yarn", GUILayout.Height(28)))
        {
            _showPreview = !_showPreview;
            if (_showPreview) _previewText = BuildYarnContent();
        }

        // ── Generar ──────────────────────────────────────────────────
        GUI.enabled = CanGenerate();
        if (GUILayout.Button("Generate File", GUILayout.Height(28)))
            GenerateFile();
        GUI.enabled = true;
        EditorGUILayout.EndHorizontal();

        if (_showPreview)
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Preview", _sectionStyle);
            EditorGUILayout.TextArea(_previewText, _previewStyle, GUILayout.ExpandHeight(true), GUILayout.MinHeight(120));
        }

        EditorGUILayout.Space(10);
        EditorGUILayout.EndScrollView();
    }

    // ── Dibuja una entrada de nodo ───────────────────────────────────
    private void DrawNodeEntry(int index)
    {
        var node = _nodes[index];

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        // Título de nodo + botón eliminar
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"Node {index + 1}", EditorStyles.miniBoldLabel, GUILayout.ExpandWidth(true));
        if (_nodes.Count > 1 && GUILayout.Button("✕", GUILayout.Width(22), GUILayout.Height(18)))
        {
            _nodes.RemoveAt(index);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            return;
        }
        EditorGUILayout.EndHorizontal();

        // Sufijo del título
        EditorGUILayout.BeginHorizontal();
        string fullNodeName = $"{GetPrefix()}_{(string.IsNullOrWhiteSpace(_fileName) ? "<file>" : _fileName)}";
        EditorGUILayout.LabelField(fullNodeName + "_", GUILayout.Width(EditorStyles.label.CalcSize(new GUIContent(fullNodeName + "_")).x + 4));
        node.Suffix = EditorGUILayout.TextField(node.Suffix).ToLower().Replace(" ", "_");
        EditorGUILayout.EndHorizontal();

        // Body
        EditorGUILayout.LabelField("Body text:", EditorStyles.miniLabel);
        node.Body = EditorGUILayout.TextArea(node.Body, GUILayout.MinHeight(60));

        EditorGUILayout.EndVertical();
    }

    // ── Helpers ──────────────────────────────────────────────────────
    private string GetPrefix() => TypeCodes[_typeIndex].ToLower();

    private float GetPrefixLabelWidth()
    {
        string prefix = GetPrefix() + "_";
        return EditorStyles.label.CalcSize(new GUIContent(prefix)).x + 4;
    }

    private bool CanGenerate()
    {
        if (string.IsNullOrWhiteSpace(_outputPath)) return false;
        if (string.IsNullOrWhiteSpace(_fileName))   return false;
        foreach (var n in _nodes)
            if (string.IsNullOrWhiteSpace(n.Suffix) || string.IsNullOrWhiteSpace(n.Body))
                return false;
        return true;
    }

    private string GetFullFileName() => $"{GetPrefix()}_{_fileName}.yarn";

    private string BuildYarnContent()
    {
        var sb = new System.Text.StringBuilder();
        string fileBase = $"{GetPrefix()}_{_fileName}";

        foreach (var node in _nodes)
        {
            string title = string.IsNullOrWhiteSpace(node.Suffix)
                ? fileBase
                : $"{fileBase}_{node.Suffix}";

            sb.AppendLine($"title: {title}");
            sb.AppendLine("---");
            sb.AppendLine(node.Body.Trim());
            sb.AppendLine("===");
            sb.AppendLine();
        }

        return sb.ToString().TrimEnd() + "\n";
    }

    private void GenerateFile()
    {
        if (!Directory.Exists(_outputPath))
            Directory.CreateDirectory(_outputPath);

        string filePath = Path.Combine(_outputPath, GetFullFileName());

        if (File.Exists(filePath))
        {
            bool overwrite = EditorUtility.DisplayDialog(
                "File already exists",
                $"Ya existe el archivo:\n{GetFullFileName()}\n\n¿Sobreescribir?",
                "Sobreescribir", "Cancelar");
            if (!overwrite) return;
        }

        File.WriteAllText(filePath, BuildYarnContent(), System.Text.Encoding.UTF8);

        // Guardar ruta en prefs por si el usuario la cambió
        EditorPrefs.SetString(PrefKey, _outputPath);

        // Refrescar AssetDatabase solo si la carpeta está dentro de Assets/
        string assetsPath = Application.dataPath;
        if (filePath.Replace("\\", "/").StartsWith(assetsPath.Replace("\\", "/")))
            AssetDatabase.Refresh();

        Debug.Log($"[YarnFileCreator] Archivo generado: {filePath}");
        EditorUtility.DisplayDialog("¡Listo!", $"Archivo generado:\n{GetFullFileName()}", "OK");

        // Reset form
        _fileName    = "";
        _nodes       = new List<NodeEntry> { new NodeEntry() };
        _showPreview = false;
    }

    private void DrawHRule()
    {
        var rect = EditorGUILayout.GetControlRect(false, 1);
        EditorGUI.DrawRect(rect, new Color(0.3f, 0.3f, 0.3f, 0.5f));
        EditorGUILayout.Space(2);
    }
}
