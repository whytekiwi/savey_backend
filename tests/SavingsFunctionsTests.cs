using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Internal;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Newtonsoft.Json.Linq;

namespace Savey.Tests
{
    public class SavingsFunctionsTests
    {
        // Mocked ILogger, allowing us to call HTTP functions progmatically
        private static readonly ILogger logger = NullLoggerFactory.Instance.CreateLogger("Test");

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
        public async Task GetFile_ReturnsData()
        {
            // Set up
            var savingsFileManager = new Mock<ISavingsFileDataManager>();
            savingsFileManager.Setup(m => m.GetSavedValueAsync(It.IsAny<string>()))
                .ReturnsAsync(() =>
                {
                    return (JObject)JObject.FromObject(largeObject);
                });
            var savingsFunctions = new SavingsFunctions(savingsFileManager.Object);
            var mockRequest = MockHttpRequest("MockUserId");

            // Act
            var response = await savingsFunctions.RunGet(mockRequest, logger);

            // Test
            savingsFileManager.Verify(m => m.GetSavedValueAsync(It.IsAny<string>()), Times.Once);
            Assert.IsType<OkObjectResult>(response);
            if (response is OkObjectResult okResponse)
            {
                Assert.IsType<JObject>(okResponse.Value);
                if (okResponse.Value is JObject json)
                {
                    TestJsonAgainstObject(json);
                }
            }
        }

        [Fact]
        public async Task GetFile_NoIdReturnsUnauthorized()
        {
            // Set up
            var savingsFileManager = new Mock<ISavingsFileDataManager>();
            var savingsFunctions = new SavingsFunctions(savingsFileManager.Object);
            var mockRequest = MockHttpRequest();

            // Act
            var response = await savingsFunctions.RunGet(mockRequest, logger);

            // Test
            Assert.IsType<UnauthorizedResult>(response);
            savingsFileManager.Verify(m => m.GetSavedValueAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task PostFile_CallsSaveValue()
        {
            // Set up
            var savingsFileManager = new Mock<ISavingsFileDataManager>();
            var savingsFunctions = new SavingsFunctions(savingsFileManager.Object);

            // Make sure when we save JSON we save the data correctly
            savingsFileManager.Setup(m => m.SaveValueAsync(It.IsAny<JToken>(), It.IsAny<string>()))
                .Callback((JToken json, string id) =>
                {
                    TestJsonAgainstObject(json);
                });

            // Set up our mock HTTP request with a body
            var mockRequest = MockHttpRequest("mockUserId");
            using Stream body = await WriteDataToSteam(JObject.FromObject(largeObject).ToString());
            mockRequest.Body = body;

            // Act
            var response = await savingsFunctions.RunPost(mockRequest, logger);

            // Test
            Assert.IsType<NoContentResult>(response);
            savingsFileManager.Verify(m => m.SaveValueAsync(It.IsAny<JToken>(), It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task PostFile_BadJsonReturnsBadRequest()
        {
            // Set up
            var savingsFileManager = new Mock<ISavingsFileDataManager>();
            var savingsFunctions = new SavingsFunctions(savingsFileManager.Object);

            // Set up our mock HTTP request with a body containing invalid JSON
            var mockRequest = MockHttpRequest("mockUserId");
            using Stream body = await WriteDataToSteam("{");
            mockRequest.Body = body;

            // Act
            var response = await savingsFunctions.RunPost(mockRequest, logger);

            // Test
            Assert.IsType<BadRequestObjectResult>(response);
            savingsFileManager.Verify(m => m.SaveValueAsync(It.IsAny<JToken>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task PostFile_NoIdReturnsUnauthorized()
        {
            // Set up
            var savingsFileManager = new Mock<ISavingsFileDataManager>();
            var savingsFunctions = new SavingsFunctions(savingsFileManager.Object);
            var mockRequest = MockHttpRequest();

            // Act
            var response = await savingsFunctions.RunPost(mockRequest, logger);

            Assert.IsType<UnauthorizedResult>(response);
            savingsFileManager.Verify(m => m.SaveValueAsync(It.IsAny<JToken>(), It.IsAny<string>()), Times.Never);
        }

        // Creates a mock HTTP request, allowing us to call HTTP functions programatically
        private static HttpRequest MockHttpRequest(string? userId = null)
        {
            var request = new DefaultHttpRequest(new DefaultHttpContext());
            if (!string.IsNullOrEmpty(userId))
            {
                request.Headers["saveyUserId"] = userId;
            }
            return request;
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
}