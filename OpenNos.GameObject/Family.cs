﻿/*
 * This file is part of the OpenNos Emulator Project. See AUTHORS file for Copyright information
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation; either version 2 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 */

using OpenNos.DAL;
using OpenNos.Data;
using OpenNos.Domain;
using OpenNos.WebApi.Reference;
using System;
using System.Collections.Generic;

namespace OpenNos.GameObject
{
    public class Family : FamilyDTO
    {
        #region Instantiation

        public Family()
        {
            FamilyCharacters = new List<FamilyCharacter>();
        }

        #endregion

        #region Properties

        public List<FamilyCharacter> FamilyCharacters { get; set; }

        public List<FamilyLogDTO> FamilyLogs { get; set; }

        public List<ItemInstance> Warehouse { get; set; }

        public MapInstance LandOfDeath { get; set; }

        #endregion

        #region Methods

        public override void Initialize()
        {
        }

        public void InsertFamilyLog(FamilyLogType logtype, string characterName = "", string characterName2 = "", string rainBowFamily = "", string message = "", byte level = 0, int experience = 0, int itemVNum = 0, byte upgrade = 0, int raidType = 0, int right = 0, int righttype = 0, int rightvalue = 0)
        {
            string value = string.Empty;
            switch (logtype)
            {
                case FamilyLogType.DailyMessage:
                    value = $"{characterName}|{message}";
                    break;

                case FamilyLogType.FamilyXP:
                    value = $"{characterName}|{experience}";
                    break;

                case FamilyLogType.Level:
                    value = $"{characterName}|{level}";
                    break;

                case FamilyLogType.Raid:
                    value = raidType.ToString();
                    break;

                case FamilyLogType.Upgrade:
                    value = $"{characterName}|{itemVNum}|{upgrade}";
                    break;

                case FamilyLogType.UserManage:
                    value = $"{characterName}|{characterName2}";
                    break;

                case FamilyLogType.FamilyLevel:
                    value = level.ToString();
                    break;

                case FamilyLogType.AuthorityChange:
                    value = $"{characterName}|{right}|{characterName2}";
                    break;

                case FamilyLogType.FamilyManage:
                    value = characterName;
                    break;

                case FamilyLogType.RainbowBattle:
                    value = rainBowFamily;
                    break;

                case FamilyLogType.RightChange:
                    value = $"{characterName}|{right}|{righttype}|{rightvalue}";
                    break;
            }
            FamilyLogDTO log = new FamilyLogDTO
            {
                FamilyId = FamilyId,
                FamilyLogData = value,
                FamilyLogType = logtype,
                Timestamp = DateTime.Now
            };
            DAOFactory.FamilyLogDAO.InsertOrUpdate(ref log);
            ServerManager.Instance.FamilyRefresh(FamilyId);
            int? sentChannelId2 = ServerCommunicationClient.Instance.HubProxy.Invoke<int?>("SendMessageToCharacter", ServerManager.ServerGroup, string.Empty, FamilyId.ToString(), "fhis_stc", ServerManager.Instance.ChannelId, MessageType.Family).Result;
        }

        internal Family DeepCopy()
        {
            Family clonedCharacter = (Family)MemberwiseClone();
            return clonedCharacter;
        }

        #endregion
    }
}