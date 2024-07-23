using System.Buffers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Nexus.Core;

public class PackedNumbers
{
    private readonly byte[] packedNumbers;

    public PackedNumbers(byte[] packedNumbers)
    {
        this.packedNumbers = packedNumbers;
    }

    public PackedNumbersReader GetReader()
    {
        return new(packedNumbers);
    }

    public int Size => packedNumbers.Length;
}

public class PackedNumbersReader
{
    private readonly byte[] packedNumbers;
    private int readPos;

    public PackedNumbersReader(byte[] packedNumbers)
    {
        this.packedNumbers = packedNumbers;
    }
    public ReadOnlyMemory<byte> ReadOne()

    {
        var packed = packedNumbers.AsSpan();
        PackedType packedType = (PackedType)packed[readPos];
        readPos++;
        if (packedType == PackedType.EightIEEE754)
        {
            var packedDouble = packed.Slice(readPos, 8);
            readPos += packedType.GetPackedSize();
            var value = BitConverter.ToDouble(packedDouble);
            MemoryBufferWriter memoryBufferWriter = new();
            Utf8JsonWriter writer = new(memoryBufferWriter);
            JsonSerializer.Serialize(writer, value);
            return memoryBufferWriter.WrittenMemory;
        }

        var (leftPackedSize, rightPackedSize) = packedType.GetComponentSizes();
        var leftPacked = packed.Slice(readPos, leftPackedSize);
        readPos += leftPackedSize;
        bool isNeg = packedType.IsNeg();
        var leftString = leftPacked.ConvertToWholeNumber(isNeg).GetUtf8String();

        if (rightPackedSize == 0)
        { 
            return leftString;
        }
            
        var rightPacked = packed.Slice(readPos, rightPackedSize);
        readPos += rightPackedSize;
        var rightString = rightPacked.ConvertToWholeNumber(false).GetUtf8String();

        int bytesSizeTotal = leftString.Length + 1 + rightString.Length;
       
        byte[] result = new byte[bytesSizeTotal];
        Span<byte> resultSpan = result;

        leftString.CopyTo(resultSpan);
        resultSpan[leftString.Length] = (byte)'.';
        rightString.CopyTo(resultSpan[(leftString.Length + 1)..]);

        return result;
    }
}
public class PackedNumbersCollector
{
    private readonly List<PackedNumber> packedNumbers = new();
    private int size = 0;

    public PackedNumbers GetPacked()
    {
        byte[] bytes = new byte[size];
        Memory<byte> span = bytes;
        int pos = 0;
        foreach(var pn in packedNumbers)
        {
            pn.Memory.CopyTo(span.Slice(pos, pn.Memory.Length));
            pos+=pn.Memory.Length;
        }
        return new PackedNumbers(bytes);
    }

    public void Add(ReadOnlySpan<byte> span)
    {
        PackedNumber pn = new(span);
        size += pn.Memory.Length;
        packedNumbers.Add(pn);
    }
}

