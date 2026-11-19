#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace DesktopRider.Core.Configuration
{
    /// <summary>
    /// Who is out riding (the "Who rides" buttons): one, two or all three riders at once. <see cref="AppearanceSettings.Character"/>
    /// rides in the main window, which has the tray icon and the settings; every other rider out rides in a small window of
    /// their own (a "companion", a second copy of the program started with <see cref="CompanionArguments"/>). At least one
    /// rider is always out.
    /// </summary>
    public static class RiderLineUp
    {
        public const string CompanionSwitch = "--companion";
        public const string HostSwitch = "--host";

        /// <summary>Everyone out, in the order of the buttons. Always holds the main window's rider.</summary>
        public static IReadOnlyList<RiderCharacter> Out(AppearanceSettings appearance)
        {
            if (appearance is null) throw new ArgumentNullException(nameof(appearance));
            var main = Enum.IsDefined(typeof(RiderCharacter), appearance.Character) ? appearance.Character : RiderCharacter.Harvey;
            return (appearance.Riders ?? new List<RiderCharacter>())
                .Where(c => Enum.IsDefined(typeof(RiderCharacter), c))
                .Append(main)
                .Distinct()
                .OrderBy(c => (int)c)
                .ToList();
        }

        public static bool IsOut(AppearanceSettings appearance, RiderCharacter who) => Out(appearance).Contains(who);

        /// <summary>The riders out in windows of their own: everyone out but the main window's rider.</summary>
        public static IReadOnlyList<RiderCharacter> Companions(AppearanceSettings appearance) =>
            Out(appearance).Where(c => c != appearance.Character).ToList();

        /// <summary>
        /// Brings <paramref name="who"/> out, or sends them home if they are out. The last rider out stays (returns false).
        /// Sending home the main window's rider hands that window to the first rider still out.
        /// </summary>
        public static bool Toggle(AppearanceSettings appearance, RiderCharacter who)
        {
            if (!Enum.IsDefined(typeof(RiderCharacter), who)) return false;
            var riders = Out(appearance).ToList();
            if (!riders.Contains(who))
            {
                riders.Add(who);
            }
            else
            {
                if (riders.Count == 1) return false;
                riders.Remove(who);
            }

            riders.Sort((a, b) => ((int)a).CompareTo((int)b));
            if (!riders.Contains(appearance.Character)) appearance.Character = riders[0];
            appearance.Riders = riders;
            return true;
        }

        /// <summary>Puts the list in order and makes sure it is never empty and always holds the main window's rider.</summary>
        public static void Normalize(AppearanceSettings appearance)
        {
            if (!Enum.IsDefined(typeof(RiderCharacter), appearance.Character)) appearance.Character = RiderCharacter.Harvey;
            appearance.Riders = Out(appearance).ToList();
        }

        /// <summary>The command line that starts a companion window for <paramref name="who"/>, tied to the main program.</summary>
        public static string CompanionArguments(RiderCharacter who, int hostProcessId) =>
            CompanionSwitch + " " + who + " " + HostSwitch + " " + hostProcessId.ToString(CultureInfo.InvariantCulture);

        /// <summary>True when the command line asks for a companion window; gives who rides in it and the main program's process.</summary>
        public static bool TryReadCompanion(IReadOnlyList<string> arguments, out RiderCharacter who, out int hostProcessId)
        {
            who = RiderCharacter.Harvey;
            hostProcessId = 0;
            if (arguments is null) return false;
            bool found = false;
            for (int i = 0; i + 1 < arguments.Count; i++)
            {
                if (arguments[i] == CompanionSwitch)
                {
                    found = Enum.TryParse(arguments[i + 1], ignoreCase: true, out who) && Enum.IsDefined(typeof(RiderCharacter), who);
                }
                else if (arguments[i] == HostSwitch)
                {
                    int.TryParse(arguments[i + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out hostProcessId);
                }
            }

            return found && hostProcessId > 0;
        }
    }
}
