using System;
using System.Collections.Generic;
using System.Linq;
using RainMeadow.Chat;
using RainMeadow.Exceptions;
using UnityEngine;

namespace RainMeadow
{
    public static class ChatLogManager
    {
        public static event Action<IChatMessage>? MessageLogged;

        public static List<IChatMessage> ChatMessages { get;} = [];

        // HACK: put this somewhere better
        public static bool shownChatTutorial = false;
        public static bool logErrorsInChat = false;

        private static Dictionary<MeadowPlayerId, Color> _colorByPlayerId = [];

        public static Color DefaultSystemColor { get; } = new(1f, 1f, 0.3333333f);
        public static Color OrangeSystemColor { get; } = new(1f, 0.55f, 0.25f);
        public static Color RedSystemColor { get; } = new(1f, 0.35f, 0.35f);

        public static readonly Dictionary<SystemMessage.Kind, Color> ColorBySystemMessageKind = new()
        {
            { SystemMessage.Kind.Custom, DefaultSystemColor },
            { SystemMessage.Kind.ErrorLog, Color.Lerp(RedSystemColor, Color.black, 0.25f) },
            { SystemMessage.Kind.Notification, RedSystemColor },
            { SystemMessage.Kind.PlayerJoin, DefaultSystemColor },
            { SystemMessage.Kind.PlayerJoinFail, Color.Lerp(DefaultSystemColor, Color.black, 0.5f) },
            { SystemMessage.Kind.Death, DefaultSystemColor },
            { SystemMessage.Kind.SessionStart, Color.Lerp(OrangeSystemColor, Color.black, 0.25f) },
            { SystemMessage.Kind.SessionResult, Color.Lerp(OrangeSystemColor, Color.black, 0.25f) },
            { SystemMessage.Kind.SittingResult, OrangeSystemColor }
        };

        public static bool ShouldPingForMessage(IChatMessage chatMessage)
        {
            switch (chatMessage)
            {
                case TextPlayerMessage playerMessage:
                    MeadowPlayerId myPlayerId = OnlineManager.mePlayer.id;

                    return RainMeadow.rainMeadowOptions.ChatPing.Value
                        && playerMessage.PlayerId != myPlayerId
                        && playerMessage.Text.IndexOf(myPlayerId.DisplayName, StringComparison.OrdinalIgnoreCase) != -1;

                case SystemMessage:
                    return false;

                default: throw new NonExhaustiveException(chatMessage);
            }
        }

        public static bool ShouldSoundPlayForMessage(IChatMessage chatMessage, out bool quieter)
        {
            switch (chatMessage)
            {
                case TextPlayerMessage playerMessage:
                    quieter = false;
                    if (playerMessage.PlayerId == OnlineManager.mePlayer.id)
                        return false;
                    break;

                case SystemMessage:
                    quieter = true;
                    break;

                default: throw new NonExhaustiveException(chatMessage);
            }

            return RainMeadow.rainMeadowOptions.ChatSound.Value
                && !ShouldPingForMessage(chatMessage);
        }

        public static void ToggleLogErrorInChat()
        {
            logErrorsInChat = !logErrorsInChat;

            string notificationText = logErrorsInChat
                ? Utils.Translate("Enabled Error Logging in chat.")
                : Utils.Translate("Disabled Error Logging in chat.");

            SystemMessage systemMessage = new(SystemMessage.Kind.Notification, notificationText);
            LogMessage(systemMessage);
        }

        public static bool IsPlayerMuted(MeadowPlayerId playerId)
        {
            bool globalMute = RainMeadow.rainMeadowOptions.GlobalMute.Value;

            return globalMute &&
                OnlineManager.lobby.gameMode?.mutedPlayers?
                    .Contains(playerId.GetPersonaName()) == true;
        }

        public static bool ShouldLogMessage(IChatMessage chatMessage)
        {
            switch (chatMessage)
            {
                case TextPlayerMessage playerMessage:
                    return !IsPlayerMuted(playerMessage.PlayerId);

                case SystemMessage systemMessage:
                    bool isArena = RainMeadow.isArenaMode(out _);
                    bool isStory = RainMeadow.isStoryMode(out _);

                    bool storyDeathNotification = RainMeadow.rainMeadowOptions.EnableChatStoryDeathNotification.Value;
                    bool arenaDeathNotification = RainMeadow.rainMeadowOptions.EnableChatArenaDeathNotification.Value;
                    bool storyJoinNotification = RainMeadow.rainMeadowOptions.EnableChatStoryJoinNotification.Value;
                    bool arenaJoinNotification = RainMeadow.rainMeadowOptions.EnableChatArenaJoinNotification.Value;
                    bool sessionNotification = RainMeadow.rainMeadowOptions.EnableChatSessionNotification.Value;
                    bool roundNotification = RainMeadow.rainMeadowOptions.EnableChatRoundNotification.Value;

                    // More extraction!!!
                    bool deathNotification = isStory && storyDeathNotification || isArena && arenaDeathNotification;
                    bool joinNotification = isStory && storyJoinNotification || isArena && arenaJoinNotification;

                    return systemMessage.MessageKind switch
                    {
                        SystemMessage.Kind.Death          => deathNotification,
                        SystemMessage.Kind.PlayerJoin     => joinNotification,
                        SystemMessage.Kind.PlayerJoinFail => joinNotification,
                        SystemMessage.Kind.SittingResult  => sessionNotification,
                        SystemMessage.Kind.SessionResult  => roundNotification,
                        SystemMessage.Kind.SessionStart   => roundNotification,
                        _                                 => true
                    };

                default: throw new NonExhaustiveException(chatMessage);
            }
        }

        public static void LogMessage(IChatMessage chatMessage)
        {
            if (!ShouldLogMessage(chatMessage))
                return;

            ChatMessages.Add(chatMessage);
            MessageLogged?.Invoke(chatMessage);
        }

        public static void UpdatePlayerColors()
        {
            foreach (OnlinePlayer onlinePlayer in OnlineManager.lobby.participants)
            {
                if (OnlineManager.lobby.clientSettings.TryGetValue(onlinePlayer, out var cs) && cs.chatUsernameColor is Color color)
                {
                    _colorByPlayerId[onlinePlayer.id] = color;
                }
                else if (OnlineManager.lobby.playerAvatars.Exists(kv => kv.Key == onlinePlayer)
                    && OnlineManager.lobby.playerAvatars.First(kv => kv.Key == onlinePlayer).Value?.FindEntity(true) is OnlinePhysicalObject opo)
                {
                    // If we successfully get the customization data, upsert
                    if (opo.TryGetData<SlugcatCustomization>(out var customization))
                        _colorByPlayerId[onlinePlayer.id] = customization.bodyColor;
                }
            }
        }

        public static bool TryGetPlayerColor(MeadowPlayerId playerId, out Color color)
        {
            if (_colorByPlayerId.TryGetValue(playerId, out var colorOrig))
            {
                Color.RGBToHSV(colorOrig, out float h, out float s, out float v);

                color = v < 0.8f
                    ? Color.HSVToRGB(h, s, 0.8f)
                    : colorOrig;

                return true;
            }

            color = default(Color);
            return false;
        }

        public static void ClearPlayerColors()
        {
            _colorByPlayerId.Clear();
            RainMeadow.Debug("Cleared player colors.");
        }

        public static void ClearChatLog()
        {
            _colorByPlayerId.Clear();
            RainMeadow.Debug("Cleared chat log.");
        }
    }
}
