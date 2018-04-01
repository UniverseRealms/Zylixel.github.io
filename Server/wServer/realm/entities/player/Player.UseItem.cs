﻿#region

using System;
using System.Collections.Generic;
using System.Linq;
using db.data;
using wServer.networking;
using wServer.networking.cliPackets;
using wServer.networking.svrPackets;

#endregion

namespace wServer.realm.entities.player
{
    partial class Player
    {
        private static readonly ConditionEffect[] NegativeEffs =
        {
            new ConditionEffect
            {
                Effect = ConditionEffectIndex.Slowed,
                DurationMS = 0
            },
            new ConditionEffect
            {
                Effect = ConditionEffectIndex.Paralyzed,
                DurationMS = 0
            },
            new ConditionEffect
            {
                Effect = ConditionEffectIndex.Weak,
                DurationMS = 0
            },
            new ConditionEffect
            {
                Effect = ConditionEffectIndex.Stunned,
                DurationMS = 0
            },
            new ConditionEffect
            {
                Effect = ConditionEffectIndex.Confused,
                DurationMS = 0
            },
            new ConditionEffect
            {
                Effect = ConditionEffectIndex.Blind,
                DurationMS = 0
            },
            new ConditionEffect
            {
                Effect = ConditionEffectIndex.Quiet,
                DurationMS = 0
            },
            new ConditionEffect
            {
                Effect = ConditionEffectIndex.ArmorBroken,
                DurationMS = 0
            },
            new ConditionEffect
            {
                Effect = ConditionEffectIndex.Bleeding,
                DurationMS = 0
            },
            new ConditionEffect
            {
                Effect = ConditionEffectIndex.Dazed,
                DurationMS = 0
            },
            new ConditionEffect
            {
                Effect = ConditionEffectIndex.Sick,
                DurationMS = 0
            },
            new ConditionEffect
            {
                Effect = ConditionEffectIndex.Drunk,
                DurationMS = 0
            },
            new ConditionEffect
            {
                Effect = ConditionEffectIndex.Hallucinating,
                DurationMS = 0
            },
            new ConditionEffect
            {
                Effect = ConditionEffectIndex.Hexed,
                DurationMS = 0
            },
            new ConditionEffect
            {
                Effect = ConditionEffectIndex.Unstable,
                DurationMS = 0
            }
        };
        
        public static int Oldstat { get; set; }

        public static Position Targetlink { get; set; }
        public int PetIdLookup { get; private set; }
        public Entity Ground { get; private set; }
        
        public bool canChangePetSkin;

        public static void ActivateHealHp(Player player, int amount, List<Packet> pkts)
        {
            var maxHp = player.Stats[0] + player.Boost[0];
            var newHp = Math.Min(maxHp, player.HP + amount);
            if (newHp != player.HP)
            {
                pkts.Add(new ShowEffectPacket
                {
                    EffectType = EffectType.Potion,
                    TargetId = player.Id,
                    Color = new ARGB(0xffffffff)
                });
                pkts.Add(new NotificationPacket
                {
                    Color = new ARGB(0xff00ff00),
                    ObjectId = player.Id,
                    Text = "{\"key\":\"blank\",\"tokens\":{\"data\":\"+" + (newHp - player.HP) + "\"}}"
                    //"+" + (newHp - player.HP)
                });
                player.HP = newHp;
                player.UpdateCount++;
            }
        }

        private static void ActivateHealMp(Player player, int amount, List<Packet> pkts)
        {
            var maxMp = player.Stats[1] + player.Boost[1];
            var newMp = Math.Min(maxMp, player.Mp + amount);
            if (newMp != player.Mp)
            {
                pkts.Add(new ShowEffectPacket
                {
                    EffectType = EffectType.Potion,
                    TargetId = player.Id,
                    Color = new ARGB(0x6084e0)
                });
                pkts.Add(new NotificationPacket
                {
                    Color = new ARGB(0x6084e0),
                    ObjectId = player.Id,
                    Text = "{\"key\":\"blank\",\"tokens\":{\"data\":\"+" + (newMp - player.Mp) + "\"}}"
                });
                player.Mp = newMp;
                player.UpdateCount++;
            }
        }

        private void ActivateShoot(RealmTime time, OldItem item, Position target)
        {
            var arcGap = item.ArcGap * Math.PI / 180;
            var startAngle = Math.Atan2(target.Y - Y, target.X - X) - (item.NumProjectiles - 1) / 2 * arcGap;
            var prjDesc = item.Projectiles[0]; //Assume only one
            for (var i = 0; i < item.NumProjectiles; i++)
            {
                var proj = CreateProjectile(prjDesc, item.ObjectType,
                    (int) StatsManager.GetAttackDamage(prjDesc.MinDamage, prjDesc.MaxDamage),
                    time.tickTimes, new Position {X = X, Y = Y}, (float) (startAngle + arcGap * i));
                Owner.EnterWorld(proj);
                FameCounter.Shoot(proj);
            }
        }

