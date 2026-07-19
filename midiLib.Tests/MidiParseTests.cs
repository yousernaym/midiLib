using System;
using System.IO;
using System.IO.Packaging;
using System.Text;
using Xunit;

namespace Midi.Tests
{
    public class MidiParseTests
    {
        static byte[] Vlq(int value)
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

        static byte[] BuildMidi(params byte[][] trackChunks)
        {
            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms);
            w.Write(Encoding.ASCII.GetBytes("MThd"));
            // big-endian header
            w.Write(new byte[] { 0, 0, 0, 6, 0, 1 });
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

        static string WriteTempMidi(byte[] data)
        {
            string path = Path.Combine(Path.GetTempPath(), "vm_midi_" + Guid.NewGuid().ToString("N") + ".mid");
            File.WriteAllBytes(path, data);
            return path;
        }

        [Fact]
        public void OpenMidiFile_rejects_bad_header()
        {
            string path = WriteTempMidi(Encoding.ASCII.GetBytes("XXXX" + new string('\0', 20)));
            try
            {
                var song = new Song();
                Assert.ThrowsAny<FileFormatException>(() => song.OpenMidiFile(path));
            }
            finally { File.Delete(path); }
        }

        [Fact]
        public void OpenMidiFile_rejects_truncated_track()
        {
            // Valid header claiming a track, but truncated body
            var data = new byte[]
            {
                0x4D, 0x54, 0x68, 0x64, 0, 0, 0, 6, 0, 1, 0, 1, 0x01, 0xE0,
                0x4D, 0x54, 0x72, 0x6B, 0, 0, 0, 10, 0x00
            };
            string path = WriteTempMidi(data);
            try
            {
                var song = new Song();
                Assert.ThrowsAny<FileFormatException>(() => song.OpenMidiFile(path));
            }
            finally { File.Delete(path); }
        }

        [Fact]
        public void OpenMidiFile_pairs_note_on_and_off()
        {
            using var track = new MemoryStream();
            track.Write(Vlq(0)); track.Write(new byte[] { 0xFF, 0x51, 0x03, 0x07, 0xA1, 0x20 });
            track.Write(Vlq(0)); track.Write(new byte[] { 0x90, 60, 100 });
            track.Write(Vlq(240)); track.Write(new byte[] { 0x80, 60, 0 });
            track.Write(Vlq(0)); track.Write(new byte[] { 0xFF, 0x2F, 0x00 });

            string path = WriteTempMidi(BuildMidi(track.ToArray()));
            try
            {
                var song = new Song();
                song.OpenMidiFile(path);
                Assert.Single(song.Tracks[0].Notes);
                var n = song.Tracks[0].Notes[0];
                Assert.Equal(0, n.start);
                Assert.Equal(240, n.stop);
                Assert.Equal(60, n.pitch);
                Assert.Equal(0, n.channel);
            }
            finally { File.Delete(path); }
        }

        [Fact]
        public void OpenMidiFile_velocity_zero_is_note_off()
        {
            using var track = new MemoryStream();
            track.Write(Vlq(0)); track.Write(new byte[] { 0xFF, 0x51, 0x03, 0x07, 0xA1, 0x20 });
            track.Write(Vlq(0)); track.Write(new byte[] { 0x90, 64, 80 });
            track.Write(Vlq(100)); track.Write(new byte[] { 0x90, 64, 0 }); // vel 0 = off
            track.Write(Vlq(0)); track.Write(new byte[] { 0xFF, 0x2F, 0x00 });

            string path = WriteTempMidi(BuildMidi(track.ToArray()));
            try
            {
                var song = new Song();
                song.OpenMidiFile(path);
                Assert.Single(song.Tracks[0].Notes);
                Assert.Equal(100, song.Tracks[0].Notes[0].stop);
            }
            finally { File.Delete(path); }
        }

        [Fact]
        public void OpenMidiFile_running_status()
        {
            using var track = new MemoryStream();
            track.Write(Vlq(0)); track.Write(new byte[] { 0xFF, 0x51, 0x03, 0x07, 0xA1, 0x20 });
            track.Write(Vlq(0)); track.Write(new byte[] { 0x90, 60, 100 }); // status
            track.Write(Vlq(10)); track.Write(new byte[] { 62, 100 });     // running status note-on
            track.Write(Vlq(100)); track.Write(new byte[] { 0x80, 60, 0 });
            track.Write(Vlq(0)); track.Write(new byte[] { 0x80, 62, 0 });
            track.Write(Vlq(0)); track.Write(new byte[] { 0xFF, 0x2F, 0x00 });

            string path = WriteTempMidi(BuildMidi(track.ToArray()));
            try
            {
                var song = new Song();
                song.OpenMidiFile(path);
                Assert.Equal(2, song.Tracks[0].Notes.Count);
                Assert.Contains(song.Tracks[0].Notes, n => n.pitch == 60);
                Assert.Contains(song.Tracks[0].Notes, n => n.pitch == 62);
            }
            finally { File.Delete(path); }
        }