public readonly struct PackedNumber
{
    public PackedType PackedType => (PackedType)bytes[0];
    private readonly byte[] bytes;

    public PackedNumber(ReadOnlySpan<byte> jsonInput)
    {
        bool isNeg = jsonInput[0] == '-';
        ulong left = 0;
        ulong right = 0;
        int dec = 0;
        PackedType marker;

        for (int i = isNeg ? 1 : 0; i < jsonInput.Length; i++)
        {
            char c = (char)jsonInput[i];
            if (c == '.')
            {
                //we are done with the left
                dec = i;
            }
            else if (c == 'e')
            {
                //abort - just construct a double
                goto constructDouble;
            }
            else if (dec == 0)
            {
                left *= 10;
                //convert the char to the digit
                left += (uint)(c - '0');
            }
            else
            {
                right *= 10;
                right += (uint)(c - '0');
            }
        }

        NumberSize leftNumberSize = left.GetNumberSize();
        if (leftNumberSize == NumberSize.RequiresDouble)
            goto constructDouble;

        //if we only have a left value we can exit early
        if (dec == 0)
        {
            bytes = new byte[(int)leftNumberSize + 1];
            marker = (PackedType)leftNumberSize;
            if (isNeg) 
                marker = marker.GetNeg();

            bytes[0] = (byte)marker;
            left.PackBytes((int)leftNumberSize, 1, bytes);
            return;
        }

        NumberSize rightNumberSize = right.GetNumberSize();
        if (rightNumberSize == NumberSize.RequiresDouble)
            goto constructDouble;

        //got our left and right
        marker = (leftNumberSize, rightNumberSize) switch
        {
            (NumberSize.Byte, NumberSize.Byte) => PackedType.OneOneSplit,
            (NumberSize.TwoByte, NumberSize.Byte) => PackedType.TwoOneSplit,
            (NumberSize.ThreeByte, NumberSize.Byte) => PackedType.ThreeOneSplit,
            (NumberSize.FourByte, NumberSize.Byte) => PackedType.FourOneSplit,
            (NumberSize.FiveByte, NumberSize.Byte) => PackedType.FiveOneSplit,
            (NumberSize.SixByte, NumberSize.Byte) => PackedType.SixOneSplit,

            (NumberSize.Byte, NumberSize.TwoByte) => PackedType.OneTwoSplit,
            (NumberSize.TwoByte, NumberSize.TwoByte) => PackedType.TwoTwoSplit,
            (NumberSize.ThreeByte, NumberSize.TwoByte) => PackedType.ThreeTwoSplit,
            (NumberSize.FourByte, NumberSize.TwoByte) => PackedType.FourTwoSplit,
            (NumberSize.FiveByte, NumberSize.TwoByte) => PackedType.FiveTwoSplit,

            (NumberSize.Byte, NumberSize.ThreeByte) => PackedType.OneThreeSplit,
            (NumberSize.TwoByte, NumberSize.ThreeByte) => PackedType.TwoThreeSplit,
            (NumberSize.ThreeByte, NumberSize.ThreeByte) => PackedType.ThreeThreeSplit,
            (NumberSize.FourByte, NumberSize.ThreeByte) => PackedType.FourThreeSplit,

            (NumberSize.Byte, NumberSize.FourByte) => PackedType.OneFourSplit,
            (NumberSize.TwoByte, NumberSize.FourByte) => PackedType.TwoFourSplit,
            (NumberSize.ThreeByte, NumberSize.FourByte) => PackedType.ThreeFourSplit,

            (NumberSize.Byte, NumberSize.FiveByte) => PackedType.OneFiveSplit,
            (NumberSize.TwoByte, NumberSize.FiveByte) => PackedType.TwoFiveSplit,

            (NumberSize.Byte, NumberSize.SixByte) => PackedType.OneSixSplit,

            _ => PackedType.None
        };

        if (marker == PackedType.None)
            goto constructDouble;

        //flip to neg version if required
        if (isNeg)
        {
            marker = marker.GetNeg();
        }
        int packedSize = marker.GetPackedSize();
        //+1 for the marker
        bytes = new byte[packedSize + 1];
        bytes[0] = (byte)marker;
        left.PackBytes((int)leftNumberSize, 1, bytes);
        right.PackBytes((int)rightNumberSize, 1 + (int)leftNumberSize, bytes);

        return;

    constructDouble:
        marker = PackedType.EightIEEE754;
        double value = JsonNode.Parse(jsonInput)!.GetValue<double>();
        bytes = GetPackedBytes((byte)marker, value);
        return;
    }

    public ReadOnlyMemory<byte> Memory => bytes;

    private static unsafe byte[] GetPackedBytes(byte marker, double value)
    {
        byte* bytes = (byte*)&value;

        byte[] result = new byte[9];
        result[0] = marker;

        for (int i = 0; i < sizeof(double); i++)
        {
            result[i + 1] = bytes[i];
        }

        return result;
    }
}

public static class Extensions
{

