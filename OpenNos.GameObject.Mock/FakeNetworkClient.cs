﻿using OpenNos.Core;
using OpenNos.Core.Networking.Communication.Scs.Communication.Messages;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace OpenNos.GameObject.Mock
{
    public class FakeNetworkClient : INetworkClient
    {
        #region Members

        private long _clientId;
        private ClientSession _clientSession;
        private bool _isConnected;
        private Queue<string> _receivedPackets;
        private Queue<string> _sentPackets;
        private long lastKeepAliveIdentitiy;

        #endregion

        #region Instantiation

        public FakeNetworkClient()
        {
            _clientId = 0;
            _sentPackets = new Queue<string>();
            _receivedPackets = new Queue<string>();
            lastKeepAliveIdentitiy = 1;
            _isConnected = true;
        }

        #endregion

        #region Events

        public event EventHandler<MessageEventArgs> MessageReceived;

        #endregion

        #region Properties

        public long ClientId
        {
            get
            {
                if (_clientId == 0)
                {
                    _clientId = GameObjectMockHelper.Instance.GetNextClientId();
                }

                return _clientId;
            }

            set
            {
                _clientId = value;
            }
        }

        public string IpAddress
        {
            get
            {
                return "127.0.0.1";
            }
        }

        public bool IsConnected
        {
            get
            {
                return _isConnected;
            }
        }

        public bool IsDisposing { get; set; }

        public Queue<string> ReceivedPackets
        {
            get
            {
                return _receivedPackets;
            }
        }

        public Queue<string> SentPackets
        {
            get
            {
                return _sentPackets;
            }
        }

        public ClientSession Session
        {
            get
            {
                return GetClientSession();
            }
        }

        #endregion

        #region Methods

        public async Task ClearLowPriorityQueue()
        {
            // nothing to do here
        }

        public void Disconnect()
        {
            _isConnected = false;
        }

        public ClientSession GetClientSession()
        {
            return _clientSession;
        }

        public void Initialize(EncryptionBase encryptor)
        {
            // NOTHING TO DO HERE
        }

        /// <summary>
        /// Send a Packet to the Server as the Fake client receives it and triggers a Handler method.
        /// </summary>
        /// <param name="packet"></param>
        public void ReceivePacket(string packet)
        {
            Debug.WriteLine($"Enqueued {packet}");
            UTF8Encoding encoding = new UTF8Encoding();
            byte[] buf = encoding.GetBytes($"{lastKeepAliveIdentitiy} {packet}");
            MessageReceived?.Invoke(this, new MessageEventArgs(new ScsRawDataMessage(buf), DateTime.Now));
            lastKeepAliveIdentitiy = lastKeepAliveIdentitiy + 1;
        }

        /// <summary>
        /// Send a packet to the Server as the Fake client receives it and triggers a Handler method.
        /// </summary>
        /// <param name="packet">Packet created thru PacketFactory.</param>
        public void ReceivePacket(PacketDefinition packet)
        {
            ReceivePacket(PacketFactory.Serialize(packet));
        }

        public void SendPacket(string packet, byte priority = 10)
        {
            _sentPackets.Enqueue(packet);
        }

        public void SendPacketFormat(string packet, params object[] param)
        {
            _sentPackets.Enqueue(string.Format(packet, param));
        }

        public void SendPackets(IEnumerable<string> packets, byte priority = 10)
        {
            foreach (string packet in packets)
            {
                SendPacket(packet, priority);
            }
        }

        public void SetClientSession(object clientSession)
        {
            _clientSession = (ClientSession)clientSession;
        }

        #endregion
    }
}