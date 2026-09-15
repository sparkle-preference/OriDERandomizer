using UnityEngine;

namespace CatlikeCoding.TextBox {
    public struct CharMetaData {
        public void EraseIfVisible() {
            if (type == CharType.Visible) {
                renderer.renderedCharCount--;
            }
        }

        public void RenderIfVisible(Vector2 offset) {
            if (type == CharType.Visible) {
                var bitmapFontChar = font[id];
                if (bitmapFontChar.height == 0f) {
                    id = '□';
                }

                renderer.Add(this, offset);
            }
        }

        public void AdjustPositionInBox(Vector2 delta) {
            positionInBox.x = positionInBox.x + delta.x;
            positionInBox.y = positionInBox.y + delta.y;
        }

        public void AdjustPositionInBox(float xDelta, float yDelta) {
            positionInBox.x = positionInBox.x + xDelta;
            positionInBox.y = positionInBox.y + yDelta;
        }

        public char MarkAsStyleStatement(int unstyledIndex, Vector2 position) {
            this.unstyledIndex = unstyledIndex;
            type = CharType.Style;
            positionInBox = position;
            return id;
        }

        public void MarkAsWhitespace(int unstyledIndex, Vector2 position, ref AppliedTextStyle style) {
            this.unstyledIndex = unstyledIndex;
            type = CharType.Whitespace;
            positionInBox = position;
            font = style.font;
            scale = style.size;
        }

        public BitmapFontChar MarkAsVisible(int unstyledIndex, Vector2 position, ref AppliedTextStyle style) {
            this.unstyledIndex = unstyledIndex;
            type = CharType.Visible;
            positionInBox = position;
            color = style.color;
            font = style.font;
            scale = style.size;
            renderer = style.renderer;
            renderer.renderedCharCount++;
            return font[id];
        }

        public float After {
            get {
                if (type == CharType.Visible) {
                    return positionInBox.x + font[id].advance * scale;
                }

                if (type == CharType.Whitespace) {
                    return positionInBox.x + font.spaceAdvance * scale;
                }

                return positionInBox.x;
            }
        }

        public float HorizontalMiddle {
            get {
                return (positionInBox.x + After) * 0.5f;
            }
        }

        public char id;

        public int unstyledIndex;

        public CharType type;

        public Color32 color;

        public float scale;

        public Vector2 positionInBox;

        public BitmapFont font;

        public TextRenderer renderer;
    }
}
