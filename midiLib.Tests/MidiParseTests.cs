using System;
using System.IO;
using System.IO.Packaging;
using System.Text;
using Xunit;
using static Midi.Tests.MidiTestHelpers;

namespace Midi.Tests
{
    public class MidiParseTests
    {
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
            track.Write(Vlq(0)); track.Write(Tempo120());
            track.Write(Vlq(0)); track.Write(new byte[] { 0x90, 60, 100 });
            track.Write(Vlq(240)); track.Write(new byte[] { 0x80, 60, 0 });
            track.Write(Vlq(0)); track.Write(EndOfTrack());

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
            track.Write(Vlq(0)); track.Write(Tempo120());
            track.Write(Vlq(0)); track.Write(new byte[] { 0x90, 64, 80 });
            track.Write(Vlq(100)); track.Write(new byte[] { 0x90, 64, 0 }); // vel 0 = off
            track.Write(Vlq(0)); track.Write(EndOfTrack());

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
            track.Write(Vlq(0)); track.Write(Tempo120());
            track.Write(Vlq(0)); track.Write(new byte[] { 0x90, 60, 100 }); // status
            track.Write(Vlq(10)); track.Write(new byte[] { 62, 100 });     // running status note-on
            track.Write(Vlq(100)); track.Write(new byte[] { 0x80, 60, 0 });
            track.Write(Vlq(0)); track.Write(new byte[] { 0x80, 62, 0 });
            track.Write(Vlq(0)); track.Write(EndOfTrack());

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
            track.Write(Vlq(0)); track.Write(Tempo120()); // 120 bpm
            var name = Encoding.ASCII.GetBytes("Lead");
            track.Write(Vlq(0)); track.Write(new byte[] { 0xFF, 0x03, (byte)name.Length }); track.Write(name);
            track.Write(Vlq(0)); track.Write(EndOfTrack());

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
            track.Write(Vlq(0)); track.Write(Tempo120());
            track.Write(Vlq(0)); track.Write(new byte[] { 0x90, 60, 100 });
            track.Write(Vlq(50)); track.Write(new byte[] { 0x90, 60, 90 });
            track.Write(Vlq(50)); track.Write(new byte[] { 0x80, 60, 0 }); // closes first
            track.Write(Vlq(50)); track.Write(new byte[] { 0x80, 60, 0 }); // closes second
            track.Write(Vlq(0)); track.Write(EndOfTrack());

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

        [Fact]
        public void OpenFile_reads_minimal_fixture()
        {
            var song = new Song();
            song.OpenFile(TestFiles.PathTo("minimal.mid"));
            Assert.Single(song.Tracks[0].Notes);
            Assert.Equal(60, song.Tracks[0].Notes[0].pitch);
        }

        [Fact]
        public void IsMidiFile_detects_valid_and_invalid()
        {
            var song = new Song();
            Assert.True(song.IsMidiFile(TestFiles.PathTo("minimal.mid")));

            string junk = WriteTempMidi(Encoding.ASCII.GetBytes("NOTMIDI"));
            try
            {
                Assert.False(song.IsMidiFile(junk));
            }
            finally { File.Delete(junk); }
        }

        [Fact]
        public void IsMidiFile_reads_header_big_endian()
        {
            // File bytes for "MThd" are 4D 54 68 64. A little-endian ReadInt32 would
            // yield 0x6468544D and falsely reject; BE must match 0x4D546864.
            var song = new Song();
            string beHeader = WriteTempMidi(new byte[] { 0x4D, 0x54, 0x68, 0x64 });
            string leLayout = WriteTempMidi(new byte[] { 0x64, 0x68, 0x54, 0x4D });
            try
            {
                Assert.True(song.IsMidiFile(beHeader));
                Assert.False(song.IsMidiFile(leLayout));
            }
            finally
            {
                File.Delete(beHeader);
                File.Delete(leLayout);
            }
        }

