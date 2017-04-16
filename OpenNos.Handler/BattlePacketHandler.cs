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
using OpenNos.Data;
using OpenNos.Domain;
using OpenNos.GameObject;
using OpenNos.GameObject.Buff.Indicators;
using OpenNos.GameObject.Buff.Indicators.NoSP.Archer;
using OpenNos.GameObject.Buff.Indicators.SP1.Archer;
using OpenNos.GameObject.Buff.Indicators.SP1.Magician;
using OpenNos.GameObject.Buff.Indicators.SP1.Swordsman;
using OpenNos.GameObject.Buff.Indicators.SP2.Archer;
using OpenNos.GameObject.Buff.Indicators.SP2.Magician;
using OpenNos.GameObject.Buff.Indicators.SP2.Swordsman;
using OpenNos.GameObject.Buff.Indicators.SP3.Archer;
using OpenNos.GameObject.Buff.Indicators.SP3.Magician;
using OpenNos.GameObject.Buff.Indicators.SP3.Swordsman;
using OpenNos.GameObject.Buff.Indicators.SP4.Archer;
using OpenNos.GameObject.Buff.Indicators.SP4.Magician;
using OpenNos.GameObject.Buff.Indicators.SP4.Swordsman;
using OpenNos.GameObject.Helpers;
using OpenNos.GameObject.Networking;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;

namespace OpenNos.Handler
{
    public class BattlePacketHandler : IPacketHandler
    {
        #region Instantiation

        public BattlePacketHandler(ClientSession session)
        {
            Session = session;
        }

        #endregion

        #region Properties

        private ClientSession Session { get; }

        #endregion

        #region Methods

        /// <summary>
        /// mtlist
        /// </summary>
        /// <param name="mutliTargetListPacket"></param>
        public void MultiTargetListHit(MultiTargetListPacket mutliTargetListPacket)
        {
            PenaltyLogDTO penalty = Session.Account.PenaltyLogs.OrderByDescending(s => s.DateEnd).FirstOrDefault();
            if (Session.Character.IsMuted() && penalty != null)
            {
                Session.SendPacket("cancel 0 0");
                Session.CurrentMapInstance?.Broadcast(Session.Character.Gender == GenderType.Female ? Session.Character.GenerateSay(Language.Instance.GetMessageFromKey("MUTED_FEMALE"), 1) :
                    Session.Character.GenerateSay(Language.Instance.GetMessageFromKey("MUTED_MALE"), 1));
                Session.SendPacket(Session.Character.GenerateSay(string.Format(Language.Instance.GetMessageFromKey("MUTE_TIME"), (penalty.DateEnd - DateTime.Now).ToString("hh\\:mm\\:ss")), 11));
                Session.SendPacket(Session.Character.GenerateSay(string.Format(Language.Instance.GetMessageFromKey("MUTE_TIME"), (penalty.DateEnd - DateTime.Now).ToString("hh\\:mm\\:ss")), 12));
                return;
            }
            if ((DateTime.Now - Session.Character.LastTransform).TotalSeconds < 3)
            {
                Session.SendPacket("cancel 0 0");
                Session.SendPacket(UserInterfaceHelper.Instance.GenerateMsg(Language.Instance.GetMessageFromKey("CANT_ATTACKNOW"), 0));
                return;
            }
            if (Session.Character.IsVehicled)
            {
                Session.SendPacket("cancel 0 0");
                return;
            }
            Logger.Debug(Session.Character.GenerateIdentity(), mutliTargetListPacket.ToString());
            if (mutliTargetListPacket.TargetsAmount > 0 && mutliTargetListPacket.TargetsAmount == mutliTargetListPacket.Targets.Count)
            {
                foreach (MultiTargetListSubPacket subpacket in mutliTargetListPacket.Targets)
                {
                    List<CharacterSkill> skills = Session.Character.UseSp ? Session.Character.SkillsSp.GetAllItems() : Session.Character.Skills.GetAllItems();
                    CharacterSkill ski = skills?.FirstOrDefault(s => s.Skill.CastId == subpacket.SkillCastId - 1);
                    if (ski != null && ski.CanBeUsed() && Session.HasCurrentMapInstance)
                    {
                        MapMonster mon = Session.CurrentMapInstance.GetMonster(subpacket.TargetId);
                        if (mon != null && mon.IsInRange(Session.Character.PositionX, Session.Character.PositionY, ski.Skill.Range) && mon.CurrentHp > 0)
                        {
                            Session.Character.LastSkillUse = DateTime.Now;
                            mon.HitQueue.Enqueue(new HitRequest(TargetHitType.SpecialZoneHit, Session, ski.Skill));
                        }

                        Observable.Timer(TimeSpan.FromMilliseconds(ski.Skill.Cooldown * 100))
                            .Subscribe(
                                o =>
                                {
                                    Session.SendPacket($"sr {subpacket.SkillCastId - 1}");
                                }
                            );
                    }
                }
            }
        }

        /// <summary>
        /// u_s
        /// </summary>
        /// <param name="useSkillPacket"></param>
        public void UseSkill(UseSkillPacket useSkillPacket)
        {
            if (Session.Character.CanFight && useSkillPacket != null)
            {
                PenaltyLogDTO penalty = Session.Account.PenaltyLogs.OrderByDescending(s => s.DateEnd).FirstOrDefault();
                if (Session.Character.IsMuted() && penalty != null)
                {
                    if (Session.Character.Gender == GenderType.Female)
                    {
                        Session.SendPacket("cancel 0 0");
                        Session.CurrentMapInstance?.Broadcast(Session.Character.GenerateSay(Language.Instance.GetMessageFromKey("MUTED_FEMALE"), 1));
                        Session.SendPacket(Session.Character.GenerateSay(string.Format(Language.Instance.GetMessageFromKey("MUTE_TIME"), (penalty.DateEnd - DateTime.Now).ToString("hh\\:mm\\:ss")), 11));
                    }
                    else
                    {
                        Session.SendPacket("cancel 0 0");
                        Session.CurrentMapInstance?.Broadcast(Session.Character.GenerateSay(Language.Instance.GetMessageFromKey("MUTED_MALE"), 1));
                        Session.SendPacket(Session.Character.GenerateSay(string.Format(Language.Instance.GetMessageFromKey("MUTE_TIME"), (penalty.DateEnd - DateTime.Now).ToString("hh\\:mm\\:ss")), 11));
                    }
                    return;
                }

                Logger.Debug(Session.Character.GenerateIdentity(), useSkillPacket.ToString());

                if (useSkillPacket.MapX.HasValue && useSkillPacket.MapY.HasValue)
                {
                    Session.Character.PositionX = useSkillPacket.MapX.Value;
                    Session.Character.PositionY = useSkillPacket.MapY.Value;
                }
                if (Session.Character.IsSitting)
                {
                    Session.Character.Rest();
                }
                if (Session.Character.IsVehicled || Session.Character.InvisibleGm)
                {
                    Session.SendPacket("cancel 0 0");
                    return;
                }
                switch (useSkillPacket.UserType)
                {
                    case UserType.Monster:
                        if (Session.Character.Hp > 0)
                        {
                            TargetHit(useSkillPacket.CastId, useSkillPacket.MapMonsterId);
                        }
                        break;

                    case UserType.Player:
                        if (Session.Character.Hp > 0)
                        {
                            if (useSkillPacket.MapMonsterId != Session.Character.CharacterId)
                            {
                                TargetHit(useSkillPacket.CastId, useSkillPacket.MapMonsterId, true);
                            }
                            else
                            {
                                TargetHit(useSkillPacket.CastId, useSkillPacket.MapMonsterId);
                            }
                        }
                        else
                        {
                            Session.SendPacket("cancel 2 0");
                        }
                        break;

                    default:
                        Session.SendPacket("cancel 2 0");
                        return;
                }
            }
        }

