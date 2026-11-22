// render() of rive-rig.js, continued: the mane (spine coat, hedgehog spines, spiky or plain), the pup, collars and tags. Ported line for line like the rest of RigRender (see RigRender.cs);
// ExtraFeatures lists the parts of D this file draws (RigRender.Unported refuses a creature using a part no file lists).
using System;
using System.Collections.Generic;
using static PlushCrush.Core.Rig.RigMath;

namespace PlushCrush.Core.Rig
{
    public sealed partial class RigRender
    {
        static readonly string[] ExtraFeatures = { };

        void InitExtras() { }
        /// <summary>D.mane: E.coat / E.hog / spiky / plain.</summary>
        void Mane(double now) { }
        /// <summary>D.pup.</summary>
        void Pup(double now) { }
        /// <summary>D.collar: the ring (E.cr), or a collar on the head / body / plain, and the tag.</summary>
        void Collars(double now) { }
    }
}
