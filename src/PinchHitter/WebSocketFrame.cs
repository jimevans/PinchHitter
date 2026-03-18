// <copyright file="WebSocketFrame.cs" company="PinchHitter Committers">
// Copyright (c) PinchHitter Committers. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace PinchHitter;

/// <summary>
/// Represents a data frame for the WebSocket protocol.
/// </summary>
public class WebSocketFrame
{
    private readonly WebSocketOpcodeType opcode;
    private readonly byte[] data;

    private WebSocketFrame(WebSocketOpcodeType opcode, byte[] frameData)
    {
        this.opcode = opcode;
        this.data = frameData;
    }

    /// <summary>
    /// Gets the opcode for this frame.
    /// </summary>
    public WebSocketOpcodeType Opcode => this.opcode;

    /// <summary>
    /// Gets the byte array containing the data for this frame.
    /// </summary>
    public byte[] Data => this.data;

    /// <summary>
    /// Decodes a byte array to a WebSocket frame.
    /// </summary>
    /// <param name="buffer">The byte array to decode.</param>
    /// <returns>The WebSocket frame represented by the byte array.</returns>
    public static WebSocketFrame Decode(byte[] buffer)
    {
        WebSocketFrameHeader header = WebSocketFrameHeader.Decode(buffer);

        // Validate buffer has enough bytes for mask key + payload
        if (!header.IsHeaderComplete || buffer.Length < header.PayloadStartOffset || buffer.LongLength - header.PayloadStartOffset < header.PayloadLength)
        {
            throw new ArgumentException("Buffer is too short to contain the complete WebSocket frame.", nameof(buffer));
        }

        // Incoming messages across a WebSocket are masked. The masking algorithm
        // has a four-byte mask, which each byte of the message is XOR'd with the
        // corresponding byte of the mask.
        byte[] decoded = new byte[header.PayloadLength];
        ArraySegment<byte> key = new(buffer, header.KeyStartOffset, 4);
        long offset = Convert.ToInt64(header.KeyStartOffset + key.Count);
        for (long index = 0; index < header.PayloadLength; index++)
        {
            decoded[index] = Convert.ToByte(buffer[offset + index] ^ key.Array![Convert.ToInt32(key.Offset + (index % 4))]);
        }

        return new WebSocketFrame(header.Opcode, decoded);
    }

    /// <summary>
    /// Encodes binary data to a WebSocket frame.
    /// </summary>
    /// <param name="data">The binary data to encode.</param>
    /// <param name="opcode">The opcode of the frame to encode. Must be <see cref="WebSocketOpcodeType.Text"/>, <see cref="WebSocketOpcodeType.Binary"/>, or <see cref="WebSocketOpcodeType.Close"/>.</param>
    /// <returns>The WebSocket frame containing the encoded data.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="data"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="opcode"/> is not <see cref="WebSocketOpcodeType.Text"/>, <see cref="WebSocketOpcodeType.Binary"/>, or <see cref="WebSocketOpcodeType.Close"/>.</exception>
    /// <remarks>
    /// <para>
    /// Convenience overloads for encoding text data as text WebSocket frames are intentionally not provided.
    /// The WebSocket protocol specifies that text frames must be encoded in UTF-8, and this library does not
    /// want to make assumptions about the encoding of text data. Consumers of this library can easily encode
    /// text data to UTF-8 byte arrays before calling this method, and this approach avoids unnecessary
    /// encoding and decoding of text data in cases where consumers have their own preferred text encoding
    /// or are already working with text data as byte arrays.
    /// </para>
    /// <para>
    /// The WebSocket protocol specifies that close frames may contain a two-byte status code
    /// and a UTF-8 encoded reason, but in practice, this implementation follows many other
    /// existing implementations, and elides this additional information. Given the purpose of
    /// this library, this is a reasonable choice. Accordingly, the <paramref name="data"/>
    /// parameter is ignored when encoding close frames.
    /// </para>
    /// </remarks>
    public static WebSocketFrame Encode(byte[] data, WebSocketOpcodeType opcode = WebSocketOpcodeType.Binary)
    {
        if (data is null)
        {
            throw new ArgumentNullException(nameof(data));
        }

        if (opcode != WebSocketOpcodeType.Text && opcode != WebSocketOpcodeType.Binary && opcode != WebSocketOpcodeType.Close)
        {
            throw new ArgumentException("Text, Binary, or Close opcode is required for byte array encoding.", nameof(opcode));
        }

        if (opcode == WebSocketOpcodeType.Close)
        {
            // NOTE: Hard code the close frame data. Additional optional data
            // for close frames are ignored, as documented above.
            return new WebSocketFrame(opcode, new byte[] { 0x88, 0x00 });
        }

        WebSocketFrameHeader header = WebSocketFrameHeader.Create(opcode, data.LongLength);
        byte[] frameData = new byte[header.PayloadStartOffset + data.Length];
        Array.Copy(header.ToByteArray(), frameData, header.PayloadStartOffset);
        data.CopyTo(frameData, header.PayloadStartOffset);
        return new WebSocketFrame(opcode, frameData);
    }
}
