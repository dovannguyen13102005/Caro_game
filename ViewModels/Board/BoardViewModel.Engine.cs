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
        var engineFile = GameRule == GameRule.Renju
            ? "pbrain-Yixin2018.exe"
            : "pbrain-rapfi-windows-avx2.exe";

        var enginePath = Path.Combine(baseDirectory, "AI", engineFile);

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

            // ⚡ Gửi luật Renju cho Yixin
            if (GameRule == GameRule.Renju)
            {
                _engine.SendRaw("RULE renju");
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
