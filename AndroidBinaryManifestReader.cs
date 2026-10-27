using System.Buffers.Binary;
using System.Text;

namespace QuestMetadataEditor;

/// <summary>
/// Small defensive reader for the common Android binary XML chunks in AndroidManifest.xml.
/// It only reads metadata; it does not try to rewrite binary XML.
/// </summary>
internal static class AndroidBinaryManifestReader
{
    private const ushort ResStringPoolType = 0x0001;
    private const ushort ResXmlStartElementType = 0x0102;
    private const uint NoIndex = 0xFFFFFFFF;
    private const string AndroidNamespace = "http://schemas.android.com/apk/res/android";

    public static MetadataInfo Read(byte[] bytes)
    {
        var result = new MetadataInfo { ManifestFormat = "Binary Android XML" };
        if (bytes.Length < 8)
            return result;

        try
        {
            string[] strings = Array.Empty<string>();
            int offset = 0;
            int chunkCount = 0;
            while (offset + 8 <= bytes.Length && chunkCount++ < 100_000)
            {
                ushort type = U16(bytes, offset);
                ushort headerSize = U16(bytes, offset + 2);
                uint chunkSizeRaw = U32(bytes, offset + 4);
                if (chunkSizeRaw < 8 || chunkSizeRaw > int.MaxValue)
                    break;
                int chunkSize = (int)chunkSizeRaw;
                if (offset + chunkSize > bytes.Length || headerSize < 8 || headerSize > chunkSize)
                    break;

                if (type == ResStringPoolType)
                    strings = ReadStringPool(bytes, offset, chunkSize, headerSize);
                else if (type == ResXmlStartElementType && strings.Length > 0)
                    ReadStartElement(bytes, offset, chunkSize, strings, result);

                offset += chunkSize;
            }
        }
        catch (ArgumentOutOfRangeException)
        {
            // A truncated or non-standard manifest should still open as an archive.
        }
        catch (IndexOutOfRangeException)
        {
            // Ignore malformed chunks rather than crashing the whole application.
        }
        catch (OverflowException)
        {
            // Ignore suspicious lengths in malformed files.
        }

        return result;
    }

    private static string[] ReadStringPool(byte[] bytes, int start, int chunkSize, int headerSize)
    {
        if (chunkSize < 28 || start + chunkSize > bytes.Length)
            return Array.Empty<string>();

        uint stringCountRaw = U32(bytes, start + 8);
        uint styleCountRaw = U32(bytes, start + 12);
        uint flags = U32(bytes, start + 16);
        uint stringsStartRaw = U32(bytes, start + 20);
        if (stringCountRaw > 200_000 || styleCountRaw > 200_000 || stringsStartRaw > chunkSize)
            return Array.Empty<string>();

        int stringCount = (int)stringCountRaw;
        int offsetTable = start + headerSize;
        int stringsStart = start + (int)stringsStartRaw;
        if (offsetTable + stringCount * 4L > start + chunkSize || stringsStart > start + chunkSize)
            return Array.Empty<string>();

        bool isUtf8 = (flags & 0x100) != 0;
        var values = new string[stringCount];
        for (int i = 0; i < stringCount; i++)
        {
            uint stringOffsetRaw = U32(bytes, offsetTable + (i * 4));
            if (stringOffsetRaw > chunkSize || stringsStart + stringOffsetRaw >= start + chunkSize)
            {
                values[i] = string.Empty;
                continue;
            }

            int cursor = stringsStart + (int)stringOffsetRaw;
            int end = start + chunkSize;
            values[i] = isUtf8
                ? ReadUtf8String(bytes, ref cursor, end)
                : ReadUtf16String(bytes, ref cursor, end);
        }
        return values;
    }

    private static string ReadUtf8String(byte[] bytes, ref int cursor, int end)
    {
        _ = ReadLength8(bytes, ref cursor, end); // UTF-16 code-unit length
        int byteLength = ReadLength8(bytes, ref cursor, end);
        if (byteLength < 0 || cursor + (long)byteLength > end)
            return string.Empty;
        string value = Encoding.UTF8.GetString(bytes, cursor, byteLength);
        cursor += byteLength;
        return value;
    }

    private static string ReadUtf16String(byte[] bytes, ref int cursor, int end)
    {
        int length = ReadLength16(bytes, ref cursor, end);
        long byteLength = length * 2L;
        if (length < 0 || cursor + byteLength > end)
            return string.Empty;
        string value = Encoding.Unicode.GetString(bytes, cursor, (int)byteLength);
        cursor += (int)byteLength;
        return value;
    }

