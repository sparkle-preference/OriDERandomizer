using System;
using System.ComponentModel;
using System.Linq;
using System.Reflection;

namespace RandoExts {
	public static class RandomizerExtensions
	{
	    public static T Next<T>(this T src) where T : Enum
	    {
	        T[] Arr = (T[])Enum.GetValues(src.GetType());
	        int j = Array.IndexOf(Arr, src) + 1;
	        return Arr.Length==j ? Arr[0] : Arr[j];            
	    }
	    public static string Desc(this Enum GenericEnum)
	    {
	        Type genericEnumType = GenericEnum.GetType();
	        MemberInfo[] memberInfo = genericEnumType.GetMember(GenericEnum.ToString());
	        if (memberInfo != null && memberInfo.Length > 0)
	        {
	            var _Attribs = memberInfo[0].GetCustomAttributes(typeof(DescriptionAttribute), false);
	            if (_Attribs != null && _Attribs.Count() > 0)
	            {
	                return ((DescriptionAttribute)_Attribs.ElementAt(0)).Description;
	            }
	        }
	        return GenericEnum.ToString();
	    }
	}
}
