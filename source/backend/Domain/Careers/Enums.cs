namespace PolskiStreamerSimulator.Domain.Careers;

public enum Background
{
    DormRoomNewcomer,
    LocalMemeCreator,
    ChessClubRegular,
    IndustryInsider,
}

public enum Difficulty
{
    Easy,
    Normal,
}

/// <summary>Grammatical form only; no rule may read it.</summary>
public enum PronounSet
{
    Woman,
    Man,
    Nonbinary,
}

public enum Category
{
    JustChatting,
    Fps,
    Moba,
    Chess,
    Gambling,
}

public enum EquipmentSlot
{
    Microphone,
    Camera,
    Computer,
    Connection,
}

public enum CareerStatus
{
    Active,
    Ended,
}

public enum EndingReason
{
    Completed,
    Insolvency,
    EarlyRetirement,
    GlobalBan,
    AllPlatformsBanned,
}

/// <summary>M3-B: the streaming platform a career is on.</summary>
public enum Platform
{
    Czatnia,
    PulsLive,
    Nurt,
}

/// <summary>M3-B: stake size of the weekly wager.</summary>
public enum WagerTier
{
    Low,
    Medium,
    High,
}

/// <summary>M3-B: a one-off sponsor deal or a multi-week contract.</summary>
public enum SponsorKind
{
    OneOff,
    Contract,
}
