using Menu;
using RainMeadow.UI.Components;
using System.Linq;
using UnityEngine;
using RainMeadow.UI;
using Drown;
using System;
using System.Text;
using System.Collections.Generic;
using RWCustom;

namespace RainMeadow
{
    public class DrownMode : ExternalArenaGameMode
    {

        public static string Rock = "Rock";
        public static string Spear = "Spear";
        public static string ExplosiveSpear = "Explosive Spear";
        public static string ScavengerBomb = "Scavenger Bomb";
        public static string ElectricSpear = "Electric Spear";
        public static string Boomerang = "Boomerang";
        public static string Respawn = "Respawn";
        public static string OpenDens = "Open Dens";


        public static ArenaSetup.GameTypeID Drown = new ArenaSetup.GameTypeID("Drown", register: false);
        public override ArenaSetup.GameTypeID GetGameModeId
        {
            get
            {
                return Drown;
            }

        }

        public override bool ShowAddedScoreBetweenRoundsInOnlinePlayerUI { get => false; set { } }

        public override Dialog AddGameModeInfo(ArenaOnlineGameMode arena, Menu.Menu menu)
        {
            return new DialogNotify(menu.LongTranslate("Kill & survive to buy your escape<LINE><LINE>Turn off Spear Hits for Co-Op"), new Vector2(500f, 400f), menu.manager, () => { menu.PlaySound(SoundID.MENU_Button_Standard_Button_Pressed); });
        }

        public static bool isDrownMode(ArenaOnlineGameMode arena, out DrownMode mode)
        {
            mode = null;
            if (arena.currentGameMode == Drown.value)
            {
                mode = (arena.registeredGameModes.FirstOrDefault(x => x.Key == Drown.value).Value as DrownMode);
                return true;
            }
            return false;
        }

        public const int StartingScore = 5;

        public int spearCost = RainMeadow.rainMeadowOptions.DrownPointsForSpear.Value;
        public int spearExplCost = RainMeadow.rainMeadowOptions.DrownPointsForExplSpear.Value;
        public int bombCost = RainMeadow.rainMeadowOptions.DrownPointsForBomb.Value;
        public int electricSpearCost = RainMeadow.rainMeadowOptions.DrownPointsForElectricSpear.Value;
        public int boomerangeCost = RainMeadow.rainMeadowOptions.DrownPointsForBoomerang.Value;
        public int respCost = RainMeadow.rainMeadowOptions.DrownPointsForRespawn.Value;
        public int rockCost = RainMeadow.rainMeadowOptions.DrownPointsForRock.Value;

        public int denCost = RainMeadow.rainMeadowOptions.DrownPointsForDenOpen.Value;
        public int maxCreatures = RainMeadow.rainMeadowOptions.DrownMaxCreatureCount.Value;
        public int creatureCleanupWaves = RainMeadow.rainMeadowOptions.DrownCreatureCleanup.Value;

        private int _timerDuration;
        public bool openedDen = false;
        public int waveStart = 20;
        public int currentWaveTimer = 20;
        public int currentWave = 0;
        public int lastCleanupWave = 0;
        public bool waveNeedsUpdate = true;

        /// <summary>
        /// Helper property that calculates the sum of all active players'
        /// <see cref="ArenaSitting.ArenaPlayer.score"/> values.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown when:<br/>
        /// - There is no active <see cref="ArenaGameSession"/>.
        /// - <see cref="ArenaSetup.GameTypeSetup.spearsHitPlayers"/> is <see langword="true"/>.
        /// </exception>
        public int TeamScore
        {
            get
            {
                ArenaOnlineGameMode arenaOnline = (ArenaOnlineGameMode)OnlineManager.lobby.gameMode;
                ArenaSitting arenaSitting = arenaOnline.session.arenaSitting;

                if (arenaOnline.session is null)
                    throw new InvalidOperationException("No active arena session exists.");
                if (arenaOnline.session.GameTypeSetup.spearsHitPlayers)
                    throw new InvalidOperationException("Spear hits must be off for team score to exist.");


                int score = 0;
                foreach (ArenaSitting.ArenaPlayer arenaPlayer in arenaSitting.players)
                {
                    OnlinePlayer? onlinePlayer = ArenaHelpers.FindOnlinePlayerByFakePlayerNumber(arenaOnline, arenaPlayer.playerNumber);
                    if (onlinePlayer is null)
                        continue;
                    if (arenaPlayer.playerClass == RainMeadow.Ext_SlugcatStatsName.OnlineOverseerSpectator)
                        continue;

                    score += arenaPlayer.score;
                }

                return score;
            }
        }

