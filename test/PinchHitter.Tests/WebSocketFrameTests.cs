// <copyright file="WebSocketFrameTests.cs" company="PinchHitter Committers">
// Copyright (c) PinchHitter Committers. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace PinchHitter;

using System.Buffers.Binary;
using System.Text;

[TestFixture]
public class WebSocketFrameTests
{
    [Test]
    public void TestCanEncodeShortTextFrame()
    {
        // "hello" = 5 bytes, fits in single-byte length (≤125)
        WebSocketFrame frame = WebSocketFrame.Encode(Encoding.UTF8.GetBytes("hello"), WebSocketOpcodeType.Text);

        Assert.Multiple(() =>
        {
            Assert.That(frame.Opcode, Is.EqualTo(WebSocketOpcodeType.Text));
            Assert.That(frame.Data[0], Is.EqualTo(0x81)); // FIN + Text opcode
            Assert.That(frame.Data[1], Is.EqualTo(0x05)); // payload length = 5
            Assert.That(Encoding.UTF8.GetString(frame.Data, 2, 5), Is.EqualTo("hello"));
        });
    }

    [Test]
    public void TestCanEncodeMediumTextFrame()
    {
        // 126 bytes triggers the two-byte length encoding path (126–65535)
        string data = new('a', 126);
        WebSocketFrame frame = WebSocketFrame.Encode(Encoding.UTF8.GetBytes(data), WebSocketOpcodeType.Text);

        ushort encodedLength = BinaryPrimitives.ReadUInt16BigEndian(new ReadOnlySpan<byte>(frame.Data, 2, sizeof(ushort)));
        Assert.Multiple(() =>
        {
            Assert.That(frame.Opcode, Is.EqualTo(WebSocketOpcodeType.Text));
            Assert.That(frame.Data[0], Is.EqualTo(0x81)); // FIN + Text opcode
            Assert.That(frame.Data[1], Is.EqualTo(0x7E)); // length indicator = 126
            Assert.That(encodedLength, Is.EqualTo(126));
            Assert.That(Encoding.UTF8.GetString(frame.Data, 4, 126), Is.EqualTo(data));
        });
    }

    [Test]
    public void TestCanEncodeMediumLargeTextFrame()
    {
        // 40,000 bytes is in the two-byte length range (126–65535) and above the
        // signed-short boundary (32767), exercising the full unsigned 16-bit length path.
        string data = new('a', 40000);
        WebSocketFrame frame = WebSocketFrame.Encode(Encoding.UTF8.GetBytes(data), WebSocketOpcodeType.Text);

        ushort encodedLength = BinaryPrimitives.ReadUInt16BigEndian(new ReadOnlySpan<byte>(frame.Data, 2, sizeof(ushort)));
        Assert.Multiple(() =>
        {
            Assert.That(frame.Opcode, Is.EqualTo(WebSocketOpcodeType.Text));
            Assert.That(frame.Data[0], Is.EqualTo(0x81)); // FIN + Text opcode
            Assert.That(frame.Data[1], Is.EqualTo(0x7E)); // length indicator = 126
            Assert.That(encodedLength, Is.EqualTo(40000));
            Assert.That(Encoding.UTF8.GetString(frame.Data, 4, 40000), Is.EqualTo(data));
        });
    }

    [Test]
    public void TestCanEncodeLongTextFrame()
    {
        // 65536 bytes triggers the eight-byte length encoding path (>65535)
        string data = new('a', 65536);
        WebSocketFrame frame = WebSocketFrame.Encode(Encoding.UTF8.GetBytes(data), WebSocketOpcodeType.Text);

        long encodedLength = BinaryPrimitives.ReadInt64BigEndian(new ReadOnlySpan<byte>(frame.Data, 2, sizeof(long)));
        Assert.Multiple(() =>
        {
            Assert.That(frame.Opcode, Is.EqualTo(WebSocketOpcodeType.Text));
            Assert.That(frame.Data[0], Is.EqualTo(0x81)); // FIN + Text opcode
            Assert.That(frame.Data[1], Is.EqualTo(0x7F)); // length indicator = 127
            Assert.That(encodedLength, Is.EqualTo(65536));
            Assert.That(Encoding.UTF8.GetString(frame.Data, 10, 65536), Is.EqualTo(data));
        });
    }

