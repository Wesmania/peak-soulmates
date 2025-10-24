using UnityEngine;

namespace Soulmates;

static public class Colors
{
    static public Color soulmateColor = new Color(24f / 256f, 221f / 256f, 39f / 256f);
    static public Color[] c =
    [
        new Color(215f / 256f, 33f / 256f, 33f / 256f),    // Red
        new Color(102f / 256f, 124f / 256f, 255f / 256f),  // Light blue
        new Color(233f / 256f, 208f / 256f, 80f / 256f),   // Yellow
        new Color(163f / 256f, 88f / 256f, 228f / 256f),   // Light purple
        new Color(76f / 256f, 231f / 256f, 228f / 256f),   // Cyan
        new Color(207f / 256f, 103f / 256f, 24f / 256f),   // Orange
        new Color(254f / 256f, 61f / 256f, 218f / 256f),   // Pink
        new Color(153f / 256f, 153f / 256f, 153f / 256f),  // Grey
        new Color(138f / 256f, 88f / 256f, 0f),             // Brown
        new Color(28f / 256f, 25f / 256f, 135f / 256f),    // Deep blue
    ];

    static public Color getColor(int idx)
    {
        if (idx < c.Length)
        {
            return c[idx];
        }
        return Color.white;
    }
}