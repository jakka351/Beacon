using Windows.Storage.Streams;

namespace BleWorkbench.Ble
{
    /// <summary>Conversions between WinRT <see cref="IBuffer"/> and managed byte arrays.</summary>
    internal static class BleBuffers
    {
        public static byte[] ToBytes(IBuffer buffer)
        {
            if (buffer == null || buffer.Length == 0) return new byte[0];
            var reader = DataReader.FromBuffer(buffer);
            var bytes = new byte[buffer.Length];
            reader.ReadBytes(bytes);
            return bytes;
        }

        public static IBuffer FromBytes(byte[] data)
        {
            var writer = new DataWriter();
            if (data != null && data.Length > 0) writer.WriteBytes(data);
            return writer.DetachBuffer();
        }
    }
}
