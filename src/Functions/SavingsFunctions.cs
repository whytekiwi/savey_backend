using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using Azure.Storage.Blobs.Specialized;
using System;
using Azure;
using Azure.Storage.Blobs.Models;
using System.IO;

namespace Savey
{
    public class SavingsFunctions
    {
        private readonly ISavingsFileDataManager dataManager;

        public SavingsFunctions(ISavingsFileDataManager dataManager)
        {
            this.dataManager = dataManager;
        }

        [FunctionName("GetWish")]
        public async Task<IActionResult> GetWishAsync(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "Wish/{id}")] HttpRequest req,
            ILogger log, string id)
        {
            var savingsFile = await dataManager.GetSavedWishAsync(id);
            return new OkObjectResult(savingsFile);
        }

        [FunctionName("GetColors")]
        public async Task<IActionResult> GetColorsAsync(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "Colors")] HttpRequest req,
            ILogger log)
        {
            var colors = await dataManager.GetColorsAsync();
            return new OkObjectResult(colors);
        }

        [FunctionName("CreateWish")]
        public async Task<IActionResult> CreateWishAsync(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "Wish")] HttpRequest req,
            ILogger log)
        {
            var wish = await dataManager.CreateNewWishAsync();
            return new OkObjectResult(wish);
        }

        [FunctionName("SaveWish")]
        public async Task<IActionResult> SaveWishAsync(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "Wish")] HttpRequest req,
            ILogger log)
        {
            Wish wish;

            try
            {
                JToken json = await Utilities.ReadJsonFromStreamAsync(req.Body);
                wish = json.ToObject<Wish>();
            }
            catch (JsonReaderException ex)
            {
                string errorMessage = string.Format("Could not parse JSON from request body: Path '{0}', line {1}, position {2}.",
                    ex.Path,
                    ex.LineNumber,
                    ex.LinePosition);
                return new BadRequestObjectResult(errorMessage);
            }

            await dataManager.SaveWishAsync(wish);
            return new NoContentResult();
        }

        [FunctionName("GetFile")]
        public async Task<IActionResult> GetFileAsync(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "Wish/{id}/Download/{filename}")] HttpRequest req,
            ILogger log,
            string id,
            string filename)
        {
            return await dataManager.DownloadFileAsync(id, filename);
        }

        [FunctionName("UploadPhoto")]
        public async Task<IActionResult> UploadPhotoAsync(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "Wish/{id}/Photo")] HttpRequest req,
            ILogger log,
            string id)
        {
            IFormFile file;
            try
            {
                var formDate = await req.ReadFormAsync();
                file = req.Form.Files["photo"];
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Could not read file from incoming request");
                return new BadRequestResult();
            }

            return await SaveFileWithLeaseAsync(file, id, log, (wish) => wish.PhotoFileName, (wish, blobName) => wish.PhotoFileName = blobName);
        }

        [FunctionName("UploadVideo")]
        public async Task<IActionResult> UploadVideoAsync(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "Wish/{id}/Video")] HttpRequest req,
            ILogger log,
            string id)
        {
            IFormFile file;
            try
            {
                var formDate = await req.ReadFormAsync();
                file = req.Form.Files["video"];
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Could not read file from incoming request");
                return new BadRequestResult();
            }

            return await SaveFileWithLeaseAsync(file, id, log, (wish) => wish.VideoFileName, (wish, blobName) => wish.VideoFileName = blobName);
        }

        private async Task<IActionResult> SaveFileWithLeaseAsync(
            IFormFile file,
            string id,
            ILogger log,
            Func<Wish, string?> filenameSelector,
            Action<Wish, string> wishUpdateDelegate)
        {

            if (file == null)
            {
                return new BadRequestObjectResult("Could not read file");
            }

            BlobLeaseClient lease;
            try
            {
                lease = await dataManager.GetLeaseAsync(id);
            }
            catch (RequestFailedException ex)
            {
                if (ex.ErrorCode == BlobErrorCode.BlobNotFound)
                {
                    return new BadRequestObjectResult("Wish does not exist");
                }
                if (ex.ErrorCode == BlobErrorCode.LeaseAlreadyPresent)
                {
                    return new BadRequestObjectResult("Already uploading file");
                }
                throw;
            }

            try
            {
                var wish = await dataManager.GetSavedWishAsync(id, lease.LeaseId);
                if (wish == null)
                {
                    return new NoContentResult();
                }

                var blobName = await dataManager.UploadFileAsync(file, id);
                var fileName = filenameSelector(wish);
                if (!string.IsNullOrEmpty(fileName))
                {
                    await dataManager.DeleteFileAsync(id, fileName);
                }
                wishUpdateDelegate(wish, blobName);
                await dataManager.SaveWishAsync(wish, leaseId: lease.LeaseId);

                return new OkObjectResult(wish);
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Could not upload file to blob storage");
                return new BadRequestResult();
            }
            finally
            {
                await lease.ReleaseAsync();
            }
        }
    }
}
