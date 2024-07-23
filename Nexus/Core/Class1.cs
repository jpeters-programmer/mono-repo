using System.Text;
using System.Text.Json;
using System.Text.Unicode;

namespace Nexus.Core;

public class Document
{
    int PathSet { get; }
    int[] ValueKeys { get; }
    PackedNumbers EmbeddedNumbers { get; }
}


public class NsonTokenizer
{
    public static async IAsyncEnumerable<string> GetIEnumerableTest(Stream utf8source, NsonSettings settings = default)
    {
        byte[] byteBuffer = new byte[50];

        JsonReaderState readerState = new(new JsonReaderOptions() { });

        int leftoverLength = 0;
        int bytesRead;
        int depth = 0;

        PathBuffer pathBuffer = new(new byte[settings.MaxPathSize]);

        //temporary
        List<string> testResults = new();
        while ((bytesRead = await utf8source.ReadAsync(byteBuffer.AsMemory(leftoverLength, byteBuffer.Length - leftoverLength))) > 0)
        {
            bool isFinalBlock = bytesRead < byteBuffer.Length - leftoverLength;
            int blockSize = bytesRead + leftoverLength;
            Span<byte> readerBuffer = byteBuffer[0..blockSize];
            Utf8JsonReader reader = new(readerBuffer, isFinalBlock, readerState);

            int dataBufferIndex = 0;

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.StartObject)
                {
                    depth++;
                }
                else if (reader.TokenType == JsonTokenType.EndObject)
                {
                    depth--;
                }
                else if (reader.TokenType == JsonTokenType.PropertyName)
                {
                    pathBuffer.Update(reader.ValueSpan, depth);
                    testResults.Add(Encoding.UTF8.GetString(pathBuffer.Path));
                }
                else
                {
                    //pickout the value

                }

                dataBufferIndex++;
            }

            readerState = reader.CurrentState;
            int bytesConsumed = (int)reader.BytesConsumed;
            leftoverLength = 0;

            //this is true if buffer cuts off in-progress json token
            if (bytesConsumed < blockSize)
            {
                //create a span over the leftover portion
                ReadOnlySpan<byte> leftover = byteBuffer.AsSpan(start: bytesConsumed);

                //resize our buffer for next time if we were unable to parse any tokens (meaning a single token is too big too fit)
                if (bytesConsumed == 0)
                {
                    Array.Resize(ref byteBuffer, byteBuffer.Length * 2);
                }

                //copy our left over to the start of the buffer for next time
                leftover.CopyTo(byteBuffer);
                //note our leftover length, which controls where our buffer starts to get filled
                leftoverLength = leftover.Length;
            }
        }

        //can't reference our reader after this point (ref struct + after yield == cry)
        foreach (var item in testResults)
            yield return item;
    }
}

public struct PathBuffer
{
    private readonly Memory<byte> buffer;
    private int pos;
    private int depth;
    private const byte DOT = (byte)'.';

    public PathBuffer(Memory<byte> buffer) : this()
    {
        this.buffer = buffer;
    }

    public void Update(in ReadOnlySpan<byte> input, int atDepth)
    {
        int depthIncrease = atDepth - depth;
        if (depthIncrease > 1)
            throw new ArgumentOutOfRangeException(nameof(atDepth), "The depth level cannot increase by more than one from the last call to Write");

        if (depthIncrease <= 0)
            pos = GoUp(Math.Abs(depthIncrease));
        

        //if (depthIncrease == 1)  //don't need to do anything, the pos is already where we need it to be

        input.CopyTo(buffer.Span[pos..]);
        pos += input.Length;
        buffer.Span[pos] = DOT;
        pos++;
        depth = atDepth;
    }
    public int GoUp(int qty)
    {
        qty++;
        for (int i = pos - 2; i > 0; i--)
        {
            if (buffer.Span[i] == DOT)
                qty--;

            if (qty == 0)
            {
                return i + 1;
            }
        }

        return 0;
    }
    public ReadOnlySpan<byte> Path => buffer.Span[0..pos];
}

internal struct NsonPathAndValue
{
    public int PathKey;
    public ReadOnlyMemory<byte> Value;

    public NsonPathAndValue(int pathKey, ReadOnlyMemory<byte> value)
    {
        PathKey = pathKey;
        Value = value;
    }
}

public struct NsonSettings
{
    public int MaxPathSize = 100;

    public NsonSettings()
    {
    }
}