    [Test]
    public void TestCanEncodeCloseFrame()
    {
        WebSocketFrame frame = WebSocketFrame.Encode(new byte[] { }, WebSocketOpcodeType.Close);

        Assert.Multiple(() =>
        {
            Assert.That(frame.Opcode, Is.EqualTo(WebSocketOpcodeType.Close));
            Assert.That(frame.Data, Is.EqualTo(new byte[] { 0x88, 0x00 }));
        });
    }

    [Test]
    public void TestCanEncodeCloseFrameFromByteArray()
    {
        byte[] payload = [0x01, 0x02, 0x03];
        WebSocketFrame frame = WebSocketFrame.Encode(payload, WebSocketOpcodeType.Close);

        Assert.Multiple(() =>
        {
            Assert.That(frame.Opcode, Is.EqualTo(WebSocketOpcodeType.Close));
            Assert.That(frame.Data, Is.EqualTo(new byte[] { 0x88, 0x00 }));
        });
    }

    [Test]
    public void TestCanEncodeShortBinaryFrame()
    {
        byte[] payload = [0x01, 0x02, 0x03, 0x04, 0x05];
        WebSocketFrame frame = WebSocketFrame.Encode(payload);

        Assert.Multiple(() =>
        {
            Assert.That(frame.Opcode, Is.EqualTo(WebSocketOpcodeType.Binary));
            Assert.That(frame.Data[0], Is.EqualTo(0x82)); // FIN + Binary opcode
            Assert.That(frame.Data[1], Is.EqualTo(0x05)); // payload length = 5
            Assert.That(frame.Data[2..7], Is.EqualTo(payload));
        });
    }

    [Test]
    public void TestCanEncodeMediumBinaryFrame()
    {
        byte[] payload = new byte[126];
        for (int i = 0; i < payload.Length; i++)
        {
            payload[i] = (byte)(i % 256);
        }

        WebSocketFrame frame = WebSocketFrame.Encode(payload);

        ushort encodedLength = BinaryPrimitives.ReadUInt16BigEndian(new ReadOnlySpan<byte>(frame.Data, 2, sizeof(ushort)));
        byte[] decodedPayload = frame.Data[4..];
        Assert.Multiple(() =>
        {
            Assert.That(frame.Opcode, Is.EqualTo(WebSocketOpcodeType.Binary));
            Assert.That(frame.Data[0], Is.EqualTo(0x82)); // FIN + Binary opcode
            Assert.That(frame.Data[1], Is.EqualTo(0x7E)); // length indicator = 126
            Assert.That(encodedLength, Is.EqualTo(126));
            Assert.That(decodedPayload, Is.EqualTo(payload));
        });
    }

    [Test]
    public void TestCanEncodeLongBinaryFrame()
    {
        byte[] payload = new byte[65536];
        for (int i = 0; i < payload.Length; i++)
        {
            payload[i] = (byte)(i % 256);
        }

        WebSocketFrame frame = WebSocketFrame.Encode(payload);

        long encodedLength = BinaryPrimitives.ReadInt64BigEndian(new ReadOnlySpan<byte>(frame.Data, 2, sizeof(long)));
        byte[] decodedPayload = frame.Data[10..];
        Assert.Multiple(() =>
        {
            Assert.That(frame.Opcode, Is.EqualTo(WebSocketOpcodeType.Binary));
            Assert.That(frame.Data[0], Is.EqualTo(0x82)); // FIN + Binary opcode
            Assert.That(frame.Data[1], Is.EqualTo(0x7F)); // length indicator = 127
            Assert.That(encodedLength, Is.EqualTo(65536));
            Assert.That(decodedPayload, Is.EqualTo(payload));
        });
    }

    [Test]
    public void TestEncodeBinaryWithNullDataThrows()
    {
        Assert.That(() => WebSocketFrame.Encode((byte[])null!), Throws.ArgumentNullException.With.Property("ParamName").EqualTo("data"));
    }

