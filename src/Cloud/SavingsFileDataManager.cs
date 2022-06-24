using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Savey
{
    public interface ISavingsFileDataManager
    {
        Task<JToken?> GetSavedValueAsync(string id);
        Task SaveValueAsync(JToken value, string id);
    }

    /// <summary>
    /// Reads and writes data to Azure Blob Storage
    /// </summary>
    public class SavingsFileDataManager : ISavingsFileDataManager
    {
        // The storage container to save data in
        private readonly BlobContainerClient cloudContainer;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="containerClientProvider">Provides instances of <see cref="BlobContainerClient"/></param>
        public SavingsFileDataManager(IBlobContainerClientProvider containerClientProvider)
        {
            this.cloudContainer = containerClientProvider.CloudContainer;
        }

        /// <summary>
        /// Get the value saved in Azure blob storage
        /// </summary>
        /// <param name="id">The id for the user fetching data</param>
        /// <returns>The persisted data for the user</returns>
        /// <remarks>Can return null if the user has not uploaded any data yet</remarks>
        public async Task<JToken?> GetSavedValueAsync(string id)
        {
            var blob = cloudContainer.GetBlobClient(GetFileName(id));

            try
            {
                using var blobStream = new MemoryStream();
                await blob.DownloadToAsync(blobStream);

                await blobStream.FlushAsync();
                blobStream.Seek(0, SeekOrigin.Begin);

                return await Utilities.ReadJsonFromStream(blobStream);
            }
            catch (RequestFailedException ex)
            {
                if (ex.Status == (int)HttpStatusCode.NotFound)
                {
                    // The blob doesn't exist, which is perfectly valid
                    return null;
                }

                // Something else failed, we can't fix it
                throw;
            }
            catch (JsonReaderException)
            {
                // Read the file correctly, but it didn't have valid JSON
                await blob.DeleteAsync();

                return null;
            }

        }

        /// <summary>
        /// Save user data to Azure blob storage
        /// </summary>
        /// <param name="value">The user data to save</param>
        /// <param name="id">The id for the user</param>
        public async Task SaveValueAsync(JToken value, string id)
        {
            using MemoryStream stream = new MemoryStream();
            await Utilities.WriteJsonToStream(value, stream);

            BlobUploadOptions uploadOptions = new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders
                {
                    ContentType = "application/json"
                }
            };

            var blob = cloudContainer.GetBlobClient(GetFileName(id));
            await blob.UploadAsync(stream, uploadOptions);
        }

        private static string GetFileName(string id)
            => Path.ChangeExtension(id, "json");
    }
}