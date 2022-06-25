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
    /// <summary>
    /// Functions related to user data
    /// </summary>
    public class SavingsFunctions
    {
        private readonly ISavingsFileDataManager dataManager;

        public SavingsFunctions(ISavingsFileDataManager dataManager)
        {
            this.dataManager = dataManager;
        }

        /// <summary>
        /// Get the users data from storage
        /// </summary>
        [FunctionName("GetSavingsFile")]
        public async Task<IActionResult> RunGet(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "SavingsFile")] HttpRequest req,
            ILogger log)
        {
            string id = Utilities.GetUserIdFromRequest(req);
            if (string.IsNullOrEmpty(id))
            {
                return new UnauthorizedResult();
            }

            var savingsFile = await dataManager.GetSavedValueAsync(id);

            return new OkObjectResult(savingsFile);
        }

        /// <summary>
        /// Save the users data to storage
        /// </summary>
        [FunctionName("PostSavingsFile")]
        public async Task<IActionResult> RunPost(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "SavingsFile")] HttpRequest req,
            ILogger log)
        {
            string id = Utilities.GetUserIdFromRequest(req);
            if (string.IsNullOrEmpty(id))
            {
                return new UnauthorizedResult();
            }

            JToken json;

            try
            {
                json = await Utilities.ReadJsonFromStream(req.Body);
            }
            catch (JsonReaderException ex)
            {
                string errorMessage = string.Format("Could not parse JSON from request body: Path '{0}', line {1}, position {2}.",
                    ex.Path,
                    ex.LineNumber,
                    ex.LinePosition);
                return new BadRequestObjectResult(errorMessage);
            }

            await dataManager.SaveValueAsync(json, id);

            return new NoContentResult();
        }
    }
}
