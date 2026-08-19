using System;
using RainMeadow.Exceptions;
using RWCustom;

namespace RainMeadow.Chat;

public abstract record ChatMessage
{
    protected static InGameTranslator Translator => Custom.rainWorld.inGameTranslator;

    /// <remarks>This is guaranteed to never be empty.</remarks>
    public abstract string Text { get; }
}

public record TextPlayerMessage : ChatMessage
{
    public MeadowPlayerId PlayerId { get; }
    public override string Text { get; }

    /// <exception cref="ArgumentException">Thrown if <paramref name="text"/> is empty.</exception>
    public TextPlayerMessage(MeadowPlayerId playerId, string text)
    {
        if (text == "")
            throw new ArgumentException("A message cannot be empty.", nameof(text));

        PlayerId = playerId;
        Text = text;
    }
}

public record TextSystemMessage : ChatMessage
{
    /// <inheritdoc/>
    public override string Text { get; }

    /// <exception cref="ArgumentException">Thrown if <paramref name="text"/> is empty.</exception>
    public TextSystemMessage(string text)
    {
        if (text == "")
            throw new ArgumentException("A message cannot be empty.", nameof(text));

        Text = text;
    }
}

public record ErrorLogMessage : ChatMessage
{
    /// <inheritdoc/>
    public override string Text { get; }

    /// <exception cref="ArgumentException">Thrown if <paramref name="text"/> is empty.</exception>
    public ErrorLogMessage(string text)
    {
        if (text == "")
            throw new ArgumentException("A message cannot be empty.", nameof(text));

        Text = text;
    }
}

public record NotificationMessage : ChatMessage
{
    /// <inheritdoc/>
    public override string Text { get; }

    /// <exception cref="ArgumentException">Thrown if <paramref name="text"/> is empty.</exception>
    public NotificationMessage(string text)
    {
        if (text == "")
            throw new ArgumentException("A message cannot be empty.", nameof(text));

        Text = text;
    }
}

public record PlayerJoinMessage : ChatMessage
{
    public enum JoinResult
    {
        Success,
        WasKickedBefore
    }

    /// <inheritdoc/>
    public override string Text
    {
        get
        {
#pragma warning disable CS8524 // The switch expression does not handle some values of its input type (it is not exhaustive) involving an unnamed enum value.
            return MessageJoinResult switch
            {
                JoinResult.Success         => $"{PlayerId.DisplayName} {Translator.Translate("joined the game.")}",
                JoinResult.WasKickedBefore => $"{PlayerId.DisplayName} {Translator.Translate("tried to join the game but was kicked.")}"
            };
#pragma warning restore CS8524 // The switch expression does not handle some values of its input type (it is not exhaustive) involving an unnamed enum value.
        }
    }

    public MeadowPlayerId PlayerId { get; }
    public JoinResult MessageJoinResult { get; }

    public PlayerJoinMessage(MeadowPlayerId playerId, JoinResult messageJoinResult)
    {
        PlayerId = playerId;
        MessageJoinResult = messageJoinResult;
    }
}

public record SessionStartMessage : ChatMessage
{
    /// <inheritdoc/>
    public override string Text =>
        $"{Translator.Translate("Starting match in")} {MultiplayerUnlocks.LevelDisplayName(RoomName)}";

    // TODO: Is 'RoomName' correct?
    public string RoomName { get; }

    public SessionStartMessage(string roomName)
    {
        // TODO: Throw if invalid room name
        RoomName = roomName;
    }
}

public record SittingResultMessage : ChatMessage
{
    /// <inheritdoc/>
    public override string Text
    {
        get
        {
            string text = Translator.Translate("SESSION ENDED!");
            if (IsSpecific)
                text += $" {ResultText}";;

            return text;
        }
    }
    /// <remarks>
    /// This is guaranteed to not be empty if
    /// <see cref="IsSpecific"/> is <see langword="true"/>.
    /// </remarks>
    public string ResultText { get; }
    public bool IsSpecific { get; }

    /// <exception cref="ArgumentException">
    /// Thrown if <paramref name="resultText"/> is empty when
    /// <paramref name="isSpecific"/> is <see langword="true"/>.
    /// </exception>
    public SittingResultMessage(string resultText, bool isSpecific)
    {
        if (IsSpecific && resultText == "")
        {
            throw new ArgumentException(
                "Result text cannot be empty if it is supposed to be specific.",
                nameof(resultText)
            );
        }

        ResultText = resultText;
        IsSpecific = isSpecific;
    }
}