        [Fact]
        public void IsMidiFile_short_file_returns_false()
        {
            var song = new Song();
            string empty = WriteTempMidi(Array.Empty<byte>());
            string short3 = WriteTempMidi(new byte[] { 0x4D, 0x54, 0x68 });
            try
            {
                Assert.False(song.IsMidiFile(empty));
                Assert.False(song.IsMidiFile(short3));
            }
            finally
            {
                File.Delete(empty);
                File.Delete(short3);
            }
        }

        [Fact]
        public void OpenMidiFile_format_0_duplicates_track()
        {
            using var track = new MemoryStream();
            track.Write(Vlq(0)); track.Write(Tempo120());
            track.Write(Vlq(0)); track.Write(new byte[] { 0x90, 60, 100 });
            track.Write(Vlq(100)); track.Write(new byte[] { 0x80, 60, 0 });
            track.Write(Vlq(0)); track.Write(EndOfTrack());

            string path = WriteTempMidi(BuildMidi(0, track.ToArray()));
            try
            {
                var song = new Song();
                song.OpenMidiFile(path);
                Assert.Equal(0, song.FormatType);
                Assert.Equal(2, song.Tracks.Count);
                Assert.Same(song.Tracks[0], song.Tracks[1]);
                Assert.Single(song.Tracks[0].Notes);
            }
            finally { File.Delete(path); }
        }

        [Fact]
        public void OpenMidiFile_multi_track_format_1()
        {
            using var t0 = new MemoryStream();
            t0.Write(Vlq(0)); t0.Write(Tempo120());
            t0.Write(Vlq(0)); t0.Write(new byte[] { 0x90, 60, 100 });
            t0.Write(Vlq(100)); t0.Write(new byte[] { 0x80, 60, 0 });
            t0.Write(Vlq(0)); t0.Write(EndOfTrack());

            using var t1 = new MemoryStream();
            t1.Write(Vlq(0)); t1.Write(new byte[] { 0x91, 72, 90 });
            t1.Write(Vlq(200)); t1.Write(new byte[] { 0x81, 72, 0 });
            t1.Write(Vlq(0)); t1.Write(EndOfTrack());

            string path = WriteTempMidi(BuildMidi(t0.ToArray(), t1.ToArray()));
            try
            {
                var song = new Song();
                song.OpenMidiFile(path);
                Assert.Equal(2, song.Tracks.Count);
                Assert.Single(song.Tracks[0].Notes);
                Assert.Equal(60, song.Tracks[0].Notes[0].pitch);
                Assert.Single(song.Tracks[1].Notes);
                Assert.Equal(72, song.Tracks[1].Notes[0].pitch);
                Assert.Equal(1, song.Tracks[1].Notes[0].channel);
            }
            finally { File.Delete(path); }
        }

        [Fact]
        public void OpenMidiFile_non_note_channel_events_are_skipped()
        {
            using var track = new MemoryStream();
            track.Write(Vlq(0)); track.Write(Tempo120());
            track.Write(Vlq(0)); track.Write(new byte[] { 0xB0, 7, 100 });  // CC
            track.Write(Vlq(0)); track.Write(new byte[] { 0xC0, 1 });       // program
            track.Write(Vlq(0)); track.Write(new byte[] { 0xD0, 64 });      // aftertouch
            track.Write(Vlq(0)); track.Write(new byte[] { 0xE0, 0, 64 });   // pitch bend
            track.Write(Vlq(0)); track.Write(new byte[] { 0x90, 60, 100 });
            track.Write(Vlq(100)); track.Write(new byte[] { 0x80, 60, 0 });
            track.Write(Vlq(0)); track.Write(EndOfTrack());

            string path = WriteTempMidi(BuildMidi(track.ToArray()));
            try
            {
                var song = new Song();
                song.OpenMidiFile(path);
                Assert.Single(song.Tracks[0].Notes);
                Assert.Equal(60, song.Tracks[0].Notes[0].pitch);
            }
            finally { File.Delete(path); }
        }

