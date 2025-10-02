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
                if (aiMove.Status == EngineClient.EngineMoveStatus.Move)
                {
                    PlaceAiIfValid(aiMove);
                }
                else
                {
                    HandleProfessionalEngineFailure(aiMove);
                }
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

    private void HandleProfessionalViolation(EngineClient.EngineMoveResult result)
    {
        var snapshot = _pendingProfessionalValidation;
        _pendingProfessionalValidation = null;

        Application.Current.Dispatcher?.Invoke(() =>
        {
            RestoreSnapshot(snapshot);

            string displayMessage = !string.IsNullOrWhiteSpace(result.Message)
                ? result.Message
                : (result.Status == EngineClient.EngineMoveStatus.Forbidden
                    ? "Nước đi này bị cấm theo luật chuẩn quốc tế."
                    : "Nước đi không hợp lệ theo luật chuẩn quốc tế.");

            MessageBox.Show(displayMessage, "Chuyên nghiệp", MessageBoxButton.OK, MessageBoxImage.Warning);

            if (Application.Current.MainWindow?.DataContext is MainViewModel vm)
            {
                vm.SetStatus("Nước đi bị từ chối, hãy chọn nước khác.");
            }
        });
    }

    private void HandleProfessionalEngineFailure(EngineClient.EngineMoveResult result)
    {
        var snapshot = _pendingProfessionalValidation;
        _pendingProfessionalValidation = null;

        string detail = !string.IsNullOrWhiteSpace(result.Message)
            ? result.Message
            : (!string.IsNullOrWhiteSpace(result.RawResponse)
                ? result.RawResponse
                : "Engine không phản hồi.");

        Application.Current.Dispatcher?.Invoke(() => RestoreSnapshot(snapshot));

        NotifyProfessionalModeUnavailable(
            "Engine chuyên nghiệp đã bị vô hiệu hóa do lỗi.\n" + detail);

        Application.Current.Dispatcher?.Invoke(() =>
        {
            if (Application.Current.MainWindow?.DataContext is MainViewModel vm)
            {
                vm.SetStatus("AI chuyên nghiệp đã tắt do lỗi.");
            }
        });
    }

    private void RestoreSnapshot(MoveSnapshot? snapshot)
    {
        if (snapshot == null)
        {
            return;
        }

        snapshot.Cell.Value = snapshot.PreviousValue ?? string.Empty;
        snapshot.Cell.IsLastMove = snapshot.PreviousIsLastMove;
        snapshot.Cell.IsWinningCell = false;

        if (snapshot.PreviousLastMoveCell != null)
        {
            snapshot.PreviousLastMoveCell.IsLastMove = true;
        }

        _lastMoveCell = snapshot.PreviousLastMoveCell;
        _lastMovePlayer = snapshot.PreviousLastMovePlayer;
        _lastHumanMoveCell = snapshot.PreviousLastHumanMoveCell;
        CurrentPlayer = snapshot.PreviousCurrentPlayer;

        RebuildCandidatePositions();
    }

    public void DisposeEngine()
    {
        _engine?.Dispose();
        _engine = null;
    }
}
