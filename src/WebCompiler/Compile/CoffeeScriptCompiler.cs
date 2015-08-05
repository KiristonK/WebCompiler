﻿using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace WebCompiler
{
    internal class CoffeeScriptCompiler : ICompiler
    {
        private static Regex _errorRx = new Regex(":(?<line>[0-9]+):(?<column>[0-9]+).*error: (?<message>.+)", RegexOptions.Compiled);
        private string _path;
        private string _output = string.Empty;
        private string _error = string.Empty;
        private string _temp = Path.Combine(Path.GetTempPath(), ".coffee-script");

        public CoffeeScriptCompiler(string path)
        {
            _path = path;
        }

        public CompilerResult Compile(Config config)
        {
            string baseFolder = Path.GetDirectoryName(config.FileName);
            string inputFile = Path.Combine(baseFolder, config.InputFile);

            FileInfo info = new FileInfo(inputFile);
            string content = File.ReadAllText(info.FullName);

            CompilerResult result = new CompilerResult
            {
                FileName = info.FullName,
                OriginalContent = content,
            };

            try
            {
                RunCompilerProcess(config, info);

                string tempFile = Path.ChangeExtension(Path.Combine(_temp, info.Name), ".js");

                if (File.Exists(tempFile))
                {
                    result.CompiledContent = File.ReadAllText(tempFile);

                    if (config.SourceMap)
                    {
                        string mapFile = tempFile + ".map";
                        if (File.Exists(mapFile))
                            result.SourceMap = File.ReadAllText(mapFile);
                    }
                }

                if (_error.Length > 0)
                {
                    CompilerError ce = new CompilerError
                    {
                        FileName = info.FullName,
                        Message = _error.Replace(baseFolder, string.Empty),
                    };

                    var match = _errorRx.Match(_error);

                    if (match.Success)
                    {
                        ce.Message = match.Groups["message"].Value.Replace(baseFolder, string.Empty);
                        ce.LineNumber = int.Parse(match.Groups["line"].Value);
                        ce.ColumnNumber = int.Parse(match.Groups["column"].Value);
                    }

                    result.Errors.Add(ce);
                }
            }
            catch (Exception ex)
            {
                CompilerError error = new CompilerError
                {
                    FileName = info.FullName,
                    Message = string.IsNullOrEmpty(_error) ? ex.Message : _error,
                    LineNumber = 0,
                    ColumnNumber = 0,
                };

                result.Errors.Add(error);
            }

            return result;
        }

        private void RunCompilerProcess(Config config, FileInfo info)
        {
            string arguments = ConstructArguments(config);

            ProcessStartInfo start = new ProcessStartInfo
            {
                WorkingDirectory = info.Directory.FullName,
                UseShellExecute = false,
                WindowStyle = ProcessWindowStyle.Hidden,
                CreateNoWindow = true,
                FileName = "cmd.exe",
                Arguments = $"/c \"\"{Path.Combine(_path, "node_modules\\.bin\\coffee.cmd")}\" {arguments} \"{info.FullName}\"\"",
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            start.EnvironmentVariables["PATH"] = _path + ";" + start.EnvironmentVariables["PATH"];

            Process p = Process.Start(start);
            var stdout = p.StandardOutput.ReadToEndAsync();
            var stderr = p.StandardError.ReadToEndAsync();
            p.WaitForExit();

            _output = stdout.Result;
            _error = stderr.Result;
        }

        private string ConstructArguments(Config config)
        {
            string arguments = $" --compile --output \"{_temp}\"";

            if (config.SourceMap)
                arguments += " --map";

            CoffeeScriptOptions options = new CoffeeScriptOptions(config);

            if (options.Bare)
                arguments += " --bare";

            //if (options.Globals)
            //    arguments += " --globals";

            return arguments;
        }
    }
}
