using System;
using System.Diagnostics;
using System.IO;

namespace Caro_game.AI
{
    public class EngineClient : IDisposable
    {
        private Process _process;
        private StreamWriter _input;
        private StreamReader _output;

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

            Send($"INFO timeout_turn 2000");
            Send($"INFO timeout_match 60000");
        }

        private void Send(string cmd)
        {
            _input.WriteLine(cmd);
            _input.Flush();
        }

        private string Receive()
        {
            string line;
            while ((line = _output.ReadLine()) != null)
            {
                if (line.StartsWith("MESSAGE"))
                {
                    Console.WriteLine(line);
                    continue;
                }
                return line;
            }
            return string.Empty;
        }

        public void StartBoard(int size) => Send($"START {size}");
        public string Begin() { Send("BEGIN"); return Receive(); }
        public string Turn(int x, int y) { Send($"TURN {x},{y}"); return Receive(); }
        public void End() { Send("END"); if (!_process.HasExited) _process.Kill(); }

        public void Dispose() { End(); _process?.Dispose(); }
    }
}
