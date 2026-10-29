using System.Buffers.Binary;
using System.Security.Cryptography;
using MelonLoader;
using MelonLoader.Utils;

namespace CustomWeaponsFramework
{
    internal static class BundleCache
    {
        private const int BlockSize = 128 * 1024;
        private const uint BlocksInfoAtEnd = 0x80;
        private const uint PaddingAtStart = 0x200;

        private static readonly string Directory = Path.Combine(MelonEnvironment.UserDataDirectory, "CustomWeaponsFramework", "Cache");
        private static readonly Dictionary<string, (long Length, DateTime Written, string Path)> Resolved = new(StringComparer.OrdinalIgnoreCase);

        internal static string Resolve(string path)
        {
            try
            {
                FileInfo source = new(path);
                if (Resolved.TryGetValue(path, out var known) && known.Length == source.Length && known.Written == source.LastWriteTimeUtc && File.Exists(known.Path))
                {
                    return known.Path;
                }

                byte[] file = File.ReadAllBytes(path);
                byte[] unpacked = Unpack(file);
                string result = path;

                if (unpacked != null)
                {
                    result = Path.Combine(Directory, Convert.ToHexString(SHA256.HashData(file)) + ".bundle");
                    if (!File.Exists(result))
                    {
                        System.IO.Directory.CreateDirectory(Directory);
                        string temporary = result + ".tmp";
                        File.WriteAllBytes(temporary, unpacked);
                        File.Move(temporary, result, true);
                        MelonLogger.Msg($"Unpacked the LZMA bundle {path} for fast loading.");
                    }
                }

                Resolved[path] = (source.Length, source.LastWriteTimeUtc, result);

                return result;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException or IndexOutOfRangeException or OverflowException)
            {
                MelonLogger.Warning($"Cannot unpack {path}: {exception.Message}");
                return path;
            }
        }

