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

// UOSagas-Razor: Crash-/Error-Report-Fenster (Razor-Zusatz).
//
// Zwei Modi:
//  * Crash (Razor-Fehler):  kopierbares Log + Kommentarfeld.
//  * Script-Fehler:         kompakt — Fehlertext, Kommentarbox und die
//    Checkbox "Include script" (default an, haengt das Script an den Report).
// Erster Button = "Report & Close": schickt den Report an den CLIENT
// (SubmitCrashReport-ABI, die Discord-Webhook-URL lebt nur dort) und
// schliesst. Der Discord-Name wird in Data/crash-reporter.json gemerkt.
//
// Dunkle Optik wie die Script-IDE; alle Farben lokal gesetzt, damit die
// globalen (hellen) RazorApp-Styles nicht dazwischenfunken.

using System;
using Assistant;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Razor.UI
{
    public sealed class CrashReportWindow : Window
    {
        private static CrashReportWindow _instance;

        private readonly CrashReport _report;
        private readonly TextBox _discordName;
        private readonly TextBox _comment;
        private readonly CheckBox _includeScript;
        private readonly TextBlock _status;
        private readonly Button _reportButton;
        private readonly Button _copyButton;
        private readonly Button _closeButton;

        private static readonly SolidColorBrush BgBrush = new SolidColorBrush(Color.Parse("#1E1E1E"));
        private static readonly SolidColorBrush PanelBrush = new SolidColorBrush(Color.Parse("#252526"));
        private static readonly SolidColorBrush FgBrush = new SolidColorBrush(Color.Parse("#DCDCDC"));
        private static readonly SolidColorBrush DimBrush = new SolidColorBrush(Color.Parse("#9A9A9A"));
        private static readonly SolidColorBrush BorderBrushDark = new SolidColorBrush(Color.Parse("#3C3C3C"));
        private static readonly SolidColorBrush AccentBrush = new SolidColorBrush(Color.Parse("#B03038"));
        private static readonly SolidColorBrush GoldBrush = new SolidColorBrush(Color.Parse("#B8973A"));
        private static readonly SolidColorBrush ErrorBrush = new SolidColorBrush(Color.Parse("#F48771"));
        private static readonly SolidColorBrush OkBrush = new SolidColorBrush(Color.Parse("#89D185"));

        /// <summary>Fenster fuer einen Report anzeigen (UI-Thread; eine Instanz).</summary>
        public static void Present(CrashReport report)
        {
            if (_instance != null)
            {
                // Ein Report-Fenster reicht — das offene nach vorn holen.
                _instance.Activate();
                report.Closed.Set();
                return;
            }

            _instance = new CrashReportWindow(report);
            _instance.Show();
            _instance.Activate();
        }

        public CrashReportWindow(CrashReport report)
        {
            _report = report;
            bool scriptMode = report.IsScriptError;

            Title = scriptMode ? "UOSagas Razor - Script Error" : "UOSagas Razor - Crash Report";
            Width = scriptMode ? 560 : 780;
            Height = scriptMode ? 460 : 620;
            MinWidth = 480;
            MinHeight = 360;
            CanResize = true;
            Topmost = true;
            Background = BgBrush;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;

            Closed += (_, _) =>
            {
                _instance = null;
                PersistDiscordName();
                _report.Closed.Set();
            };

            // ----- Kopf -----

            TextBlock headline = new TextBlock
            {
                Text = scriptMode
                    ? $"A script error occurred in '{report.ScriptName}' ({report.ScriptEngine})."
                    : report.Fatal
                        ? "Razor has hit a critical error and cannot continue."
                        : "Razor has hit an unexpected error.",
                Foreground = FgBrush,
                FontSize = 14,
                FontWeight = FontWeight.Bold,
                TextWrapping = TextWrapping.Wrap
            };

            TextBlock subline = new TextBlock
            {
                Text = scriptMode
                    ? "You can report this to the UOSagas team. If the script runs fine in the integrated assistant, this is likely a Razor bug."
                    : report.LogFile != null
                        ? $"The report below was saved to:  {report.LogFile}\nPlease send it to the UOSagas team so the bug can be fixed."
                        : "Please send the report below to the UOSagas team so the bug can be fixed.",
                Foreground = DimBrush,
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 6, 0, 0)
            };

            StackPanel header = new StackPanel
            {
                Margin = new Thickness(12, 10, 12, 8),
                Children = { headline, subline }
            };

            Border headerBorder = new Border
            {
                Background = PanelBrush,
                BorderBrush = scriptMode ? GoldBrush : AccentBrush,
                BorderThickness = new Thickness(0, 0, 0, 2),
                Child = header
            };

            // ----- Fehlertext / Log (kopierbar) -----

            TextBox log = new TextBox
            {
                Text = scriptMode ? report.Details : report.BuildText(),
                IsReadOnly = true,
                AcceptsReturn = true,
                TextWrapping = scriptMode ? TextWrapping.Wrap : TextWrapping.NoWrap,
                FontFamily = new FontFamily("Cascadia Mono,Consolas,Menlo,monospace"),
                FontSize = 12,
                Background = BgBrush,
                Foreground = FgBrush,
                BorderBrush = BorderBrushDark,
                BorderThickness = new Thickness(1),
                Margin = new Thickness(12, 10, 12, 0)
            };

            if (scriptMode)
                log.MaxHeight = 140;

            ScrollViewer.SetHorizontalScrollBarVisibility(log, Avalonia.Controls.Primitives.ScrollBarVisibility.Auto);
            ScrollViewer.SetVerticalScrollBarVisibility(log, Avalonia.Controls.Primitives.ScrollBarVisibility.Auto);

            // ----- Kommentar (+ Script-Checkbox im Script-Modus) -----

            _comment = new TextBox
            {
                Watermark = "Comment: what were you doing when it happened? (optional)",
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                Height = scriptMode ? 90 : 64,
                Background = PanelBrush,
                Foreground = FgBrush,
                BorderBrush = BorderBrushDark,
                Margin = new Thickness(12, 10, 12, 0)
            };
            ScrollViewer.SetVerticalScrollBarVisibility(_comment, Avalonia.Controls.Primitives.ScrollBarVisibility.Auto);

            StackPanel middle = new StackPanel();
            middle.Children.Add(_comment);

            if (scriptMode)
            {
                _includeScript = new CheckBox
                {
                    Content = "Include the script in the report (recommended)",
                    IsChecked = true,
                    Foreground = FgBrush,
                    FontSize = 12,
                    Margin = new Thickness(12, 8, 12, 0)
                };

                if (string.IsNullOrEmpty(report.ScriptContent))
                {
                    _includeScript.IsChecked = false;
                    _includeScript.IsEnabled = false;
                    _includeScript.Content = "Script content not available";
                }

                middle.Children.Add(_includeScript);
            }

            // ----- Fusszeile: Buttons + Discord-Name + Status -----

            _discordName = new TextBox
            {
                Width = 200,
                Watermark = "Discord name (optional)",
                Text = CrashReporter.Settings.DiscordName ?? "",
                Background = PanelBrush,
                Foreground = FgBrush,
                BorderBrush = BorderBrushDark,
                VerticalAlignment = VerticalAlignment.Center
            };

            _status = new TextBlock
            {
                Foreground = DimBrush,
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(10, 0, 0, 0),
                TextTrimming = TextTrimming.CharacterEllipsis
            };

            // Avalonia rendert "&" literal (kein WinForms-Mnemonic) — nicht doppeln.
            _reportButton = DarkButton("Report & Close", scriptMode ? (IBrush) GoldBrush : AccentBrush, Brushes.White);
            _reportButton.Click += async (_, _) => await ReportAndCloseAsync();

            _copyButton = DarkButton("Copy to clipboard", PanelBrush, FgBrush);
            _copyButton.Click += async (_, _) =>
            {
                try
                {
                    await Clipboard.SetTextAsync(_report.BuildText());
                    SetStatus("Copied to clipboard.", OkBrush);
                }
                catch (Exception ex)
                {
                    SetStatus($"Copy failed: {ex.Message}", ErrorBrush);
                }
            };

            _closeButton = DarkButton("Close", PanelBrush, FgBrush);
            _closeButton.Click += (_, _) => Close();

            StackPanel buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Children = { _reportButton, _copyButton, _closeButton }
            };

            DockPanel footer = new DockPanel { Margin = new Thickness(12, 10, 12, 12) };
            DockPanel.SetDock(buttons, Dock.Left);
            footer.Children.Add(buttons);

            DockPanel.SetDock(_discordName, Dock.Left);
            _discordName.Margin = new Thickness(12, 0, 0, 0);
            footer.Children.Add(_discordName);

            footer.Children.Add(_status);

            // ----- Layout -----

            DockPanel root = new DockPanel();
            DockPanel.SetDock(headerBorder, Dock.Top);
            root.Children.Add(headerBorder);
            DockPanel.SetDock(footer, Dock.Bottom);
            root.Children.Add(footer);
            DockPanel.SetDock(middle, Dock.Bottom);
            root.Children.Add(middle);
            root.Children.Add(log);

            Content = root;
        }

        private async System.Threading.Tasks.Task ReportAndCloseAsync()
        {
            PersistDiscordName();
            SetButtonsEnabled(false);
            SetStatus("Sending report…", DimBrush);

            string error = await CrashReporter.SendAsync(_report, _discordName.Text?.Trim(),
                _comment.Text, _includeScript?.IsChecked == true);

            if (error == null)
            {
                SetStatus("Report sent. Thank you!", OkBrush);
                Close();
                return;
            }

            SetStatus(error, ErrorBrush);
            SetButtonsEnabled(true);
        }

        private void PersistDiscordName()
        {
            try
            {
                string name = _discordName.Text?.Trim() ?? "";

                if (name != (CrashReporter.Settings.DiscordName ?? ""))
                {
                    CrashReporter.Settings.DiscordName = name;
                    CrashReporter.SaveSettings();
                }
            }
            catch
            {
            }
        }

        private void SetButtonsEnabled(bool enabled)
        {
            _reportButton.IsEnabled = enabled;
            _copyButton.IsEnabled = enabled;
            _closeButton.IsEnabled = enabled;
        }

        private void SetStatus(string text, IBrush brush)
        {
            _status.Text = text;
            _status.Foreground = brush;
        }

        private static Button DarkButton(string text, IBrush background, IBrush foreground)
        {
            return new Button
            {
                Content = text,
                Padding = new Thickness(14, 5),
                Background = background,
                Foreground = foreground,
                BorderBrush = BorderBrushDark,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(2),
                FontSize = 12
            };
        }
    }
}