        [Fact]
        public void OpenMidiFile_sysex_is_skipped()
        {
            using var track = new MemoryStream();
            track.Write(Vlq(0)); track.Write(Tempo120());
            track.Write(Vlq(0)); track.Write(new byte[] { 0xF0, 0x7E, 0x00, 0xF7 });
            track.Write(Vlq(0)); track.Write(new byte[] { 0x90, 60, 100 });
            track.Write(Vlq(100)); track.Write(new byte[] { 0x80, 60, 0 });
            track.Write(Vlq(0)); track.Write(EndOfTrack());

            string path = WriteTempMidi(BuildMidi(track.ToArray()));
            try
            {
                var song = new Song();
                song.OpenMidiFile(path);
                Assert.Single(song.Tracks[0].Notes);
                Assert.Equal(60, song.Tracks[0].Notes[0].pitch);
            }
            finally { File.Delete(path); }
        }

        [Fact]
        public void OpenMidiFile_orphan_note_off_is_ignored()
        {
            using var track = new MemoryStream();
            track.Write(Vlq(0)); track.Write(Tempo120());
            track.Write(Vlq(0)); track.Write(new byte[] { 0x80, 60, 0 }); // orphan off
            track.Write(Vlq(0)); track.Write(new byte[] { 0x90, 62, 100 });
            track.Write(Vlq(50)); track.Write(new byte[] { 0x80, 62, 0 });
            track.Write(Vlq(0)); track.Write(EndOfTrack());

            string path = WriteTempMidi(BuildMidi(track.ToArray()));
            try
            {
                var song = new Song();
                song.OpenMidiFile(path);
                Assert.Single(song.Tracks[0].Notes);
                Assert.Equal(62, song.Tracks[0].Notes[0].pitch);
            }
            finally { File.Delete(path); }
        }

        [Fact]
        public void OpenMidiFile_multi_byte_vlq_delta()
        {
            using var track = new MemoryStream();
            track.Write(Vlq(0)); track.Write(Tempo120());
            track.Write(Vlq(0)); track.Write(new byte[] { 0x90, 60, 100 });
            track.Write(Vlq(128)); track.Write(new byte[] { 0x80, 60, 0 }); // VLQ 0x81 0x00
            track.Write(Vlq(0)); track.Write(EndOfTrack());

            string path = WriteTempMidi(BuildMidi(track.ToArray()));
            try
            {
                var song = new Song();
                song.OpenMidiFile(path);
                Assert.Single(song.Tracks[0].Notes);
                Assert.Equal(128, song.Tracks[0].Notes[0].stop);
                Assert.Equal(128, song.SongLengthT);
            }
            finally { File.Delete(path); }
        }

        [Fact]
        public void OpenMidiFile_same_time_tempo_replaces()
        {
            using var track = new MemoryStream();
            // 120 bpm then 60 bpm at the same absolute time
            track.Write(Vlq(0)); track.Write(Tempo120());
            track.Write(Vlq(0)); track.Write(new byte[] { 0xFF, 0x51, 0x03, 0x0F, 0x42, 0x40 }); // 60 bpm
            track.Write(Vlq(0)); track.Write(EndOfTrack());

            string path = WriteTempMidi(BuildMidi(track.ToArray()));
            try
            {
                var song = new Song();
                song.OpenMidiFile(path);
                Assert.Single(song.TempoEvents);
                Assert.Equal(60.0, song.TempoEvents[0].Tempo, 3);
            }
            finally { File.Delete(path); }
        }

