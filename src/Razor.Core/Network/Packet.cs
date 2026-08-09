#region license
// Razor: An Ultima Online Assistant
// Copyright (c) 2022 Razor Development Community on GitHub <https://github.com/markdwags/Razor>
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.
#endregion

// Portiert aus Razor CE (Razor/Network/Packet.cs, Klasse Packet).
// ENTFERNT: das statische Packet-Logging (Engine/Config/WinForms-Abhaengigkeiten,
// unsafe Log(byte*)) — TODO: bei Bedarf ueber ein eigenes Log reaktivieren.

using System;
using System.IO;
using System.Text;

namespace Assistant
{
    public enum PacketPath
    {
        ClientToServer,
        RazorToServer,
        ServerToClient,
        RazorToClient
    }

    public class Packet
    {
        private static byte[] m_Buffer = new byte[4]; // Internal format buffer.
        private MemoryStream m_Stream;
        private bool m_DynSize;
        private byte m_PacketID;

        public Packet()
        {
            m_Stream = new MemoryStream();
        }

        public Packet(byte packetID)
        {
            m_PacketID = packetID;
            m_DynSize = true;
        }

        public Packet(byte packetID, int capacity)
        {
            m_Stream = new MemoryStream(capacity);

            m_PacketID = packetID;
            m_DynSize = false;

            m_Stream.WriteByte(packetID);
        }

        public Packet(byte[] data, int len, bool dynLen)
        {
            m_Stream = new MemoryStream(len);
            m_PacketID = data[0];
            m_DynSize = dynLen;

            m_Stream.Position = 0;
            m_Stream.Write(data, 0, len);

            MoveToData();
        }

        public void EnsureCapacity(int capacity)
        {
            m_Stream = new MemoryStream(capacity);
            Write((byte) m_PacketID);
            if (m_DynSize)
                Write((short) 0);
        }

        public byte[] Compile()
        {
            if (m_DynSize)
            {
                m_Stream.Seek(1, SeekOrigin.Begin);
                Write((ushort) m_Stream.Length);
            }

            return ToArray();
        }

        public void MoveToData()
        {
            m_Stream.Position = m_DynSize ? 3 : 1;
        }

        public void Copy(Packet p)
        {
            m_Stream = new MemoryStream((int) p.Length);
            byte[] data = p.ToArray();
            m_Stream.Write(data, 0, data.Length);

            m_DynSize = p.m_DynSize;
            m_PacketID = p.m_PacketID;

            MoveToData();
        }

        public long Seek(int offset, SeekOrigin origin)
        {
            return m_Stream.Seek(offset, origin);
        }

        public int ReadInt32()
        {
            if (m_Stream.Position + 4 > m_Stream.Length)
                return 0;

            return (ReadByte() << 24)
                   | (ReadByte() << 16)
                   | (ReadByte() << 8)
                   | ReadByte();
        }

        public short ReadInt16()
        {
            if (m_Stream.Position + 2 > m_Stream.Length)
                return 0;
            return (short) ((ReadByte() << 8) | ReadByte());
        }

        public byte ReadByte()
        {
            if (m_Stream.Position + 1 > m_Stream.Length)
                return 0;
            return (byte) m_Stream.ReadByte();
        }

        public uint ReadUInt32()
        {
            if (m_Stream.Position + 4 > m_Stream.Length)
                return 0;
            return (uint) ((ReadByte() << 24)
                           | (ReadByte() << 16)
                           | (ReadByte() << 8)
                           | ReadByte());
        }

        public ushort ReadUInt16()
        {
            if (m_Stream.Position + 2 > m_Stream.Length)
                return 0;
            return (ushort) ((ReadByte() << 8) | ReadByte());
        }

        public sbyte ReadSByte()
        {
            if (m_Stream.Position + 1 > m_Stream.Length)
                return 0;
            return (sbyte) m_Stream.ReadByte();
        }

