// <copyright file="WebSocketFrameHeader.cs" company="PinchHitter Committers">
// Copyright (c) PinchHitter Committers. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace PinchHitter;

using System.Buffers.Binary;

/// <summary>
/// Represents the header of a WebSocket frame, which contains metadata about the frame
/// such as the opcode, payload length, and offsets for the mask key and payload data.
/// </summary>
public record WebSocketFrameHeader
{
    private const int MinimumHeaderLength = 2;
    private const byte OpcodeMask = 0x0F;
    private const byte ParityBit = 0x80;
    private const byte MessageLengthMask = 0x7F;
    private const int MaskKeyLength = 4;

    // The threshold below which the length of a WebSocket message can be
    // expressed in a single byte.
    private const byte MessageLengthIndicatorSingleByte = 125;

    // Indicates that the length of a WebSocket message is between 126 and 65535
    // bytes, inclusive, and can therefore be expressed in a 16-bit integer.
    private const byte MessageLengthIndicatorTwoBytes = 126;

    // Indicates that the length of a WebSocket message is greater than 65535
    // bytes, and therefore must be expressed as a 64-bit integer.
    private const byte MessageLengthIndicatorEightBytes = 127;

    private WebSocketFrameHeader()
    {
    }

    /// <summary>
    /// Gets the opcode for this frame, which indicates the type of data contained in the frame.
    /// </summary>
    public WebSocketOpcodeType Opcode { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the header is complete.
    /// </summary>
    public bool IsHeaderComplete { get; private set; }

    /// <summary>
    /// Gets the starting offset of the mask key within the frame.
    /// </summary>
    public int KeyStartOffset { get; private set; }

    /// <summary>
    /// Gets the starting offset of the payload data within the frame.
    /// </summary>
    public int PayloadStartOffset { get; private set; }

    /// <summary>
    /// Gets the length of the payload data for the message.
    /// </summary>
    public long PayloadLength { get; private set; }

    /// <summary>
    /// Encodes the WebSocket frame header to a byte array that can be sent over the network.
    /// </summary>
    /// <returns>The byte array containing the encoded header.</returns>
    public byte[] ToByteArray()
    {
        byte opcodeByte = Convert.ToByte(Convert.ToByte(this.Opcode) | ParityBit);
        byte[] headerBytes;
        if (this.PayloadLength <= MessageLengthIndicatorSingleByte)
        {
            headerBytes = new byte[] { opcodeByte, Convert.ToByte(this.PayloadLength) };
        }
        else if (this.PayloadLength <= ushort.MaxValue)
        {
            headerBytes = new byte[MinimumHeaderLength + sizeof(ushort)];
            headerBytes[0] = opcodeByte;
            headerBytes[1] = MessageLengthIndicatorTwoBytes;
            BinaryPrimitives.WriteUInt16BigEndian(headerBytes.AsSpan(MinimumHeaderLength), Convert.ToUInt16(this.PayloadLength));
        }
        else
        {
            headerBytes = new byte[MinimumHeaderLength + sizeof(long)];
            headerBytes[0] = opcodeByte;
            headerBytes[1] = MessageLengthIndicatorEightBytes;
            BinaryPrimitives.WriteInt64BigEndian(headerBytes.AsSpan(MinimumHeaderLength), this.PayloadLength);
        }

        return headerBytes;
    }

    /// <summary>
    /// Creates a WebSocket frame header for the given opcode and payload length,
    /// which can be used to construct a complete WebSocket frame for sending data
    /// over the network.
    /// </summary>
    /// <param name="opcode">The opcode for the frame.</param>
    /// <param name="payloadLength">The length of the payload data.</param>
    /// <returns>The created WebSocket frame header.</returns>
    public static WebSocketFrameHeader Create(WebSocketOpcodeType opcode, long payloadLength)
    {
        int payloadStartOffset = MinimumHeaderLength;
        if (payloadLength > MessageLengthIndicatorSingleByte && payloadLength <= ushort.MaxValue)
        {
            payloadStartOffset += sizeof(ushort);
        }
        else if (payloadLength > ushort.MaxValue)
        {
            payloadStartOffset += sizeof(long);
        }

        return new WebSocketFrameHeader
        {
            Opcode = opcode,
            IsHeaderComplete = true,
            KeyStartOffset = 0,
            PayloadStartOffset = payloadStartOffset,
            PayloadLength = payloadLength,
        };
    }

    /// <summary>
    /// Decodes a byte array to a WebSocket frame header, extracting the opcode, payload length,
    /// and offsets for the mask key and payload data. Validates that the buffer contains enough
    /// bytes to represent a complete header and the indicated payload length.
    /// </summary>
    /// <param name="buffer">The byte array to decode.</param>
    /// <returns>The WebSocket frame header represented by the byte array.</returns>
    /// <exception cref="ArgumentException">Thrown when the buffer is too short to contain a complete WebSocket header.</exception>
    public static WebSocketFrameHeader Decode(byte[] buffer)
    {
        if (buffer.Length < MinimumHeaderLength)
        {
            throw new ArgumentException("Buffer is too short to contain a WebSocket header.", nameof(buffer));
        }

        bool isHeaderComplete = false;
        WebSocketOpcodeType opcode = (WebSocketOpcodeType)(buffer[0] & OpcodeMask);
        byte messageLengthIndicator = Convert.ToByte(buffer[1] & MessageLengthMask);
        int keyOffset = messageLengthIndicator switch
        {
            MessageLengthIndicatorTwoBytes => MinimumHeaderLength + sizeof(ushort),
            MessageLengthIndicatorEightBytes => MinimumHeaderLength + sizeof(long),
            _ => MinimumHeaderLength,
        };
        long payloadLength = 0;
        int payloadStartOffset = 0;
        if (buffer.Length >= keyOffset)
        {
            payloadLength = messageLengthIndicator switch
            {
                MessageLengthIndicatorTwoBytes => BinaryPrimitives.ReadUInt16BigEndian(new ReadOnlySpan<byte>(buffer, MinimumHeaderLength, sizeof(ushort))),
                MessageLengthIndicatorEightBytes => BinaryPrimitives.ReadInt64BigEndian(new ReadOnlySpan<byte>(buffer, MinimumHeaderLength, sizeof(long))),
                _ => messageLengthIndicator,
            };
            payloadStartOffset = keyOffset + MaskKeyLength;
            isHeaderComplete = buffer.Length >= payloadStartOffset;
        }

        return new WebSocketFrameHeader
        {
            Opcode = opcode,
            IsHeaderComplete = isHeaderComplete,
            KeyStartOffset = keyOffset,
            PayloadStartOffset = payloadStartOffset,
            PayloadLength = payloadLength,
        };
    }
}