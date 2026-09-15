using System;
using UnityEngine;

namespace CatlikeCoding.TextBox {
    [Serializable]
    public sealed class TextStyle {
        public string name;

        public Color32 color;

        public BitmapFont font;

        public TextRenderer renderer;

        public float letterSpacing;

        public float fontScale;

        public bool absoluteFontScale;

        public float lineScale;

        public bool hasColor;

        public bool hasLetterSpacing;

        public bool hasFontScale;

        public bool hasLineScale;

        [NonSerialized]
        public int rendererId;

        // evenly spaced stops of a "color=a,b,.." ramp, null for a flat colour; color is the first stop
        [NonSerialized]
        public Color32[] gradient;
    }
}
