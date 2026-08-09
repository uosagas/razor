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

// UOSagas-Razor: Branding (Fenster-Icon + About-Logo).
// Die Bilder sind als EmbeddedResource in die Assembly eingebettet
// (Razor.UI.Assets.sagas_icon.ico/.png) — der Plugin-Output enthaelt keine
// losen Asset-Dateien. Fehlt eine Ressource, passiert einfach nichts.

using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Media.Imaging;

namespace Razor.UI
{
    internal static class Branding
    {
        /// <summary>Eingebettete Ressource oeffnen (null, wenn nicht vorhanden).
        /// Logischer Name = RootNamespace (Razor.UI) + "Assets." + Dateiname.</summary>
        private static Stream OpenAsset(string fileName)
        {
            try
            {
                return typeof(Branding).Assembly
                    .GetManifestResourceStream("Razor.UI.Assets." + fileName);
            }
            catch
            {
                return null;
            }
        }

        private static WindowIcon _windowIcon;
        private static bool _windowIconLoaded;

        /// <summary>Titlebar-/Taskbar-Icon des Razor-Fensters (null, wenn Ressource fehlt). Gecacht.</summary>
        public static WindowIcon TryLoadWindowIcon()
        {
            if (_windowIconLoaded)
                return _windowIcon;

            _windowIconLoaded = true;
            try
            {
                using Stream stream = OpenAsset("sagas_icon.ico") ?? OpenAsset("sagas_icon.png");
                _windowIcon = stream != null ? new WindowIcon(stream) : null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[UOSagas Razor] Fenster-Icon konnte nicht geladen werden: {ex.Message}");
            }

            return _windowIcon;
        }

        /// <summary>
        /// Setzt das Sagas-Icon in die Titelleiste. Fuer JEDES Fenster aufrufen
        /// (MainWindow, IDE, Gump-Inspector, Dialoge), damit die App einheitlich
        /// auftritt statt mit dem Avalonia-Standard-Icon.
        /// </summary>
        public static void ApplyTo(Window window)
        {
            WindowIcon icon = TryLoadWindowIcon();
            if (icon != null && window != null)
                window.Icon = icon;
        }

        /// <summary>
        /// Laedt das PNG-Logo (eingebettet: sagas_icon.png) fuer den About-Tab.
        /// Liefert null, wenn die Ressource fehlt oder nicht ladbar ist (kein
        /// Absturz — der About-Tab zeigt dann einfach kein Logo).
        /// </summary>
        public static Bitmap TryLoadLogo()
        {
            try
            {
                using Stream stream = OpenAsset("sagas_icon.png");
                return stream != null ? new Bitmap(stream) : null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[UOSagas Razor] Logo konnte nicht geladen werden: {ex.Message}");
                return null;
            }
        }
    }
}
