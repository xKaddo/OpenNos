﻿////<auto-generated <- Codemaid exclusion for now (PacketIndex Order is important for maintenance)

using OpenNos.Core;
using OpenNos.Domain;

namespace OpenNos.GameObject
{
    [PacketHeader("$AddSkill", PassNonParseablePacket = true, Authority = AuthorityType.GameMaster)]
    public class AddSkillPacket : PacketDefinition
    {
        #region Properties

        [PacketIndex(0)]
        public short SkillVnum { get; set; }

        #endregion
    }
}