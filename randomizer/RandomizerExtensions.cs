using System;
using System.ComponentModel;
using System.Linq;

namespace RandoExts {
    public static class RandomizerExtensions {
        public static T Next<T>(this T src) where T : Enum {
            var arr = (T[])Enum.GetValues(src.GetType());
            var j = Array.IndexOf(arr, src) + 1;
            return arr.Length == j ? arr[0] : arr[j];
        }

        public static string Desc(this Enum genericEnum) {
            var genericEnumType = genericEnum.GetType();
            var memberInfo = genericEnumType.GetMember(genericEnum.ToString());
            if (memberInfo.Length > 0) {
                var attribs = memberInfo[0].GetCustomAttributes(typeof(DescriptionAttribute), false);
                if (attribs.Count() > 0) {
                    return ((DescriptionAttribute)attribs.ElementAt(0)).Description;
                }
            }

            return genericEnum.ToString();
        }
    }
}
