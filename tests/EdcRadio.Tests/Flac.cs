using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace EdcRadio.Tests
{
    internal static class Flac
    {
        public static void Write(string path, int rate, long samples, params string[] comments)
        {
            var bytes = new List<byte>(Encoding.ASCII.GetBytes("fLaC"));
            var info = new byte[34];
            info[10] = (byte)(rate >> 12);
            info[11] = (byte)(rate >> 4);
            info[12] = (byte)(((rate & 0xF) << 4) | 0x02);
            info[13] = (byte)(0xF0 | ((samples >> 32) & 0x0F));
            info[14] = (byte)(samples >> 24);
            info[15] = (byte)(samples >> 16);
            info[16] = (byte)(samples >> 8);
            info[17] = (byte)samples;
            Block(bytes, 0, comments.Length == 0, info);
            if (comments.Length > 0)
            {
                var block = new List<byte>();
                block.AddRange(BitConverter.GetBytes(4));
                block.AddRange(Encoding.ASCII.GetBytes("test"));
                block.AddRange(BitConverter.GetBytes(comments.Length));
                foreach (string c in comments)
                {
                    byte[] text = Encoding.UTF8.GetBytes(c);
                    block.AddRange(BitConverter.GetBytes(text.Length));
                    block.AddRange(text);
                }
                Block(bytes, 4, true, block.ToArray());
            }
            bytes.AddRange(new byte[16]);
            File.WriteAllBytes(path, bytes.ToArray());
        }

        private static void Block(List<byte> bytes, int type, bool last, byte[] data)
        {
            bytes.Add((byte)((last ? 0x80 : 0) | type));
            bytes.Add((byte)(data.Length >> 16));
            bytes.Add((byte)(data.Length >> 8));
            bytes.Add((byte)data.Length);
            bytes.AddRange(data);
        }
    }
}
