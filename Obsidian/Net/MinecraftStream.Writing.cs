﻿using Obsidian.Chat;
using Obsidian.Serializer.Attributes;
using Obsidian.Serializer.Enums;
using Obsidian.Util.DataTypes;
using System;
using System.Text;
using System.Threading.Tasks;

namespace Obsidian.Net
{
    public partial class MinecraftStream
    {
        [WriteMethod(DataType.Byte)]
        public void WriteByte(sbyte value)
        {
            Write(new[] { (byte)value });
        }
        
        public async Task WriteByteAsync(sbyte value)
        {
#if PACKETLOG
            await Program.PacketLogger.LogDebugAsync($"Writing Byte (0x{value.ToString("X")})");
#endif

            await this.WriteUnsignedByteAsync((byte)value);
        }

        [WriteMethod(DataType.UnsignedByte)]
        public void WriteUnsignedByte(byte value)
        {
            Write(new[] { value });
        }

        public async Task WriteUnsignedByteAsync(byte value)
        {
#if PACKETLOG
            await Program.PacketLogger.LogDebugAsync($"Writing unsigned Byte (0x{value.ToString("X")})");
#endif

            await this.WriteAsync(new[] { value });
        }

        public async Task WriteBooleanAsync(bool value)
        {
#if PACKETLOG
            await Program.PacketLogger.LogDebugAsync($"Writing Boolean ({value})");
#endif

            await this.WriteByteAsync((sbyte)(value ? 0x01 : 0x00));
        }

        public async Task WriteUnsignedShortAsync(ushort value)
        {
#if PACKETLOG
            await Program.PacketLogger.LogDebugAsync($"Writing unsigned Short ({value})");
#endif

            var write = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(write);
            }
            await this.WriteAsync(write);
        }

        public async Task WriteShortAsync(short value)
        {
#if PACKETLOG
            await Program.PacketLogger.LogDebugAsync($"Writing Short ({value})");
#endif

            var write = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(write);
            }
            await this.WriteAsync(write);
        }

        [WriteMethod(DataType.Int)]
        public void WriteInt(int value)
        {
            var write = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(write);
            }
            Write(write);
        }

        public async Task WriteIntAsync(int value)
        {
#if PACKETLOG
            await Program.PacketLogger.LogDebugAsync($"Writing Int ({value})");
#endif

            var write = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(write);
            }
            await this.WriteAsync(write);
        }

        [WriteMethod(DataType.Long)]
        public void WriteLong(long value)
        {
            var write = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(write);
            }
            Write(write);
        }

        public async Task WriteLongAsync(long value)
        {
#if PACKETLOG
            await Program.PacketLogger.LogDebugAsync($"Writing Long ({value})");
#endif

            var write = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(write);
            }
            await this.WriteAsync(write);
        }

        [WriteMethod(DataType.Float)]
        public void WriteFloat(float value)
        {
            var write = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(write);
            }
            Write(write);
        }

        public async Task WriteFloatAsync(float value)
        {
#if PACKETLOG
            await Program.PacketLogger.LogDebugAsync($"Writing Float ({value})");
#endif

            var write = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(write);
            }
            await this.WriteAsync(write);
        }

        [WriteMethod(DataType.Double)]
        public void WriteDouble(double value)
        {
            var write = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(write);
            }
            Write(write);
        }

        public async Task WriteDoubleAsync(double value)
        {
#if PACKETLOG
            await Program.PacketLogger.LogDebugAsync($"Writing Double ({value})");
#endif

            var write = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(write);
            }
            await this.WriteAsync(write);
        }

        [WriteMethod(DataType.String)]
        public void WriteString(string value)
        {
            var bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
            WriteVarInt(bytes.Length);
            Write(bytes);
        }

        public async Task WriteStringAsync(string value, int maxLength = 32767)
        {
            //await Program.PacketLogger.LogDebugAsync($"Writing String ({value})");

            if (value == null)
                throw new ArgumentNullException(nameof(value));

            if (value.Length > maxLength)
                throw new ArgumentException($"string ({value.Length}) exceeded maximum length ({maxLength})", nameof(value));

            var bytes = Encoding.UTF8.GetBytes(value);
            await this.WriteVarIntAsync(bytes.Length);
            await this.WriteAsync(bytes);
        }

        [WriteMethod(DataType.VarInt)]
        public void WriteVarInt(int value)
        {
            var v = (uint)value;

            do
            {
                var temp = (byte)(v & 127);

                v >>= 7;

                if (v != 0)
                    temp |= 128;

                WriteUnsignedByte(temp);
            } while (v != 0);
        }

        public async Task WriteVarIntAsync(int value)
        {
            //await Program.PacketLogger.LogDebugAsync($"Writing VarInt ({value})");

            var v = (uint)value;

            do
            {
                var temp = (byte)(v & 127);

                v >>= 7;

                if (v != 0)
                    temp |= 128;

                await this.WriteUnsignedByteAsync(temp);
            } while (v != 0);
        }

        /// <summary>
        /// Writes a "VarInt Enum" to the specified <paramref name="stream"/>.
        /// </summary>
        public async Task WriteVarIntAsync(Enum value) => await this.WriteVarIntAsync(Convert.ToInt32(value));

        public async Task WriteLongArrayAsync(long[] values)
        {
            foreach (var value in values)
                await this.WriteLongAsync(value);
        }

        public async Task WriteLongArrayAsync(ulong[] values)
        {
            foreach (var value in values)
                await this.WriteLongAsync((long)value);
        }

        public async Task WriteVarLongAsync(long value)
        {
#if PACKETLOG
            await Program.PacketLogger.LogDebugAsync($"Writing VarLong ({value})");
#endif

            var v = (ulong)value;

            do
            {
                var temp = (byte)(v & 127);

                v >>= 7;

                if (v != 0)
                    temp |= 128;


                await this.WriteUnsignedByteAsync(temp);
            } while (v != 0);
        }

        [WriteMethod(DataType.Transform)]
        public void WriteTransform(Transform transform)
        {
            WriteDouble(transform.X);
            WriteDouble(transform.Y);
            WriteDouble(transform.Z);
            WriteFloat(transform.Yaw.Degrees);
            WriteFloat(transform.Pitch.Degrees);
        }

        [WriteMethod(DataType.Chat)]
        public void WriteChat(ChatMessage value)
        {
            WriteString(value.ToString());
        }
    }
}
