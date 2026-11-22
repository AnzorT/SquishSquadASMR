// render() of rive-rig.js, continued: the body's other shapes (a 3D body, fur rings, lumps, a fluffy neck, the belly patch). Ported line for line like the rest of RigRender (see RigRender.cs);
// BodyFeatures lists the parts of D this file draws (RigRender.Unported refuses a creature using a part no file lists).
using System;
using System.Collections.Generic;
using static PlushCrush.Core.Rig.RigMath;

namespace PlushCrush.Core.Rig
{
    public sealed partial class RigRender
    {
        static readonly string[] BodyFeatures = { };

        void InitBody() { }
        /// <summary>D.body3: the body as a turning ellipsoid.</summary>
        void Body3() { }
        /// <summary>D.bodyD (fur ring, I.furB): the body outline from furPts.</summary>
        void BodyFur() { }
        /// <summary>D.headD (fur ring, I.furH): the skull outline from furPts, into skD.</summary>
        void HeadFur(RigPath skD, RigMap hF) { }
        /// <summary>D.lumps (sets LumpFront).</summary>
        void Lumps() { }
        /// <summary>D.neck3.</summary>
        void Neck3() { }
        /// <summary>D.spots3, D.belly / D.bellyD / D.bellyTurn (after the whiskers in render()).</summary>
        void Belly() { }
    }
}