    public static PackedType GetNeg(this PackedType packedType)
    {
        return (PackedType)((int)packedType + (int)PackedType.OneSixSplit);
    }
    public static void PackBytes(this ulong value, in int count, in int pos, in byte[] target)
    {
        if (count < 1 || count > 8)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "Count must be between 1 and 8.");
        }

        for (int i = 0; i < count; i++)
        {
            target[pos + i] = (byte)((value >> (8 * i)) & 0xFF);
        }
    }

    internal static long ConvertToWholeNumber(this Span<byte> bytes, bool neg)
    {
        if (bytes.Length > 8)
            throw new ArgumentException("Span length must be 8 or less.", nameof(bytes));

        long value = 0;
        for (int i = 0; i < bytes.Length; i++)
        {
            value |= (long)bytes[i] << (8 * i);
        }

        return neg ? -value : value;
    }

    internal static byte[] GetUtf8String(this long value)
    {
        // Convert the ulong to a string
        string stringValue = value.ToString();

        // Convert the string to UTF-8 bytes
        return Encoding.UTF8.GetBytes(stringValue);
    }

    private const ulong THREE_BYTE_MAX = 16777215;
    private const ulong FIVE_BYTE_MAX = 1099511627775;
    private const ulong SIX_BYTE_MAX = 281474976710655;
    private const ulong SEVEN_BYTE_MAX = 72057594037927935;

    public static NumberSize GetNumberSize(this ulong value)
    {
        return value switch
        {
            <= byte.MaxValue => NumberSize.Byte,
            <= ushort.MaxValue => NumberSize.TwoByte,
            <= THREE_BYTE_MAX => NumberSize.ThreeByte,
            <= int.MaxValue => NumberSize.FourByte,
            <= FIVE_BYTE_MAX => NumberSize.FiveByte,
            <= SIX_BYTE_MAX => NumberSize.SixByte,
            <= SEVEN_BYTE_MAX => NumberSize.SevenByte,
            _ => NumberSize.RequiresDouble
        };
    }
}

public enum NumberSize : byte
{
    RequiresDouble = 0,
    Byte = 1,
    TwoByte = 2,
    ThreeByte = 3,
    FourByte = 4,
    FiveByte = 5,
    SixByte = 6,
    SevenByte = 7,
}

public enum PackedType : byte
{
    None = 0,
    Byte = 1,
    TwoByte = 2,
    ThreeByte = 3,
    FourByte = 4,
    FiveByte = 5,
    SixByte = 6,
    SevenByte = 7,
    OneOneSplit,
    TwoOneSplit,
    OneTwoSplit,
    ThreeOneSplit,
    TwoTwoSplit,
    OneThreeSplit,
    FourOneSplit,
    ThreeTwoSplit,
    TwoThreeSplit,
    OneFourSplit,
    FiveOneSplit,
    FourTwoSplit,
    ThreeThreeSplit,
    TwoFourSplit,
    OneFiveSplit,
    SixOneSplit,
    FiveTwoSplit,
    FourThreeSplit,
    ThreeFourSplit,
    TwoFiveSplit,
    OneSixSplit,
    NegByte,
    NegTwoByte,
    NegThreeByte,
    NegFourByte,
    NegFiveByte,
    NegSixByte,
    NegSevenByte,
    NegOneOneSplit,
    NegTwoOneSplit,
    NegOneTwoSplit,
    NegThreeOneSplit,
    NegTwoTwoSplit,
    NegOneThreeSplit,
    NegFourOneSplit,
    NegThreeTwoSplit,
    NegTwoThreeSplit,
    NegOneFourSplit,
    NegFiveOneSplit,
    NegFourTwoSplit,
    NegThreeThreeSplit,
    NegTwoFourSplit,
    NegOneFiveSplit,
    NegSixOneSplit,
    NegFiveTwoSplit,
    NegFourThreeSplit,
    NegThreeFourSplit,
    NegTwoFiveSplit,
    NegOneSixSplit,
    //Can ignore this one
    EightIEEE754 = 255,
}


public static class PackedTypeExtensions
{


