﻿#region

using db;
using System;
using wServer.networking.cliPackets;
using wServer.networking.svrPackets;
using wServer.realm.entities.player;
using FailurePacket = wServer.networking.svrPackets.FailurePacket;

#endregion

namespace wServer.networking.handlers
{
    internal class LoadHandler : PacketHandlerBase<LoadPacket>
    {
        public override PacketID Id => PacketID.LOAD;

        protected override void HandlePacket(Client client, LoadPacket packet)
        {
            using (Database db = new Database())
            {
                client.Character = db.LoadCharacter(client.Account, packet.CharacterId);
                if (client.Character != null)
                {
                    if (client.Character.Dead)
                    {
                        client.SendPacket(new FailurePacket
                        {
                            ErrorId = 0,
                            ErrorDescription = "Character is dead."
                        });
                    }
                    else
                    {
                        client.SendPacket(new CreateSuccessPacket
                        {
                            CharacterId = client.Character.CharacterId,
                            ObjectId =
                                client.Manager.Worlds[client.TargetWorld].EnterWorld(
                                    client.Player = new Player(client.Manager, client))
                        });
                        client.Stage = ProtocalStage.Ready;
                    }
                }
                else
                {
                    client.SendPacket(new FailurePacket
                    {
                        ErrorId = 0,
                        ErrorDescription = "Failed to Load character."
                    });
                    client.Disconnect();
                }
            }
        }
    }
}