using System.Collections.Generic;
using Xunit;

namespace Midi.Tests
{
    public class TrackQueryTests
    {
        static Track TrackWithNotes(params Note[] notes)
        {
            var track = new Track { Length = 10000 };
            foreach (var n in notes)
                track.Notes.Add(n);
            return track;
        }

        [Fact]
        public void GetLastNoteIndexAtTime_hits_sounding_note()
        {
            var track = TrackWithNotes(
                new Note { start = 0, stop = 100, pitch = 60 },
                new Note { start = 200, stop = 300, pitch = 62 });

            Assert.Equal(0, track.GetLastNoteIndexAtTime(50));
            Assert.Equal(1, track.GetLastNoteIndexAtTime(250));
        }

        [Fact]
        public void GetLastNoteIndexAtTime_before_first_or_after_last_returns_minus_one()
        {
            var track = TrackWithNotes(
                new Note { start = 100, stop = 200, pitch = 60 },
                new Note { start = 300, stop = 400, pitch = 62 });

            Assert.Equal(-1, track.GetLastNoteIndexAtTime(50));
            Assert.Equal(-1, track.GetLastNoteIndexAtTime(500));
        }

        [Fact]
        public void GetLastNoteIndexAtTime_overlapping_returns_last_matching()
        {
            // Sorted by start; both sound at time 75
            var track = TrackWithNotes(
                new Note { start = 0, stop = 100, pitch = 60 },
                new Note { start = 50, stop = 150, pitch = 62 });

            Assert.Equal(1, track.GetLastNoteIndexAtTime(75));
        }

        [Fact]
        public void GetLastNoteIndexAtTime_later_note_starting_exactly_at_time()
        {
            var track = TrackWithNotes(
                new Note { start = 0, stop = 100, pitch = 60 },
                new Note { start = 50, stop = 150, pitch = 62 });

            Assert.Equal(1, track.GetLastNoteIndexAtTime(50));
        }

        [Fact]
        public void GetLastNoteIndexAtTime_skips_ended_note_between_sounding()
        {
            // Note 1 has started by time 75 but already stopped; note 2 still sounds
            var track = TrackWithNotes(
                new Note { start = 0, stop = 100, pitch = 60 },
                new Note { start = 50, stop = 60, pitch = 61 },
                new Note { start = 70, stop = 90, pitch = 62 });

            Assert.Equal(2, track.GetLastNoteIndexAtTime(75));
        }

        [Fact]
        public void GetLastNoteIndexAtTime_long_note_after_shorter_later_ended()
        {
            // Last-started note has ended, but an earlier longer note still sounds
            var track = TrackWithNotes(
                new Note { start = 0, stop = 100, pitch = 60 },
                new Note { start = 50, stop = 60, pitch = 61 });

            Assert.Equal(0, track.GetLastNoteIndexAtTime(70));
        }

        [Fact]
        public void GetLastNoteIndexAtTime_long_note_in_gap_before_next_start()
        {
            // Short middle note ended; next note not started yet; long note still sounds
            var track = TrackWithNotes(
                new Note { start = 0, stop = 100, pitch = 60 },
                new Note { start = 50, stop = 60, pitch = 61 },
                new Note { start = 70, stop = 90, pitch = 62 });

            Assert.Equal(0, track.GetLastNoteIndexAtTime(65));
        }

        [Fact]
        public void GetLastNoteIndexAtTime_inclusive_stop()
        {
            var track = TrackWithNotes(
                new Note { start = 0, stop = 100, pitch = 60 },
                new Note { start = 200, stop = 300, pitch = 62 });

            Assert.Equal(0, track.GetLastNoteIndexAtTime(100));
            Assert.Equal(-1, track.GetLastNoteIndexAtTime(101));
        }

        [Fact]
        public void GetLastNoteIndexAtTime_empty_returns_minus_one()
        {
            Assert.Equal(-1, TrackWithNotes().GetLastNoteIndexAtTime(0));
        }

        [Fact]
        public void GetLastNoteIndexAtTime_gap_returns_minus_one()
        {
            var track = TrackWithNotes(
                new Note { start = 0, stop = 100, pitch = 60 },
                new Note { start = 200, stop = 300, pitch = 62 });

            Assert.Equal(-1, track.GetLastNoteIndexAtTime(150));
        }

        [Fact]
        public void GetNotes_two_arg_uses_full_pitch_range()
        {
            var song = new Song { TicksPerBeat = 480, SongLengthT = 2000 };
            var track = new Track { Length = 2000 };
            track.Notes.Add(new Note { start = 0, stop = 100, pitch = 10 });
            track.Notes.Add(new Note { start = 500, stop = 600, pitch = 120 });
            song.Tracks = new List<Track> { track };
            song.CreateNoteBsp();

            var all = track.GetNotes(0, 2000);
            Assert.Equal(2, all.Count);
        }

        [Fact]
        public void GetNotes_clamps_and_early_outs()
        {
            var song = new Song { TicksPerBeat = 480, SongLengthT = 1000 };
            var track = new Track { Length = 1000 };
            track.Notes.Add(new Note { start = 100, stop = 200, pitch = 60 });
            song.Tracks = new List<Track> { track };
            song.CreateNoteBsp();

            Assert.Empty(track.GetNotes(0, -1));
            Assert.Empty(track.GetNotes(1001, 1100));

            // x1 < 0 clamped to 0; x2 >= Length clamped to Length - 1
            var clamped = track.GetNotes(-10, 5000);
            Assert.Single(clamped);
            Assert.Equal(60, clamped[0].pitch);
        }
    }
}
