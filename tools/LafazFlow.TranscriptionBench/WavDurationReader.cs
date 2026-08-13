using System.IO;

namespace LafazFlow.TranscriptionBench;

public static class WavDurationReader
{
    public static long? ReadMilliseconds(string path)
    {
        try
        {
            var bytes = File.ReadAllBytes(path);
            var position = 12;
            long? dataSize = null;
            var byteRate = 0L;
            while (position + 8 <= bytes.Length)
            {
                var id = System.Text.Encoding.ASCII.GetString(bytes, position, 4);
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

            return (long)Math.Round(dataSize.Value * 1000.0 / byteRate);
        }
        catch
        {
            return null;
        }
    }
}
