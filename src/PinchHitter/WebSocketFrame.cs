// <copyright file="WebSocketFrame.cs" company="PinchHitter Committers">
// Copyright (c) PinchHitter Committers. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace PinchHitter;

using System.Buffers.Binary;

/// <summary>
/// Represents a data frame for the WebSocket protocol.
/// </summary>
public class WebSocketFrame
{
    // The parity bit used for identifying the WebSocket opcode in the
    // WebSocket protocol.
    private static readonly byte ParityBit = 0x80;

    // The threshold below which the length of a WebSocket message can be
    // expressed in a single byte.
    private static readonly byte MessageLengthIndicatorSingleByte = 125;

    // Indicates that the length of a WebSocket message is between 126 and 65535
    // bytes, inclusive, and can therefore be expressed in a 16-bit integer.
    private static readonly byte MessageLengthIndicatorTwoBytes = 126;

    // Indicates that the length of a WebSocket message is greater than 65535
    // bytes, and therefore must be expressed as a 64-bit integer.
    private static readonly byte MessageLengthIndicatorEightBytes = 127;

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
        const byte opcodeMask = 0x0F;
        const byte messageLengthMask = 0x7F;
        if (buffer.Length < 2)
        {
            throw new ArgumentException("Buffer is too short to contain a WebSocket frame.", nameof(buffer));
        }

        WebSocketOpcodeType opcode = (WebSocketOpcodeType)(buffer[0] & opcodeMask);

        byte messageLengthIndicator = Convert.ToByte(buffer[1] & messageLengthMask);

        int keyOffset;
        long messageLength;
        if (messageLengthIndicator == MessageLengthIndicatorTwoBytes)
        {
            // Message length is between 126 and 65535 bytes, inclusive
            ReadOnlySpan<byte> messageLengthSpan = new(buffer, 2, sizeof(ushort));
            messageLength = BinaryPrimitives.ReadUInt16BigEndian(messageLengthSpan);
            keyOffset = 4;
        }
        else if (messageLengthIndicator == MessageLengthIndicatorEightBytes)
        {
            // Message length is over 65535 bytes
            ReadOnlySpan<byte> messageLengthSpan = new(buffer, 2, sizeof(long));
            messageLength = BinaryPrimitives.ReadInt64BigEndian(messageLengthSpan);
            keyOffset = 10;
        }
        else
        {
            // Message length is less than 126 bytes, and can be expressed in a
            // single byte, so the message length indicator byte in the frame
            // contains the actual length of the message.
            messageLength = messageLengthIndicator;
            keyOffset = 2;
        }

        // Validate buffer has enough bytes for mask key + payload
        long payloadStart = keyOffset + 4;
        if (buffer.Length < payloadStart || buffer.LongLength - payloadStart < messageLength)
        {
            throw new ArgumentException("Buffer is too short to contain the complete WebSocket frame.", nameof(buffer));
        }

        // Incoming messages across a WebSocket are masked. The masking algorithm
        // has a four-byte mask, which each byte of the message is XOR'd with the
        // corresponding byte of the mask.
        byte[] decoded = new byte[messageLength];
        ArraySegment<byte> key = new(buffer, keyOffset, 4);
        long offset = Convert.ToInt64(keyOffset + key.Count);
        for (long index = 0; index < messageLength; index++)
        {
            decoded[index] = Convert.ToByte(buffer[offset + index] ^ key.Array![Convert.ToInt32(key.Offset + (index % 4))]);
        }

        return new WebSocketFrame(opcode, decoded);
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

        byte opcodeByte = Convert.ToByte(Convert.ToByte(opcode) | ParityBit);
        long messageLength = data.LongLength;

        byte[] frameHeader = new byte[10];
        frameHeader[0] = opcodeByte;

        long dataOffset;
        if (messageLength <= MessageLengthIndicatorSingleByte)
        {
            // Message length is less than 126 bytes
            frameHeader[1] = Convert.ToByte(messageLength);
            dataOffset = 2;
        }
        else if (messageLength <= 65535)
        {
            // Message length is between 126 and 65535 bytes, inclusive
            frameHeader[1] = MessageLengthIndicatorTwoBytes;
            Span<byte> messageLengthSpan = new(frameHeader, 2, sizeof(ushort));
            BinaryPrimitives.WriteUInt16BigEndian(messageLengthSpan, Convert.ToUInt16(messageLength));
            dataOffset = 4;
        }
        else
        {
            // Message length is over 65535 bytes
            frameHeader[1] = MessageLengthIndicatorEightBytes;
            Span<byte> messageLengthSpan = new(frameHeader, 2, sizeof(long));
            BinaryPrimitives.WriteInt64BigEndian(messageLengthSpan, messageLength);
            dataOffset = 10;
        }

        byte[] buffer = new byte[dataOffset + messageLength];
        Array.Copy(frameHeader, buffer, dataOffset);
        data.CopyTo(buffer, dataOffset);
        return new WebSocketFrame(opcode, buffer);
    }
}
