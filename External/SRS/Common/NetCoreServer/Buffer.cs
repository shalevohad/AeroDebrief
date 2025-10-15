using System;
using System.Diagnostics;
using System.Text;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.NetCoreServer;

/// <summary>
///     Dynamic byte buffer
/// </summary>
public class Buffer
{
    /// <summary>
    ///     Initialize a new expandable buffer with zero capacity
    /// </summary>
    public Buffer()
    {
        Data = new byte[0];
        Size = 0;
        Offset = 0;
    }

    /// <summary>
    ///     Initialize a new expandable buffer with the given capacity
    /// </summary>
    public Buffer(long capacity)
    {
        Data = new byte[capacity];
        Size = 0;
        Offset = 0;
    }

    /// <summary>
    ///     Initialize a new expandable buffer with the given data
    /// </summary>
    public Buffer(byte[] data)
    {
        Data = data;
        Size = data.Length;
        Offset = 0;
    }

    /// <summary>
    ///     Is the buffer empty?
    /// </summary>
    public bool IsEmpty => Data == null || Size == 0;

    /// <summary>
    ///     Bytes memory buffer
    /// </summary>
    public byte[] Data { get; private set; }

    /// <summary>
    ///     Bytes memory buffer capacity
    /// </summary>
    public long Capacity => Data.Length;

    /// <summary>
    ///     Bytes memory buffer size
    /// </summary>
    public long Size { get; private set; }

    /// <summary>
    ///     Bytes memory buffer offset
    /// </summary>
    public long Offset { get; private set; }

    /// <summary>
    ///     Buffer indexer operator
    /// </summary>
    public byte this[int index] => Data[index];

    #region Memory buffer methods

    // Clear the current buffer and its offset
    public void Clear()
    {
        Size = 0;
        Offset = 0;
    }

    /// <summary>
    ///     Extract the string from buffer of the given offset and size
    /// </summary>
    public string ExtractString(long offset, long size)
    {
        Debug.Assert(offset + size <= Size, "Invalid offset & size!");
        if (offset + size > Size)
            throw new ArgumentException("Invalid offset & size!", nameof(offset));

        return Encoding.UTF8.GetString(Data, (int)offset, (int)size);
    }

    /// <summary>
    ///     Remove the buffer of the given offset and size
    /// </summary>
    public void Remove(long offset, long size)
    {
        Debug.Assert(offset + size <= Size, "Invalid offset & size!");
        if (offset + size > Size)
            throw new ArgumentException("Invalid offset & size!", nameof(offset));

        Array.Copy(Data, offset + size, Data, offset, Size - size - offset);
        Size -= size;
        if (Offset >= offset + size)
        {
            Offset -= size;
        }
        else if (Offset >= offset)
        {
            Offset -= Offset - offset;
            if (Offset > Size)
                Offset = Size;
        }
    }

    /// <summary>
    ///     Reserve the buffer of the given capacity
    /// </summary>
    public void Reserve(long capacity)
    {
        Debug.Assert(capacity >= 0, "Invalid reserve capacity!");
        if (capacity < 0)
            throw new ArgumentException("Invalid reserve capacity!", nameof(capacity));

        if (capacity > Capacity)
        {
            var data = new byte[Math.Max(capacity, 2 * Capacity)];
            Array.Copy(Data, 0, data, 0, Size);
            Data = data;
        }
    }

    // Resize the current buffer
    public void Resize(long size)
    {
        Reserve(size);
        Size = size;
        if (Offset > Size)
            Offset = Size;
    }

    // Shift the current buffer offset
    public void Shift(long offset)
    {
        Offset += offset;
    }

    // Unshift the current buffer offset
    public void Unshift(long offset)
    {
        Offset -= offset;
    }

    #endregion

    #region Buffer I/O methods

    /// <summary>
    ///     Append the given buffer
    /// </summary>
    /// <param name="buffer">Buffer to append</param>
    /// <returns>Count of append bytes</returns>
    public long Append(byte[] buffer)
    {
        Reserve(Size + buffer.Length);
        Array.Copy(buffer, 0, Data, Size, buffer.Length);
        Size += buffer.Length;
        return buffer.Length;
    }

    /// <summary>
    ///     Append the given buffer fragment
    /// </summary>
    /// <param name="buffer">Buffer to append</param>
    /// <param name="offset">Buffer offset</param>
    /// <param name="size">Buffer size</param>
    /// <returns>Count of append bytes</returns>
    public long Append(byte[] buffer, long offset, long size)
    {
        Reserve(Size + size);
        Array.Copy(buffer, offset, Data, Size, size);
        Size += size;
        return size;
    }

    /// <summary>
    ///     Append the given text in UTF-8 encoding
    /// </summary>
    /// <param name="text">Text to append</param>
    /// <returns>Count of append bytes</returns>
    public long Append(string text)
    {
        Reserve(Size + Encoding.UTF8.GetMaxByteCount(text.Length));
        long result = Encoding.UTF8.GetBytes(text, 0, text.Length, Data, (int)Size);
        Size += result;
        return result;
    }

    #endregion
}