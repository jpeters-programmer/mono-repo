using System.Text;
using System.Text.Json;

namespace JsonStreaming;
public class JsonArray
{
    public static IAsyncEnumerable<T> Get<T>(HttpResponseMessage responseMessage, JsonSerializerOptions serializerOptions)
    {
        return Get<T>(responseMessage.Content.ReadAsStream(), serializerOptions);
    }

    public static async IAsyncEnumerable<T> Get<T>(Stream utf8JsonStream, JsonSerializerOptions serializerOptions)
    {
        await foreach (var (bytes, range) in Get(utf8JsonStream))
        {
            yield return JsonSerializer.Deserialize<T>(bytes[range], serializerOptions)!;
        }
    }

    public static async IAsyncEnumerable<(byte[], Range)> Get(Stream utf8JsonStream, int initSize = 50, int initSkip = 1)
    {
        var buffer = new byte[initSize];
        //skip the opener "["
        utf8JsonStream.Position = initSkip;

        CollectorState state = new(false, false, 0, 0, -1, -1);
        const byte stringParens = (byte)'"';
        const byte escapeChar = (byte)'\\';
        const byte depthOpener = (byte)'{';
        const byte depthCloser = (byte)'}';

        while (true)
        {
            var readQty = await utf8JsonStream.ReadAsync(buffer, state.Offset, buffer.Length - state.Offset);
            if (readQty == 0) yield break;
            var offset = state.Offset;
            var limit = readQty + offset;
            for (int i = offset; i < limit; i++)
            {
                if (state.SkipNext)
                {
                    state.SkipNext = false;
                    continue;
                }
                var c = buffer[i];
                if (state.InString)
                {
                    if (c == escapeChar)
                    {
                        state.SkipNext = true;
                        continue;
                    }
                    if (c == stringParens)
                    {
                        state.InString = false;
                    }
                    continue;
                }
                if (c == stringParens)
                {
                    state.InString = true;
                    continue;
                }
                if (c == depthOpener)
                {
                    if (state.Depth == 0)
                    {
                        state.Start = i;
                    }
                    state.Depth++;
                    continue;
                }
                if (c == depthCloser)
                {
                    state.Depth--;
                    if (state.Depth == 0)
                    {
                        //return our chonk
                        state.End = i + 1;
                        yield return (buffer, state.Start..state.End);
                        state = new(false, false, 0, 0, -1, -1);
                        continue;
                    }
                }
            }
            if (state.End == -1)
            {
                //our buffer was not big enough
                state.Offset = buffer.Length;
                Array.Resize(ref buffer, buffer.Length * 2);
            }
        }
    }

    public record struct CollectorState(bool InString, bool SkipNext, int Offset, int Depth, int Start = -1, int End = -1);

}
