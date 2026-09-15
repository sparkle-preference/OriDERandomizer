using UnityEngine;

namespace CatlikeCoding.TextBox {
    public struct AppliedTextStyle {
        public void Apply(TextStyle style, TextRenderer renderer) {
            color = style.color;
            gradient = style.gradient;
            font = style.font;
            letterSpacing = style.letterSpacing;
            size = style.fontScale;
            lineHeight = size * style.lineScale;
            lineDescent.baseline = font.baseline * lineHeight;
            lineDescent.baselineToBottom = font.baselineToBottom * lineHeight;
            this.renderer = renderer;
        }

        public void ApplyOnTop(TextStyle style) {
            var refreshDescent = false;
            if (style.hasColor) {
                color = style.color;
                gradient = style.gradient;
            }

            if (style.font != null) {
                font = style.font;
                refreshDescent = true;
            }

            if (style.hasLetterSpacing) {
                letterSpacing = style.letterSpacing;
            }

            if (style.hasFontScale) {
                if (style.absoluteFontScale) {
                    size = style.fontScale;
                } else {
                    size *= style.fontScale;
                }
            }

            if (style.hasLineScale) {
                lineHeight = size * style.lineScale;
                refreshDescent = true;
            }

            if (refreshDescent) {
                lineDescent.baseline = font.baseline * lineHeight;
                lineDescent.baselineToBottom = font.baselineToBottom * lineHeight;
            }
        }

        public Color32 color;

        // the ramp color is the first stop of, null when the colour is flat
        public Color32[] gradient;

        public BitmapFont font;

        public TextRenderer renderer;

        public float size;

        public float letterSpacing;

        public float lineHeight;

        public LineDescent lineDescent;
    }
}
