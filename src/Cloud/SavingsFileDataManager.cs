using System.Collections.Generic;
using System.IO;
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
        Task<Wish?> GetSavedWishAsync(string id);
        Task SaveWishAsync(Wish wish, bool overwrite = true);
        Task<Wish> CreateNewWishAsync();
        Task<Dictionary<string, int>> GetColorsAsync();
    }

    /// <summary>
    /// Reads and writes data to Azure Blob Storage
    /// </summary>
    public class SavingsFileDataManager : ISavingsFileDataManager
    {
        // The storage container to save data in
        private readonly BlobContainerClient cloudContainer;

        private const string colorMetadataKey = "Color";

        public SavingsFileDataManager(IBlobContainerClientProvider containerClientProvider)
        {
            this.cloudContainer = containerClientProvider.CloudContainer;
        }

        public async Task<Wish?> GetSavedWishAsync(string id)
        {
            var blob = cloudContainer.GetBlobClient(GetFileName(id));

            try
            {
                using var blobStream = new MemoryStream();
                await blob.DownloadToAsync(blobStream);

                await blobStream.FlushAsync();
                blobStream.Seek(0, SeekOrigin.Begin);

                var json = await Utilities.ReadJsonFromStreamAsync(blobStream);
                return json.ToObject<Wish>();
            }
            catch (RequestFailedException ex)
            {
                if (ex.ErrorCode == BlobErrorCode.BlobNotFound)
                {
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

        public async Task SaveWishAsync(Wish wish, bool overwrite = true)
        {
            JToken json = JObject.FromObject(wish);
            using MemoryStream stream = new MemoryStream();
            await Utilities.WriteJsonToStreamAsync(json, stream);

            BlobUploadOptions uploadOptions = new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders
                {
                    ContentType = "application/json"
                }
            };

            if (!string.IsNullOrEmpty(wish.Color))
            {
                uploadOptions.Metadata = new Dictionary<string, string>
                {
                    {colorMetadataKey, wish.Color}
                };
            }

            if (!overwrite)
            {
                uploadOptions.Conditions = new BlobRequestConditions
                {
                    IfNoneMatch = ETag.All
                };
            }

            var blob = cloudContainer.GetBlobClient(GetFileName(wish.Id));
            await blob.UploadAsync(stream, uploadOptions);
        }

        public async Task<Wish> CreateNewWishAsync()
        {
            Wish newWish;
            bool success;
            do
            {
                newWish = new Wish();

                try
                {
                    await SaveWishAsync(newWish, false);
                    success = true;
                }
                catch (RequestFailedException ex)
                {
                    if (ex.ErrorCode == BlobErrorCode.BlobAlreadyExists)
                    {
                        // This blob already exists, we need to generate a new id
                        success = false;
                        continue;
                    }

                    throw;
                }
            } while (!success);

            return newWish;
        }

        public async Task<Dictionary<string, int>> GetColorsAsync()
        {
            Dictionary<string, int> colorLookup = new Dictionary<string, int>();
            var resultsSegment = cloudContainer.GetBlobsAsync()
                .AsPages();

            await foreach (var page in resultsSegment)
            {
                foreach (var blob in page.Values)
                {
                    if (blob.Metadata.TryGetValue(colorMetadataKey, out string? color) && !string.IsNullOrEmpty(color))
                    {
                        if (!colorLookup.TryGetValue(color, out int count))
                        {
                            count = 0;
                        }

                        colorLookup[color] = count + 1;
                    }
                }
            }

            return colorLookup;
        }

        private static string GetFileName(string id)
            => Path.Combine(id, Path.ChangeExtension(id, "json"));
    }
}