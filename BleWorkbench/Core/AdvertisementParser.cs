using System;
using System.Collections.Generic;
using System.Text;
using BleWorkbench.Models;

namespace BleWorkbench.Core
{
    /// <summary>
    /// Parses a raw advertising/scan-response payload (a sequence of
    /// length-prefixed AD structures) into sections and applies the recognised
    /// fields onto a <see cref="BleDeviceInfo"/>. Used by the external HCI
    /// sniffer (the WinRT watcher pre-parses sections for us).
    /// </summary>
    public static class AdvertisementParser
    {
        public static List<AdvertisementSection> ParseSections(byte[] data)
        {
            var list = new List<AdvertisementSection>();
            if (data == null) return list;
            int i = 0;
            while (i < data.Length)
            {
                int len = data[i];
                if (len == 0) break;            // end of significant data
                if (i + len >= data.Length + 0 && i + 1 + len > data.Length) break; // truncated
                if (i + 1 >= data.Length) break;
                byte type = data[i + 1];
                int dataLen = len - 1;
                if (dataLen < 0) break;
                int start = i + 2;
                if (start + dataLen > data.Length) dataLen = Math.Max(0, data.Length - start);
                var payload = new byte[dataLen];
                Array.Copy(data, start, payload, 0, dataLen);
                list.Add(new AdvertisementSection { DataType = type, Data = payload });
                i += 1 + len;
            }
            return list;
        }

        public static void ApplyToDevice(BleDeviceInfo info, List<AdvertisementSection> sections)
        {
            if (info == null || sections == null) return;
            info.Sections.Clear();
            info.Sections.AddRange(sections);
            info.ServiceUuids.Clear();
            info.ManufacturerData.Clear();

            foreach (var s in sections)
            {
                switch (s.DataType)
                {
                    case 0x08:
                    case 0x09:
                        info.Name = Encoding.UTF8.GetString(s.Data);
                        break;
                    case 0x0A:
                        if (s.Data.Length > 0) info.TxPower = (sbyte)s.Data[0];
                        break;
                    case 0x02:
                    case 0x03:
                        for (int k = 0; k + 1 < s.Data.Length; k += 2)
                            info.ServiceUuids.Add(AssignedNumbers.GuidFrom16Bit((ushort)(s.Data[k] | (s.Data[k + 1] << 8))));
                        break;
                    case 0x06:
                    case 0x07:
                        for (int k = 0; k + 15 < s.Data.Length; k += 16)
                        {
                            var le = new byte[16];
                            Array.Copy(s.Data, k, le, 0, 16);
                            info.ServiceUuids.Add(AssignedNumbers.GuidFromLittleEndian(le));
                        }
                        break;
                    case 0xFF:
                        if (s.Data.Length >= 2)
                        {
                            ushort company = (ushort)(s.Data[0] | (s.Data[1] << 8));
                            var rest = new byte[s.Data.Length - 2];
                            Array.Copy(s.Data, 2, rest, 0, rest.Length);
                            info.ManufacturerData[company] = rest;
                        }
                        break;
                }
            }
        }
    }
}