        public bool ReadBoolean()
        {
            if (m_Stream.Position + 1 > m_Stream.Length)
                return false;
            return (m_Stream.ReadByte() != 0);
        }

        public string ReadUnicodeStringLE()
        {
            StringBuilder sb = new StringBuilder();

            int c;

            while (m_Stream.Position + 1 < m_Stream.Length && (c = ReadByte() | (ReadByte() << 8)) != 0)
                sb.Append((char) c);

            return sb.ToString();
        }

        public string ReadUnicodeStringLESafe()
        {
            StringBuilder sb = new StringBuilder();

            int c;

            while (m_Stream.Position + 1 < m_Stream.Length && (c = ReadByte() | (ReadByte() << 8)) != 0)
            {
                if (IsSafeChar(c))
                    sb.Append((char) c);
            }

            return sb.ToString();
        }

        public string ReadUnicodeStringSafe()
        {
            StringBuilder sb = new StringBuilder();

            int c;

            while (m_Stream.Position + 1 < m_Stream.Length && (c = ReadUInt16()) != 0)
            {
                if (IsSafeChar(c))
                    sb.Append((char) c);
            }

            return sb.ToString();
        }

        public string ReadUnicodeString()
        {
            StringBuilder sb = new StringBuilder();

            int c;

            while (m_Stream.Position + 1 < m_Stream.Length && (c = ReadUInt16()) != 0)
                sb.Append((char) c);

            return sb.ToString();
        }

        public bool IsSafeChar(int c)
        {
            return (c >= 0x20 && c < 0xFFFE);
        }

        public string ReadUTF8StringSafe(int fixedLength)
        {
            if (m_Stream.Position >= m_Stream.Length)
                return String.Empty;

            long bound = m_Stream.Position + fixedLength;

            if (bound > m_Stream.Length)
                bound = m_Stream.Length;

            int count = 0;
            long index = m_Stream.Position;
            long start = m_Stream.Position;

            while (index < bound && ReadByte() != 0)
                ++count;

            m_Stream.Seek(start, SeekOrigin.Begin);

            index = 0;

            byte[] buffer = new byte[count];
            int value = 0;

            while (m_Stream.Position < bound && (value = ReadByte()) != 0)
                buffer[index++] = (byte) value;

            string s = Encoding.UTF8.GetString(buffer);

            bool isSafe = true;

            for (int i = 0; isSafe && i < s.Length; ++i)
                isSafe = IsSafeChar((int) s[i]);

            m_Stream.Seek(start + fixedLength, SeekOrigin.Begin);

            if (isSafe)
                return s;

            StringBuilder sb = new StringBuilder(s.Length);

            for (int i = 0; i < s.Length; ++i)
            {
                if (IsSafeChar((int) s[i]))
                    sb.Append(s[i]);
            }

            return sb.ToString();
        }

        public string ReadUTF8StringSafe()
        {
            if (m_Stream.Position >= m_Stream.Length)
                return String.Empty;

            int count = 0;
            long index = m_Stream.Position;
            long start = index;

            while (index < m_Stream.Length && ReadByte() != 0)
                ++count;

            m_Stream.Seek(start, SeekOrigin.Begin);

            index = 0;

            byte[] buffer = new byte[count];
            int value = 0;

            while (m_Stream.Position < m_Stream.Length && (value = ReadByte()) != 0)
                buffer[index++] = (byte) value;

            string s = Encoding.UTF8.GetString(buffer);

            bool isSafe = true;

            for (int i = 0; isSafe && i < s.Length; ++i)
                isSafe = IsSafeChar((int) s[i]);

            if (isSafe)
                return s;

            StringBuilder sb = new StringBuilder(s.Length);

            for (int i = 0; i < s.Length; ++i)
            {
                if (IsSafeChar((int) s[i]))
                    sb.Append(s[i]);
            }

            return sb.ToString();
        }

