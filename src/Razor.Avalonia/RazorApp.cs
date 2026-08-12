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

// UOSagas-Razor: Avalonia-Application (Phase 3a/3b) — komplett in Code,
// ohne XAML (kein AXAML-Compile-Schritt noetig fuer eine Classlib).
//
// Phase 3b: Klassischer Windows-Look wie Razor CE (WinForms):
// Simple-Theme statt Fluent, Fensterhintergrund #F0F0F0, Segoe UI 12px,
// kompakte Controls ohne Rundungen und ohne Padding-Luxus.

using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Themes.Simple;

namespace Razor.UI
{
    public class RazorApp : Application
    {
        public override void Initialize()
        {
            // Razor-CE-Look: klassisches, helles Tool-Fenster (WinForms-Optik).
            Styles.Add(new SimpleTheme());

            // AvaloniaEdit (IDE-Editor) braucht sein ControlTemplate als
            // StyleInclude — OHNE rendert der TextEditor als leere Flaeche
            // ohne Eingabe/Zeilennummern (Phase-4a-Bugfix).
            Styles.Add(new Avalonia.Markup.Xaml.Styling.StyleInclude(new Uri("avares://Razor.Avalonia"))
            {
                Source = new Uri("avares://AvaloniaEdit/Themes/Simple/AvaloniaEdit.xaml")
            });

            RequestedThemeVariant = ThemeVariant.Light;
            AddClassicStyles();
        }

