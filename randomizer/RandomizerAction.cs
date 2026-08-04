using System;
using System.Collections.Generic;
using System.Linq;

public class RandomizerAction
{
    public static List<string> StringValPickupTypes = new List<string> {"TP", "SH", "NO", "WT", "MU", "HN", "WP", "RP", "WS", "TW", "NB", "MW"};

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
        if(Action == "MU" || Action == "RP") {
            try {
                string[] pieces = ((string)Value).Split('/');
                for(int i = 0; i < pieces.Length; i+=2) {
                    ret.Add(new RandomizerAction(pieces[i], pieces[i+1]));
                }
            } catch(Exception e) {
                Randomizer.LogError($"Malformed Multipickup {Action}|{Value}, treating as {String.Join(",", ret.Select(r=>$"{r}").ToArray())}\nError Msg: {e.Message}");
            }
        } else {
            ret.Add(this);
        }   
        return ret;
    }

    public static RandomizerAction AsMulti(List<RandomizerAction> actions, bool repeatable = false) =>
         new RandomizerAction(repeatable ? "RP" : "MU", String.Join("/", actions.Select(act => $"{act.Action}/{act.Value}").ToArray()));
}
