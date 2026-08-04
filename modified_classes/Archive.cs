using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class Archive
	{
		public Archive()
		{
			MemoryStream = new MemoryStream();
		}

		public MemoryStream MemoryStream
		{
			get => m_memoryStream;
			set
			{
				if (m_memoryStream != null)
				{
					((IDisposable)m_memoryStream).Dispose();
				}
				if (m_binaryReader != null)
				{
					((IDisposable)m_binaryReader).Dispose();
				}
				if (m_binaryWriter != null)
				{
					((IDisposable)m_binaryWriter).Dispose();
				}
				m_memoryStream = value;
				m_binaryReader = new BinaryReader(m_memoryStream);
				m_binaryWriter = new BinaryWriter(m_memoryStream);
			}
		}

		public void WriteMemoryStreamToBinaryWriter(BinaryWriter binaryWriter)
		{
			binaryWriter.Write((int)MemoryStream.Length);
			MemoryStream.WriteTo(binaryWriter.BaseStream);
		}

		public void ReadMemoryStreamFromBinaryReader(BinaryReader binaryReader)
		{
			var num = binaryReader.ReadInt32();
			MemoryStream.SetLength(num);
			binaryReader.Read(MemoryStream.GetBuffer(), 0, num);
		}

		public bool Reading => !m_write;

		public bool Writing => m_write;

		public void ResetStream()
		{
			MemoryStream.Position = 0L;
		}

		public void WriteMode()
		{
			ResetStream();
			m_write = true;
		}

		public void ReadMode()
		{
			m_memoryStream.Position = 0L;
			m_write = false;
		}

		public void Serialize(ref float value)
		{
			value = Serialize(value);
		}

		public void Serialize(ref int value)
		{
			value = Serialize(value);
		}

		public void Serialize(ref bool value)
		{
			value = Serialize(value);
		}

		public void Serialize(ref string value)
		{
			value = Serialize(value);
		}

		public void Serialize(ref Vector2 value)
		{
			value = Serialize(value);
		}

		public void Serialize(ref Vector3 value)
		{
			value = Serialize(value);
		}

		public void Serialize(ref Quaternion value)
		{
			value = Serialize(value);
		}

		public void Serialize(ref Dictionary<int,int> value)
		{
			value = Serialize(value);
		}

		public float Serialize(float value)
		{
			if (m_write)
			{
				m_binaryWriter.Write(value);
				return value;
			}
			return m_binaryReader.ReadSingle();
		}

		public int Serialize(int value)
		{
			if (m_write)
			{
				m_binaryWriter.Write(value);
				return value;
			}
			return m_binaryReader.ReadInt32();
		}

		public bool Serialize(bool value)
		{
			if (m_write)
			{
				m_binaryWriter.Write(value);
				return value;
			}
			return m_binaryReader.ReadBoolean();
		}

		public string Serialize(string value)
		{
			if (m_write)
			{
				m_binaryWriter.Write(value);
				return value;
			}
			return m_binaryReader.ReadString();
		}

		public Vector2 Serialize(Vector2 value)
		{
			value.x = Serialize(value.x);
			value.y = Serialize(value.y);
			return value;
		}

		public Vector3 Serialize(Vector3 value)
		{
			value.x = Serialize(value.x);
			value.y = Serialize(value.y);
			value.z = Serialize(value.z);
			return value;
		}

		public Quaternion Serialize(Quaternion value)
		{
			value.x = Serialize(value.x);
			value.y = Serialize(value.y);
			value.z = Serialize(value.z);
			value.w = Serialize(value.w);
			return value;
		}

		public Dictionary<int,int> Serialize(Dictionary<int,int> value)
		{
			var pairs = "";
			if (m_write)
			{
				foreach(var key in value.Keys) {
					pairs += key + ":"+value[key]+",";
				}
				pairs = pairs.TrimEnd(',');

				m_binaryWriter.Write(pairs);
				return value;
			}
			value.Clear();
			pairs = m_binaryReader.ReadString();
			foreach(var pair in pairs.Split(',')) {
				var kandv = pair.Split(':');
				value[int.Parse(kandv[0])] = int.Parse(kandv[1]);
			}
			return value;
		}

		public void SerializeVersion(ref int version)
		{
		}

		private MemoryStream m_memoryStream = new MemoryStream();

		private BinaryReader m_binaryReader;

		private BinaryWriter m_binaryWriter;

		private bool m_write;
	}
