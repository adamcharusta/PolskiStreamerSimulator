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
}
