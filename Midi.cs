using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using GisSharpBlog.NetTopologySuite.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Midi
{
	public enum FileType { Midi, Mod, Sid };
	public class NoteBsp
	{
		List<Note> notes;
		NoteBsp leftNode;
		NoteBsp rightNode;
		int leftBound;
		int rightBound;
		public void createNode(int x1, int x2, List<Note> nodeNotes, Song song)
		{
			leftBound = x1;
			rightBound = x2;
			notes = nodeNotes;
			int middle = (x2+x1) / 2;
			if (notes.Count == 0 || middle - x1 < 4 * song.TicksPerBeat)
				return;
			List<Note> leftNoteList = new List<Note>();
			List<Note> rightNoteList = new List<Note>();
			foreach (Note note in nodeNotes)
			{
				if (note.start < middle && note.stop > x1)
					leftNoteList.Add(note);
				if (note.start < x2 && note.stop > middle)
					rightNoteList.Add(note);
			}
			leftNode = new NoteBsp();
			leftNode.createNode(x1, middle, leftNoteList, song);
			rightNode = new NoteBsp();
			rightNode.createNode(middle, x2, rightNoteList, song);
		}
		public List<Note> getNotes(int x1, int x2, int minPitch, int maxPitch)
		{
			if (leftNode != null && leftNode.leftBound <= x1 && leftNode.rightBound >= x2)
				return leftNode.getNotes(x1, x2, minPitch, maxPitch);
			else if (rightNode != null && rightNode.leftBound <= x1 && rightNode.rightBound >= x2)
				return rightNode.getNotes(x1, x2, minPitch, maxPitch);
			else
			{
				List<Note> selectedNotes = new List<Note>();
				foreach (Note note in notes)
				{
					if (note.pitch >= minPitch && note.pitch <= maxPitch && note.start < x2 && note.stop > x1)
						selectedNotes.Add(note);
				}
				return selectedNotes;
			}
		}
	}
	public class Event
	{
		public byte Type;
	}
	public class ChannelEvent : Event
	{
		public byte Channel;
		public byte Param1;
		public byte Param2;
	}
	public class Note
	{
		public int start;
		public int stop;
		public int channel;
		public int pitch;
		public int velocity;
	}
	public class MetaEvent : Event
	{
		public byte[] Data;
	}
	public class Track
	{
		public SortedDictionary<int,List<MetaEvent>> MetaEvents { get; set; }
		public SortedDictionary<int, List<ChannelEvent>> ChannelEvents { get; set; }
		public List<Note> Notes { get; set; }
		public NoteBsp NoteBsp { get; set; }
		public int Length { get; set; }
		public string Name { get; set; }
		public Track()
		{
			ChannelEvents = new SortedDictionary<int, List<ChannelEvent>>();
			MetaEvents = new SortedDictionary<int, List<MetaEvent>>();
			Notes = new List<Note>();
		}
		public void addEvent(int time, MetaEvent e)
		{
			if (!MetaEvents.ContainsKey(time))
				MetaEvents.Add(time, new List<MetaEvent>());
			MetaEvents[time].Add(e);
		}
		public void addEvent(int time, ChannelEvent e)
		{
			if (!ChannelEvents.ContainsKey(time))
				ChannelEvents.Add(time, new List<ChannelEvent>());
			ChannelEvents[time].Add(e);
		}
		public List<Note> getNotes(int x1, int x2, int minPitch, int maxPitch)
		{
			if (x2 < 0 || x1 > Length)
				return new List<Note>();
			if (x1 < 0)
				x1 = 0;
			if (x2 >= Length)
				x2 = Length - 1;
			return NoteBsp.getNotes(x1, x2, minPitch, maxPitch);
		}
		public List<Note> getNotes(int x1, int x2)
		{
			return getNotes(x1, x2, 0, 127);
		}

		int noteStartBeforeStopComp(Note runningNote, Note newNote)
		{
			if (runningNote.stop < newNote.start)
				return -1;
			else if (runningNote.start > newNote.start)
				return 1;
			else 
				return 0;
		}

		public int getLastNoteIndexAtTime(int time)
		{
			if (Notes[0].start > time || Notes.Last().stop < time)
				return -1;
			Note refNote = new Note();
			
			refNote.start = time;
			int index = Notes.BinarySearch(refNote, Comparer<Note>.Create(noteStartBeforeStopComp));
			if (index < 0)
				return index;
			//Find a note crossing "time"
			//int step = Notes.Count / 2;
			//int index = 0;
			//while (true)
			//{
			//	if (Notes[index].start > time)
			//		index -= step;
			//	else if (Notes[index].stop < time)
			//		index += step;
			//	else
			//		break;
			//	step /= 2;
			//	if (step == 0)
			//		return -1;
			//}

			//Find last matching note
			while (index + 1 < Notes.Count && Notes[index + 1].start < time)
				index++;
			
			return index;
		}
	}
	public class TempoEvent
	{
		public int Time { get; set; }
		public double Tempo { get; set; }
		public TempoEvent(int _time, double _tempo)
		{
			Time = _time;
			Tempo = _tempo;
		}
		public TempoEvent(int _time, int _tempo)
		{
			Time = _time;
			
		}
		public TempoEvent(int _time, byte[] _tempo)
		{
			Time = _time;
			setTempo(_tempo);
		}
		public void setTempo(byte[] _tempo)
		{
			setTempo((_tempo[0] << 16) | (_tempo[1] << 8) | _tempo[2]);
		}
		public void setTempo(int _tempo)
		{
			Tempo = (double)(60000000.0 / _tempo);
		}
	}
	public partial class Song
	{
		int[] pitchStatus = new int[128];
		byte runningType;
		byte runningChannel;
		int totalBytesRead;
		int chunkBytesRead;
		int ChunkBytesRead
		{
			get
			{
				return chunkBytesRead;
			}
			set
			{
				int dif = value - chunkBytesRead;
				chunkBytesRead = value;
				totalBytesRead += dif;
			}
		}
		
		List<Track> tracks;
		public List<Track> Tracks { get { return tracks; } set { tracks = value; } }
		List<TempoEvent> tempoEvents;
		public List<TempoEvent> TempoEvents { get { return tempoEvents; } set { tempoEvents = value; } }
		public const float StartTempo = 120;
		int formatType;
		public int FormatType { get { return formatType; } }
		int ticksPerBeat;
		public int TicksPerBeat { get { return ticksPerBeat; } set { ticksPerBeat = value; } }

		int songLengthInTicks;
		public int SongLengthT { get { return songLengthInTicks; } set { songLengthInTicks = value; } }
		int minPitch;
		public int MinPitch { get { return minPitch; } }
		int maxPitch;
		public int MaxPitch { get { return maxPitch; } }
		int numPitches;
		public int NumPitches { get { return numPitches; } }
				
		public Song()
		{
		}
		public bool isMidiFile(string path)
		{
			using (BinaryReader file = new BinaryReader(File.Open(path, FileMode.Open)))
			{
				return file.ReadInt32() == 0x4D546864;
			}
		}
		public void openFile(string path, ref string audioPath, bool modInsTrack, bool mixdown, double songLengthS, FileType noteFileType)
		{
			//if (path == null || path == "")
			//return;
			//try
			//{
			//    using (BEBinaryReader file = new BEBinaryReader(File.Open(path, FileMode.Open)))
			//    {
			//    }
			//}
			//catch(Exception e)
			//{
			//    MessageBox.Show("Couldn't open song file " + path + "\n" + e.Message);
			//}

			if (noteFileType == FileType.Midi)
				openMidiFile(path);
			else
				if (!importSongFile(path, ref audioPath, modInsTrack, mixdown, songLengthS))
					throw (new FormatException());
		}

		public void openMidiFile(string path)
		{
			BEBinaryReader file = new BEBinaryReader(File.Open(path, FileMode.Open));
			using (file)
			{
				//Header
				int headerId = file.ReadInt32();
				if (headerId != 0x4D546864)
					throw (new FormatException("Unrecognized midi format."));
				int headerSize = file.ReadInt32();
				formatType = (int)file.ReadInt16();
				int numTracks = (int)file.ReadInt16();
				ticksPerBeat = (int)file.ReadInt16();
				songLengthInTicks = 0;
				maxPitch = 0;
				minPitch = 127;
				tracks = new List<Track>();
				tempoEvents = new List<TempoEvent>();
				tempoEvents.Add(new TempoEvent(0, 120.0f));
				totalBytesRead = 14;
				//Track chunks
				for (int i = 0; i < numTracks; i++)
				{
					//if (i != 9 && i!=0)
					//continue;
					tracks.Add(new Track());
					int chunkId = file.ReadInt32();
					totalBytesRead += 4;
					if (chunkId != 0x4D54726B)
						throw (new FormatException("Wrong chunk id for track " + i + "."));
					int chunkSize = file.ReadInt32();
					totalBytesRead += 4;
					chunkBytesRead = 0;
					int absoluteTime = 0;
					while (ChunkBytesRead < chunkSize)
					{
						readEvent(Tracks.Last(), ref absoluteTime, file, chunkSize);
					}
					//totalBytesRead += chunkBytesRead;
					if (songLengthInTicks < absoluteTime)
						songLengthInTicks = absoluteTime;
					if (Tracks.Last().Length < absoluteTime)
						Tracks.Last().Length = absoluteTime;

					//if (i == 2)
					//break;
				}

				numPitches = maxPitch - minPitch + 1;
			}
		}
		
		int readVarLengthValue(BEBinaryReader stream)
		{
			int value = 0;
			byte b = 128;
			while ((b & 128) == 128)
			{
				b = stream.ReadByte();
				ChunkBytesRead++;
				value <<= 7;
				value |= (b & 127);
			}
			return value;
		}
		void splitByte(out byte value1, out byte value2, byte b)
		{
			value1 = (byte)((b >> 4) & 15);
			value2 = (byte)(b & 15);
		}
		void readEvent(Track track, ref int absoluteTime, BEBinaryReader stream, int chunkSize)
		{
			int deltaTime = readVarLengthValue(stream);
			absoluteTime += deltaTime;
					
			byte b = stream.ReadByte();
			ChunkBytesRead++;
			if (b == 0xff) //meta event
			{
				MetaEvent e = new MetaEvent();
				int time = absoluteTime;
				e.Type = stream.ReadByte();
				ChunkBytesRead++;
				int length = readVarLengthValue(stream);
				if (e.Type == 0x3f && length != 0)
					throw (new Exception("End-of-track event has data length of " + length + ". Should be 0."));
				e.Data = stream.ReadBytes(length);
				ChunkBytesRead += length;
				if (e.Type == 0x51) //Tempo event
				{
					TempoEvent te = new TempoEvent(absoluteTime, e.Data);
					if (!double.IsInfinity(te.Tempo))
						tempoEvents.Add(te);
				}
				else if (e.Type == 0x03)
				{
					track.Name = ASCIIEncoding.ASCII.GetString(e.Data);
				}
				else
					track.addEvent(time, e);
				
				if (e.Type == 0x2f && ChunkBytesRead != chunkSize)
					throw (new Exception("End-of-track event at byte "+ChunkBytesRead+" of "+chunkSize+"."));
				if (e.Type != 0x2f && ChunkBytesRead >= chunkSize)
					throw (new Exception("End-of-track event missing at end of track."));
			}
			else if (b == 0xf0 || b == 0xf7) //sysex
			{
				while (b != 0xf7)
				{
					b = stream.ReadByte();
					ChunkBytesRead++;
				}
			}
			else //Channel event
			{
				ChannelEvent e = new ChannelEvent();
				int time = absoluteTime;
				if (b > 127)
				{
					splitByte(out e.Type, out e.Channel, b);
					runningType = e.Type;
					runningChannel = e.Channel;
					e.Param1 = stream.ReadByte();
					ChunkBytesRead++;
				}
				else //Running status
				{
					e.Type = runningType;
					e.Channel = runningChannel;
					e.Param1 = b;
				}
				if (e.Type != 0xc && e.Type != 0xd)
				{
					e.Param2 = stream.ReadByte();
					ChunkBytesRead++;
				}
				
				if (e.Type == 0x9)  //Note on
				{
					if (minPitch > e.Param1)
						minPitch = e.Param1;
					if (maxPitch < e.Param1)
						maxPitch = e.Param1;
					if (e.Param2 == 0)
						e.Type = 0x8;
				}
				if (e.Type == 0x8)
				{
					if (pitchStatus[e.Param1] == -1)
						return;

					Note note = new Note();
					note.start = pitchStatus[e.Param1];
					note.stop = absoluteTime;
					note.channel = e.Channel;
					note.pitch = e.Param1;
					note.velocity = e.Param2;
					for (int i = 0; i <= track.Notes.Count; i++)
					{
						if (i == track.Notes.Count || track.Notes[i].start > note.start)
						{
							track.Notes.Insert(i, note);
							break;
						}
					}
					pitchStatus[e.Param1] = -1;
				}
				else if (e.Type == 0x9)
				{
					pitchStatus[e.Param1] = absoluteTime;
				}
				else
					track.addEvent(time, e);
				if (ChunkBytesRead >= chunkSize)
					throw (new Exception("Error at chunk byte "+ChunkBytesRead+" of "+chunkSize+". Last track event is channel event. Shouls be meta event."));

			}
		}
		public void createNoteBsp()
		{
			foreach (Track track in Tracks)
			{
				track.NoteBsp = new NoteBsp();
				track.NoteBsp.createNode(0, SongLengthT, track.Notes, this);
			}
		}
	}
}
