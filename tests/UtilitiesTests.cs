using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Savey.Tests;

public class UtilitiesTests
{        
    // A generic "large" object to use for serialisaion
    private static readonly dynamic largeObject = new
    {
        Name = "Test Name",
        Age = 12,
        Address = new
        {
            Street = "Test Street",
            City = "Test City"
        }
    };

    [Fact]
    public async Task WriteJsonToStream_KeepsStreamOpen()
    {
        // Set up
        JToken json = JToken.FromObject(largeObject);
        using MemoryStream stream = new MemoryStream();

        // Act
        await Utilities.WriteJsonToStream(json, stream);

        // Test
        Assert.Equal(0, stream.Position); // This will fail if steam is closed
    }

    [Fact]
    public async Task WriteJsonToStream_HasAllProperties()
    {
        // Set up
        JObject json = JObject.FromObject(largeObject);
        using MemoryStream stream = new MemoryStream();

        // Act
        await Utilities.WriteJsonToStream(json, stream);

        // Deserialise stream
        using StreamReader streamReader = new StreamReader(stream);
        using JsonTextReader jsonReader = new JsonTextReader(streamReader);
        JToken parsed = await JObject.ReadFromAsync(jsonReader);

        // Test
        TestJsonAgainstObject(parsed);
    }

    [Fact]
    public async Task ReadJsonFromStream_HasAllProperties()
    {
        // Set up
        string json = ((JObject)JToken.FromObject(largeObject)).ToString();
        using var stream = await WriteDataToSteam(json);

        // Act
        JToken parsed = await Utilities.ReadJsonFromStream(stream);

        // Test
        TestJsonAgainstObject(parsed);
    }

    [Fact]
    public async Task ReadJsonFromStream_ThorwsExceptionOnBadJson()
    {
        // Set up
        string json = "{";
        using var stream = await WriteDataToSteam(json);

        // Act / Test
        await Assert.ThrowsAsync<JsonReaderException>(() => Utilities.ReadJsonFromStream(stream));
    }
    
    // Write a string to a stream, allowing it to be used as required
    private static async Task<Stream> WriteDataToSteam(string json)
    {
        Stream memoryStream = new MemoryStream();
        using StreamWriter writer = new StreamWriter(memoryStream, leaveOpen: true);
        await writer.WriteAsync(json);

        await writer.FlushAsync();
        memoryStream.Seek(0, SeekOrigin.Begin);

        return memoryStream;
    }

    // Test the state of JSON against the desired state of the object
    private static void TestJsonAgainstObject(JToken json)
    {
        JToken address = json["Address"];

        // Test
        Assert.Equal(largeObject.Name, json["Name"].Value<string>());
        Assert.Equal(largeObject.Age, json["Age"].Value<int>());
        Assert.Equal(largeObject.Address.Street, address["Street"].Value<string>());
        Assert.Equal(largeObject.Address.City, address["City"].Value<string>());
    }
}