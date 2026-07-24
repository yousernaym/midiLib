using System;
using System.IO;
using System.Text;

namespace Midi.Tests
{
    static class MidiTestHelpers
    {
        public static byte[] Vlq(int value)
        {
            if (value == 0) return new byte[] { 0 };
            var stack = new System.Collections.Generic.Stack<byte>();
            stack.Push((byte)(value & 0x7F));
            value >>= 7;
            while (value > 0)
            {
                stack.Push((byte)((value & 0x7F) | 0x80));
                value >>= 7;
            }
            var bytes = new byte[stack.Count];
            for (int i = 0; i < bytes.Length; i++)
                bytes[i] = stack.Pop();
            return bytes;
        }

        public static byte[] BuildMidi(params byte[][] trackChunks)
        {
            return BuildMidi(1, trackChunks);
        }

        public static byte[] BuildMidi(int formatType, params byte[][] trackChunks)
        {
            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms);
            w.Write(Encoding.ASCII.GetBytes("MThd"));
            w.Write(new byte[] { 0, 0, 0, 6, 0, (byte)formatType });
            w.Write(new byte[] { 0, (byte)trackChunks.Length, 0x01, 0xE0 }); // 480 TPB
            foreach (var track in trackChunks)
            {
                w.Write(Encoding.ASCII.GetBytes("MTrk"));
                w.Write(new byte[]
                {
                    (byte)((track.Length >> 24) & 0xFF),
                    (byte)((track.Length >> 16) & 0xFF),
                    (byte)((track.Length >> 8) & 0xFF),
                    (byte)(track.Length & 0xFF)
                });
                w.Write(track);
            }
            return ms.ToArray();
        }

        public static string WriteTempMidi(byte[] data)
        {
            string path = Path.Combine(Path.GetTempPath(), "vm_midi_" + Guid.NewGuid().ToString("N") + ".mid");
            File.WriteAllBytes(path, data);
            return path;
        }

        public static byte[] Tempo120()
        {
            return new byte[] { 0xFF, 0x51, 0x03, 0x07, 0xA1, 0x20 };
        }

        public static byte[] EndOfTrack()
        {
            return new byte[] { 0xFF, 0x2F, 0x00 };
        }
    }
}
