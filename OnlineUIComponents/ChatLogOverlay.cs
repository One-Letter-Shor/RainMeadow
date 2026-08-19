using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Menu;
using Menu.Remix.MixedUI;
using RainMeadow.Chat;
using RainMeadow.Exceptions;
using RainMeadow.UI.Components;
using UnityEngine;

namespace RainMeadow
{
    public class ChatLogOverlay : MenuObject
    {
        public IChatMessage[] ChatLog = [];
        public ButtonScroller scroller; //idk, makes things easier to manage ;-;
        private ChatHud chatHud;
        private List<float> msgExtents;
        private FSprite[] chatBg;
        private float bgSideOffset = 20;
        private const int maxMessagesHistoryOnStart = 40;
        private const float textOffsetSquishFix = 0.01f; // thanks Five Blue Moons for this fix on this... weird chat text bug
        private readonly int messageHistoryStart; 

        private const int maxVisibleMessages = 13;
        private Rect chatRect;

        public float opacity = 1.0f;
        private float lastOpacity = 1.0f;
        public int inactivityTimer;

        private FSprite? debug;

        public ChatLogOverlay(ChatHud chatHud, ProcessManager manager) : base(RMOverlayHUDMenu.GetOverlayMenu(), RMOverlayHUDMenu.GetOverlayMenu().pages[0])
        {
            // if (chatHud.hud is RMOverlayHUD) this.container = chatHud.hud.fContainers[1];
            
            this.chatHud = chatHud;

            chatBg = [];
            Array.Resize(ref chatBg, maxVisibleMessages);
            for (int i = 0; i < chatBg.Length; ++i)
            {
                chatBg[i] = new("pixel")
                {
                    anchorX = 0,
                    anchorY = 0,
                    color = Color.black,
                    alpha = Mathf.Clamp01(RainMeadow.rainMeadowOptions.ChatBgOpacity.Value),
                };
                this.Container.AddChild(chatBg[i]);
            }
            msgExtents = [];

            scroller = new(this.menu, this, new(1366f - 660f - manager.rainWorld.screenSize.x / 2 - bgSideOffset, 330 - maxVisibleMessages * 20), new(manager.rainWorld.screenSize.x / 2.7f + bgSideOffset, maxVisibleMessages * 20))
            {
                buttonHeight = 20,
                textAnchor = RainMeadow.rainMeadowOptions.ChatTextDownscroll.Value 
                    ? ButtonScroller.TextAnchor.Bottom 
                    : ButtonScroller.TextAnchor.Top 
            };
            this.subObjects.Add(scroller);
            
            this.messageHistoryStart = Mathf.Max(0, ChatLogManager.ChatMessages.Count - maxMessagesHistoryOnStart);
            UpdateLogDisplay();
            scroller.scrollOffset = scroller.DownScrollOffset = chatHud.logScrollPos == -1? scroller.MaxDownScroll : chatHud.logScrollPos;

            chatRect = new Rect(scroller.pos, scroller.size).CloneWithExpansion(20);
            // debug = new("pixel")
            // {
            //    anchorX = 0,
            //    anchorY = 0,
            //    color = Color.red,
            //    alpha = Mathf.Clamp01(RainMeadow.rainMeadowOptions.ChatBgOpacity.Value),
            // };
            // pages[0].Container.AddChild(debug);
        }

        public override void RemoveSprites()
        {
            base.RemoveSprites();
            chatBg.Do(x => x.RemoveFromContainer());
        }

        public override void Update()
        {
            base.Update();
            OpacityUpdate();
            inactivityTimer++;
        }

        public override void GrafUpdate(float timeStacker)
        {
            /// Obtains the first visible button index on the scroller
            int GetFirstIndex()
            {
                for (int i = 0; i < scroller.buttons.Count; ++i)
                    if (scroller.buttons[i].Alpha >= 0.5f && scroller.buttons[i].Pos.y >= scroller.LowerBound)
                        return i;
                return 0;
            }
            base.GrafUpdate(timeStacker);

            if (debug != null)
            {
                debug.x = chatRect.x;
                debug.y = chatRect.y;
                debug.width = chatRect.width;
                debug.height = chatRect.height;
            }

            float tOpacity = Mathf.Lerp(lastOpacity, opacity, timeStacker);

            // Make everything "invisible" by default (just 0-sized)
            for (int i = 0; i < chatBg.Length; ++i)
            {
                chatBg[i].scaleX = 0;
                chatBg[i].scaleY = 0;
            }
            int firstIndex = GetFirstIndex();
            // float longestMessage = 0;
            for (int i = 0; i < chatBg.Length; ++i)
            {
                int j = firstIndex + i;
                if (j >= 0 && j < scroller.buttons.Count)
                {
                    // We'll bypass IPartOfButtonScroller.Alpha and modify just the labels directly so
                    // messages fading out work as intended.
                    if (scroller.buttons[j] is AlignedMenuLabel label)
                    {
                        label.label.alpha = tOpacity;
                        foreach(var subObj in label.subObjects)
                        {
                            if (subObj is AlignedMenuLabel sub) sub.label.alpha = tOpacity;
                        }
                    }

                    chatBg[i].x = scroller.pos.x + scroller.buttons[j].Pos.x - 4f;
                    chatBg[i].y = scroller.pos.y + scroller.buttons[j].Pos.y;
                    chatBg[i].scaleX = msgExtents[j] + 8f;
                    chatBg[i].scaleY = scroller.ButtonHeightAndSpacing + 1f;
                    chatBg[i].alpha = tOpacity * (scroller.buttons[j].Alpha * Mathf.Clamp01(RainMeadow.rainMeadowOptions.ChatBgOpacity.Value));
                }
            }
        }

