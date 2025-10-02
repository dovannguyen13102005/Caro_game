using System;
using System.IO;
using System.Linq;
using System.Windows;
using Caro_game;

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
        var enginePath = Path.Combine(projectRoot, "AI", "pbrain-rapfi_avx2.exe");

        if (string.IsNullOrWhiteSpace(enginePath) || !File.Exists(enginePath))
        {
            NotifyProfessionalModeUnavailable("Không tìm thấy tệp AI cần thiết cho cấp độ Chuyên nghiệp.\n" +
                                              $"Đường dẫn: {enginePath}");
            return;
        }

        try
        {
            _engine = new EngineClient(enginePath);

            if (Rows == Columns)
            {
                _engine.StartSquare(Rows);
            }
            else if (!_engine.StartRect(Columns, Rows))
            {
                MessageBox.Show("AI không hỗ trợ kích thước bàn chữ nhật. Hãy chọn bàn vuông.",
                    "Chuyên nghiệp", MessageBoxButton.OK, MessageBoxImage.Warning);

                DisposeEngine();
                IsAIEnabled = false;
                AIMode = "Khó";
                return;
            }

            if (!string.IsNullOrWhiteSpace(_rule.EngineKeyword))
            {
                _engine.SetRule(_rule.EngineKeyword);
            }

            var configFile = GetEngineConfigFile();
            if (!string.IsNullOrWhiteSpace(configFile))
            {
                var configFullPath = Path.Combine(Path.GetDirectoryName(enginePath) ?? projectRoot, configFile);

                if (File.Exists(configFullPath))
                {
                    _engine.SetConfig(configFile);
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
}
