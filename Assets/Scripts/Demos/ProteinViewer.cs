using MolecularSurfaces.DataSources;
using MolecularSurfaces.Geometry;
using MolecularSurfaces.Molecules.Proteins;
using MolecularSurfaces.Rendering;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
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

        public float RadiusScale = 1;
        [Range(1, 2)] public int Resolution = 1;
        public ColorScheme Scheme = ColorScheme.Chain;

        public OrbitCamera Orbit;
        public SurfaceEffects Effects;

        static readonly string[] Examples = { "1UBQ", "1CRN", "4HHB", "2PTC", "1AON" };
        static readonly Regex _PdbId = new Regex("^[0-9][A-Za-z0-9]{3}$");

        MolecularSurfaceView _View;
        Protein _Protein;
        CancellationTokenSource _Cancel;

        string _Input;
        string _Status = "";
        bool _Busy;
        float _Progress = -1;
        bool _Failed;

        int _HoverAtom = -1;
        Vector3 _LastMouse;

        void Start()
        {
            _Input = ProteinId;
            if (Orbit == null) Orbit = FindObjectOfType<OrbitCamera>();
            if (Effects == null) Effects = FindObjectOfType<SurfaceEffects>();
            if (Orbit != null) Orbit.IsPointerBlocked = OverPanel;

            _View = new GameObject("[Surface]").AddComponent<MolecularSurfaceView>();
            _View.transform.SetParent(transform, false);

            if (LoadOnStart) Load(ProteinId);
        }

        void OnDestroy() => _Cancel?.Cancel();

        // Input.mousePosition is bottom-left origin; GUI rects are top-left.
        bool OverPanel(Vector2 mouse) => _PanelRect.Contains(new Vector2(mouse.x, Screen.height - mouse.y));

        #region Loading
        async void Load(string id)
        {
            id = (id ?? "").Trim().ToUpperInvariant();
            if (!_PdbId.IsMatch(id))
            {
                SetStatus("PDB IDs are 4 characters, starting with a digit (e.g. 1UBQ).", true);
                return;
            }

            var _token = Restart();
            _Input = id;
            SetStatus($"Downloading {id}...");
            _Progress = 0;

            try
            {
                // Download and parse off the main thread so the UI stays responsive.
                var _protein = await Task.Run(() => PDBBank.DownloadPDB(id), _token);
                _token.ThrowIfCancellationRequested();

                _Protein = _protein;
                ProteinId = id;
                await Build(_token, frame: true);
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                var _inner = e is AggregateException _agg ? _agg.Flatten().InnerException : e;
                SetStatus(_inner is HttpRequestException
                    ? $"Couldn't download {id}. Check the ID exists on rcsb.org and that you're online. (Very large entries have no .pdb file, only mmCIF.)"
                    : $"Failed to load {id}: {_inner.Message}", true);
                Debug.LogException(e);
                _Busy = false;
                _Progress = -1;
            }
        }

        async void Rebuild()
        {
            if (_Protein == null) return;
            var _token = Restart();
            try { await Build(_token, frame: false); }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                SetStatus($"Failed to build surface: {e.Message}", true);
                Debug.LogException(e);
                _Busy = false;
                _Progress = -1;
            }
        }

        async Task Build(CancellationToken token, bool frame)
        {
            var _protein = _Protein;
            var _resolution = Resolution;
            var _radiusScale = RadiusScale;

            SetStatus($"Building surface for {ProteinId} ({_protein.Atoms.Length:N0} atoms)...");
            _Progress = 0;

            // The lookup tables are built lazily and aren't thread-safe, so warm them here.
            MarchingCubes.InitCache();
            var _surface = await Task.Run(() => SurfaceBuilder.Build(_protein, _resolution, _radiusScale,
                p => _Progress = p, token), token);
            token.ThrowIfCancellationRequested();

            _HoverAtom = -1;
            _View.Show(_surface);
            _View.SetScheme(Scheme);
            // The furthest atom is usually an outlier on a loop, so frame slightly inside it.
            if (frame && Orbit != null) Orbit.Frame(transform.position, _surface.Radius * 0.85f + 2 * RadiusScale);

            _Busy = false;
            _Progress = -1;
            SetStatus($"{ProteinId}: {_surface.Chains.Length} chain{(_surface.Chains.Length == 1 ? "" : "s")}, " +
                      $"{_protein.Residues.Length:N0} residues, {_protein.Atoms.Length:N0} atoms");
        }

        /// <summary>Cancels any load or build in flight and starts a new one.</summary>
        CancellationToken Restart()
        {
            _Cancel?.Cancel();
            _Cancel = new CancellationTokenSource();
            _Busy = true;
            return _Cancel.Token;
        }

        void SetStatus(string status, bool failed = false)
        {
            _Status = status;
            _Failed = failed;
        }
        #endregion

        #region Hover
        void Update()
        {
            if (_View == null || _View.Surface == null || Camera.main == null) return;

            var _mouse = Input.mousePosition;
            var _dragging = Orbit != null && Orbit.IsDragging;
            if (_dragging || OverPanel(_mouse))
            {
                SetHover(-1);
                return;
            }

            // The model may be rotating under a still mouse, so pick every frame the
            // camera moves; picking is a cheap ray-vs-spheres test.
            if (_mouse == _LastMouse && (Orbit == null || !Orbit.AutoRotate) && Time.frameCount % 4 != 0) return;
            _LastMouse = _mouse;

            var _ray = Camera.main.ScreenPointToRay(_mouse);
            SetHover(_View.Pick(_ray, RadiusScale, out var _atom) ? _atom : -1);
        }

        void SetHover(int atom)
        {
            _HoverAtom = atom;
            _View.SetHighlight(atom < 0 ? -1 : _View.Surface.AtomResidue[atom]);
        }

        string HoverLabel()
        {
            var _surface = _View.Surface;
            var _atom = _surface.Protein.Atoms[_HoverAtom];
            var _residue = _surface.Protein.Residues[_surface.AtomResidue[_HoverAtom]];
            var _name = _residue.Type == ResidueType.Unknown ? _residue.Category.ToString() : _residue.Type.ToString().ToUpperInvariant();
            return $"{_name} {_residue.Key.ResidueNumber}{(_residue.Key.InsertCode == ' ' || _residue.Key.InsertCode == '\0' ? "" : _residue.Key.InsertCode.ToString())}" +
                   $"  ·  Chain {_residue.Key.ChainID}  ·  {_atom.Name}";
        }
        #endregion

        #region UI
        const float PANEL_WIDTH = 300;
        Rect _PanelRect = new Rect(12, 12, PANEL_WIDTH, 200);
        Vector2 _LegendScroll;

        GUIStyle _Panel, _Title, _Label, _Small, _Error, _Tooltip, _Swatch, _Header;
        Texture2D _PanelTexture, _TooltipTexture, _BarTexture, _BarFill;

        void InitStyles()
        {
            if (_Panel != null) return;

            _PanelTexture = SolidTexture(new Color(0.07f, 0.08f, 0.1f, 0.86f));
            _TooltipTexture = SolidTexture(new Color(0.02f, 0.02f, 0.03f, 0.9f));
            _BarTexture = SolidTexture(new Color(1, 1, 1, 0.12f));
            _BarFill = SolidTexture(new Color(0.45f, 0.72f, 1f, 0.95f));

            _Panel = new GUIStyle(GUI.skin.box) { padding = new RectOffset(14, 14, 12, 12) };
            _Panel.normal.background = _PanelTexture;
            _Title = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold };
            _Title.normal.textColor = Color.white;
            _Header = new GUIStyle(GUI.skin.label) { fontSize = 10, fontStyle = FontStyle.Bold, margin = new RectOffset(4, 4, 8, 2) };
            _Header.normal.textColor = new Color(0.6f, 0.68f, 0.8f);
            _Label = new GUIStyle(GUI.skin.label) { wordWrap = true };
            _Label.normal.textColor = new Color(0.86f, 0.88f, 0.92f);
            _Small = new GUIStyle(_Label) { fontSize = 10 };
            _Small.normal.textColor = new Color(0.6f, 0.64f, 0.7f);
            _Error = new GUIStyle(_Label);
            _Error.normal.textColor = new Color(1f, 0.55f, 0.5f);
            _Tooltip = new GUIStyle(GUI.skin.box) { fontSize = 12, padding = new RectOffset(8, 8, 5, 5), alignment = TextAnchor.MiddleLeft };
            _Tooltip.normal.background = _TooltipTexture;
            _Tooltip.normal.textColor = Color.white;
            _Swatch = new GUIStyle { fixedWidth = 12, fixedHeight = 12, margin = new RectOffset(4, 6, 4, 0) };
        }

        static Texture2D SolidTexture(Color color)
        {
            var _texture = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            _texture.SetPixel(0, 0, color);
            _texture.Apply();
            return _texture;
        }

        void OnGUI()
        {
            InitStyles();

            // Enter in the text field submits.
            var _submit = Event.current.type == EventType.KeyDown
                && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter)
                && GUI.GetNameOfFocusedControl() == "PdbId";

            GUILayout.BeginArea(new Rect(12, 12, PANEL_WIDTH, Screen.height - 24));
            GUILayout.BeginVertical(_Panel);

            GUILayout.Label("Protein Viewer", _Title);

            GUILayout.BeginHorizontal();
            GUILayout.Label("PDB ID", _Label, GUILayout.Width(50));
            GUI.SetNextControlName("PdbId");
            _Input = GUILayout.TextField(_Input ?? "", 4, GUILayout.Width(64)).ToUpperInvariant();
            if (GUILayout.Button("Load")) _submit = true;
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            foreach (var _example in Examples)
                if (GUILayout.Button(_example, GUILayout.MinWidth(0))) Load(_example);
            GUILayout.EndHorizontal();

            // Status and progress.
            GUILayout.Label(_Status, _Failed ? _Error : _Label);
            if (_Busy && _Progress >= 0)
            {
                var _bar = GUILayoutUtility.GetRect(PANEL_WIDTH - 28, 4);
                GUI.DrawTexture(_bar, _BarTexture);
                // Download has no progress, so show a moving segment until the build starts reporting.
                var _fill = _Progress > 0
                    ? new Rect(_bar.x, _bar.y, _bar.width * _Progress, _bar.height)
                    : new Rect(_bar.x + (_bar.width * 0.75f) * Mathf.PingPong(Time.unscaledTime * 0.8f, 1), _bar.y, _bar.width * 0.25f, _bar.height);
                GUI.DrawTexture(_fill, _BarFill);
            }

            GUILayout.Label("COLOUR BY", _Header);
            var _schemes = (ColorScheme[])Enum.GetValues(typeof(ColorScheme));
            var _schemeIndex = GUILayout.Toolbar(Array.IndexOf(_schemes, Scheme), Array.ConvertAll(_schemes, s => s.Label()));
            if (_schemes[_schemeIndex] != Scheme)
            {
                Scheme = _schemes[_schemeIndex];
                _View.SetScheme(Scheme);
            }

            GUILayout.Label("SURFACE", _Header);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Resolution", _Label, GUILayout.Width(80));
            var _resolution = GUILayout.Toolbar(Resolution - 1, new[] { "x1", "x2" }) + 1;
            GUILayout.EndHorizontal();
            if (_resolution != Resolution)
            {
                Resolution = _resolution;
                Rebuild();
            }

            if (Effects != null)
            {
                GUILayout.BeginHorizontal();
                Effects.AmbientOcclusion = GUILayout.Toggle(Effects.AmbientOcclusion, " Ambient occlusion");
                Effects.Outlines = GUILayout.Toggle(Effects.Outlines, " Outlines");
                GUILayout.EndHorizontal();
            }
            if (Orbit != null) Orbit.AutoRotate = GUILayout.Toggle(Orbit.AutoRotate, " Auto-rotate");

            DrawLegend();

            GUILayout.Space(6);
            GUILayout.Label("Drag: rotate   Right-drag: pan   Scroll: zoom   F: reframe", _Small);

            GUILayout.EndVertical();
            if (Event.current.type == EventType.Repaint)
            {
                var _last = GUILayoutUtility.GetLastRect();
                _PanelRect = new Rect(_last.x + 12, _last.y + 12, _last.width, _last.height);
            }
            GUILayout.EndArea();

            if (_HoverAtom >= 0 && _View.Surface != null) DrawTooltip(HoverLabel());

            if (_submit)
            {
                if (Event.current.type == EventType.KeyDown) Event.current.Use();
                Load(_Input);
            }
        }

        void DrawLegend()
        {
            if (_View == null || _View.Surface == null) return;

            var _entries = ColorSchemes.Legend(_View.Surface, Scheme);
            GUILayout.Label(Scheme == ColorScheme.Chain ? "CHAINS (click to show/hide)" : "LEGEND", _Header);

            // Long chain lists (e.g. 1AON has 21) scroll instead of running off screen.
            var _scroll = _entries.Count > 8;
            if (_scroll) _LegendScroll = GUILayout.BeginScrollView(_LegendScroll, GUILayout.Height(8 * 20));

            foreach (var _entry in _entries)
            {
                GUILayout.BeginHorizontal();
                var _visible = _entry.Chain < 0 || _View.IsChainVisible(_entry.Chain);

                var _swatch = GUILayoutUtility.GetRect(12, 12, _Swatch);
                var _color = _entry.Color;
                if (!_visible) _color.a = 0.25f;
                var _previous = GUI.color;
                GUI.color = _color;
                GUI.DrawTexture(_swatch, Texture2D.whiteTexture);
                GUI.color = _previous;

                if (_entry.Chain >= 0)
                {
                    var _toggled = GUILayout.Toggle(_visible, " " + _entry.Label);
                    if (_toggled != _visible) _View.SetChainVisible(_entry.Chain, _toggled);
                }
                else
                {
                    GUILayout.Label(_entry.Label, _Label);
                }
                GUILayout.EndHorizontal();
            }

            if (_scroll) GUILayout.EndScrollView();
        }

        void DrawTooltip(string text)
        {
            var _content = new GUIContent(text);
            var _size = _Tooltip.CalcSize(_content);
            var _mouse = Event.current.mousePosition;
            var _rect = new Rect(_mouse.x + 16, _mouse.y + 12, _size.x, _size.y);
            if (_rect.xMax > Screen.width) _rect.x = _mouse.x - _size.x - 8;
            if (_rect.yMax > Screen.height) _rect.y = _mouse.y - _size.y - 8;
            GUI.Label(_rect, _content, _Tooltip);
        }
        #endregion
    }
}
