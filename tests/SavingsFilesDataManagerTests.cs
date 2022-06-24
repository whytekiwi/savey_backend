using System.Net;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Moq;
using Newtonsoft.Json.Linq;

namespace Savey.Tests;

public class SavingsFilesDataManagerTests
{
    [Fact]
    public async Task TestGetJsonFile_DownloadsAndParsesValidJson()
    {
        // Set up dependencies
        var mockBlobContainerProvider = new Mock<IBlobContainerClientProvider>();
        // Set up the download functionality to return valid JSON
        mockBlobContainerProvider.Setup(m => m.CloudContainer
            .GetBlobClient(It.IsAny<string>()).DownloadToAsync(It.IsAny<Stream>()))
            .Returns(async (Stream stream) =>
            {
                using StreamWriter streamWriter = new StreamWriter(stream, leaveOpen: true);
                await streamWriter.WriteAsync("{}");
                await streamWriter.FlushAsync();
                stream.Seek(0, SeekOrigin.Begin);

                return new Mock<Azure.Response>().Object;
            });

        var savingsFileDataManager = new SavingsFileDataManager(mockBlobContainerProvider.Object);

        // Act
        JToken? json = await savingsFileDataManager.GetSavedValueAsync("fakeId");

        // Test
        Assert.NotNull(json);
    }

    [Fact]
    public async Task TestGetJsonFile_HandlesNotFoundErrors()
    {
        // Set up the download functionality to throw exception
        var mockBlob = new Mock<BlobClient>();
        mockBlob.Setup(m => m.DownloadToAsync(It.IsAny<Stream>()))
            .Throws(new RequestFailedException((int)HttpStatusCode.NotFound, ""));

        // Set up dependencies
        var mockBlobContainerProvider = new Mock<IBlobContainerClientProvider>();
        mockBlobContainerProvider.Setup(m => m.CloudContainer.GetBlobClient(It.IsAny<string>()))
            .Returns(mockBlob.Object);

        var savingsFileDataManager = new SavingsFileDataManager(mockBlobContainerProvider.Object);

        // Act
        JToken? json = await savingsFileDataManager.GetSavedValueAsync("fakeId");

        // Test
        Assert.Null(json);
    }

    [Fact]
    public async Task TestGetJsonFile_ThrownsUnhandledErrors()
    {
        // Set up the download functionality to throw exception
        var mockBlob = new Mock<BlobClient>();
        mockBlob.Setup(m => m.DownloadToAsync(It.IsAny<Stream>()))
            .Throws(new RequestFailedException(""));

        // Set up dependencies
        var mockBlobContainerProvider = new Mock<IBlobContainerClientProvider>();
        mockBlobContainerProvider.Setup(m => m.CloudContainer
            .GetBlobClient(It.IsAny<string>()))
            .Returns(mockBlob.Object);
        var savingsFileDataManager = new SavingsFileDataManager(mockBlobContainerProvider.Object);

        // Act / Test
        await Assert.ThrowsAsync<RequestFailedException>(async () => await savingsFileDataManager.GetSavedValueAsync("fakeId"));
    }

    [Fact]
    public async Task TestGetJsonFile_DeletesInvalidJsonBlobs()
    {
        // Set up the download functionality to return invalid JSON
        var mockBlob = new Mock<BlobClient>();
        mockBlob.Setup(m => m.DownloadToAsync(It.IsAny<Stream>()))
            .Returns(async (Stream stream) =>
            {
                using StreamWriter streamWriter = new StreamWriter(stream, leaveOpen: true);
                await streamWriter.WriteAsync("{");
                await streamWriter.FlushAsync();
                stream.Seek(0, SeekOrigin.Begin);

                return new Mock<Azure.Response>().Object;
            });

        // Set up dependencies
        var mockBlobContainerProvider = new Mock<IBlobContainerClientProvider>();
        mockBlobContainerProvider.Setup(m => m.CloudContainer
            .GetBlobClient(It.IsAny<string>()))
            .Returns(mockBlob.Object);

        var savingsFileDataManager = new SavingsFileDataManager(mockBlobContainerProvider.Object);

        // Act
        JToken? json = await savingsFileDataManager.GetSavedValueAsync("fakeId");

        // Test
        Assert.Null(json);
        mockBlob.Verify(m => m.DeleteAsync(DeleteSnapshotsOption.None, null, It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task TestSaveJsonFile_UploadsWithContentType()
    {
        // Set up
        var mockBlobContainerProvider = new Mock<IBlobContainerClientProvider>();
        var mockBlob = new Mock<BlobClient>();
        mockBlobContainerProvider.Setup(m => m.CloudContainer
            .GetBlobClient(It.IsAny<string>()))
            .Returns(mockBlob.Object);
        var savingsFileDataManager = new SavingsFileDataManager(mockBlobContainerProvider.Object);
        JObject json = JObject.Parse("{}");

        // Ensure we validate the outgoing HTTP requests on the upload function
        mockBlob.Setup(m => m.UploadAsync(It.IsAny<Stream>(), It.IsAny<BlobUploadOptions>(), It.IsAny<CancellationToken>()))
            .Callback((Stream content, BlobUploadOptions uploadOptions, CancellationToken cancellationToken) =>
            {
                string contentType = uploadOptions.HttpHeaders.ContentType;
                Assert.Equal("application/json", contentType);
            });

        // Act
        await savingsFileDataManager.SaveValueAsync(json, "fakeId");

        // Test
        mockBlob.Verify(m => m.UploadAsync(It.IsAny<Stream>(), It.IsAny<BlobUploadOptions>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}