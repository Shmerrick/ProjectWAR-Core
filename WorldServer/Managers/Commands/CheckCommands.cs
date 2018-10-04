﻿using System;
using System.Collections.Generic;
using System.Linq;
using static WorldServer.Managers.Commands.GMUtils;
using System.Text;
using GameData;
using WorldServer.Services.World;
using WorldServer.World.Battlefronts.Apocalypse;

namespace WorldServer.Managers.Commands
{
    /// <summary>Debugging commands under .check</summary>
    internal class CheckCommands
    {

        /// <summary>
        /// Check how many groups exist on the server.
        /// </summary>
        /// <param name="plr">Player that initiated the command</param>
        /// <param name="values">List of command arguments (after command name)</param>
        /// <returns>True if command was correctly handled, false if operation was canceled</returns>
        public static bool CheckGroups(Player plr, ref List<string> values)
        {
            plr.SendClientMessage(Group.WorldGroups.Count + " groups on the server:");

            lock (Group.WorldGroups)
            {
                foreach (Group group in Group.WorldGroups)
                {
                    Player ldr = group.Leader;

                    if (ldr == null)
                        plr.SendClientMessage("Leaderless group");
                    else plr.SendClientMessage("Group led by " + ldr.Name);
                }
            }

            return true;
        }

        /// <summary>
        /// Check how many objects exist in the current region.
        /// </summary>
        /// <param name="plr">Player that initiated the command</param>
        /// <param name="values">List of command arguments (after command name)</param>
        /// <returns>True if command was correctly handled, false if operation was canceled</returns>
        public static bool CheckObjects(Player plr, ref List<string> values)
        {
            plr.Region?.CountObjects(plr);

            return true;
        }

        public static bool GetPlayerContribution(Player plr, ref List<string> values)
        {
            var activeBattleFrontId = WorldMgr.UpperTierCampaignManager.ActiveBattleFront.BattleFrontId;
            var activeBattleFrontStatus = WorldMgr.UpperTierCampaignManager.GetBattleFrontStatus(activeBattleFrontId);

            var target = (Player)plr.CbtInterface.GetCurrentTarget();
            if (target != null)
            {
                var playerContribution = activeBattleFrontStatus.ContributionManagerInstance.GetContribution(target.CharacterId);

                if (playerContribution == null)
                {
                    plr.SendClientMessage("Player has no contribution");
                }
                else
                {
                    foreach (var contribution in playerContribution)
                    {
                        plr.SendClientMessage(contribution.ToString());
                    }
                    var stageDictionary = activeBattleFrontStatus.ContributionManagerInstance.GetContributionStageList(target.CharacterId);

                    foreach (var contributionStage in stageDictionary)
                    {
                        plr.SendClientMessage(contributionStage.ToString());
                    }
                }
            }
            return true;
        }

        public static bool GetPlayerBounty(Player plr, ref List<string> values)
        {
            var activeBattleFrontId = WorldMgr.UpperTierCampaignManager.ActiveBattleFront.BattleFrontId;
            var activeBattleFrontStatus = WorldMgr.UpperTierCampaignManager.GetBattleFrontStatus(activeBattleFrontId);

            var target = (Player)plr.CbtInterface.GetCurrentTarget();
            if (target != null)
            {
                var playerBounty = activeBattleFrontStatus.BountyManagerInstance.GetBounty(target.CharacterId);
                plr.SendClientMessage(playerBounty.ToString());
            }

            return true;
        }

        public static bool GetPlayerImpactMatrix(Player plr, ref List<string> values)
        {
            var activeBattleFrontId = WorldMgr.UpperTierCampaignManager.ActiveBattleFront.BattleFrontId;
            var activeBattleFrontStatus = WorldMgr.UpperTierCampaignManager.GetBattleFrontStatus(activeBattleFrontId);

            var target = (Player)plr.CbtInterface.GetCurrentTarget();
            if (target != null)
            {
                var killImpacts = activeBattleFrontStatus.ImpactMatrixManagerInstance.GetKillImpacts(target.CharacterId);
                if (killImpacts == null)
                {
                    plr.SendClientMessage($"{target.Name} has no impacts");
                }
                foreach (var impact in killImpacts)
                {
                    plr.SendClientMessage($"{target.Name} {impact.ToString()}");
                }
            }
            return true;
        }


