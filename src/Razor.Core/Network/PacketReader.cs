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

// Portiert aus Razor CE (Razor/Network/Packet.cs, Klasse PacketReader).
// Abweichung: managed byte[] statt unsafe byte* (der Paket-Mirror des
// UOSagas-Clients liefert ohnehin byte[]-Kopien; das originale
// byte[]-Konstrukt in Razor CE liess den fixed-Pointer entweichen).
// GetCompressedReader nutzt System.IO.Compression.ZLibStream (net8) statt
// der CE-eigenen ZLib-Interop.

using System;
using System.IO;
using System.Text;

namespace Assistant
{
    public sealed class PacketReader
    {
        private readonly byte[] m_Data;
        private int m_Pos;
        private int m_Length;
        private bool m_Dyn;

        public PacketReader(byte[] buff, bool dyn)
        {
            m_Data = buff ?? Array.Empty<byte>();
            m_Length = m_Data.Length;
            m_Pos = 0;
            m_Dyn = dyn;
        }

        public void MoveToData()
        {
            m_Pos = m_Dyn ? 3 : 1;
        }

        /// <summary>
        /// Razor CE: Packet.GetCompressedReader — liest [u32 compLen][u32
        /// decompLen][zlib-Daten] ab der aktuellen Position, dekomprimiert und
        /// liefert einen Reader ueber die entpackten Bytes (Gump-Paket 0xDD).
        /// Der Lesezeiger springt hinter den komprimierten Block.
        /// </summary>
        public PacketReader GetCompressedReader()
        {
            int fullLen = ReadInt32();
            byte[] buff = Array.Empty<byte>();

            if (fullLen >= 4)
            {
                int destLen = ReadInt32();

                if (destLen < 0)
                    destLen = 0;

                int compLen = fullLen - 4;

                if (compLen > 0 && destLen > 0 && m_Pos + compLen <= m_Length)
                {
                    try
                    {
                        buff = new byte[destLen];

                        using var source = new MemoryStream(m_Data, m_Pos, compLen, writable: false);
                        using var zlib = new System.IO.Compression.ZLibStream(
                            source, System.IO.Compression.CompressionMode.Decompress);

                        int offset = 0, read;
                        while (offset < destLen && (read = zlib.Read(buff, offset, destLen - offset)) > 0)
                            offset += read;
                    }
                    catch
                    {
                        buff = Array.Empty<byte>();
                    }
                }

                m_Pos += compLen;
            }

            return new PacketReader(buff, false);
        }

        public int Seek(int offset, SeekOrigin origin)
        {
            switch (origin)
            {
                case SeekOrigin.End:
                    m_Pos = m_Length - offset;
                    break;
                case SeekOrigin.Current:
                    m_Pos += offset;
                    break;
                case SeekOrigin.Begin:
                    m_Pos = offset;
                    break;
            }

            if (m_Pos < 0)
                m_Pos = 0;
            else if (m_Pos > m_Length)
                m_Pos = m_Length;
            return m_Pos;
        }

        public int Length
        {
            get { return m_Length; }
        }

        public bool DynamicLength
        {
            get { return m_Dyn; }
        }

        public byte[] CopyBytes(int offset, int count)
        {
            byte[] read = new byte[count];
            for (m_Pos = offset; m_Pos < offset + count && m_Pos < m_Length; m_Pos++)
                read[m_Pos - offset] = m_Data[m_Pos];
            return read;
        }

        public byte ReadByte()
        {
            if (m_Pos + 1 > m_Length)
                return 0;
            return m_Data[m_Pos++];
        }

        public int ReadInt32()
        {
            return (ReadByte() << 24)
                   | (ReadByte() << 16)
                   | (ReadByte() << 8)
                   | ReadByte();
        }

        public short ReadInt16()
        {
            return (short) ((ReadByte() << 8) | ReadByte());
        }

        public uint ReadUInt32()
        {
            return (uint) (
                (ReadByte() << 24)
                | (ReadByte() << 16)
                | (ReadByte() << 8)
                | ReadByte());
        }

        public ulong ReadRawUInt64()
        {
            return (ulong)
                (((ulong) ReadByte() << 0)
                 | ((ulong) ReadByte() << 8)
                 | ((ulong) ReadByte() << 16)
                 | ((ulong) ReadByte() << 24)
                 | ((ulong) ReadByte() << 32)
                 | ((ulong) ReadByte() << 40)
                 | ((ulong) ReadByte() << 48)
                 | ((ulong) ReadByte() << 56));
        }

        public ushort ReadUInt16()
        {
            return (ushort) ((ReadByte() << 8) | ReadByte());
        }

