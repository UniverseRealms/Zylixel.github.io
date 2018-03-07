﻿#region

using System;
using System.Collections.Generic;
using System.Linq;
using wServer.logic;
using wServer.realm.entities;
using wServer.realm.worlds;

#endregion

namespace wServer.realm
{
    public class RealmPortalMonitor
    {

        private readonly RealmManager manager;
        private readonly Nexus nexus;
        private readonly Random rand = new Random();
        private readonly object worldLock = new object();
        public Dictionary<World, Portal> portals = new Dictionary<World, Portal>();

        public RealmPortalMonitor(RealmManager manager)
        {
            if (CheckConfig.IsDebugOn())
                Console.WriteLine("Initalizing Portal Monitor...");
            this.manager = manager;
            nexus = manager.Worlds[World.NEXUS_ID] as Nexus;
            lock (worldLock)
                foreach (KeyValuePair<int, World> i in manager.Worlds)
                {
                    if (i.Value is GameWorld)
                        WorldAdded(i.Value);
                }
            if (CheckConfig.IsDebugOn())
                Console.WriteLine("Portal Monitor initialized.");
        }

        private Position GetRandPosition()
        {
            int x, y;
            do
            {
                x = rand.Next(0, nexus.Map.Width);
                y = rand.Next(0, nexus.Map.Height);
            } while (
                portals.Values.Any(_ => _.X == x && _.Y == y) ||
                nexus.Map[x, y].Region != TileRegion.Realm_Portals);
            return new Position {X = x, Y = y};
        }

        public void WorldAdded(World world)
        {
            lock (worldLock)
            {
                Position pos = GetRandPosition();
                Portal portal = new Portal(manager, 0x0712, null)
                {
                    Size = 80,
                    WorldInstance = world,
                    Name = world.Name
                };
                portal.Move(pos.X + 0.5f, pos.Y + 0.5f);
                nexus.EnterWorld(portal);
                portals.Add(world, portal);
                if (CheckConfig.IsDebugOn())
                    Console.WriteLine("World {0}({1}) added to monitor.", world.Id, world.Name);
            }
        }

        public bool AddPortal(World world)
        {
            lock (worldLock)
            {
                if (portals.ContainsKey(world))
                    return false;
                
                if (world == null)
                    return false;

                Position pos = GetRandPosition();
                Portal portal = new Portal(manager, 0x0712, null)
                {
                    Size = 80,
                    WorldInstance = world,
                    Name = world.Name + " Link"
                };

                portal.Move(pos.X + 0.5f, pos.Y + 0.5f);
                nexus.EnterWorld(portal);
                portals.Add(world, portal);
                RealmManager.CurrentPortalNames.Add(world.Name + " Link");
                world.isLinked = true;
                if (CheckConfig.IsDebugOn())
                    Console.WriteLine("World {0}({1}) added to monitor.", world.Id, world.Name);
                foreach (var i in manager.Clients.Values)
                    if (i.Player.Owner != world)
                            i.Player.SendInfo($"A Link to {world.Name} has spawned in the nexus!");
                return true;
            }
        }

        public bool RemovePortal(World world)
        {
            if (world == null)
                return false;

            lock (worldLock)
            {
                var portal = portals.FirstOrDefault(p => p.Value.WorldInstance == world);
                if (portal.Value == null)
                    return false;

                if (CheckConfig.IsDebugOn())
                    Console.WriteLine($"Portal {portal.Key}({portal.Value.WorldInstance.Name}) removed.");

                nexus.LeaveWorld(portal.Value);
                portals.Remove(portal.Key);
                world.isLinked = false;
                return true;
            }
        }

        public void WorldRemoved(World world)
        {
            lock (worldLock)
            {
                if (portals.ContainsKey(world))
                {
                    Portal portal = portals[world];
                    nexus.LeaveWorld(portal);
                    RealmManager.Realms.Add(portal.PortalName);
                    RealmManager.CurrentPortalNames.Remove(portal.PortalName);
                    portals.Remove(world);
                    if (CheckConfig.IsDebugOn())
                        Console.WriteLine("World {0}({1}) removed from monitor.", world.Id, world.Name);
                }
            }
        }

        public void WorldClosed(World world)
        {
            lock (worldLock)
            {
                Portal portal = portals[world];
                nexus.LeaveWorld(portal);
                portals.Remove(world);
                if (CheckConfig.IsDebugOn())
                    Console.WriteLine("World {0}({1}) closed.", world.Id, world.Name);
            }
        }

        public void WorldOpened(World world)
        {
            lock (worldLock)
            {
                Position pos = GetRandPosition();
                Portal portal = new Portal(manager, 0x71c, null)
                {
                    Size = 150,
                    WorldInstance = world,
                    Name = world.Name
                };
                portal.Move(pos.X, pos.Y);
                nexus.EnterWorld(portal);
                portals.Add(world, portal);
                if (CheckConfig.IsDebugOn())
                    Console.WriteLine("World {0}({1}) opened.", world.Id, world.Name);
            }
        }

        public World GetRandomRealm()
        {
            lock (worldLock)
            {
                World[] worlds = portals.Keys.ToArray();
                if (worlds.Length == 0)
                    return manager.Worlds[World.NEXUS_ID];
                return worlds[Environment.TickCount%worlds.Length];
            }
        }
    }
}