        public string ReadUTF8String()
        {
            if (m_Stream.Position >= m_Stream.Length)
                return String.Empty;

            int count = 0;
            long index = m_Stream.Position;
            long start = index;

            while (index < m_Stream.Length && ReadByte() != 0)
                ++count;

            m_Stream.Seek(start, SeekOrigin.Begin);

            index = 0;

            byte[] buffer = new byte[count];
            int value = 0;

            while (m_Stream.Position < m_Stream.Length && (value = ReadByte()) != 0)
                buffer[index++] = (byte) value;

            return Encoding.UTF8.GetString(buffer);
        }

        public string ReadString()
        {
            return ReadStringSafe();
        }

        public string ReadStringSafe()
        {
            StringBuilder sb = new StringBuilder();

            int c;

            while (m_Stream.Position < m_Stream.Length && (c = ReadByte()) != 0)
                sb.Append((char) c);

            return sb.ToString();
        }

        public string ReadUnicodeStringSafe(int fixedLength)
        {
            return ReadUnicodeString(fixedLength);
        }

        public string ReadUnicodeString(int fixedLength)
        {
            long bound = m_Stream.Position + (fixedLength << 1);
            long end = bound;

            if (bound > m_Stream.Length)
                bound = m_Stream.Length;

            StringBuilder sb = new StringBuilder();

            int c;

            while ((m_Stream.Position + 1) < bound && (c = ReadUInt16()) != 0)
                if (IsSafeChar(c))
                    sb.Append((char) c);

            m_Stream.Seek(end, SeekOrigin.Begin);

            return sb.ToString();
        }

        public string ReadStringSafe(int fixedLength)
        {
            return ReadString(fixedLength);
        }

        public string ReadString(int fixedLength)
        {
            long bound = m_Stream.Position + fixedLength;

            if (bound > m_Stream.Length)
                bound = m_Stream.Length;

            long end = bound;

            StringBuilder sb = new StringBuilder();

            int c;

            while (m_Stream.Position < bound && (c = ReadByte()) != 0)
                sb.Append((char) c);

            m_Stream.Seek(end, SeekOrigin.Begin);

            return sb.ToString();
        }


/////////////////////////////////////////////
        ///Packet Writer/////////////////////////////
/////////////////////////////////////////////
        public void Write(bool value)
        {
            m_Stream.WriteByte((byte) (value ? 1 : 0));
        }

        public void Write(byte value)
        {
            m_Stream.WriteByte(value);
        }

        public void Write(sbyte value)
        {
            m_Stream.WriteByte((byte) value);
        }

        public void Write(short value)
        {
            m_Buffer[0] = (byte) (value >> 8);
            m_Buffer[1] = (byte) value;

            m_Stream.Write(m_Buffer, 0, 2);
        }

        public void Write(ushort value)
        {
            m_Buffer[0] = (byte) (value >> 8);
            m_Buffer[1] = (byte) value;

            m_Stream.Write(m_Buffer, 0, 2);
        }

        public void Write(int value)
        {
            m_Buffer[0] = (byte) (value >> 24);
            m_Buffer[1] = (byte) (value >> 16);
            m_Buffer[2] = (byte) (value >> 8);
            m_Buffer[3] = (byte) value;

            m_Stream.Write(m_Buffer, 0, 4);
        }

        public void Write(uint value)
        {
            m_Buffer[0] = (byte) (value >> 24);
            m_Buffer[1] = (byte) (value >> 16);
            m_Buffer[2] = (byte) (value >> 8);
            m_Buffer[3] = (byte) value;

            m_Stream.Write(m_Buffer, 0, 4);
        }

        public void Write(byte[] buffer, int offset, int size)
        {
            m_Stream.Write(buffer, offset, size);
        }

        public void WriteAsciiFixed(string value, int size)
        {
            if (value == null)
                value = String.Empty;

            byte[] buffer = Encoding.ASCII.GetBytes(value);

            if (buffer.Length >= size)
            {
                m_Stream.Write(buffer, 0, size);
            }
            else
            {
                m_Stream.Write(buffer, 0, buffer.Length);

                byte[] pad = new byte[size - buffer.Length];

                m_Stream.Write(pad, 0, pad.Length);
            }
        }

