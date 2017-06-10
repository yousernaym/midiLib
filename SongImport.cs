using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.IO;
using System.Windows.Forms;

namespace Midi
{
	[StructLayout(LayoutKind.Sequential, Pack = 8)]
	struct Marshal_TempoEvent
	{
		public int time;
		public double tempo;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 8)]
	struct Marshal_Note
	{
		public int start;
		public int stop;
		public int pitch;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 8)]
	struct Marshal_Track
	{
		public IntPtr name;
		public IntPtr notes;
		public int numNotes;
	}

    enum Marshal_SongType { Mod, Sid };

    [StructLayout(LayoutKind.Sequential, Pack = 8)]
	struct Marshal_Song
	{
		public IntPtr tempoEvents;
		public int numTempoEvents;
        public int ticksPerBeat;
        public int songLengthT;
		public int minPitch;
		public int maxPitch;
		public IntPtr tracks;
		public int numTracks;
        public Marshal_SongType songType;
	}

	public partial class Song
	{
		//static string modWavFileName = Path.GetDirectoryName(Application.ExecutablePath) + "\\music.wav";
		//static public string ModWavFileName
		//{
		//    get { return modWavFileName; }
		//    //set { modWavFileName = value; }
		//}
		static string mixdownDir = "";
		[DllImport("NoteExtractor.dll", EntryPoint = "initLib", CallingConvention = CallingConvention.Cdecl)]
		static extern void _initLib();
		[DllImport("NoteExtractor.dll", CallingConvention = CallingConvention.Cdecl)]
		public static extern void exitLib();
		[DllImport("NoteExtractor.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
		static extern bool loadFile(string path, out Marshal_Song marSong, string mixdownPath, bool modInsTrack, double songLengthS);
		[DllImport("NoteExtractor.dll", EntryPoint = "getMixdownPath", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
		static extern IntPtr getMixdownPath_intptr();
		public static string getMixdownPath()
		{
			return Marshal.PtrToStringAnsi(getMixdownPath_intptr());
		}
		public static void initLib(string _mixdownDir)
		{
			mixdownDir = _mixdownDir;
			if (!Directory.Exists(mixdownDir))
				Directory.CreateDirectory(mixdownDir);
			_initLib();
		}
		public static void deleteMixdowns()
		{
			if (Directory.Exists(mixdownDir))
			{
				foreach (string file in Directory.GetFiles(mixdownDir))
					File.Delete(file);
			}
		}
		public static void deleteMixdownDir()
		{
			deleteMixdowns();
			if (Directory.Exists(mixdownDir))
				Directory.Delete(mixdownDir);
		}

		bool importSongFile(string path, ref string audioPath, bool modInsTrack, bool mixdown, double songLengthS)
		{
            //bool mixdown = audioPath == null || audioPath == "";
			Marshal_Song marSong;
			string mixdownPath = mixdown ? Path.Combine(mixdownDir, Path.GetFileName(path))+".wav" : null;
			if (!loadFile(path, out marSong, mixdownPath, modInsTrack, songLengthS))
				return false;

			if (mixdown)
				audioPath = mixdownPath;

			ticksPerBeat = marSong.ticksPerBeat;
			songLengthInTicks = marSong.songLengthT;
			maxPitch = marSong.maxPitch;
			minPitch = marSong.minPitch;
			numPitches = maxPitch - minPitch + 1;
			tempoEvents = new List<TempoEvent>(marSong.numTempoEvents);
			for (int i = 0; i < marSong.numTempoEvents; i++)
			{
				Marshal_TempoEvent mte = new Marshal_TempoEvent();
				mte = (Marshal_TempoEvent)Marshal.PtrToStructure(marSong.tempoEvents, typeof(Marshal_TempoEvent));
				tempoEvents.Add(new TempoEvent(mte.time, mte.tempo));
                marSong.tempoEvents += Marshal.SizeOf(typeof(Marshal_TempoEvent));
			}
			tracks = new List<Track>(marSong.numTracks);
			for (int i = 0; i < marSong.numTracks; i++)
			{
				Marshal_Track mt = new Marshal_Track();
				mt = (Marshal_Track)Marshal.PtrToStructure(marSong.tracks, typeof(Marshal_Track));
				tracks.Add(new Track());
				tracks[i].Name = Marshal.PtrToStringAnsi(mt.name);
				tracks[i].Notes = new List<Note>(mt.numNotes);
				tracks[i].Length = marSong.songLengthT;
				for (int j = 0; j < mt.numNotes; j++)
				{
					Marshal_Note mn = new Marshal_Note();
					mn = (Marshal_Note)Marshal.PtrToStructure(mt.notes, typeof(Marshal_Note));
					tracks[i].Notes.Add(new Note());
					tracks[i].Notes[j].pitch = mn.pitch;
					tracks[i].Notes[j].start = mn.start;
					tracks[i].Notes[j].stop = mn.stop;
					mt.notes += Marshal.SizeOf(typeof(Marshal_Note));
				}
                marSong.tracks += Marshal.SizeOf(typeof(Marshal_Track));
			}
			return true;
		}
	}
}
