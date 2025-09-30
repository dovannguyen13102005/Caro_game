using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Caro_game.Models;

namespace Caro_game.Services;

public static class RapfiConfigManager
{
    private static readonly string[] KnownConfigFileNames =
    {
        "rapfi_config.toml",
        "rapfi.toml",
        "rapfi.cfg",
        "rapfi_config.cfg"
    };

    public static string PrepareConfig(string engineExecutablePath, GameRule rule)
    {
        var engineDirectory = Path.GetDirectoryName(engineExecutablePath);
        if (string.IsNullOrWhiteSpace(engineDirectory))
        {
            throw new InvalidOperationException("Không xác định được thư mục của engine Rapfi.");
        }

        ValidateRequiredFiles(engineDirectory, rule);

        var configContent = BuildConfig(rule);

        var configTargets = GetConfigTargets(engineDirectory).ToList();

        foreach (var path in configTargets)
        {
            File.WriteAllText(path, configContent, Encoding.UTF8);
        }

        return configTargets[0];
    }

    private static void ValidateRequiredFiles(string engineDirectory, GameRule rule)
    {
        EnsureFileExists(engineDirectory, "model210901.bin");

        switch (rule)
        {
            case GameRule.Freestyle:
                EnsureFileExists(engineDirectory, "mix9svqfreestyle_bsmix.bin.lz4");
                break;
            case GameRule.Standard:
                EnsureFileExists(engineDirectory, "mix9svqstandard_bs15.bin.lz4");
                break;
            case GameRule.Renju:
                EnsureFileExists(engineDirectory, "mix9svqrenju_bs15_black.bin.lz4");
                EnsureFileExists(engineDirectory, "mix9svqrenju_bs15_white.bin.lz4");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(rule), rule, "Luật chơi không được hỗ trợ.");
        }
    }

    private static void EnsureFileExists(string engineDirectory, string fileName)
    {
        var filePath = Path.Combine(engineDirectory, fileName);
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Không tìm thấy tệp {fileName} trong thư mục AI.", filePath);
        }
    }

    private static IEnumerable<string> GetConfigTargets(string engineDirectory)
    {
        var existing = KnownConfigFileNames
            .Select(name => Path.Combine(engineDirectory, name))
            .Where(File.Exists)
            .ToList();

        if (existing.Count == 0)
        {
            existing.Add(Path.Combine(engineDirectory, KnownConfigFileNames[0]));
        }

        return existing;
    }

    private static string BuildConfig(GameRule rule)
    {
        var sb = new StringBuilder();

        sb.AppendLine("[requirement]");
        sb.AppendLine("min_version = [0,43,1]");
        sb.AppendLine();
        sb.AppendLine("[general]");
        sb.AppendLine("reload_config_each_move = false");
        sb.AppendLine("clear_hash_after_config_loaded = false");
        sb.AppendLine("default_thread_num = 1");
        sb.AppendLine("message_mode = \"normal\"");
        sb.AppendLine("coord_conversion_mode = \"none\"");
        sb.AppendLine("default_candidate_range = \"square3_line4\"");
        sb.AppendLine("memory_reserved_mb = 0");
        sb.AppendLine("default_tt_size_kb = 32768");
        sb.AppendLine();
        sb.AppendLine("[model]");
        sb.AppendLine("binary_file = \"model210901.bin\"");
        sb.AppendLine();
        sb.AppendLine("[model.evaluator]");
        sb.AppendLine("type = \"mix9svq\"");
        sb.AppendLine("draw_black_winrate = 0.5");
        sb.AppendLine("draw_ratio = 1.0");
        sb.AppendLine();

        foreach (var weightSection in BuildWeightSections(rule))
        {
            sb.Append(weightSection);
        }

        sb.AppendLine("[search]");
        sb.AppendLine("aspiration_window = true");
        sb.AppendLine("num_iteration_after_mate = 24");
        sb.AppendLine("num_iteration_after_singular_root = 24");
        sb.AppendLine("max_search_depth = 99");
        sb.AppendLine();
        sb.AppendLine("[search.timectl]");
        sb.AppendLine("match_space = 21.0");
        sb.AppendLine("match_space_min = 7.0");
        sb.AppendLine("average_branch_factor = 1.7");
        sb.AppendLine("advanced_stop_ratio = 0.9");
        sb.AppendLine("move_horizon = 64");
        sb.AppendLine();
        sb.AppendLine("[database]");
        sb.AppendLine("enable_by_default = false");
        sb.AppendLine("type = \"yixindb\"");
        sb.AppendLine("url = \"rapfi.db\"");
        sb.AppendLine();
        sb.AppendLine("[database.yixindb]");
        sb.AppendLine("compressed_save = true");
        sb.AppendLine("save_on_close = true");
        sb.AppendLine("num_backups_on_save = 2");
        sb.AppendLine("ignore_corrupted = true");
        sb.AppendLine();
        sb.AppendLine("[database.search]");
        sb.AppendLine("readonly_mode = false");
        sb.AppendLine("query_ply = 3");
        sb.AppendLine("pv_iter_per_ply_increment = 1");
        sb.AppendLine("nonpv_iter_per_ply_increment = 2");
        sb.AppendLine("pv_write_ply = 0");
        sb.AppendLine("pv_write_min_depth = 25");
        sb.AppendLine("write_value_range = 800");
        sb.AppendLine("mate_write_ply = 2");
        sb.AppendLine("mate_write_min_depth_exact = 0");
        sb.AppendLine("mate_write_min_depth_nonexact = 0");
        sb.AppendLine("mate_write_min_step = 0");
        sb.AppendLine("exact_overwrite_ply = 100");
        sb.AppendLine("nonexact_overwrite_ply = 0");
        sb.AppendLine("overwrite_rule = \"better_value_depth_bound\"");
        sb.AppendLine("overwrite_exact_bias = 4");
        sb.AppendLine("overwrite_depth_bound_bias = -1");
        sb.AppendLine("query_result_depth_bound_bias = 0");
        sb.AppendLine();
        sb.AppendLine("[database.libfile]");
        sb.AppendLine("black_win_mark = \"a\"");
        sb.AppendLine("white_win_mark = \"c\"");
        sb.AppendLine("black_lose_mark = \"c\"");
        sb.AppendLine("white_lose_mark = \"a\"");
        sb.AppendLine("ignore_comment = false");

        return sb.ToString();
    }

    private static IEnumerable<string> BuildWeightSections(GameRule rule)
    {
        switch (rule)
        {
            case GameRule.Freestyle:
                yield return "[[model.evaluator.weights]]\n" +
                             "weight_file = \"mix9svqfreestyle_bsmix.bin.lz4\"\n\n";
                yield break;
            case GameRule.Standard:
                yield return "[[model.evaluator.weights]]\n" +
                             "weight_file = \"mix9svqstandard_bs15.bin.lz4\"\n\n";
                yield break;
            case GameRule.Renju:
                yield return "[[model.evaluator.weights]]\n" +
                             "weight_file_black = \"mix9svqrenju_bs15_black.bin.lz4\"\n" +
                             "weight_file_white = \"mix9svqrenju_bs15_white.bin.lz4\"\n\n";
                yield break;
            default:
                throw new ArgumentOutOfRangeException(nameof(rule), rule, "Luật chơi không được hỗ trợ.");
        }
    }
}
