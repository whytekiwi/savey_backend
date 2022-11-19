using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Savey
{
    /// <summary>
    /// Static set of utility functions to be used
    /// </summary>
    public static class Utilities
    {
        private const int idLength = 6;
        internal static readonly char[] chars =
            "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890".ToCharArray();


        /// <summary>
        /// Write a JSON object to a stream
        /// </summary>
        /// <param name="value">The JSON to encode</param>
        /// <param name="stream">The stream to write data to</param>
        /// <remarks>
        /// This is mainly used for Azure Blob Storage, as we can't upload a file from disk.
        /// So instead we encode the JSON into memory, and upload that stream.
        /// </remarks>
        public static async Task WriteJsonToStreamAsync(JToken value, Stream stream)
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
        public static Task<JToken> ReadJsonFromStreamAsync(Stream stream)
        {
            using StreamReader streamReader = new StreamReader(stream);
            using JsonReader jsonReader = new JsonTextReader(streamReader);
            return JToken.ReadFromAsync(jsonReader);
        }

        /// <summary>
        /// Generate a new ID to be used as necessary
        /// </summary>
        /// <param name="size">The size of the id to genrate. Defaults to <see cref="idLength"/></param>
        /// <returns>A new id to use</returns>
        /// <remarks>There is no guarantee that this ID is unique, it must be used with caution
        public static string GenerateId(int size = idLength)
        {
            byte[] data = new byte[4 * size];
            using (var crypto = RandomNumberGenerator.Create())
            {
                crypto.GetBytes(data);
            }
            StringBuilder result = new StringBuilder(size);
            for (int i = 0; i < size; i++)
            {
                var rnd = BitConverter.ToUInt32(data, i * 4);
                var idx = rnd % chars.Length;

                result.Append(chars[idx]);
            }

            return result.ToString();
        }
    }
}