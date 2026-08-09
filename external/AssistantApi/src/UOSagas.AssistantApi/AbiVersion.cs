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

namespace UOSagas.AssistantApi
{
    public static class Abi
    {
        /// <summary>
        /// Version of the HostBindings/ClientBindings layout and semantics.
        /// Bump when fields are appended or behavior changes; never reorder existing fields.
        /// </summary>
        public const int Version = 3;

        /// <summary>Exported entry point of the native client library.</summary>
        public const string ClientEntryPoint = "Initialize";
    }
}
