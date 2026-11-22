// render() of rive-rig.js, continued: the face's other parts (cheek tufts and eye domes, faceExtra, the 3D snout, headExtra, the crown). Ported line for line like the rest of RigRender (see RigRender.cs);
// FaceFeatures lists the parts of D this file draws (RigRender.Unported refuses a creature using a part no file lists).
using System;
using System.Collections.Generic;
using static PlushCrush.Core.Rig.RigMath;

namespace PlushCrush.Core.Rig
{
    public sealed partial class RigRender
    {
        static readonly string[] FaceFeatures = { };

        void InitFace() { }
        /// <summary>D.tufts (after the muzzle).</summary>
        void Tufts() { }
        /// <summary>D.faceExtra (E.fx).</summary>
        void FaceExtra() { }
        /// <summary>D.snout (before the nose; sets SnoutBehind, W["nose"]).</summary>
        void Snout() { }
        /// <summary>The map eye i is painted with (D.eyeDome: its tuft's).</summary>
        RigMap EyeFace(int i) => _face;
        /// <summary>D.headExtra (E.hx).</summary>
        void HeadExtra() { }
        /// <summary>D.crown.</summary>
        void Crown() { }
    }
}
