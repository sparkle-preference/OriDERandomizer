using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

public class RandomizerAction {
    public static List<string> StringValPickupTypes = new List<string> { "TP", "SH", "NO", "WT", "MU", "HN", "WP", "RP", "WS", "TW", "NB", "MW", "RI" };

    public RandomizerAction(string action, object value) {
        Action = action;
        Value = StringValPickupTypes.Contains(action) ? value : int.Parse((string)value);
    }

    public bool IsStringVal() => StringValPickupTypes.Contains(Action);

    public string ValAsStr() => StringValPickupTypes.Contains(Action) ? (string)Value : ((int)Value).ToString();

    public string Action;

    public object Value;

    public override string ToString() => $"{Action}|{Value}";

    public List<RandomizerAction> Decompose() {
        var ret = new List<RandomizerAction>();
        if (Action == "MU" || Action == "RP") {
            try {
                string firstPiece = null;
                var cur = new StringBuilder();

                var value = (string)Value;
                if (value == "") {
                    return ret;
                }

                for (var i = 0; i < value.Length; ++i) {
                    var c = value[i];
                    switch (c) {
                        case '/':
                            if (i < value.Length - 1 && value[i + 1] == '/') {
                                cur.Append('/');
                                ++i;
                                break;
                            }

                            if (firstPiece == null) {
                                firstPiece = cur.ToString();
                            } else {
                                ret.Add(new RandomizerAction(firstPiece, cur.ToString()));
                                firstPiece = null;
                            }

                            cur.Length = 0;
                            break;
                        default:
                            cur.Append(c);
                            break;
                    }
                }

                if (firstPiece == null) {
                    throw new ArgumentException("MU/RP Pickup doesn't have an even number of pieces");
                }

                ret.Add(new RandomizerAction(firstPiece, cur.ToString()));
            } catch (Exception e) {
                Randomizer.LogError($"Malformed Multipickup {Action}|{Value}, treating as {String.Join(",", ret.Select(r => $"{r}").ToArray())}\nError Msg: {e.Message}");
            }
        } else {
            ret.Add(this);
        }

        return ret;
    }

    public static RandomizerAction AsMulti(List<RandomizerAction> actions, bool repeatable = false) =>
        new RandomizerAction(
            repeatable ? "RP" : "MU",
            String.Join(
                "/",
                actions.Select(act => {
                    var escapedValue = act.Value.ToString().Replace("/", "//");
                    return $"{act.Action}/{escapedValue}";
                }
                ).ToArray()
            )
        );
}
