﻿using System.Threading.Tasks;

namespace Obsidian.Net.Packets
{
    public class PlayerLook : Packet
    {
        /*public PlayerLook(float yaw, float pitch, bool onground)
        {
            this.Yaw = yaw;
            this.Pitch = pitch;
            this.OnGround = onground;
        }*/

        public PlayerLook(byte[] data) : base(0x00, data) { }

        public float Yaw { get; private set; } = 0;

        public float Pitch { get; private set; } = 0;

        public bool OnGround { get; private set; } = false;

        public override async Task PopulateAsync()
        {
            using (var stream = new MinecraftStream(this.PacketData))
            {
                this.Yaw = await stream.ReadFloatAsync();
                this.Pitch = await stream.ReadFloatAsync();
                this.OnGround = await stream.ReadBooleanAsync();
            }
        }

        public override async Task<byte[]> ToArrayAsync()
        {
            using (var stream = new MinecraftStream())
            {
                await stream.WriteFloatAsync(this.Yaw);
                await stream.WriteFloatAsync(this.Pitch);
                await stream.WriteBooleanAsync(this.OnGround);
                return stream.ToArray();
            }
        }
    }
}