        [Fact]
        public void OpenMidiFile_tempo_and_track_name()
        {
            using var track = new MemoryStream();
            track.Write(Vlq(0)); track.Write(new byte[] { 0xFF, 0x51, 0x03, 0x07, 0xA1, 0x20 }); // 120 bpm
            var name = Encoding.ASCII.GetBytes("Lead");
            track.Write(Vlq(0)); track.Write(new byte[] { 0xFF, 0x03, (byte)name.Length }); track.Write(name);
            track.Write(Vlq(0)); track.Write(new byte[] { 0xFF, 0x2F, 0x00 });

            string path = WriteTempMidi(BuildMidi(track.ToArray()));
            try
            {
                var song = new Song();
                song.OpenMidiFile(path);
                Assert.Equal("Lead", song.Tracks[0].Name);
                Assert.NotEmpty(song.TempoEvents);
                Assert.Equal(120.0, song.TempoEvents[0].Tempo, 3);
            }
            finally { File.Delete(path); }
        }

        [Fact]
        public void OpenMidiFile_overlapping_same_pitch_stack()
        {
            // Two note-ons before first note-off on same pitch/channel → linked-list stack
            using var track = new MemoryStream();
            track.Write(Vlq(0)); track.Write(new byte[] { 0xFF, 0x51, 0x03, 0x07, 0xA1, 0x20 });
            track.Write(Vlq(0)); track.Write(new byte[] { 0x90, 60, 100 });
            track.Write(Vlq(50)); track.Write(new byte[] { 0x90, 60, 90 });
            track.Write(Vlq(50)); track.Write(new byte[] { 0x80, 60, 0 }); // closes first
            track.Write(Vlq(50)); track.Write(new byte[] { 0x80, 60, 0 }); // closes second
            track.Write(Vlq(0)); track.Write(new byte[] { 0xFF, 0x2F, 0x00 });

            string path = WriteTempMidi(BuildMidi(track.ToArray()));
            try
            {
                var song = new Song();
                song.OpenMidiFile(path);
                Assert.Equal(2, song.Tracks[0].Notes.Count);
                Assert.Equal(0, song.Tracks[0].Notes[0].start);
                Assert.Equal(100, song.Tracks[0].Notes[0].stop);
                Assert.Equal(50, song.Tracks[0].Notes[1].start);
                Assert.Equal(150, song.Tracks[0].Notes[1].stop);
            }
            finally { File.Delete(path); }
        }

        [Fact]
        public void NoteBsp_GetNotes_filters_by_range_and_pitch()
        {
            var song = new Song { TicksPerBeat = 480, SongLengthT = 2000 };
            var track = new Track { Length = 2000 };
            track.Notes.Add(new Note { start = 0, stop = 100, pitch = 60 });
            track.Notes.Add(new Note { start = 500, stop = 600, pitch = 72 });
            track.Notes.Add(new Note { start = 800, stop = 900, pitch = 40 });
            song.Tracks = new System.Collections.Generic.List<Track> { track };
            song.CreateNoteBsp();

            var inRange = track.GetNotes(400, 700, 60, 80);
            Assert.Single(inRange);
            Assert.Equal(72, inRange[0].pitch);

            var pitchFiltered = track.GetNotes(0, 2000, 0, 50);
            Assert.Single(pitchFiltered);
            Assert.Equal(40, pitchFiltered[0].pitch);
        }

        [Fact]
        public void OpenMidiFile_reads_minimal_fixture()
        {
            var song = new Song();
            song.OpenMidiFile(TestFiles.PathTo("minimal.mid"));
            Assert.Equal(480, song.TicksPerBeat);
            Assert.Equal(1, song.FormatType);
            Assert.NotEmpty(song.Tracks);
            Assert.Single(song.Tracks[0].Notes);
            Assert.Equal(60, song.Tracks[0].Notes[0].pitch);
            Assert.Equal("minimal", song.Tracks[0].Name);
            Assert.Equal(120.0, song.TempoEvents[0].Tempo, 3);
        }
    }
}
