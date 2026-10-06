#nullable enable

using System.Collections.Generic;
using System.ComponentModel;

// Methods that are part of newer .net versions
// Implemented as extension methods since we can't modify the StdLib source
public static class StdLibExtensions {
    extension<TK, TV>(KeyValuePair<TK, TV> kvp) {
        [EditorBrowsable(EditorBrowsableState.Never)]
        public void Deconstruct(out TK key, out TV value) {
            key = kvp.Key;
            value = kvp.Value;
        }
    }
}
