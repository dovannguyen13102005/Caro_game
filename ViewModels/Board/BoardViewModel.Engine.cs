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

            // ✅ Nếu bàn trống và lượt đầu tiên thuộc AI → cho AI đi luôn
            if (!_skipProfessionalAutoMoveDuringInit &&
                Cells != null &&
                Cells.All(c => string.IsNullOrEmpty(c.Value)) &&
                CurrentPlayer == _aiSymbol)
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


    public bool RestoreProfessionalEngineState()
    {
        _skipProfessionalAutoMoveDuringInit = false;

        if (AIMode != "Chuyên nghiệp" || _engine == null)
        {
            return false;
        }

        if (_moveHistory.Count == 0)
        {
            if (Cells.Any(c => !string.IsNullOrEmpty(c.Value)))
            {
                Application.Current.Dispatcher?.Invoke(() =>
                {
                    MessageBox.Show(
                        "Bản lưu không chứa lịch sử nước đi nên không thể khôi phục AI Chuyên nghiệp.\n" +
                        "AI sẽ chuyển sang cấp độ Khó để tiếp tục ván đấu.",
                        "Chuyên nghiệp",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                });

                AIMode = "Khó";
            }

            return false;
        }

        try
        {
            var stones = _moveHistory
                .Select(m => (X: m.Col, Y: m.Row, Player: NormalizePlayer(m.Player)))
                .ToList();

            var response = _engine.SyncBoard(stones);
            bool aiTurn = IsAIEnabled && !IsPaused && CurrentPlayer == _aiSymbol;

            if (aiTurn && !string.IsNullOrWhiteSpace(response))
            {
                PlaceAiIfValid(response);
                return true;
            }
        }
        catch (Exception ex)
        {
            Application.Current.Dispatcher?.Invoke(() =>
            {
                MessageBox.Show(
                    $"Không thể khôi phục trạng thái AI Chuyên nghiệp.\nChi tiết: {ex.Message}",
                    "Chuyên nghiệp",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            });

            DisposeEngine();
        }

        return false;
    }

    private static int NormalizePlayer(string? player)
        => string.Equals(player, "O", StringComparison.OrdinalIgnoreCase) ? 2 : 1;



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
