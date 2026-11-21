using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace NOVA.IDE.Packaging;

public enum NovaTarget : byte { Rp2040 = 1, Rp2350ArmM33 = 2 }
public enum NovaImageKind : byte { RawBinary = 1, ArmElf32 = 2 }

public sealed record NovaPackageInfo(string Name, string Version, NovaTarget Target,
    NovaImageKind ImageKind, uint AbiVersion, uint LoadAddress, uint PayloadLength, string Sha256);

public static class NovaPackage
{
    public const int HeaderSize = 128;
    public const int MaximumPackageSize = 2 * 1024 * 1024;
    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("NOVAAPP1");

    public static NovaPackageInfo Create(string inputPath, string outputPath, string name,
        string version, NovaTarget target, uint abiVersion, uint loadAddress = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        if (!Enum.IsDefined(target)) throw new ArgumentOutOfRangeException(nameof(target));
        if (abiVersion == 0) throw new ArgumentOutOfRangeException(nameof(abiVersion));
        if (name.Length > 47 || version.Length > 15) throw new ArgumentException("Name or version is too long.");
        var extension = Path.GetExtension(inputPath).ToLowerInvariant();
        if (extension == ".uf2") throw new InvalidDataException("UF2 is firmware flashing/recovery, not an application payload.");
        var kind = extension switch
        {
            ".bin" => NovaImageKind.RawBinary,
            ".elf" => NovaImageKind.ArmElf32,
            _ => throw new InvalidDataException("Alpha input must be ARM ELF32 (.elf) or raw binary (.bin).")
        };
        var payload = File.ReadAllBytes(inputPath);
        if (payload.Length == 0 || payload.Length > MaximumPackageSize - HeaderSize)
            throw new InvalidDataException("Payload exceeds the 2 MiB NOVA package limit.");
        if (kind == NovaImageKind.ArmElf32)
        {
            if (payload.Length < 52 || payload[0] != 0x7F || payload[1] != (byte)'E' ||
                payload[2] != (byte)'L' || payload[3] != (byte)'F' || payload[4] != 1 ||
                payload[5] != 1 || BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(16, 2)) != 2 || BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(18, 2)) != 40)
                throw new InvalidDataException("ELF must be little-endian ARM ELF32.");
            loadAddress = BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(24, 4));
            if (loadAddress == 0 || (loadAddress & 1u) == 0u) throw new InvalidDataException("ELF entry point must be a non-zero Thumb address.");
            if (!HasNovaEntryPoint(payload, loadAddress)) throw new InvalidDataException("ELF must export nova_app_main as its entry point. Link with ENTRY(nova_app_main) and keep the symbol table.");
        }
        else if (loadAddress == 0) throw new InvalidDataException("Raw BIN requires an explicit non-zero load address.");

        var hash = SHA256.HashData(payload);
        using var output = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);
        using var writer = new BinaryWriter(output, Encoding.UTF8, leaveOpen: true);
        writer.Write(Magic);
        writer.Write((ushort)1);
        writer.Write((byte)target);
        writer.Write((byte)kind);
        writer.Write(abiVersion);
        writer.Write((uint)payload.Length);
        writer.Write(loadAddress);
        WriteFixed(writer, name, 48);
        WriteFixed(writer, version, 16);
        writer.Write(hash);
        writer.Write(new byte[HeaderSize - 120]);
        writer.Write(payload);
        writer.Flush();
        return new NovaPackageInfo(name, version, target, kind, abiVersion, loadAddress,
            (uint)payload.Length, Convert.ToHexString(hash));
    }

    public static NovaPackageInfo Inspect(string packagePath)
    {
        using var stream = File.OpenRead(packagePath);
        if (stream.Length < HeaderSize || stream.Length > MaximumPackageSize)
            throw new InvalidDataException("Invalid NOVA package size.");
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
        if (!reader.ReadBytes(8).SequenceEqual(Magic)) throw new InvalidDataException("Invalid package signature.");
        if (reader.ReadUInt16() != 1) throw new InvalidDataException("Unsupported package version.");
        var target = (NovaTarget)reader.ReadByte();
        var kind = (NovaImageKind)reader.ReadByte();
        if (!Enum.IsDefined(target) || !Enum.IsDefined(kind)) throw new InvalidDataException("Unknown target or image kind.");
        var abi = reader.ReadUInt32();
        if (abi == 0) throw new InvalidDataException("ABI version zero is invalid.");
        var length = reader.ReadUInt32();
        var address = reader.ReadUInt32();
        var name = ReadFixed(reader, 48);
        var version = ReadFixed(reader, 16);
        var expectedHash = reader.ReadBytes(32);
        stream.Position = HeaderSize;
        if (length == 0 || length != stream.Length - HeaderSize) throw new InvalidDataException("Payload length mismatch.");
        var actualHash = SHA256.HashData(stream);
        if (!CryptographicOperations.FixedTimeEquals(expectedHash, actualHash))
            throw new InvalidDataException("SHA-256 validation failed.");
        return new NovaPackageInfo(name, version, target, kind, abi, address, length, Convert.ToHexString(actualHash));
    }

    private static bool HasNovaEntryPoint(byte[] elf, uint entryPoint)
    {
        if (elf.Length < 52) return false;
        var sectionOffset = BinaryPrimitives.ReadUInt32LittleEndian(elf.AsSpan(32, 4));
        var sectionEntrySize = BinaryPrimitives.ReadUInt16LittleEndian(elf.AsSpan(46, 2));
        var sectionCount = BinaryPrimitives.ReadUInt16LittleEndian(elf.AsSpan(48, 2));
        if (sectionEntrySize < 40 || sectionCount == 0 ||
            (ulong)sectionOffset + (ulong)sectionEntrySize * sectionCount > (ulong)elf.Length) return false;

        for (var i = 0; i < sectionCount; i++)
        {
            var section = checked((int)sectionOffset + i * sectionEntrySize);
            var type = BinaryPrimitives.ReadUInt32LittleEndian(elf.AsSpan(section + 4, 4));
            if (type != 2 && type != 11) continue; // SHT_SYMTAB / SHT_DYNSYM
            var symbolsOffset = BinaryPrimitives.ReadUInt32LittleEndian(elf.AsSpan(section + 16, 4));
            var symbolsSize = BinaryPrimitives.ReadUInt32LittleEndian(elf.AsSpan(section + 20, 4));
            var stringIndex = BinaryPrimitives.ReadUInt32LittleEndian(elf.AsSpan(section + 24, 4));
            var symbolEntrySize = BinaryPrimitives.ReadUInt32LittleEndian(elf.AsSpan(section + 36, 4));
            if (stringIndex >= sectionCount || symbolEntrySize < 16 ||
                (ulong)symbolsOffset + symbolsSize > (ulong)elf.Length) continue;
            var stringsSection = checked((int)sectionOffset + (int)stringIndex * sectionEntrySize);
            var stringsOffset = BinaryPrimitives.ReadUInt32LittleEndian(elf.AsSpan(stringsSection + 16, 4));
            var stringsSize = BinaryPrimitives.ReadUInt32LittleEndian(elf.AsSpan(stringsSection + 20, 4));
            if ((ulong)stringsOffset + stringsSize > (ulong)elf.Length) continue;
            for (uint offset = 0; offset + 16u <= symbolsSize; offset += symbolEntrySize)
            {
                var symbol = checked((int)(symbolsOffset + offset));
                var nameOffset = BinaryPrimitives.ReadUInt32LittleEndian(elf.AsSpan(symbol, 4));
                if (nameOffset >= stringsSize) continue;
                var nameStart = checked((int)(stringsOffset + nameOffset));
                var nameEnd = Array.IndexOf(elf, (byte)0, nameStart, checked((int)(stringsSize - nameOffset)));
                if (nameEnd < 0 || Encoding.ASCII.GetString(elf, nameStart, nameEnd - nameStart) != "nova_app_main") continue;
                var value = BinaryPrimitives.ReadUInt32LittleEndian(elf.AsSpan(symbol + 4, 4));
                var symbolInfo = elf[symbol + 12];
                var sectionIndex = BinaryPrimitives.ReadUInt16LittleEndian(elf.AsSpan(symbol + 14, 2));
                return (symbolInfo & 0x0F) == 2 && sectionIndex != 0 && (value & ~1u) == (entryPoint & ~1u);
            }
        }
        return false;
    }

    private static void WriteFixed(BinaryWriter writer, string value, int length)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        if (bytes.Length >= length) throw new ArgumentException("Metadata field is too long.");
        writer.Write(bytes);
        writer.Write(new byte[length - bytes.Length]);
    }

    private static string ReadFixed(BinaryReader reader, int length)
    {
        var bytes = reader.ReadBytes(length);
        var end = Array.IndexOf(bytes, (byte)0);
        return Encoding.UTF8.GetString(bytes, 0, end < 0 ? bytes.Length : end);
    }
}