        /*  
        *   Rewrites an LZMA bundle as an uncompressed one, which Unity loads much faster
        *   Steps: read the header and the block list, decompress every block, then write the same header
        *   with one uncompressed 128 KB block entry per chunk. Returns null when the bundle has no LZMA blocks
        */
        private static byte[] Unpack(byte[] file)
        {
            int position = Array.IndexOf(file, (byte)0) + 1;
            uint version = BinaryPrimitives.ReadUInt32BigEndian(file.AsSpan(position));
            position = Array.IndexOf(file, (byte)0, position + 4) + 1;
            position = Array.IndexOf(file, (byte)0, position) + 1;
            int header = position;

            uint compressedInfo = BinaryPrimitives.ReadUInt32BigEndian(file.AsSpan(position + 8));
            uint uncompressedInfo = BinaryPrimitives.ReadUInt32BigEndian(file.AsSpan(position + 12));
            uint flags = BinaryPrimitives.ReadUInt32BigEndian(file.AsSpan(position + 16));
            position += 20;

            if (version >= 7) position = Align(position);

            byte[] packedInfo;
            if ((flags & BlocksInfoAtEnd) != 0)
            {
                packedInfo = file.AsSpan(file.Length - (int)compressedInfo, (int)compressedInfo).ToArray();
            }
            else
            {
                packedInfo = file.AsSpan(position, (int)compressedInfo).ToArray();
                position += (int)compressedInfo;
            }

            byte[] info = Decompress(packedInfo, (int)uncompressedInfo, flags & 0x3F);
            int count = BinaryPrimitives.ReadInt32BigEndian(info.AsSpan(16));
            var blocks = new (int Uncompressed, int Compressed, int Compression)[count];
            bool lzma = false;

            for (int index = 0; index < count; index++)
            {
                ReadOnlySpan<byte> entry = info.AsSpan(20 + index * 10, 10);
                blocks[index] = (checked((int)BinaryPrimitives.ReadUInt32BigEndian(entry)), checked((int)BinaryPrimitives.ReadUInt32BigEndian(entry[4..])), BinaryPrimitives.ReadUInt16BigEndian(entry[8..]) & 0x3F);
                lzma |= blocks[index].Compression == 1;
            }

            if (!lzma) return null;

            if ((flags & PaddingAtStart) != 0) position = Align(position);

            using MemoryStream data = new();
            foreach (var block in blocks)
            {
                data.Write(Decompress(file.AsSpan(position, block.Compressed).ToArray(), block.Uncompressed, (uint)block.Compression));
                position += block.Compressed;
            }

            byte[] content = data.ToArray();
            int chunks = (content.Length + BlockSize - 1) / BlockSize;
            byte[] nodes = info.AsSpan(20 + count * 10).ToArray();
            byte[] newInfo = new byte[20 + chunks * 10 + nodes.Length];
            info.AsSpan(0, 16).CopyTo(newInfo);
            BinaryPrimitives.WriteInt32BigEndian(newInfo.AsSpan(16), chunks);

            for (int index = 0; index < chunks; index++)
            {
                int size = Math.Min(BlockSize, content.Length - index * BlockSize);
                Span<byte> entry = newInfo.AsSpan(20 + index * 10, 10);
                BinaryPrimitives.WriteUInt32BigEndian(entry, (uint)size);
                BinaryPrimitives.WriteUInt32BigEndian(entry[4..], (uint)size);
                BinaryPrimitives.WriteUInt16BigEndian(entry[8..], 0x40);
            }

            nodes.CopyTo(newInfo, 20 + chunks * 10);
            uint newFlags = flags & ~0x3Fu & ~BlocksInfoAtEnd;

            using MemoryStream output = new();
            output.Write(file, 0, header);
            output.Write(new byte[20]);
            if (version >= 7) Pad(output);
            output.Write(newInfo);
            if ((newFlags & PaddingAtStart) != 0) Pad(output);
            output.Write(content);

            byte[] result = output.ToArray();
            BinaryPrimitives.WriteInt64BigEndian(result.AsSpan(header), result.Length);
            BinaryPrimitives.WriteUInt32BigEndian(result.AsSpan(header + 8), (uint)newInfo.Length);
            BinaryPrimitives.WriteUInt32BigEndian(result.AsSpan(header + 12), (uint)newInfo.Length);
            BinaryPrimitives.WriteUInt32BigEndian(result.AsSpan(header + 16), newFlags);
            return result;
        }

        private static int Align(int position) => (position + 15) & ~15;

        private static void Pad(MemoryStream stream)
        {
            while (stream.Length % 16 != 0) stream.WriteByte(0);
        }

        private static byte[] Decompress(byte[] source, int size, uint compression) => compression switch
        {
            0 when source.Length == size => source,
            1 => Lzma.Decode(source, size),
            2 or 3 => Lz4(source, size),
            _ => throw new InvalidDataException("Unsupported bundle compression " + compression + ".")
        };

        private static byte[] Lz4(byte[] source, int size)
        {
            byte[] output = new byte[size];
            int input = 0, written = 0;

            while (input < source.Length)
            {
                int token = source[input++];
                int literals = token >> 4;
                if (literals == 15) literals += Length(source, ref input);

                Buffer.BlockCopy(source, input, output, written, literals);
                input += literals;
                written += literals;

                if (input == source.Length) break;

                int offset = source[input] | source[input + 1] << 8;
                input += 2;

                int match = token & 15;
                if (match == 15) match += Length(source, ref input);
                match += 4;

                if (offset == 0 || offset > written || match > size - written)
                {
                    throw new InvalidDataException("Corrupt LZ4 data.");
                }

                for (int index = 0; index < match; index++)
                {
                    output[written] = output[written - offset];
                    written++;
                }
            }

            return written == size ? output : throw new InvalidDataException("LZ4 block size mismatch.");
        }

        private static int Length(byte[] source, ref int input)
        {
            int length = 0;
            byte value;

            do
            {
                value = source[input++];
                length += value;
            }
            while (value == 255);

            return length;
        }
    }
}
