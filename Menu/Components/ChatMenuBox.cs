using System.Collections.Generic;
using System.Linq;
using Menu;
using Menu.Remix.MixedUI;
using RainMeadow.Chat;
using RainMeadow.Exceptions;
using UnityEngine;

namespace RainMeadow.UI.Components
{
    public class ChatMenuBox : RectangularMenuObject // Subscribe/Unsubscribe from ChatLogManager.MessageLogged somewhere in main process.
    {
        public const int MaxVisibleMessageLabels = 25;

        public RoundedRect roundedRect;
        public ChatTextBox chatTypingBox;
        public ButtonScroller messageScroller;

        public ChatMenuBox(Menu.Menu menu, MenuObject owner, Vector2 pos, Vector2 size) : base(menu, owner, pos, size)
        {
            roundedRect = new(menu, this, Vector2.zero, this.size, true) { fillAlpha = 0.3f };
            chatTypingBox = new(menu, this, "", new(10, 10), new(this.size.x - 30, 30), true);
            //chatTypingBox = new(menu, this, "", new(10, 10), new(this.size.x - 30, 30));
            chatTypingBox.OnTextSubmit += () =>
            {
                if (messageScroller != null) messageScroller.MoveAtBottom();
            };
            float posYOffset = chatTypingBox.size.y + 10;
            messageScroller = new(menu, this, new(chatTypingBox.pos.x, chatTypingBox.pos.y + posYOffset), new(chatTypingBox.size.x, this.size.y - chatTypingBox.size.y - chatTypingBox.pos.y - 10), true, new(-5, -posYOffset), posYOffset - 25)
            {
                sliderDefaultIsDown = true,
                buttonHeight = 20,
                buttonSpacing = 3,
                textAnchor = RainMeadow.rainMeadowOptions.ChatTextDownscroll.Value 
                    ? ButtonScroller.TextAnchor.Bottom 
                    : ButtonScroller.TextAnchor.Top
            };
            menu.MutualHorizontalButtonBind(chatTypingBox, messageScroller.scrollSlider);
            subObjects.AddRange([roundedRect, chatTypingBox, messageScroller]);

            for (int i = Mathf.Max(0, ChatLogManager.ChatMessages.Count - MaxVisibleMessageLabels - 1); i < ChatLogManager.ChatMessages.Count; i++)
            {
                AddNewMessageToScroller(ChatLogManager.ChatMessages[i]);
            }
        }

        public AlignedMenuLabel CreateMessageLabel(
            IChatMessage chatMessage,
            string text,
            bool isFirstOfSplitLabels,
            Vector2 pos_,
            Vector2 size_)
        {
            switch (chatMessage)
            {
                case TextPlayerMessage playerMessage:
                {
                    string personaName = playerMessage.PlayerId.GetPersonaName();

                    if (isFirstOfSplitLabels)
                    {
                        UsernameMenuLabel userLabel = new(menu, messageScroller, personaName, pos_, size_, false)
                        {
                            labelPosAlignment = FLabelAlignment.Left,
                            verticalLabelPosAlignment = OpLabel.LabelVAlignment.Bottom,
                            label =
                            {
                                alignment = FLabelAlignment.Left,
                                color = ChatLogManager.TryGetPlayerColor(playerMessage.PlayerId, out Color foundColor)
                                    ? foundColor
                                    : MenuColorEffect.rgbMediumGrey
                            }
                        };

                        AlignedMenuLabel messageWithUserLabel = new(
                            menu,
                            userLabel,
                            $": {text}",
                            new Vector2(LabelTest.GetWidth($"{personaName}: ") + (userLabel.Host ? 14 : 0), 0),
                            userLabel.size,
                            false)
                        {
                            labelPosAlignment = FLabelAlignment.Left,
                            verticalLabelPosAlignment = OpLabel.LabelVAlignment.Bottom,
                            label = { alignment = FLabelAlignment.Left }
                        };

                        userLabel.subObjects.Add(messageWithUserLabel);
                        return userLabel;
                    }

                    AlignedMenuLabel messageLabel = new(menu, messageScroller, text, pos_, size_, false)
                    {
                        labelPosAlignment = FLabelAlignment.Left,
                        verticalLabelPosAlignment = OpLabel.LabelVAlignment.Bottom,
                        label = { alignment = FLabelAlignment.Left }
                    };

                    return messageLabel;
                }

                case SystemMessage systemMessage:
                {
                    AlignedMenuLabel messageLabel = new(menu, messageScroller, text, pos_, size_, false)
                    {
                        labelPosAlignment = FLabelAlignment.Left,
                        verticalLabelPosAlignment = OpLabel.LabelVAlignment.Bottom,
                        label =
                        {
                            alignment = FLabelAlignment.Left,
                            color = ChatLogManager.ColorBySystemMessageKind[systemMessage.MessageKind]
                        }
                    };

                    return messageLabel;
                }

                default: throw new NonExhaustiveException(chatMessage);
            }
        }

        public void AddNewMessageToScroller(IChatMessage chatMessage)
        {
            bool setNewScrollPosToLatest = messageScroller.IsAtBottom();
            messageScroller.AddScrollObjects(CreateMessageLabels(chatMessage));
            if (setNewScrollPosToLatest) messageScroller.MoveAtBottom();
        }

        public AlignedMenuLabel[] CreateMessageLabels(IChatMessage chatMessage)
        {
            float maxWidth = messageScroller.size.x - 5;
            float maxFirstTextWidth;
            Vector2 desiredSize = new(maxWidth, messageScroller.buttonHeight);

            switch (chatMessage)
            {
                case TextPlayerMessage playerMessage:
                    bool isFromHost = playerMessage.PlayerId == OnlineManager.lobby.owner.id;
                    string personaName = playerMessage.PlayerId.GetPersonaName();

                    maxFirstTextWidth = (maxWidth - LabelTest.GetWidth($"{personaName}: ")) + (isFromHost ? 14f : 0f);
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

            AlignedMenuLabel[] messageLabels = splitTextList
                .Select(
                    (text, i) => CreateMessageLabel(
                        chatMessage,
                        text,
                        i == 0,
                        new Vector2(
                            5,
                            messageScroller.GetIdealPosWithScrollForButton(i + messageScroller.buttons.Count).y
                        ),
                        desiredSize
                    )
                )
                .ToArray();

            return messageLabels;
        }

        public void OnMessageLogged(IChatMessage chatMessage)
        {
            if (!menu.Active)
                return;

            if (ChatLogManager.ShouldPingForMessage(chatMessage))
                menu.manager.menuMic.PlaySound(RainMeadow.Ext_SoundID.RM_Slugcat_Call, 0, 1f, 1.2f);

            if (ChatLogManager.ShouldSoundPlayForMessage(chatMessage, out bool quieter))
            {
                menu.manager.menuMic.PlaySound(
                    quieter ? SoundID.MENU_First_Scroll_Tick : SoundID.MENU_Scroll_Tick,
                    0,
                    quieter ? 0.7f : 1.5f,
                    quieter ? 0.7f : 0.6f
                );
            }

            AddNewMessageToScroller(chatMessage);
        }
    }
}
