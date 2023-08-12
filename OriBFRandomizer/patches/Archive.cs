using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using OriDeModLoader.Util;

namespace OriBFRandomizer.patches
{
    public static class ArchiveExt
    {
        public static void Serialize(this Archive archive, ref Dictionary<int, int> value)
        {
            value = archive.Serialize(value);
        }


        public static Dictionary<int, int> Serialize(this Archive archive, Dictionary<int, int> value)
        {
            var pairs = "";
            if ((bool)AccessTools.Field(typeof(Archive), "m_write").GetValue(archive))
            {
                foreach(var key in value.Keys) 
                    pairs += key.ToString() + ":"+value[key].ToString()+",";	
                
                pairs = pairs.TrimEnd(',');
                ((BinaryWriter)AccessTools.Field(typeof(Archive), "m_binaryWriter").GetValue(archive)).Write(pairs);
                return value;
            }
            value.Clear();
            pairs = ((BinaryReader)AccessTools.Field(typeof(Archive), "m_binaryWriter").GetValue(archive)).ReadString();
            foreach(var pair in pairs.Split(',')) {
                var kandv = pair.Split(':');
                value[int.Parse(kandv[0])] = int.Parse(kandv[1]);							
            }
            return value;
        }
    }
}