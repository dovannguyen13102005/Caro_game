using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;

namespace Caro_game
{
    public class EngineClient : IDisposable
    {
        private Process? _process;
        private StreamWriter? _input;
        private StreamReader? _output;
        private readonly string _logFile;

        public EngineClient(string enginePath)
        {
            _logFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "engine_log.txt");

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

            Log($"[Init] Engine started: {enginePath}");

            // gửi timeout mặc định
            Send("INFO timeout_turn 1000");
            Send("INFO timeout_match 60000");
        }

        private void Log(string msg)
        {
            try
            {
                File.AppendAllText(_logFile, $"{DateTime.Now:HH:mm:ss} {msg}\n");
            }
            catch
            {
                // nếu không ghi được file thì bỏ qua
            }
        }

        private void Send(string cmd)
        {
            if (_input == null) return;
            Log("[Send] " + cmd);
            _input.WriteLine(cmd);
            _input.Flush();
        }

        private string ReceiveLine()
        {
            if (_output == null) return string.Empty;

            string? line;
            while ((line = _output.ReadLine()) != null)
            {
                if (line.StartsWith("MESSAGE"))
                {
                    Log("[Engine MESSAGE] " + line);
                    continue;
                }
                Log("[Recv] " + line);
                return line;
            }

            if (_process != null && _process.HasExited)
            {
                Log("[Engine] process exited unexpectedly!");
            }

            return string.Empty;
        }
        public void SendRaw(string cmd)
        {
            if (_input == null) return;
            Log("[SendRaw] " + cmd);
            _input.WriteLine(cmd);
            _input.Flush();
        }

        public void StartSquare(int size)
        {
            Send($"START {size}");
            var resp = ReceiveLine(); // nuốt OK nếu có
            if (!string.IsNullOrEmpty(resp))
                Log("[StartSquare resp] " + resp);
        }

        public bool StartRect(int width, int height)
        {
            Send($"RECTSTART {width},{height}");
            var resp = ReceiveLine();
            return !string.IsNullOrEmpty(resp) &&
                   resp.StartsWith("OK", StringComparison.OrdinalIgnoreCase);
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
            _input = null;
            _output = null;
            _process = null;
        }
        

    }
}