        [Fact]
        public void OpenMidiFile_sets_pitch_range_and_song_length()
        {
            using var track = new MemoryStream();
            track.Write(Vlq(0)); track.Write(Tempo120());
            track.Write(Vlq(0)); track.Write(new byte[] { 0x90, 40, 100 });
            track.Write(Vlq(0)); track.Write(new byte[] { 0x90, 80, 100 });
            track.Write(Vlq(200)); track.Write(new byte[] { 0x80, 40, 0 });
            track.Write(Vlq(0)); track.Write(new byte[] { 0x80, 80, 0 });
            track.Write(Vlq(0)); track.Write(EndOfTrack());

            string path = WriteTempMidi(BuildMidi(track.ToArray()));
            try
            {
                var song = new Song();
                song.OpenMidiFile(path);
                Assert.Equal(40, song.MinPitch);
                Assert.Equal(80, song.MaxPitch);
                Assert.Equal(41, song.NumPitches);
                Assert.Equal(200, song.SongLengthT);
            }
            finally { File.Delete(path); }
        }

        [Fact]
        public void OpenMidiFile_rejects_wrong_track_chunk_id()
        {
            using var ms = new MemoryStream();
            using (var w = new BinaryWriter(ms, Encoding.ASCII, leaveOpen: true))
            {
                w.Write(Encoding.ASCII.GetBytes("MThd"));
                w.Write(new byte[] { 0, 0, 0, 6, 0, 1, 0, 1, 0x01, 0xE0 });
                w.Write(Encoding.ASCII.GetBytes("XXXX"));
                w.Write(new byte[] { 0, 0, 0, 4 });
                w.Write(new byte[] { 0x00, 0xFF, 0x2F, 0x00 });
            }
            string path = WriteTempMidi(ms.ToArray());
            try
            {
                var song = new Song();
                Assert.ThrowsAny<FileFormatException>(() => song.OpenMidiFile(path));
            }
            finally { File.Delete(path); }
        }

        [Fact]
        public void OpenMidiFile_rejects_eot_with_nonzero_length()
        {
            using var track = new MemoryStream();
            track.Write(Vlq(0)); track.Write(Tempo120());
            track.Write(Vlq(0)); track.Write(new byte[] { 0xFF, 0x2F, 0x01, 0x00 });

            string path = WriteTempMidi(BuildMidi(track.ToArray()));
            try
            {
                var song = new Song();
                Assert.ThrowsAny<FileFormatException>(() => song.OpenMidiFile(path));
            }
            finally { File.Delete(path); }
        }

        [Fact]
        public void OpenMidiFile_rejects_eot_not_at_end_of_chunk()
        {
            using var track = new MemoryStream();
            track.Write(Vlq(0)); track.Write(EndOfTrack());
            // Extra bytes after EOT still counted in chunk size
            track.Write(Vlq(0)); track.Write(new byte[] { 0xFF, 0x01, 0x01, 0x41 });

            string path = WriteTempMidi(BuildMidi(track.ToArray()));
            try
            {
                var song = new Song();
                Assert.ThrowsAny<FileFormatException>(() => song.OpenMidiFile(path));
            }
            finally { File.Delete(path); }
        }

        [Fact]
        public void OpenMidiFile_rejects_missing_eot()
        {
            using var track = new MemoryStream();
            track.Write(Vlq(0)); track.Write(Tempo120());
            track.Write(Vlq(0)); track.Write(new byte[] { 0xFF, 0x01, 0x01, 0x41 });
            // Chunk ends on a non-EOT meta event

            string path = WriteTempMidi(BuildMidi(track.ToArray()));
            try
            {
                var song = new Song();
                Assert.ThrowsAny<FileFormatException>(() => song.OpenMidiFile(path));
            }
            finally { File.Delete(path); }
        }

        [Fact]
        public void OpenMidiFile_rejects_last_event_is_channel()
        {
            using var track = new MemoryStream();
            track.Write(Vlq(0)); track.Write(new byte[] { 0x90, 60, 100 });
            // Chunk ends on a channel event — no EOT

            string path = WriteTempMidi(BuildMidi(track.ToArray()));
            try
            {
                var song = new Song();
                Assert.ThrowsAny<FileFormatException>(() => song.OpenMidiFile(path));
            }
            finally { File.Delete(path); }
        }
    }
}
