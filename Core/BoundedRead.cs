using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DiscordActivityMockV2
{
    internal static class BoundedRead
    {
        /// <summary>
        /// Reads a UTF-8 text stream but stops with <see cref="InvalidDataException"/> as soon as it exceeds
        /// <paramref name="maxBytes"/>, even when the server sends no Content-Length.
        /// </summary>
        public static async Task<string> ReadUtf8Async(Stream stream, int maxBytes, CancellationToken cancellationToken)
        {
            using var buffer = new MemoryStream();
            var chunk = new byte[16 * 1024];
            int bytesRead;
            while ((bytesRead = await stream.ReadAsync(chunk.AsMemory(), cancellationToken)) > 0)
            {
                if (buffer.Length + bytesRead > maxBytes)
                    throw new InvalidDataException(Localization.Get("CatalogTooLarge"));
                buffer.Write(chunk, 0, bytesRead);
            }

            buffer.Position = 0;
            using var reader = new StreamReader(buffer, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), detectEncodingFromByteOrderMarks: true);
            return await reader.ReadToEndAsync(cancellationToken);
        }
    }
}