        /// <summary>
        /// u_as
        /// </summary>
        /// <param name="useAOESkillPacket"></param>
        public void UseZonesSkill(UseAOESkillPacket useAOESkillPacket)
        {
            PenaltyLogDTO penalty = Session.Account.PenaltyLogs.OrderByDescending(s => s.DateEnd).FirstOrDefault();
            if (Session.Character.IsMuted() && penalty != null)
            {
                if (Session.Character.Gender == GenderType.Female)
                {
                    Session.SendPacket("cancel 0 0");
                    Session.CurrentMapInstance?.Broadcast(Session.Character.GenerateSay(Language.Instance.GetMessageFromKey("MUTED_FEMALE"), 1));
                    Session.SendPacket(Session.Character.GenerateSay(string.Format(Language.Instance.GetMessageFromKey("MUTE_TIME"), (penalty.DateEnd - DateTime.Now).ToString("hh\\:mm\\:ss")), 11));
                    Session.SendPacket(Session.Character.GenerateSay(string.Format(Language.Instance.GetMessageFromKey("MUTE_TIME"), (penalty.DateEnd - DateTime.Now).ToString("hh\\:mm\\:ss")), 12));
                }
                else
                {
                    Session.SendPacket("cancel 0 0");
                    Session.CurrentMapInstance?.Broadcast(Session.Character.GenerateSay(Language.Instance.GetMessageFromKey("MUTED_MALE"), 1));
                    Session.SendPacket(Session.Character.GenerateSay(string.Format(Language.Instance.GetMessageFromKey("MUTE_TIME"), (penalty.DateEnd - DateTime.Now).ToString("hh\\:mm\\:ss")), 11));
                    Session.SendPacket(Session.Character.GenerateSay(string.Format(Language.Instance.GetMessageFromKey("MUTE_TIME"), (penalty.DateEnd - DateTime.Now).ToString("hh\\:mm\\:ss")), 12));
                }
            }
            else
            {
                if (Session.Character.LastTransform.AddSeconds(3) > DateTime.Now)
                {
                    Session.SendPacket("cancel 0 0");
                    Session.SendPacket(UserInterfaceHelper.Instance.GenerateMsg(Language.Instance.GetMessageFromKey("CANT_ATTACK"), 0));
                    return;
                }
                if (Session.Character.IsVehicled)
                {
                    Session.SendPacket("cancel 0 0");
                    return;
                }
                Logger.Debug(Session.Character.GenerateIdentity(), useAOESkillPacket.ToString());
                if (Session.Character.CanFight)
                {
                    if (Session.Character.Hp > 0)
                    {
                        ZoneHit(useAOESkillPacket.CastId, useAOESkillPacket.MapX, useAOESkillPacket.MapY);
                    }
                }
            }
        }

