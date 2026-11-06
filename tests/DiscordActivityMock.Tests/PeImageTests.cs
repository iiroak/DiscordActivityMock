using System;
using System.IO;
using Xunit;

namespace DiscordActivityMockV2.Tests
{
    public class PeImageTests
    {
        internal static byte[] BuildPe(ushort machine, int peOffset = 0x80, bool dosSignature = true,
                                       uint peSignature = 0x00004550, int length = 0x100)
        {
            var bytes = new byte[length];
            if (dosSignature)
            {
                bytes[0] = (byte)'M';
                bytes[1] = (byte)'Z';
            }

            BitConverter.GetBytes(peOffset).CopyTo(bytes, 0x3C);
            if (peOffset >= 0 && peOffset + 6 <= length)
            {
                BitConverter.GetBytes(peSignature).CopyTo(bytes, peOffset);
                BitConverter.GetBytes(machine).CopyTo(bytes, peOffset + 4);
            }
            return bytes;
        }

        [Theory]
        [InlineData(PeImage.MachineI386)]
        [InlineData(PeImage.MachineAmd64)]
        public void ReadMachineReturnsTheCoffMachine(ushort machine)
        {
            using var stream = new MemoryStream(BuildPe(machine));
            Assert.Equal(machine, PeImage.ReadMachine(stream));
        }

        [Fact]
        public void ReadMachineRejectsShortFiles()
        {
            using var stream = new MemoryStream(new byte[10]);
            Assert.Throws<InvalidDataException>(() => PeImage.ReadMachine(stream));
        }

        [Fact]
        public void ReadMachineRejectsMissingDosSignature()
        {
            using var stream = new MemoryStream(BuildPe(PeImage.MachineI386, dosSignature: false));
            Assert.Throws<InvalidDataException>(() => PeImage.ReadMachine(stream));
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(0xFC)]
        [InlineData(0x10000)]
        public void ReadMachineRejectsOffsetsOutsideTheFile(int peOffset)
        {
            using var stream = new MemoryStream(BuildPe(PeImage.MachineI386, peOffset: peOffset));
            Assert.Throws<InvalidDataException>(() => PeImage.ReadMachine(stream));
        }

        [Fact]
        public void ReadMachineRejectsWrongPeSignature()
        {
            using var stream = new MemoryStream(BuildPe(PeImage.MachineI386, peSignature: 0x12345678));
            Assert.Throws<InvalidDataException>(() => PeImage.ReadMachine(stream));
        }

        [Fact]
        public void TryReadMachineReturnsFalseForMissingFiles()
        {
            Assert.False(PeImage.TryReadMachine(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".exe"), out _));
        }

        [Fact]
        public void ValidateWordPadExecutableAcceptsX86AndRejectsOtherMachines()
        {
            using var temp = new TempDirectory();
            string x86 = temp.CreateFile("x86.exe", BuildPe(PeImage.MachineI386));
            string arm64 = temp.CreateFile("arm64.exe", BuildPe(0xAA64));

            PeImage.ValidateWordPadExecutable(x86);
            Assert.Throws<InvalidDataException>(() => PeImage.ValidateWordPadExecutable(arm64));
            Assert.Throws<FileNotFoundException>(() => PeImage.ValidateWordPadExecutable(temp.Combine("missing.exe")));
        }
    }
}