    public static bool IsNeg(this PackedType packedType)
    {
        return packedType switch
        {
            PackedType.NegByte => true,
            PackedType.NegTwoByte => true,
            PackedType.NegThreeByte => true,
            PackedType.NegFourByte => true,
            PackedType.NegFiveByte => true,
            PackedType.NegSixByte => true,
            PackedType.NegSevenByte => true,
            PackedType.NegOneOneSplit => true,
            PackedType.NegTwoOneSplit => true,
            PackedType.NegOneTwoSplit => true,
            PackedType.NegThreeOneSplit => true,
            PackedType.NegTwoTwoSplit => true,
            PackedType.NegOneThreeSplit => true,
            PackedType.NegFourOneSplit => true,
            PackedType.NegThreeTwoSplit => true,
            PackedType.NegTwoThreeSplit => true,
            PackedType.NegOneFourSplit => true,
            PackedType.NegFiveOneSplit => true,
            PackedType.NegFourTwoSplit => true,
            PackedType.NegThreeThreeSplit => true,
            PackedType.NegTwoFourSplit => true,
            PackedType.NegOneFiveSplit => true,
            PackedType.NegSixOneSplit => true,
            PackedType.NegFiveTwoSplit => true,
            PackedType.NegFourThreeSplit => true,
            PackedType.NegThreeFourSplit => true,
            PackedType.NegTwoFiveSplit => true,
            PackedType.NegOneSixSplit => true,
            _ => false
        };
    }


    public static (byte, byte) GetComponentSizes(this PackedType packedType)
    {
        return packedType switch
        {
            PackedType.None => (0, 0),
            PackedType.Byte => (1, 0),
            PackedType.TwoByte => (2, 0),
            PackedType.ThreeByte => (3, 0),
            PackedType.FourByte => (4, 0),
            PackedType.FiveByte => (5, 0),
            PackedType.SixByte => (6, 0),
            PackedType.SevenByte => (7, 0),
            PackedType.OneOneSplit => (1, 1),
            PackedType.TwoOneSplit => (2, 1),
            PackedType.OneTwoSplit => (1, 2),
            PackedType.ThreeOneSplit => (3, 1),
            PackedType.TwoTwoSplit => (2, 2),
            PackedType.OneThreeSplit => (1, 3),
            PackedType.FourOneSplit => (4, 1),
            PackedType.ThreeTwoSplit => (3, 2),
            PackedType.TwoThreeSplit => (2, 3),
            PackedType.OneFourSplit => (1, 4),
            PackedType.FiveOneSplit => (5, 1),
            PackedType.FourTwoSplit => (4, 2),
            PackedType.ThreeThreeSplit => (3, 3),
            PackedType.TwoFourSplit => (2, 4),
            PackedType.OneFiveSplit => (1, 5),
            PackedType.SixOneSplit => (6, 1),
            PackedType.FiveTwoSplit => (5, 2),
            PackedType.FourThreeSplit => (4, 3),
            PackedType.ThreeFourSplit => (3, 4),
            PackedType.TwoFiveSplit => (2, 5),
            PackedType.OneSixSplit => (1, 6),
            PackedType.NegByte => (1, 0),
            PackedType.NegTwoByte => (2, 0),
            PackedType.NegThreeByte => (3, 0),
            PackedType.NegFourByte => (4, 0),
            PackedType.NegOneOneSplit => (2, 0),
            PackedType.NegTwoOneSplit => (2, 1),
            PackedType.NegOneTwoSplit => (1, 2),
            PackedType.NegThreeOneSplit => (3, 1),
            PackedType.NegTwoTwoSplit => (2, 2),
            PackedType.NegOneThreeSplit => (1, 3),
            PackedType.NegFourOneSplit => (4, 1),
            PackedType.NegThreeTwoSplit => (3, 2),
            PackedType.NegTwoThreeSplit => (2, 3),
            PackedType.NegOneFourSplit => (1, 4),
            PackedType.NegFiveOneSplit => (5, 1),
            PackedType.NegFourTwoSplit => (4, 2),
            PackedType.NegThreeThreeSplit => (3, 3),
            PackedType.NegTwoFourSplit => (2, 4),
            PackedType.NegOneFiveSplit => (1, 5),
            PackedType.NegSixOneSplit => (6, 1),
            PackedType.NegFiveTwoSplit => (5, 2),
            PackedType.NegFourThreeSplit => (4, 3),
            PackedType.NegThreeFourSplit => (3, 4),
            PackedType.NegTwoFiveSplit => (2, 5),
            PackedType.NegOneSixSplit => (1, 6),
            PackedType.EightIEEE754 => (8, 0),
            _ => throw new ArgumentOutOfRangeException(nameof(packedType), $"Unexpected packedType value: {packedType}")
        };
    }


