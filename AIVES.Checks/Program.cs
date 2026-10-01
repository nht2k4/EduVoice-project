// Kiểm tra nhanh logic cốt lõi của Chức năng 2 + 3, không cần database/internet: dotnet run --project AIVES.Checks
using AIVES.Business.Interfaces;
using AIVES.Business.Models;
using AIVES.Business.Services;

// 1) Hai thí sinh thi liên tiếp không trùng câu, câu hỏi được chia đều
var pool = Enumerable.Range(1, 8).ToList();
var sets = QuestionAllocator.Allocate(pool, 20, 3, new Random(1));
for (var i = 0; i < sets.Count; i++)
{
    Assert(sets[i].Length == 3 && sets[i].Distinct().Count() == 3, $"thí sinh {i}: đủ 3 câu khác nhau");
    if (i > 0) Assert(!sets[i].Intersect(sets[i - 1]).Any(), $"thí sinh {i} trùng thí sinh {i - 1}");
}
var usage = sets.SelectMany(s => s).GroupBy(x => x).Select(g => g.Count()).ToList();
Assert(usage.Max() - usage.Min() <= 2, "câu hỏi được giao khá đều nhau");

// Ngân hàng quá nhỏ (4 câu, mỗi người 3 câu): buộc phải dùng lại, nhưng vẫn đủ 3 câu khác nhau
var tight = QuestionAllocator.Allocate(new[] { 1, 2, 3, 4 }, 5, 3, new Random(2));
Assert(tight.All(s => s.Distinct().Count() == 3), "ngân hàng nhỏ vẫn đủ câu");

// 2) AI dự phòng (không có ApiKey): câu quá ngắn thì hỏi xoáy, trả lời đủ ý thì không hỏi
var ai = new FollowUpGenerator(new AiOptions());
FollowUpContext Ctx(string answer) => new("Giải thích DI?", "AddScoped;AddSingleton", "vi-VN", new[] { ("Giải thích DI?", answer) });

Assert((await ai.DecideAsync(Ctx("Không biết"))).Ask, "câu trả lời ngắn phải bị hỏi xoáy");
var missing = await ai.DecideAsync(Ctx("Dependency injection là cách ASP.NET Core cung cấp đối tượng qua constructor, mình dùng AddScoped để đăng ký service theo từng request"));
Assert(missing.Ask && missing.Question!.Contains("AddSingleton"), "thiếu ý AddSingleton phải được hỏi lại");
var full = await ai.DecideAsync(Ctx("Dependency injection cung cấp đối tượng qua constructor, đăng ký bằng AddScoped cho mỗi request hoặc AddSingleton cho toàn ứng dụng"));
Assert(!full.Ask, "đủ ý thì không hỏi thêm");

Console.WriteLine("OK: mọi kiểm tra đều đạt.");

static void Assert(bool condition, string message)
{
    if (!condition) throw new Exception("FAIL: " + message);
}