    private static int ReadLength8(byte[] bytes, ref int cursor, int end)
    {
        if (cursor >= end) throw new ArgumentOutOfRangeException(nameof(cursor));
        int first = bytes[cursor++];
        if ((first & 0x80) == 0) return first;
        if (cursor >= end) throw new ArgumentOutOfRangeException(nameof(cursor));
        return ((first & 0x7F) << 8) | bytes[cursor++];
    }

    private static int ReadLength16(byte[] bytes, ref int cursor, int end)
    {
        if (cursor + 2 > end) throw new ArgumentOutOfRangeException(nameof(cursor));
        int first = U16(bytes, cursor);
        cursor += 2;
        if ((first & 0x8000) == 0) return first;
        if (cursor + 2 > end) throw new ArgumentOutOfRangeException(nameof(cursor));
        int second = U16(bytes, cursor);
        cursor += 2;
        return ((first & 0x7FFF) << 16) | second;
    }

    private static void ReadStartElement(byte[] bytes, int start, int chunkSize, string[] strings, MetadataInfo info)
    {
        if (chunkSize < 36 || start + chunkSize > bytes.Length)
            return;

        uint nameIndex = U32(bytes, start + 20);
        string elementName = StringAt(strings, nameIndex);
        ushort attributeStart = U16(bytes, start + 24);
        ushort attributeSize = U16(bytes, start + 26);
        ushort attributeCount = U16(bytes, start + 28);
        if (attributeSize < 20 || attributeCount > 2_000)
            return;

        long attributeStartLong = start + 16L + attributeStart;
        if (attributeStartLong < start || attributeStartLong + attributeCount * (long)attributeSize > start + chunkSize)
            return;
        int attrs = (int)attributeStartLong;
        var attributes = new Dictionary<string, string>(StringComparer.Ordinal);

        for (int i = 0; i < attributeCount; i++)
        {
            int a = attrs + i * attributeSize;
            uint namespaceIndex = U32(bytes, a);
            uint attributeNameIndex = U32(bytes, a + 4);
            uint rawValueIndex = U32(bytes, a + 8);
            byte valueType = bytes[a + 15];
            uint valueData = U32(bytes, a + 16);
            string attributeName = StringAt(strings, attributeNameIndex);
            string ns = namespaceIndex == NoIndex ? string.Empty : StringAt(strings, namespaceIndex);
            if (attributeName.Length == 0 || (ns.Length > 0 && ns != AndroidNamespace && elementName != "manifest"))
                continue;

            string value = rawValueIndex != NoIndex
                ? StringAt(strings, rawValueIndex)
                : DecodeTypedValue(valueType, valueData, strings);
            attributes[attributeName] = value;
        }

        if (elementName == "manifest")
        {
            SetIfPresent(attributes, "package", v => info.PackageName = v);
            SetIfPresent(attributes, "versionName", v => info.VersionName = v);
            SetIfPresent(attributes, "versionCode", v => info.VersionCode = v);
        }
        else if (elementName == "uses-sdk")
        {
            SetIfPresent(attributes, "minSdkVersion", v => info.MinSdk = v);
            SetIfPresent(attributes, "targetSdkVersion", v => info.TargetSdk = v);
        }
        else if (elementName == "application")
        {
            SetIfPresent(attributes, "label", v => info.AppLabel = v);
        }
        else if (elementName.StartsWith("uses-permission", StringComparison.Ordinal) && attributes.TryGetValue("name", out string? permission))
        {
            if (!string.IsNullOrWhiteSpace(permission) && !info.Permissions.Contains(permission, StringComparer.Ordinal))
                info.Permissions.Add(permission);
        }
    }

    private static string DecodeTypedValue(byte type, uint data, string[] strings)
    {
        return type switch
        {
            0x03 => StringAt(strings, data),
            0x10 => data.ToString(System.Globalization.CultureInfo.InvariantCulture),
            0x11 => "0x" + data.ToString("X8", System.Globalization.CultureInfo.InvariantCulture),
            0x12 => data != 0 ? "true" : "false",
            0x01 => "@0x" + data.ToString("X8", System.Globalization.CultureInfo.InvariantCulture),
            0x02 => "?0x" + data.ToString("X8", System.Globalization.CultureInfo.InvariantCulture),
            _ => data.ToString(System.Globalization.CultureInfo.InvariantCulture)
        };
    }

    private static void SetIfPresent(Dictionary<string, string> attributes, string name, Action<string> setter)
    {
        if (attributes.TryGetValue(name, out string? value) && !string.IsNullOrWhiteSpace(value))
            setter(value);
    }

    private static string StringAt(string[] strings, uint index) =>
        index < (uint)strings.Length ? strings[(int)index] : string.Empty;

    private static ushort U16(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset, 2));
    private static uint U32(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4));
}
