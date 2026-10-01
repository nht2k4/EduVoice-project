namespace AIVES.Business.Services;

// Chọn bộ câu hỏi cho từng thí sinh theo thứ tự thi.
// Thích ứng theo mức đã dùng: câu nào ít được giao nhất thì được ưu tiên, và câu của thí sinh ngay trước
// chỉ bị dùng lại khi ngân hàng câu hỏi không đủ. Nhờ đó hai thí sinh thi liên tiếp không trùng câu.
public static class QuestionAllocator
{
    public static List<int[]> Allocate(IReadOnlyList<int> pool, int students, int perStudent, Random rng)
    {
        var used = pool.ToDictionary(id => id, _ => 0);
        var result = new List<int[]>();
        var previous = new HashSet<int>();

        for (var i = 0; i < students; i++)
        {
            var picked = pool
                .OrderBy(id => previous.Contains(id) ? 1 : 0)
                .ThenBy(id => used[id])
                .ThenBy(_ => rng.Next())
                .Take(perStudent)
                .ToArray();

            foreach (var id in picked) used[id]++;
            previous = picked.ToHashSet();
            result.Add(picked.OrderBy(_ => rng.Next()).ToArray());
        }

        return result;
    }
}
