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

// UOSagas-Razor: Identitaet des Assistants fuer die Server-Versionsmeldung.
// Nach dem Login-Confirm schickt Razor 0xBF sub 0x40 ([len]Name[len]Version)
// an den Server; das Server-Gate (AssistantVerification) friert veraltete
// Versionen ein und trennt nach 30s. Kein Razor = keine Meldung = Login ok.

namespace Assistant
{
    public static class AssistantInfo
    {
        public const string Name = "SagasRazor";

        /// <summary>Wird von RazorPlugin beim Start auf die Assembly-Version
        /// von UOSagas.Razor.dll gesetzt (dieselbe Version, die der Launcher
        /// anzeigt und der Server als Pflichtversion prueft).</summary>
        public static string Version = "0.0.0";
    }
}
