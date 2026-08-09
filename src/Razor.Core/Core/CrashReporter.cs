#region license
// UOSagas Razor: An Ultima Online Assistant for the UOSagas shard
// Copyright (C) 2026 UOSagas (3HMonkey)
//
// Based on Razor: An Ultima Online Assistant
// Copyright (c) 2022 Razor Development Community on GitHub <https://github.com/markdwags/Razor>
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.
#endregion

// UOSagas-Razor: Crash-Reporter (Razor-Zusatz).
//
// Faengt kritische Fehler ein (UI-Thread, Game-Thread-Callbacks, AppDomain,
// unbeobachtete Tasks), schreibt ein Crashlog nach Data/CrashLogs und zeigt
// (ueber den von Razor.Avalonia registrierten UiPresenter) ein Fenster mit
// kopierbarem Log. "Report & Close" schickt den Report als Embed-Karte an
// einen Discord-Webhook (Account/Charakter/Discord-Name/Shard/Version +
// vollstaendiges Log als Datei-Anhang).
//
// Razor.Core kennt kein Avalonia — die UI haengt sich ueber UiPresenter ein.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Assistant
{
    /// <summary>Ein erfasster Fehler samt Kontext fuer Anzeige + Discord-Report.</summary>
    public sealed class CrashReport
    {
        public DateTime TimeUtc;
        public string Source;
        public bool Fatal;
        public Exception Exception;

        /// <summary>Fehlertext, wenn keine Exception vorliegt (Script-Fehler).</summary>
        public string ErrorText;

        /// <summary>"Lua"/"VScript"/"Razor Script" bei Script-Fehlern, sonst null.</summary>
        public string ScriptEngine;
        public string ScriptName;
        /// <summary>Scriptinhalt fuer den optionalen Anhang (Checkbox im Dialog).</summary>
        public string ScriptContent;

        public bool IsScriptError => ScriptEngine != null;

        /// <summary>Exception-Text bzw. Fehlertext — der Kern des Reports.</summary>
        public string Details => Exception?.ToString() ?? ErrorText ?? "(no details)";

        public string Account;
        public string Character;
        public string Shard;
        public string Version;
        public string Os;

        /// <summary>Pfad des geschriebenen Crashlogs (null, wenn Schreiben fehlschlug).</summary>
        public string LogFile;

        /// <summary>Wird vom Crash-Fenster beim Schliessen gesetzt (fuer fatale Fehler:
        /// der sterbende Thread wartet darauf, damit das Fenster sichtbar bleibt).</summary>
        public readonly ManualResetEventSlim Closed = new ManualResetEventSlim(false);

        /// <summary>Vollstaendiger Report-Text (Anzeige, Logdatei, Discord-Anhang).</summary>
        public string BuildText()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("UOSagas Razor - Crash Report");
            sb.AppendLine("============================");
            sb.AppendLine($"Time (UTC): {TimeUtc:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Source:     {Source}");
            sb.AppendLine($"Fatal:      {(Fatal ? "yes" : "no")}");
            sb.AppendLine($"Razor:      {Version}");
            sb.AppendLine($"OS:         {Os}");
            sb.AppendLine($"Shard:      {OrDash(Shard)}");
            sb.AppendLine($"Account:    {OrDash(Account)}");
            sb.AppendLine($"Character:  {OrDash(Character)}");

            if (IsScriptError)
                sb.AppendLine($"Script:     {OrDash(ScriptName)} ({ScriptEngine})");

            sb.AppendLine();
            sb.AppendLine(Details);
            return sb.ToString();
        }

        private static string OrDash(string s) => string.IsNullOrEmpty(s) ? "-" : s;
    }

    /// <summary>Persistierte Reporter-Einstellungen (Data/crash-reporter.json).</summary>
    public sealed class CrashReporterSettings
    {
        public string DiscordName { get; set; } = "";

        /// <summary>Optionaler Override des einkompilierten Webhooks.</summary>
        public string WebhookUrl { get; set; } = "";
    }

    public static class CrashReporter
    {
        /// <summary>Gleiche Fehlersignatur innerhalb dieses Fensters → kein neues
        /// Fenster/Logfile (verhindert Dialog-Stuerme aus OnTick/OnPacket).</summary>
        public static TimeSpan DedupeWindow = TimeSpan.FromSeconds(60);

        /// <summary>Von Razor.Avalonia gesetzt: zeigt das Crash-Fenster.
        /// Rueckgabe true = Fenster wird angezeigt (Closed wird spaeter gesetzt).</summary>
        public static Func<CrashReport, bool> UiPresenter;

        /// <summary>Nur fuer Tests: Datenwurzel erzwingen (statt Config/BaseDirectory).</summary>
        public static string DataRootOverride;

        private static readonly object _lock = new object();
        private static readonly Dictionary<string, DateTime> _lastReported = new Dictionary<string, DateTime>();
        private static readonly HttpClient _http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };

        private static CrashReporterSettings _settings;
        private static bool _initialized;

        /// <summary>Prozessweite Auffangnetze registrieren (idempotent).</summary>
        public static void Initialize()
        {
            if (_initialized)
                return;

            _initialized = true;

            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            {
                Report(e.ExceptionObject as Exception, "AppDomain (unhandled)", fatal: e.IsTerminating);
            };

            TaskScheduler.UnobservedTaskException += (_, e) =>
            {
                e.SetObserved();
                Report(e.Exception?.GetBaseException(), "Task (unobserved)");
            };
        }

        /// <summary>Fehler erfassen: Logfile schreiben, Konsole, Crash-Fenster zeigen.
        /// Bei fatalen Fehlern blockiert der Aufrufer-Thread, bis das Fenster
        /// geschlossen ist (haelt den sterbenden Prozess fuer den Dialog am Leben).</summary>
        public static void Report(Exception ex, string source, bool fatal = false)
        {
            if (ex == null)
                return;

            CrashReport report;

            lock (_lock)
            {
                string signature = Signature(ex, source);
                DateTime now = DateTime.UtcNow;

                if (_lastReported.TryGetValue(signature, out DateTime last) && now - last < DedupeWindow)
                    return;

                _lastReported[signature] = now;
                report = Capture(ex, source, fatal);
            }

            Console.WriteLine($"[UOSagas Razor] {(fatal ? "FATALER Fehler" : "Fehler")} ({source}): {ex}");
            if (report.LogFile != null)
                Console.WriteLine($"[UOSagas Razor] Crashlog: {report.LogFile}");

            bool shown = false;
            try
            {
                shown = UiPresenter?.Invoke(report) ?? false;
            }
            catch (Exception uiEx)
            {
                Console.WriteLine($"[UOSagas Razor] Crash-Fenster konnte nicht angezeigt werden: {uiEx}");
            }

            if (fatal && shown)
            {
                // Der Prozess stirbt, sobald dieser Handler zurueckkehrt — warten,
                // bis der User den Dialog schliesst (Report & Close / Close).
                try
                {
                    report.Closed.Wait(TimeSpan.FromMinutes(10));
                }
                catch
                {
                }
            }
        }

        /// <summary>Script-Fehler (Lua/VScript/Razor-Script) still protokollieren:
        /// Logfile in CrashLogs mit Dedupe, aber KEIN Report-Fenster — der Dialog
        /// bleibt echten Crashes vorbehalten. Razor laeuft normal weiter.</summary>
        public static void ReportScriptError(string engine, string scriptName, string scriptContent, string errorText)
        {
            if (string.IsNullOrWhiteSpace(errorText))
                return;

            lock (_lock)
            {
                string signature = $"script|{engine}|{scriptName}|{errorText}";
                DateTime now = DateTime.UtcNow;

                if (_lastReported.TryGetValue(signature, out DateTime last) && now - last < DedupeWindow)
                    return;

                _lastReported[signature] = now;

                CrashReport report = NewReport($"{engine} script", fatal: false);
                report.ErrorText = errorText;
                report.ScriptEngine = engine;
                report.ScriptName = string.IsNullOrWhiteSpace(scriptName) ? "(unnamed)" : scriptName;
                report.ScriptContent = scriptContent;
                WriteLogFile(report);
            }
        }

        /// <summary>Report bauen + Logfile schreiben, OHNE das Fenster zu zeigen
        /// (fuer den PushFrame-Fallback des UI-Threads).</summary>
        public static CrashReport Capture(Exception ex, string source, bool fatal)
        {
            CrashReport report = NewReport(source, fatal);
            report.Exception = ex;
            WriteLogFile(report);
            return report;
        }

        private static CrashReport NewReport(string source, bool fatal)
        {
            return new CrashReport
            {
                TimeUtc = DateTime.UtcNow,
                Source = source ?? "(unknown)",
                Fatal = fatal,
                Account = World.AccountName,
                Character = World.Player?.Name,
                Shard = World.ShardName,
                Version = RazorVersion,
                Os = Environment.OSVersion.ToString()
            };
        }

        private static void WriteLogFile(CrashReport report)
        {
            try
            {
                string dir = Path.Combine(DataRoot(), "CrashLogs");
                Directory.CreateDirectory(dir);

                string file = Path.Combine(dir,
                    $"crash-{report.TimeUtc:yyyyMMdd-HHmmss-fff}.txt");
                File.WriteAllText(file, report.BuildText(), Encoding.UTF8);
                report.LogFile = file;
            }
            catch (Exception ioEx)
            {
                Console.WriteLine($"[UOSagas Razor] Crashlog konnte nicht geschrieben werden: {ioEx.Message}");
            }
        }

        // ----- Versand: primaer ueber den Client (ABI SubmitCrashReport), die
        // Webhook-URL lebt NUR im NativeAOT-Client. Fallback fuer Dev-Setups:
        // direkter Webhook aus Data/crash-reporter.json.

        /// <summary>Report abschicken. Rueckgabe null = Erfolg, sonst Fehlertext
        /// fuer die Statuszeile des Dialogs.</summary>
        public static async Task<string> SendAsync(CrashReport report, string discordName, string comment,
            bool includeScript)
        {
            string envelope = BuildEnvelope(report, discordName, comment, includeScript);

            // 1) Ueber den Client (blockierend, daher Task.Run — der Dialog bleibt fluessig).
            if (ClientProxy.SupportsCrashReport)
            {
                bool delivered = await Task.Run(() => ClientProxy.SubmitCrashReport(envelope)).ConfigureAwait(false);

                if (delivered)
                    return null;

                // Rate-Limit oder Netzwerkfehler im Client — Details stehen im Client-Log.
                return "The client rejected the report (rate limit) or delivery failed.";
            }

            // 2) Dev-Fallback: direkter Webhook, wenn lokal konfiguriert.
            string url = Settings.WebhookUrl;

            if (string.IsNullOrWhiteSpace(url))
                return "Reporting is not available (client too old, no webhook configured).";

            try
            {
                using MultipartFormDataContent form = new MultipartFormDataContent();

                StringContent payload = new StringContent(BuildWebhookPayload(report, discordName, comment),
                    Encoding.UTF8, "application/json");
                form.Add(payload, "payload_json");

                int index = 0;

                foreach ((string name, byte[] content) in BuildAttachments(report, includeScript))
                {
                    form.Add(new ByteArrayContent(content), $"files[{index}]", name);
                    index++;
                }

                using HttpResponseMessage response = await _http.PostAsync(url, form).ConfigureAwait(false);

                if (response.IsSuccessStatusCode)
                    return null;

                return $"Discord returned {(int) response.StatusCode} {response.ReasonPhrase}.";
            }
            catch (Exception ex)
            {
                return $"Sending failed: {ex.Message}";
            }
        }

        /// <summary>Envelope fuer den Client-Relay (oeffentlich fuer Tests):
        /// {"payloadJson":..., "attachments":[{"name":..., "contentBase64":...}]}.</summary>
        public static string BuildEnvelope(CrashReport report, string discordName, string comment, bool includeScript)
        {
            var attachments = new List<object>();

            foreach ((string name, byte[] content) in BuildAttachments(report, includeScript))
                attachments.Add(new { name, contentBase64 = Convert.ToBase64String(content) });

            return JsonSerializer.Serialize(new
            {
                payloadJson = BuildWebhookPayload(report, discordName, comment),
                attachments
            });
        }

        private static List<(string Name, byte[] Content)> BuildAttachments(CrashReport report, bool includeScript)
        {
            var result = new List<(string, byte[])>
            {
                ($"crash-{report.TimeUtc:yyyyMMdd-HHmmss}.txt", Encoding.UTF8.GetBytes(report.BuildText()))
            };

            if (includeScript && !string.IsNullOrEmpty(report.ScriptContent))
            {
                string ext = report.ScriptEngine switch
                {
                    "Lua" => ".lua",
                    "VScript" => ".vscript",
                    _ => ".razor"
                };

                string baseName = string.IsNullOrWhiteSpace(report.ScriptName) ? "script" : report.ScriptName;
                result.Add((SanitizeFileName(baseName) + ext, Encoding.UTF8.GetBytes(report.ScriptContent)));
            }

            return result;
        }

        /// <summary>Embed-Karte fuer den Webhook (oeffentlich fuer Tests).</summary>
        public static string BuildWebhookPayload(CrashReport report, string discordName, string comment)
        {
            string description = "```\n" + Truncate(report.Details, 3800) + "\n```";

            var fields = new List<object>
            {
                new { name = "Account", value = FieldValue(report.Account), inline = true },
                new { name = "Character", value = FieldValue(report.Character), inline = true },
                new { name = "Discord", value = FieldValue(discordName), inline = true },
                new { name = "Shard", value = FieldValue(report.Shard), inline = true },
                new { name = "Razor", value = FieldValue(report.Version), inline = true },
                new { name = "Source", value = FieldValue(report.Source), inline = true }
            };

            if (report.IsScriptError)
                fields.Add(new { name = "Script", value = FieldValue($"{report.ScriptName} ({report.ScriptEngine})"), inline = true });

            if (!string.IsNullOrWhiteSpace(comment))
                fields.Add(new { name = "Comment", value = Truncate(comment.Trim(), 1000), inline = false });

            string title = report.IsScriptError
                ? $"Razor Script Error ({report.ScriptEngine})"
                : report.Fatal
                    ? "Razor Crash (fatal)"
                    : "Razor Crash Report";

            object payload = new
            {
                username = "Razor Crash Reporter",
                embeds = new object[]
                {
                    new
                    {
                        title,
                        color = report.IsScriptError ? 0xD4AF37 : 0xB00020,
                        description,
                        fields,
                        footer = new { text = "UOSagas Razor Crash Reporter" },
                        timestamp = report.TimeUtc.ToString("o")
                    }
                }
            };

            return JsonSerializer.Serialize(payload);
        }

        private static string SanitizeFileName(string name)
        {
            StringBuilder sb = new StringBuilder(Math.Min(name.Length, 48));

            foreach (char c in name)
            {
                if (sb.Length >= 48)
                    break;

                sb.Append(char.IsLetterOrDigit(c) || c == '.' || c == '-' || c == '_' ? c : '_');
            }

            return sb.Length > 0 ? sb.ToString() : "script";
        }

        // ----- Einstellungen (Discord-Name merken, Webhook-Override) -----

        public static CrashReporterSettings Settings
        {
            get
            {
                lock (_lock)
                {
                    if (_settings == null)
                        _settings = LoadSettings();

                    return _settings;
                }
            }
        }

        public static void SaveSettings()
        {
            lock (_lock)
            {
                if (_settings == null)
                    return;

                try
                {
                    File.WriteAllText(SettingsPath(),
                        JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true }),
                        Encoding.UTF8);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[UOSagas Razor] crash-reporter.json konnte nicht gespeichert werden: {ex.Message}");
                }
            }
        }

        /// <summary>Nur fuer Tests: Settings-Cache verwerfen.</summary>
        public static void ResetSettingsCache()
        {
            lock (_lock)
                _settings = null;
        }

        private static CrashReporterSettings LoadSettings()
        {
            try
            {
                string path = SettingsPath();

                if (File.Exists(path))
                {
                    CrashReporterSettings loaded =
                        JsonSerializer.Deserialize<CrashReporterSettings>(File.ReadAllText(path));

                    if (loaded != null)
                        return loaded;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[UOSagas Razor] crash-reporter.json konnte nicht gelesen werden: {ex.Message}");
            }

            return new CrashReporterSettings();
        }

        private static string SettingsPath() => Path.Combine(DataRoot(), "crash-reporter.json");

        // ----- Helfer -----

        private static string DataRoot()
        {
            if (!string.IsNullOrEmpty(DataRootOverride))
                return DataRootOverride;

            try
            {
                string dir = Config.GetInstallDirectory();
                if (!string.IsNullOrEmpty(dir))
                    return dir;
            }
            catch
            {
            }

            string fallback = Path.Combine(AppContext.BaseDirectory, "Data");
            Directory.CreateDirectory(fallback);
            return fallback;
        }

        private static string RazorVersion
        {
            get
            {
                try
                {
                    return Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "?";
                }
                catch
                {
                    return "?";
                }
            }
        }

        private static string Signature(Exception ex, string source)
        {
            string frame = "";

            string stack = ex.StackTrace;
            if (!string.IsNullOrEmpty(stack))
            {
                int nl = stack.IndexOf('\n');
                frame = nl > 0 ? stack.Substring(0, nl) : stack;
            }

            return $"{source}|{ex.GetType().FullName}|{frame}";
        }

        private static string FieldValue(string s)
        {
            if (string.IsNullOrWhiteSpace(s))
                return "-";

            return Truncate(s.Trim(), 256);
        }

        private static string Truncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s) || s.Length <= max)
                return s;

            return s.Substring(0, max) + "\n… (truncated)";
        }
    }
}