public record EnvironmentalDeathMessage : ChatMessage
{
    public enum Kind
    {
        Invalid,
        Rain,
        Abyss,
        Drown,
        FallDamage,
        Oracle,
        Burn,
        PyroDeath,
        Freeze,
        WormGrass,
        WallRot,
        Electric,
        DeadlyLick,
        Coalescipede,
        SoloExplosion,
        Sandstorm,
        Poison,
        Lightning,
        Locust,
        Ripple,
        Pomegranate,
        Fire,
        UnderwaterShock
    }

    /// <inheritdoc/>
    public override string Text
    {
        get
        {
            switch (MessageKind)
            {
                case Kind.Rain:
                    return $"{Victim.DisplayName} {Translator.Translate("was crushed by the rain.")}";

                case Kind.Abyss:
                    return Attacker is null
                        ? $"{Victim.DisplayName} {Translator.Translate("fell into the abyss.")}"
                        : $"{Victim.DisplayName} {Translator.Translate("fell into the abyss thanks to")} {Attacker.DisplayName}.";

                case Kind.Drown:
                    if (Attacker is not null)
                        return $"{Victim.DisplayName} {Translator.Translate("was drowned by")} {Attacker.DisplayName}.";

                    if (AttackerTemplateName is not null)
                        return $"{Victim.DisplayName} {Translator.Translate("was drowned by a")} {Translator.Translate(AttackerTemplateName)}";

                    return $"{Victim.DisplayName} {Translator.Translate("drowned.")}";

                case Kind.FallDamage:
                    return Attacker is null
                        ? $"{Victim.DisplayName} {Translator.Translate("hit the ground too hard.")}"
                        : $"{Victim.DisplayName} {Translator.Translate("hit the ground too hard thanks to")} {Attacker.DisplayName}.";

                case Kind.Oracle:
                    return $"{Victim.DisplayName} {Translator.Translate("was killed through unknown means.")}";

                case Kind.Burn:
                    return Attacker is null
                        ? $"{Victim.DisplayName} {Translator.Translate("tried to swim in burning liquid.")}"
                        : $"{Victim.DisplayName} {Translator.Translate("tried to swim in burning liquid to escape")} {Attacker.DisplayName}.";

                case Kind.PyroDeath:
                    return $"{Victim.DisplayName} {Translator.Translate("spontaneously combusted.")}";

                case Kind.Freeze:
                    return $"{Victim.DisplayName} {Translator.Translate("froze to death.")}";

                case Kind.WormGrass:
                    return $"{Victim.DisplayName} {Translator.Translate("was swallowed by the grass.")}";

                case Kind.WallRot:
                    return Attacker is null
                        ? $"{Victim.DisplayName} {Translator.Translate("was swallowed by the walls.")}"
                        : $"{Victim.DisplayName} {Translator.Translate("was swallowed by the walls thanks to")} {Attacker.DisplayName}.";

                case Kind.Electric:
                    return $"{Victim.DisplayName} {Translator.Translate("was electrocuted.")}";

                case Kind.DeadlyLick:
                    return $"{Victim.DisplayName} {Translator.Translate("licked the power.")}";

                case Kind.Coalescipede:
                    return $"{Victim.DisplayName} {Translator.Translate("was consumed by the swarm.")}";

                case Kind.UnderwaterShock:
                    return $"{Victim.DisplayName} {Translator.Translate("was electrocuted in the water.")}";

                case Kind.SoloExplosion:
                    return $"{Victim.DisplayName} {Translator.Translate("blew up.")}";

                case Kind.Sandstorm:
                    return $"{Victim.DisplayName} {Translator.Translate("asphyxiated.")}";

                case Kind.Poison:
                    return $"{Victim.DisplayName} {Translator.Translate("died from poison.")}";

                case Kind.Lightning:
                    return $"{Victim.DisplayName} {Translator.Translate("was struck by lightning.")}";

                case Kind.Locust:
                    return $"{Victim.DisplayName} {Translator.Translate("was consumed by locusts.")}";

                case Kind.Ripple:
                    return $"{Victim.DisplayName} {Translator.Translate("swam with the ripples.")}";

                case Kind.Pomegranate:
                    return $"{Victim.DisplayName} {Translator.Translate("experienced the force of fresh fruit.")}";

                case Kind.Fire:
                    return $"{Victim.DisplayName} {Translator.Translate("was burnt to a crisp.")}";

                case Kind.Invalid:
                    return $"{Victim.DisplayName} {Translator.Translate("died.")}";

                default: throw new NonExhaustiveException(MessageKind);
            }
        }
    }
    public MeadowPlayerId Victim { get; }
    public MeadowPlayerId? Attacker { get; }
    public string? AttackerTemplateName { get; }
    public Kind MessageKind { get; }

