using System;
using System.Diagnostics;
using System.IO;

namespace Caro_game
{
    public class EngineClient : IDisposable
    {
        private Process? _process;
        private StreamWriter? _input;
        private StreamReader? _output;

        public EngineClient(string enginePath)
        {
            var psi = new ProcessStartInfo
            {
                FileName = enginePath,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            _process = new Process { StartInfo = psi };
            _process.Start();

            _input = _process.StandardInput;
            _output = _process.StandardOutput;

            // cấu hình thời gian
            Send("INFO timeout_turn 1000");   // 1 giây mỗi nước
            Send("INFO timeout_match 60000"); // 60 giây mỗi ván
        }

        private void Send(string cmd)
        {
            try
            {
                if (_input == null) return;
                Console.WriteLine("[Send] " + cmd);
                _input.WriteLine(cmd);
                _input.Flush();
            }
            catch (Exception ex)
            {
                Console.WriteLine("[EngineClient] Send error: " + ex.Message);
            }
        }

        private string Receive()
        {
            try
            {
                if (_output == null) return string.Empty;

                string? line;
                while ((line = _output.ReadLine()) != null)
                {
                    if (line.StartsWith("MESSAGE"))
                    {
                        Console.WriteLine("[Engine] " + line);
                        continue;
                    }
                    Console.WriteLine("[Recv] " + line);
                    return line;
                }

                if (_process != null && _process.HasExited)
                {
                    Console.WriteLine("[Engine] process exited unexpectedly!");
                    return string.Empty;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[EngineClient] Receive error: " + ex.Message);
            }
            return string.Empty;
        }

        /// <summary>
        /// Khởi tạo bàn cờ (phải gọi khi bắt đầu ván)
        /// </summary>
        public void StartBoard(int size) => Send($"START {size}");

        /// <summary>
        /// Nếu AI đi trước (O trước), gọi BEGIN để engine trả về nước đi đầu tiên
        /// </summary>
        public string Begin()
        {
            Send("BEGIN");
            return Receive();
        }

        /// <summary>
        /// Báo cho engine biết nước đi mới của đối thủ (X hoặc O)
        /// và nhận về nước đi tiếp theo của AI
        /// </summary>
        public string Turn(int x, int y)
        {
            Send($"TURN {x},{y}");
            return Receive();
        }

        public void End()
        {
            try { Send("END"); } catch { }

            try
            {
                if (_process != null && !_process.HasExited)
                    _process.Kill(true);
            }
            catch { }
        }

        public void Dispose()
        {
            End();
            _input?.Dispose();
            _output?.Dispose();
            _process?.Dispose();

            _input = null;
            _output = null;
            _process = null;
        }
        public string SendBoard(IEnumerable<(int x, int y, int player)> moves)
        {
            try
            {
                Send("BOARD");
                foreach (var m in moves)
                {
                    Send($"{m.x},{m.y},{m.player}");
                }
                Send("DONE");
                return Receive();
            }
            catch (Exception ex)
            {
                Console.WriteLine("[EngineClient] SendBoard error: " + ex.Message);
                return string.Empty;
            }
        }
    }
}
