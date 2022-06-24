using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Savey
{
    /// <summary>
    /// Static set of utility functions to be used
    /// </summary>
    public static class Utilities
    {
        private const string headerIdKey = "saveyUserId";

        /// <summary>
        /// Get the identifier for the user from the inconing HTTP request
        /// </summary>
        /// <param name="req">The request to fetch the user ID from</param>
        /// <returns>The ID for the user</returns>
        /// <remarks>
        /// Very temporary. We will implement proper user auth later on
        /// </remarks>
        public static string GetUserIdFromRequest(HttpRequest req)
        {
            var header = req.Headers.FirstOrDefault(header => header.Key == headerIdKey);
            return header.Value;
        }

        /// <summary>
        /// Write a JSON object to a stream
        /// </summary>
        /// <param name="value">The JSON to encode</param>
        /// <param name="stream">The stream to write data to</param>
        /// <remarks>
        /// This is mainly used for Azure Blob Storage, as we can't upload a file from disk.
        /// So instead we encode the JSON into memory, and upload that stream.
        /// </remarks>
        public static async Task WriteJsonToStream(JToken value, Stream stream)
        {
            using StreamWriter streamWriter = new StreamWriter(stream, leaveOpen: true);
            using JsonTextWriter jsonWriter = new JsonTextWriter(streamWriter);
            jsonWriter.CloseOutput = false;
            await value.WriteToAsync(jsonWriter);
            await jsonWriter.FlushAsync();

            // Rewind the stream
            stream.Seek(0, SeekOrigin.Begin);
        }

        /// <summary>
        /// Deserialize a stream of bytes into a valid JSON object
        /// </summary>
        /// <param name="stream">The steam to read</param>
        /// <returns>The JSON reprsentation of the object</returns>
        public static Task<JToken> ReadJsonFromStream(Stream stream)
        {
            using StreamReader streamReader = new StreamReader(stream);
            using JsonReader jsonReader = new JsonTextReader(streamReader);
            return JToken.ReadFromAsync(jsonReader);
        }
    }
}