    public EnvironmentalDeathMessage(MeadowPlayerId victim, Kind messageKind)
    {
        Victim = victim;
        MessageKind = messageKind;
    }

    public EnvironmentalDeathMessage(MeadowPlayerId victim, MeadowPlayerId attacker, Kind messageKind)
    {
        Victim = victim;
        Attacker = attacker;
        MessageKind = messageKind;
    }

    public EnvironmentalDeathMessage(MeadowPlayerId victim, string attackerTemplateName, Kind messageKind)
    {
        Victim = victim;
        AttackerTemplateName = attackerTemplateName;
        MessageKind = messageKind;
    }
}

public record PvpDeathMessage : ChatMessage
{
    public enum Kind
    {
        Unspecified,
        Ascension,
        Explosion,
    }

    /// <inheritdoc/>
    public override string Text
    {
        get
        {
#pragma warning disable CS8524 // The switch expression does not handle some values of its input type (it is not exhaustive) involving an unnamed enum value.
            return MessageKind switch
            {
                Kind.Unspecified => $"{Victim.DisplayName} {Translator.Translate("was slain by")} {Attacker.DisplayName}",
                Kind.Ascension   => $"{Victim.DisplayName} {Translator.Translate("was ascended by")} {Attacker.DisplayName}",
                Kind.Explosion   => $"{Victim.DisplayName} {Translator.Translate("was blown up by")} {Attacker.DisplayName}"
            };
#pragma warning restore CS8524 // The switch expression does not handle some values of its input type (it is not exhaustive) involving an unnamed enum value.
        }
    }

    public MeadowPlayerId Victim { get; }
    public MeadowPlayerId Attacker { get; }
    public Kind MessageKind { get; }

    public PvpDeathMessage(MeadowPlayerId victim, MeadowPlayerId attacker, Kind messageKind)
    {
        Victim = victim;
        Attacker = attacker;
        MessageKind = messageKind;
    }
}

public record PvcDeathMessage : ChatMessage
{
    public enum Kind
    {
        Unspecified,
        Ascension,
        Explosion
    }

    /// <inheritdoc/>
    public override string Text
    {
        get
        {
#pragma warning disable CS8524 // The switch expression does not handle some values of its input type (it is not exhaustive) involving an unnamed enum value.
            return MessageKind switch
            {
                Kind.Unspecified => $"{Translator.Translate(AttackerTemplateName)} {Translator.Translate("was slain by")} {Victim.DisplayName}",
                Kind.Ascension   => $"{Translator.Translate(AttackerTemplateName)} {Translator.Translate("was ascended by")} {Victim.DisplayName}",
                Kind.Explosion   => $"{Translator.Translate(AttackerTemplateName)} {Translator.Translate("was blown up by")} {Victim.DisplayName}",
            };
#pragma warning restore CS8524 // The switch expression does not handle some values of its input type (it is not exhaustive) involving an unnamed enum value.
        }
    }

    public MeadowPlayerId Victim { get; }
    public string AttackerTemplateName { get; }
    public Kind MessageKind { get; }

    public PvcDeathMessage(MeadowPlayerId victim, string attackerTemplateName, Kind messageKind)
    {
        Victim = victim;
        AttackerTemplateName = attackerTemplateName;
        MessageKind = messageKind;
    }
}

public record CvpDeathMessage : ChatMessage
{
    /// <inheritdoc/>
    public override string Text => AttackerType == CreatureTemplate.Type.Centipede
            ? $"{Victim.DisplayName} {Translator.Translate("was zapped by a")} {Translator.Translate(AttackerTemplateName)}."
            : $"{Victim.DisplayName} {Translator.Translate("was slain by a")} {Translator.Translate(AttackerTemplateName)}.";

    public MeadowPlayerId Victim { get; }
    public string AttackerTemplateName { get; }
    public CreatureTemplate.Type AttackerType { get; }

    public CvpDeathMessage(MeadowPlayerId victim, string attackerTemplateName, CreatureTemplate.Type attackerType)
    {
        Victim = victim;
        AttackerTemplateName = attackerTemplateName;
        AttackerType = attackerType;
    }
}
