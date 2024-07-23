using System.Text;
using Nexus.Core;

namespace Tests;

public class DevTests
{
    [Fact]
    public async Task Test1Async()
    {
        // Your input string
        string inputString = """
        {
            "a" : 1,
            "b" : 
            {
                "c" : 
                {
                    "d": 2
                },
                "e" : 3
            },
            "f" : 4
        }
        """;

        // Convert the string to UTF-8 bytes
        byte[] utf8Bytes = Encoding.UTF8.GetBytes(inputString);

        // Create a MemoryStream from the UTF-8 bytes
        using (MemoryStream stream = new MemoryStream(utf8Bytes))
        {
            stream.Position = 0; // Reset the stream position to the beginning
            var paths = await NsonTokenizer.GetIEnumerableTest(stream, new NsonSettings()).ToListAsync();
            Assert.Equal(["a.", "b.", "b.c.", "b.c.d.", "b.e.", "f."], paths);
        }

    }

    [Fact]
    public void PathBuffer_Should_Work()
    {
        PathBuffer sut = new(new byte[100]);

        sut.Update("a"u8, 1);
        Assert.Equal("a.", Encoding.UTF8.GetString(sut.Path));

        sut.Update("b"u8, 1);
        Assert.Equal("b.", Encoding.UTF8.GetString(sut.Path));
        
        sut.Update("c"u8, 2);
        Assert.Equal("b.c.", Encoding.UTF8.GetString(sut.Path));
        
        sut.Update("d"u8, 3);
        Assert.Equal("b.c.d.", Encoding.UTF8.GetString(sut.Path));
        
        sut.Update("e"u8, 2);
        Assert.Equal("b.e.", Encoding.UTF8.GetString(sut.Path));
        
        sut.Update("f"u8, 1);
        Assert.Equal("f.", Encoding.UTF8.GetString(sut.Path));
    }
}