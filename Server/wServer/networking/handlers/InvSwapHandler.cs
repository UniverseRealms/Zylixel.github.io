﻿#region

using System;
using System.Collections.Generic;
using db;
using MySql.Data.MySqlClient;
using wServer.networking.cliPackets;
using wServer.networking.svrPackets;
using wServer.realm;
using wServer.realm.entities;
using wServer.realm.entities.player;
using wServer.realm.worlds;

#endregion

namespace wServer.networking.handlers
{
    internal class InvSwapHandler : PacketHandlerBase<InvSwapPacket>
    {
        public override PacketID Id => PacketID.INVSWAP;

        protected override void HandlePacket(Client client, InvSwapPacket packet)
        {
            if (client.Player.Owner == null) return;

            if (client.Player.Owner is PetYard)
            {
                client.SendPacket(new InvResultPacket
                {
                    Result = 0
                });
            }

            client.Manager.Logic.AddPendingAction(t =>
            {
                Entity en1 = client.Player.Owner.GetEntity(packet.SlotObject1.ObjectId);
                Entity en2 = client.Player.Owner.GetEntity(packet.SlotObject2.ObjectId);
                IContainer con1 = en1 as IContainer;
                IContainer con2 = en2 as IContainer;

                if (packet.SlotObject1.SlotId == 254 || packet.SlotObject1.SlotId == 255 ||
                    packet.SlotObject2.SlotId == 254 || packet.SlotObject2.SlotId == 255)
                {
                    if (packet.SlotObject2.SlotId == 254)
                        if (client.Player.HealthPotions < 6)
                        {
                            client.Player.HealthPotions++;
                            con1.Inventory[packet.SlotObject1.SlotId] = null;
                        }
                    if (packet.SlotObject2.SlotId == 255)
                        if (client.Player.MagicPotions < 6)
                        {
                            client.Player.MagicPotions++;
                            con1.Inventory[packet.SlotObject1.SlotId] = null;
                        }
                    if (packet.SlotObject1.SlotId == 254)
                        if (client.Player.HealthPotions > 0)
                        {
                            client.Player.HealthPotions--;
                            con2.Inventory[packet.SlotObject2.SlotId] = null;
                        }
                    if (packet.SlotObject1.SlotId == 255)
                        if (client.Player.MagicPotions > 0)
                        {
                            client.Player.MagicPotions--;
                            con2.Inventory[packet.SlotObject1.SlotId] = null;
                        }
                    if (en1 is Player)
                        (en1 as Player).Client.SendPacket(new InvResultPacket {Result = 0});
                    else if (en2 is Player)
                        (en2 as Player).Client.SendPacket(new InvResultPacket {Result = 0});
                    return;
                }
                //TODO: locker
                Item item1 = con1.Inventory[packet.SlotObject1.SlotId];
                Item item2 = con2.Inventory[packet.SlotObject2.SlotId];
                List<ushort> publicbags = new List<ushort>
                {
                    0x0500,
                    0x0506,
                    0x0501
                };

                if (en1.Dist(en2) > 1)
                {
                    if (en1 is Player)
                        (en1 as Player).Client.SendPacket(new InvResultPacket
                        {
                            Result = -1
                        });
                    else if (en2 is Player)
                        (en2 as Player).Client.SendPacket(new InvResultPacket
                        {
                            Result = -1
                        });
                    en1.UpdateCount++;
                    en2.UpdateCount++;
                    return;
                }

                if (en1 is Player && en2 is Player & en1.Id != en2.Id)
                {
                    client.Player.kickforCheats(Player.possibleExploit.ITEM_STEAL);
                    return;
                };

                if (!IsValid(item1, item2, con1, con2, packet, client))
                {
                    client.Disconnect(Client.DisconnectReason.INVALID_INVSWAP);
                    return;
                }

                if (con2 is OneWayContainer || (en2.ObjectType == 0x0504 && Client.Player.Owner.Name != "Vault" && item1.Soulbound))
                {
                    con1.Inventory[packet.SlotObject1.SlotId] = null;
                    con2.Inventory[packet.SlotObject2.SlotId] = null;
                    client.Player.DropBag(item1);
                    en1.UpdateCount++;
                    en2.UpdateCount++;
                    return;
                }
                if (con1 is OneWayContainer)
                {
                    if (con2.Inventory[packet.SlotObject2.SlotId] != null)
                    {
                        (en2 as Player)?.Client.SendPacket(new InvResultPacket {Result = -1});
                        (con1 as OneWayContainer).UpdateCount++;
                        en2.UpdateCount++;
                        return;
                    }

                    using (Database db = new Database())
                    {
                        try
                        {
                            Account acc = db.GetAccount(client.Account.AccountId, Manager.GameData);
                            acc.Gifts.Remove(con1.Inventory[packet.SlotObject1.SlotId].serialId);

                            MySqlCommand cmd = db.CreateQuery();
                            cmd.CommandText = @"UPDATE accounts SET gifts=@gifts WHERE id=@accId;";
                            cmd.Parameters.AddWithValue("@accId", acc.AccountId);
                            cmd.Parameters.AddWithValue("@gifts", Utils.GetCommaSepString(acc.Gifts.ToArray()));
                            cmd.ExecuteNonQuery();
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(ex);
                        }
                    }
                    con1.Inventory[packet.SlotObject1.SlotId] = null;
                    con2.Inventory[packet.SlotObject2.SlotId] = item1;
                    (en2 as Player).CalcBoost();
                    client.Save();
                    en1.UpdateCount++;
                    en2.UpdateCount++;
                    (en2 as Player).Client.SendPacket(new InvResultPacket {Result = 0});
                    return;
                }

                con1.Inventory[packet.SlotObject1.SlotId] = item2;
                if (item1 != null && item1.serialId == -1 && item1.firstUser == -1) //When players pick up items from mob drops
                {
                    Item newItem = Manager.CreateSerial((en2 as Player).SerialConvert(item1), DroppedIn: item1.droppedIn);
                    newItem.firstUser = Convert.ToInt32((en2 as Player).AccountId);
                    con2.Inventory[packet.SlotObject2.SlotId] = newItem;
                } else
                con2.Inventory[packet.SlotObject2.SlotId] = item1;
                
                if (item2 != null)
                {
                    if (publicbags.Contains(en1.ObjectType) && item2.Soulbound)
                    {
                        client.Player.DropBag(item2);
                        con1.Inventory[packet.SlotObject1.SlotId] = null;
                    }
                }
                if (item1 != null)
                {
                    if (publicbags.Contains(en2.ObjectType) && item1.Soulbound)
                    {
                        client.Player.DropBag(item1);
                        con2.Inventory[packet.SlotObject2.SlotId] = null;
                    }
                }

                en1.UpdateCount++;
                en2.UpdateCount++;

                if (en1 is Player)
                {
                    if (en1.Owner.Name == "Vault")
                        (en1 as Player).Client.Save();
                    (en1 as Player).CalcBoost();
                    (en1 as Player).Client.SendPacket(new InvResultPacket {Result = 0});
                }
                if (en2 is Player)
                {
                    if (en2.Owner.Name == "Vault")
                        (en2 as Player).Client.Save();
                    (en2 as Player).CalcBoost();
                    (en2 as Player).Client.SendPacket(new InvResultPacket {Result = 0});
                }

                if (client.Player.Owner is Vault)
                    if ((client.Player.Owner as Vault).PlayerOwnerName == client.Account.Name)
                        return;
                
                client.Save();
            }, PendingPriority.Networking);
        }