        private void PoisonEnemy(Enemy enemy, ActivateEffect eff)
        {
            try
            {
                if (eff.ConditionEffect != null)
                    enemy.ApplyConditionEffect(new ConditionEffect
                    {
                        Effect = (ConditionEffectIndex) eff.ConditionEffect,
                        DurationMS = (int) eff.EffectDuration
                    });
                var remainingDmg =
                    (int) StatsManager.GetDefenseDamage(enemy, eff.TotalDamage, enemy.ObjectDesc.Defense);
                var perDmg = remainingDmg * 1000 / eff.DurationMS;
                WorldTimer tmr = null;
                var x = 0;
                tmr = new WorldTimer(100, (w, t) =>
                {
                    if (enemy.Owner == null) return;
                    w.BroadcastPacket(new ShowEffectPacket
                    {
                        EffectType = EffectType.Dead,
                        TargetId = enemy.Id,
                        Color = new ARGB(0xffddff00)
                    }, null);
                    if (x % 10 == 0)
                    {
                        int thisDmg;
                        if (remainingDmg < perDmg) thisDmg = remainingDmg;
                        else thisDmg = perDmg;
                        enemy.Damage(this, t, thisDmg, true);
                        remainingDmg -= thisDmg;
                        if (remainingDmg <= 0) return;
                    }
                    x++;
                    tmr.Reset();
                    Manager.Logic.AddPendingAction(_ => w.Timers.Add(tmr), PendingPriority.Creation);
                });
                Owner.Timers.Add(tmr);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        public bool Activate(RealmTime time, OldItem item, UseItemPacket pkt, XmlData data)
        {
            var endMethod = false;
            var target = pkt.ItemUsePos;

            if (Mp < item.MpCost)
                return false;

            Mp -= item.MpCost;
            
            var con = Owner.GetEntity(pkt.SlotObject.ObjectId) as IContainer;
            if (con == null) return true;
            if (pkt.SlotObject.SlotId != 255 && pkt.SlotObject.SlotId != 254 &&
                con.Inventory[pkt.SlotObject.SlotId].ObjectType != item.ObjectType)
            {
                Console.WriteLine("Cheat engine detected for player {0},\nItem should be {1}, but its {2}.",
                    Name, Inventory[pkt.SlotObject.SlotId].ObjectId, item.ObjectId);
                kickforCheats(possibleExploit.DIFF_ITEM);
                return true;
            }
            if (item.Maxy)
            {
                if (Client?.Player?.Stats == null)
                    return true;
                Client.Player.Stats[0] = Client.Player.ObjectDesc.MaxHitPoints;
                Client.Player.Stats[1] = Client.Player.ObjectDesc.MaxMagicPoints;
                Client.Player.Stats[2] = Client.Player.ObjectDesc.MaxAttack;
                Client.Player.Stats[3] = Client.Player.ObjectDesc.MaxDefense;
                Client.Player.Stats[4] = Client.Player.ObjectDesc.MaxSpeed;
                Client.Player.Stats[5] = Client.Player.ObjectDesc.MaxHpRegen;
                Client.Player.Stats[6] = Client.Player.ObjectDesc.MaxMpRegen;
                Client.Player.Stats[7] = Client.Player.ObjectDesc.MaxDexterity;
                Client.Player.SaveToCharacter();
                Client.Player.Client.Save();
                Client.Player.UpdateCount++;
            }
            if (item.IsBackpack)
            {
                if (HasBackpack) return true;
                Client.Character.Backpack = new[] {-1, -1, -1, -1, -1, -1, -1, -1};
                HasBackpack = true;
                Client.Character.HasBackpack = 1;
                Manager.Database.DoActionAsync(db =>
                    db.SaveBackpacks(Client.Character, Client.Account));
                Array.Resize(ref _inventory, 20);
                var slotTypes =
                    Utils.FromCommaSepString32(
                        Manager.GameData.ObjectTypeToElement[ObjectType].Element("SlotTypes").Value);
                Array.Resize(ref slotTypes, 20);
                for (var i = 0; i < slotTypes.Length; i++)
                    if (slotTypes[i] == 0) slotTypes[i] = 10;
                SlotTypes = slotTypes;
                return false;
            }
            if (item.XpBooster)
            {
                if (!XpBoosted)
                {
                    XpBoostTimeLeft = (float) item.Timer;
                    XpBoosted = item.XpBooster;
                    _xpFreeTimer = (float) item.Timer == -1.0 ? false : true;
                    return false;
                }
                {
                    SendInfo("You have already an active XP Booster.");
                    return true;
                }
            }
            if (item.LootDropBooster)
            {
                if (!LootDropBoost)
                {
                    LootDropBoostTimeLeft = (float) item.Timer;
                    _lootDropBoostFreeTimer = (float) item.Timer == -1.0 ? false : true;
                    return false;
                }
                {
                    SendInfo("You have already an active Loot Drop Booster.");
                    return true;
                }
            }
            if (item.LootTierBooster)
            {
                if (!LootTierBoost)
                {
                    LootTierBoostTimeLeft = (float) item.Timer;
                    _lootTierBoostFreeTimer = (float) item.Timer == -1.0 ? false : true;
                    return false;
                }
                {
                    SendInfo("You have already an active Loot Tier Booster.");
                    return true;
                }
            }
            foreach (var eff in item.ActivateEffects)
                switch (eff.Effect)
                {
                    case ActivateEffects.BulletNova:
                    {
                        var prjDesc = item.Projectiles[0]; //Assume only one
                        var batch = new Packet[21];
                        var s = Random.CurrentSeed;
                        Random.CurrentSeed = (uint) (s * time.tickTimes);
                        for (var i = 0; i < 20; i++)
                        {
                            var proj = CreateProjectile(prjDesc, item.ObjectType,
                                (int) StatsManager.GetAttackDamage(prjDesc.MinDamage, prjDesc.MaxDamage),
                                time.tickTimes, target, (float) (i * (Math.PI * 2) / 20));
                            Owner.EnterWorld(proj);
                            FameCounter.Shoot(proj);
                            batch[i] = new Shoot2Packet
                            {
                                BulletId = proj.ProjectileId,
                                OwnerId = Id,
                                ContainerType = item.ObjectType,
                                StartingPos = target,
                                Angle = proj.Angle,
                                Damage = (short) proj.Damage
                            };
                        }
                        Random.CurrentSeed = s;
                        batch[20] = new ShowEffectPacket
                        {
                            EffectType = EffectType.Trail,
                            PosA = target,
                            TargetId = Id,
                            Color = new ARGB(0xFFFF00AA)
                        };
                        BroadcastSync(batch, p => this.Dist(p) < 35);
                    }
                        break;
                    case ActivateEffects.Shoot:
                    {
                        ActivateShoot(time, item, target);
                    }
                        break;
                    case ActivateEffects.StatBoostSelf:
                    {
                        var idx = -1;
                        if (eff.Stats == StatsType.MaximumHp) idx = 0;
                            else if (eff.Stats == StatsType.MaximumMp) idx = 1;
                            else if (eff.Stats == StatsType.Attack) idx = 2;
                            else if (eff.Stats == StatsType.Defense) idx = 3;
                            else if (eff.Stats == StatsType.Speed) idx = 4;
                            else if (eff.Stats == StatsType.Vitality) idx = 5;
                            else if (eff.Stats == StatsType.Wisdom) idx = 6;
                            else if (eff.Stats == StatsType.Dexterity) idx = 7;
                        if (idx == -1) return false;
                        tempBoost[idx] += eff.Amount;
                        ApplyConditionEffect(new ConditionEffect
                        {
                            DurationMS = eff.DurationMS,
                            Effect = (ConditionEffectIndex) idx + 39
                        });
                        CalcBoost();
                        UpdateCount++;
                        Owner.Timers.Add(new WorldTimer(eff.DurationMS, (world, t) =>
                        {
                            tempBoost[idx] -= eff.Amount;
                            CalcBoost();
                            UpdateCount++;
                        }));
                        Owner.BroadcastPacket(new ShowEffectPacket
                        {
                            EffectType = EffectType.Potion,
                            TargetId = Id,
                            Color = new ARGB(0xffffffff)
                        }, null);
                    }
                        break;
                    case ActivateEffects.StatBoostAura:
                    {
                        var amountSba = eff.Amount;
                        var durationSba = eff.DurationMS;
                        var rangeSba = eff.Range;
                        var idx = -1;
                        if (eff.Stats == StatsType.MaximumHp) idx = 0;
                        if (eff.Stats == StatsType.MaximumMp) idx = 1;
                        if (eff.Stats == StatsType.Attack) idx = 2;
                        if (eff.Stats == StatsType.Defense) idx = 3;
                        if (eff.Stats == StatsType.Speed) idx = 4;
                        if (eff.Stats == StatsType.Vitality) idx = 5;
                        if (eff.Stats == StatsType.Wisdom) idx = 6;
                        if (eff.Stats == StatsType.Dexterity) idx = 7;
                        var bit = idx + 39;
                        if (eff.UseWisMod)
                        {
                            amountSba = (int) UseWisMod(eff.Amount, 0);
                            durationSba = (int) (UseWisMod(eff.DurationSec) * 1000);
                            rangeSba = UseWisMod(eff.Range);
                        }
                        if (HasConditionEffect(ConditionEffectIndex.HPBoost))
                            if (amountSba >= 1)
                                return false;
                        this.Aoe(rangeSba, true, player =>
                        {
                            ApplyConditionEffect(new ConditionEffect
                            {
                                DurationMS = durationSba,
                                Effect = (ConditionEffectIndex) bit
                            });
                            (player as Player).tempBoost[idx] += amountSba;
                            player.UpdateCount++;
                            Owner.Timers.Add(new WorldTimer(durationSba, (world, t) =>
                            {
                                (player as Player).tempBoost[idx] -= amountSba;
                                player.UpdateCount++;
                            }));
                        });
                        BroadcastSync(new ShowEffectPacket
                        {
                            EffectType = EffectType.AreaBlast,
                            TargetId = Id,
                            Color = new ARGB(0xffffffff),
                            PosA = new Position {X = rangeSba}
                        }, p => this.Dist(p) < 25);
                    }
                        break;
                    case ActivateEffects.ConditionEffectSelf:
                    {
                        var durationCes = eff.DurationMS;
                        if (eff.UseWisMod)
                            durationCes = (int) (UseWisMod(eff.DurationSec) * 1000);
                        var color = 0xffffffff;
                        switch (eff.ConditionEffect.Value)
                        {
                            case ConditionEffectIndex.Damaging:
                                color = 0xffff0000;
                                break;
                            case ConditionEffectIndex.Berserk:
                                color = 0x808080;
                                break;
                        }
                        ApplyConditionEffect(new ConditionEffect
                        {
                            Effect = eff.ConditionEffect.Value,
                            DurationMS = durationCes
                        });
                        Owner.BroadcastPacket(new ShowEffectPacket
                        {
                            EffectType = EffectType.AreaBlast,
                            TargetId = Id,
                            Color = new ARGB(color),
                            PosA = new Position {X = 2F}
                        }, null);
                    }
                        break;
                    case ActivateEffects.ConditionEffectAura:
                    {
                        var durationCea = eff.DurationMS;
                        var rangeCea = eff.Range;
                        if (eff.UseWisMod)
                        {
                            durationCea = (int) (UseWisMod(eff.DurationSec) * 1000);
                            rangeCea = UseWisMod(eff.Range);
                        }
                        this.Aoe(rangeCea, true, player =>
                        {
                            player.ApplyConditionEffect(new ConditionEffect
                            {
                                Effect = eff.ConditionEffect.Value,
                                DurationMS = durationCea
                            });
                        });
                        var color = 0xffffffff;
                        switch (eff.ConditionEffect.Value)
                        {
                            case ConditionEffectIndex.Damaging:
                                color = 0xffff0000;
                                break;
                            case ConditionEffectIndex.Berserk:
                                color = 0x808080;
                                break;
                        }
                        BroadcastSync(new ShowEffectPacket
                        {
                            EffectType = EffectType.AreaBlast,
                            TargetId = Id,
                            Color = new ARGB(color),
                            PosA = new Position {X = rangeCea}
                        }, p => this.Dist(p) < 25);
                    }
                        break;
                    case ActivateEffects.Heal:
                    {
                        var pkts = new List<Packet>();
                        ActivateHealHp(this, eff.Amount, pkts);
                        Owner.BroadcastPackets(pkts, null);
                    }
                        break;
                    case ActivateEffects.HealNova:
                    {
                        var amountHn = eff.Amount;
                        var rangeHn = eff.Range;
                        if (eff.UseWisMod)
                        {
                            amountHn = (int) UseWisMod(eff.Amount, 0);
                            rangeHn = UseWisMod(eff.Range);
                        }
                        var pkts = new List<Packet>();
                        this.Aoe(rangeHn, true, player => { ActivateHealHp(player as Player, amountHn, pkts); });
                        pkts.Add(new ShowEffectPacket
                        {
                            EffectType = EffectType.AreaBlast,
                            TargetId = Id,
                            Color = new ARGB(0xffffffff),
                            PosA = new Position {X = rangeHn}
                        });
                        BroadcastSync(pkts, p => this.Dist(p) < 25);
                    }
                        break;
                    case ActivateEffects.Magic:
                    {
                        var pkts = new List<Packet>();
                        ActivateHealMp(this, eff.Amount, pkts);
                        Owner.BroadcastPackets(pkts, null);
                    }
                        break;
                    case ActivateEffects.MagicNova:
                    {
                        var pkts = new List<Packet>();
                        this.Aoe(eff.Range / 2, true,
                            player => { ActivateHealMp(player as Player, eff.Amount, pkts); });
                        pkts.Add(new ShowEffectPacket
                        {
                            EffectType = EffectType.AreaBlast,
                            TargetId = Id,
                            Color = new ARGB(0xffffffff),
                            PosA = new Position {X = eff.Range}
                        });
                        Owner.BroadcastPackets(pkts, null);
                    }
                        break;
                    case ActivateEffects.Teleport:
                    {
                        Move(target.X, target.Y);
                        UpdateCount++;
                        Owner.BroadcastPackets(new Packet[]
                        {
                            new GotoPacket
                            {
                                ObjectId = Id,
                                Position = new Position
                                {
                                    X = X,
                                    Y = Y
                                }
                            },
                            new ShowEffectPacket
                            {
                                EffectType = EffectType.Teleport,
                                TargetId = Id,
                                PosA = new Position
                                {
                                    X = X,
                                    Y = Y
                                },
                                Color = new ARGB(0xFFFFFFFF)
                            }
                        }, null);
                    }
                        break;
                    case ActivateEffects.VampireBlast:
                    {
                        var pkts = new List<Packet>();
                        pkts.Add(new ShowEffectPacket
                        {
                            EffectType = EffectType.Trail,
                            TargetId = Id,
                            PosA = target,
                            Color = new ARGB(0xFFFF0000)
                        });
                        pkts.Add(new ShowEffectPacket
                        {
                            EffectType = EffectType.Diffuse,
                            Color = new ARGB(0xFFFF0000),
                            TargetId = Id,
                            PosA = target,
                            PosB = new Position {X = target.X + eff.Radius, Y = target.Y}
                        });
                        var totalDmg = 0;
                        var enemies = new List<Enemy>();
                        Owner.Aoe(target, eff.Radius, false, enemy =>
                        {
                            enemies.Add(enemy as Enemy);
                            totalDmg += (enemy as Enemy).Damage(this, time, eff.TotalDamage, false);
                        });
                        var players = new List<Player>();
                        this.Aoe(eff.Radius, true, player =>
                        {
                            players.Add(player as Player);
                            ActivateHealHp(player as Player, totalDmg, pkts);
                        });
                        if (enemies.Count > 0)
                        {
                            var rand = new Random();
                            for (var i = 0; i < 5; i++)
                            {
                                var a = enemies[rand.Next(0, enemies.Count)];
                                var b = players[rand.Next(0, players.Count)];
                                pkts.Add(new ShowEffectPacket
                                {
                                    EffectType = EffectType.Flow,
                                    TargetId = b.Id,
                                    PosA = new Position {X = a.X, Y = a.Y},
                                    Color = new ARGB(0xffffffff)
                                });
                            }
                        }
                        BroadcastSync(pkts, p => this.Dist(p) < 25);
                    }
                        break;
                    case ActivateEffects.Trap:
                    {
                        BroadcastSync(new ShowEffectPacket
                        {
                            EffectType = EffectType.Throw,
                            Color = new ARGB(0xff9000ff),
                            TargetId = Id,
                            PosA = target
                        }, p => this.Dist(p) < 25);
                        Owner.Timers.Add(new WorldTimer(1500, (world, t) =>
                        {
                            var trap = new Trap(
                                this,
                                eff.Radius,
                                eff.TotalDamage,
                                eff.ConditionEffect ?? ConditionEffectIndex.Slowed,
                                eff.EffectDuration);
                            trap.Move(target.X, target.Y);
                            world.EnterWorld(trap);
                        }));
                    }
                        break;
                    case ActivateEffects.StasisBlast:
                    {
                        var pkts = new List<Packet>();
                        pkts.Add(new ShowEffectPacket
                        {
                            EffectType = EffectType.Concentrate,
                            TargetId = Id,
                            PosA = target,
                            PosB = new Position {X = target.X + 3, Y = target.Y},
                            Color = new ARGB(0xFF00D0)
                        });
                        Owner.Aoe(target, 3, false, enemy =>
                        {
                            if (IsSpecial(enemy.ObjectType)) return;
                            if (enemy.HasConditionEffect(ConditionEffectIndex.StasisImmune))
                            {
                                if (!enemy.HasConditionEffect(ConditionEffectIndex.Invincible))
                                    pkts.Add(new NotificationPacket
                                    {
                                        ObjectId = enemy.Id,
                                        Color = new ARGB(0xff00ff00),
                                        Text = "{\"key\":\"blank\",\"tokens\":{\"data\":\"Immune\"}}"
                                    });
                            }
                            else if (!enemy.HasConditionEffect(ConditionEffectIndex.Stasis))
                            {
                                enemy.ApplyConditionEffect(new ConditionEffect
                                {
                                    Effect = ConditionEffectIndex.Stasis,
                                    DurationMS = eff.DurationMS
                                });
                                Owner.Timers.Add(new WorldTimer(eff.DurationMS, (world, t) =>
                                {
                                    enemy.ApplyConditionEffect(new ConditionEffect
                                    {
                                        Effect = ConditionEffectIndex.StasisImmune,
                                        DurationMS = 3000
                                    });
                                }));
                                pkts.Add(new NotificationPacket
                                {
                                    ObjectId = enemy.Id,
                                    Color = new ARGB(0xffff0000),
                                    Text = "{\"key\":\"blank\",\"tokens\":{\"data\":\"Stasis\"}}"
                                });
                            }
                        });
                        BroadcastSync(pkts, p => this.Dist(p) < 25);
                    }
                        break;
                    case ActivateEffects.Decoy:
                    {
                        var decoy = new Decoy(Manager, this, eff.DurationMS, StatsManager.GetSpeed(), eff.random, eff.explode);
                        decoy.Move(X, Y);
                        Owner.EnterWorld(decoy);
                    }
                        break;
                    case ActivateEffects.Lightning:
                    {
                        Enemy start = null;
                        var angle = Math.Atan2(target.Y - Y, target.X - X);
                        var diff = Math.PI / 3;
                        Owner.Aoe(target, 6, false, enemy =>
                        {
                            if (!(enemy is Enemy)) return;
                            var x = Math.Atan2(enemy.Y - Y, enemy.X - X);
                            if (Math.Abs(angle - x) < diff)
                            {
                                start = enemy as Enemy;
                                diff = Math.Abs(angle - x);
                            }
                        });
                        if (start == null)
                            break;
                        var current = start;
                        var targets = new Enemy[eff.MaxTargets];
                        for (var i = 0; i < targets.Length; i++)
                        {
                            targets[i] = current;
                            var next = current.GetNearestEntity(8, false,
                                enemy =>
                                    enemy is Enemy &&
                                    Array.IndexOf(targets, enemy) == -1 &&
                                    this.Dist(enemy) <= 6) as Enemy;
                            if (next == null) break;
                            current = next;
                        }
                        var pkts = new List<Packet>();
                        for (var i = 0; i < targets.Length; i++)
                        {
                            if (targets[i] == null) break;
                            if (targets[i].HasConditionEffect(ConditionEffectIndex.Invincible)) continue;
                            var prev = i == 0 ? (Entity) this : targets[i - 1];
                            targets[i].Damage(this, time, eff.TotalDamage, false);
                            if (eff.ConditionEffect != null)
                                targets[i].ApplyConditionEffect(new ConditionEffect
                                {
                                    Effect = eff.ConditionEffect.Value,
                                    DurationMS = (int) (eff.EffectDuration * 1000)
                                });
                            pkts.Add(new ShowEffectPacket
                            {
                                EffectType = EffectType.Lightning,
                                TargetId = prev.Id,
                                Color = new ARGB(0xffff0088),
                                PosA = new Position
                                {
                                    X = targets[i].X,
                                    Y = targets[i].Y
                                },
                                PosB = new Position {X = 350}
                            });
                        }
                        BroadcastSync(pkts, p => this.Dist(p) < 25);
                    }
                        break;
                    case ActivateEffects.PoisonGrenade:
                    {
                        try
                        {
                            BroadcastSync(new ShowEffectPacket
                            {
                                EffectType = EffectType.Throw,
                                Color = new ARGB(0xffddff00),
                                TargetId = Id,
                                PosA = target
                            }, p => this.Dist(p) < 25);
                            var x = new Placeholder(Manager, 1500);
                            x.Move(target.X, target.Y);
                            Owner.EnterWorld(x);
                            try
                            {
                                Owner.Timers.Add(new WorldTimer(1500, (world, t) =>
                                {
                                    world.BroadcastPacket(new ShowEffectPacket
                                    {
                                        EffectType = EffectType.AreaBlast,
                                        Color = new ARGB(0xffddff00),
                                        TargetId = x.Id,
                                        PosA = new Position {X = eff.Radius}
                                    }, null);
                                    world.Aoe(target, eff.Radius, false,
                                        enemy => PoisonEnemy(enemy as Enemy, eff));
                                }));
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine("Poison ShowEffect:\n{0}", ex);
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("Poisons General:\n{0}", ex);
                        }
                    }
                        break;
                    case ActivateEffects.RemoveNegativeConditions:
                    {
                        this.Aoe(eff.Range / 2, true, player => { ApplyConditionEffect(NegativeEffs); });
                        BroadcastSync(new ShowEffectPacket
                        {
                            EffectType = EffectType.AreaBlast,
                            TargetId = Id,
                            Color = new ARGB(0xffffffff),
                            PosA = new Position {X = eff.Range / 2}
                        }, p => this.Dist(p) < 25);
                    }
                        break;
                    case ActivateEffects.RemoveNegativeConditionsSelf:
                    {
                        ApplyConditionEffect(NegativeEffs);
                        Owner.BroadcastPacket(new ShowEffectPacket
                        {
                            EffectType = EffectType.AreaBlast,
                            TargetId = Id,
                            Color = new ARGB(0xffffffff),
                            PosA = new Position {X = 1}
                        }, null);
                    }
                        break;
                    case ActivateEffects.IncrementStat:
                    {
                        var idx = -1;
                        if (eff.Stats == StatsType.MaximumHp) idx = 0;
                        else if (eff.Stats == StatsType.MaximumMp) idx = 1;
                        else if (eff.Stats == StatsType.Attack) idx = 2;
                        else if (eff.Stats == StatsType.Defense) idx = 3;
                        else if (eff.Stats == StatsType.Speed) idx = 4;
                        else if (eff.Stats == StatsType.Vitality) idx = 5;
                        else if (eff.Stats == StatsType.Wisdom) idx = 6;
                        else if (eff.Stats == StatsType.Dexterity) idx = 7;
                        Stats[idx] += eff.Amount;
                        var limit =
                            int.Parse(
                                Manager.GameData.ObjectTypeToElement[ObjectType].Element(
                                        StatsManager.StatsIndexToName(idx))
                                    .Attribute("max")
                                    .Value);
                        if (Stats[idx] > limit)
                            Stats[idx] = limit;
                        UpdateCount++;
                    }
                        break;
                    case ActivateEffects.UnlockPortal:
                        var portal =
                            this.GetNearestEntity(5, Manager.GameData.IdToObjectType[eff.LockedName]) as Portal;
                        var packets = new Packet[3];
                        packets[0] = new ShowEffectPacket
                        {
                            EffectType = EffectType.AreaBlast,
                            Color = new ARGB(0xFFFFFF),
                            PosA = new Position {X = 5},
                            TargetId = Id
                        };
                        if (portal == null) break;
                        portal.Unlock(eff.DungeonName);
                        packets[1] = new NotificationPacket
                        {
                            Color = new ARGB(0x00FF00),
                            Text =
                                "{\"key\":\"blank\",\"tokens\":{\"data\":\"Unlocked by " +
                                Name + "\"}}",
                            ObjectId = Id
                        };
                        packets[2] = new TextPacket
                        {
                            BubbleTime = 0,
                            Stars = -1,
                            Name = "",
                            Text = eff.DungeonName + " Unlocked by " + Name + "."
                        };
                        BroadcastSync(packets);
                        break;
                    case ActivateEffects.Create:
                    {
                        ushort objType;
                        if (!Manager.GameData.IdToObjectType.TryGetValue(eff.Id, out objType) ||
                            !Manager.GameData.Portals.ContainsKey(objType))
                            break;
                        var entity = Resolve(Manager, objType);
                        var w = Manager.GetWorld(Owner.Id);
                        var timeoutTime = Manager.GameData.Portals[objType].TimeoutTime;
                        var dungName = Manager.GameData.Portals[objType].DungeonName;
                        var c = new ARGB(0x00FF00);
                        entity.Move(X, Y);
                        w.EnterWorld(entity);
                        w.BroadcastPacket(new NotificationPacket
                        {
                            Color = c,
                            Text =
                                dungName + " opened by " +
                                Client.Account.Name + "\"",
                            ObjectId = Client.Player.Id
                        }, null);
                        w.BroadcastPacket(new TextPacket
                        {
                            BubbleTime = 0,
                            Stars = -1,
                            Name = "",
                            Text = dungName + " opened by " + Client.Account.Name
                        }, null);
                        w.Timers.Add(new WorldTimer(timeoutTime * 1000,
                            (world, t) =>
                            {
                                try
                                {
                                    w.LeaveWorld(entity);
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"Couldn't despawn portal.\n{ex}");
                                }
                            }));
                    }
                        break;
                    case ActivateEffects.Dye:
                    {
                        if (item.Texture1 != 0)
                            Texture1 = item.Texture1;
                        if (item.Texture2 != 0)
                            Texture2 = item.Texture2;
                        SaveToCharacter();
                    }
                        break;
                    case ActivateEffects.ShurikenAbility:
                    {
                        if (!_ninjaShoot)
                        {
                            ApplyConditionEffect(new ConditionEffect
                            {
                                Effect = ConditionEffectIndex.Speedy,
                                DurationMS = -1
                            });
                            _ninjaFreeTimer = true;
                            _ninjaShoot = true;
                        }
                        else
                        {
                            ApplyConditionEffect(new ConditionEffect
                            {
                                Effect = ConditionEffectIndex.Speedy,
                                DurationMS = 0
                            });
                            ushort obj;
                            Manager.GameData.IdToObjectType.TryGetValue(item.ObjectId, out obj);
                            if (Mp >= item.MpEndCost)
                            {
                                ActivateShoot(time, item, pkt.ItemUsePos);
                                Mp -= (int) item.MpEndCost;
                            }
                            Targetlink = target;
                            _ninjaShoot = false;
                        }
                    }
                        break;
                    case ActivateEffects.RandomPetStone:
                        ushort[] items =
                        {
                            0x6073, //Gigacorn
                            0x6075, //Gship
                            0x6079, //RedStoneGuard
                            0x6081, //BlueStoneGuard
                            0x6083, //Limon
                            0x6089, //Sprite God
                            0x6091, //Forgotten King
                            0x6094, //Crystal Steed
                            0x6096, //GSphinx
                            0x6118, //Bridge Sentinal
                            0x6119 //Twilight Archmage
                        };
                        bool sb = Inventory[pkt.SlotObject.SlotId].Soulbound;
                        Owner.Timers.Add(new WorldTimer(1, (w, t) => { //Delay so it won't delete it
                            Inventory[pkt.SlotObject.SlotId] = Manager.CreateSerial(Manager.GameData.Items[items[new Random(trueRandom(pkt)).Next(0, items.Length)]], DroppedIn: Owner.Name.Replace("'", ""), soulbound: sb); //Generates some very random numbers so you can't abuse the item
                            Client.Save();
                            UpdateCount++;
                        }));
                        break;
                    case ActivateEffects.TreasureFind:
                        List<ushort> treasureItems = new List<ushort>();
                        string TreasureFind = "The Marked Spot"; //Default
                        int Chance = 100;

                        foreach (var treasureitem in data.Items)
                        {
                            if (eff.treaureTier == 1) //Recieves Treasure Tier (Common, Uncommon, etc)
                            {
                                #region ItemData

                                if (treasureitem.Value.Tier == -1 || treasureitem.Value.Tier > 8 || treasureitem.Value.SetType != -1) //No Uts or high level gear or STs
                                    continue;
                                if (treasureitem.Value.SlotType <= 3) //sword, dag, bow
                                    treasureItems.Add(treasureitem.Value.ObjectType);
                                if (treasureitem.Value.SlotType >= 6 && treasureitem.Value.SlotType >= 8) //Leather, wand, Armor
                                    treasureItems.Add(treasureitem.Value.ObjectType);
                                if (treasureitem.Value.SlotType == 17) //staff
                                    treasureItems.Add(treasureitem.Value.ObjectType);
                                if (treasureitem.Value.SlotType == 24) //Katana
                                    treasureItems.Add(treasureitem.Value.ObjectType);
                                if (treasureitem.Value.Tier <= 4)
                                    if (treasureitem.Value.Usable && treasureitem.Value.MpCost >= 1) //Abilites
                                        treasureItems.Add(treasureitem.Value.ObjectType);
                                if (treasureitem.Value.Tier <= 4 && treasureitem.Value.SlotType == 11) //Rings
                                    treasureItems.Add(treasureitem.Value.ObjectType);
                                if (treasureitem.Value.SlotType == 14) //Robe
                                    treasureItems.Add(treasureitem.Value.ObjectType);

                                #endregion
                            }
                            if (eff.treaureTier == 2) //Recieves Treasure Tier (Common, Uncommon, etc)
                            {
                                #region ItemData

                                if (treasureitem.Value.Tier == -1 || treasureitem.Value.Tier > 10 || treasureitem.Value.SetType != -1) //No Uts or high level gear or STs
                                    continue;
                                if (treasureitem.Value.SlotType <= 3) //sword, dag, bow
                                    treasureItems.Add(treasureitem.Value.ObjectType);
                                if (treasureitem.Value.SlotType >= 6 && treasureitem.Value.SlotType >= 8) //Leather, wand, Armor
                                    treasureItems.Add(treasureitem.Value.ObjectType);
                                if (treasureitem.Value.SlotType == 17) //staff
                                    treasureItems.Add(treasureitem.Value.ObjectType);
                                if (treasureitem.Value.SlotType == 24) //Katana
                                    treasureItems.Add(treasureitem.Value.ObjectType);
                                if (treasureitem.Value.Tier <= 5)
                                    if (treasureitem.Value.Usable && treasureitem.Value.MpCost >= 1) //Abilites
                                        treasureItems.Add(treasureitem.Value.ObjectType);
                                if (treasureitem.Value.Tier <= 5 && treasureitem.Value.SlotType == 11) //Rings
                                    treasureItems.Add(treasureitem.Value.ObjectType);
                                if (treasureitem.Value.SlotType == 14) //Robe
                                    treasureItems.Add(treasureitem.Value.ObjectType);

                                #endregion
                                TreasureFind = "The Uncommon Marked Spot";
                            }
                            if (eff.treaureTier == 3) //Recieves Treasure Tier (Common, Uncommon, etc)
                            {
                                #region ItemData

                                if (treasureitem.Value.Tier == -1 || treasureitem.Value.Tier > 12 || treasureitem.Value.SetType != -1) //No Uts or high level gear or STs
                                    continue;
                                if (treasureitem.Value.SlotType <= 3) //sword, dag, bow
                                    treasureItems.Add(treasureitem.Value.ObjectType);
                                if (treasureitem.Value.SlotType >= 6 && treasureitem.Value.SlotType >= 8) //Leather, wand, Armor
                                    treasureItems.Add(treasureitem.Value.ObjectType);
                                if (treasureitem.Value.SlotType == 17) //staff
                                    treasureItems.Add(treasureitem.Value.ObjectType);
                                if (treasureitem.Value.SlotType == 24) //Katana
                                    treasureItems.Add(treasureitem.Value.ObjectType);
                                if (treasureitem.Value.Tier <= 6)
                                    if (treasureitem.Value.Usable && treasureitem.Value.MpCost >= 1) //Abilites
                                        treasureItems.Add(treasureitem.Value.ObjectType);
                                if (treasureitem.Value.Tier <= 6 && treasureitem.Value.SlotType == 11) //Rings
                                    treasureItems.Add(treasureitem.Value.ObjectType);
                                if (treasureitem.Value.SlotType == 14) //Robe
                                    treasureItems.Add(treasureitem.Value.ObjectType);

                                #endregion
                                TreasureFind = "The Rare Marked Spot";
                            }
                            if (eff.treaureTier == 4) //Recieves Treasure Tier (Common, Uncommon, etc)
                            {
                                #region ItemData

                                if (treasureitem.Value.Tier == -1 || treasureitem.Value.Tier > 13 || treasureitem.Value.SetType != -1) //No Uts or high level gear or STs
                                    continue;
                                if (treasureitem.Value.SlotType <= 3) //sword, dag, bow
                                    treasureItems.Add(treasureitem.Value.ObjectType);
                                if (treasureitem.Value.SlotType >= 6 && treasureitem.Value.SlotType >= 8) //Leather, wand, Armor
                                    treasureItems.Add(treasureitem.Value.ObjectType);
                                if (treasureitem.Value.SlotType == 17) //staff
                                    treasureItems.Add(treasureitem.Value.ObjectType);
                                if (treasureitem.Value.SlotType == 24) //Katana
                                    treasureItems.Add(treasureitem.Value.ObjectType);
                                if (treasureitem.Value.Tier <= 6)
                                    if (treasureitem.Value.Usable && treasureitem.Value.MpCost >= 1) //Abilites
                                        treasureItems.Add(treasureitem.Value.ObjectType);
                                if (treasureitem.Value.Tier <= 6 && treasureitem.Value.SlotType == 11) //Rings
                                    treasureItems.Add(treasureitem.Value.ObjectType);
                                if (treasureitem.Value.SlotType == 14) //Robe
                                    treasureItems.Add(treasureitem.Value.ObjectType);

                                #endregion
                                TreasureFind = "The Legendary Marked Spot";
                            }
                            if (eff.treaureTier == 5) //Recieves Treasure Tier (Shatters)
                            {
                                #region ItemData

                                if (treasureitem.Value.ObjectId == "The Forgotten Crown")
                                    treasureItems.Add(treasureitem.Value.ObjectType);
                                if (treasureitem.Value.ObjectId == "The Twilight Gemstone")
                                    treasureItems.Add(treasureitem.Value.ObjectType);
                                if (treasureitem.Value.ObjectId == "Bracer of the Guardian")
                                    treasureItems.Add(treasureitem.Value.ObjectType);

                                #endregion
                                Chance = 5;
                                TreasureFind = "The Shatters Marked Spot";
                            }
                        }

                        var ground =
                            this.GetNearestEntity(3, Manager.GameData.IdToObjectType[TreasureFind]); //find treasure
                        if (!(Chance >= Random.Next(0, 100))) //Failure
                            Client.Player.SendError("You did not find any treasure");
                        if (ground == null) //If no treasure in sight
                        {
                            Client.SendPacket(new DialogPacket
                            {
                                Title = "Can't find any treasure...",
                                Description = $"You may have to move closer to {TreasureFind} or make sure you have the right shovel."
                            });
                            return true;
                        }

                        Owner.LeaveWorld(ground); //Remove Ground
                        sb = Inventory[pkt.SlotObject.SlotId].Soulbound;
                        Owner.Timers.Add(new WorldTimer(1, (w, t) => { //Delay so it won't delete it
                            Inventory[pkt.SlotObject.SlotId] = Manager.CreateSerial(Manager.GameData.Items[treasureItems[new Random(trueRandom(pkt)).Next(0, treasureItems.Count)]], DroppedIn: Owner.Name.Replace("'", ""), soulbound: sb); //Give Item
                            Client.Save();
                        }));
                        break;
                    case ActivateEffects.UnlockSkin:
                        if (!Client.Account.OwnedSkins.Contains(item.ActivateEffects[0].SkinType))
                        {
                            Manager.Database.DoActionAsync(db =>
                            {
                                Client.Account.OwnedSkins.Add(item.ActivateEffects[0].SkinType);
                                var cmd = db.CreateQuery();
                                cmd.CommandText = "UPDATE accounts SET ownedSkins=@ownedSkins WHERE id=@id";
                                cmd.Parameters.AddWithValue("@ownedSkins",
                                    Utils.GetCommaSepString(Client.Account.OwnedSkins.ToArray()));
                                cmd.Parameters.AddWithValue("@id", AccountId);
                                cmd.ExecuteNonQuery();
                                SendInfo(
                                    "New skin unlocked successfully. Change skins in your Vault, or start a new character to use.");
                                Client.SendPacket(new UnlockedSkinPacket
                                {
                                    SkinId = item.ActivateEffects[0].SkinType
                                });
                            });
                            endMethod = false;
                            break;
                        }
                        SendInfo("Error.alreadyOwnsSkin");
                        endMethod = true;
                        break;
                    case ActivateEffects.Pet:
                        var en = Resolve(Manager, eff.ObjectId);
                        en.Move(X, Y);
                        en.SetPlayerOwner(this);
                        Owner.EnterWorld(en);
                        Owner.Timers.Add(new WorldTimer(30 * 1000, (w, t) => { w.LeaveWorld(en); }));
                        break;
                    case ActivateEffects.CreateChest:
                        if (Owner.Name != "Vault")
                        {
                            Client.Player.SendError("Can only be used in the vault");
                            return true;
                        }
                        var chest = Resolve(Manager, eff.Id);
                        chest.Move(X, Y);
                        Owner.EnterWorld(chest);
                        break;
                    case ActivateEffects.PetSkin:
                        if (!canChangePetSkin) {
                            Client.SendPacket(new DialogPacket
                            {
                                Title = "Pet Stone Failed",
                                Description = $"Please enter a new world before applying this Pet Stone."
                            });
                            return true;
                        }
                        if (Pet == null)
                        {
                            Client.SendPacket(new DialogPacket
                            {
                                Title = "Pet Stone Failed",
                                Description = $"Please equip a pet before applying this Pet Stone"
                            });
                            return true;
                        }
                        canChangePetSkin = false;
                        string newPetName = Inventory[pkt.SlotObject.SlotId].ObjectId.Replace(" Pet Stone", "");
                        Manager.Database.DoActionAsync(db =>
                        {
                            var cmd = db.CreateQuery();
                            cmd.CommandText =
                                "Update pets SET skin=@petStone WHERE accId=@id AND petId=@petId; Update pets SET skinName=@skinName WHERE accId=@id AND petId=@petId;";
                            cmd.Parameters.AddWithValue("@petId", Pet.PetId);
                            cmd.Parameters.AddWithValue("@id", AccountId);
                            cmd.Parameters.AddWithValue("@petStone", eff.PetType);
                            cmd.Parameters.AddWithValue("@skinName", newPetName);
                            cmd.ExecuteNonQuery();
                        });
                        if (eff.newSize != 0)
                            Manager.Database.DoActionAsync(db =>
                            {
                                var cmd = db.CreateQuery();
                                cmd.CommandText =
                                    "Update pets SET size=@size WHERE accId=@id AND petId=@petId";
                                cmd.Parameters.AddWithValue("@petId", Pet.PetId);
                                cmd.Parameters.AddWithValue("@id", AccountId);
                                cmd.Parameters.AddWithValue("@size", eff.newSize);
                                cmd.ExecuteNonQuery();
                            });
                        Owner.LeaveWorld(Pet);
                        Pet.Info.Skin = eff.PetType;
                        Owner.Timers.Add(new WorldTimer(1, (w, t) => { //Delay so it loads correctly
                            var pet = new Pet(Manager, Pet.Info, this);
                            Owner.EnterWorld(pet);
                            Client.SendPacket(new DialogPacket
                            {
                                Title = "Pet Stone Activated!",
                                Description = $"Your pet changed into a {newPetName}"
                            });
                        }));
                        break;
                    case ActivateEffects.AddFame:
                        Manager.Database.DoActionAsync(db =>
                            db.UpdateFame(Client.Account, eff.Amount));
                        Client.Save();
                        UpdateCount++;
                        break;
                    case ActivateEffects.CreatePet:
                        if (!Owner.Name.StartsWith("Pet Yard"))
                        {
                            SendInfo("server.use_in_petyard");
                            return true;
                        }
                        Pet.Create(Manager, this, item);
                        break;
                    case ActivateEffects.MysteryPortal:
                        string[] dungeons =
                        {
                            "Pirate Cave Portal",
                            "Forest Maze Portal",
                            "Spider Den Portal",
                            "Snake Pit Portal",
                            "Glowing Portal",
                            "Forbidden Jungle Portal",
                            "Candyland Portal",
                            "Haunted Cemetery Portal",
                            "Undead Lair Portal",
                            "Davy Jones' Locker Portal",
                            "Manor of the Immortals Portal",
                            "Abyss of Demons Portal",
                            "Lair of Draconis Portal",
                            "Mad Lab Portal",
                            "Ocean Trench Portal",
                            "Tomb of the Ancients Portal",
                            "Beachzone Portal",
                            "The Shatters",
                            "Deadwater Docks",
                            "Woodland Labyrinth",
                            "The Crawling Depths",
                            "Battle Nexus Portal",
                            "Belladonna's Garden Portal",
                            "Lair of Shaitan Portal"
                        };
                        var descs = Manager.GameData.Portals.Where(_ => dungeons.Contains(_.Value.ObjectId))
                            .Select(_ => _.Value).ToArray();
                        var portalDesc = descs[Random.Next(0, descs.Count())];
                        var por = Resolve(Manager, portalDesc.ObjectId);
                        por.Move(X, Y);
                        Owner.EnterWorld(por);
                        Client.SendPacket(new NotificationPacket
                        {
                            Color = new ARGB(0x00FF00),
                            Text =
                                "{\"key\":\"blank\",\"tokens\":{\"data\":\"" + portalDesc.DungeonName + " opened by " +
                                Client.Account.Name + "\"}}",
                            ObjectId = Client.Player.Id
                        });
                        Owner.BroadcastPacket(new TextPacket
                        {
                            BubbleTime = 0,
                            Stars = -1,
                            Name = "",
                            Text = portalDesc.ObjectId + " opened by " + Name
                        }, null);
                        Owner.Timers.Add(new WorldTimer(portalDesc.TimeoutTime * 1000,
                            (w, t) => //default portal close time * 1000
                            {
                                try
                                {
                                    w.LeaveWorld(por);
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine("Couldn't despawn portal.\n{0}", ex);
                                }
                            }));
                        break;
                    case ActivateEffects.GenericActivate:
                        var targetPlayer = eff.Target.Equals("player");
                        var centerPlayer = eff.Center.Equals("player");
                        var duration = eff.UseWisMod ? (int) (UseWisMod(eff.DurationSec) * 1000) : eff.DurationMS;
                        var range = eff.UseWisMod
                            ? UseWisMod(eff.Range)
                            : eff.Range;
                        Owner.Aoe(eff.Center.Equals("mouse") ? target : new Position {X = X, Y = Y}, range,
                            targetPlayer, entity =>
                            {
                                if (IsSpecial(entity.ObjectType)) return;
                                if (!entity.HasConditionEffect(ConditionEffectIndex.Stasis) &&
                                    !entity.HasConditionEffect(ConditionEffectIndex.Invincible))
                                    entity.ApplyConditionEffect(
                                        new ConditionEffect
                                        {
                                            Effect = eff.ConditionEffect.Value,
                                            DurationMS = duration
                                        });
                            });

                        // replaced this last bit with what I had, never noticed any issue with it. Perhaps I'm wrong?
                        BroadcastSync(new ShowEffectPacket
                        {
                            EffectType = (EffectType) eff.VisualEffect,
                            TargetId = Id,
                            Color = new ARGB(eff.Color ?? 0xffffffff),
                            PosA = centerPlayer ? new Position {X = range} : target,
                            PosB = new Position(target.X - range, target.Y) //Its the range of the diffuse effect
                        }, p => this.DistSqr(p) < 25);
                        break;
                }
            UpdateCount++;
            return endMethod;
        }

        private float UseWisMod(float value, int offset = 1)
        {
            double totalWisdom = Stats[6] + 2 * Boost[6];
            if (totalWisdom < 30)
                return value;
            double m = value < 0 ? -1 : 1;
            var n = value * totalWisdom / 150 + value * m;
            n = Math.Floor(n * Math.Pow(10, offset)) / Math.Pow(10, offset);
            if (n - (int) n * m >= 1 / Math.Pow(10, offset) * m)
                return (int) (n * 10) / 10.0f;
            return (int) n;
        }

        public int trueRandom(UseItemPacket pkt) //Not really 'true' but semi-patches a bug where spamming items gives the same seed
        { //Uses Variables such as mouse pos, object activated, players in world, player X and Y, items in inventory, and time
            float ret = pkt.ItemUsePos.X + pkt.ItemUsePos.Y + pkt.SlotObject.ObjectId + Owner.Players.Count + X + Y;
            for (var i = 0; i < Inventory.Length; i++)
            {
                if (Inventory[i] != null)
                    ret += Inventory[i].ObjectType;
            }
            ret /= DateTime.Now.Millisecond;
            return (int)ret;
        }

        private static bool IsSpecial(ushort objType)
        {
            return objType == 0x750d || objType == 0x750e || objType == 0x222c || objType == 0x222d;
        }
    }
}