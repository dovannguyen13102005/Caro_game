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

        var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
        var engineDirectory = Path.Combine(baseDirectory, "AI");
        var engineCandidates = new[]
        {
            Path.Combine(engineDirectory, "pbrain-rapfi-windows-avx2.exe"),
            Path.Combine(engineDirectory, "pbrain-rapfi-windows-sse.exe"),
            Path.Combine(engineDirectory, "pbrain-rapfi.exe")
        };

        var enginePath = engineCandidates.FirstOrDefault(File.Exists);

        if (string.IsNullOrWhiteSpace(enginePath))
        {
            NotifyProfessionalModeUnavailable("Không tìm thấy tệp AI cần thiết cho cấp độ Chuyên nghiệp.\n" +
                                              string.Join("\n", engineCandidates.Select(path => $"Đã thử: {path}")));
            return;
        }

        var configFileName = GameRule switch
        {
            GameRule.Freestyle => "config_freestyle.toml",
            GameRule.Standard => "config_standard.toml",
            GameRule.Renju => "config_renju.toml",
            _ => "config.toml"
        };

        var configPath = Path.Combine(engineDirectory, configFileName);

        if (!File.Exists(configPath))
        {
            NotifyProfessionalModeUnavailable("Không tìm thấy tệp cấu hình AI phù hợp cho cấp độ Chuyên nghiệp.\n" +
                                              $"Đường dẫn: {configPath}");
            return;
        }

        try
        {
            var engineArgs = $"--config \"{configPath}\"";
            _engine = new EngineClient(enginePath, engineArgs);

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

            if (Cells.All(c => string.IsNullOrEmpty(c.Value)) && CurrentPlayer == AiPiece)
            {
                MaybeScheduleAiMove(null);
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
}
