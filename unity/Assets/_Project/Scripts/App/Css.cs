// Reading the CSS values the exporters write (colors as the browser computes them: "rgb(255, 246, 226)",
// "rgba(20, 0, 50, 0.45)", "#e8f2ff").
using System.Globalization;
using UnityEngine;

namespace PlushCrush.App
{
    public static class Css
    {
        public static Color ToColor(string css, Color fallback = default)
        {
            if (string.IsNullOrEmpty(css)) return fallback;
            css = css.Trim();
            if (css.StartsWith("#"))
                return ColorUtility.TryParseHtmlString(css, out var c) ? c : fallback;
            int open = css.IndexOf('('), close = css.LastIndexOf(')');
            if (open < 0 || close < open) return fallback;
            var p = css.Substring(open + 1, close - open - 1).Split(',');
            if (p.Length < 3) return fallback;
            float F(int i) => float.Parse(p[i].Trim(), CultureInfo.InvariantCulture);
            return new Color(F(0) / 255f, F(1) / 255f, F(2) / 255f, p.Length > 3 ? F(3) : 1f);
        }
    }
}
