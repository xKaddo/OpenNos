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
using OpenNos.DAL;
using OpenNos.Data;
using OpenNos.Domain;
using OpenNos.GameObject.Helpers;
using OpenNos.WebApi.Reference;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Reactive.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace OpenNos.GameObject
{
    public class ServerManager : BroadcastableBase
    {
        #region Members

        public bool ShutdownStop;

        private static readonly List<Item> _items = new List<Item>();
        private static readonly ConcurrentDictionary<Guid, MapInstance> _mapinstances = new ConcurrentDictionary<Guid, MapInstance>();

        private static readonly List<Map> _maps = new List<Map>();

        private static readonly List<NpcMonster> _npcs = new List<NpcMonster>();

        private static readonly List<Skill> _skills = new List<Skill>();

        private static readonly ThreadLocal<Random> random = new ThreadLocal<Random>(() => new Random(Interlocked.Increment(ref seed)));

        private static ServerManager _instance;

        private static int seed = Environment.TickCount;

        private bool _disposed;

        private List<DropDTO> _generalDrops;

        private ThreadSafeSortedList<long, Group> _groups;

        private long _lastGroupId;

        private long _lastRaidId;

        private ThreadSafeSortedList<short, List<MapNpc>> _mapNpcs;

        private ThreadSafeSortedList<short, List<DropDTO>> _monsterDrops;

        private ThreadSafeSortedList<short, List<NpcMonsterSkill>> _monsterSkills;

        private ThreadSafeSortedList<long, Raid> _raids;

        private ThreadSafeSortedList<int, List<Recipe>> _recipes;

        private ThreadSafeSortedList<int, List<ShopItemDTO>> _shopItems;

        private ThreadSafeSortedList<int, Shop> _shops;

        private ThreadSafeSortedList<int, List<ShopSkillDTO>> _shopSkills;

        private ThreadSafeSortedList<int, List<TeleporterDTO>> _teleporters;

        private bool inRelationRefreshMode;

        #endregion

        #region Instantiation

        private ServerManager()
        {
            // do nothing
        }

        #endregion

        #region Properties

        public static ServerManager Instance => _instance ?? (_instance = new ServerManager());

        public MapInstance ArenaInstance { get; private set; }

        public List<BazaarItemLink> BazaarList { get; set; }

        public int ChannelId { get; set; }

        public List<CharacterRelationDTO> CharacterRelations { get; set; }

        public int DropRate { get; set; }

        public bool EventInWaiting { get; set; }

        public int FairyXpRate { get; set; }

        public MapInstance FamilyArenaInstance { get; private set; }

        public List<Family> FamilyList { get; set; }

        public int GoldDropRate { get; set; }

        public int GoldRate { get; set; }

        public List<Group> Groups => _groups.GetAllItems();

        public int HeroicStartLevel { get; set; }

        public int HeroXpRate { get; set; }

        public bool inBazaarRefreshMode { get; set; }

        public bool inFamilyRefreshMode { get; set; }

        public List<MailDTO> Mails { get; private set; }

        public List<int> MateIds { get; internal set; } = new List<int>();

        public long MaxGold { get; set; }

        public byte MaxHeroLevel { get; set; }

        public byte MaxJobLevel { get; set; }

        public byte MaxLevel { get; set; }

        public byte MaxSPLevel { get; set; }

        public List<PenaltyLogDTO> PenaltyLogs { get; set; }

        public List<Raid> Raids => _raids.GetAllItems();

        public List<Schedule> Schedules { get; set; }

        public string ServerGroup { get; set; }

        public List<EventType> StartedEvents { get; set; }

        public Task TaskShutdown { get; set; }

        public List<CharacterDTO> TopComplimented { get; set; }

        public List<CharacterDTO> TopPoints { get; set; }

        public List<CharacterDTO> TopReputation { get; set; }

        public Guid WorldId { get; private set; }

        public int XPRate { get; set; }

        #endregion

        #region Methods

        public void AddGroup(Group group)
        {
            _groups[group.GroupId] = group;
        }

        public void AddRaid(Raid raid)
        {
            _raids[raid.RaidId] = raid;
        }

        public void AskPVPRevive(long characterId)
        {
            ClientSession Session = GetSessionByCharacterId(characterId);
            if (Session != null && Session.HasSelectedCharacter)
            {
                if (Session.Character.IsVehicled)
                {
                    Session.Character.RemoveVehicle();
                }
                Session.Character.Buff.Clear();
                Session.SendPacket(Session.Character.GenerateStat());
                Session.SendPacket(Session.Character.GenerateCond());
                Session.SendPackets(UserInterfaceHelper.Instance.GenerateVb());

                Session.SendPacket("eff_ob -1 -1 0 4269");
                Session.SendPacket(UserInterfaceHelper.Instance.GenerateDialog($"#revival^2 #revival^1 {Language.Instance.GetMessageFromKey("ASK_REVIVE_PVP")}"));
                Task.Factory.StartNew(async () =>
                {
                    bool revive = true;
                    for (int i = 1; i <= 30; i++)
                    {
                        await Task.Delay(1000);
                        if (Session.Character.Hp > 0)
                        {
                            revive = false;
                            break;
                        }
                    }
                    if (revive)
                    {
                        Instance.ReviveFirstPosition(Session.Character.CharacterId);
                    }
                });
            }
        }

        // PacketHandler -> with Callback?
        public void AskRevive(long characterId)
        {
            ClientSession Session = GetSessionByCharacterId(characterId);
            if (Session != null && Session.HasSelectedCharacter)
            {
                if (Session.Character.IsVehicled)
                {
                    Session.Character.RemoveVehicle();
                }
                Session.Character.Buff.Clear();
                Session.SendPacket(Session.Character.GenerateStat());
                Session.SendPacket(Session.Character.GenerateCond());
                Session.SendPackets(UserInterfaceHelper.Instance.GenerateVb());
                switch (Session.CurrentMapInstance.MapInstanceType)
                {
                    case MapInstanceType.BaseMapInstance:
                        if (Session.Character.Level > 20)
                        {
                            Session.Character.Dignity -= (short)(Session.Character.Level < 50 ? Session.Character.Level : 50);
                            if (Session.Character.Dignity < -1000)
                            {
                                Session.Character.Dignity = -1000;
                            }
                            Session.SendPacket(Session.Character.GenerateSay(string.Format(Language.Instance.GetMessageFromKey("LOSE_DIGNITY"), (short)(Session.Character.Level < 50 ? Session.Character.Level : 50)), 11));
                            Session.SendPacket(Session.Character.GenerateFd());
                            Session.CurrentMapInstance?.Broadcast(Session, Session.Character.GenerateIn(), ReceiverType.AllExceptMe);
                            Session.CurrentMapInstance?.Broadcast(Session, Session.Character.GenerateGidx(), ReceiverType.AllExceptMe);
                        }
                        Session.SendPacket("eff_ob -1 -1 0 4269");

                        Session.SendPacket(UserInterfaceHelper.Instance.GenerateDialog($"#revival^0 #revival^1 {(Session.Character.Level > 20 ? Language.Instance.GetMessageFromKey("ASK_REVIVE") : Language.Instance.GetMessageFromKey("ASK_REVIVE_FREE"))}"));
                        Task.Factory.StartNew(async () =>
                        {
                            bool revive = true;
                            for (int i = 1; i <= 30; i++)
                            {
                                await Task.Delay(1000);
                                if (Session.Character.Hp > 0)
                                {
                                    revive = false;
                                    break;
                                }
                            }
                            if (revive)
                            {
                                Instance.ReviveFirstPosition(Session.Character.CharacterId);
                            }
                        });
                        break;

                    case MapInstanceType.TimeSpaceInstance:
                        if (!(Session.CurrentMapInstance.InstanceBag.Lives - Session.CurrentMapInstance.InstanceBag.DeadList.Count() <= 1))
                        {
                            Session.Character.Hp = 1;
                            Session.Character.Mp = 1;
                            return;
                        }
                        Session.SendPacket(UserInterfaceHelper.Instance.GenerateMsg(string.Format(Language.Instance.GetMessageFromKey("YOU_HAVE_LIFE"), Session.CurrentMapInstance.InstanceBag.Lives - Session.CurrentMapInstance.InstanceBag.DeadList.Count() + 1), 0));
                        Session.SendPacket(UserInterfaceHelper.Instance.GenerateDialog($"#revival^1 #revival^1 {(Session.Character.Level > 10 ? Language.Instance.GetMessageFromKey("ASK_REVIVE_TS_LOW_LEVEL") : Language.Instance.GetMessageFromKey("ASK_REVIVE_TS"))}"));
                        Session.CurrentMapInstance.InstanceBag.DeadList.Add(Session.Character.CharacterId);
                        Task.Factory.StartNew(async () =>
                        {
                            bool revive = true;
                            for (int i = 1; i <= 30; i++)
                            {
                                await Task.Delay(1000);
                                if (Session.Character.Hp > 0)
                                {
                                    revive = false;
                                    break;
                                }
                            }
                            if (revive)
                            {
                                Instance.ReviveFirstPosition(Session.Character.CharacterId);
                            }
                        });

                        break;

                    case MapInstanceType.LodInstance:
                        Session.SendPacket(UserInterfaceHelper.Instance.GenerateDialog($"#revival^0 #revival^1 {Language.Instance.GetMessageFromKey("ASK_REVIVE_LOD")}"));
                        Task.Factory.StartNew(async () =>
                        {
                            bool revive = true;
                            for (int i = 1; i <= 30; i++)
                            {
                                await Task.Delay(1000);
                                if (Session.Character.Hp > 0)
                                {
                                    revive = false;
                                    break;
                                }
                            }
                            if (revive)
                            {
                                Instance.ReviveFirstPosition(Session.Character.CharacterId);
                            }
                        });
                        break;

                    default:
                        Instance.ReviveFirstPosition(Session.Character.CharacterId);
                        break;
                }
            }
        }

        public void BazaarRefresh(long BazaarItemId)
        {
            inBazaarRefreshMode = true;
            ServerCommunicationClient.Instance.HubProxy.Invoke("BazaarRefresh", ServerGroup, BazaarItemId);
            SpinWait.SpinUntil(() => !inBazaarRefreshMode);
        }

        public void ChangeMap(long id, short? MapId = null, short? mapX = null, short? mapY = null)
        {
            ClientSession session = GetSessionByCharacterId(id);
            if (session?.Character != null)
            {
                if (MapId != null)
                {
                    session.Character.MapInstanceId = GetBaseMapInstanceIdByMapId((short)MapId);
                }
                ChangeMapInstance(id, session.Character.MapInstanceId, mapX, mapY);
            }
        }

        public void ChangeMapInstance(long characterId, Guid mapInstanceId, object startX, object startY)
        {
            throw new NotImplementedException();
        }

        // Both partly
        public void ChangeMapInstance(long id, Guid MapInstanceId, int? mapX = null, int? mapY = null)
        {
            ClientSession session = GetSessionByCharacterId(id);
            if (session?.Character != null && !session.Character.IsChangingMapInstance)
            {
                try
                {
                    LeaveMap(session.Character.CharacterId);

                    session.Character.IsChangingMapInstance = true;

                    session.CurrentMapInstance.RemoveMonstersTarget(session.Character.CharacterId);
                    session.CurrentMapInstance.UnregisterSession(session.Character.CharacterId);

                    // cleanup sending queue to avoid sending uneccessary packets to it
                    session.ClearLowPriorityQueue();

                    session.Character.MapInstanceId = MapInstanceId;
                    if (session.Character.MapInstance.MapInstanceType == MapInstanceType.BaseMapInstance)
                    {
                        session.Character.MapId = session.Character.MapInstance.Map.MapId;
                        if (mapX != null && mapY != null)
                        {
                            session.Character.MapX = (short)mapX;
                            session.Character.MapY = (short)mapY;
                        }
                    }
                    if (mapX != null && mapY != null)
                    {
                        session.Character.PositionX = (short)mapX;
                        session.Character.PositionY = (short)mapY;
                    }

                    session.CurrentMapInstance = session.Character.MapInstance;
                    session.CurrentMapInstance.RegisterSession(session);

                    session.SendPacket(session.Character.GenerateCInfo());
                    session.SendPacket(session.Character.GenerateCMode());
                    session.SendPacket(session.Character.GenerateEq());
                    session.SendPacket(session.Character.GenerateEquipment());
                    session.SendPacket(session.Character.GenerateLev());
                    session.SendPacket(session.Character.GenerateStat());
                    session.SendPacket(session.Character.GenerateAt());
                    session.SendPacket(session.Character.GenerateCond());
                    session.SendPacket(session.Character.GenerateCMap());
                    session.SendPacket(session.Character.GenerateStatChar());

                    session.SendPacket(session.Character.GeneratePairy());
                    session.Character.Mates.Where(s => s.IsTeamMember).ToList().ForEach(s =>
                    {
                        s.PositionX = (short)(session.Character.PositionX + (s.MateType == MateType.Partner ? -1 : 1));
                        s.PositionY = (short)(session.Character.PositionY + 1);
                    });

                    session.SendPacket(session.Character.GeneratePinit()); // clear party list
                    session.Character.SendPst();
                    session.SendPacket("act6"); // act6 1 0 14 0 0 0 14 0 0 0
                    session.SendPacket(session.Character.GenerateScpStc());

                    session.CurrentMapInstance.Sessions.Where(s => s.Character != null && !s.Character.InvisibleGm).ToList().ForEach(s =>
                    {
                        session.SendPacket(s.Character.GenerateIn());
                        session.SendPacket(s.Character.GenerateGidx());
                    });

                    session.SendPackets(session.CurrentMapInstance.GetMapItems());

                    MapInstancePortalHandler.GenerateMinilandEntryPortals(session.CurrentMapInstance.Map.MapId, session.Character.Miniland.MapInstanceId).ForEach(p => session.SendPacket(p.GenerateGp()));

                    if (session.CurrentMapInstance.InstanceBag.Clock.Enabled)
                    {
                        session.SendPacket(session.CurrentMapInstance.InstanceBag.Clock.GetClock());
                    }
                    if (session.CurrentMapInstance.Clock.Enabled)
                    {
                        session.SendPacket(session.CurrentMapInstance.InstanceBag.Clock.GetClock());
                    }

                    // TODO: fix this
                    if (session.Character.MapInstance.Map.MapTypes.Any(m => m.MapTypeId == (short)MapTypeEnum.CleftOfDarkness))
                    {
                        session.SendPacket("bc 0 0 0");
                    }
                    if (!session.Character.InvisibleGm)
                    {
                        session.CurrentMapInstance?.Broadcast(session, session.Character.GenerateIn(), ReceiverType.AllExceptMe);
                        session.CurrentMapInstance?.Broadcast(session, session.Character.GenerateGidx(), ReceiverType.AllExceptMe);
                    }
                    if (session.Character.Size != 10)
                    {
                        session.SendPacket(session.Character.GenerateScal());
                    }
                    if (session.CurrentMapInstance != null && session.CurrentMapInstance.IsDancing && !session.Character.IsDancing)
                    {
                        session.CurrentMapInstance?.Broadcast("dance 2");
                    }
                    else if (session.CurrentMapInstance != null && !session.CurrentMapInstance.IsDancing && session.Character.IsDancing)
                    {
                        session.Character.IsDancing = false;
                        session.CurrentMapInstance?.Broadcast("dance");
                    }
                    if (Groups != null)
                    {
                        foreach (Group g in Groups)
                        {
                            foreach (ClientSession groupSession in g.Characters)
                            {
                                ClientSession chara = Sessions.FirstOrDefault(s => s.Character != null && s.Character.CharacterId == groupSession.Character.CharacterId && s.CurrentMapInstance == groupSession.CurrentMapInstance);
                                if (chara == null) continue;
                                groupSession.SendPacket(groupSession.Character.GeneratePinit());
                                groupSession.Character.SendPst();
                            }
                        }
                    }

                    if (session.Character.Group != null)
                    {
                        session.CurrentMapInstance?.Broadcast(session, session.Character.GeneratePidx(), ReceiverType.AllExceptMe);
                    }
                    session.Character.IsChangingMapInstance = false;
                    session.SendPacket(session.Character.GenerateMinimapPosition());
                    session.CurrentMapInstance.OnCharacterDiscoveringMapEvents.ForEach(
                         e =>
                         {
                             if (!e.Item2.Contains(session.Character.CharacterId))
                             {
                                 e.Item2.Add(session.Character.CharacterId);
                                 EventHelper.Instance.RunEvent(e.Item1, session);
                             }
                         }
                     );
                }
                catch (Exception)
                {
                    Logger.Log.Warn("Character changed while changing map. Do not abuse Commands.");
                    session.Character.IsChangingMapInstance = false;
                }
            }
        }

        public override void Dispose()
        {
            if (!_disposed)
            {
                Dispose(true);
                GC.SuppressFinalize(this);
                _disposed = true;
            }
        }

        public void FamilyRefresh(long FamilyId)
        {
            inFamilyRefreshMode = true;
            ServerCommunicationClient.Instance.HubProxy.Invoke("FamilyRefresh", ServerGroup, FamilyId);
            SpinWait.SpinUntil(() => !inFamilyRefreshMode);
        }

        public MapInstance GenerateMapInstance(short MapId, MapInstanceType type, InstanceBag mapclock)
        {
            Map map = _maps.FirstOrDefault(m => m.MapId.Equals(MapId));
            if (map != null)
            {
                Guid guid = Guid.NewGuid();
                MapInstance mapInstance = new MapInstance(map, guid, false, type, mapclock);
                mapInstance.LoadMonsters();
                mapInstance.LoadNpcs();
                mapInstance.LoadPortals();
                foreach (MapMonster mapMonster in mapInstance.Monsters)
                {
                    mapMonster.MapInstance = mapInstance;
                    mapInstance.AddMonster(mapMonster);
                }
                foreach (MapNpc mapNpc in mapInstance.Npcs)
                {
                    mapNpc.MapInstance = mapInstance;
                    mapInstance.AddNPC(mapNpc);
                }
                _mapinstances.TryAdd(guid, mapInstance);
                return mapInstance;
            }
            return null;
        }

        public IEnumerable<Skill> GetAllSkill()
        {
            return _skills;
        }

        public Guid GetBaseMapInstanceIdByMapId(short MapId)
        {
            return _mapinstances.FirstOrDefault(s => s.Value?.Map.MapId == MapId && s.Value.MapInstanceType == MapInstanceType.BaseMapInstance).Key;
        }

        public List<DropDTO> GetDropsByMonsterVNum(short monsterVNum)
        {
            return _monsterDrops.ContainsKey(monsterVNum) ? _generalDrops.Concat(_monsterDrops[monsterVNum]).ToList() : new List<DropDTO>();
        }

        public Group GetGroupByCharacterId(long characterId)
        {
            return Groups?.SingleOrDefault(g => g.IsMemberOfGroup(characterId));
        }

        public Item GetItem(short vnum)
        {
            return _items.FirstOrDefault(m => m.VNum.Equals(vnum));
        }

        public MapInstance GetMapInstance(Guid id)
        {
            return _mapinstances.ContainsKey(id) ? _mapinstances[id] : null;
        }

        public long GetNextGroupId()
        {
            _lastGroupId++;
            return _lastGroupId;
        }

        public long GetNextRaidId()
        {
            _lastRaidId++;
            return _lastRaidId;
        }

        public NpcMonster GetNpc(short npcVNum)
        {
            return _npcs.FirstOrDefault(m => m.NpcMonsterVNum.Equals(npcVNum));
        }

        public T GetProperty<T>(string charName, string property)
        {
            ClientSession session = Sessions.FirstOrDefault(s => s.Character != null && s.Character.Name.Equals(charName));
            if (session == null)
            {
                return default(T);
            }
            return (T)session.Character.GetType().GetProperties().Single(pi => pi.Name == property).GetValue(session.Character, null);
        }

        public T GetProperty<T>(long charId, string property)
        {
            ClientSession session = GetSessionByCharacterId(charId);
            if (session == null)
            {
                return default(T);
            }
            return (T)session.Character.GetType().GetProperties().Single(pi => pi.Name == property).GetValue(session.Character, null);
        }

        public List<Recipe> GetReceipesByMapNpcId(int mapNpcId)
        {
            return _recipes.ContainsKey(mapNpcId) ? _recipes[mapNpcId] : new List<Recipe>();
        }

        public ClientSession GetSessionByCharacterName(string name)
        {
            return Sessions.SingleOrDefault(s => s.Character.Name == name);
        }

        public Skill GetSkill(short skillVNum)
        {
            return _skills.FirstOrDefault(m => m.SkillVNum.Equals(skillVNum));
        }

        public T GetUserMethod<T>(long characterId, string methodName)
        {
            ClientSession session = GetSessionByCharacterId(characterId);
            if (session == null)
            {
                return default(T);
            }
            MethodInfo method = session.Character.GetType().GetMethod(methodName);

            return (T)method.Invoke(session.Character, null);
        }

        public void GroupLeave(ClientSession session)
        {
            if (Groups != null)
            {
                Group grp = Instance.Groups.FirstOrDefault(s => s.IsMemberOfGroup(session.Character.CharacterId));
                if (grp != null)
                {
                    if (grp.CharacterCount >= 3)
                    {
                        if (grp.Characters.ElementAt(0) == session)
                        {
                            Broadcast(session, UserInterfaceHelper.Instance.GenerateInfo(Language.Instance.GetMessageFromKey("NEW_LEADER")), ReceiverType.OnlySomeone, string.Empty, grp.Characters.ElementAt(1).Character.CharacterId);
                        }
                        grp.LeaveGroup(session);
                        foreach (ClientSession groupSession in grp.Characters)
                        {
                            groupSession.SendPacket(groupSession.Character.GeneratePinit());
                            groupSession.Character.SendPst();
                            groupSession.SendPacket(UserInterfaceHelper.Instance.GenerateMsg(string.Format(Language.Instance.GetMessageFromKey("LEAVE_GROUP"), session.Character.Name), 0));
                        }
                        session.SendPacket(session.Character.GeneratePinit());
                        session.Character.SendPst();
                        Broadcast(session.Character.GeneratePidx(true));
                        session.SendPacket(UserInterfaceHelper.Instance.GenerateMsg(Language.Instance.GetMessageFromKey("GROUP_LEFT"), 0));
                    }
                    else
                    {
                        ClientSession[] grpmembers = new ClientSession[3];
                        grp.Characters.CopyTo(grpmembers);
                        foreach (ClientSession targetSession in grpmembers)
                        {
                            if (targetSession != null)
                            {
                                targetSession.SendPacket(UserInterfaceHelper.Instance.GenerateMsg(Language.Instance.GetMessageFromKey("GROUP_CLOSED"), 0));
                                Broadcast(targetSession.Character.GeneratePidx(true));
                                grp.LeaveGroup(targetSession);
                                targetSession.SendPacket(targetSession.Character.GeneratePinit());
                                targetSession.Character.SendPst();
                            }
                        }
                        RemoveGroup(grp);
                    }
                    session.Character.Group = null;
                }
            }
        }

        public void Initialize()
        {
            // parse rates
            XPRate = int.Parse(ConfigurationManager.AppSettings["RateXp"]);
            HeroXpRate = int.Parse(ConfigurationManager.AppSettings["RateHeroicXp"]);
            DropRate = int.Parse(ConfigurationManager.AppSettings["RateDrop"]);
            MaxGold = long.Parse(ConfigurationManager.AppSettings["MaxGold"]);
            GoldDropRate = int.Parse(ConfigurationManager.AppSettings["GoldRateDrop"]);
            GoldRate = int.Parse(ConfigurationManager.AppSettings["RateGold"]);
            FairyXpRate = int.Parse(ConfigurationManager.AppSettings["RateFairyXp"]);
            MaxLevel = byte.Parse(ConfigurationManager.AppSettings["MaxLevel"]);
            MaxJobLevel = byte.Parse(ConfigurationManager.AppSettings["MaxJobLevel"]);
            MaxSPLevel = byte.Parse(ConfigurationManager.AppSettings["MaxSPLevel"]);
            MaxHeroLevel = byte.Parse(ConfigurationManager.AppSettings["MaxHeroLevel"]);
            HeroicStartLevel = byte.Parse(ConfigurationManager.AppSettings["HeroicStartLevel"]);
            Schedules = ConfigurationManager.GetSection("eventScheduler") as List<Schedule>;
            Mails = DAOFactory.MailDAO.LoadAll().ToList();

            // load explicite type of ItemDTO
            foreach (ItemDTO itemDTO in DAOFactory.ItemDAO.LoadAll())
            {
                Item item;

                switch (itemDTO.ItemType)
                {
                    case ItemType.Ammo:
                        item = new NoFunctionItem(itemDTO);
                        break;

                    case ItemType.Armor:
                        item = new WearableItem(itemDTO);
                        break;

                    case ItemType.Box:
                        item = new BoxItem(itemDTO);
                        break;

                    case ItemType.Event:
                        item = new MagicalItem(itemDTO);
                        break;

                    case ItemType.Fashion:
                        item = new WearableItem(itemDTO);
                        break;

                    case ItemType.Food:
                        item = new FoodItem(itemDTO);
                        break;

                    case ItemType.Jewelery:
                        item = new WearableItem(itemDTO);
                        break;

                    case ItemType.Magical:
                        item = new MagicalItem(itemDTO);
                        break;

                    case ItemType.Main:
                        item = new NoFunctionItem(itemDTO);
                        break;

                    case ItemType.Map:
                        item = new NoFunctionItem(itemDTO);
                        break;

                    case ItemType.Part:
                        item = new NoFunctionItem(itemDTO);
                        break;

                    case ItemType.Potion:
                        item = new PotionItem(itemDTO);
                        break;

                    case ItemType.Production:
                        item = new ProduceItem(itemDTO);
                        break;

                    case ItemType.Quest1:
                        item = new NoFunctionItem(itemDTO);
                        break;

                    case ItemType.Quest2:
                        item = new NoFunctionItem(itemDTO);
                        break;

                    case ItemType.Sell:
                        item = new NoFunctionItem(itemDTO);
                        break;

                    case ItemType.Shell:
                        item = new MagicalItem(itemDTO);
                        break;

                    case ItemType.Snack:
                        item = new SnackItem(itemDTO);
                        break;

                    case ItemType.Special:
                        item = new SpecialItem(itemDTO);
                        break;

                    case ItemType.Specialist:
                        item = new WearableItem(itemDTO);
                        break;

                    case ItemType.Teacher:
                        item = new TeacherItem(itemDTO);
                        break;

                    case ItemType.Upgrade:
                        item = new UpgradeItem(itemDTO);
                        break;

                    case ItemType.Weapon:
                        item = new WearableItem(itemDTO);
                        break;

                    default:
                        item = new NoFunctionItem(itemDTO);
                        break;
                }
                _items.Add(item);
            }
            Logger.Log.Info(string.Format(Language.Instance.GetMessageFromKey("ITEMS_LOADED"), _items.Count));

            // intialize monsterdrops
            _monsterDrops = new ThreadSafeSortedList<short, List<DropDTO>>();
            foreach (var monsterDropGrouping in DAOFactory.DropDAO.LoadAll().GroupBy(d => d.MonsterVNum))
            {
                if (monsterDropGrouping.Key.HasValue)
                {
                    _monsterDrops[monsterDropGrouping.Key.Value] = monsterDropGrouping.OrderBy(d => d.DropChance).ToList();
                }
                else
                {
                    _generalDrops = monsterDropGrouping.ToList();
                }
            }
            Logger.Log.Info(string.Format(Language.Instance.GetMessageFromKey("DROPS_LOADED"), _monsterDrops.GetAllItems().Sum(i => i.Count)));

            // initialiize monsterskills
            _monsterSkills = new ThreadSafeSortedList<short, List<NpcMonsterSkill>>();
            foreach (var monsterSkillGrouping in DAOFactory.NpcMonsterSkillDAO.LoadAll().GroupBy(n => n.NpcMonsterVNum))
            {
                _monsterSkills[monsterSkillGrouping.Key] = monsterSkillGrouping.Select(n => n as NpcMonsterSkill).ToList();
            }
            Logger.Log.Info(string.Format(Language.Instance.GetMessageFromKey("MONSTERSKILLS_LOADED"), _monsterSkills.GetAllItems().Sum(i => i.Count)));

            // initialize Families

            // initialize Families
            LoadBazaar();
            Logger.Log.Info(string.Format(Language.Instance.GetMessageFromKey("BAZAR_LOADED"), _monsterSkills.GetAllItems().Sum(i => i.Count)));

            // initialize npcmonsters
            foreach (NpcMonsterDTO npcmonsterDTO in DAOFactory.NpcMonsterDAO.LoadAll())
            {
                _npcs.Add(npcmonsterDTO as NpcMonster);
            }
            Logger.Log.Info(string.Format(Language.Instance.GetMessageFromKey("NPCMONSTERS_LOADED"), _npcs.Count));

            // intialize receipes
            _recipes = new ThreadSafeSortedList<int, List<Recipe>>();
            foreach (var recipeGrouping in DAOFactory.RecipeDAO.LoadAll().GroupBy(r => r.MapNpcId))
            {
                _recipes[recipeGrouping.Key] = recipeGrouping.Select(r => r as Recipe).ToList();
            }
            Logger.Log.Info(string.Format(Language.Instance.GetMessageFromKey("RECIPES_LOADED"), _recipes.GetAllItems().Sum(i => i.Count)));

            // initialize shopitems
            _shopItems = new ThreadSafeSortedList<int, List<ShopItemDTO>>();
            foreach (var shopItemGrouping in DAOFactory.ShopItemDAO.LoadAll().GroupBy(s => s.ShopId))
            {
                _shopItems[shopItemGrouping.Key] = shopItemGrouping.ToList();
            }
            Logger.Log.Info(string.Format(Language.Instance.GetMessageFromKey("SHOPITEMS_LOADED"), _shopItems.GetAllItems().Sum(i => i.Count)));

            // initialize shopskills
            _shopSkills = new ThreadSafeSortedList<int, List<ShopSkillDTO>>();
            foreach (var shopSkillGrouping in DAOFactory.ShopSkillDAO.LoadAll().GroupBy(s => s.ShopId))
            {
                _shopSkills[shopSkillGrouping.Key] = shopSkillGrouping.ToList();
            }
            Logger.Log.Info(string.Format(Language.Instance.GetMessageFromKey("SHOPSKILLS_LOADED"), _shopSkills.GetAllItems().Sum(i => i.Count)));

            // initialize shops
            _shops = new ThreadSafeSortedList<int, Shop>();
            foreach (var shopGrouping in DAOFactory.ShopDAO.LoadAll())
            {
                _shops[shopGrouping.MapNpcId] = (Shop)shopGrouping;
            }
            Logger.Log.Info(string.Format(Language.Instance.GetMessageFromKey("SHOPS_LOADED"), _shops.GetAllItems().Count));

            // initialize teleporters
            _teleporters = new ThreadSafeSortedList<int, List<TeleporterDTO>>();
            foreach (var teleporterGrouping in DAOFactory.TeleporterDAO.LoadAll().GroupBy(t => t.MapNpcId))
            {
                _teleporters[teleporterGrouping.Key] = teleporterGrouping.Select(t => t).ToList();
            }
            Logger.Log.Info(string.Format(Language.Instance.GetMessageFromKey("TELEPORTERS_LOADED"), _teleporters.GetAllItems().Sum(i => i.Count)));

            // initialize skills
            foreach (SkillDTO skillDTO in DAOFactory.SkillDAO.LoadAll())
            {
                Skill skill = (Skill)skillDTO;
                skill.Combos.AddRange(DAOFactory.ComboDAO.LoadBySkillVnum(skill.SkillVNum).ToList());
                _skills.Add(skill);
            }
            Logger.Log.Info(string.Format(Language.Instance.GetMessageFromKey("SKILLS_LOADED"), _skills.Count));

            // intialize mapnpcs
            _mapNpcs = new ThreadSafeSortedList<short, List<MapNpc>>();
            foreach (var mapNpcGrouping in DAOFactory.MapNpcDAO.LoadAll().GroupBy(t => t.MapId))
            {
                _mapNpcs[mapNpcGrouping.Key] = mapNpcGrouping.Select(t => t as MapNpc).ToList();
            }
            Logger.Log.Info(string.Format(Language.Instance.GetMessageFromKey("MAPNPCS_LOADED"), _mapNpcs.GetAllItems().Sum(i => i.Count)));

            try
            {
                int i = 0;
                int monstercount = 0;

                foreach (MapDTO map in DAOFactory.MapDAO.LoadAll())
                {
                    Guid guid = Guid.NewGuid();
                    Map mapinfo = new Map(map.MapId, map.Data)
                    {
                        Music = map.Music
                    };
                    _maps.Add(mapinfo);

                    MapInstance newMap = new MapInstance(mapinfo, guid, map.ShopAllowed, MapInstanceType.BaseMapInstance, new InstanceBag());

                    // register for broadcast
                    _mapinstances.TryAdd(guid, newMap);
                    i++;

                    newMap.LoadMonsters();
                    newMap.LoadNpcs();
                    newMap.LoadPortals();

                    foreach (MapMonster mapMonster in newMap.Monsters)
                    {
                        mapMonster.MapInstance = newMap;
                        newMap.AddMonster(mapMonster);
                    }

                    foreach (MapNpc mapNpc in newMap.Npcs)
                    {
                        mapNpc.MapInstance = newMap;
                        newMap.AddNPC(mapNpc);
                    }
                    monstercount += newMap.Monsters.Count;
                }
                if (i != 0)
                {
                    Logger.Log.Info(string.Format(Language.Instance.GetMessageFromKey("MAPS_LOADED"), i));
                }
                else
                {
                    Logger.Log.Error(Language.Instance.GetMessageFromKey("NO_MAP"));
                }
                Logger.Log.Info(string.Format(Language.Instance.GetMessageFromKey("MAPMONSTERS_LOADED"), monstercount));

                StartedEvents = new List<EventType>();
                LoadFamilies();
                LaunchEvents();
                RefreshRanking();
                CharacterRelations = DAOFactory.CharacterRelationDAO.LoadAll().ToList();
                PenaltyLogs = DAOFactory.PenaltyLogDAO.LoadAll().ToList();
                ArenaInstance = GenerateMapInstance(2006, MapInstanceType.NormalInstance, new InstanceBag());
                ArenaInstance.IsPVP = true;
                FamilyArenaInstance = GenerateMapInstance(2106, MapInstanceType.NormalInstance, new InstanceBag());
                FamilyArenaInstance.IsPVP = true;
                LoadTimeSpaces();
            }
            catch (Exception ex)
            {
                Logger.Log.Error("General Error", ex);
            }

            //Register the new created TCPIP server to the api
            Guid serverIdentification = Guid.NewGuid();
            WorldId = serverIdentification;
        }

        public bool IsCharacterMemberOfGroup(long characterId)
        {
            return Groups != null && Groups.Any(g => g.IsMemberOfGroup(characterId));
        }

        public bool IsCharactersGroupFull(long characterId)
        {
            return Groups != null && Groups.Any(g => g.IsMemberOfGroup(characterId) && g.CharacterCount == 3);
        }

        public void JoinMiniland(ClientSession Session, ClientSession MinilandOwner)
        {
            ChangeMapInstance(Session.Character.CharacterId, MinilandOwner.Character.Miniland.MapInstanceId, 5, 8);
            if (Session.Character.Miniland.MapInstanceId != MinilandOwner.Character.Miniland.MapInstanceId)
            {
                Session.SendPacket(UserInterfaceHelper.Instance.GenerateMsg(Session.Character.MinilandMessage.Replace(' ', '^'), 0));
                Session.SendPacket(Session.Character.GenerateMlinfobr());
                MinilandOwner.Character.GeneralLogs.Add(new GeneralLogDTO { AccountId = Session.Account.AccountId, CharacterId = Session.Character.CharacterId, IpAddress = Session.IpAddress, LogData = "Miniland", LogType = "World", Timestamp = DateTime.Now });
                Session.SendPacket(MinilandOwner.Character.GenerateMinilandObjectForFriends());
            }
            else
            {
                Session.SendPacket(Session.Character.GenerateMlinfo());
                Session.SendPacket(MinilandOwner.Character.GetMinilandObjectList());
            }
            MinilandOwner.Character.Mates.Where(s => !s.IsTeamMember).ToList().ForEach(s => Session.SendPacket(s.GenerateIn()));
            Session.SendPackets(MinilandOwner.Character.GetMinilandEffects());
            Session.SendPacket(Session.Character.GenerateSay(string.Format(Language.Instance.GetMessageFromKey("MINILAND_VISITOR"), Session.Character.GeneralLogs.Count(s => s.LogData == "Miniland" && s.Timestamp.Day == DateTime.Now.Day), Session.Character.GeneralLogs.Count(s => s.LogData == "Miniland")), 10));
        }

        // Server
        public void Kick(string characterName)
        {
            ClientSession session = Sessions.FirstOrDefault(s => s.Character != null && s.Character.Name.Equals(characterName));
            session?.Disconnect();
        }

        // Map
        public void LeaveMap(long id)
        {
            ClientSession session = GetSessionByCharacterId(id);
            if (session == null)
            {
                return;
            }
            session.SendPacket(UserInterfaceHelper.Instance.GenerateMapOut());
            session.Character.Mates.Where(s => s.IsTeamMember).ToList().ForEach(s => session.CurrentMapInstance?.Broadcast(session, s.GenerateOut(), ReceiverType.AllExceptMe));
            session.CurrentMapInstance?.Broadcast(session, session.Character.GenerateOut(), ReceiverType.AllExceptMe);
        }

        public void RaidDisolve(ClientSession session, Raid raid = null)
        {
            if (raid == null)
                raid = Instance.Raids.FirstOrDefault(s => s.IsMemberOfRaid(session.Character.CharacterId));
            if (raid == null) return;
            foreach (ClientSession targetSession in raid.Characters)
            {
                targetSession.SendPacket(UserInterfaceHelper.Instance.GenerateMsg(Language.Instance.GetMessageFromKey("RAID_CLOSED"), 0));
                raid.Leave(targetSession);
            }
            raid.DestroyRaid();
        }

        public void RaidLeave(ClientSession session)
        {
            if (session == null) return;
            if (Raids == null) return;
            Raid raid = Instance.Raids.FirstOrDefault(s => s.IsMemberOfRaid(session.Character.CharacterId));
            if (raid == null) return;
            if (raid.Characters.Count > 1)
            {
                if (raid.Leader != session)
                {
                    raid.Leave(session);
                }
                else
                {
                    raid.Leave(raid.Leader);
                    raid.Leader.SendPacket(
                        $"say 1 {raid.Leader.Character.CharacterId} 10 {Language.Instance.GetMessageFromKey("RAID_NEW_LEADER")}");
                    raid.Leader.SendPacket(
                        UserInterfaceHelper.Instance.GenerateMsg(Language.Instance.GetMessageFromKey("RAID_NEW_LEADER"),
                            0));
                    raid.Leader.SendPacket(UserInterfaceHelper.Instance.GenerateMsg(
                        string.Format(Language.Instance.GetMessageFromKey("LEAVE_RAID"), session.Character.Name), 0));
                }
                session.SendPacket(UserInterfaceHelper.Instance.GenerateMsg(Language.Instance.GetMessageFromKey("RAID_LEFT"), 0));
                raid.UpdateVisual();
            }
            else
            {
                RaidDisolve(session, raid);
            }
        }

        public int RandomNumber(int min = 0, int max = 100)
        {
            return random.Value.Next(min, max);
        }

        public void RefreshRanking()
        {
            TopComplimented = DAOFactory.CharacterDAO.GetTopCompliment();
            TopPoints = DAOFactory.CharacterDAO.GetTopPoints();
            TopReputation = DAOFactory.CharacterDAO.GetTopReputation();
        }

        public void RelationRefresh(long RelationId)
        {
            inRelationRefreshMode = true;
            ServerCommunicationClient.Instance.HubProxy.Invoke("RelationRefresh", ServerGroup, RelationId);
            SpinWait.SpinUntil(() => !inRelationRefreshMode);
        }

        public void RemoveMapInstance(Guid MapId)
        {
            KeyValuePair<Guid, MapInstance> map = _mapinstances.FirstOrDefault(s => s.Key == MapId);
            if (!map.Equals(default(KeyValuePair<Guid, MapInstance>)))
            {
                map.Value.Dispose();
                ((IDictionary)_mapinstances).Remove(map);
            }
        }

        public void RemoveRaid(Raid raid)
        {
            _raids.Remove(raid.RaidId);
        }

        // Map
        public void ReviveFirstPosition(long characterId)
        {
            ClientSession session = GetSessionByCharacterId(characterId);
            if (session != null && session.Character.Hp <= 0)
            {
                if (session.CurrentMapInstance.MapInstanceType == MapInstanceType.TimeSpaceInstance)
                {
                    session.Character.Hp = (int)session.Character.HPLoad();
                    session.Character.Mp = (int)session.Character.MPLoad();
                    session.CurrentMapInstance?.Broadcast(session, session.Character.GenerateRevive());
                    session.SendPacket(session.Character.GenerateStat());
                }
                else
                {
                    session.Character.Hp = 1;
                    session.Character.Mp = 1;
                    if (session.CurrentMapInstance.MapInstanceType == MapInstanceType.BaseMapInstance)
                    {
                        RespawnMapTypeDTO resp = session.Character.Respawn;
                        short x = (short)(resp.DefaultX + RandomNumber(-3, 3));
                        short y = (short)(resp.DefaultY + RandomNumber(-3, 3));
                        ChangeMap(session.Character.CharacterId, resp.DefaultMapId, x, y);
                    }
                    else
                    {
                        Instance.ChangeMap(session.Character.CharacterId, session.Character.MapId, session.Character.MapX, session.Character.MapY);
                    }
                    session.CurrentMapInstance?.Broadcast(session, session.Character.GenerateTp());
                    session.CurrentMapInstance?.Broadcast(session.Character.GenerateRevive());
                    session.SendPacket(session.Character.GenerateStat());
                }
            }
        }

        public void SaveAll()
        {
            List<ClientSession> sessions = Sessions.Where(c => c.IsConnected).ToList();
            sessions.ForEach(s => s.Character?.Save());
        }

        public void SetProperty(long charId, string property, object value)
        {
            ClientSession session = GetSessionByCharacterId(charId);
            if (session == null)
            {
                return;
            }
            PropertyInfo propertyinfo = session.Character.GetType().GetProperties().Single(pi => pi.Name == property);
            propertyinfo.SetValue(session.Character, value, null);
        }

        public void Shout(string message)
        {
            Broadcast($"say 1 0 10 ({Language.Instance.GetMessageFromKey("ADMINISTRATOR")}){message}");
            Broadcast($"msg 2 {message}");
        }

        public void TeleportOnRandomPlaceInMap(ClientSession Session, Guid guid)
        {
            MapInstance map = GetMapInstance(guid);
            if (guid != default(Guid))
            {
                MapCell pos = map.Map.GetRandomPosition();
                ChangeMapInstance(Session.Character.CharacterId, guid, pos.X, pos.Y);
            }
        }

        // Server
        public void UpdateGroup(long charId)
        {
            try
            {
                if (Groups != null)
                {
                    Group myGroup = Groups.FirstOrDefault(s => s.IsMemberOfGroup(charId));
                    if (myGroup == null)
                    {
                        return;
                    }
                    ThreadSafeGenericList<ClientSession> groupMembers = Groups.FirstOrDefault(s => s.IsMemberOfGroup(charId))?.Characters;
                    if (groupMembers != null)
                    {
                        foreach (ClientSession session in groupMembers)
                        {
                            session.SendPacket(session.Character.GeneratePinit());
                            session.Character.SendPst();
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Error(e);
            }
        }

        internal List<NpcMonsterSkill> GetNpcMonsterSkillsByMonsterVNum(short npcMonsterVNum)
        {
            return _monsterSkills.ContainsKey(npcMonsterVNum) ? _monsterSkills[npcMonsterVNum] : new List<NpcMonsterSkill>();
        }

        internal Shop GetShopByMapNpcId(int mapNpcId)
        {
            return _shops.ContainsKey(mapNpcId) ? _shops[mapNpcId] : null;
        }

        internal List<ShopItemDTO> GetShopItemsByShopId(int shopId)
        {
            return _shopItems.ContainsKey(shopId) ? _shopItems[shopId] : new List<ShopItemDTO>();
        }

        internal List<ShopSkillDTO> GetShopSkillsByShopId(int shopId)
        {
            return _shopSkills.ContainsKey(shopId) ? _shopSkills[shopId] : new List<ShopSkillDTO>();
        }

        internal List<TeleporterDTO> GetTeleportersByNpcVNum(short npcMonsterVNum)
        {
            if (_teleporters != null && _teleporters.ContainsKey(npcMonsterVNum))
            {
                return _teleporters[npcMonsterVNum];
            }
            return new List<TeleporterDTO>();
        }

        internal void StopServer()
        {
            Instance.ShutdownStop = true;
            Instance.TaskShutdown = null;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _monsterDrops.Dispose();
                _groups.Dispose();
                _raids.Dispose();
                _monsterSkills.Dispose();
                _shopSkills.Dispose();
                _shopItems.Dispose();
                _shops.Dispose();
                _recipes.Dispose();
                _mapNpcs.Dispose();
                _teleporters.Dispose();
            }
        }

        // Server
        private void BotProcess()
        {
            try
            {
                Shout(Language.Instance.GetMessageFromKey($"BOT_MESSAGE_{ RandomNumber(0, 5) }"));
            }
            catch (Exception e)
            {
                Logger.Error(e);
            }
        }

        private void GroupProcess()
        {
            try
            {
                if (Groups != null)
                {
                    foreach (Group grp in Groups)
                    {
                        foreach (ClientSession session in grp.Characters)
                        {
                            foreach (string str in grp.GeneratePst(session))
                            {
                                session.SendPacket(str);
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Error(e);
            }
        }

        private void LaunchEvents()
        {
            _groups = new ThreadSafeSortedList<long, Group>();
            _raids = new ThreadSafeSortedList<long, Raid>();

            Observable.Interval(TimeSpan.FromMinutes(5)).Subscribe(x =>
            {
                SaveAllProcess();
            });

            Observable.Interval(TimeSpan.FromSeconds(2)).Subscribe(x =>
            {
                GroupProcess();
            });

            Observable.Interval(TimeSpan.FromHours(3)).Subscribe(x =>
            {
                BotProcess();
            });
            Observable.Interval(TimeSpan.FromHours(3)).Subscribe(x =>
            {
                BotProcess();
            });

            EventHelper.Instance.RunEvent(new EventContainer(ServerManager.Instance.GetMapInstance(ServerManager.Instance.GetBaseMapInstanceIdByMapId(98)), EventActionType.NPCSEFFECTCHANGESTATE, true));
            foreach (Schedule schedul in Schedules)
            {
                Observable.Timer(TimeSpan.FromSeconds(EventHelper.Instance.GetMilisecondsBeforeTime(schedul.Time).TotalSeconds), TimeSpan.FromDays(1))
                .Subscribe(
                e =>
                {
                    EventHelper.Instance.GenerateEvent(schedul.Event);
                });
            }

            Observable.Interval(TimeSpan.FromSeconds(30)).Subscribe(x =>
            {
                MailProcess();
            });

            Observable.Interval(TimeSpan.FromSeconds(1)).Subscribe(x =>
            {
                RemoveItemProcess();
            });

            ServerCommunicationClient.Instance.SessionKickedEvent += OnSessionKicked;
            ServerCommunicationClient.Instance.MessageSentToCharacter += OnMessageSentToCharacter;
            ServerCommunicationClient.Instance.FamilyRefresh += OnFamilyRefresh;
            ServerCommunicationClient.Instance.RelationRefresh += OnRelationRefresh;
            ServerCommunicationClient.Instance.BazaarRefresh += OnBazaarRefresh;
            ServerCommunicationClient.Instance.PenaltyLogRefresh += OnPenaltyLogRefresh;
            _lastGroupId = 1;
            _lastRaidId = 1;
        }

        private void LoadBazaar()
        {
            BazaarList = new List<BazaarItemLink>();
            foreach (BazaarItemDTO bz in DAOFactory.BazaarItemDAO.LoadAll())
            {
                BazaarItemLink item = new BazaarItemLink { BazaarItem = bz };
                CharacterDTO chara = DAOFactory.CharacterDAO.LoadById(bz.SellerId);
                if (chara != null)
                {
                    item.Owner = chara.Name;
                    item.Item = (ItemInstance)DAOFactory.IteminstanceDAO.LoadById(bz.ItemInstanceId);
                }
                BazaarList.Add(item);
            }
        }

        private void LoadFamilies()
        {
            FamilyList = new List<Family>();
            foreach (FamilyDTO fam in DAOFactory.FamilyDAO.LoadAll())
            {
                Family fami = (Family)fam;
                fami.FamilyCharacters = new List<FamilyCharacter>();
                foreach (FamilyCharacterDTO famchar in DAOFactory.FamilyCharacterDAO.LoadByFamilyId(fami.FamilyId).ToList())
                {
                    fami.FamilyCharacters.Add((FamilyCharacter)famchar);
                }
                FamilyCharacter familyCharacter = fami.FamilyCharacters.FirstOrDefault(s => s.Authority == FamilyAuthority.Head);
                if (familyCharacter != null)
                {
                    fami.Warehouse = new Inventory((Character)familyCharacter.Character);
                    foreach (ItemInstanceDTO inventory in DAOFactory.IteminstanceDAO.LoadByCharacterId(familyCharacter.CharacterId).Where(s => s.Type == InventoryType.FamilyWareHouse).ToList())
                    {
                        inventory.CharacterId = familyCharacter.CharacterId;
                        fami.Warehouse[inventory.Id] = (ItemInstance)inventory;
                    }
                }
                fami.FamilyLogs = DAOFactory.FamilyLogDAO.LoadByFamilyId(fami.FamilyId).ToList();
                FamilyList.Add(fami);
            }
        }

        private void LoadTimeSpaces()
        {
            foreach (var map in _mapinstances)
            {
                foreach (ScriptedInstance timespace in DAOFactory.TimeSpaceDAO.LoadByMap(map.Value.Map.MapId).ToList())
                {
                    timespace.LoadGlobals();
                    map.Value.TimeSpaces.Add(timespace);
                }
            }
        }

        private void MailProcess()
        {
            try
            {
                Mails = DAOFactory.MailDAO.LoadAll().ToList();
                Sessions.Where(c => c.IsConnected).ToList().ForEach(s => s.Character?.RefreshMail());
            }
            catch (Exception e)
            {
                Logger.Error(e);
            }
        }

        private void OnBazaarRefresh(object sender, EventArgs e)
        {
            Tuple<string, long> tuple = (Tuple<string, long>)sender;
            if (ServerGroup != tuple.Item1)
            {
                return;
            }
            long BazaarId = tuple.Item2;
            BazaarItemDTO bzdto = DAOFactory.BazaarItemDAO.LoadById(BazaarId);
            BazaarItemLink bzlink = BazaarList.FirstOrDefault(s => s.BazaarItem.BazaarItemId == BazaarId);
            lock (BazaarList)
            {
                if (bzdto != null)
                {
                    CharacterDTO chara = DAOFactory.CharacterDAO.LoadById(bzdto.SellerId);
                    if (bzlink != null)
                    {
                        BazaarList.Remove(bzlink);
                        bzlink.BazaarItem = bzdto;
                        bzlink.Owner = chara.Name;
                        bzlink.Item = (ItemInstance)DAOFactory.IteminstanceDAO.LoadById(bzdto.ItemInstanceId);
                        BazaarList.Add(bzlink);
                    }
                    else
                    {
                        BazaarItemLink item = new BazaarItemLink { BazaarItem = bzdto };
                        if (chara != null)
                        {
                            item.Owner = chara.Name;
                            item.Item = (ItemInstance)DAOFactory.IteminstanceDAO.LoadById(bzdto.ItemInstanceId);
                        }
                        BazaarList.Add(item);
                    }
                }
                else if (bzlink != null)
                {
                    BazaarList.Remove(bzlink);
                }
            }
            inBazaarRefreshMode = false;
        }

        private void OnFamilyRefresh(object sender, EventArgs e)
        {
            Tuple<string, long> tuple = (Tuple<string, long>)sender;
            if (ServerGroup != tuple.Item1)
            {
                return;
            }
            long FamilyId = tuple.Item2;
            FamilyDTO famdto = DAOFactory.FamilyDAO.LoadById(FamilyId);
            Family fam = FamilyList.FirstOrDefault(s => s.FamilyId == FamilyId);

            lock (FamilyList)
            {
                if (famdto != null)
                {
                    if (fam != null)
                    {
                        MapInstance lod = fam.LandOfDeath;
                        FamilyList.Remove(fam);
                        fam = (Family)famdto;
                        fam.FamilyCharacters = new List<FamilyCharacter>();
                        foreach (FamilyCharacterDTO famchar in DAOFactory.FamilyCharacterDAO.LoadByFamilyId(fam.FamilyId).ToList())
                        {
                            fam.FamilyCharacters.Add((FamilyCharacter)famchar);
                        }
                        FamilyCharacter familyCharacter = fam.FamilyCharacters.FirstOrDefault(s => s.Authority == FamilyAuthority.Head);
                        if (familyCharacter != null)
                        {
                            fam.Warehouse = new Inventory((Character)familyCharacter.Character);
                            foreach (ItemInstanceDTO inventory in DAOFactory.IteminstanceDAO.LoadByCharacterId(familyCharacter.CharacterId).Where(s => s.Type == InventoryType.FamilyWareHouse).ToList())
                            {
                                inventory.CharacterId = familyCharacter.CharacterId;
                                fam.Warehouse[inventory.Id] = (ItemInstance)inventory;
                            }
                        }
                        fam.FamilyLogs = DAOFactory.FamilyLogDAO.LoadByFamilyId(fam.FamilyId).ToList();
                        fam.LandOfDeath = lod;
                        FamilyList.Add(fam);
                    }
                    else
                    {
                        Family fami = (Family)famdto;
                        fami.FamilyCharacters = new List<FamilyCharacter>();
                        foreach (FamilyCharacterDTO famchar in DAOFactory.FamilyCharacterDAO.LoadByFamilyId(fami.FamilyId).ToList())
                        {
                            fami.FamilyCharacters.Add((FamilyCharacter)famchar);
                        }
                        FamilyCharacter familyCharacter = fami.FamilyCharacters.FirstOrDefault(s => s.Authority == FamilyAuthority.Head);
                        if (familyCharacter != null)
                        {
                            fami.Warehouse = new Inventory((Character)familyCharacter.Character);
                            foreach (ItemInstanceDTO inventory in DAOFactory.IteminstanceDAO.LoadByCharacterId(familyCharacter.CharacterId).Where(s => s.Type == InventoryType.FamilyWareHouse).ToList())
                            {
                                inventory.CharacterId = familyCharacter.CharacterId;
                                fami.Warehouse[inventory.Id] = (ItemInstance)inventory;
                            }
                        }
                        fami.FamilyLogs = DAOFactory.FamilyLogDAO.LoadByFamilyId(fami.FamilyId).ToList();
                        FamilyList.Add(fami);
                    }
                }
                else if (fam != null)
                {
                    FamilyList.Remove(fam);
                }
            }
            inFamilyRefreshMode = false;
        }

        private void OnMessageSentToCharacter(object sender, EventArgs e)
        {
            if (sender != null)
            {
                Tuple<string, string, string, string, int, MessageType> message = (Tuple<string, string, string, string, int, MessageType>)sender;
                if (ServerGroup != message.Item1 && message.Item1 != "*")
                {
                    return;
                }
                ClientSession targetSession = Sessions.SingleOrDefault(s => s.Character.Name == message.Item3);
                long familyId;
                switch (message.Item6)
                {
                    case MessageType.WhisperGM:
                    case MessageType.Whisper:
                        if (targetSession == null || message.Item6 == MessageType.WhisperGM && targetSession.Account.Authority != AuthorityType.GameMaster)
                        {
                            return;
                        }

                        if (targetSession.Character.GmPvtBlock)
                        {
                            ServerCommunicationClient.Instance.HubProxy.Invoke<int?>("SendMessageToCharacter", ServerGroup, targetSession.Character.Name, message.Item2, targetSession.Character.GenerateSay(Language.Instance.GetMessageFromKey("GM_CHAT_BLOCKED"), 10), Instance.ChannelId, MessageType.PrivateChat);
                        }
                        else if (targetSession.Character.WhisperBlocked)
                        {
                            ServerCommunicationClient.Instance.HubProxy.Invoke<int?>("SendMessageToCharacter", ServerGroup, targetSession.Character.Name, message.Item2, UserInterfaceHelper.Instance.GenerateMsg(Language.Instance.GetMessageFromKey("USER_WHISPER_BLOCKED"), 0), Instance.ChannelId, MessageType.PrivateChat);
                        }
                        else
                        {
                            if (message.Item5 != ChannelId)
                            {
                                ServerCommunicationClient.Instance.HubProxy.Invoke<int?>("SendMessageToCharacter", ServerGroup, targetSession.Character.Name, message.Item2, targetSession.Character.GenerateSay(string.Format(Language.Instance.GetMessageFromKey("MESSAGE_SENT_TO_CHARACTER"), message.Item3, Instance.ChannelId), 11), Instance.ChannelId, MessageType.PrivateChat);
                                targetSession.SendPacket($"{message.Item4} <{Language.Instance.GetMessageFromKey("CHANNEL")}: {message.Item5}>");
                            }
                            else
                            {
                                targetSession.SendPacket(message.Item4);
                            }
                        }
                        break;

                    case MessageType.Shout:
                        Shout(message.Item4);
                        break;

                    case MessageType.PrivateChat:
                        targetSession?.SendPacket(message.Item4);
                        break;

                    case MessageType.FamilyChat:
                        if (long.TryParse(message.Item3, out familyId))
                        {
                            if (message.Item5 != ChannelId)
                            {
                                foreach (ClientSession s in Instance.Sessions)
                                {
                                    if (s.HasSelectedCharacter && s.Character.Family != null)
                                    {
                                        if (s.Character.Family.FamilyId == familyId)
                                        {
                                            s.SendPacket($"say 1 0 6 <{Language.Instance.GetMessageFromKey("CHANNEL")}: {message.Item5}>{message.Item4}");
                                        }
                                    }
                                }
                            }
                        }
                        break;

                    case MessageType.Family:
                        if (long.TryParse(message.Item3, out familyId))
                        {
                            foreach (ClientSession s in Instance.Sessions)
                            {
                                if (s.HasSelectedCharacter && s.Character.Family != null)
                                {
                                    if (s.Character.Family.FamilyId == familyId)
                                    {
                                        s.SendPacket(message.Item4);
                                    }
                                }
                            }
                        }
                        break;
                }
            }
        }

        private void OnPenaltyLogRefresh(object sender, EventArgs e)
        {
            int relId = (int)sender;
            PenaltyLogDTO reldto = DAOFactory.PenaltyLogDAO.LoadById(relId);
            PenaltyLogDTO rel = PenaltyLogs.FirstOrDefault(s => s.PenaltyLogId == relId);
            if (reldto != null)
            {
                if (rel != null)
                {
                    rel = reldto;
                }
                else
                {
                    PenaltyLogs.Add(reldto);
                }
            }
            else if (rel != null)
            {
                PenaltyLogs.Remove(rel);
            }
        }

        private void OnRelationRefresh(object sender, EventArgs e)
        {
            inRelationRefreshMode = true;
            Tuple<string, long> tuple = (Tuple<string, long>)sender;
            if (ServerGroup != tuple.Item1)
            {
                return;
            }
            long relId = tuple.Item2;
            lock (CharacterRelations)
            {
                CharacterRelationDTO reldto = DAOFactory.CharacterRelationDAO.LoadById(relId);
                CharacterRelationDTO rel = CharacterRelations.FirstOrDefault(s => s.CharacterRelationId == relId);
                if (reldto != null)
                {
                    if (rel != null)
                    {
                        rel = reldto;
                    }
                    else
                    {
                        CharacterRelations.Add(reldto);
                    }
                }
                else if (rel != null)
                {
                    CharacterRelations.Remove(rel);
                }
            }
            inRelationRefreshMode = false;
        }

        private void OnSessionKicked(object sender, EventArgs e)
        {
            if (sender != null)
            {
                Tuple<long?, string> kickedSession = (Tuple<long?, string>)sender;

                ClientSession targetSession = Sessions.FirstOrDefault(s => (!kickedSession.Item1.HasValue || s.SessionId == kickedSession.Item1.Value)
                                                        && (string.IsNullOrEmpty(kickedSession.Item2) || s.Account.Name == kickedSession.Item2));

                targetSession?.Disconnect();
            }
        }

        private void RemoveGroup(Group grp)
        {
            _groups.Remove(grp.GroupId);
        }

        private void RemoveItemProcess()
        {
            try
            {
                Sessions.Where(c => c.IsConnected).ToList().ForEach(s => s.Character?.RefreshValidity());
            }
            catch (Exception e)
            {
                Logger.Error(e);
            }
        }

        // Server
        private void SaveAllProcess()
        {
            try
            {
                Logger.Log.Info(Language.Instance.GetMessageFromKey("SAVING_ALL"));
                SaveAll();
            }
            catch (Exception e)
            {
                Logger.Error(e);
            }
        }

        #endregion
    }
}