    public static byte GetPackedSize(this PackedType packedType)
    {
        return packedType switch
        {
            PackedType.None => 0,
            PackedType.Byte => 1,
            PackedType.TwoByte => 2,
            PackedType.ThreeByte => 3,
            PackedType.FourByte => 4,
            PackedType.FiveByte => 5,
            PackedType.SixByte => 6,
            PackedType.SevenByte => 7,
            PackedType.OneOneSplit => 2,
            PackedType.TwoOneSplit => 3,
            PackedType.OneTwoSplit => 3,
            PackedType.ThreeOneSplit => 4,
            PackedType.TwoTwoSplit => 4,
            PackedType.OneThreeSplit => 4,
            PackedType.FourOneSplit => 5,
            PackedType.ThreeTwoSplit => 5,
            PackedType.TwoThreeSplit => 5,
            PackedType.OneFourSplit => 5,
            PackedType.FiveOneSplit => 6,
            PackedType.FourTwoSplit => 6,
            PackedType.ThreeThreeSplit => 6,
            PackedType.TwoFourSplit => 6,
            PackedType.OneFiveSplit => 6,
            PackedType.SixOneSplit => 7,
            PackedType.FiveTwoSplit => 7,
            PackedType.FourThreeSplit => 7,
            PackedType.ThreeFourSplit => 7,
            PackedType.TwoFiveSplit => 7,
            PackedType.OneSixSplit => 7,
            PackedType.NegByte => 1,
            PackedType.NegTwoByte => 2,
            PackedType.NegThreeByte => 3,
            PackedType.NegFourByte => 4,
            PackedType.NegOneOneSplit => 2,
            PackedType.NegTwoOneSplit => 3,
            PackedType.NegOneTwoSplit => 3,
            PackedType.NegThreeOneSplit => 4,
            PackedType.NegTwoTwoSplit => 4,
            PackedType.NegOneThreeSplit => 4,
            PackedType.NegFourOneSplit => 5,
            PackedType.NegThreeTwoSplit => 5,
            PackedType.NegTwoThreeSplit => 5,
            PackedType.NegOneFourSplit => 5,
            PackedType.NegFiveOneSplit => 6,
            PackedType.NegFourTwoSplit => 6,
            PackedType.NegThreeThreeSplit => 6,
            PackedType.NegTwoFourSplit => 6,
            PackedType.NegOneFiveSplit => 6,
            PackedType.NegSixOneSplit => 7,
            PackedType.NegFiveTwoSplit => 7,
            PackedType.NegFourThreeSplit => 7,
            PackedType.NegThreeFourSplit => 7,
            PackedType.NegTwoFiveSplit => 7,
            PackedType.NegOneSixSplit => 7,
            PackedType.EightIEEE754 => 8,
            _ => throw new ArgumentOutOfRangeException(nameof(packedType), $"Not expected packedType value: {packedType}")
        };
    }
}

public class MemoryBufferWriter : IBufferWriter<byte>
{
    private Memory<byte> _buffer;
    private int _index;


    public MemoryBufferWriter(int initialCapacity = 24)
    {
        _buffer = new Memory<byte>(new byte[initialCapacity]);
        _index = 0;
    }


    public void Advance(int count)
    {
        if (count < 0 || _index + count > _buffer.Length)
        {
            throw new InvalidOperationException("Cannot advance past the end of the buffer");
        }


        _index += count;
    }


    public Memory<byte> GetMemory(int sizeHint = 0)
    {
        EnsureCapacity(sizeHint);
        return _buffer[_index..];
    }


    public Span<byte> GetSpan(int sizeHint = 0)
    {
        EnsureCapacity(sizeHint);
        return _buffer.Span[_index..];
    }


    private void EnsureCapacity(int sizeHint)
    {
        if (sizeHint < 0)
        {
            throw new ArgumentException("Size hint cannot be negative", nameof(sizeHint));
        }


        if (sizeHint == 0)
        {
            sizeHint = 1;
        }


        if (_index + sizeHint > _buffer.Length)
        {
            int newSize = Math.Max(_index + sizeHint, _buffer.Length * 2);
            Memory<byte> newBuffer = new Memory<byte>(new byte[newSize]);
            _buffer.Slice(0, _index).CopyTo(newBuffer);
            _buffer = newBuffer;
        }
    }


    public Memory<byte> WrittenMemory => _buffer.Slice(0, _index);


    public int WrittenCount => _index;
}




