using System.Text;
using System.Text.Json;

namespace JsonStreaming.Tests;

public class JsonArrayTests
{
    [Fact]
    public async Task BytesTest()
    {
        var sample = """
        [
            {
                a: "a",
                x: 1
            },
            {
                a: "b",
                x: 2
            }
        ]
        """;

        Stream utf8JsonStream = GetStreamFromString(sample);
        var results = await JsonStreaming.JsonArray.Get(utf8JsonStream).ToListAsync();
        var actual = results.Select(x => x.Item1[x.Item2]).ToList();

        var expected = new List<byte[]>() {
            """
            {
                    a: "a",
                    x: 1
                }
            """u8.ToArray(),
            """
            {
                    a: "b",
                    x: 2
                }
            """u8.ToArray()
        };
        
        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task SerializeTest()
    {
        var expected = new List<Item>() {
           new("\"a", 1, new ("\"S\""), new ColItem[]{new ("1\""), new ("2\"\"\"")} ),
           new("b\"", 1, new ("\"X\""), new ColItem[]{new ("3\""), new ("4\"\"")} ),
        };
        var expectedSerialized = JsonSerializer.Serialize(expected);
        Stream utf8JsonStream = GetStreamFromString(expectedSerialized);
        var results = await JsonStreaming.JsonArray.Get<Item>(utf8JsonStream, new(){PropertyNameCaseInsensitive = true}).ToListAsync();
        var actualSerialized = JsonSerializer.Serialize(results);
        Assert.Equal(expectedSerialized, actualSerialized);
    }

    internal record Item {
        public Item()
        {
        }

        public Item(string a, int x, SubItem subItem, ColItem[] colItems)
        {
            A = a;
            X = x;
            SubItem = subItem;
            ColItems = colItems;
        }

        public string A {get;set;} = string.Empty;
        public int X {get;set;}
        public SubItem SubItem {get;set;}
        public ColItem[] ColItems {get;set;}

    }

    internal record SubItem 
    {
        public SubItem(string s)
        {
            S = s;
        }

        public string S {get;set;} = string.Empty;
    }

    internal record ColItem 
    {
        public ColItem(string c)
        {
            C = c;
        }

        public string C {get;set;} = string.Empty;
    }

    private Stream GetStreamFromString(string v)
    {
        return new MemoryStream(Encoding.UTF8.GetBytes(v));
    }
}