        /// <summary>Globale Styles fuer den WinForms-Look (Razor CE).</summary>
        private void AddClassicStyles()
        {
            var buttonFace = new SolidColorBrush(Color.Parse("#E1E1E1"));
            var buttonBorder = new SolidColorBrush(Color.Parse("#ADADAD"));
            var controlBorder = new SolidColorBrush(Color.Parse("#7A7A7A"));

            // Buttons: eckig, klassisches Grau.
            Styles.Add(new Style(x => x.OfType<Button>())
            {
                Setters =
                {
                    new Setter(TemplatedControl.CornerRadiusProperty, new CornerRadius(0)),
                    new Setter(TemplatedControl.BackgroundProperty, buttonFace),
                    new Setter(TemplatedControl.BorderBrushProperty, buttonBorder),
                    new Setter(TemplatedControl.PaddingProperty, new Thickness(2, 0)),
                    new Setter(TemplatedControl.FontSizeProperty, Ce.FontSize),
                    new Setter(ContentControl.HorizontalContentAlignmentProperty,
                        Avalonia.Layout.HorizontalAlignment.Center),
                    new Setter(ContentControl.VerticalContentAlignmentProperty,
                        Avalonia.Layout.VerticalAlignment.Center)
                }
            });

            // Checkboxen/Radios: kompakt (WinForms ~16px Glyphe, Text mittig).
            Styles.Add(new Style(x => x.OfType<CheckBox>())
            {
                Setters =
                {
                    new Setter(Layoutable.MinHeightProperty, 0d),
                    new Setter(TemplatedControl.PaddingProperty, new Thickness(4, 0, 0, 0)),
                    new Setter(TemplatedControl.FontSizeProperty, Ce.FontSize)
                }
            });
            Styles.Add(new Style(x => x.OfType<RadioButton>())
            {
                Setters =
                {
                    new Setter(Layoutable.MinHeightProperty, 0d),
                    new Setter(TemplatedControl.PaddingProperty, new Thickness(4, 0, 0, 0)),
                    new Setter(TemplatedControl.FontSizeProperty, Ce.FontSize)
                }
            });

            // TextBoxen: eckig, kompakt.
            Styles.Add(new Style(x => x.OfType<TextBox>())
            {
                Setters =
                {
                    new Setter(TemplatedControl.CornerRadiusProperty, new CornerRadius(0)),
                    new Setter(Layoutable.MinHeightProperty, 0d),
                    new Setter(TemplatedControl.PaddingProperty, new Thickness(3, 2)),
                    new Setter(TemplatedControl.FontSizeProperty, Ce.FontSize)
                }
            });

            // Combos: kompakt.
            Styles.Add(new Style(x => x.OfType<ComboBox>())
            {
                Setters =
                {
                    new Setter(TemplatedControl.CornerRadiusProperty, new CornerRadius(0)),
                    new Setter(Layoutable.MinHeightProperty, 0d),
                    new Setter(TemplatedControl.PaddingProperty, new Thickness(4, 1)),
                    new Setter(TemplatedControl.FontSizeProperty, Ce.FontSize)
                }
            });

            // Tabs: originalgetreue WinForms-Visual-Styles-Reiter wie Razor CE.
            // Unselektiert: heller Vertikal-Verlauf (oben fast weiss), 1px-Rahmen,
            // minimal gerundete Oberkanten, Nachbarn teilen sich den Rahmen
            // (Margin -1). Aktiv: SEITENFARBE (verbindet sich nahtlos mit der
            // Seite), waechst 2px nach oben und 1px nach unten UEBER den
            // Seitenrahmen (ZIndex), kein Fettdruck — exakt WinForms.
            var tabBorder = new SolidColorBrush(Color.Parse("#8C8C8C"));
            var tabGrad = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(Color.Parse("#FCFCFC"), 0),
                    new GradientStop(Color.Parse("#F4F4F4"), 0.5),
                    new GradientStop(Color.Parse("#EAEAEA"), 1)
                }
            };
            var tabGradHover = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(Color.Parse("#FFFFFF"), 0),
                    new GradientStop(Color.Parse("#FAFAFA"), 0.5),
                    new GradientStop(Color.Parse("#F1F1F1"), 1)
                }
            };
            Styles.Add(new Style(x => x.OfType<TabItem>())
            {
                Setters =
                {
                    new Setter(TemplatedControl.FontSizeProperty, Ce.FontSize),
                    new Setter(TemplatedControl.FontWeightProperty, FontWeight.Normal),
                    new Setter(Layoutable.MinHeightProperty, 0d),
                    new Setter(TemplatedControl.PaddingProperty, new Thickness(8, 2)),
                    new Setter(TemplatedControl.BackgroundProperty, tabGrad),
                    new Setter(TemplatedControl.BorderBrushProperty, tabBorder),
                    new Setter(TemplatedControl.BorderThicknessProperty, new Thickness(1, 1, 1, 0)),
                    new Setter(TemplatedControl.CornerRadiusProperty, new CornerRadius(2, 2, 0, 0)),
                    new Setter(Layoutable.MarginProperty, new Thickness(0, 2, -1, 0))
                }
            });
            Styles.Add(new Style(x => x.OfType<TabItem>().Class(":pointerover"))
            {
                Setters = { new Setter(TemplatedControl.BackgroundProperty, tabGradHover) }
            });
            Styles.Add(new Style(x => x.OfType<TabItem>().Class(":selected"))
            {
                Setters =
                {
                    new Setter(TemplatedControl.BackgroundProperty, Ce.WindowBackground),
                    new Setter(Layoutable.MarginProperty, new Thickness(0, 0, -1, -1)),
                    new Setter(TemplatedControl.PaddingProperty, new Thickness(9, 3, 9, 4)),
                    new Setter(Visual.ZIndexProperty, 1)
                }
            });
            // Simple-Theme-Eigenheit: der TabItem-Template-Baum ist nur ein
            // ContentPresenter (kein Border-Part) — :selected/:pointerover
            // faerbt das Theme den Presenter DIREKT (halbtransparenter Akzent),
            // deshalb dort ebenso direkt uebersteuern.
            Styles.Add(new Style(x =>
                x.OfType<TabItem>().Class(":pointerover").Template()
                    .OfType<Avalonia.Controls.Presenters.ContentPresenter>()
                    .Name("PART_ContentPresenter"))
            {
                Setters =
                {
                    new Setter(Avalonia.Controls.Presenters.ContentPresenter.BackgroundProperty, tabGradHover)
                }
            });
            Styles.Add(new Style(x =>
                x.OfType<TabItem>().Class(":selected").Template()
                    .OfType<Avalonia.Controls.Presenters.ContentPresenter>()
                    .Name("PART_ContentPresenter"))
            {
                Setters =
                {
                    new Setter(Avalonia.Controls.Presenters.ContentPresenter.BackgroundProperty, Ce.WindowBackground)
                }
            });
            // WinForms-Seitenrahmen: 1px um die Tab-Seite (PART_SelectedContentHost).
            Styles.Add(new Style(x =>
                x.OfType<TabControl>().Template()
                    .OfType<Avalonia.Controls.Presenters.ContentPresenter>()
                    .Name("PART_SelectedContentHost"))
            {
                Setters =
                {
                    new Setter(Avalonia.Controls.Presenters.ContentPresenter.BorderBrushProperty, tabBorder),
                    new Setter(Avalonia.Controls.Presenters.ContentPresenter.BorderThicknessProperty,
                        new Thickness(1))
                }
            });
            Styles.Add(new Style(x => x.OfType<TabControl>())
            {
                Setters =
                {
                    new Setter(TemplatedControl.PaddingProperty, new Thickness(2))
                }
            });

            // Listen: weiss mit klassischem Rahmen.
            Styles.Add(new Style(x => x.OfType<ListBox>())
            {
                Setters =
                {
                    new Setter(TemplatedControl.BackgroundProperty, Brushes.White),
                    new Setter(TemplatedControl.BorderBrushProperty, controlBorder),
                    new Setter(TemplatedControl.BorderThicknessProperty, new Thickness(1)),
                    new Setter(TemplatedControl.CornerRadiusProperty, new CornerRadius(0)),
                    new Setter(TemplatedControl.FontSizeProperty, Ce.FontSize)
                }
            });
            Styles.Add(new Style(x => x.OfType<ListBoxItem>())
            {
                Setters =
                {
                    new Setter(Layoutable.MinHeightProperty, 0d),
                    new Setter(TemplatedControl.PaddingProperty, new Thickness(3, 1))
                }
            });
            Styles.Add(new Style(x => x.OfType<TreeView>())
            {
                Setters =
                {
                    new Setter(TemplatedControl.BackgroundProperty, Brushes.White),
                    new Setter(TemplatedControl.BorderBrushProperty, controlBorder),
                    new Setter(TemplatedControl.BorderThicknessProperty, new Thickness(1)),
                    new Setter(TemplatedControl.FontSizeProperty, Ce.FontSize)
                }
            });
            Styles.Add(new Style(x => x.OfType<TreeViewItem>())
            {
                Setters =
                {
                    new Setter(Layoutable.MinHeightProperty, 0d),
                    new Setter(TemplatedControl.FontSizeProperty, Ce.FontSize)
                }
            });

            Styles.Add(new Style(x => x.OfType<TextBlock>())
            {
                Setters =
                {
                    new Setter(TextBlock.FontSizeProperty, Ce.FontSize)
                }
            });

            // Deaktivierte Controls: auch der TEXT wird grau (WinForms-Verhalten).
            // Das Simple-Theme dimmt sonst nur die Glyphe/den Rahmen, die
            // Beschriftung bleibt schwarz und sieht bedienbar aus.
            foreach (Style style in new[]
            {
                new Style(x => x.OfType<CheckBox>().Class(":disabled")),
                new Style(x => x.OfType<RadioButton>().Class(":disabled")),
                new Style(x => x.OfType<Button>().Class(":disabled")),
                new Style(x => x.OfType<ComboBox>().Class(":disabled")),
                new Style(x => x.OfType<TextBox>().Class(":disabled"))
            })
            {
                style.Setters.Add(new Setter(TemplatedControl.ForegroundProperty, Ce.GrayText));
                Styles.Add(style);
            }
        }

        public override void OnFrameworkInitializationCompleted()
        {
            try
            {
                // Crash-Reporter: eine unbehandelte Exception in einem UI-Handler
                // beendet sonst die Dispatcher-Loop — Razor "schliesst sich
                // wortlos". Handled=true haelt die Loop am Leben, der Fehler
                // geht ins Crash-Fenster (Report & Close -> Discord).
                Avalonia.Threading.Dispatcher.UIThread.UnhandledException += (_, e) =>
                {
                    e.Handled = true;
                    Assistant.CrashReporter.Report(e.Exception, "UI thread");
                };

                // Crash-Fenster-Anbindung fuer Fehler von anderen Threads
                // (Game-Thread-Callbacks, AppDomain, unbeobachtete Tasks).
                Assistant.CrashReporter.UiPresenter = report =>
                {
                    if (!RazorUi.FrameworkReady)
                        return false;

                    Avalonia.Threading.Dispatcher.UIThread.Post(
                        () => CrashReportWindow.Present(report));
                    return true;
                };

                if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    // Fenster schliessen darf NIE den Prozess (das Spiel) beenden.
                    desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

                    var window = new MainWindow();
                    RazorUi.MainWindow = window;
                    desktop.MainWindow = window;
                    window.Show();
                }

                RazorUi.OnFrameworkReady();

                // Phase 4b: Host fuer die Lua-Script-Fenster (UI.CreateWindow)
                // — pollt den ScriptUIManager und zeichnet Avalonia-Fenster.
                ScriptUi.ScriptUiHost.Start();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[UOSagas Razor] UI-Initialisierung fehlgeschlagen: {ex}");
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}
