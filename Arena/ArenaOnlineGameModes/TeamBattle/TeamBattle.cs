using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System;
using System.Text;

namespace RainMeadow.Arena.ArenaOnlineGameModes.TeamBattle
{
    public partial class TeamBattleMode : ExternalArenaGameMode
    {
        public static ArenaSetup.GameTypeID TeamBattle = new ArenaSetup.GameTypeID(
            "Team Battle",
            register: false
        );

        public override ArenaSetup.GameTypeID GetGameModeId => TeamBattle;

        public static bool isTeamBattleMode(ArenaOnlineGameMode arena, out TeamBattleMode tb)
        {
            tb = null;
            if (arena.currentGameMode == TeamBattle.value)
            {
                tb = (
                    arena.registeredGameModes.FirstOrDefault(x => x.Key == TeamBattle.value).Value
                    as TeamBattleMode
                );
                return true;
            }
            return false;
        }

        public int? winningTeamIndex { get; set; } = null;

        public Dictionary<int, int> scoreByTeamIndex
        {
            get
            {
                field = new Dictionary<int, int>
                {
                    { 0, 0 },
                    { 1, 0 },
                    { 2, 0 },
                    { 3, 0 }
                };

                ArenaOnlineGameMode arenaOnline = (ArenaOnlineGameMode)OnlineManager.lobby.gameMode;
                ArenaSitting arenaSitting = arenaOnline.session.arenaSitting;

                foreach (ArenaSitting.ArenaPlayer arenaPlayer in arenaSitting.players)
                {
                    if (ArenaHelpers.FindOnlinePlayerByFakePlayerNumber(arenaOnline, arenaPlayer.playerNumber) is not OnlinePlayer onlinePlayer)
                    {
                        RainMeadow.Warn($"Unable to find arena player A's online player. Player number: {arenaPlayer.playerNumber}");
                        continue;
                    }
                    if (!OnlineManager.lobby.clientSettings[onlinePlayer].TryGetData(out ArenaTeamClientSettings clientData))
                    {
                        RainMeadow.Error($"Unable to find {onlinePlayer}'s team client data.");
                        continue;
                    }

                    field[clientData.team] += arenaPlayer.score;
                }

                return field;
            }
        } = new()
        {
            { 0, 0 },
            { 1, 0 },
            { 2, 0 },
            { 3, 0 }
        };

        public Dictionary<int, int> totalScoreByTeamIndex
        {
            get
            {
                field = new Dictionary<int, int>
                {
                    { 0, 0 },
                    { 1, 0 },
                    { 2, 0 },
                    { 3, 0 }
                };

                ArenaOnlineGameMode arenaOnline = (ArenaOnlineGameMode)OnlineManager.lobby.gameMode;
                ArenaSitting arenaSitting = arenaOnline.session.arenaSitting;

                foreach (ArenaSitting.ArenaPlayer arenaPlayer in arenaSitting.players)
                {
                    if (ArenaHelpers.FindOnlinePlayerByFakePlayerNumber(arenaOnline, arenaPlayer.playerNumber) is not OnlinePlayer onlinePlayer)
                    {
                        RainMeadow.Warn($"Unable to find arena player A's online player. Player number: {arenaPlayer.playerNumber}");
                        continue;
                    }
                    if (!OnlineManager.lobby.clientSettings[onlinePlayer].TryGetData(out ArenaTeamClientSettings clientData))
                    {
                        RainMeadow.Error($"Unable to find {onlinePlayer}'s team client data.");
                        continue;
                    }

                    field[clientData.team] += arenaPlayer.totScore;
                }

                return field;
            }
        } = new()
        {
            { 0, 0 },
            { 1, 0 },
            { 2, 0 },
            { 3, 0 }
        };

        private int _timerDuration;

        public override void ResetOnSessionEnd()
        {
            winningTeamIndex = -1;
            martyrsSpawn = 0;
            outlawsSpawn = 0;
            dragonslayersSpawn = 0;
            chieftainsSpawn = 0;
            roundSpawnPointCycler = 0;
        }

