// Plays a RigDisplay list on the GPU into a texture the UI shows (Resources/Shaders/RigFill.shader does the drawing).
// The same steps as the check's software copy (tools/unity-port/rig/check/RigRaster.cs), which is compared pixel by
// pixel with Chrome's screenshots of the game:
//   Draw      fan pass (stencil: nonzero windings, or a union for strokes) then a cover quad that paints and clears
//   PushClip  fan pass, then ClipSet raises the level inside the shape;  PopClip  ClipPopDecr + ClearLow
//   Layers    their own multisampled texture, laid back with the Composite pass (opacity, the fabric grain)
// The finished drawing is turned to straight alpha (the UI's) in Output.
using System;
using System.Collections.Generic;
using PlushCrush.Core.Rig;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace PlushCrush.App.Rig
{
    public sealed class RigDrawer : IDisposable
    {
        const int PFanNonzero = 0, PFanUnion = 1, PCover = 2, PClipSet = 3, PClipPopDecr = 4, PClearLow = 5, PComposite = 6, PStraight = 7;

        /// <summary>The drawing, straight alpha, for a UI background.</summary>
        public RenderTexture Output { get; private set; }
        public int Width { get; private set; }
        public int Height { get; private set; }

        readonly Shader _shader;
        readonly Texture _grain;
        readonly Dictionary<int, Material> _mats = new Dictionary<int, Material>();
        readonly Dictionary<RigMesh, (Mesh mesh, int version, Vector3[] verts)> _meshes = new Dictionary<RigMesh, (Mesh, int, Vector3[])>();
        readonly List<RenderTexture> _targets = new List<RenderTexture>();   // [0] the drawing, [1..] nested layers
        readonly MaterialPropertyBlock _p = new MaterialPropertyBlock();
        readonly CommandBuffer _cmd = new CommandBuffer { name = "Rig" };
        readonly int[] _level = new int[16];
        Mesh _quad;
        RenderTextureDescriptor _desc;
        float _k, _ox, _oy;

        static readonly int IdRigToClip = Shader.PropertyToID("_RigToClip"), IdTargetSize = Shader.PropertyToID("_TargetSize"), IdUseBox = Shader.PropertyToID("_UseBox"),
            IdBox = Shader.PropertyToID("_Box"), IdPaintType = Shader.PropertyToID("_PaintType"), IdColor = Shader.PropertyToID("_Color"), IdAlpha = Shader.PropertyToID("_Alpha"),
            IdGBox = Shader.PropertyToID("_GBox"), IdGUser = Shader.PropertyToID("_GUser"), IdGRad = Shader.PropertyToID("_GRad"), IdGLin = Shader.PropertyToID("_GLin"),
            IdGOff = Shader.PropertyToID("_GOff"), IdGN = Shader.PropertyToID("_GN"), IdLayer = Shader.PropertyToID("_Layer"), IdOpacity = Shader.PropertyToID("_Opacity"),
            IdFabric = Shader.PropertyToID("_Fabric"), IdGrain = Shader.PropertyToID("_Grain"), IdGrainRect = Shader.PropertyToID("_GrainRect"), IdRef = Shader.PropertyToID("_Ref");
        static readonly int[] IdGCol = { Shader.PropertyToID("_GCol0"), Shader.PropertyToID("_GCol1"), Shader.PropertyToID("_GCol2"), Shader.PropertyToID("_GCol3") };

        public RigDrawer(Shader shader, Texture grain)
        {
            _shader = shader != null ? shader : throw new ArgumentNullException(nameof(shader), "Hidden/PlushCrush/RigFill is missing");
            _grain = grain;
            _quad = new Mesh { name = "RigQuad" };
            _quad.SetVertices(new[] { new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(1, 1, 0), new Vector3(0, 1, 0) });
            _quad.SetIndices(new[] { 0, 1, 2, 0, 2, 3 }, MeshTopology.Triangles, 0);
            _quad.bounds = new Bounds(Vector3.zero, Vector3.one * 1e6f);
        }

        /// <summary>The texture's size in pixels and where rig units land in it: pixel = rig * pxPerUnit + (ox, oy).</summary>
        public void Resize(int width, int height, float pxPerUnit, float ox, float oy)
        {
            width = Mathf.Max(1, width); height = Mathf.Max(1, height);
            _k = pxPerUnit; _ox = ox; _oy = oy;
            if (width == Width && height == Height && Output != null) return;
            ReleaseTargets();
            Width = width; Height = height;
            _desc = new RenderTextureDescriptor(width, height, GraphicsFormat.R8G8B8A8_UNorm, SystemInfo.GetGraphicsFormat(DefaultFormat.DepthStencil))
            { msaaSamples = 1, useMipMap = false, autoGenerateMips = false, bindMS = false };
            _desc.msaaSamples = Mathf.Max(1, SystemInfo.GetRenderTextureSupportedMSAASampleCount(WithMsaa(_desc, 8)));
            Output = new RenderTexture(new RenderTextureDescriptor(width, height, GraphicsFormat.R8G8B8A8_UNorm, 0) { useMipMap = false, msaaSamples = 1 })
            { name = "RigOutput", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            Output.Create();
        }

        static RenderTextureDescriptor WithMsaa(RenderTextureDescriptor d, int n) { d.msaaSamples = n; return d; }

        RenderTexture Target(int depth)
        {
            while (_targets.Count <= depth)
            {
                var rt = new RenderTexture(_desc) { name = "RigLayer" + _targets.Count, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
                rt.Create(); _targets.Add(rt);
            }
            return _targets[depth];
        }

        Material Mat(int stencilRef)
        {
            if (!_mats.TryGetValue(stencilRef, out var m))
            {
                m = new Material(_shader) { name = "Rig" + stencilRef, hideFlags = HideFlags.HideAndDontSave };
                m.SetInt(IdRef, stencilRef);
                if (_grain != null) m.SetTexture(IdGrain, _grain);
                _mats[stencilRef] = m;
            }
            return m;
        }

        Mesh Upload(RigMesh rm)
        {
            if (!_meshes.TryGetValue(rm, out var e)) { e = (new Mesh { name = "RigMesh", indexFormat = IndexFormat.UInt32 }, -1, new Vector3[Math.Max(64, rm.NV)]); e.mesh.MarkDynamic(); }
            if (e.version != rm.Version)
            {
                if (e.verts.Length < rm.NV) e.verts = new Vector3[rm.NV * 2];
                for (int i = 0; i < rm.NV; i++) e.verts[i] = new Vector3(rm.V[i * 2], rm.V[i * 2 + 1], 0);
                e.mesh.Clear(true);
                e.mesh.SetVertices(e.verts, 0, rm.NV);
                e.mesh.SetIndices(rm.T, 0, rm.NT, MeshTopology.Triangles, 0, false);
                e.mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 1e6f);
                e.version = rm.Version;
            }
            _meshes[rm] = e;
            return e.mesh;
        }

        Vector4 MeshBox(RigMesh m) => new Vector4(m.MinX, m.MinY, m.MaxX, m.MaxY);
        Vector4 FullBox => new Vector4(-_ox / _k, -_oy / _k, (Width - _ox) / _k, (Height - _oy) / _k);

        void Quad(int pass, int stencilRef, Vector4 box)
        {
            _p.SetFloat(IdUseBox, 1); _p.SetVector(IdBox, box);
            _cmd.DrawMesh(_quad, Matrix4x4.identity, Mat(stencilRef), 0, pass, _p);
        }

        void Fan(RigMesh m, int level)
        {
            _p.Clear(); _p.SetFloat(IdUseBox, 0);
            _cmd.DrawMesh(Upload(m), Matrix4x4.identity, Mat(m.Union ? (level << 4) | 1 : level << 4), 0, m.Union ? PFanUnion : PFanNonzero, _p);
        }

        void Paint(RigCmd c)
        {
            var p = c.Paint;
            _p.SetFloat(IdAlpha, c.Alpha);
            if (p.Grad == null) { _p.SetFloat(IdPaintType, 0); _p.SetVector(IdColor, new Vector4(p.Color.R, p.Color.G, p.Color.B, p.Color.A)); return; }
            var g = p.Grad;
            _p.SetFloat(IdPaintType, g.Radial ? 1 : 2);
            _p.SetVector(IdGBox, new Vector4(c.GX0, c.GY0, c.GX1, c.GY1));
            _p.SetFloat(IdGUser, g.UserSpace ? 1 : 0);
            _p.SetVector(IdGRad, new Vector4(g.Cx, g.Cy, g.R, 0));
            _p.SetVector(IdGLin, new Vector4(g.X1, g.Y1, g.X2, g.Y2));
            int n = Math.Min(4, g.Off.Length);
            var off = new Vector4(1, 1, 1, 1);
            for (int i = 0; i < 4; i++)
            {
                var col = g.Col.Length > 0 ? g.Col[Math.Min(i, g.Col.Length - 1)] : default;
                _p.SetVector(IdGCol[i], new Vector4(col.R, col.G, col.B, col.A));
                if (i < n) off[i] = g.Off[i];
            }
            _p.SetVector(IdGOff, off); _p.SetFloat(IdGN, n);
        }

        /// <summary>Draws the list into Output (records and runs a command buffer).</summary>
        public void Draw(List<RigCmd> cmds)
        {
            if (Output == null) return;
            _cmd.Clear();
            var proj = GL.GetGPUProjectionMatrix(Matrix4x4.Ortho(0, Width, Height, 0, -1, 1), true);
            _cmd.SetGlobalMatrix(IdRigToClip, proj * Matrix4x4.TRS(new Vector3(_ox, _oy, 0), Quaternion.identity, new Vector3(_k, _k, 1)));
            _cmd.SetGlobalVector(IdTargetSize, new Vector4(Width, Height, 1f / Width, 1f / Height));
            int d = 0; _level[0] = 0;
            Begin(Target(0));
            foreach (var c in cmds)
            {
                int L = _level[d];
                switch (c.Kind)
                {
                    case RigCmdKind.Draw:
                        Fan(c.Mesh, L);
                        _p.Clear(); Paint(c); Quad(PCover, L << 4, MeshBox(c.Mesh));
                        break;
                    case RigCmdKind.PushClip:
                        if (L >= 15) throw new InvalidOperationException("RigDrawer: clips nested past 15");
                        Fan(c.Mesh, L);
                        _p.Clear(); Quad(PClipSet, (L + 1) << 4, MeshBox(c.Mesh));
                        _level[d] = L + 1;
                        break;
                    case RigCmdKind.PopClip:
                        _p.Clear(); Quad(PClipPopDecr, L << 4, MeshBox(c.Mesh)); Quad(PClearLow, (L - 1) << 4, MeshBox(c.Mesh));
                        _level[d] = L - 1;
                        break;
                    case RigCmdKind.PushLayer:
                        d++; _level[d] = 0;
                        Begin(Target(d));
                        break;
                    case RigCmdKind.PopLayer:
                        var layer = Target(d); d--;
                        _cmd.ResolveAntiAliasedSurface(layer);
                        Resume(Target(d));
                        _cmd.SetGlobalTexture(IdLayer, layer);
                        _p.Clear(); _p.SetFloat(IdOpacity, c.Opacity);
                        _p.SetFloat(IdFabric, c.Filter != null ? 1 : 0);
                        if (c.Filter != null) _p.SetVector(IdGrainRect, new Vector4(c.Filter.X, c.Filter.Y, c.Filter.W, c.Filter.H));
                        Quad(PComposite, _level[d] << 4, FullBox);
                        break;
                }
            }
            var main = Target(0);
            _cmd.ResolveAntiAliasedSurface(main);
            _cmd.SetRenderTarget(Output);
            _cmd.ClearRenderTarget(false, true, Color.clear);
            _cmd.SetGlobalTexture(IdLayer, main);
            _p.Clear(); Quad(PStraight, 0, FullBox);
            Graphics.ExecuteCommandBuffer(_cmd);
        }

        // multisampled surfaces are kept (Store) when another target is drawn into, and resolved when sampled
        void Begin(RenderTexture rt)
        {
            _cmd.SetRenderTarget(rt, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
            _cmd.ClearRenderTarget(RTClearFlags.All, Color.clear, 1f, 0u);   // colour, depth and stencil
        }

        void Resume(RenderTexture rt) =>
            _cmd.SetRenderTarget(rt, RenderBufferLoadAction.Load, RenderBufferStoreAction.Store, RenderBufferLoadAction.Load, RenderBufferStoreAction.Store);

        void ReleaseTargets()
        {
            foreach (var t in _targets) if (t != null) { t.Release(); UnityEngine.Object.Destroy(t); }
            _targets.Clear();
            if (Output != null) { Output.Release(); UnityEngine.Object.Destroy(Output); Output = null; }
            Width = Height = 0;
        }

        public void Dispose()
        {
            ReleaseTargets();
            foreach (var m in _mats.Values) UnityEngine.Object.Destroy(m);
            _mats.Clear();
            foreach (var e in _meshes.Values) UnityEngine.Object.Destroy(e.mesh);
            _meshes.Clear();
            if (_quad != null) UnityEngine.Object.Destroy(_quad);
            _cmd.Release();
        }
    }
}
