// PlaceholderService.cs
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectCreator
{
    /// <summary>
    /// 提供和解析预设占位符（如日期、时间、GUID等）。
    /// </summary>
    public static class PlaceholderService
    {
        // 使用 Func<DateTime, string> 来延迟执行，确保每次都获取最新时间
        private static readonly Dictionary<string, Func<DateTime, string>> _presetGenerators;

        static PlaceholderService()
        {
            _presetGenerators = new Dictionary<string, Func<DateTime, string>>(StringComparer.OrdinalIgnoreCase)
            {
                // --- 日期 ---
                { "%year%", (now) => now.ToString("yyyy") }, // 2025
                { "%yy%", (now) => now.ToString("yy") },     // 25
                { "%month%", (now) => now.ToString("MM") },    // 10
                { "%month_name%", (now) => now.ToString("MMMM") }, // 十月
                { "%month_name_short%", (now) => now.ToString("MMM") }, // 十
                { "%day%", (now) => now.ToString("dd") },     // 30
                { "%day_of_week%", (now) => now.ToString("dddd") }, // 星期四
                { "%day_of_week_short%", (now) => now.ToString("ddd") }, // 四
                { "%day_of_year%", (now) => now.DayOfYear.ToString() }, // 303

                // --- 常用组合 ---
                { "%date_iso%", (now) => now.ToString("yyyy-MM-dd") }, // 2025-10-30
                { "%date_cn%", (now) => now.ToString("yyyy年MM月dd日") }, // 2025年10月30日
                { "%date_compact%", (now) => now.ToString("yyyyMMdd") }, // 20251030
                
                // --- 时间 ---
                { "%hour_24%", (now) => now.ToString("HH") }, // 17 (24小时制)
                { "%hour_12%", (now) => now.ToString("hh") }, // 05 (12小时制)
                { "%minute%", (now) => now.ToString("mm") },  // 15
                { "%second%", (now) => now.ToString("ss") },  // 30
                { "%am_pm%", (now) => now.ToString("tt") },   // PM

                // --- 组合 ---
                { "%datetime_iso%", (now) => now.ToString("yyyy-MM-ddTHH:mm:ss") },
                { "%datetime_compact%", (now) => now.ToString("yyyyMMddHHmmss") },

                // --- 其他 ---
                { "%guid%", (now) => Guid.NewGuid().ToString() }, // 唯一ID
                { "%username%", (now) => Environment.UserName }   // 当前Windows用户名
            };
        }

        /// <summary>
        /// 获取所有预设占位符的键（例如 "%year%"）。
        /// </summary>
        public static HashSet<string> GetPresetKeys()
        {
            return _presetGenerators.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 生成当前时间的预设占位符键值对。
        /// </summary>
        public static Dictionary<string, string> GetPresetVariables()
        {
            var now = DateTime.Now;
            return _presetGenerators.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value(now),
                StringComparer.OrdinalIgnoreCase
            );
        }

        /// <summary>
        /// 使用提供的变量字典解析输入字符串中的所有占位符。
        /// </summary>
        public static string Resolve(string input, Dictionary<string, string> variables)
        {
            if (string.IsNullOrEmpty(input) || variables == null) return input;

            // 使用 LINQ 的 Aggregate 方法高效替换所有占位符
            return variables.Aggregate(input, (current, variable) =>
                current.Replace(variable.Key, variable.Value, StringComparison.OrdinalIgnoreCase));
        }
    }
}