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
            var savingsFile = await dataManager.CreateNewWishAsync();
            return new OkObjectResult(savingsFile);
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

        [FunctionName("GetPhoto")]
        public async Task<IActionResult> GetPhotoAsync(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "Wish/{id}/Photo")] HttpRequest req,
            ILogger log,
            string id)
        {
            var wish = await dataManager.GetSavedWishAsync(id);
            if (wish == null)
            {
                return new NoContentResult();
            }

            if (!string.IsNullOrEmpty(wish.PhotoUrl))
            {
                var blob = await dataManager.DownloadFileAsync(wish.PhotoUrl);
                return new BlobResult(blob);
            }
            return new NoContentResult();
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
                    return new BadRequestObjectResult("Already uploading photo");
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

                var url = await dataManager.UploadFileAsync(file, id);
                if (!string.IsNullOrEmpty(wish.PhotoUrl))
                {
                    await dataManager.DeleteFileAsync(wish.PhotoUrl);
                }
                wish.PhotoUrl = url;
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