        private void PVPHit(HitRequest hitRequest, ClientSession target)
        {
            if (target.Character.Hp > 0 && hitRequest.Session.Character.Hp > 0)
            {
                if (target.Character.IsSitting)
                {
                    target.Character.Rest();
                }
                int hitmode = 0;

                // calculate damage
                //int damage = hitRequest.Session.Character.GenerateDamage(this, hitRequest.Skill, ref hitmode);
                int damage = hitRequest.Session.Character.GeneratePVPDamage(target.Character, hitRequest.Skill, ref hitmode);
                if (target.Character.HasGodMode)
                {
                    damage = 0;
                    hitmode = 1;
                }
                else if (target.Character.LastPVPRevive > DateTime.Now.AddSeconds(-10) || hitRequest.Session.Character.LastPVPRevive > DateTime.Now.AddSeconds(-10))
                {
                    damage = 0;
                    hitmode = 1;
                }
                target.Character.GetDamage(damage / 2);
                target.Character.LastDefence = DateTime.Now;
                target.SendPacket(target.Character.GenerateStat());
                bool IsAlive = target.Character.Hp > 0;
                if (!IsAlive)
                {
                    if (target?.CurrentMapInstance?.Map?.MapTypes.Any(s => s.MapTypeId == (short)MapTypeEnum.Act4) == true)
                    {
                        hitRequest.Session.Character.Act4Kill += 1;
                        target.Character.Act4Dead += 1;
                        target.Character.GetAct4Points(-1);
                        if (target.Character.Level + 10 >= hitRequest.Session.Character.Level && hitRequest.Session.Character.Level <= target.Character.Level - 10)
                        {
                            hitRequest.Session.Character.GetAct4Points(2);
                        }
                        if (target.Character.Reput < 50000)
                        {
                            target.SendPacket(Session.Character.GenerateSay(string.Format(Language.Instance.GetMessageFromKey("LOSE_REP"), 0), 11));
                        }
                        else
                        {
                            target.Character.Reput -= target.Character.Level * 50;
                            hitRequest.Session.Character.Reput += target.Character.Level * 50;
                            hitRequest.Session.SendPacket(hitRequest.Session.Character.GenerateLev());
                            target.SendPacket(target.Character.GenerateSay(string.Format(Language.Instance.GetMessageFromKey("LOSE_REP"), (short)(target.Character.Level * 50)), 11));
                        }
                        target.SendPacket(target.Character.GenerateFd());
                        Session.Character.Buff.Clear();
                        target.CurrentMapInstance?.Broadcast(target, target.Character.GenerateIn(), ReceiverType.AllExceptMe);
                        target.CurrentMapInstance?.Broadcast(target, target.Character.GenerateGidx(), ReceiverType.AllExceptMe);
                        target.SendPacket(target.Character.GenerateSay(Language.Instance.GetMessageFromKey("ACT4_PVP_DIE"), 11));
                        target.SendPacket(UserInterfaceHelper.Instance.GenerateMsg(Language.Instance.GetMessageFromKey("ACT4_PVP_DIE"), 0));
                        Observable.Timer(TimeSpan.FromMilliseconds(30000))
                               .Subscribe(
                               o =>
                               {
                                   target.Character.Hp = (int)target.Character.HPLoad();
                                   target.Character.Mp = (int)target.Character.MPLoad();
                                   short x = (short)(39 + ServerManager.Instance.RandomNumber(-2, 3));
                                   short y = (short)(42 + ServerManager.Instance.RandomNumber(-2, 3));
                                   ServerManager.Instance.ChangeMap(target.Character.CharacterId, 130, x, y);
                                   target.CurrentMapInstance?.Broadcast(target, target.Character.GenerateTp());
                                   target.CurrentMapInstance?.Broadcast(target.Character.GenerateRevive());
                                   target.SendPacket(target.Character.GenerateStat());
                               });
                    }
                    else
                    {
                        hitRequest.Session.Character.TalentWin += 1;
                        target.Character.TalentLose += 1;
                        Observable.Timer(TimeSpan.FromMilliseconds(1000))
                               .Subscribe(
                               o =>
                               {
                                   ServerManager.Instance.AskPVPRevive(target.Character.CharacterId);
                               });
                    }
                }
                switch (hitRequest.TargetHitType)
                {
                    case TargetHitType.SingleTargetHit:
                        {
                            // Target Hit
                            hitRequest.Session.CurrentMapInstance?.Broadcast($"su 1 {hitRequest.Session.Character.CharacterId} 1 {target.Character.CharacterId} {hitRequest.Skill.SkillVNum} {hitRequest.Skill.Cooldown} {hitRequest.Skill.AttackAnimation} {hitRequest.SkillEffect} {hitRequest.Session.Character.PositionX} {hitRequest.Session.Character.PositionY} {(IsAlive ? 1 : 0)} {(int)((float)target.Character.Hp / (float)target.Character.HPLoad() * 100)} {damage} {hitmode} {hitRequest.Skill.SkillType - 1}");
                            break;
                        }
                    case TargetHitType.SingleTargetHitCombo:
                        {
                            // Taget Hit Combo
                            hitRequest.Session.CurrentMapInstance?.Broadcast($"su 1 {hitRequest.Session.Character.CharacterId} 1 {target.Character.CharacterId} {hitRequest.Skill.SkillVNum} {hitRequest.Skill.Cooldown} {hitRequest.SkillCombo.Animation} {hitRequest.SkillCombo.Effect} {hitRequest.Session.Character.PositionX} {hitRequest.Session.Character.PositionY} {(IsAlive ? 1 : 0)} {(int)((float)target.Character.Hp / (float)target.Character.HPLoad() * 100)} {damage} {hitmode} {hitRequest.Skill.SkillType - 1}");
                            break;
                        }
                    case TargetHitType.SingleAOETargetHit:
                        {
                            // Target Hit Single AOE
                            switch (hitmode)
                            {
                                case 1:
                                    hitmode = 4;
                                    break;

                                case 3:
                                    hitmode = 6;
                                    break;

                                default:
                                    hitmode = 5;
                                    break;
                            }
                            if (hitRequest.ShowTargetHitAnimation)
                            {
                                hitRequest.Session.CurrentMapInstance?.Broadcast($"su 1 {hitRequest.Session.Character.CharacterId} 1 {target.Character.CharacterId} {hitRequest.Skill.SkillVNum} {hitRequest.Skill.Cooldown} {hitRequest.Skill.AttackAnimation} {hitRequest.SkillEffect} 0 0 {(IsAlive ? 1 : 0)} {(int)((double)target.Character.Hp / target.Character.HPLoad()) * 100} 0 0 {hitRequest.Skill.SkillType - 1}");
                            }
                            hitRequest.Session.CurrentMapInstance?.Broadcast($"su 1 {hitRequest.Session.Character.CharacterId} 1 {target.Character.CharacterId} {hitRequest.Skill.SkillVNum} {hitRequest.Skill.Cooldown} {hitRequest.Skill.AttackAnimation} {hitRequest.SkillEffect} {hitRequest.Session.Character.PositionX} {hitRequest.Session.Character.PositionY} {(IsAlive ? 1 : 0)} {(int)((float)target.Character.Hp / (float)target.Character.HPLoad() * 100)} {damage} {hitmode} {hitRequest.Skill.SkillType - 1}");
                            break;
                        }
                    case TargetHitType.AOETargetHit:
                        {
                            // Target Hit AOE
                            switch (hitmode)
                            {
                                case 1:
                                    hitmode = 4;
                                    break;

                                case 3:
                                    hitmode = 6;
                                    break;

                                default:
                                    hitmode = 5;
                                    break;
                            }
                            hitRequest.Session.CurrentMapInstance?.Broadcast($"su 1 {hitRequest.Session.Character.CharacterId} 1 {target.Character.CharacterId} {hitRequest.Skill.SkillVNum} {hitRequest.Skill.Cooldown} {hitRequest.Skill.AttackAnimation} {hitRequest.SkillEffect} {hitRequest.Session.Character.PositionX} {hitRequest.Session.Character.PositionY} {(IsAlive ? 1 : 0)} {(int)((float)target.Character.Hp / (float)target.Character.HPLoad() * 100)} {damage} {hitmode} {hitRequest.Skill.SkillType - 1}");
                            break;
                        }
                    case TargetHitType.ZoneHit:
                        {
                            // Zone HIT
                            hitRequest.Session.CurrentMapInstance?.Broadcast($"su 1 {hitRequest.Session.Character.CharacterId} 1 {target.Character.CharacterId} {hitRequest.Skill.SkillVNum} {hitRequest.Skill.Cooldown} {hitRequest.Skill.AttackAnimation} {hitRequest.SkillEffect} {hitRequest.MapX} {hitRequest.MapY} {(IsAlive ? 1 : 0)} {(int)((float)target.Character.Hp / (float)target.Character.HPLoad() * 100)} {damage} 5 {hitRequest.Skill.SkillType - 1}");
                            break;
                        }
                    case TargetHitType.SpecialZoneHit:
                        {
                            // Special Zone hit
                            hitRequest.Session.CurrentMapInstance?.Broadcast($"su 1 {hitRequest.Session.Character.CharacterId} 1 {target.Character.CharacterId} {hitRequest.Skill.SkillVNum} {hitRequest.Skill.Cooldown} {hitRequest.Skill.AttackAnimation} {hitRequest.SkillEffect} {hitRequest.Session.Character.PositionX} {hitRequest.Session.Character.PositionY} {(IsAlive ? 1 : 0)} {(int)((float)target.Character.Hp / (float)target.Character.HPLoad() * 100)} {damage} 0 {hitRequest.Skill.SkillType - 1}");
                            break;
                        }
                }
            }
            else
            {
                // monster already has been killed, send cancel
                hitRequest.Session.SendPacket($"cancel 2 {target.Character.CharacterId}");
            }
        }

