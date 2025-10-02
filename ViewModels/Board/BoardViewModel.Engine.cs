using System;
using System.IO;
using System.Linq;
using System.Windows;
using Caro_game;
using Caro_game.Models;

namespace Caro_game.ViewModels;

public partial class BoardViewModel
{
    private void TryInitializeProfessionalEngine()
    {
        DisposeEngine();

        // 🔹 Xác định thư mục gốc project (từ bin quay ngược ra)
        var projectRoot = Path.GetFullPath(
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\")
        );

        // 🔹 Đường dẫn tới AI ngoài repo
        var aiFolder = Path.Combine(projectRoot, "AI");
        var enginePath = Path.Combine(aiFolder, "pbrain-rapfi_avx2.exe");

        if (string.IsNullOrWhiteSpace(enginePath) || !File.Exists(enginePath))
        {
            NotifyProfessionalModeUnavailable("Không tìm thấy tệp AI cần thiết cho cấp độ Chuyên nghiệp.\n" +
                                              $"Đường dẫn: {enginePath}");
            return;
        }

        try
        {
            _engine = new EngineClient(enginePath);

            var startResponse = Rows == Columns
                ? _engine.StartSquare(Rows)
                : _engine.StartRect(Columns, Rows);

            if (ResponseIndicatesError(startResponse))
            {
                NotifyProfessionalModeUnavailable($"AI từ chối khởi động với kích thước bàn hiện tại.\nPhản hồi: {startResponse}");
                return;
            }

            if (!string.IsNullOrEmpty(startResponse) && !startResponse.StartsWith("OK", StringComparison.OrdinalIgnoreCase))
            {
                LogEngineSetupIssue($"Unexpected start response: {startResponse}");
            }

            if (!string.IsNullOrWhiteSpace(_rule.EngineKeyword))
            {
                _engine.SetRule(_rule.EngineKeyword);
            }

            var configFile = GetEngineConfigFile();
            if (!string.IsNullOrWhiteSpace(configFile))
            {
                var configFullPath = Path.Combine(aiFolder, configFile);

                if (File.Exists(configFullPath))
                {
                    _engine.SetConfig(configFullPath);
                }
                else
                {
                    LogEngineSetupIssue($"Không tìm thấy tệp cấu hình AI: {configFullPath}");
                }
            }

            // ✅ Nếu bàn trống và lượt đầu tiên thuộc AI → cho AI đi luôn
            if (Cells != null && Cells.All(c => string.IsNullOrEmpty(c.Value)) && CurrentPlayer == _aiSymbol)
            {
                var aiMove = _engine.Begin();
                if (ResponseIndicatesError(aiMove))
                {
                    NotifyProfessionalModeUnavailable($"AI trả về lỗi khi bắt đầu.\nPhản hồi: {aiMove}");
                    return;
                }

                PlaceAiIfValid(aiMove);
            }
        }
        catch (Exception ex)
        {
            NotifyProfessionalModeUnavailable($"Không thể khởi động AI Chuyên nghiệp.\nChi tiết: {ex}");
        }
    }



    private void NotifyProfessionalModeUnavailable(string message)
    {
        IsAIEnabled = false;
        AIMode = "Khó";

        Application.Current.Dispatcher?.Invoke(() =>
        {
            MessageBox.Show(message, "Caro", MessageBoxButton.OK, MessageBoxImage.Warning);
        });
    }

    public void DisposeEngine()
    {
        _engine?.Dispose();
        _engine = null;
    }

    private static void LogEngineSetupIssue(string message)
    {
        try
        {
            var logFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "engine_log.txt");
            File.AppendAllText(logFile, $"{DateTime.Now:HH:mm:ss} [SetupWarning] {message}{Environment.NewLine}");
        }
        catch
        {
            // ignore logging failures
        }
    }

    private string? GetEngineConfigFile()
    {
        return RuleType switch
        {
            GameRuleType.Freestyle => "config_freestyle.toml",
            GameRuleType.Standard => "config_standard.toml",
            GameRuleType.Renju => _aiSymbol == "X" ? "config_renju_black.toml" : "config_renju_white.toml",
            _ => null
        };
    }

    private static bool ResponseIndicatesError(string? response)
        => !string.IsNullOrWhiteSpace(response) &&
           response.StartsWith("ERROR", StringComparison.OrdinalIgnoreCase);
}