        public sbyte ReadSByte()
        {
            if (m_Pos + 1 > m_Length)
                return 0;
            return (sbyte) m_Data[m_Pos++];
        }

        public bool ReadBoolean()
        {
            return (ReadByte() != 0);
        }

        public string ReadUnicodeStringLE()
        {
            return ReadUnicodeString();
        }

        public string ReadUnicodeStringLESafe()
        {
            return ReadUnicodeStringSafe();
        }

        public string ReadUnicodeStringSafe()
        {
            StringBuilder sb = new StringBuilder();

            int c;

            while (m_Pos < m_Length && (c = ReadUInt16()) != 0)
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

            while (m_Pos < m_Length && (c = ReadUInt16()) != 0)
                sb.Append((char) c);

            return sb.ToString();
        }

        public bool IsSafeChar(int c)
        {
            return (c >= 0x20 && c < 0xFFFE);
        }

        public string ReadUTF8StringSafe(int fixedLength)
        {
            if (m_Pos >= m_Length)
                return String.Empty;

            int bound = m_Pos + fixedLength;

            if (bound > m_Length)
                bound = m_Length;

            int count = 0;
            int index = m_Pos;
            int start = m_Pos;

            while (index < bound && ReadByte() != 0)
                ++count;

            Seek(start, SeekOrigin.Begin);

            index = 0;

            byte[] buffer = new byte[count];
            int value = 0;

            while (m_Pos < bound && (value = ReadByte()) != 0)
                buffer[index++] = (byte) value;

            string s = Encoding.UTF8.GetString(buffer);

            bool isSafe = true;

            for (int i = 0; isSafe && i < s.Length; ++i)
                isSafe = IsSafeChar((int) s[i]);

            Seek(start + fixedLength, SeekOrigin.Begin);

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
            if (m_Pos >= m_Length)
                return String.Empty;

            int count = 0;
            int index = m_Pos;
            int start = index;

            while (index < m_Length && ReadByte() != 0)
                ++count;

            Seek(start, SeekOrigin.Begin);

            index = 0;

            byte[] buffer = new byte[count];
            int value = 0;

            while (m_Pos < m_Length && (value = ReadByte()) != 0)
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
            if (m_Pos >= m_Length)
                return String.Empty;

            int count = 0;
            int index = m_Pos;
            int start = index;

            while (index < m_Length && ReadByte() != 0)
                ++count;

            Seek(start, SeekOrigin.Begin);

            index = 0;

            byte[] buffer = new byte[count];
            int value = 0;

            while (m_Pos < m_Length && (value = ReadByte()) != 0)
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

            while (m_Pos < m_Length && (c = ReadByte()) != 0)
                sb.Append((char) c);

            return sb.ToString();
        }

        public string ReadUnicodeStringSafe(int fixedLength)
        {
            return ReadUnicodeString(fixedLength);
        }

        public string ReadUnicodeString(int fixedLength)
        {
            int bound = m_Pos + (fixedLength << 1);
            int end = bound;

            if (bound > m_Length)
                bound = m_Length;

            StringBuilder sb = new StringBuilder();

            int c;

            while ((m_Pos + 1) < bound && (c = ReadUInt16()) != 0)
                if (IsSafeChar(c))
                    sb.Append((char) c);

            Seek(end, SeekOrigin.Begin);

            return sb.ToString();
        }

        public string ReadUnicodeStringBE(int fixedLength)
        {
            int bound = m_Pos + (fixedLength << 1);
            int end = bound;

            if (bound > m_Length)
                bound = m_Length;

            StringBuilder sb = new StringBuilder();

            int c;

            while ((m_Pos + 1) < bound)
            {
                c = (ushort) (ReadByte() | (ReadByte() << 8));
                sb.Append((char) c);
            }

            Seek(end, SeekOrigin.Begin);

            return sb.ToString();
        }

        public string ReadStringSafe(int fixedLength)
        {
            return ReadString(fixedLength);
        }

        public string ReadString(int fixedLength)
        {
            int bound = m_Pos + fixedLength;
            int end = bound;

            if (bound > m_Length)
                bound = m_Length;

            StringBuilder sb = new StringBuilder();

            int c;

            while (m_Pos < bound && (c = ReadByte()) != 0)
                sb.Append((char) c);

            Seek(end, SeekOrigin.Begin);

            return sb.ToString();
        }

        public byte PacketID
        {
            get { return m_Length > 0 ? m_Data[0] : (byte) 0; }
        }

        public int Position
        {
            get { return m_Pos; }
            set { m_Pos = value; }
        }

        public bool AtEnd
        {
            get { return m_Pos >= m_Length; }
        }
    }
}
