// using System.Buffers;
// using System.Text;
// using System.Text.Json;


// namespace Nexus.Json;


// public class JsonTokenDataProvider
// {
    
//     public static IEnumerable<JsonTokenData> GetData(Stream utf8source)
//     {
//          /*
//         what you want to do is read into the buffer
//         if there is some left over, shift it to the start
//         and on the next read into the buffer, skip the leftover count, and only fill the remaining part of the buffer
//         if the jsonreader was unable to parse ANYTHING, then resize the buffer
//         */
//         byte[] buffer = new byte[8];
//         JsonTokenData[] jsonTokenDataBuffer = new JsonTokenData[buffer.Length / 4];
//         JsonReaderState readerState = new(new JsonReaderOptions() { });


//         int leftoverLength = 0;
//         int bytesRead;
//         while ((bytesRead = utf8source.Read(buffer, leftoverLength, buffer.Length - leftoverLength)) > 0)
//         {
//             bool isFinalBlock = bytesRead < buffer.Length - leftoverLength;
//             int blockSize = bytesRead + leftoverLength;
//             Span<byte> readerBuffer = buffer[0..blockSize];
//             Utf8JsonReader reader = new(readerBuffer, isFinalBlock, readerState);


//             int tokensBufferIndex = 0;


//             while (reader.Read())
//             {
//                 jsonTokenDataBuffer[tokensBufferIndex] = new(ConvertToMemory(reader), reader.TokenType);
//                 tokensBufferIndex++;
//             }


//             readerState = reader.CurrentState;
//             int bytesConsumed = (int)reader.BytesConsumed;
//             leftoverLength = 0;


//             //can't reference our reader after this point (ref struct + after yield == cry)
//             for (int i = 0; i < tokensBufferIndex; i++)
//             {
//                 yield return jsonTokenDataBuffer[i];
//             }


//             //this is true if buffer cuts off in-progress json token
//             if (bytesConsumed < blockSize)
//             {
//                 //create a span over the leftover portion
//                 ReadOnlySpan<byte> leftover = buffer.AsSpan(start: bytesConsumed);


//                 //resize our buffer for next time if we were unable to parse any tokens (meaning a single token is too big too fit)
//                 if (bytesConsumed == 0)
//                 {
//                     Array.Resize(ref buffer, buffer.Length * 2);
//                     Array.Resize(ref jsonTokenDataBuffer, jsonTokenDataBuffer.Length * 2);
//                 }


//                 //copy our left over to the start of the buffer for next time
//                 leftover.CopyTo(buffer);
//                 //note our leftover length, which controls where our buffer starts to get filled
//                 leftoverLength = leftover.Length;
//             }


           
//         }
//     }


//     public static async IAsyncEnumerable<JsonTokenData> GetDataAsync(Stream utf8source)
//     {
//         /*
//         what you want to do is read into the buffer
//         if there is some left over, shift it to the start
//         and on the next read into the buffer, skip the leftover count, and only fill the remaining part of the buffer
//         if the jsonreader was unable to parse ANYTHING, then resize the buffer
//         */
//         byte[] buffer = new byte[50];
//         JsonTokenData[] jsonTokenDataBuffer = new JsonTokenData[buffer.Length / 4];
//         JsonReaderState readerState = new(new JsonReaderOptions() { });


//         int leftoverLength = 0;
//         int bytesRead;
//         while ((bytesRead = await utf8source.ReadAsync(buffer, leftoverLength, buffer.Length - leftoverLength)) > 0)
//         {
//             bool isFinalBlock = bytesRead < buffer.Length - leftoverLength;
//             int blockSize = bytesRead + leftoverLength;
//             Span<byte> readerBuffer = buffer[0..blockSize];
//             Utf8JsonReader reader = new(readerBuffer, isFinalBlock, readerState);


//             int tokensBufferIndex = 0;


//             while (reader.Read())
//             {
//                 jsonTokenDataBuffer[tokensBufferIndex] = new(ConvertToMemory(reader), reader.TokenType);
//                 tokensBufferIndex++;
//             }


//             readerState = reader.CurrentState;
//             int bytesConsumed = (int)reader.BytesConsumed;
//             leftoverLength = 0;


//             //can't reference our reader after this point (ref struct + after yield == cry)
//             for (int i = 0; i < tokensBufferIndex; i++)
//             {
//                 yield return jsonTokenDataBuffer[i];
//             }


//             //this is true if buffer cuts off in-progress json token
//             if (bytesConsumed < blockSize)
//             {
//                 //create a span over the leftover portion
//                 ReadOnlySpan<byte> leftover = buffer.AsSpan(start: bytesConsumed);


//                 //resize our buffer for next time if we were unable to parse any tokens (meaning a single token is too big too fit)
//                 if (bytesConsumed == 0)
//                 {
//                     Array.Resize(ref buffer, buffer.Length * 2);
//                     Array.Resize(ref jsonTokenDataBuffer, jsonTokenDataBuffer.Length * 2);
//                 }


//                 //copy our left over to the start of the buffer for next time
//                 leftover.CopyTo(buffer);
//                 //note our leftover length, which controls where our buffer starts to get filled
//                 leftoverLength = leftover.Length;
//             }


           
//         }
//     }


//     public static ReadOnlyMemory<byte> ConvertToMemory(in Utf8JsonReader utf8JsonReader)
//     {
//         return utf8JsonReader.HasValueSequence switch
//         {
//             false => utf8JsonReader.ValueSpan.ToArray(),
//             _ => utf8JsonReader.ValueSequence.ToArray()
//         };
//     }
// }




// public interface IJsonTokenDataConsumer
// {
//     ValueTask Consume(JsonTokenData jsonReaderData, JsonTokenAddress jsonTokenAddress);
// }



// n
