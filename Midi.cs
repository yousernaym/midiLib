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
	public enum MixdownType { None, Tparty, Internal }
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
		//public SortedDictionary<int, List<ChannelEvent>> ChannelEvents { get; set; }
		public List<Note> Notes { get; set; }
		public NoteBsp NoteBsp { get; set; }
		public int Length { get; set; }
		public string Name { get; set; }
		public Track()
		{
			//ChannelEvents = new SortedDictionary<int, List<ChannelEvent>>();
			MetaEvents = new SortedDictionary<int, List<MetaEvent>>();
			Notes = new List<Note>();
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

	class RunningStatus
	{
		public byte EventType;
		public byte Channel;
	}

	public partial class Song
	{
		LinkedList<int>[,] startOfPlayingNotes = new LinkedList<int>[16, 128];
		RunningStatus runningStatus = new RunningStatus();
		int chunkBytesRead;
		List<Track> tracks;
		public List<Track> Tracks { get { return tracks; } set { tracks = value; } }
		List<TempoEvent> tempoEvents;
		public List<TempoEvent> TempoEvents { get { return tempoEvents; } set { tempoEvents = value; } }
		public const float StartTempo = 120;
		int formatType;
		public int FormatType { get { return formatType; } }
		int ticksPerBeat;
		public int TicksPerBeat { get { return ticksPerBeat; } set { ticksPerBeat = value; } }

		int songLengtT;
		public int SongLengthT { get { return songLengtT; } set { songLengtT = value; } }
		int minPitch;
		public int MinPitch { get { return minPitch; } }
		int maxPitch;
		public int MaxPitch { get { return maxPitch; } }
		int numPitches;
		public int NumPitches { get { return numPitches; } }
				
		public Song()
		{
			for (int i = 0; i < startOfPlayingNotes.GetLength(0); i++)
				for (int j = 0; j < startOfPlayingNotes.GetLength(1); j++)
					startOfPlayingNotes[i, j] = new LinkedList<int>();
		}
		public bool isMidiFile(string path)
		{
			using (BinaryReader file = new BinaryReader(File.Open(path, FileMode.Open)))
			{
				return file.ReadInt32() == 0x4D546864;
			}
		}
		public void openFile(string path)
		{
			openMidiFile(path);
		}

		public void openMidiFile(string path)
		{
			BEBinaryReader file = new BEBinaryReader(File.Open(path, FileMode.Open));
			var pathUri = new Uri(path);
			try
			{
				using (file)
				{
					//Header
					int headerId = file.ReadInt32();
					if (headerId != 0x4D546864)
						throw (new FileFormatException(pathUri, "Unrecognized midi format."));
					int headerSize = file.ReadInt32();
					formatType = (int)file.ReadInt16();
					int numTracks = (int)file.ReadInt16();
					ticksPerBeat = (int)file.ReadInt16();
					songLengtT = 0;
					maxPitch = 0;
					minPitch = 127;
					tracks = new List<Track>();
					tempoEvents = new List<TempoEvent>();
					//Track chunks
					for (int i = 0; i < numTracks; i++)
					{
						tracks.Add(new Track());
						int chunkId = file.ReadInt32();
						if (chunkId != 0x4D54726B)
							throw (new FileFormatException(pathUri, "Wrong chunk id for track " + i + "."));
						int chunkSize = file.ReadInt32();
						chunkBytesRead = 0;
						int absoluteTime = 0;
						while (chunkBytesRead < chunkSize)
						{
							readEvent(Tracks.Last(), ref absoluteTime, file, chunkSize, pathUri);
						}
						if (songLengtT < absoluteTime)
							songLengtT = absoluteTime;
						if (Tracks.Last().Length < absoluteTime)
							Tracks.Last().Length = absoluteTime;
					}
					if (formatType == 0)
					{
						Tracks.Add(tracks[0]);
					}
					numPitches = maxPitch - minPitch + 1;
				}
			}
			catch (EndOfStreamException)
			{
				throw new FileFormatException(pathUri, "Unexpected end of file.");
			}
		}
		
		int readVarLengthValue(BEBinaryReader stream)
		{
			int value = 0;
			byte b = 128;
			while ((b & 128) == 128)
			{
				b = stream.ReadByte();
				chunkBytesRead++;
				value <<= 7;
				value |= (b & 127);
			}
			return value;
		}
		
		void readEvent(Track track, ref int absoluteTime, BEBinaryReader stream, int chunkSize, Uri fileUri)
		{
			int deltaTime = readVarLengthValue(stream);
			absoluteTime += deltaTime;
					
			byte firstByte = stream.ReadByte(); //First byte in event
			chunkBytesRead++;
			if (firstByte == 0xff) //meta or sysex event
			{
				MetaEvent e = new MetaEvent();
				int time = absoluteTime;
				e.Type = stream.ReadByte();
				chunkBytesRead++;
				int length = readVarLengthValue(stream);
				if (e.Type == 0x2f && length != 0)
					throw (new FileFormatException("End-of-track event has data length of " + length + ". Should be 0."));
				e.Data = stream.ReadBytes(length);
				chunkBytesRead += length;
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
				
				if (e.Type == 0x2f && chunkBytesRead != chunkSize)
					throw (new FileFormatException(fileUri, "End-of-track event at byte "+ chunkBytesRead + " of "+chunkSize+"."));
				if (e.Type != 0x2f && chunkBytesRead >= chunkSize)
					throw (new FileFormatException(fileUri, "End-of-track event missing at end of track."));
			}
			else if (firstByte == 0xf0 || firstByte == 0xf7) //sysex
			{
				byte b;
				do
				{
					b = stream.ReadByte();
					chunkBytesRead++;
				} while (b != 0xf7);
			}
			else //Channel event
			{
				ChannelEvent chnEvent = new ChannelEvent
				{
		
				};
				int time = absoluteTime;
				if (firstByte > 127) //Status information present
				{
					chnEvent.Channel = runningStatus.Channel = (byte)(firstByte & 0xf);
					chnEvent.Type = runningStatus.EventType = (byte)((firstByte >> 4) & 0xf);
					chnEvent.Param1 = stream.ReadByte();
					chunkBytesRead++;
				}
				else //Running status
				{
					chnEvent.Type = runningStatus.EventType;
					chnEvent.Channel = runningStatus.Channel;
					chnEvent.Param1 = firstByte;
				}
				if (chnEvent.Type != 0xc && chnEvent.Type != 0xd)
				{
					chnEvent.Param2 = stream.ReadByte();
					chunkBytesRead++;
				}
				
				if (chnEvent.Type == 0x9)  //Note on/off
				{
					//param1 = pitch, param2 = velocity

					//Note off if velocity is 0
					if (chnEvent.Param2 == 0)
						chnEvent.Type = 0x8;
					else //Note on
					{
						startOfPlayingNotes[chnEvent.Channel, chnEvent.Param1].AddLast(absoluteTime);
						if (minPitch > chnEvent.Param1)
							minPitch = chnEvent.Param1;
						if (maxPitch < chnEvent.Param1)
							maxPitch = chnEvent.Param1;
					}
				}
				if (chnEvent.Type == 0x8)  //note off
				{
					//param1 = pitch, param2 = velocity
					if (startOfPlayingNotes[chnEvent.Channel, chnEvent.Param1].Count == 0)
						return;

					Note note = new Note();
					note.start = startOfPlayingNotes[chnEvent.Channel, chnEvent.Param1].First();
					note.stop = absoluteTime;
					note.channel = chnEvent.Channel;
					note.pitch = chnEvent.Param1;
					note.velocity = chnEvent.Param2;
					for (int i = 0; i <= track.Notes.Count; i++)
					{
						if (i == track.Notes.Count || track.Notes[i].start > note.start)
						{
							track.Notes.Insert(i, note);
							break;
						}
					}
					startOfPlayingNotes[chnEvent.Channel, chnEvent.Param1].RemoveFirst();
				}

				if (chunkBytesRead >= chunkSize)
					throw (new FileFormatException(fileUri, "Error at chunk byte " + chunkBytesRead + " of "+chunkSize+". Last track event is a channel event. Should be meta event."));
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