        public static bool GetServerPopulation(Player plr, ref List<string> values)
        {
            lock (Player._Players)
            {
                plr.SendClientMessage($"Server Population ");
                plr.SendClientMessage($"Online players : {Player._Players.Count} ");
                plr.SendClientMessage($"Order : {Player._Players.Count(x => x.Realm == Realms.REALMS_REALM_ORDER && !x.IsDisposed && x.IsInWorld() && x != null)} ");
                plr.SendClientMessage($"Destro : {Player._Players.Count(x => x.Realm == Realms.REALMS_REALM_DESTRUCTION && !x.IsDisposed && x.IsInWorld() && x != null)}");

                plr.SendClientMessage("------------------------------------");
                var message = String.Empty;

                foreach (var regionMgr in WorldMgr._Regions)
                {
                    if (regionMgr.Players.Count > 0)
                    {
                        message += $"Region {regionMgr.RegionId} : " +
                                   $"Total : {Player._Players.Count(x => !x.IsDisposed && x.IsInWorld() && x != null && x.Region.RegionId == regionMgr.RegionId)} " +
                                   $"Order : {Player._Players.Count(x => x.Realm == Realms.REALMS_REALM_ORDER && !x.IsDisposed && x.IsInWorld() && x != null && x.Region.RegionId == regionMgr.RegionId)} " +
                                   $"Dest : {Player._Players.Count(x => x.Realm == Realms.REALMS_REALM_DESTRUCTION && !x.IsDisposed && x.IsInWorld() && x != null && x.Region.RegionId == regionMgr.RegionId)} ";
                    }
                }
                plr.SendClientMessage(message);
            }
            return true;
        }

        public static bool GetRewardEligibility(Player plr, ref List<string> values)
        {
            var activeBattleFrontId = WorldMgr.UpperTierCampaignManager.ActiveBattleFront.BattleFrontId;
            var activeBattleFrontStatus = WorldMgr.UpperTierCampaignManager.GetActiveBattleFrontStatus(activeBattleFrontId);

            var players = activeBattleFrontStatus.ContributionManagerInstance.GetEligiblePlayers(0);

            plr.SendClientMessage($"Eligible players ({players.Count()}):");

            foreach (var player in players)
            {
                var playerObject = Player.GetPlayer(player.Key);

                if (playerObject.Realm == Realms.REALMS_REALM_DESTRUCTION)
                    plr.SendClientMessage($"{playerObject.Name} (D)");
                else
                {
                    plr.SendClientMessage($"{playerObject.Name} (O)");
                }
            }

            return true;
        }



        /// <summary>
        /// Finds all players currently in range.
        /// </summary>
        /// <param name="plr">Player that initiated the command</param>
        /// <param name="values">List of command arguments (after command name)</param>
        /// <returns>True if command was correctly handled, false if operation was canceled</returns>
        public static bool CheckPlayersInRange(Player plr, ref List<string> values)
        {
            StringBuilder str = new StringBuilder(256);
            int curOnLine = 0;

            lock (plr.PlayersInRange)
            {
                foreach (Player player in plr.PlayersInRange)
                {
                    if (curOnLine != 0)
                        str.Append(", ");
                    str.Append(player.Name);
                    str.Append(" (");
                    str.Append(player.Zone.Info.Name);
                    str.Append(")");

                    ++curOnLine;

                    if (curOnLine == 5)
                    {
                        plr.SendClientMessage(str.ToString());
                        curOnLine = 0;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Find the closest respawn point for the specified realm.
        /// </summary>
        /// <param name="plr">Player that initiated the command</param>
        /// <param name="values">List of command arguments (after command name)</param>
        /// <returns>True if command was correctly handled, false if operation was canceled</returns>
        public static bool FindClosestRespawn(Player plr, ref List<string> values)
        {
            byte realm = (byte)GetInt(ref values);

            plr.SendClientMessage("Closest respawn for " + (realm == 1 ? "Order" : "Destruction") + " is " +
                             WorldMgr.GetZoneRespawn(plr.Zone.ZoneId, realm, plr).RespawnID);

            return true;
        }

        /// <summary>
        /// Toggles logging outgoing packet volume.
        /// </summary>
        /// <param name="plr">Player that initiated the command</param>
        /// <param name="values">List of command arguments (after command name)</param>
        /// <returns>True if command was correctly handled, false if operation was canceled</returns>
        public static bool LogPackets(Player plr, ref List<string> values)
        {
            if (plr.Region == null)
                return false;

            plr.Region.TogglePacketLogging();

            plr.SendClientMessage(plr.Region.LogPacketVolume ? "Logging outgoing packet volume." : "No longer logging outgoing packet volume.");

            return true;
        }

        /// <summary>
        /// Displays the volume of outgoing packets over the defined period.
        /// </summary>
        /// <param name="plr">Player that initiated the command</param>
        /// <param name="values">List of command arguments (after command name)</param>
        /// <returns>True if command was correctly handled, false if operation was canceled</returns>
        public static bool ReadPackets(Player plr, ref List<string> values)
        {
            plr.Region.SendPacketVolumeInfo(plr);

            return true;
        }

        /// <summary>
        /// Starts/Stops line of sight monitoring for selected target.
        /// </summary>
        /// <param name="plr">Player that initiated the command</param>
        /// <param name="values">List of command arguments (after command name)</param>
        /// <returns>True if command was correctly handled, false if operation was canceled</returns>
        public static bool StartStopLosMonitor(Player plr, ref List<string> values)
        {
            var target = plr.CbtInterface.GetCurrentTarget();

            if (target != null)
            {
                plr.EvtInterface.AddEvent(() =>
                {
                    if (plr.LOSHit(target))
                        plr.SendClientMessage("LOS=YES " + DateTime.Now.Second);
                    else
                        plr.SendClientMessage("LOS=NO" + DateTime.Now.Second);
                }, 1000, 30);

            }
            return true;
        }
    }
}