        public void WriteAsciiNull(string value)
        {
            if (value == null)
                value = String.Empty;

            byte[] buffer = Encoding.ASCII.GetBytes(value);

            m_Stream.Write(buffer, 0, buffer.Length);
            m_Stream.WriteByte(0);
        }

        public void WriteLittleUniNull(string value)
        {
            if (value == null)
                value = String.Empty;

            byte[] buffer = Encoding.Unicode.GetBytes(value);

            m_Stream.Write(buffer, 0, buffer.Length);

            m_Buffer[0] = 0;
            m_Buffer[1] = 0;
            m_Stream.Write(m_Buffer, 0, 2);
        }

        public void WriteLittleUniFixed(string value, int size)
        {
            if (value == null)
                value = String.Empty;

            size *= 2;

            byte[] buffer = Encoding.Unicode.GetBytes(value);

            if (buffer.Length >= size)
            {
                m_Stream.Write(buffer, 0, size);
            }
            else
            {
                m_Stream.Write(buffer, 0, buffer.Length);

                byte[] pad = new byte[size - buffer.Length];

                m_Stream.Write(pad, 0, pad.Length);
            }
        }

        public void WriteBigUniNull(string value)
        {
            if (value == null)
                value = String.Empty;

            byte[] buffer = Encoding.BigEndianUnicode.GetBytes(value);

            m_Stream.Write(buffer, 0, buffer.Length);

            m_Buffer[0] = 0;
            m_Buffer[1] = 0;
            m_Stream.Write(m_Buffer, 0, 2);
        }

        public void WriteBigUniFixed(string value, int size)
        {
            if (value == null)
                value = String.Empty;

            size *= 2;

            byte[] buffer = Encoding.BigEndianUnicode.GetBytes(value);

            if (buffer.Length >= size)
            {
                m_Stream.Write(buffer, 0, size);
            }
            else
            {
                m_Stream.Write(buffer, 0, buffer.Length);

                byte[] pad = new byte[size - buffer.Length];

                m_Stream.Write(pad, 0, pad.Length);
            }
        }

        public void WriteUTF8Fixed(string value, int size)
        {
            if (value == null)
                value = String.Empty;

            size *= 2;

            byte[] buffer = Encoding.UTF8.GetBytes(value);

            if (buffer.Length >= size)
            {
                m_Stream.Write(buffer, 0, size);
            }
            else
            {
                m_Stream.Write(buffer, 0, buffer.Length);

                byte[] pad = new byte[size - buffer.Length];

                m_Stream.Write(pad, 0, pad.Length);
            }
        }

        public void WriteUTF8Null(string value)
        {
            if (value == null)
                value = String.Empty;

            byte[] buffer = Encoding.UTF8.GetBytes(value);

            m_Stream.Write(buffer, 0, buffer.Length);
            m_Buffer[0] = 0;
            m_Buffer[1] = 0;
            m_Stream.Write(m_Buffer, 0, 2);
        }

        public void Fill()
        {
            byte[] buffer = new byte[m_Stream.Capacity - Position];
            m_Stream.Write(buffer, 0, buffer.Length);
        }

        public void Fill(int length)
        {
            m_Stream.Write(new byte[length], 0, length);
        }

        public int PacketID
        {
            get { return m_PacketID; }
        }

        public long Length
        {
            get { return m_Stream.Length; }
        }

        public long Position
        {
            get { return m_Stream.Position; }
            set { m_Stream.Position = value; }
        }

        public MemoryStream UnderlyingStream
        {
            get { return m_Stream; }
        }

        public long Seek(long offset, SeekOrigin origin)
        {
            return m_Stream.Seek(offset, origin);
        }

        public byte[] ToArray()
        {
            return m_Stream.ToArray();
        }
    }
}
