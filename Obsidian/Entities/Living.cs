﻿using Obsidian.Net;
using Obsidian.Net.Packets.Play.Client;
using Obsidian.Util.DataTypes;
using System;
using System.Threading.Tasks;

namespace Obsidian.Entities
{
    public class Living : Entity
    {
        
        public LivingBitMask LivingBitMask { get; set; }

        public float Health { get; set; }

        public uint ActiveEffectColor { get; private set; }

        public bool AmbientPotionEffect { get; set; }

        public int AbsorbedArrows { get; set; }

        public int AbsorbtionAmount { get; set; }

        public Position BedBlockPosition { get; set; }

        

        public override async Task WriteAsync(MinecraftStream stream)
        {
            await base.WriteAsync(stream);

            await stream.WriteEntityMetdata(7, EntityMetadataType.Byte, (byte)this.LivingBitMask);

            await stream.WriteEntityMetdata(8, EntityMetadataType.Float, this.Health);

            await stream.WriteEntityMetdata(9, EntityMetadataType.VarInt, (int)this.ActiveEffectColor);

            await stream.WriteEntityMetdata(10, EntityMetadataType.Boolean, this.AmbientPotionEffect);

            await stream.WriteEntityMetdata(11, EntityMetadataType.VarInt, this.AbsorbedArrows);

            await stream.WriteEntityMetdata(12, EntityMetadataType.VarInt, this.AbsorbtionAmount);

            await stream.WriteEntityMetdata(13, EntityMetadataType.OptPosition, this.BedBlockPosition, this.BedBlockPosition != null);
        }

    }

    [Flags]
    public enum LivingBitMask : byte
    {
        None = 0x00,

        HandActive = 0x01,
        ActiveHand = 0x02,
        InRiptideSpinAttack = 0x04
    }
}
