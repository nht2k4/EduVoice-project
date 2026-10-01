namespace AIVES.Business.Models;

// Giá trị lưu trong database (cột VARCHAR có CHECK constraint)
public static class ExamStatuses
{
    public const string Open = "Open";
    public const string Closed = "Closed";
}

public static class ParticipantStatuses
{
    public const string Scheduled = "Scheduled";
    public const string InProgress = "InProgress";
    public const string Completed = "Completed";
}

public static class TurnKinds
{
    public const string Main = "Main";
    public const string FollowUp = "FollowUp";
}

// Cấu hình AI sinh câu hỏi xoáy. Để trống ApiKey thì hệ thống dùng bộ luật dự phòng (không cần internet).
public class AiOptions
{
    public string? ApiKey { get; set; }
    public string Model { get; set; } = "deepseek-chat";
}
