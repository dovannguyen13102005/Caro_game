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

        // 🔹 Đường dẫn tới exe
        var enginePath = Path.Combine(projectRoot, "AI", "pbrain-rapfi_avx2.exe");

        if (string.IsNullOrWhiteSpace(enginePath) || !File.Exists(enginePath))
        {
            NotifyProfessionalModeUnavailable("Không tìm thấy tệp AI cần thiết cho cấp độ Chuyên nghiệp.\n" +
                                              $"Đường dẫn: {enginePath}");
            return;
        }

        try
        {
            // 🔹 Copy đúng config luật thành config.toml
            if (_rule != null)
            {
                var configFileName = _rule.GetConfigFileName(_aiSymbol == "X");
                if (!string.IsNullOrWhiteSpace(configFileName))
                {
                    var configSource = Path.Combine(projectRoot, "AI", configFileName);
                    var configDest = Path.Combine(projectRoot, "AI", "config.toml");

                    if (File.Exists(configSource))
                    {
                        File.Copy(configSource, configDest, true); // copy đè
                    }
                    else
                    {
                        NotifyProfessionalModeUnavailable(
                            $"Không tìm thấy tệp cấu hình cho luật {_rule.Name}.\nĐường dẫn: {configSource}");
                        DisposeEngine();
                        return;
                    }
                }
            }

            // 🔹 Khởi tạo engine
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

            // 🔹 Gửi rule keyword nếu có (freestyle/renju/standard)
            if (_rule != null && !string.IsNullOrWhiteSpace(_rule.EngineRuleKeyword))
            {
                _engine.SendInfo("rule", _rule.EngineRuleKeyword!);
            }

            // ❌ KHÔNG gửi END ở đây, chỉ gửi END khi quit game!

            // ✅ Nếu bàn trống và AI đi trước → gọi BEGIN
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
}