    [Test]
    public void TestEncodeBinaryWithInvalidOpcodeThrows()
    {
        byte[] payload = [0x01, 0x02, 0x03];
        Assert.That(() => WebSocketFrame.Encode(payload, WebSocketOpcodeType.Ping), Throws.ArgumentException.With.Property("ParamName").EqualTo("opcode"));
    }

    [Test]
    public void TestCanDecodeShortMaskedTextFrame()
    {
        // "hello" masked with [0x12, 0x34, 0x56, 0x78] — single-byte length path
        byte[] buffer = BuildMaskedFrame(0x81, Encoding.UTF8.GetBytes("hello"), [0x12, 0x34, 0x56, 0x78]);
        WebSocketFrame frame = WebSocketFrame.Decode(buffer);

        Assert.Multiple(() =>
        {
            Assert.That(frame.Opcode, Is.EqualTo(WebSocketOpcodeType.Text));
            Assert.That(Encoding.UTF8.GetString(frame.Data), Is.EqualTo("hello"));
        });
    }

    [Test]
    public void TestCanDecodeMediumMaskedTextFrame()
    {
        // 126 'a' bytes masked — two-byte length path (126–65535)
        byte[] buffer = BuildMaskedFrame(0x81, Encoding.UTF8.GetBytes(new string('a', 126)), [0x01, 0x02, 0x03, 0x04]);
        WebSocketFrame frame = WebSocketFrame.Decode(buffer);

        Assert.Multiple(() =>
        {
            Assert.That(frame.Opcode, Is.EqualTo(WebSocketOpcodeType.Text));
            Assert.That(Encoding.UTF8.GetString(frame.Data), Is.EqualTo(new string('a', 126)));
        });
    }

    [Test]
    public void TestCanDecodeMediumLargeMaskedTextFrame()
    {
        // 40,000 'a' bytes masked — two-byte length path, above the signed-short boundary (32767)
        byte[] buffer = BuildMaskedFrame(0x81, Encoding.UTF8.GetBytes(new string('a', 40000)), [0x01, 0x02, 0x03, 0x04]);
        WebSocketFrame frame = WebSocketFrame.Decode(buffer);

        Assert.Multiple(() =>
        {
            Assert.That(frame.Opcode, Is.EqualTo(WebSocketOpcodeType.Text));
            Assert.That(Encoding.UTF8.GetString(frame.Data), Is.EqualTo(new string('a', 40000)));
        });
    }

    [Test]
    public void TestCanDecodeLongMaskedTextFrame()
    {
        // 65536 'a' bytes masked — eight-byte length path (>65535)
        byte[] buffer = BuildMaskedFrame(0x81, Encoding.UTF8.GetBytes(new string('a', 65536)), [0x01, 0x02, 0x03, 0x04]);
        WebSocketFrame frame = WebSocketFrame.Decode(buffer);

        Assert.Multiple(() =>
        {
            Assert.That(frame.Opcode, Is.EqualTo(WebSocketOpcodeType.Text));
            Assert.That(Encoding.UTF8.GetString(frame.Data), Is.EqualTo(new string('a', 65536)));
        });
    }

    [Test]
    public void TestCanDecodeClosedConnectionFrame()
    {
        // Empty-payload close frame from client
        byte[] buffer = BuildMaskedFrame(0x88, [], [0x00, 0x00, 0x00, 0x00]);
        WebSocketFrame frame = WebSocketFrame.Decode(buffer);

        Assert.That(frame.Opcode, Is.EqualTo(WebSocketOpcodeType.Close));
    }

    [Test]
    public void TestInvalidFrameLengthThrows()
    {
        Assert.That(() => WebSocketFrame.Decode(new byte[] { }), Throws.InstanceOf<ArgumentException>());
        Assert.That(() => WebSocketFrame.Decode(new byte[] { 0x81 }), Throws.InstanceOf<ArgumentException>());
    }

    [Test]
    public void TestDecodeTruncatedSingleByteLengthFrameThrows()
    {
        // "hello" = 5 bytes; full frame needs 2 (header) + 4 (mask) + 5 (payload) = 11 bytes.
        // Truncate to 10 bytes (missing 1 payload byte).
        byte[] fullFrame = BuildMaskedFrame(0x81, Encoding.UTF8.GetBytes("hello"), [0x12, 0x34, 0x56, 0x78]);
        byte[] truncated = fullFrame[..10];

        Assert.That(() => WebSocketFrame.Decode(truncated), Throws.InstanceOf<ArgumentException>().With.Message.Contain("too short").And.Property("ParamName").EqualTo("buffer"));
    }

