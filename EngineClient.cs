using System;
using System.Diagnostics;
using System.IO;

namespace Caro_game
{
    public class EngineClient : IDisposable
    {
        public enum EngineMoveStatus
        {
            Move,
            Forbidden,
            Illegal,
            InvalidResponse,
            NoResponse,
            Error
        }

        public sealed class EngineMoveResult
        {
            public EngineMoveResult(EngineMoveStatus status, int? x, int? y, string rawResponse, string? message)
            {
                Status = status;
                X = x;
                Y = y;
                RawResponse = rawResponse;
                Message = message;
            }

            public EngineMoveStatus Status { get; }

            public int? X { get; }

            public int? Y { get; }

            public string RawResponse { get; }

            public string? Message { get; }

            public bool HasCoordinates => X.HasValue && Y.HasValue;

            internal static EngineMoveResult FromResponse(string? responseLine, string? message)
            {
                if (string.IsNullOrWhiteSpace(responseLine))
                {
                    return new EngineMoveResult(EngineMoveStatus.NoResponse, null, null, string.Empty, message);
                }

                var trimmed = responseLine.Trim();

                if (StartsWith(trimmed, "FORBID"))
                {
                    var coords = ParseCoordinates(trimmed, "FORBID");
                    return new EngineMoveResult(EngineMoveStatus.Forbidden, coords?.x, coords?.y, trimmed, message);
                }

                if (StartsWith(trimmed, "ILLEGAL"))
                {
                    var coords = ParseCoordinates(trimmed, "ILLEGAL");
                    return new EngineMoveResult(EngineMoveStatus.Illegal, coords?.x, coords?.y, trimmed, message);
                }

                if (StartsWith(trimmed, "ERROR"))
                {
                    return new EngineMoveResult(EngineMoveStatus.Error, null, null, trimmed, message);
                }

                if (TryParseCoordinates(trimmed, out var parsedX, out var parsedY))
                {
                    return new EngineMoveResult(EngineMoveStatus.Move, parsedX, parsedY, trimmed, message);
                }

                return new EngineMoveResult(EngineMoveStatus.InvalidResponse, null, null, trimmed, message);
            }

            private static bool StartsWith(string value, string prefix)
                => value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);

            private static (int x, int y)? ParseCoordinates(string line, string prefix)
            {
                string remaining = line.Substring(prefix.Length).Trim();
                if (TryParseCoordinates(remaining, out var x, out var y))
                {
                    return (x, y);
                }

                return null;
            }

            private static bool TryParseCoordinates(string value, out int x, out int y)
            {
                x = 0;
                y = 0;

                if (string.IsNullOrWhiteSpace(value))
                {
                    return false;
                }

                var parts = value.Split(',');
                if (parts.Length != 2)
                {
                    return false;
                }

                return int.TryParse(parts[0], out x) && int.TryParse(parts[1], out y);
            }
        }

        private Process? _process;
        private StreamWriter? _input;
        private StreamReader? _output;
        private readonly string _logFile;

        public EngineClient(string enginePath, string? configPath = null)
        {
            _logFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "engine_log.txt");

            var psi = new ProcessStartInfo
            {
                FileName = enginePath,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(enginePath) ?? AppDomain.CurrentDomain.BaseDirectory
            };

            if (!string.IsNullOrWhiteSpace(configPath))
            {
                psi.EnvironmentVariables["RAPFI_CONFIG_PATH"] = configPath;
                Log($"[Init] Using config: {configPath}");
            }

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

        private (string? Response, string? Message) ReceiveNonMessageLine()
        {
            if (_output == null) return (string.Empty, null);

            string? line;
            string? lastMessage = null;
            while ((line = _output.ReadLine()) != null)
            {
                if (line.StartsWith("MESSAGE", StringComparison.OrdinalIgnoreCase))
                {
                    Log("[Engine MESSAGE] " + line);
                    lastMessage = line.Length > 7
                        ? line.Substring(7).Trim()
                        : string.Empty;
                    continue;
                }
                Log("[Recv] " + line);
                return (line, lastMessage);
            }

            if (_process != null && _process.HasExited)
            {
                Log("[Engine] process exited unexpectedly!");
            }

            return (string.Empty, lastMessage);
        }

        public void StartSquare(int size)
        {
            Send($"START {size}");
            var resp = ReceiveNonMessageLine(); // nuốt OK nếu có
            if (!string.IsNullOrEmpty(resp.Response))
                Log("[StartSquare resp] " + resp.Response);
        }

        public bool StartRect(int width, int height)
        {
            Send($"RECTSTART {width},{height}");
            var resp = ReceiveNonMessageLine();
            return !string.IsNullOrEmpty(resp.Response) &&
                   resp.Response.StartsWith("OK", StringComparison.OrdinalIgnoreCase);
        }

        public EngineMoveResult Begin()
        {
            Send("BEGIN");
            var (response, message) = ReceiveNonMessageLine();
            return EngineMoveResult.FromResponse(response, message);
        }

        public EngineMoveResult Turn(int x, int y)
        {
            Send($"TURN {x},{y}");
            var (response, message) = ReceiveNonMessageLine();
            return EngineMoveResult.FromResponse(response, message);
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