        public DrownInterface? drownInterface;
        public TabContainer.Tab? myTab;

        public override bool On_ArenaBehaviors_ExitManager_ExitsOpen(ArenaOnlineGameMode arena, On.ArenaBehaviors.ExitManager.orig_ExitsOpen orig, ArenaBehaviors.ExitManager self)
        {
            if (self.gameSession != null && self.gameSession.GameTypeSetup.wildLifeSetting == ArenaSetup.GameTypeSetup.WildLifeSetting.Off && self.gameSession.thisFrameActivePlayers == 1 && arena.setupTime > 10)
            {
                return true;
            }

            return openedDen;

        }


        public override bool SpawnBatflies(FliesWorldAI self, int spawnRoom)
        {
            return false;
        }

        public override void On_ArenaGameSession_ctor(ArenaOnlineGameMode arena, On.ArenaGameSession.orig_ctor orig, ArenaGameSession self, RainWorldGame game)
        {
            base.On_ArenaGameSession_ctor(arena, orig, self, game);
            openedDen = false;
            currentWave = 1;
            lastCleanupWave = 0;


            ArenaSitting arenaSitting = arena.session.arenaSitting;

            foreach (ArenaSitting.ArenaPlayer arenaPlayer in arenaSitting.players)
            {
                OnlinePlayer? onlinePlayer = ArenaHelpers.FindOnlinePlayerByFakePlayerNumber(arena, arenaPlayer.playerNumber);

                if (onlinePlayer is null)
                    continue;
                if (arenaPlayer.playerClass == RainMeadow.Ext_SlugcatStatsName.OnlineOverseerSpectator)
                    continue;

                if (OnlineManager.lobby.isOwner)
                {
                    arenaPlayer.score = StartingScore;
                    arena.CopyStatsToLobbyData(arenaPlayer, onlinePlayer);
                }
                else
                    arena.CopyStatsFromLobbyData(arenaPlayer, onlinePlayer);
            }
        }

        public override void InitAsCustomGameType(ArenaOnlineGameMode arenaOnline, ArenaSetup.GameTypeSetup self)
        {
            base.InitAsCustomGameType(arenaOnline, self);

            // self.savingAndLoadingSession = true; // TODO: Is this fine (in base method)
            self.survivalScore = 0;
            self.repeatSingleLevelForever = false;
            self.rainWhenOnePlayerLeft = false;
            self.fliesSpawn = true; // TODO: Check when RW sets this value. Is it related to SpawnBatflies()?
        }

        public override string TimerText()
        {
            ArenaOnlineGameMode arenaOnline = (ArenaOnlineGameMode)OnlineManager.lobby.gameMode;
            ArenaSetup.GameTypeSetup gameTypeSetup = arenaOnline.session.GameTypeSetup;


            string scoreTypeText = gameTypeSetup.spearsHitPlayers
                ? "Current points"
                : "Team points";

            int displayScore = gameTypeSetup.spearsHitPlayers
                ? ArenaHelpers.FindArenaPlayerByOnlinePlayer(arenaOnline, OnlineManager.mePlayer)!.score
                : TeamScore;

            string waveText = gameTypeSetup.wildLifeSetting == ArenaSetup.GameTypeSetup.WildLifeSetting.Off
                ? ""
                : $" Current Wave: {currentWave}. Next wave: {ArenaPrepTimer.FormatTime(currentWaveTimer)}";


            return $": {scoreTypeText}: {displayScore}.{waveText}";
        }

        public override int SetTimer(ArenaOnlineGameMode arena)
        {
            return arena.setupTime = 1;
        }

        public override void ResetGameTimer()
        {
            _timerDuration = 1;

        }

        public override int TimerDuration
        {
            get { return _timerDuration; }
            set { _timerDuration = value; }
        }

        public override int TimerDirection(ArenaOnlineGameMode arena, int timer)
        {
            if (!openedDen)
            {

                currentWaveTimer--;
                if (currentWaveTimer == 0)
                {
                    currentWaveTimer = waveStart;
                    waveNeedsUpdate = true;
                }

                return ++arena.setupTime;
            }
            else
            {
                return arena.setupTime;
            }
        }

        public override void On_Player_Die(
            ArenaOnlineGameMode arenaOnline,
            On.Player.orig_Die orig,
            Player self)
        {
            // Prevent empty score changes.
            orig(self);
        }

        public override void HUD_InitMultiplayerHud(ArenaOnlineGameMode arena, HUD.HUD self, ArenaGameSession session)
        {
            base.HUD_InitMultiplayerHud(arena, self, session);
            self.AddPart(new StoreHUD(self, session.game.cameras[0], this));
        }

