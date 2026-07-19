using GisSharpBlog.NetTopologySuite.IO;
using System.IO;
using Xunit;

namespace Midi.Tests
{
    public class BEBinaryReaderTests
    {
        [Fact]
        public void ReadInt16_reads_big_endian()
        {
            using var reader = new BEBinaryReader(new MemoryStream(new byte[] { 0x01, 0xE0 }));
            Assert.Equal(480, reader.ReadInt16());
        }

        [Fact]
        public void ReadInt32_reads_big_endian()
        {
            using var reader = new BEBinaryReader(new MemoryStream(new byte[] { 0x4D, 0x54, 0x68, 0x64 }));
            Assert.Equal(0x4D546864, reader.ReadInt32());
        }

        [Fact]
        public void ReadUInt16_reads_big_endian()
        {
            using var reader = new BEBinaryReader(new MemoryStream(new byte[] { 0xFF, 0xFE }));
            Assert.Equal(0xFFFE, reader.ReadUInt16());
        }
    }
}
