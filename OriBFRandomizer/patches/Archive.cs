using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using OriDeModLoader.Util;

namespace OriBFRandomizer.patches
{
    public static class ArchiveExt
    {
        // private static readonly Lazy<AccessTools.FieldRef<Archive, bool>> MWrite =
        //     new Lazy<AccessTools.FieldRef<Archive, bool>>(() => AccessTools.FieldRefAccess<Archive, bool>("m_write"));
        //
        // private static readonly Lazy<AccessTools.FieldRef<Archive, BinaryWriter>> MBinWriter =
        //     new Lazy<AccessTools.FieldRef<Archive, BinaryWriter>>(() =>
        //         AccessTools.FieldRefAccess<Archive, BinaryWriter>("m_binaryWriter"));
        //
        // private static readonly Lazy<AccessTools.FieldRef<Archive, BinaryReader>> MBinReader =
        //     new Lazy<AccessTools.FieldRef<Archive, BinaryReader>>(() =>
        //         AccessTools.FieldRefAccess<Archive, BinaryReader>("m_binaryReader"));


        public static void Serialize(this Archive archive, ref Dictionary<int, int> value)
        {
            // value = archive.Serialize(value);
        }


        public static Dictionary<int, int> Serialize(this Archive archive, Dictionary<int, int> value)
        {
            // string text = "";
            // if (MWrite.Value(archive))
            // {
            //     foreach (int key in value.Keys)
            //     {
            //         text = string.Concat(new string[]
            //         {
            //             text,
            //             key.ToString(),
            //             ":",
            //             value[key].ToString(),
            //             ","
            //         });
            //     }
            //
            //     text = text.TrimEnd(new char[]
            //     {
            //         ','
            //     });
            //     MBinWriter.Value(archive).Write(text);
            //     return value;
            // }
            //
            // value.Clear();
            // text = MBinReader.Value(archive).ReadString();
            // string[] array = text.Split(new char[]
            // {
            //     ','
            // });
            // for (int i = 0; i < array.Length; i++)
            // {
            //     string[] array2 = array[i].Split(new char[]
            //     {
            //         ':'
            //     });
            //     value[int.Parse(array2[0])] = int.Parse(array2[1]);
            // }

            return value;
        }
    }
}