        public override bool HoldFireWhileTimerIsActive(ArenaOnlineGameMode arena)
        {
            return arena.countdownInitiatedHoldFire = false;
        }

        public override string AddIcon(ArenaOnlineGameMode arena, OnlinePlayerDisplay display, PlayerSpecificOnlineHud owner, SlugcatCustomization customization, OnlinePlayer player)
        {
            if (player != null)
            {
                OnlineManager.lobby.clientSettings.TryGetValue(player, out var cs);
                if (cs != null)
                {

                    cs.TryGetData<ArenaDrownClientSettings>(out var clientSettings);
                    if (clientSettings != null && clientSettings.isInStore)
                    {
                        return "spearSymbol";
                    }
                    else
                    {
                        return "Kill_Slugcat";

                    }
                }
            }


            return base.AddIcon(arena, display, owner, customization, player);
        }

        public override Color IconColor(ArenaOnlineGameMode arena, OnlinePlayerDisplay display, PlayerSpecificOnlineHud owner, SlugcatCustomization customization, OnlinePlayer player)
        {
            if (owner.PlayerConsideredDead)
            {
                return Color.grey;
            }

            return base.IconColor(arena, display, owner, customization, player);
        }



        public override void OnUIEnabled(ArenaOnlineLobbyMenu menu)
        {
            base.OnUIEnabled(menu);
            myTab = menu.arenaMainLobbyPage.tabContainer.AddTab(menu.Translate("Drown Settings"));
            myTab.AddObjects(drownInterface = new DrownInterface((ArenaOnlineGameMode)OnlineManager.lobby.gameMode, this, myTab.menu, myTab, new(0, 0), menu.arenaMainLobbyPage.tabContainer.size));
        }
        public override void OnUIDisabled(ArenaOnlineLobbyMenu menu)
        {
            base.OnUIDisabled(menu);
            drownInterface?.OnShutdown();
            if (myTab != null) menu.arenaMainLobbyPage.tabContainer.RemoveTab(myTab);
            myTab = null;
        }

        public override void On_ArenaGameSession_Update(On.ArenaGameSession.orig_Update orig, ArenaGameSession self, ArenaOnlineGameMode arena)
        {

            if (isDrownMode(arena, out var drown))
            {
                if (!self.sessionEnded)
                {
                    for (int i = 0; i < self.Players.Count; i++)
                    {
                        var onlinePlayer = OnlinePhysicalObject.map.TryGetValue(self.Players[i], out var onlineP);
                        if (onlinePlayer)
                        {
                            if (self.Players[i].state.alive)
                            {
                                bool openedDen = false;
                                OnlineManager.lobby.clientSettings.TryGetValue(onlineP.owner, out var cs);
                                if (cs != null)
                                {

                                    cs.TryGetData<ArenaDrownClientSettings>(out var clientSettings);
                                    if (clientSettings != null)
                                    {
                                        openedDen = clientSettings.iOpenedDen;
                                    }
                                }

                                if (drown.openedDen && !openedDen && self.Players[i] != null && self.Players[i].realizedCreature != null && self.Players[i].realizedCreature.State.alive && self.GameTypeSetup.spearsHitPlayers)
                                {
                                    self.game.cameras[0].hud.PlaySound(SoundID.UI_Slugcat_Die);
                                    self.Players[i].realizedCreature.Die();
                                }
                            }
                        }

                    }
                }

                if (!openedDen)
                {
                    if (currentWaveTimer % waveStart == 0 && self.playersSpawned && waveNeedsUpdate)
                    {
                        var creatureAlive = 0;
                        for (int i = 0; i < self.room.abstractRoom.creatures.Count; i++)
                        {
                            var currentCreature = self.room.abstractRoom.creatures[i];

                            // Check if the creature is actually realized in the room
                            if (currentCreature.realizedCreature != null)
                            {
                                // Check if it is alive and not a Slugcat
                                if (currentCreature.state.alive && currentCreature.creatureTemplate.type != CreatureTemplate.Type.Slugcat)
                                {
                                    creatureAlive++;
                                }
                            }
                        }
                        if (creatureAlive < maxCreatures)
                        {
                            self.SpawnCreatures();
                        }
                        currentWave++;
                    }
                    if (currentWave % creatureCleanupWaves == 0 && currentWave > lastCleanupWave)
                    {
                        lastCleanupWave = currentWave;

                        CreatureCleanup(arena, self);
                    }
                    waveNeedsUpdate = false;
                }
            }
            base.On_ArenaGameSession_Update(orig, self, arena);

        }

