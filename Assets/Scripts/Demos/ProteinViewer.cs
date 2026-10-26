using MolecularSurfaces.Components;
using MolecularSurfaces.Components.Instances;
using MolecularSurfaces.DataSources;
using MolecularSurfaces.Helpers;
using MolecularSurfaces.Molecules.Proteins;
using System;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;

namespace MolecularSurfaces.Demos
{
    /// <summary>
    /// Single-scene viewer: type a 4-character PDB ID, download it from RCSB and
    /// render its molecular surface. Camera controls live on <see cref="OrbitCamera"/>.
    /// </summary>
    public class ProteinViewer : MonoBehaviour
    {
        public string ProteinId = "1UBQ";
        public bool LoadOnStart = true;

        public Color ModelColor = new Color(0.55f, 0.8f, 1f);
        public float RenderScale = 1;
        public float RadiusScale = 1;

        public OrbitCamera Orbit;

        static readonly Regex _PdbId = new Regex("^[0-9][A-Za-z0-9]{3}$");

        Transform _ProteinContainer => transform.Cursor("[Protein]");
        ProteinInstance _Instance;

        string _Input;
        string _Status = "";
        bool _Busy;

        void Start()
        {
            _Input = ProteinId;
            if (Orbit == null) Orbit = FindObjectOfType<OrbitCamera>();
            if (Orbit != null) Orbit.IsPointerBlocked = OverPanel;
            if (LoadOnStart) Load(ProteinId);
        }

        // Input.mousePosition is bottom-left origin; GUI rects are top-left.
        bool OverPanel(Vector2 mouse)
        {
            return PanelRect.Contains(new Vector2(mouse.x, Screen.height - mouse.y));
        }

        async void Load(string id)
        {
            if (_Busy) return;

            id = (id ?? "").Trim().ToUpperInvariant();
            if (!_PdbId.IsMatch(id))
            {
                _Status = "PDB IDs are 4 characters, starting with a digit (e.g. 1UBQ).";
                return;
            }

            _Busy = true;
            _Status = $"Downloading {id}...";

            try
            {
                // Download and parse off the main thread so the UI stays responsive.
                var _protein = await Task.Run(() => PDBBank.DownloadPDB(id));

                _Status = $"Building surface for {id} ({_protein.Atoms.Length:N0} atoms)...";
                await Task.Yield();

                Show(_protein);
                ProteinId = id;
                _Status = $"{id}: {_protein.Residues.Length:N0} residues, {_protein.Atoms.Length:N0} atoms";
            }
            catch (Exception e)
            {
                var _inner = e is AggregateException _agg ? _agg.Flatten().InnerException : e;
                _Status = _inner is HttpRequestException
                    ? $"Couldn't download {id}. Check the ID exists on rcsb.org and that you're online. (Very large entries have no .pdb file, only mmCIF.)"
                    : $"Failed to load {id}: {_inner.Message}";
                Debug.LogException(e);
            }
            finally
            {
                _Busy = false;
            }
        }

        void Show(Protein protein)
        {
            _ProteinContainer.Purge<ProteinInstance>();

            // Centre the model on the container so the camera can orbit the origin.
            var _positions = protein.Atoms.Select(s => s.Position * RenderScale).ToArray();
            var _center = _positions.Aggregate(Vector3.zero, (sum, p) => sum + p) / Mathf.Max(1, _positions.Length);
            var _radius = _positions.Length == 0 ? 1 : _positions.Max(p => Vector3.Distance(p, _center));

            _Instance = _ProteinContainer.Create<ProteinInstance>(protein.Name, null);
            _Instance.transform.localPosition = -_center;
            _Instance.ModelColor = ModelColor;
            _Instance.RenderScale = RenderScale;
            _Instance.RadiusScale = RadiusScale;
            _Instance.Value = protein;

            if (Orbit != null) Orbit.Frame(_ProteinContainer.position, _radius + 2 * RadiusScale);
        }

        #region UI
        const float PANEL_WIDTH = 320;
        public Rect PanelRect => new Rect(12, 12, PANEL_WIDTH, 132);

        void OnGUI()
        {
            GUILayout.BeginArea(PanelRect, GUI.skin.box);
            GUILayout.Label("<b>Protein Viewer</b>", new GUIStyle(GUI.skin.label) { richText = true, fontSize = 14 });

            // Enter in the text field submits.
            var _submit = Event.current.type == EventType.KeyDown
                && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter)
                && GUI.GetNameOfFocusedControl() == "PdbId";

            GUILayout.BeginHorizontal();
            GUILayout.Label("PDB ID", GUILayout.Width(50));
            GUI.SetNextControlName("PdbId");
            _Input = GUILayout.TextField(_Input ?? "", 4, GUILayout.Width(70)).ToUpperInvariant();
            GUI.enabled = !_Busy;
            if (GUILayout.Button(_Busy ? "Loading..." : "Load")) _submit = true;
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            GUILayout.Label(_Status, new GUIStyle(GUI.skin.label) { wordWrap = true });
            GUILayout.FlexibleSpace();
            GUILayout.Label("Drag: rotate   Right-drag: pan   Scroll: zoom   F: reframe",
                new GUIStyle(GUI.skin.label) { fontSize = 10 });
            GUILayout.EndArea();

            if (_submit)
            {
                if (Event.current.type == EventType.KeyDown) Event.current.Use();
                Load(_Input);
            }
        }
        #endregion
    }
}
