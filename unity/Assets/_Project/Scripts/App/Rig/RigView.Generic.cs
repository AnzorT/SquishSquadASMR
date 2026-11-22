// The live view's side of the generic rig (rive-generic.js: the 98 creatures without a hand-made rig): their geometry
// is setup()'s result exported as data (record-generic.mjs: geo-<key>.json, "generic": true) and drawn by RigGeneric;
// the moves their mount adds (the house's '<move>@<key>' ones, which the shared rig-states.json misses) come in
// Rig/rig-states-<key>.json and are merged into the moves when the creature is first shown.
using System.Collections.Generic;
using PlushCrush.Core.Rig;
using UnityEngine;

namespace PlushCrush.App.Rig
{
    public static partial class RigLibrary
    {
        static readonly Dictionary<string, RigGenericGeo> _generic = new Dictionary<string, RigGenericGeo>();
        static readonly HashSet<string> _ownMoves = new HashSet<string>();

        /// <summary>Whether geo-<key>.json is a generic rig's (it's kept for GenericGeo).</summary>
        static bool IsGeneric(string key, string json)
        {
            if (json.IndexOf("\"generic\":true", System.StringComparison.Ordinal) < 0) return false;
            var root = (Dictionary<string, object>)MiniJson.Parse(json);
            if (!RigGenericGeo.IsGeneric(root)) return false;
            _generic[key] = RigGenericGeo.From(root);
            return true;
        }

        /// <summary>The generic rig's geometry for this creature, or null (hand-made, or not exported).</summary>
        public static RigGenericGeo GenericGeo(string key)
        {
            if (!_generic.ContainsKey(key)) Geo(key);
            return _generic.TryGetValue(key, out var g) ? g : null;
        }

        /// <summary>Merges the creature's own moves (Rig/rig-states-<key>.json) into the moves, once.</summary>
        public static void OwnMoves(string key)
        {
            var data = Data; if (data == null || !_ownMoves.Add(key)) return;
            var ta = Resources.Load<TextAsset>("Rig/rig-states-" + key);
            if (ta == null) { Debug.LogWarning("RigLibrary: Rig/rig-states-" + key + ".json is missing (run tools/unity-port/rig/record-generic.mjs " + key + ")"); return; }
            var root = (Dictionary<string, object>)MiniJson.Parse(ta.text);
            Resources.UnloadAsset(ta);
            if (root.TryGetValue("STATES", out var st) && st is Dictionary<string, object> sd)
                foreach (var kv in sd) data.States[kv.Key] = RigData.ReadState(kv.Key, kv.Value as Dictionary<string, object>);
        }
    }

    public sealed partial class RigView
    {
        RigGeneric _generic;

        /// <summary>A generic creature's renderer and drawing, mounted as the game mounts it: one frame before its art has
        /// loaded (nothing drawn), then setup() draws the first at once (RiveRig.tick1), both at the mount's time.</summary>
        RigDisplay MountGeneric(string key, double now)
        {
            var gg = RigLibrary.GenericGeo(key);
            RigLibrary.OwnMoves(key);
            Engine.Generic = true;
            _generic = new RigGeneric(gg, key);
            Engine.FromM = _generic.FromM;
            var display = new RigDisplay(RigTree.FromTree(gg.Tree, _generic.All), null) { Fx = _fx };
            Engine.Tick(now, 0.016);
            var P = Engine.Tick(now, 0.016); _generic.Render(P, Engine, now);
            return display;
        }
    }
}