        public void OpacityUpdate()
        {
            // If the chat input is open or we aren't in game we won't check for players.
            if (chatHud.chatInputActive || chatHud.camera is null)
            {
                lastOpacity = 1.0f;
                opacity = 1.0f;
                inactivityTimer = 0;
                return;
            }

            lastOpacity = opacity;
            if (msgExtents.Count > 0)
            {
                // TODO only check messages currently visible
                chatRect.width = msgExtents.Max() + 20;
            }

            bool fade = false;

            if (inactivityTimer > RainMeadow.rainMeadowOptions.ChatInactivityTimer.Value * 40)
            {
                fade = true;
            }
            else
            {
                foreach (var avatar in OnlineManager.lobby.playerAvatars)
                {
                    var entity = avatar.Value.FindEntity(true);
                    if (entity is OnlineCreature oc && oc.abstractCreature != null && oc.abstractCreature.realizedCreature != null && !oc.abstractCreature.realizedCreature.dead)
                    {
                        if (chatRect.Contains(oc.abstractCreature.realizedCreature.mainBodyChunk.pos - chatHud.camera.pos))
                        {
                            // A player avatar is currently being obscured by chat.
                            fade = true;
                            break;
                        }
                    }
                }
            }

            if (fade)
            {
                opacity = Mathf.Max(RainMeadow.rainMeadowOptions.ChatInactivityOpacity.Value, opacity - 0.05f);
            }
            else
            {
                opacity = Mathf.Min(1.0f, opacity + 0.05f);
            }
        }

        public void UpdateLogDisplay()
        {
            if (ChatLogManager.ChatMessages.Count > ChatLog.Length + messageHistoryStart)
            {
                ChatLogManager.UpdatePlayerColors();
                float maxWidth = scroller.size.x - bgSideOffset * 2, xPos = bgSideOffset + textOffsetSquishFix;

                IEnumerable<IChatMessage> newMessages = ChatLogManager.ChatMessages.Skip(ChatLog.Length + messageHistoryStart);

                foreach (IChatMessage chatMessage in newMessages)
                {
                    float maxFirstTextWidth;
                    switch (chatMessage)
                    {
                        case TextPlayerMessage playerMessage:
                            string personaName = playerMessage.PlayerId.GetPersonaName();
                            maxFirstTextWidth = maxWidth - LabelTest.GetWidth($"{personaName}: ");
                            break;

                        case SystemMessage:
                            maxFirstTextWidth = maxWidth;
                            break;

                        default: throw new NonExhaustiveException(chatMessage);
                    }

                    List<string> splitTextList = MenuHelpers
                        .SmartSplitIntoFixedStrings(chatMessage.Text, maxFirstTextWidth, 1, out string remainingMessage)
                        .ToList();
                    splitTextList.AddRange(MenuHelpers.SmartSplitIntoStrings(remainingMessage, maxWidth));

                    for (int i = 0; i < splitTextList.Count; i++)
                    {
                        float yPos = scroller.GetIdealYPosWithScroll(scroller.buttons.Count) + textOffsetSquishFix;
                        string text = splitTextList[i];

                        switch (chatMessage)
                        {
                            case TextPlayerMessage playerMessage:
                                string personaName = playerMessage.PlayerId.GetPersonaName();

                                if (i == 0)
                                {
                                    Color color = ChatLogManager.TryGetPlayerColor(
                                        playerMessage.PlayerId,
                                        out Color foundColor
                                    )
                                        ? foundColor
                                        : default(Color);

                                    UsernameMenuLabel personaNameLabel = new(
                                        menu,
                                        scroller,
                                        personaName,
                                        new Vector2(xPos, yPos),
                                        new Vector2(0f, 20f),
                                        false)
                                    {
                                        label =
                                        {
                                            alignment = FLabelAlignment.Left,
                                            color = color
                                        }
                                    };

                                    AlignedMenuLabel messageWithUserLabel = new(menu, personaNameLabel, $": {text}", new Vector2(LabelTest.GetWidth($"{personaNameLabel}: ") + (personaNameLabel.Host ? 14 : 0), 0), new Vector2(0, 20), false)
                                    {
                                        labelPosAlignment = FLabelAlignment.Left,
                                        label = { alignment = FLabelAlignment.Left }
                                    };

                                    personaNameLabel.subObjects.Add(messageWithUserLabel);
                                    scroller.AddScrollObjects(personaNameLabel);
                                    msgExtents.Add(LabelTest.GetWidth($"{personaName}: {text}") + 4f + (personaNameLabel.Host ? 14f : 0));
                                }
                                else
                                {
                                    AlignedMenuLabel messageLabel = new(
                                        menu,
                                        scroller,
                                        text,
                                        new Vector2(xPos, yPos),
                                        new Vector2(0f, 20f),
                                        false)
                                    { label = { alignment = FLabelAlignment.Left } };

                                    scroller.AddScrollObjects(messageLabel);
                                    msgExtents.Add(LabelTest.GetWidth(text) + 4f);
                                }
                                break;

                            case SystemMessage systemMessage:
                                AlignedMenuLabel systemMessageLabel = new(menu, scroller, text, new Vector2(xPos, yPos), new Vector2(0, 20), false)
                                {
                                    label =
                                    {
                                        alignment = FLabelAlignment.Left,
                                        color = ChatLogManager.ColorBySystemMessageKind[systemMessage.MessageKind]
                                    }
                                };
                                scroller.AddScrollObjects(systemMessageLabel);
                                msgExtents.Add(LabelTest.GetWidth(text) + 2f);
                                break;

                            default: throw new NonExhaustiveException(chatMessage);
                        }
                    }
                }
                ChatLog = [.. ChatLogManager.ChatMessages.Skip(messageHistoryStart)];
                inactivityTimer = 0;
            }
        }
    }
}
