using System.IO;
using System.Text;

namespace LafazFlow.Windows.Services;

public sealed record WavFileInfo(
    long FileByteCount,
    long DataSize,
    long SampleCount,
    long DurationMilliseconds,
    long ByteRate);

public static class WavFileValidator
{
    public static WavFileInfo? Inspect(string path)
    {
        try
        {
            var bytes = File.ReadAllBytes(path);
            var position = 12;
            long? dataSize = null;
            long byteRate = 0;
            while (position + 8 <= bytes.Length)
            {
                var id = Encoding.ASCII.GetString(bytes, position, 4);
                var size = BitConverter.ToInt32(bytes, position + 4);
                if (id == "fmt " && position + 8 + 12 <= bytes.Length)
                {
                    byteRate = BitConverter.ToInt32(bytes, position + 8 + 8);
                }

                if (id == "data")
                {
                    dataSize = size;
                    break;
                }

                position += 8 + size + (size % 2);
            }

            if (dataSize is null || byteRate <= 0)
            {
                return null;
            }

            var sampleCount = dataSize.Value / 2;
            return new WavFileInfo(
                bytes.LongLength,
                dataSize.Value,
                sampleCount,
                sampleCount * 1000 / (byteRate / 2),
                byteRate);
        }
        catch
        {
            return null;
        }
    }
}