        private void TargetHit(int castingId, int targetId, bool isPvp = false)
        {
            if ((DateTime.Now - Session.Character.LastTransform).TotalSeconds < 3)
            {
                Session.SendPacket("cancel 0 0");
                Session.SendPacket(UserInterfaceHelper.Instance.GenerateMsg(Language.Instance.GetMessageFromKey("CANT_ATTACK"), 0));
                return;
            }

            List<CharacterSkill> skills = Session.Character.UseSp ? Session.Character.SkillsSp?.GetAllItems() : Session.Character.Skills?.GetAllItems();

            if (skills != null)
            {
                CharacterSkill ski = skills.FirstOrDefault(s => s.Skill?.CastId == castingId && s.Skill?.UpgradeSkill == 0);
                if (castingId != 0)
                {
                    Session.SendPacket("ms_c 0");
                }
                if (ski != null && (!Session.Character.WeaponLoaded(ski) || !ski.CanBeUsed()))
                {
                    Session.SendPacket($"cancel 2 {targetId}");
                    return;
                }

                if (ski != null && Session.Character.Mp >= ski.Skill.MpCost)
                {
                    // AOE Target hit
                    if (ski.Skill.TargetType == 1 && ski.Skill.HitType == 1)
                    {
                        Session.Character.LastSkillUse = DateTime.Now;
                        if (!Session.Character.HasGodMode)
                        {
                            Session.Character.Mp -= ski.Skill.MpCost;
                        }

                        if (Session.Character.UseSp && ski.Skill.CastEffect != -1)
                        {
                            Session.SendPackets(Session.Character.GenerateQuicklist());
                        }

                        Session.SendPacket(Session.Character.GenerateStat());
                        CharacterSkill skillinfo = Session.Character.Skills.GetAllItems().OrderBy(o => o.SkillVNum).FirstOrDefault(s => s.Skill.UpgradeSkill == ski.Skill.SkillVNum && s.Skill.Effect > 0 && s.Skill.SkillType == 2);
                        Session.CurrentMapInstance?.Broadcast($"ct 1 {Session.Character.CharacterId} 1 {Session.Character.CharacterId} {ski.Skill.CastAnimation} {skillinfo?.Skill.CastEffect ?? ski.Skill.CastEffect} {ski.Skill.SkillVNum}");

                        // Generate scp
                        ski.LastUse = DateTime.Now;
                        if (ski.Skill.CastEffect != 0)
                        {
                            Thread.Sleep(ski.Skill.CastTime * 100);
                        }
                        if (Session.HasCurrentMapInstance)
                        {
                            Session.CurrentMapInstance.Broadcast($"su 1 {Session.Character.CharacterId} 1 {Session.Character.CharacterId} {ski.Skill.SkillVNum} {ski.Skill.Cooldown} {ski.Skill.AttackAnimation} {skillinfo?.Skill.Effect ?? ski.Skill.Effect} {Session.Character.PositionX} {Session.Character.PositionY} 1 {(int)((double)Session.Character.Hp / Session.Character.HPLoad()) * 100} 0 -2 {ski.Skill.SkillType - 1}");
                            if (ski.Skill.TargetRange != 0 && Session.HasCurrentMapInstance)
                            {
                                foreach (ClientSession character in ServerManager.Instance.Sessions.Where(s => s.CurrentMapInstance == Session.CurrentMapInstance && s.Character.CharacterId != Session.Character.CharacterId && s.Character.IsInRange(Session.Character.PositionX, Session.Character.PositionY, ski.Skill.TargetRange)))
                                {
                                    if (Session.CurrentMapInstance.Map.MapTypes.Any(s => s.MapTypeId == (short)MapTypeEnum.Act4))
                                    {
                                        if (Session.Character.Family == null || character.Character.Family == null || Session.Character.Family.FamilyId != character.Character.Family.FamilyId)
                                        {
                                            if (Session.Character.MapInstance.Map.MapTypes.Any(m => m.MapTypeId == (short)MapTypeEnum.Citadel))
                                            {
                                                PVPHit(new HitRequest(TargetHitType.AOETargetHit, Session, ski.Skill), character);
                                            }
                                        }
                                    }
                                    else if (Session.Character.MapInstance.Map.MapTypes.Any(m => m.MapTypeId == (short)MapTypeEnum.PVPMap))
                                    {
                                        if (Session.Character.Group == null || !Session.Character.Group.IsMemberOfGroup(character.Character.CharacterId))
                                        {
                                            PVPHit(new HitRequest(TargetHitType.AOETargetHit, Session, ski.Skill), character);
                                        }
                                    }
                                    else if (Session.CurrentMapInstance.IsPVP)
                                    {
                                        if (Session.Character.Group == null || !Session.Character.Group.IsMemberOfGroup(character.Character.CharacterId))
                                        {
                                            PVPHit(new HitRequest(TargetHitType.AOETargetHit, Session, ski.Skill), character);
                                        }
                                    }
                                    else
                                    {
                                        Session.SendPacket($"cancel 2 {targetId}");
                                    }
                                }
                                foreach (
                                    MapMonster mon in
                                    Session.CurrentMapInstance.GetListMonsterInRange(Session.Character.PositionX,
                                        Session.Character.PositionY, ski.Skill.TargetRange).Where(s => s.CurrentHp > 0))
                                {
                                    mon.HitQueue.Enqueue(new HitRequest(
                                        TargetHitType.AOETargetHit, Session, ski.Skill,
                                        skillinfo?.Skill.Effect ?? ski.Skill.Effect));
                                }
                            }
                        }
                    }
                    else if (ski.Skill.TargetType == 2 && ski.Skill.HitType == 0)
                    {
                        Session.CurrentMapInstance?.Broadcast($"ct 1 {Session.Character.CharacterId} 1 {Session.Character.CharacterId} {ski.Skill.CastAnimation} {ski.Skill.CastEffect} {ski.Skill.SkillVNum}");
                        Session.CurrentMapInstance?.Broadcast($"su 1 {Session.Character.CharacterId} 1 {targetId} {ski.Skill.SkillVNum} {ski.Skill.Cooldown} {ski.Skill.AttackAnimation} {ski.Skill.Effect} {Session.Character.PositionX} {Session.Character.PositionY} 1 {(int)((double)Session.Character.Hp / Session.Character.HPLoad()) * 100} 0 -1 {ski.Skill.SkillType - 1}");
                        ClientSession target = ServerManager.Instance.GetSessionByCharacterId(targetId) ?? Session;
                        switch (ski.Skill.Effect)
                        {
                            case 3409:
                                IndicatorBase triplecharging = new FirstBlessing(Session.Character.Level);
                                target.Character.Buff.Add(triplecharging);
                                break;

                            case 3411:
                                IndicatorBase shiningeffect = new ShiningEffect(Session.Character.Level);
                                target.Character.Buff.Add(shiningeffect);
                                break;

                            case 4203:
                                IndicatorBase hawkeye = new HawkEye(Session.Character.Level);
                                target.Character.Buff.Add(hawkeye);
                                break;

                            case 4207:
                                IndicatorBase windwalker = new WindWalker(Session.Character.Level);
                                target.Character.Buff.Add(windwalker);
                                break;

                            case 4403:
                                IndicatorBase healing = new Healing(Session.Character.Level);
                                target.Character.Buff.Add(healing);
                                break;
                        }
                    }
                    else if (ski.Skill.TargetType == 1 && ski.Skill.HitType != 1)
                    {
                        Session.CurrentMapInstance?.Broadcast($"ct 1 {Session.Character.CharacterId} 1 {Session.Character.CharacterId} {ski.Skill.CastAnimation} {ski.Skill.CastEffect} {ski.Skill.SkillVNum}");
                        Session.CurrentMapInstance?.Broadcast($"su 1 {Session.Character.CharacterId} 1 {Session.Character.CharacterId} {ski.Skill.SkillVNum} {ski.Skill.Cooldown} {ski.Skill.AttackAnimation} {ski.Skill.Effect} {Session.Character.PositionX} {Session.Character.PositionY} 1 {(int)((double)Session.Character.Hp / Session.Character.HPLoad()) * 100} 0 -1 {ski.Skill.SkillType - 1}");
                        switch (ski.Skill.HitType)
                        {
                            case 2:
                                IEnumerable<ClientSession> clientSessions = Session.CurrentMapInstance?.Sessions?.Where(s => s.Character.IsInRange(Session.Character.PositionX, Session.Character.PositionY, ski.Skill.TargetRange));
                                if (clientSessions != null)
                                {
                                    foreach (ClientSession target in clientSessions)
                                    {
                                        switch (ski.Skill.Effect)
                                        {
                                            case 4117:
                                                IndicatorBase moraleincrease = new MoraleIncrease(Session.Character.Level);
                                                IndicatorBase sprint = new Sprint(Session.Character.Level);
                                                target.Character.Buff.Add(moraleincrease);
                                                target.Character.Buff.Add(sprint);
                                                break;

                                            case 3417:
                                                IndicatorBase prayerofdefence = new PrayerofDefence(Session.Character.Level);
                                                target.Character.Buff.Add(prayerofdefence);
                                                break;

                                            case 3419:
                                                IndicatorBase prayerofoffence = new PrayerofOffence(Session.Character.Level);
                                                target.Character.Buff.Add(prayerofoffence);
                                                break;

                                            case 4013:
                                                IndicatorBase fireblessing = new FireBlessing(Session.Character.Level);
                                                target.Character.Buff.Add(fireblessing);
                                                break;

                                            case 4415:
                                                IndicatorBase grouhealing = new GroupHealing(Session.Character.Level);
                                                target.Character.Buff.Add(grouhealing);
                                                break;

                                            case 4417:
                                                IndicatorBase holyweapon = new HolyWeapon(Session.Character.Level);
                                                target.Character.Buff.Add(holyweapon);
                                                break;

                                            case 4419:
                                                IndicatorBase blessing = new Blessing(Session.Character.Level);
                                                target.Character.Buff.Add(blessing);
                                                break;

                                            case 3815:
                                                IndicatorBase bow = new BlessingofWater(Session.Character.Level);
                                                target.Character.Buff.Add(bow);
                                                break;

                                            case 3910:
                                                IndicatorBase darkforce = new DarkForce(Session.Character.Level);
                                                target.Character.Buff.Add(darkforce);
                                                break;

                                            case 3708:
                                                IndicatorBase elementalshine = new ElementalShine(Session.Character.Level);
                                                target.Character.Buff.Add(elementalshine);
                                                break;

                                            case 3706:
                                                switch (ski.Skill.SkillVNum)
                                                {
                                                    case 931:
                                                        IndicatorBase bearspirit = new BearSpirit(Session.Character.Level);
                                                        target.Character.Buff.Add(bearspirit);
                                                        break;

                                                    case 928:
                                                        IndicatorBase wolfghost = new WolfGhost(Session.Character.Level);
                                                        target.Character.Buff.Add(wolfghost);
                                                        break;
                                                }
                                                break;
                                        }
                                    }
                                }
                                break;

                            case 0:
                                switch (ski.Skill.Effect)
                                {
                                    case 4106:
                                        IndicatorBase ironskin = new IronSkin(Session.Character.Level);
                                        Session.Character.Buff.Add(ironskin);
                                        break;

                                    case 4318:
                                        IndicatorBase sharpedge = new SharpEdge(Session.Character.Level);
                                        Session.Character.Buff.Add(sharpedge);
                                        break;

                                    case 4314:
                                        IndicatorBase breathofrecovery = new BreathofRecovery(Session.Character.Level);
                                        Session.Character.Buff.Add(breathofrecovery);
                                        break;

                                    case 3415:
                                        IndicatorBase holyshield = new HolyShield(Session.Character.Level);
                                        Session.Character.Buff.Add(holyshield);
                                        break;

                                    case 3506:
                                        IndicatorBase berserker = new Berserker(Session.Character.Level);
                                        Session.Character.Buff.Add(berserker);
                                        break;

                                    case 4504:
                                        IndicatorBase crithit = new CriticalHit(Session.Character.Level);
                                        Session.Character.Buff.Add(crithit);
                                        IndicatorBase pod = new PactofDarkness(Session.Character.Level);
                                        Session.Character.Buff.Add(pod);
                                        IndicatorBase sinistershadow = new SinisterShadow(Session.Character.Level);
                                        Session.Character.Buff.Add(sinistershadow);
                                        break;

                                    case 3615:
                                        IndicatorBase mh = new MiraclousHealing(Session.Character.Level);
                                        Session.Character.Buff.Add(mh);
                                        break;

                                    case 3607:
                                        IndicatorBase boost = new BoosterOn(Session.Character.Level);
                                        Session.Character.Buff.Add(boost);
                                        break;

                                    case 3706:
                                        IndicatorBase eaglespirit = new EagleSpirit(Session.Character.Level);
                                        Session.Character.Buff.Add(eaglespirit);
                                        break;

                                    case 4007:
                                        IndicatorBase manatransfusion = new ManaTransfusion(Session.Character.Level);
                                        Session.Character.Buff.Add(manatransfusion);
                                        break;

                                    case 4407:
                                        IndicatorBase manashield = new ManaShield(Session.Character.Level);
                                        Session.Character.Buff.Add(manashield);
                                        break;

                                    case 3811:
                                        IndicatorBase frozenshield = new FrozenShield(Session.Character.Level);
                                        Session.Character.Buff.Add(frozenshield);
                                        break;

                                    case 3906:
                                        IndicatorBase ghostguard = new GhostGuard(Session.Character.Level);
                                        Session.Character.Buff.Add(ghostguard);
                                        break;
                                }
                                break;

                            case 4:
                                switch (ski.Skill.Effect)
                                {
                                    case 281:
                                        IndicatorBase ritualofhawk = new RitualOfHawk(Session.Character.Level);
                                        Session.Character.Buff.Add(ritualofhawk);
                                        break;
                                }
                                break;
                        }
                    }
                    else if (ski.Skill.TargetType == 0 && Session.HasCurrentMapInstance) // monster target
                    {
                        if (isPvp)
                        {
                            ClientSession playerToAttack = ServerManager.Instance.GetSessionByCharacterId(targetId);
                            if (playerToAttack != null && Session.Character.Mp >= ski.Skill.MpCost)
                            {
                                if (Map.GetDistance(new MapCell { X = Session.Character.PositionX, Y = Session.Character.PositionY }, new MapCell { X = playerToAttack.Character.PositionX, Y = playerToAttack.Character.PositionY }) <= ski.Skill.Range + 1)
                                {
                                    Session.Character.LastSkillUse = DateTime.Now;
                                    ski.LastUse = DateTime.Now;
                                    if (!Session.Character.HasGodMode)
                                    {
                                        Session.Character.Mp -= ski.Skill.MpCost;
                                    }
                                    if (Session.Character.UseSp && ski.Skill.CastEffect != -1)
                                    {
                                        Session.SendPackets(Session.Character.GenerateQuicklist());
                                    }
                                    Session.SendPacket(Session.Character.GenerateStat());
                                    CharacterSkill characterSkillInfo = Session.Character.Skills.GetAllItems().OrderBy(o => o.SkillVNum)
                                        .FirstOrDefault(s => s.Skill.UpgradeSkill == ski.Skill.SkillVNum && s.Skill.Effect > 0 && s.Skill.SkillType == 2);

                                    Session.CurrentMapInstance?.Broadcast($"ct 1 {Session.Character.CharacterId} 3 {targetId} {ski.Skill.CastAnimation} {characterSkillInfo?.Skill.CastEffect ?? ski.Skill.CastEffect} {ski.Skill.SkillVNum}");
                                    Session.Character.Skills.GetAllItems().Where(s => s.Id != ski.Id).ToList().ForEach(i => i.Hit = 0);

                                    // Generate scp
                                    ski.LastUse = DateTime.Now;
                                    if ((DateTime.Now - ski.LastUse).TotalSeconds > 3)
                                    {
                                        ski.Hit = 0;
                                    }
                                    else
                                    {
                                        ski.Hit++;
                                    }

                                    if (ski.Skill.CastEffect != 0)
                                    {
                                        Thread.Sleep(ski.Skill.CastTime * 100);
                                    }

                                    // check if we will hit mutltiple targets
                                    if (ski.Skill.TargetRange != 0)
                                    {
                                        ComboDTO skillCombo = ski.Skill.Combos.FirstOrDefault(s => ski.Hit == s.Hit);
                                        if (skillCombo != null)
                                        {
                                            if (ski.Skill.Combos.OrderByDescending(s => s.Hit).First().Hit == ski.Hit)
                                            {
                                                ski.Hit = 0;
                                            }
                                            IEnumerable<ClientSession> playersInAOERange = ServerManager.Instance.Sessions.Where(s => s.CurrentMapInstance == Session.CurrentMapInstance && s.Character.CharacterId != Session.Character.CharacterId && s.Character.IsInRange(Session.Character.PositionX, Session.Character.PositionY, ski.Skill.TargetRange));
                                            foreach (ClientSession character in playersInAOERange)
                                            {
                                                if (Session.CurrentMapInstance != null && Session.CurrentMapInstance.Map.MapTypes.Any(s => s.MapTypeId == (short)MapTypeEnum.Act4))
                                                {
                                                    if (Session.Character.Family == null || character.Character.Family == null || Session.Character.Family.FamilyId != character.Character.Family.FamilyId)
                                                    {
                                                        if (Session.Character.MapInstance.Map.MapTypes.Any(m => m.MapTypeId == (short)MapTypeEnum.Citadel))
                                                        {
                                                            PVPHit(new HitRequest(TargetHitType.SingleTargetHitCombo, Session, ski.Skill, skillCombo: skillCombo), playerToAttack);
                                                        }
                                                    }
                                                }
                                                else if (Session.Character.MapInstance.Map.MapTypes.Any(m => m.MapTypeId == (short)MapTypeEnum.PVPMap))
                                                {
                                                    if (Session.Character.Group == null || !Session.Character.Group.IsMemberOfGroup(character.Character.CharacterId))
                                                    {
                                                        PVPHit(new HitRequest(TargetHitType.SingleTargetHitCombo, Session, ski.Skill, skillCombo: skillCombo), playerToAttack);
                                                    }
                                                }
                                                else if (Session.CurrentMapInstance != null && Session.CurrentMapInstance.IsPVP)
                                                {
                                                    if (Session.Character.Group == null || !Session.Character.Group.IsMemberOfGroup(character.Character.CharacterId))
                                                    {
                                                        PVPHit(new HitRequest(TargetHitType.SingleTargetHitCombo, Session, ski.Skill, skillCombo: skillCombo), playerToAttack);
                                                    }
                                                }
                                            }
                                            if (playerToAttack.Character.Hp <= 0)
                                            {
                                                Session.SendPacket($"cancel 2 {targetId}");
                                            }
                                        }
                                        else
                                        {
                                            IEnumerable<ClientSession> playersInAOERange = ServerManager.Instance.Sessions.Where(s => s.CurrentMapInstance == Session.CurrentMapInstance && s.Character.CharacterId != Session.Character.CharacterId && s.Character.IsInRange(Session.Character.PositionX, Session.Character.PositionY, ski.Skill.TargetRange));

                                            // hit the targetted monster

                                            if (Session.CurrentMapInstance != null && Session.CurrentMapInstance.Map.MapTypes.Any(s => s.MapTypeId == (short)MapTypeEnum.Act4))
                                            {
                                                if (Session.Character.Family == null || playerToAttack.Character.Family == null || Session.Character.Family.FamilyId != playerToAttack.Character.Family.FamilyId)
                                                {
                                                    if (Session.Character.MapInstance.Map.MapTypes.Any(m => m.MapTypeId == (short)MapTypeEnum.Citadel))
                                                    {
                                                        PVPHit(new HitRequest(TargetHitType.SingleAOETargetHit, Session, ski.Skill), playerToAttack);
                                                    }
                                                    else
                                                    {
                                                        Session.SendPacket($"cancel 2 {targetId}");
                                                    }
                                                }
                                                else
                                                {
                                                    Session.SendPacket($"cancel 2 {targetId}");
                                                }
                                            }
                                            else if (Session.Character.MapInstance.Map.MapTypes.Any(m => m.MapTypeId == (short)MapTypeEnum.PVPMap))
                                            {
                                                if (Session.Character.Group == null || !Session.Character.Group.IsMemberOfGroup(playerToAttack.Character.CharacterId))
                                                {
                                                    PVPHit(new HitRequest(TargetHitType.SingleAOETargetHit, Session, ski.Skill), playerToAttack);
                                                }
                                                else
                                                {
                                                    Session.SendPacket($"cancel 2 {targetId}");
                                                }
                                            }
                                            else if (Session.CurrentMapInstance != null && Session.CurrentMapInstance.IsPVP)
                                            {
                                                if (Session.Character.Group == null || !Session.Character.Group.IsMemberOfGroup(playerToAttack.Character.CharacterId))
                                                {
                                                    PVPHit(new HitRequest(TargetHitType.SingleAOETargetHit, Session, ski.Skill), playerToAttack);
                                                }
                                                else
                                                {
                                                    Session.SendPacket($"cancel 2 {targetId}");
                                                }
                                            }
                                            else
                                            {
                                                Session.SendPacket($"cancel 2 {targetId}");
                                            }

                                            //hit all other monsters
                                            foreach (ClientSession character in playersInAOERange)
                                            {
                                                if (Session.CurrentMapInstance != null && Session.CurrentMapInstance.Map.MapTypes.Any(s => s.MapTypeId == (short)MapTypeEnum.Act4))
                                                {
                                                    if (Session.Character.Family == null || character.Character.Family == null || Session.Character.Family.FamilyId != character.Character.Family.FamilyId)
                                                    {
                                                        if (Session.Character.MapInstance.Map.MapTypes.Any(m => m.MapTypeId == (short)MapTypeEnum.Citadel))
                                                        {
                                                            PVPHit(new HitRequest(TargetHitType.SingleAOETargetHit, Session, ski.Skill), character);
                                                        }
                                                    }
                                                }
                                                else if (Session.Character.MapInstance.Map.MapTypes.Any(m => m.MapTypeId == (short)MapTypeEnum.PVPMap))
                                                {
                                                    if (Session.Character.Group == null || !Session.Character.Group.IsMemberOfGroup(character.Character.CharacterId))
                                                    {
                                                        PVPHit(new HitRequest(TargetHitType.SingleAOETargetHit, Session, ski.Skill), character);
                                                    }
                                                }
                                                else if (Session.CurrentMapInstance.IsPVP)
                                                {
                                                    if (Session.Character.Group == null || !Session.Character.Group.IsMemberOfGroup(character.Character.CharacterId))
                                                    {
                                                        PVPHit(new HitRequest(TargetHitType.SingleAOETargetHit, Session, ski.Skill), character);
                                                    }
                                                }
                                            }
                                            if (playerToAttack.Character.Hp <= 0)
                                            {
                                                Session.SendPacket($"cancel 2 {targetId}");
                                            }
                                        }
                                    }
                                    else
                                    {
                                        ComboDTO skillCombo = ski.Skill.Combos.FirstOrDefault(s => ski.Hit == s.Hit);
                                        if (skillCombo != null)
                                        {
                                            if (ski.Skill.Combos.OrderByDescending(s => s.Hit).First().Hit == ski.Hit)
                                            {
                                                ski.Hit = 0;
                                            }
                                            if (Session.CurrentMapInstance != null && Session.CurrentMapInstance.Map.MapTypes.Any(s => s.MapTypeId == (short)MapTypeEnum.Act4))
                                            {
                                                if (Session.Character.Family == null || playerToAttack.Character.Family == null || Session.Character.Family.FamilyId != playerToAttack.Character.Family.FamilyId)
                                                {
                                                    if (Session.Character.MapInstance.Map.MapTypes.Any(m => m.MapTypeId == (short)MapTypeEnum.Citadel))
                                                    {
                                                        PVPHit(new HitRequest(TargetHitType.SingleTargetHitCombo, Session, ski.Skill, skillCombo: skillCombo), playerToAttack);
                                                    }
                                                    else
                                                    {
                                                        Session.SendPacket($"cancel 2 {targetId}");
                                                    }
                                                }
                                                else
                                                {
                                                    Session.SendPacket($"cancel 2 {targetId}");
                                                }
                                            }
                                            else if (Session.Character.MapInstance.Map.MapTypes.Any(m => m.MapTypeId == (short)MapTypeEnum.PVPMap))
                                            {
                                                if (Session.Character.Group == null || !Session.Character.Group.IsMemberOfGroup(playerToAttack.Character.CharacterId))
                                                {
                                                    PVPHit(new HitRequest(TargetHitType.SingleTargetHitCombo, Session, ski.Skill, skillCombo: skillCombo), playerToAttack);
                                                }
                                                else
                                                {
                                                    Session.SendPacket($"cancel 2 {targetId}");
                                                }
                                            }
                                            else if (Session.CurrentMapInstance != null && Session.CurrentMapInstance.IsPVP)
                                            {
                                                if (Session.CurrentMapInstance.MapInstanceId !=
                                                    ServerManager.Instance.FamilyArenaInstance.MapInstanceId)
                                                {
                                                    if (Session.Character.Group == null ||
                                                        !Session.Character.Group.IsMemberOfGroup(
                                                            playerToAttack.Character.CharacterId))
                                                    {
                                                        PVPHit(new HitRequest(TargetHitType.SingleTargetHit, Session,
                                                            ski.Skill), playerToAttack);
                                                    }
                                                    else
                                                    {
                                                        Session.SendPacket($"cancel 2 {targetId}");
                                                    }
                                                }
                                                else
                                                {
                                                    if (Session.Character.Family == null ||
                                                        Session.Character.Family.FamilyCharacters.Any(
                                                            s => s.Character.CharacterId != playerToAttack.Character
                                                                     .CharacterId))
                                                    {
                                                        PVPHit(
                                                            new HitRequest(TargetHitType.SingleTargetHit, Session,
                                                                ski.Skill), playerToAttack);
                                                    }
                                                    else
                                                    {
                                                        Session.SendPacket($"cancel 2 {targetId}");
                                                    }
                                                }
                                            }
                                            else
                                            {
                                                Session.SendPacket($"cancel 2 {targetId}");
                                            }
                                        }
                                        else
                                        {
                                            if (Session.CurrentMapInstance != null && Session.CurrentMapInstance.Map.MapTypes.Any(s => s.MapTypeId == (short)MapTypeEnum.Act4))
                                            {
                                                if (Session.Character.Family == null || playerToAttack.Character.Family == null || Session.Character.Family.FamilyId != playerToAttack.Character.Family.FamilyId)
                                                {
                                                    if (Session.Character.MapInstance.Map.MapTypes.Any(m => m.MapTypeId == (short)MapTypeEnum.Citadel))
                                                    {
                                                        PVPHit(new HitRequest(TargetHitType.SingleTargetHit, Session, ski.Skill), playerToAttack);
                                                    }
                                                    else
                                                    {
                                                        Session.SendPacket($"cancel 2 {targetId}");
                                                    }
                                                }
                                                else
                                                {
                                                    Session.SendPacket($"cancel 2 {targetId}");
                                                }
                                            }
                                            else if (Session.Character.MapInstance.Map.MapTypes.Any(m => m.MapTypeId == (short)MapTypeEnum.PVPMap))
                                            {
                                                if (Session.Character.Group == null || !Session.Character.Group.IsMemberOfGroup(playerToAttack.Character.CharacterId))
                                                {
                                                    PVPHit(new HitRequest(TargetHitType.SingleTargetHit, Session, ski.Skill), playerToAttack);
                                                }
                                                else
                                                {
                                                    Session.SendPacket($"cancel 2 {targetId}");
                                                }
                                            }
                                            else if (Session.CurrentMapInstance != null && Session.CurrentMapInstance.IsPVP)
                                            {
                                                if (Session.Character.Group == null || !Session.Character.Group.IsMemberOfGroup(playerToAttack.Character.CharacterId))
                                                {
                                                    PVPHit(new HitRequest(TargetHitType.SingleTargetHit, Session, ski.Skill), playerToAttack);
                                                }
                                                else
                                                {
                                                    Session.SendPacket($"cancel 2 {targetId}");
                                                }
                                            }
                                            else
                                            {
                                                Session.SendPacket($"cancel 2 {targetId}");
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    Session.SendPacket($"cancel 2 {targetId}");
                                }
                            }
                            else
                            {
                                Session.SendPacket($"cancel 2 {targetId}");
                            }
                        }
                        else
                        {
                            MapMonster monsterToAttack = Session.CurrentMapInstance.GetMonster(targetId);
                            if (monsterToAttack != null && Session.Character.Mp >= ski.Skill.MpCost)
                            {
                                if (Map.GetDistance(new MapCell { X = Session.Character.PositionX, Y = Session.Character.PositionY },
                                                    new MapCell { X = monsterToAttack.MapX, Y = monsterToAttack.MapY }) <= ski.Skill.Range + 1 + monsterToAttack.Monster.BasicArea)
                                {
                                    Session.Character.LastSkillUse = DateTime.Now;
                                    ski.LastUse = DateTime.Now;
                                    if (!Session.Character.HasGodMode)
                                    {
                                        Session.Character.Mp -= ski.Skill.MpCost;
                                    }
                                    if (Session.Character.UseSp && ski.Skill.CastEffect != -1)
                                    {
                                        Session.SendPackets(Session.Character.GenerateQuicklist());
                                    }
                                    Session.SendPacket(Session.Character.GenerateStat());
                                    CharacterSkill characterSkillInfo = Session.Character.Skills.GetAllItems().OrderBy(o => o.SkillVNum)
                                        .FirstOrDefault(s => s.Skill.UpgradeSkill == ski.Skill.SkillVNum && s.Skill.Effect > 0 && s.Skill.SkillType == 2);

                                    Session.CurrentMapInstance?.Broadcast($"ct 1 {Session.Character.CharacterId} 3 {monsterToAttack.MapMonsterId} {ski.Skill.CastAnimation} {characterSkillInfo?.Skill.CastEffect ?? ski.Skill.CastEffect} {ski.Skill.SkillVNum}");
                                    Session.Character.Skills.GetAllItems().Where(s => s.Id != ski.Id).ToList().ForEach(i => i.Hit = 0);

                                    // Generate scp
                                    ski.LastUse = DateTime.Now;
                                    if ((DateTime.Now - ski.LastUse).TotalSeconds > 3)
                                    {
                                        ski.Hit = 0;
                                    }
                                    else
                                    {
                                        ski.Hit++;
                                    }

                                    if (ski.Skill.CastEffect != 0)
                                    {
                                        Thread.Sleep(ski.Skill.CastTime * 100);
                                    }

                                    // check if we will hit mutltiple targets
                                    if (ski.Skill.TargetRange != 0)
                                    {
                                        ComboDTO skillCombo = ski.Skill.Combos.FirstOrDefault(s => ski.Hit == s.Hit);
                                        if (skillCombo != null)
                                        {
                                            if (ski.Skill.Combos.OrderByDescending(s => s.Hit).First().Hit == ski.Hit)
                                            {
                                                ski.Hit = 0;
                                            }
                                            IEnumerable<MapMonster> monstersInAOERange = Session.CurrentMapInstance?.GetListMonsterInRange(monsterToAttack.MapX, monsterToAttack.MapY, ski.Skill.TargetRange).ToList();
                                            if (monstersInAOERange != null)
                                            {
                                                foreach (MapMonster mon in monstersInAOERange)
                                                {
                                                    mon.HitQueue.Enqueue(new HitRequest(TargetHitType.SingleTargetHitCombo, Session, ski.Skill
                                                        , skillCombo: skillCombo));
                                                }
                                            }
                                            else
                                            {
                                                Session.SendPacket($"cancel 2 {targetId}");
                                            }
                                            if (!monsterToAttack.IsAlive)
                                            {
                                                Session.SendPacket($"cancel 2 {targetId}");
                                            }
                                        }
                                        else
                                        {
                                            IEnumerable<MapMonster> monstersInAOERange = Session.CurrentMapInstance?.GetListMonsterInRange(monsterToAttack.MapX, monsterToAttack.MapY, ski.Skill.TargetRange).ToList();

                                            //hit the targetted monster
                                            monsterToAttack.HitQueue.Enqueue(new HitRequest(TargetHitType.SingleAOETargetHit, Session, ski.Skill
                                                        , characterSkillInfo?.Skill.Effect ?? ski.Skill.Effect, showTargetAnimation: true));

                                            //hit all other monsters
                                            if (monstersInAOERange != null)
                                            {
                                                foreach (MapMonster mon in monstersInAOERange.Where(m => m.MapMonsterId != monsterToAttack.MapMonsterId)) //exclude targetted monster
                                                {
                                                    mon.HitQueue.Enqueue(new HitRequest(TargetHitType.SingleAOETargetHit, Session, ski.Skill
                                                        , characterSkillInfo?.Skill.Effect ?? ski.Skill.Effect));
                                                }
                                            }
                                            else
                                            {
                                                Session.SendPacket($"cancel 2 {targetId}");
                                            }
                                            if (!monsterToAttack.IsAlive)
                                            {
                                                Session.SendPacket($"cancel 2 {targetId}");
                                            }
                                        }
                                    }
                                    else
                                    {
                                        ComboDTO skillCombo = ski.Skill.Combos.FirstOrDefault(s => ski.Hit == s.Hit);
                                        if (skillCombo != null)
                                        {
                                            if (ski.Skill.Combos.OrderByDescending(s => s.Hit).First().Hit == ski.Hit)
                                            {
                                                ski.Hit = 0;
                                            }
                                            monsterToAttack.HitQueue.Enqueue(new HitRequest(TargetHitType.SingleTargetHitCombo, Session, ski.Skill, skillCombo: skillCombo));
                                        }
                                        else
                                        {
                                            monsterToAttack.HitQueue.Enqueue(new HitRequest(TargetHitType.SingleTargetHit, Session, ski.Skill));
                                        }
                                    }
                                }
                                else
                                {
                                    Session.SendPacket($"cancel 2 {targetId}");
                                }
                            }
                            else
                            {
                                Session.SendPacket($"cancel 2 {targetId}");
                            }
                        }
                    }
                    else
                    {
                        Session.SendPacket($"cancel 2 {targetId}");
                    }
                    Session.SendPacketAfterWait($"sr {castingId}", ski.Skill.Cooldown * 100);
                }
                else
                {
                    Session.SendPacket($"cancel 2 {targetId}");
                    Session.SendPacket(Session.Character.GenerateSay(Language.Instance.GetMessageFromKey("NOT_ENOUGH_MP"), 10));
                }
            }
            else
            {
                Session.SendPacket($"cancel 2 {targetId}");
            }
        }

        private void ZoneHit(int Castingid, short x, short y)
        {
            List<CharacterSkill> skills = Session.Character.UseSp ? Session.Character.SkillsSp.GetAllItems() : Session.Character.Skills.GetAllItems();
            CharacterSkill characterSkill = skills.FirstOrDefault(s => s.Skill.CastId == Castingid);
            if (!Session.Character.WeaponLoaded(characterSkill) || !Session.HasCurrentMapInstance)
            {
                Session.SendPacket("cancel 2 0");
                return;
            }

            if (characterSkill != null && characterSkill.CanBeUsed())
            {
                if (Session.Character.Mp >= characterSkill.Skill.MpCost)
                {
                    Session.CurrentMapInstance?.Broadcast($"ct_n 1 {Session.Character.CharacterId} 3 -1 {characterSkill.Skill.CastAnimation} {characterSkill.Skill.CastEffect} {characterSkill.Skill.SkillVNum}");
                    characterSkill.LastUse = DateTime.Now;
                    if (!Session.Character.HasGodMode)
                    {
                        Session.Character.Mp -= characterSkill.Skill.MpCost;
                    }
                    Session.SendPacket(Session.Character.GenerateStat());
                    characterSkill.LastUse = DateTime.Now;
                    Observable.Timer(TimeSpan.FromMilliseconds(characterSkill.Skill.CastTime * 100))
                    .Subscribe(
                    o =>
                    {
                        Session.Character.LastSkillUse = DateTime.Now;

                        Session.CurrentMapInstance?.Broadcast($"bs 1 {Session.Character.CharacterId} {x} {y} {characterSkill.Skill.SkillVNum} {characterSkill.Skill.Cooldown} {characterSkill.Skill.AttackAnimation} {characterSkill.Skill.Effect} 0 0 1 1 0 0 0");

                        IEnumerable<MapMonster> monstersInRange = Session.CurrentMapInstance?.GetListMonsterInRange(x, y, characterSkill.Skill.TargetRange).ToList();
                        if (monstersInRange != null)
                        {
                            foreach (MapMonster mon in monstersInRange.Where(s => s.CurrentHp > 0))
                            {
                                mon.HitQueue.Enqueue(new HitRequest(TargetHitType.ZoneHit, Session, characterSkill.Skill, x, y));
                            }
                        }
                        foreach (ClientSession character in ServerManager.Instance.Sessions.Where(s => s.CurrentMapInstance == Session.CurrentMapInstance && s.Character.CharacterId != Session.Character.CharacterId && s.Character.IsInRange(x, y, characterSkill.Skill.TargetRange)))
                        {
                            if (Session.CurrentMapInstance != null && Session.CurrentMapInstance.Map.MapTypes.Any(s => s.MapTypeId == (short)MapTypeEnum.Act4))
                            {
                                if (Session.Character.Family == null || character.Character.Family == null || Session.Character.Family.FamilyId != character.Character.Family.FamilyId)
                                {
                                    if (Session.Character.MapInstance.Map.MapTypes.Any(m => m.MapTypeId == (short)MapTypeEnum.Citadel))
                                    {
                                        PVPHit(new HitRequest(TargetHitType.ZoneHit, Session, characterSkill.Skill, x, y), character);
                                    }
                                }
                            }
                            else if (Session.Character.MapInstance.Map.MapTypes.Any(m => m.MapTypeId == (short)MapTypeEnum.PVPMap))
                            {
                                if (Session.Character.Group == null || !Session.Character.Group.IsMemberOfGroup(character.Character.CharacterId))
                                {
                                    PVPHit(new HitRequest(TargetHitType.ZoneHit, Session, characterSkill.Skill, x, y), character);
                                }
                            }
                            else if (Session.CurrentMapInstance != null && Session.CurrentMapInstance.IsPVP)
                            {
                                if (Session.Character.Group == null || !Session.Character.Group.IsMemberOfGroup(character.Character.CharacterId))
                                {
                                    PVPHit(new HitRequest(TargetHitType.ZoneHit, Session, characterSkill.Skill, x, y), character);
                                }
                            }
                        }
                    });

                    Observable.Timer(TimeSpan.FromMilliseconds(characterSkill.Skill.Cooldown * 100))
                    .Subscribe(
                    o =>
                    {
                        Session.SendPacket($"sr {Castingid}");
                    });
                }
                else
                {
                    Session.SendPacket(Session.Character.GenerateSay(Language.Instance.GetMessageFromKey("NOT_ENOUGH_MP"), 10));
                    Session.SendPacket("cancel 2 0");
                }
            }
            else
            {
                Session.SendPacket("cancel 2 0");
            }
        }

        #endregion
    }
}