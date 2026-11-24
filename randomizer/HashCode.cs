#nullable enable

using System.Linq;

namespace System;

// Very simple HashCode.Combine implementation to give ValueTuple something to use
// (this is the algorithm Rider generates for GetHashCode impls)
public class HashCode {
    public static int Combine(params object?[] objects) {
        return objects.Aggregate(0, (current, obj) => current * 397 ^ (obj?.GetHashCode() ?? 0));
    }
}
