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

            RestoreProfessionalEngineStateFromHistory();

            if (!_isRestoringState)
            {
                ResumePendingAiTurn();
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
        _pendingResumeAfterLoad = false;
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

    private void RestoreProfessionalEngineStateFromHistory()
    {
        if (_engine == null)
        {
            return;
        }

        _pendingResumeAfterLoad = false;

        if (_moveHistory.Count == 0)
        {
            if (CurrentPlayer == _aiSymbol && !IsPaused)
            {
                _pendingResumeAfterLoad = true;
            }

            return;
        }

        int index = 0;

        if (_moveHistory[0].Player == _aiSymbol)
        {
            ValidateRestoredAiMove(_engine.Begin(), _moveHistory[0]);
            index++;
        }

        while (index < _moveHistory.Count)
        {
            var move = _moveHistory[index];

            if (move.Player != _humanSymbol)
            {
                throw new InvalidOperationException("Thứ tự nước đi đã lưu không hợp lệ.");
            }

            bool hasFollowingAiMove = index + 1 < _moveHistory.Count &&
                                      _moveHistory[index + 1].Player == _aiSymbol;

            if (!hasFollowingAiMove)
            {
                break;
            }

            var expectedAiMove = _moveHistory[index + 1];
            var aiResponse = _engine.Turn(move.Col, move.Row);
            ValidateRestoredAiMove(aiResponse, expectedAiMove);

            index += 2;
        }

        if (CurrentPlayer == _aiSymbol && !IsPaused)
        {
            _pendingResumeAfterLoad = true;
        }
    }

    private void ValidateRestoredAiMove(string? response, MoveState expected)
    {
        if (!TryParseMove(response, out var x, out var y) || x != expected.Col || y != expected.Row)
        {
            throw new InvalidOperationException("Nước đi đã lưu không khớp với phản hồi từ AI chuyên nghiệp.");
        }
    }

    public void ResumePendingAiTurn()
    {
        if (!_pendingResumeAfterLoad)
        {
            return;
        }

        _pendingResumeAfterLoad = false;

        if (IsPaused)
        {
            return;
        }

        TriggerAiTurnIfNeeded(_lastMoveCell, _lastMovePlayer);
    }
}
