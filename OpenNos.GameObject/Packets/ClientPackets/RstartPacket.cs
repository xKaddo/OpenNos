﻿////<auto-generated <- Codemaid exclusion for now (PacketIndex Order is important for maintenance)

using OpenNos.Core;

namespace OpenNos.GameObject
{
    [PacketHeader("rstart")]
    public class RstartPacket : PacketDefinition
    {
        #region Properties        

        [PacketIndex(0)]
        public byte? Type { get; set; }
        
        #endregion
    }
}