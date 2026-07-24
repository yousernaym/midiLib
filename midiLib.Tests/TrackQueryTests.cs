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
        public void GetLastStartedNoteIndexAtTime_hits_started_notes()
        {
            var track = TrackWithNotes(
                new Note { start = 0, stop = 100, pitch = 60 },
                new Note { start = 200, stop = 300, pitch = 62 });

            Assert.Equal(0, track.GetLastStartedNoteIndexAtTime(50));
            Assert.Equal(1, track.GetLastStartedNoteIndexAtTime(250));
        }

        [Fact]
        public void GetLastStartedNoteIndexAtTime_before_first_returns_minus_one()
        {
            var track = TrackWithNotes(
                new Note { start = 100, stop = 200, pitch = 60 },
                new Note { start = 300, stop = 400, pitch = 62 });

            Assert.Equal(-1, track.GetLastStartedNoteIndexAtTime(50));
        }

        [Fact]
        public void GetLastStartedNoteIndexAtTime_after_last_keeps_last_started()
        {
            var track = TrackWithNotes(
                new Note { start = 100, stop = 200, pitch = 60 },
                new Note { start = 300, stop = 400, pitch = 62 });

            // Past every stop — still the last note that has started
            Assert.Equal(1, track.GetLastStartedNoteIndexAtTime(500));
        }

        [Fact]
        public void GetLastStartedNoteIndexAtTime_overlapping_prefers_later_start()
        {
            var track = TrackWithNotes(
                new Note { start = 0, stop = 100, pitch = 60 },
                new Note { start = 50, stop = 150, pitch = 62 });

            Assert.Equal(1, track.GetLastStartedNoteIndexAtTime(75));
        }

        [Fact]
        public void GetLastStartedNoteIndexAtTime_same_start_prefers_highest_index()
        {
            var track = TrackWithNotes(
                new Note { start = 50, stop = 100, pitch = 60 },
                new Note { start = 50, stop = 60, pitch = 62 });

            Assert.Equal(1, track.GetLastStartedNoteIndexAtTime(55));
            // Shorter later note has ended; stay on it (last started)
            Assert.Equal(1, track.GetLastStartedNoteIndexAtTime(70));
        }

        [Fact]
        public void GetLastStartedNoteIndexAtTime_later_note_starting_exactly_at_time()
        {
            var track = TrackWithNotes(
                new Note { start = 0, stop = 100, pitch = 60 },
                new Note { start = 50, stop = 150, pitch = 62 });

            Assert.Equal(1, track.GetLastStartedNoteIndexAtTime(50));
        }

        [Fact]
        public void GetLastStartedNoteIndexAtTime_stays_on_ended_note_until_next_starts()
        {
            // C sustained, short D, then E — after D ends stay on D, not jump back to C
            var track = TrackWithNotes(
                new Note { start = 0, stop = 100, pitch = 60 },
                new Note { start = 50, stop = 60, pitch = 61 },
                new Note { start = 70, stop = 90, pitch = 62 });

            Assert.Equal(1, track.GetLastStartedNoteIndexAtTime(65));
            Assert.Equal(2, track.GetLastStartedNoteIndexAtTime(75));
        }

        [Fact]
        public void GetLastStartedNoteIndexAtTime_gap_keeps_last_started()
        {
            var track = TrackWithNotes(
                new Note { start = 0, stop = 100, pitch = 60 },
                new Note { start = 200, stop = 300, pitch = 62 });

            Assert.Equal(0, track.GetLastStartedNoteIndexAtTime(150));
        }

        [Fact]
        public void GetLastStartedNoteIndexAtTime_empty_returns_minus_one()
        {
            Assert.Equal(-1, TrackWithNotes().GetLastStartedNoteIndexAtTime(0));
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