    [Test]
    public void TestDecodeTruncatedTwoByteLengthFrameThrows()
    {
        // 126-byte payload; full frame needs 4 (header) + 4 (mask) + 126 (payload) = 134 bytes.
        // Truncate to 133 bytes (missing 1 payload byte).
        byte[] fullFrame = BuildMaskedFrame(0x81, Encoding.UTF8.GetBytes(new string('a', 126)), [0x01, 0x02, 0x03, 0x04]);
        byte[] truncated = fullFrame[..133];

        Assert.That(() => WebSocketFrame.Decode(truncated), Throws.InstanceOf<ArgumentException>().With.Message.Contain("too short").And.Property("ParamName").EqualTo("buffer"));
    }

    [Test]
    public void TestDecodeTruncatedEightByteLengthFrameThrows()
    {
        // 65536-byte payload; full frame needs 10 (header) + 4 (mask) + 65536 (payload) = 65550 bytes.
        // Truncate to 65549 bytes (missing 1 payload byte).
        byte[] fullFrame = BuildMaskedFrame(0x81, Encoding.UTF8.GetBytes(new string('a', 65536)), [0x01, 0x02, 0x03, 0x04]);
        byte[] truncated = fullFrame[..65549];

        Assert.That(() => WebSocketFrame.Decode(truncated), Throws.InstanceOf<ArgumentException>().With.Message.Contain("too short").And.Property("ParamName").EqualTo("buffer"));
    }

    [Test]
    public void TestDecodeBufferTooShortForMaskThrows()
    {
        // Single-byte length path: need at least 6 bytes (2 header + 4 mask). Provide 5.
        byte[] buffer = new byte[] { 0x81, 0x80, 0x12, 0x34, 0x56 }; // opcode, length=0, partial mask

        Assert.That(() => WebSocketFrame.Decode(buffer), Throws.InstanceOf<ArgumentException>().With.Message.Contain("too short").And.Property("ParamName").EqualTo("buffer"));
    }

    [Test]
    public void TestDecodeBufferWithIncompleteHeaderThrows()
    {
        // Single-byte length path: need at least 6 bytes (2 header + 4 mask). Provide 5.
        byte[] buffer = new byte[] { 0x81, 0xFE, 0x00 }; // opcode, length=126, invalid/incomplete header

        Assert.That(() => WebSocketFrame.Decode(buffer), Throws.InstanceOf<ArgumentException>().With.Message.Contain("too short").And.Property("ParamName").EqualTo("buffer"));
    }

    /// <summary>
    /// Builds a masked WebSocket frame in client-to-server wire format.
    /// The length encoding is chosen automatically based on payload size.
    /// </summary>
    private static byte[] BuildMaskedFrame(byte firstByte, byte[] payload, byte[] mask)
    {
        List<byte> frame = [firstByte];

        byte[] masked = new byte[payload.Length];
        for (int i = 0; i < payload.Length; i++)
        {
            masked[i] = (byte)(payload[i] ^ mask[i % 4]);
        }

        if (payload.Length > 65535)
        {
            frame.Add(0xFF); // MASK=1, length indicator = 127
            byte[] lenBytes = new byte[sizeof(long)];
            BinaryPrimitives.WriteInt64BigEndian(lenBytes, (long)payload.Length);
            frame.AddRange(lenBytes);
        }
        else if (payload.Length >= 126)
        {
            frame.Add(0xFE); // MASK=1, length indicator = 126
            byte[] lenBytes = new byte[sizeof(ushort)];
            BinaryPrimitives.WriteUInt16BigEndian(lenBytes, (ushort)payload.Length);
            frame.AddRange(lenBytes);
        }
        else
        {
            frame.Add((byte)(0x80 | payload.Length)); // MASK=1, length in low 7 bits
        }

        frame.AddRange(mask);
        frame.AddRange(masked);
        return frame.ToArray();
    }
}
