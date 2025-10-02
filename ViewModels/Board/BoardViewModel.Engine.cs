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
        if (!IsAIEnabled)
        {
            return;
        }

        DisposeEngine();

        // 🔹 Xác định thư mục gốc project (từ bin quay ngược ra)
        var projectRoot = Path.GetFullPath(
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\")
        );

        // 🔹 Đường dẫn tới AI ngoài repo
        var aiDirectory = Path.Combine(projectRoot, "AI");
        var enginePath = Path.Combine(aiDirectory, "pbrain-rapfi_avx2.exe");
        var configPath = ResolveEngineConfigPath(aiDirectory);

        if (string.IsNullOrWhiteSpace(enginePath) || !File.Exists(enginePath))
        {
            NotifyProfessionalModeUnavailable("Không tìm thấy tệp AI cần thiết cho cấp độ Chuyên nghiệp.\n" +
                                              $"Đường dẫn: {enginePath}");
            return;
        }

        if (configPath != null && !File.Exists(configPath))
        {
            NotifyProfessionalModeUnavailable("Thiếu tệp cấu hình luật cho engine Rapfi.\n" +
                                              $"Đường dẫn: {configPath}");
            return;
        }

        try
        {
            _engine = new EngineClient(enginePath, configPath);

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

    private string? ResolveEngineConfigPath(string aiDirectory)
    {
        string? fileName = Rule switch
        {
            GameRuleType.Freestyle => "config_freestyle.toml",
            GameRuleType.Standard => "config_standard.toml",
            GameRuleType.Renju => _aiSymbol == "X" ? "config_renju_black.toml" : "config_renju_white.toml",
            _ => null
        };

        return fileName == null ? null : Path.Combine(aiDirectory, fileName);
    }

    public void DisposeEngine()
    {
        _engine?.Dispose();
        _engine = null;
    }
}
