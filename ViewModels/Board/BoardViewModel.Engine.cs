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

            // ✅ Nếu bàn trống và lượt đầu tiên thuộc AI → cho AI đi 
            if (Cells != null && Cells.All(c => string.IsNullOrEmpty(c.Value)) && CurrentPlayer == _aiSymbol)
            {
                var aiMove = _engine.Begin();
                PlaceAiIfValid(aiMove);
            }

            SyncProfessionalEngineWithMoves();
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

    private void SyncProfessionalEngineWithMoves()
    {
        if (_engine == null || _moveHistory.Count == 0)
        {
            return;
        }

        try
        {
            var moves = _moveHistory
                .Select(m => (X: m.Col, Y: m.Row, Player: GetPlayerValue(m.Player)))
                .ToList();

            var response = _engine.SyncBoard(moves);

            if (!string.IsNullOrWhiteSpace(response) && CurrentPlayer == _aiSymbol && !IsPaused)
            {
                if (TryParseMove(response, out var x, out var y) &&
                    _cellLookup.TryGetValue((y, x), out var cell) &&
                    string.IsNullOrEmpty(cell.Value))
                {
                    PlaceAiIfValid(response);
                }
            }
        }
        catch (Exception ex)
        {
            Application.Current.Dispatcher?.Invoke(() =>
            {
                MessageBox.Show(
                    $"Không thể đồng bộ trạng thái với AI Chuyên nghiệp.\nChi tiết: {ex.Message}",
                    "Chuyên nghiệp",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            });
        }
    }

    private static bool TryParseMove(string? move, out int x, out int y)
    {
        x = y = -1;
        if (string.IsNullOrWhiteSpace(move))
        {
            return false;
        }

        var parts = move.Split(',');
        if (parts.Length != 2)
        {
            return false;
        }

        return int.TryParse(parts[0], out x) && int.TryParse(parts[1], out y);
    }
}
