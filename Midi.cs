using GisSharpBlog.NetTopologySuite.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Midi
{
    public class NoteBsp
    {
        List<Note> _notes;
        NoteBsp _leftNode;
        NoteBsp _rightNode;
        int _leftBound;
        int _rightBound;
        public void CreateNode(int x1, int x2, List<Note> nodeNotes, Song song)
        {
            _leftBound = x1;
            _rightBound = x2;
            _notes = nodeNotes;
            int middle = (x2 + x1) / 2;
            if (_notes.Count == 0 || middle - x1 < 4 * song.TicksPerBeat)
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
            _leftNode = new NoteBsp();
            _leftNode.CreateNode(x1, middle, leftNoteList, song);
            _rightNode = new NoteBsp();
            _rightNode.CreateNode(middle, x2, rightNoteList, song);
        }
        public List<Note> GetNotes(int x1, int x2, int minPitch, int maxPitch)
        {
            if (_leftNode != null && _leftNode._leftBound <= x1 && _leftNode._rightBound >= x2)
                return _leftNode.GetNotes(x1, x2, minPitch, maxPitch);
            else if (_rightNode != null && _rightNode._leftBound <= x1 && _rightNode._rightBound >= x2)
                return _rightNode.GetNotes(x1, x2, minPitch, maxPitch);
            else
            {
                List<Note> selectedNotes = new List<Note>();
                foreach (Note note in _notes)
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
        public SortedDictionary<int, List<MetaEvent>> MetaEvents { get; set; }
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

        public List<Note> GetNotes(int x1, int x2, int minPitch, int maxPitch)
        {
            if (x2 < 0 || x1 > Length)
                return new List<Note>();
            if (x1 < 0)
                x1 = 0;
            if (x2 >= Length)
                x2 = Length - 1;
            return NoteBsp.GetNotes(x1, x2, minPitch, maxPitch);
        }
        public List<Note> GetNotes(int x1, int x2)
        {
            return GetNotes(x1, x2, 0, 127);
        }

        // Notes sorted by start; last index with start <= time (stop ignored). -1 if none.
        public int GetLastStartedNoteIndexAtTime(int time)
        {
            if (Notes.Count == 0 || Notes[0].start > time)
                return -1;

            int lo = 0, hi = Notes.Count - 1, lastStart = -1;
            while (lo <= hi)
            {
                int mid = (lo + hi) / 2;
                if (Notes[mid].start <= time)
                {
                    lastStart = mid;
                    lo = mid + 1;
                }
                else
                    hi = mid - 1;
            }
            return lastStart;
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
            SetTempo(_tempo);
        }
        public void SetTempo(byte[] _tempo)
        {
            SetTempo((_tempo[0] << 16) | (_tempo[1] << 8) | _tempo[2]);
        }
        public void SetTempo(int _tempo)
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
        LinkedList<int>[,] _startOfPlayingNotes = new LinkedList<int>[16, 128];
        RunningStatus _runningStatus = new RunningStatus();
        int _chunkBytesRead;
        List<Track> _tracks;
        public List<Track> Tracks { get { return _tracks; } set { _tracks = value; } }
        List<TempoEvent> _tempoEvents;
        public List<TempoEvent> TempoEvents { get { return _tempoEvents; } set { _tempoEvents = value; } }
        public const float StartTempo = 120;
        int _formatType;
        public int FormatType { get { return _formatType; } }
        int _ticksPerBeat;
        public int TicksPerBeat { get { return _ticksPerBeat; } set { _ticksPerBeat = value; } }

        int _songLengtT;
        public int SongLengthT { get { return _songLengtT; } set { _songLengtT = value; } }
        int _minPitch;
        public int MinPitch { get { return _minPitch; } }
        int _maxPitch;
        public int MaxPitch { get { return _maxPitch; } }
        int _numPitches;
        public int NumPitches { get { return _numPitches; } }

        public Song()
        {
            for (int i = 0; i < _startOfPlayingNotes.GetLength(0); i++)
                for (int j = 0; j < _startOfPlayingNotes.GetLength(1); j++)
                    _startOfPlayingNotes[i, j] = new LinkedList<int>();
        }
        public bool IsMidiFile(string path)
        {
            using (FileStream stream = File.Open(path, FileMode.Open))
            {
                if (stream.Length < 4)
                    return false;
                using (BEBinaryReader file = new BEBinaryReader(stream))
                {
                    return file.ReadInt32() == 0x4D546864;
                }
            }
        }
        public void OpenFile(string path)
        {
            OpenMidiFile(path);
        }

        public void OpenMidiFile(string path)
        {
            using (BEBinaryReader file = new BEBinaryReader(File.Open(path, FileMode.Open)))
            {
                var fileUri = new Uri(path);
                try
                {
                    using (file)
                    {
                        //Header
                        int headerId = file.ReadInt32();
                        if (headerId != 0x4D546864)
                            throw (new FileFormatException(fileUri, "Unrecognized midi format."));
                        int headerSize = file.ReadInt32();
                        _formatType = (int)file.ReadInt16();
                        int numTracks = (int)file.ReadInt16();
                        _ticksPerBeat = (int)file.ReadInt16();
                        _songLengtT = 0;
                        _maxPitch = 0;
                        _minPitch = 127;
                        _tracks = new List<Track>();
                        _tempoEvents = new List<TempoEvent>();
                        //Track chunks
                        for (int i = 0; i < numTracks; i++)
                        {
                            _tracks.Add(new Track());
                            int chunkId = file.ReadInt32();
                            if (chunkId != 0x4D54726B)
                                throw (new FileFormatException(fileUri, "Wrong chunk id for track " + i + "."));
                            int chunkSize = file.ReadInt32();
                            _chunkBytesRead = 0;
                            int absoluteTime = 0;
                            while (_chunkBytesRead < chunkSize)
                            {
                                ReadEvent(Tracks.Last(), ref absoluteTime, file, chunkSize, fileUri);
                            }
                            if (_songLengtT < absoluteTime)
                                _songLengtT = absoluteTime;
                            if (Tracks.Last().Length < absoluteTime)
                                Tracks.Last().Length = absoluteTime;
                        }
                        if (_formatType == 0)
                        {
                            Tracks.Add(_tracks[0]);
                        }
                        _numPitches = _maxPitch - _minPitch + 1;
                    }
                }
                catch (EndOfStreamException)
                {
                    throw new FileFormatException(fileUri, "Unexpected end of file.");
                }
            }
        }

        int ReadVarLengthValue(BEBinaryReader stream)
        {
            int value = 0;
            byte b = 128;
            while ((b & 128) == 128)
            {
                b = stream.ReadByte();
                _chunkBytesRead++;
                value <<= 7;
                value |= (b & 127);
            }
            return value;
        }

        void ReadEvent(Track track, ref int absoluteTime, BEBinaryReader stream, int chunkSize, Uri fileUri)
        {
            int deltaTime = ReadVarLengthValue(stream);
            absoluteTime += deltaTime;

            byte firstByte = stream.ReadByte(); //First byte in event
            _chunkBytesRead++;
            if (firstByte == 0xff) //meta or sysex event
            {
                MetaEvent e = new MetaEvent();
                int time = absoluteTime;
                e.Type = stream.ReadByte();
                _chunkBytesRead++;
                int length = ReadVarLengthValue(stream);
                if (e.Type == 0x2f && length != 0)
                    throw (new FileFormatException(fileUri, "End-of-track event has data length of " + length + ". Should be 0."));
                e.Data = stream.ReadBytes(length);
                _chunkBytesRead += length;
                if (e.Type == 0x51) //Tempo event
                {
                    TempoEvent te = new TempoEvent(absoluteTime, e.Data);
                    if (!double.IsInfinity(te.Tempo))
                    {
                        if (_tempoEvents.Count > 0 && te.Time == _tempoEvents.Last().Time)
                            _tempoEvents[_tempoEvents.Count - 1] = te;
                        else
                            _tempoEvents.Add(te);
                    }
                }
                else if (e.Type == 0x03)
                {
                    track.Name = ASCIIEncoding.ASCII.GetString(e.Data);
                }

                if (e.Type == 0x2f && _chunkBytesRead != chunkSize)
                    throw (new FileFormatException(fileUri, "End-of-track event at byte " + _chunkBytesRead + " of " + chunkSize + "."));
                if (e.Type != 0x2f && _chunkBytesRead >= chunkSize)
                    throw (new FileFormatException(fileUri, "End-of-track event missing at end of track."));
            }
            else if (firstByte == 0xf0 || firstByte == 0xf7) //sysex
            {
                byte b;
                do
                {
                    b = stream.ReadByte();
                    _chunkBytesRead++;
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
                    chnEvent.Channel = _runningStatus.Channel = (byte)(firstByte & 0xf);
                    chnEvent.Type = _runningStatus.EventType = (byte)((firstByte >> 4) & 0xf);
                    chnEvent.Param1 = stream.ReadByte();
                    _chunkBytesRead++;
                }
                else //Running status
                {
                    chnEvent.Type = _runningStatus.EventType;
                    chnEvent.Channel = _runningStatus.Channel;
                    chnEvent.Param1 = firstByte;
                }
                if (chnEvent.Type != 0xc && chnEvent.Type != 0xd)
                {
                    chnEvent.Param2 = stream.ReadByte();
                    _chunkBytesRead++;
                }

                if (chnEvent.Type == 0x9)  //Note on/off
                {
                    //param1 = pitch, param2 = velocity

                    //Note off if velocity is 0
                    if (chnEvent.Param2 == 0)
                        chnEvent.Type = 0x8;
                    else //Note on
                    {
                        _startOfPlayingNotes[chnEvent.Channel, chnEvent.Param1].AddLast(absoluteTime);
                        if (_minPitch > chnEvent.Param1)
                            _minPitch = chnEvent.Param1;
                        if (_maxPitch < chnEvent.Param1)
                            _maxPitch = chnEvent.Param1;
                    }
                }
                if (chnEvent.Type == 0x8)  //note off
                {
                    //param1 = pitch, param2 = velocity
                    if (_startOfPlayingNotes[chnEvent.Channel, chnEvent.Param1].Count == 0)
                        return;

                    Note note = new Note();
                    note.start = _startOfPlayingNotes[chnEvent.Channel, chnEvent.Param1].First();
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
                    _startOfPlayingNotes[chnEvent.Channel, chnEvent.Param1].RemoveFirst();
                }

                if (_chunkBytesRead >= chunkSize)
                    throw (new FileFormatException(fileUri, "Error at chunk byte " + _chunkBytesRead + " of " + chunkSize + ". Last track event is a channel event. Should be meta event."));
            }
        }

        public void CreateNoteBsp()
        {
            foreach (Track track in Tracks)
            {
                track.NoteBsp = new NoteBsp();
                track.NoteBsp.CreateNode(0, SongLengthT, track.Notes, this);
            }
        }
    }
}
