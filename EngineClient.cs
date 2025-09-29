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

            Send("INFO timeout_turn 1000");
            Send("INFO timeout_match 60000");
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

        private string ReceiveLine()
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
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[EngineClient] Receive error: " + ex.Message);
            }
            return string.Empty;
        }

        private bool AwaitOk()
        {
            var resp = ReceiveLine();
            return !string.IsNullOrEmpty(resp) && resp.StartsWith("OK", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary> START N (bàn vuông N x N) và đọc OK </summary>
        public bool StartSquare(int size)
        {
            Send($"START {size}");
            return AwaitOk();
        }

        /// <summary> RECTSTART W,H (bàn chữ nhật). W=x=Columns, H=y=Rows. Đọc OK. </summary>
        public bool StartRect(int width, int height)
        {
            // theo protocol là "RECTSTART W,H" (có dấu phẩy)
            Send($"RECTSTART {width},{height}");
            return AwaitOk();
        }

        public string Begin()
        {
            Send("BEGIN");
            return ReceiveLine(); // trả về "x,y"
        }

        public string Turn(int x, int y)
        {
            Send($"TURN {x},{y}");
            return ReceiveLine(); // trả về "x,y"
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
            _input = null; _output = null; _process = null;
        }
    }
}