        private bool IsValid(Item item1, Item item2, IContainer con1, IContainer con2, InvSwapPacket packet, Client client)
        {
            if (con2 is Container || con2 is OneWayContainer)
                return true;

            bool ret = false;

            if (con1 is OneWayContainer || con1 is Container)
            {
                ret = con2.AuditItem(item1, packet.SlotObject2.SlotId);

                if (!ret)
                {
                    Program.writeError($"Cheat engine detected for player {client.Player.Name},\nInvalid InvSwap. {client.Manager.GameData.Items[packet.SlotObject1.ObjectType].ObjectId} instead of {item1.ObjectId}");
                    client.Player.kickforCheats(Player.possibleExploit.INAVLID_INVSWAP);
                }
            }
            if (con1 is Player && con2 is Player)
            {
                ret = con1.AuditItem(item1, packet.SlotObject2.SlotId) && con2.AuditItem(item2, packet.SlotObject1.SlotId);

                if (!ret)
                {
                    Program.writeError($"Cheat engine detected for player {client.Player.Name},\nInvalid InvSwap. {item1.ObjectId} instead of {client.Manager.GameData.Items[packet.SlotObject2.ObjectType].ObjectId}");
                    client.Player.kickforCheats(Player.possibleExploit.INAVLID_INVSWAP);
                }
            }

            return ret;
        }
    }
}