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
                payload[5] != 1 || BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(18, 2)) != 40)
                throw new InvalidDataException("ELF must be little-endian ARM ELF32.");
            loadAddress = BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(24, 4));
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
        var abi = reader.ReadUInt32();
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
