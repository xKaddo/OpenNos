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

using OpenNos.Core;
using OpenNos.Domain;
using System.Collections.Generic;
using System.Linq;

namespace OpenNos.GameObject
{
    public class Group
    {
        #region Members

        private ThreadSafeGenericList<ClientSession> _characters;
        private int _order;

        #endregion

        #region Instantiation

        public Group()
        {
            _characters = new ThreadSafeGenericList<ClientSession>();
            GroupId = ServerManager.Instance.GetNextGroupId();
            _order = 0;
        }

        #endregion

        #region Properties

        public int CharacterCount
        {
            get
            {
                return _characters.Count;
            }
        }

        public ThreadSafeGenericList<ClientSession> Characters
        {
            get
            {
                return _characters;
            }
        }

        public long GroupId { get; set; }

        public byte SharingMode { get; set; }

        #endregion

        #region Methods

        public List<string> GeneratePst(ClientSession player)
        {
            List<string> str = new List<string>();
            var i = 0;
            foreach (ClientSession session in Characters)
            {
                if (session == player)
                {
                    str.AddRange(
                        player.Character.Mates.Where(s => s.IsTeamMember)
                            .OrderByDescending(s => s.MateType)
                            .Select(
                                mate =>
                                    $"pst 2 {mate.MateTransportId} {(mate.MateType == MateType.Partner ? "0" : "1")} {mate.Hp / mate.MaxHp * 100} {mate.Mp / mate.MaxMp * 100} {mate.Hp} {mate.Mp} 0 0 0"));
                    i = session.Character.Mates.Count(s => s.IsTeamMember);
                    str.Add(
                        $"pst 1 {session.Character.CharacterId} {++i} {(int)(session.Character.Hp / session.Character.HPLoad() * 100)} {(int)(session.Character.Mp / session.Character.MPLoad() * 100)} {session.Character.HPLoad()} {session.Character.MPLoad()} {(byte)session.Character.Class} {(byte)session.Character.Gender} {(session.Character.UseSp ? session.Character.Morph : 0)}");
                }
                else
                {
                    str.Add(
                        $"pst 1 {session.Character.CharacterId} {++i} {(int)(session.Character.Hp / session.Character.HPLoad() * 100)} {(int)(session.Character.Mp / session.Character.MPLoad() * 100)} {session.Character.HPLoad()} {session.Character.MPLoad()} {(byte)session.Character.Class} {(byte)session.Character.Gender} {(session.Character.UseSp ? session.Character.Morph : 0)}{session.Character.Buff.GetAllActiveBuffs()}");
                }
            }
            return str;
        }

        public long? GetNextOrderedCharacterId(Character character)
        {
            lock (this)
            {
                _order++;
                List<ClientSession> sessions = Characters.Where(s => Map.GetDistance(s.Character, character) < 50).ToList();
                if (_order > sessions.Count - 1) // if order wents out of amount of ppl, reset it -> zero based index
                {
                    _order = 0;
                }

                if (!sessions.Any()) // group seems to be empty
                {
                    return null;
                }

                return sessions[_order].Character.CharacterId;
            }
        }

        public bool IsMemberOfGroup(long characterId)
        {
            return _characters != null && _characters.Any(s => s?.Character?.CharacterId == characterId);
        }

        public bool IsMemberOfGroup(ClientSession session)
        {
            return _characters != null && _characters.Any(s => s?.Character?.CharacterId == session.Character.CharacterId);
        }

        public void JoinGroup(long characterId)
        {
            ClientSession session = ServerManager.Instance.GetSessionByCharacterId(characterId);
            if (session != null)
            {
                JoinGroup(session);
            }
        }

        public void JoinGroup(ClientSession session)
        {
            session.Character.Group = this;
            _characters.Add(session);
        }

        public void LeaveGroup(ClientSession session)
        {
            session.Character.Group = null;
            _characters.RemoveAll(s => s?.Character.CharacterId == session.Character.CharacterId);
        }

        #endregion
    }
}