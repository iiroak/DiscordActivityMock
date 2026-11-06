using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace DiscordActivityMockV2
{
    /// <summary>Minimal reader for the headers of Windows executables (PE files).</summary>
    internal static class PeImage
    {
        public const ushort MachineI386 = 0x014c;
        public const ushort MachineAmd64 = 0x8664;

        private const ushort DosSignature = 0x5A4D;        // "MZ"
        private const uint PeSignature = 0x00004550;       // "PE\0\0"
        private const int PeOffsetPosition = 0x3C;

        /// <summary>Returns the COFF machine type, or throws <see cref="InvalidDataException"/>.</summary>
        public static ushort ReadMachine(Stream stream)
        {
            if (stream.Length < 64)
                throw new InvalidDataException("The WordPad executable is not a valid Windows application.");

            using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
            stream.Position = 0;
            if (reader.ReadUInt16() != DosSignature)
                throw new InvalidDataException("The WordPad executable is not a valid Windows application.");

            stream.Position = PeOffsetPosition;
            int peOffset = reader.ReadInt32();
            if (peOffset < 0 || peOffset > stream.Length - 6)
                throw new InvalidDataException("The WordPad executable has an invalid PE header.");

            stream.Position = peOffset;
            if (reader.ReadUInt32() != PeSignature)
                throw new InvalidDataException("The WordPad executable has an invalid PE signature.");

            return reader.ReadUInt16();
        }

        public static bool TryReadMachine(string path, out ushort machine)
        {
            try
            {
                using FileStream stream = File.OpenRead(path);
                machine = ReadMachine(stream);
                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
            {
                machine = 0;
                return false;
            }
        }

        /// <summary>Checks that the file is an x86 or x64 Windows program that can run on this system.</summary>
        public static void ValidateWordPadExecutable(string executablePath)
        {
            if (!File.Exists(executablePath))
                throw new FileNotFoundException("WordPad executable was not found in the source files.", executablePath);

            ushort machine;
            using (FileStream stream = File.OpenRead(executablePath))
                machine = ReadMachine(stream);

            if (machine is not (MachineI386 or MachineAmd64))
                throw new InvalidDataException("This WordPad copy must be an x86 or x64 executable.");

            if (machine == MachineAmd64 && RuntimeInformation.OSArchitecture == Architecture.X86)
                throw new PlatformNotSupportedException("This WordPad copy requires 64-bit Windows.");
        }
    }
}
