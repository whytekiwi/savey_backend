using Azure.Storage.Blobs.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using System.Text;

namespace Savey
{
    public class BlobResult : FileStreamResult
    {
        private static string FormatMd5Bytes(byte[] raw)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < raw.Length; i++)
            {
                sb.Append(raw[i].ToString("X2"));
            }

            return sb.ToString();
        }

        public BlobResult(BlobDownloadInfo blob)
            : base(blob.Content, blob.ContentType)
        {
            var md5 = FormatMd5Bytes(blob.Details.BlobContentHash);

            EntityTag = new EntityTagHeaderValue($"\"{md5}\"");
        }
    }
}