        public override List<ArenaSitting.ArenaPlayer> DetermineArenaSessionWinners(
            ArenaOnlineGameMode arenaOnline,
            ArenaGameSession arenaSession)
        {
            ArenaSitting arenaSitting = arenaSession.arenaSitting;
            List<ArenaSitting.ArenaPlayer> winners = [];

            foreach (ArenaSitting.ArenaPlayer arenaPlayer in arenaSitting.players)
            {
                OnlinePlayer? onlinePlayer = ArenaHelpers.FindOnlinePlayerByFakePlayerNumber(arenaOnline, arenaPlayer.playerNumber);
                if (onlinePlayer is null)
                    continue;
                if (arenaPlayer.playerClass == RainMeadow.Ext_SlugcatStatsName.OnlineOverseerSpectator)
                    continue;

                if (!OnlineManager.lobby.clientSettings[onlinePlayer].TryGetData(out ArenaDrownClientSettings clientData))
                {
                    RainMeadow.Error($"Unable to find {onlinePlayer}'s drown client data.");
                    continue;
                }

                if (clientData.iOpenedDen || !arenaOnline.session.GameTypeSetup.spearsHitPlayers)
                    winners.Add(arenaPlayer);
            }

            return winners;
        }

        private void CreatureCleanup(ArenaOnlineGameMode arena, ArenaGameSession session)
        {
            if (RoomSession.map.TryGetValue(session.room.abstractRoom, out var roomSession))
            {
                var entities = session.room.abstractRoom.entities;
                for (int i = entities.Count - 1; i >= 0; i--)
                {
                    if (entities[i] is AbstractPhysicalObject apo && apo is AbstractCreature ac && ac.state.dead && ac.realizedCreature.grabbedBy.Count <= 0 && OnlinePhysicalObject.map.TryGetValue(apo, out var oe))
                    {
                        for (int num = ac.stuckObjects.Count - 1; num >= 0; num--)
                        {
                            if (ac.stuckObjects[num] is AbstractPhysicalObject.AbstractSpearStick && ac.stuckObjects[num].A.type == AbstractPhysicalObject.AbstractObjectType.Spear && ac.stuckObjects[num].A.realizedObject != null)
                            {
                                (ac.stuckObjects[num].A.realizedObject as Spear).ChangeMode(Weapon.Mode.Free);
                            }
                        }
                        oe.RemoveEntityFromRoom();
                        oe.RemoveEntityFromGame();
                    }
                }
            }
        }

        public override string ExportLocalSettings(ArenaOnlineGameMode arena)
        {
            string baseExport = base.ExportLocalSettings(arena);
            string decodedBase = string.IsNullOrEmpty(baseExport) ? "" : Encoding.UTF8.GetString(Convert.FromBase64String(baseExport));

            var pairs = new List<string>
            {
                $"bombCost={bombCost}",
                $"boomerangeCost={boomerangeCost}",
                $"creatureCleanupWaves={creatureCleanupWaves}",
                $"denCost={denCost}",
                $"electricSpearCost={electricSpearCost}",
                $"maxCreatures={maxCreatures}",
                $"respCost={respCost}",
                $"rockCost={rockCost}",
                $"spearCost={spearCost}",
                $"spearExplCost={spearExplCost}",
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
            if (string.IsNullOrEmpty(base64Data)) return false;
            bool success = base.ImportLocalSettings(arena, base64Data);
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

                    // Sorted alphanumerically
                    switch (key)
                    {
                        case "bombCost":
                            if (int.TryParse(val, out int i1)) bombCost = i1;
                            break;
                        case "boomerangeCost":
                            if (int.TryParse(val, out int i2)) boomerangeCost = i2;
                            break;
                        case "creatureCleanupWaves":
                            if (int.TryParse(val, out int i3)) creatureCleanupWaves = i3;
                            break;
                        case "denCost":
                            if (int.TryParse(val, out int i4)) denCost = i4;
                            break;
                        case "electricSpearCost":
                            if (int.TryParse(val, out int i5)) electricSpearCost = i5;
                            break;
                        case "maxCreatures":
                            if (int.TryParse(val, out int i6)) maxCreatures = i6;
                            break;
                        case "respCost":
                            if (int.TryParse(val, out int i7)) respCost = i7;
                            break;
                        case "rockCost":
                            if (int.TryParse(val, out int i8)) rockCost = i8;
                            break;
                        case "spearCost":
                            if (int.TryParse(val, out int i9)) spearCost = i9;
                            break;
                        case "spearExplCost":
                            if (int.TryParse(val, out int i10)) spearExplCost = i10;
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
