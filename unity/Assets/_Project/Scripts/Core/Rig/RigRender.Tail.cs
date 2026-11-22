// render() of rive-rig.js, continued: the tapered solid tail (D.tailTaper: tailF, its shade tailS and the shade's clip). Ported line for line like the rest of RigRender (see RigRender.cs);
// TailFeatures lists the parts of D this file draws (RigRender.Unported refuses a creature using a part no file lists).
using System;
using System.Collections.Generic;
using static PlushCrush.Core.Rig.RigMath;

namespace PlushCrush.Core.Rig
{
    public sealed partial class RigRender
    {
        static readonly string[] TailFeatures = { };

        void InitTail() { }
        /// <summary>D.tailTaper, after tail / tailH: the spine sp3 and the tube width tw.</summary>
        void TailTaper((double x, double y)[] sp3, double tw) { }
    }
}
