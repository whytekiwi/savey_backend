using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

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
    }
}
