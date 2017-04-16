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

using Microsoft.AspNet.SignalR.Client;
using OpenNos.Domain;
using System;
using System.Collections.Generic;

namespace OpenNos.WebApi.Reference
{
    public class ServerCommunicationClient
    {
        #region Members

        public bool IsConnected;
        private const string remoteUrl = "http://localhost:6666/";

        private static ServerCommunicationClient _instance;
        private bool _disposed;
        private HubConnection _hubconnection;
        private IHubProxy _hubProxy;

        #endregion

        #region Events

        public event EventHandler AccountConnectedEvent;

        public event EventHandler AccountDisconnectedEvent;

        public event EventHandler BazaarRefresh;

        public event EventHandler CharacterConnectedEvent;

        public event EventHandler CharacterDisconnectedEvent;

        public event EventHandler FamilyRefresh;

        public event EventHandler MessageSentToCharacter;

        public event EventHandler PenaltyLogRefresh;

        public event EventHandler RelationRefresh;

        public event EventHandler SessionKickedEvent;

        #endregion

        #region Properties

        public static ServerCommunicationClient Instance
        {
            get
            {
                return _instance ?? (_instance = new ServerCommunicationClient());
            }
        }

        public IHubProxy HubProxy
        {
            get
            {
                if (_hubProxy == null)
                {
                    InitializeAndRegisterCallbacks();
                }
                while (!IsConnected) { }

                return _hubProxy;
            }
        }

        #endregion

        #region Methods

        public void Dispose()
        {
            if (!_disposed)
            {
                Dispose(true);
                GC.SuppressFinalize(this);
                _disposed = true;
            }
        }

        public void InitializeAndRegisterCallbacks()
        {
            _hubconnection = new HubConnection(remoteUrl);
            _hubconnection.Closed += () =>
            {
                IsConnected = false;
                _hubconnection = new HubConnection(remoteUrl);
                _hubProxy = _hubconnection.CreateHubProxy("servercommunicationhub");

                _hubProxy.On<string, long>("accountConnected", OnAccountConnected);

                _hubProxy.On<string>("accountDisconnected", OnAccountDisconnected);

                _hubProxy.On<string, string, long>("characterConnected", OnCharacterConnected);

                _hubProxy.On<string, string, long>("characterDisconnected", OnCharacterDisconnected);

                _hubProxy.On<long?, string>("kickSession", OnSessionKicked);

                _hubProxy.On<string, long>("refreshFamily", OnFamilyRefresh);

                _hubProxy.On<string, long>("refreshRelation", OnRelationRefresh);

                _hubProxy.On<int>("refreshPenaltyLog", OnPenaltyLogRefresh);

                _hubProxy.On<string, long>("refreshBazaar", OnBazaarRefresh);

                _hubProxy.On<string, string, string, string, int, MessageType>("sendMessageToCharacter", OnMessageSentToCharacter);

                _hubconnection.Start().Wait();
                IsConnected = true;
            };

            _hubProxy = _hubconnection.CreateHubProxy("servercommunicationhub");

            //register callback methods
            _hubProxy.On<string, long>("accountConnected", OnAccountConnected);

            _hubProxy.On<string>("accountDisconnected", OnAccountDisconnected);

            _hubProxy.On<string, string, long>("characterConnected", OnCharacterConnected);

            _hubProxy.On<string, string, long>("characterDisconnected", OnCharacterDisconnected);

            _hubProxy.On<long?, string>("kickSession", OnSessionKicked);

            _hubProxy.On<string, long>("refreshFamily", OnFamilyRefresh);

            _hubProxy.On<string, long>("refreshRelation", OnRelationRefresh);

            _hubProxy.On<int>("refreshPenaltyLog", OnPenaltyLogRefresh);

            _hubProxy.On<string, long>("refreshBazaar", OnBazaarRefresh);

            _hubProxy.On<string, string, string, string, int, MessageType>("sendMessageToCharacter", OnMessageSentToCharacter);

            _hubconnection.Start().Wait();
            IsConnected = true;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                // dispose communication callback service
            }
        }

        private void OnAccountConnected(string accountName, long sessionId)
        {
            if (AccountConnectedEvent != null && !string.IsNullOrEmpty(accountName))
            {
                AccountConnectedEvent(new KeyValuePair<string, long>(accountName, sessionId), new EventArgs());
            }
        }

        private void OnAccountDisconnected(string accountName)
        {
            if (AccountDisconnectedEvent != null && !string.IsNullOrEmpty(accountName))
            {
                AccountDisconnectedEvent(accountName, new EventArgs());
            }
        }

        private void OnBazaarRefresh(string worldgroup, long BazaarItemId)
        {
            BazaarRefresh?.Invoke(new Tuple<string, long>(worldgroup, BazaarItemId), new EventArgs());
        }

        private void OnCharacterConnected(string worldgroup, string characterName, long characterId)
        {
            if (CharacterConnectedEvent != null && !string.IsNullOrEmpty(characterName))
            {
                CharacterConnectedEvent(new Tuple<string, string, long>(worldgroup, characterName, characterId), new EventArgs());
            }
        }

        private void OnCharacterDisconnected(string worldgroup, string characterName, long characterId)
        {
            if (CharacterDisconnectedEvent != null && !string.IsNullOrEmpty(characterName))
            {
                CharacterDisconnectedEvent(new Tuple<string, string, long>(worldgroup, characterName, characterId), new EventArgs());
            }
        }

        private void OnFamilyRefresh(string worldgroup, long FamilyId)
        {
            FamilyRefresh?.Invoke(new Tuple<string, long>(worldgroup, FamilyId), new EventArgs());
        }

        private void OnMessageSentToCharacter(string worldgroup, string sourcecharacterName, string characterName, string message, int fromChannel, MessageType messageType)
        {
            MessageSentToCharacter?.Invoke(new Tuple<string, string, string, string, int, MessageType>(worldgroup, sourcecharacterName, characterName, message, fromChannel, messageType), new EventArgs());
        }

        private void OnPenaltyLogRefresh(int id)
        {
            PenaltyLogRefresh?.Invoke(id, new EventArgs());
        }

        private void OnRelationRefresh(string worldgroup, long id)
        {
            RelationRefresh?.Invoke(new Tuple<string, long>(worldgroup, id), new EventArgs());
        }

        private void OnSessionKicked(long? sessionId, string accountName)
        {
            if (SessionKickedEvent != null && (sessionId.HasValue || !string.IsNullOrEmpty(accountName)))
            {
                SessionKickedEvent(new Tuple<long?, string>(sessionId, accountName), new EventArgs());
            }
        }

        #endregion
    }
}