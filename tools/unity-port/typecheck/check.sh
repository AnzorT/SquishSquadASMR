#!/bin/bash
# Type-check the Unity game code. Errors that mention only Unity 6 APIs (missing from the 2021.3 reference assemblies)
# are counted apart; any other error fails the check.
cd "$(dirname "$0")"
UNITY6='backgroundSize|BackgroundSize|backgroundPosition|BackgroundPosition|painter2D|Painter2D|LineCap|QuadraticCurveTo|Angle|unitySliceScale|lineWidth|strokeColor|fillColor|Arc\b|BeginPath|MoveTo|LineTo|Stroke\(|Fill\('
out=$(dotnet build -nologo -v q 2>&1 | grep -E "error CS" | sed -E 's#^.*/Scripts/#Scripts/#' | sort -u)
other=$(echo "$out" | grep -v -E "$UNITY6" | grep . )
u6=$(echo "$out" | grep -E "$UNITY6" | grep -c .)
echo "Unity 6-only APIs (not in the 2021.3 assemblies): $u6 uses"
if [ -n "$other" ]; then echo "ERRORS:"; echo "$other"; exit 1; fi
echo "no other errors"
