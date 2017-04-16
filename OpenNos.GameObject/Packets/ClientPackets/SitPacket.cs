﻿////<auto-generated <- Codemaid exclusion for now (PacketIndex Order is important for maintenance)

using OpenNos.Core;
using System.Collections.Generic;

namespace OpenNos.GameObject
{
    [PacketHeader("rest")]
    public class SitPacket : PacketDefinition
    {
        #region Properties

        [PacketIndex(0)]
        public byte Ammout { get; set; }

        [PacketIndex(1, RemoveSeparator = true)]
        public List<SitSubPacket> Users { get; set; }

        #endregion
    }
}