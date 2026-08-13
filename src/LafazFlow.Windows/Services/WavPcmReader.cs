using System.IO;
using System.Text;

namespace LafazFlow.Windows.Services;

public sealed record WavPcmData(byte[] Pcm, uint SampleCount, uint AudioFormat);

public static class WavPcmReader
{
    public static WavPcmData? Read(string path)
    {
        try
        {
            var bytes = File.ReadAllBytes(path);
            var position = 12;
            uint dataSize = 0;
            uint sampleRate = 0;
            ushort channels = 0;
            ushort bits = 0;
            var found = false;
            while (position + 8 <= bytes.Length)
            {
                var id = Encoding.ASCII.GetString(bytes, position, 4);
                var size = BitConverter.ToInt32(bytes, position + 4);
                if (id == "fmt " && position + 8 + 16 <= bytes.Length)
                {
                    channels = BitConverter.ToUInt16(bytes, position + 8 + 2);
                    sampleRate = BitConverter.ToUInt32(bytes, position + 8 + 4);
                    bits = BitConverter.ToUInt16(bytes, position + 8 + 14);
                }
                else if (id == "data")
                {
                    dataSize = (uint)size;
                    found = true;
                    break;
                }

                position += 8 + size + (size % 2);
            }

            if (!found || sampleRate != 16000 || channels != 1 || bits != 16)
            {
                return null;
            }

            var pcm = new byte[dataSize];
            Array.Copy(bytes, position + 8, pcm, 0, dataSize);
            return new WavPcmData(pcm, dataSize / 2, WhisperPipeProtocol.AudioFormatPcm16kMono);
        }
        catch
        {
            return null;
        }
    }
}