        public override bool On_ArenaBehaviors_ExitManager_ExitsOpen(
            ArenaOnlineGameMode arena,
            On.ArenaBehaviors.ExitManager.orig_ExitsOpen orig,
            ArenaBehaviors.ExitManager self
        )
        {
            if (self.gameSession.GameTypeSetup.denEntryRule == ArenaSetup.GameTypeSetup.DenEntryRule.Always)
            {
                // idk why orig ignores this when 2 player exists
                return true;
            }

            if (self.gameSession.GameTypeSetup.denEntryRule == ArenaSetup.GameTypeSetup.DenEntryRule.Score)
            {
                return orig(self) || (self.gameSession?.arenaSitting?.players?.Any(p => p?.score >= arena.denScore) ?? false);
            }

            int playersStillStanding =
                self.gameSession.Players?.Count(player =>
                    player.realizedCreature != null && player.realizedCreature.State.alive
                ) ?? 0;

            if (
                playersStillStanding == 1
                && arena.arenaSittingOnlineOrder.Count > 1
                && !arena.countdownInitiatedHoldFire
            )
            {
                return true;
            }

            if (self.world.rainCycle.TimeUntilRain <= 100)
            {
                return true;
            }

            if (playersStillStanding > 1 && arena.setupTime == 0)
            {
                HashSet<int> aliveTeams = new HashSet<int>();
                if (self.gameSession.Players != null)
                {
                    foreach (var acPlayer in self.gameSession.Players)
                    {
                        if (acPlayer != null)
                        {
                            OnlinePhysicalObject? onlineP = acPlayer.GetOnlineObject();
                            if (onlineP != null)
                            {
                                bool gotPlayerTeam = OnlineManager.lobby.clientSettings.TryGetValue(
                                    onlineP.owner,
                                    out var onlineClientP
                                );
                                if (gotPlayerTeam)
                                {
                                    onlineClientP.TryGetData<ArenaTeamClientSettings>(
                                        out var playerTeam
                                    );
                                    if (gotPlayerTeam)
                                    {
                                        if (acPlayer.realizedCreature != null)
                                        {
                                            if (acPlayer.realizedCreature.State.alive)
                                            {
                                                aliveTeams.Add(playerTeam.team);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    if (aliveTeams.Count == 1)
                    {
                        if (self.gameSession.game.world.rainCycle.speedUpToRain == false)
                        {
                            RainMeadow.Debug("Team Battle: Adding rain");
                            self.gameSession.game.world.rainCycle.ArenaEndSessionRain();
                        }
                        return true;
                    }
                }
            }
            return orig(self);
        }

        public override bool SpawnBatflies(FliesWorldAI self, int spawnRoom)
        {
            return false;
        }

        public override string TimerText()
        {
            return Utils.Translate("Prepare for war,") + " " + Utils.Translate(PlayingAsText());
        }

        public override int SetTimer(ArenaOnlineGameMode arena)
        {
            return arena.setupTime = RainMeadow.rainMeadowOptions.ArenaCountDownTimer.Value;
        }

        public override int TimerDuration
        {
            get { return _timerDuration; }
            set { _timerDuration = value; }
        }

        public override int TimerDirection(ArenaOnlineGameMode arena, int timer)
        {
            return --arena.setupTime;
        }

        public override bool HoldFireWhileTimerIsActive(ArenaOnlineGameMode arena)
        {
            if (arena.setupTime > 0)
            {
                return arena.countdownInitiatedHoldFire = true;
            }
            else
            {
                return arena.countdownInitiatedHoldFire = false;
            }
        }

        /// <inheritdoc/>
        public override void On_ArenaGameSession_Killing(
            ArenaOnlineGameMode arenaOnline,
            On.ArenaGameSession.orig_Killing orig,
            ArenaGameSession self,
            Player attacker,
            Creature target)
        {
            // Copy ArenaGameSession.Killing's guard clause
            if (self.sessionEnded || ModManager.MSC && attacker.AI is not null)
                return;

            if (attacker.abstractCreature.GetOnlineCreature() is not OnlineCreature attackerOCreature)
            {
                RainMeadow.Error("Unable to find attacker's online creature.");
                return;
            }
            if (target.abstractCreature.GetOnlineCreature() is not OnlineCreature targetOCreature)
            {
                RainMeadow.Error("Unable to find target's online creature.");
                return;
            }
            if (ArenaHelpers.FindArenaPlayerByOnlinePlayer(arenaOnline, attackerOCreature.owner) is not ArenaSitting.ArenaPlayer attackerArenaPlayer)
            {
                RainMeadow.Error($"Unable to find {attackerOCreature.owner}'s arena player.");
                return;
            }


            bool isTeamKill = attackerOCreature.isAvatar && targetOCreature.isAvatar &&
                ArenaHelpers.CheckSameTeam(attackerOCreature.owner, targetOCreature.owner);

            if (isTeamKill)
            {
                int scoreChange = -arenaOnline.killScore;

                if (scoreChange != 0)
                {
                    ArenaRPCs.ModifyArenaPlayerScore(
                        attackerArenaPlayer.playerNumber,
                        scoreChange
                    );

                    attackerOCreature.BroadcastRPCInRoom(
                        ArenaRPCs.ModifyArenaPlayerScore,
                        attackerArenaPlayer.playerNumber,
                        scoreChange
                    );
                }
            }
            else
                base.On_ArenaGameSession_Killing(arenaOnline, orig, self, attacker, target);
        }

        public override void On_ArenaGameSession_PlayerLandSpear(
            ArenaOnlineGameMode arenaOnline,
            On.ArenaGameSession.orig_PlayerLandSpear orig,
            ArenaGameSession self,
            Player attacker,
            Creature target)
        {
            if (attacker.abstractCreature.GetOnlineCreature() is not OnlineCreature attackerOCreature)
            {
                RainMeadow.Error("Unable to find attacker's online creature.");
                return;
            }
            if (target.abstractCreature.GetOnlineCreature() is not OnlineCreature targetOCreature)
            {
                RainMeadow.Error("Unable to find target's online creature.");
                return;
            }
            if (attackerOCreature.isMine &&
                attackerOCreature.isAvatar &&
                targetOCreature.isAvatar)
            {
                if (!OnlineManager.lobby.clientSettings[attackerOCreature.owner].TryGetData(out ArenaTeamClientSettings attackerClientData))
                {
                    RainMeadow.Error($"Unable to find {attackerOCreature.owner}'s team client data.");
                    return;
                }
                if (!OnlineManager.lobby.clientSettings[targetOCreature.owner].TryGetData(out ArenaTeamClientSettings targetClientData))
                {
                    RainMeadow.Error($"Unable to find {targetOCreature.owner}'s team client data.");
                    return;
                }
                if (attackerClientData.team == targetClientData.team)
                {
                    RainMeadow.Error(
                        $"{attackerOCreature.owner} and {targetOCreature.owner} are on " +
                        $"the same team. They should not be able to stab each-other."
                    );
                    return;
                }
            }

            base.On_ArenaGameSession_PlayerLandSpear(
                arenaOnline,
                orig,
                self,
                attacker,
                target
            );
        }

        public override void SpawnPlayer(
            ArenaOnlineGameMode arena,
            ArenaGameSession self,
            Room room,
            List<int> suggestedDens
        )
        {
            // Shameful copy-paste
            if (isTeamBattleMode(arena, out var teamBattleMode))
            {
                List<OnlinePlayer> list = new List<OnlinePlayer>();

                List<OnlinePlayer> list2 = new List<OnlinePlayer>();

                for (int j = 0; j < OnlineManager.players.Count; j++)
                {
                    if (arena.arenaSittingOnlineOrder.Contains(OnlineManager.players[j].inLobbyId))
                    {
                        list2.Add(OnlineManager.players[j]);
                    }
                }

                while (list2.Count > 0)
                {
                    int index = UnityEngine.Random.Range(0, list2.Count);
                    list.Add(list2[index]);
                    list2.RemoveAt(index);
                }
                int randomExitIndex = 0;
                int totalExits = self.game.world.GetAbstractRoom(0).exits;
                teamBattleMode.roundSpawnPointCycler = (
                    teamBattleMode.roundSpawnPointCycler % totalExits
                );

                if (
                    OnlineManager
                        .lobby.clientSettings[OnlineManager.mePlayer]
                        .TryGetData<ArenaTeamClientSettings>(out var teamSettings)
                )
                {
                    teamBattleMode.martyrsSpawn =
                        (
                            (int)TeamSpawnPoints.martyrsTeamName
                            + teamBattleMode.roundSpawnPointCycler
                        ) % totalExits;
                    teamBattleMode.outlawsSpawn =
                        ((int)TeamSpawnPoints.outlawTeamName + teamBattleMode.roundSpawnPointCycler)
                        % totalExits;
                    teamBattleMode.dragonslayersSpawn =
                        (
                            (int)TeamSpawnPoints.dragonslayersTeamName
                            + teamBattleMode.roundSpawnPointCycler
                        ) % totalExits;
                    teamBattleMode.chieftainsSpawn =
                        (
                            (int)TeamSpawnPoints.chieftainsTeamName
                            + teamBattleMode.roundSpawnPointCycler
                        ) % totalExits;

                    switch ((TeamSpawnPoints)teamSettings.team)
                    {
                        case TeamSpawnPoints.martyrsTeamName:
                            randomExitIndex = teamBattleMode.martyrsSpawn;
                            break;
                        case TeamSpawnPoints.outlawTeamName:
                            randomExitIndex = teamBattleMode.outlawsSpawn;
                            break;
                        case TeamSpawnPoints.dragonslayersTeamName:
                            randomExitIndex = teamBattleMode.dragonslayersSpawn;
                            break;
                        case TeamSpawnPoints.chieftainsTeamName:
                            randomExitIndex = teamBattleMode.chieftainsSpawn;
                            break;
                        default:
                            Debug.LogWarning(
                                "Current player's team is not recognized for spawn point assignment."
                            );
                            randomExitIndex = 0;
                            break;
                    }
                    if (OnlineManager.lobby.isOwner)
                    {
                        foreach (var player in OnlineManager.players)
                        {
                            if (player.isMe)
                            {
                                continue; //
                            }
                            player.InvokeOnceRPC(
                                ArenaRPCs.Arena_NotifySpawnPoint,
                                teamBattleMode.martyrsSpawn,
                                teamBattleMode.outlawsSpawn,
                                teamBattleMode.dragonslayersSpawn,
                                teamBattleMode.chieftainsSpawn
                            );
                        }
                    }
                }

                if (
                    ArenaHelpers.GetArenaClientSettings(OnlineManager.mePlayer)!.playingAs
                    == RainMeadow.Ext_SlugcatStatsName.OnlineOverseerSpectator
                )
                {
                    RainMeadow.Debug("Player spawned as Overseer");
                    if (arena.enableOverseer)
                    {
                        SpawnPlayerOverseer(
                            arena,
                            self,
                            room,
                            randomExitIndex
                        );
                    }
                }
                else
                {
                    SpawnNonTransferableCreature(
                        arena,
                        self,
                        room,
                        randomExitIndex,
                        CreatureTemplate.Type.Slugcat
                    );
                }

                self.playersSpawned = true;
                if (OnlineManager.lobby.isOwner)
                {
                    arena.isInGame = true; // used for readied players at the beginning
                    arena.leaveForNextLevel = false;
                    arena.playersLateWaitingInLobbyForNextRound.Clear();
                    arena.hasPermissionToRejoin = false;
                }
                for (int x = 0; x < arena.arenaSittingOnlineOrder.Count; x++)
                {
                    OnlinePlayer? getPlayer = ArenaHelpers.FindOnlinePlayerByLobbyId(arena.arenaSittingOnlineOrder[x]);
                    if (getPlayer != null)
                    {
                        if (OnlineManager.lobby.isOwner)
                        {
                            arena.AddMissingStatEntries(getPlayer);
                        }
                        RainMeadow.Info($"RMEL;{getPlayer.id.DisplayName};CLASS;${ArenaHelpers.GetArenaClientSettings(getPlayer)?.playingAs}");
                        RainMeadow.Info($"RMEL;{getPlayer.id.DisplayName};TEAM;{teamNames[ArenaHelpers.GetDataSettings<ArenaTeamClientSettings>(getPlayer).team]}");

                    }
                }
            }
        }

        public override bool On_ArenaSitting_PlayerSessionResultSort(
            ArenaOnlineGameMode arenaOnline,
            On.ArenaSitting.orig_PlayerSessionResultSort orig,
            ArenaSitting self,
            ArenaSitting.ArenaPlayer a,
            ArenaSitting.ArenaPlayer b)
        {
            if (ArenaHelpers.FindOnlinePlayerByFakePlayerNumber(arenaOnline, a.playerNumber) is not OnlinePlayer onlinePlayerA)
            {
                RainMeadow.Warn($"Unable to find arena player A's online player. Player number: {a.playerNumber}");
                return false;
            }
            if (ArenaHelpers.FindOnlinePlayerByFakePlayerNumber(arenaOnline, b.playerNumber) is not OnlinePlayer onlinePlayerB)
            {
                RainMeadow.Warn($"Unable to find arena player B's online player. Player number: {b.playerNumber}");
                return false;
            }
            if (!OnlineManager.lobby.clientSettings[onlinePlayerA].TryGetData(out ArenaTeamClientSettings clientDataA))
            {
                RainMeadow.Error($"Unable to find {onlinePlayerA}'s team client data.");
                return false;
            }
            if (!OnlineManager.lobby.clientSettings[onlinePlayerB].TryGetData(out ArenaTeamClientSettings clientDataB))
            {
                RainMeadow.Error($"Unable to find {onlinePlayerB}'s team client data.");
                return false;
            }

            // We want players of the same team to be grouped together even if individuals
            // in a team scored better/worse than some individuals on other teams.
            if (clientDataA.team != clientDataB.team)
            {
                int teamAScore = scoreByTeamIndex[clientDataA.team];
                int teamBScore = scoreByTeamIndex[clientDataB.team];

                if (teamAScore != teamBScore)
                    return teamAScore > teamBScore;

                return clientDataA.team > clientDataB.team; // Not exactly ideal, but it makes less sense to have teams be mixed together.
            }

            return base.On_ArenaSitting_PlayerSessionResultSort(
                arenaOnline,
                orig,
                self,
                a,
                b
            );
        }

        public override bool On_ArenaSitting_PlayerSittingResultSort(
            ArenaOnlineGameMode arenaOnline,
            On.ArenaSitting.orig_PlayerSittingResultSort orig,
            ArenaSitting self,
            ArenaSitting.ArenaPlayer a,
            ArenaSitting.ArenaPlayer b)
        {
            if (ArenaHelpers.FindOnlinePlayerByFakePlayerNumber(arenaOnline, a.playerNumber) is not OnlinePlayer onlinePlayerA)
            {
                RainMeadow.Warn($"Unable to find arena player A's online player. Player number: {a.playerNumber}");
                return false;
            }
            if (ArenaHelpers.FindOnlinePlayerByFakePlayerNumber(arenaOnline, b.playerNumber) is not OnlinePlayer onlinePlayerB)
            {
                RainMeadow.Warn($"Unable to find arena player B's online player. Player number: {b.playerNumber}");
                return false;
            }
            if (!OnlineManager.lobby.clientSettings[onlinePlayerA].TryGetData(out ArenaTeamClientSettings clientDataA))
            {
                RainMeadow.Error($"Unable to find {onlinePlayerA}'s team client data.");
                return false;
            }
            if (!OnlineManager.lobby.clientSettings[onlinePlayerB].TryGetData(out ArenaTeamClientSettings clientDataB))
            {
                RainMeadow.Error($"Unable to find {onlinePlayerB}'s team client data.");
                return false;
            }

            // We want players of the same team to be grouped together even if individuals
            // in the team scored better/worse than some individuals on other teams.
            if (clientDataA.team != clientDataB.team)
            {
                int teamATotalScore = totalScoreByTeamIndex[clientDataA.team];
                int teamBTotalScore = totalScoreByTeamIndex[clientDataB.team];

                if (teamATotalScore != teamBTotalScore)
                    return teamATotalScore > teamBTotalScore;

                return clientDataA.team > clientDataB.team; // Not exactly ideal, but it makes less sense to have teams be mixed together.
            }

            return base.On_ArenaSitting_PlayerSittingResultSort(
                arenaOnline,
                orig,
                self,
                a,
                b
            );
        }

        public override List<ArenaSitting.ArenaPlayer> DetermineArenaSessionWinners(
            ArenaOnlineGameMode arenaOnline,
            ArenaGameSession arenaSession)
        {
            ArenaSitting arenaSitting = arenaSession.arenaSitting;

            int maxScore = scoreByTeamIndex.Max(kvp => kvp.Value);

            List<int> bestTeamIndexes = scoreByTeamIndex
                .Where(kvp => kvp.Value == maxScore)
                .Select(kvp => kvp.Key)
                .ToList();

            winningTeamIndex = bestTeamIndexes.Count == 1
                ? bestTeamIndexes[0]
                : null;

            List<ArenaSitting.ArenaPlayer> winners = [];
            foreach (ArenaSitting.ArenaPlayer arenaPlayer in arenaSitting.players)
            {
                if (ArenaHelpers.FindOnlinePlayerByFakePlayerNumber(arenaOnline, arenaPlayer.playerNumber) is not OnlinePlayer onlinePlayer)
                {
                    RainMeadow.Warn($"Unable to find arena player A's online player. Player number: {arenaPlayer.playerNumber}");
                    continue;
                }
                if (!OnlineManager.lobby.clientSettings[onlinePlayer].TryGetData(out ArenaTeamClientSettings clientData))
                {
                    RainMeadow.Error($"Unable to find {onlinePlayer}'s team client data.");
                    continue;
                }

                if (clientData.team == winningTeamIndex)
                    winners.Add(arenaPlayer);
            }

            return winners;
        }

        public override List<ArenaSitting.ArenaPlayer> DetermineArenaSittingWinners(
            ArenaOnlineGameMode arenaOnline,
            ArenaSitting arenaSitting)
        {
            int maxScore = scoreByTeamIndex.Max(kvp => kvp.Value);

            List<int> bestTeamIndexes = totalScoreByTeamIndex
                .Where(kvp => kvp.Value == maxScore)
                .Select(kvp => kvp.Key)
                .ToList();

            winningTeamIndex = bestTeamIndexes.Count == 1
                ? bestTeamIndexes[0]
                : null;

            List<ArenaSitting.ArenaPlayer> winners = [];
            foreach (ArenaSitting.ArenaPlayer arenaPlayer in arenaSitting.players)
            {
                if (ArenaHelpers.FindOnlinePlayerByFakePlayerNumber(arenaOnline, arenaPlayer.playerNumber) is not OnlinePlayer onlinePlayer)
                {
                    RainMeadow.Warn($"Unable to find arena player A's online player. Player number: {arenaPlayer.playerNumber}");
                    continue;
                }
                if (!OnlineManager.lobby.clientSettings[onlinePlayer].TryGetData(out ArenaTeamClientSettings clientData))
                {
                    RainMeadow.Error($"Unable to find {onlinePlayer}'s team client data.");
                    continue;
                }

                if (clientData.team == winningTeamIndex)
                    winners.Add(arenaPlayer);
            }

            return winners;
        }

        public override string AddIcon(
            ArenaOnlineGameMode arena,
            OnlinePlayerDisplay display,
            PlayerSpecificOnlineHud owner,
            SlugcatCustomization customization,
            OnlinePlayer player
        )
        {

            if (base.AddIcon(arena, display, owner, customization, player) != "")
            {
                return base.AddIcon(arena, display, owner, customization, player);
            }

            if (OnlineManager.lobby.clientSettings.TryGetValue(key: player, out _) == false)
            {
                return "";
            }

            if (
                OnlineManager
                    .lobby.clientSettings[player]
                    .TryGetData<ArenaTeamClientSettings>(out var tb2)
            )
            {
                return teamIcons[tb2.team];
            }
            return "";
        }

        public override Color IconColor(
            ArenaOnlineGameMode arena,
            OnlinePlayerDisplay display,
            PlayerSpecificOnlineHud owner,
            SlugcatCustomization customization,
            OnlinePlayer player
        )
        {
            if (OnlineManager.lobby.clientSettings.TryGetValue(key: player, out _) == false)
            {
                return customization.bodyColor;
            }

            if (owner.PlayerConsideredDead)
            {
                return Color.grey;
            }

            if (
                OnlineManager
                    .lobby.clientSettings[player]
                    .TryGetData<ArenaTeamClientSettings>(out var tb2)
            )
            {
                if (player.isMe
                    && OnlineManager.lobby.clientSettings.TryGetValue(player, out var cs) 
                    && cs.chatUsernameColor is Color color)
                {
                    return color;
                }
                return teamColors[tb2.team];
            }

            return customization.bodyColor;
        }

        public override string ExportLocalSettings(ArenaOnlineGameMode arena)
        {
            string baseExport = base.ExportLocalSettings(arena);
            string decodedBase = string.IsNullOrEmpty(baseExport) ? "" : Encoding.UTF8.GetString(Convert.FromBase64String(baseExport));

            var pairs = new List<string>
            {
                $"chieftainsTeamNames={chieftainsTeamNames}",
                $"dragonSlayersTeamNames={dragonSlayersTeamNames}",
                $"lerp={lerp}",
                $"martyrsTeamName={martyrsTeamName}",
                $"outlawTeamNames={outlawTeamNames}",
            };

            string combined = string.Join("|", pairs);

            if (!string.IsNullOrEmpty(decodedBase))
            {
                combined = decodedBase + "|" + combined;
            }

            return Convert.ToBase64String(Encoding.UTF8.GetBytes(combined));
        }

        public override bool ImportLocalSettings(ArenaOnlineGameMode arena, string base64Data)
        {
            bool success = base.ImportLocalSettings(arena, base64Data);
            if (string.IsNullOrEmpty(base64Data)) return false;
            if (!success) return false;

            try
            {
                string decoded = Encoding.UTF8.GetString(Convert.FromBase64String(base64Data));
                string[] pairs = decoded.Split('|');

                foreach (string pair in pairs)
                {
                    string[] kvp = pair.Split('=');
                    if (kvp.Length != 2) continue;

                    string key = kvp[0];
                    string val = kvp[1];

                    switch (key)
                    {
                        case "chieftainsTeamNames":
                            chieftainsTeamNames = val;
                            teamNames[3] = val;
                            break;
                        case "dragonSlayersTeamNames":
                            dragonSlayersTeamNames = val;
                            teamNames[2] = val;
                            break;
                        case "lerp":
                            if (float.TryParse(val, out float f1)) lerp = f1;
                            break;
                        case "martyrsTeamName":
                            martyrsTeamName = val;
                            teamNames[0] = val;
                            break;
                        case "outlawTeamNames":
                            outlawTeamNames = val;
                            teamNames[1] = val;
                            break;
                    }
                }
                return true;
            }
            catch (Exception e)
            {
                RainMeadow.Error(e);
                return false;
            }
        }
    }
}
