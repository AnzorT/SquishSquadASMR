// render() of rive-rig.js, continued: feet, ears and arms of other shapes (webbed feet, a crest, lobe ears, wings). Ported line for line like the rest of RigRender (see RigRender.cs);
// LimbFeatures lists the parts of D this file draws (RigRender.Unported refuses a creature using a part no file lists).
using System;
using System.Collections.Generic;
using static PlushCrush.Core.Rig.RigMath;

namespace PlushCrush.Core.Rig
{
    public sealed partial class RigRender
    {
        static readonly string[] LimbFeatures = { };

        void InitLimbs() { }
        /// <summary>D.web: foot i (f.x, f.y; st, cy, z, X as render() computes them); sets its key and I.W.</summary>
        void FootWeb(int i, double fx, double fy, double st, double cy, double z, double X) { }
        /// <summary>D.crest (before the ears).</summary>
        void Crest() { }
        /// <summary>Lobe ear i (e.lobe), bent by a; its masks use the skull (skD) and the body.</summary>
        void EarLobe(int i, RigEar e, double a, RigPath skD) { }
        /// <summary>D.wing: both arms as wings (instead of the paw loop).</summary>
        void Wings() { }
    }
}
