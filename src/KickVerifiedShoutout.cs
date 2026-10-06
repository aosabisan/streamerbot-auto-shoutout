// =====================================================================
//  KICK VERIFIED-BADGE AUTO-SHOUTOUT for Streamer.bot
//  When someone with Kick's verified badge chats, the bot posts a
//  shoutout with a link to their channel. Each person gets at most one
//  automatic shoutout every COOLDOWN_HOURS.
//
//  Also included:
//    * A shoutout list: anyone named in shoutout_list.txt is shouted out
//      the same way, even without a badge.
//    * A manual command for you and your mods:  !so name
//
//  Triggers on this action:
//    Kick > Chat > Chat Message
//    Core > Commands > Command Triggered   (command: Shoutout (!so))
//
//  No channel details are needed: your own account is detected
//  automatically and is never shouted out.
// =====================================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

public class CPHInline
{
    // ========================= SETTINGS =========================
    const double COOLDOWN_HOURS = 5;              // hours before the same person is shouted out again
    const bool SHOUTOUT_VERIFIED_BADGE = true;    // anyone with Kick's verified badge
    const bool SHOUTOUT_LIST = true;              // anyone listed in shoutout_list.txt

    // {name} = their display name, {login} = their channel address
    const string CHAT_TEXT = "Check out {name} over at https://kick.com/{login} - go give them a follow!";

    const bool SEND_AS_BOT = true;                // true = post from your bot account, false = from your own account

    // Accounts that should never get a shoutout (your bot, other bots). Lower case, comma separated.
    // Your own channel is skipped automatically. Example: "mybot,botrix"
    const string NEVER_SHOUT_OUT = "";

    const bool TEST_MODE = false;                 // true = also shout out yourself and log what Kick sends (for testing)

    // The list and cooldown files are kept in Documents\KickShoutouts
    static readonly string DIR = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "KickShoutouts");
    // =============================================================

    static readonly object Gate = new object();

    public bool Execute()
    {
        lock (Gate)
        {
            try { Run(); }
            catch (Exception ex) { CPH.LogError("[Shoutout] " + ex); }
        }
        return true;
    }

    void Run()
    {
        Directory.CreateDirectory(DIR);
        EnsureListFile();

        string command = Arg("command").ToLowerInvariant();

        // ---- manual: !so name  (streamer and mods only, ignores the cooldown) ----
        if (command.StartsWith("!"))
        {
            if (!IsMod()) return;
            string target = Arg("input0").TrimStart('@').Trim();
            if (target == "") { Say("Use " + command + " @name"); return; }
            Shout(target, target, true);
            return;
        }

        // ---- chat message ----
        if (Arg("isInternal").Equals("true", StringComparison.OrdinalIgnoreCase)) return;
        string loginName = Arg("userName").ToLowerInvariant();
        if (TEST_MODE) CPH.LogInfo("[Shoutout] chat from " + loginName + " args: " + string.Join(", ", args.Select(kv => kv.Key + "=" + Trunc(kv.Value))));
        if (loginName == "" || Ignored().Contains(loginName)) return;
        if (IsStreamer(loginName) && !TEST_MODE) return;

        bool listed = SHOUTOUT_LIST && ReadList().Contains(loginName);
        bool verified = SHOUTOUT_VERIFIED_BADGE && HasVerifiedBadge();
        if (!listed && !verified) return;

        string display = Arg("user") != "" ? Arg("user") : loginName;
        Shout(display, loginName, false);
    }

    // posts the shoutout unless this person had one in the last COOLDOWN_HOURS
    void Shout(string name, string login, bool ignoreCooldown)
    {
        login = login.ToLowerInvariant().TrimStart('@');
        var times = LoadTimes();
        DateTime last;
        if (!ignoreCooldown && times.TryGetValue(login, out last) && DateTime.UtcNow - last < TimeSpan.FromHours(COOLDOWN_HOURS))
            return;

        times[login] = DateTime.UtcNow;
        SaveTimes(times);
        // Kick channel addresses use - where the name has _
        Say(CHAT_TEXT.Replace("{name}", name).Replace("{login}", login.Replace("_", "-")));
    }

    // Kick sends badges as badge.0.id, badge.1.id ... ; look for one called "verified"
    bool HasVerifiedBadge()
    {
        if (args == null) return false;
        foreach (var kv in args)
        {
            if (kv.Value == null) continue;
            string key = kv.Key.ToLowerInvariant();
            string val;
            try { val = kv.Value is string ? (string)kv.Value : JsonConvert.SerializeObject(kv.Value); } catch { val = kv.Value.ToString(); }
            if (key.Contains("verified") && val.Trim('"').Equals("true", StringComparison.OrdinalIgnoreCase)) return true;
            if (key.Contains("badge") && val.IndexOf("verified", StringComparison.OrdinalIgnoreCase) >= 0) return true;
        }
        return false;
    }

    // the channel owner, taken from what Streamer.bot reports for the connected Kick account
    bool IsStreamer(string loginName)
    {
        string me = Arg("broadcastUserName").ToLowerInvariant();
        if (me != "" && me == loginName) return true;
        string myId = Arg("broadcastUserId");
        return myId != "" && myId == Arg("userId");
    }

    HashSet<string> Ignored()
    {
        return new HashSet<string>(NEVER_SHOUT_OUT.Split(',').Select(s => s.Trim().TrimStart('@').ToLowerInvariant()).Where(s => s.Length > 0));
    }

    string Trunc(object o)
    {
        string s;
        try { s = o == null ? "null" : (o is string ? (string)o : JsonConvert.SerializeObject(o)); } catch { s = "?"; }
        return s.Length > 120 ? s.Substring(0, 120) + "..." : s;
    }

    // ---------------- files ----------------

    string ListPath() { return Path.Combine(DIR, "shoutout_list.txt"); }
    string TimesPath() { return Path.Combine(DIR, "shoutout_times.json"); }

    void EnsureListFile()
    {
        if (File.Exists(ListPath())) return;
        File.WriteAllText(ListPath(),
            "# Kick usernames to shout out when they chat (once every " + COOLDOWN_HOURS + " hours).\n" +
            "# One name per line, no @. Lines starting with # are ignored.\n", Encoding.UTF8);
    }

    HashSet<string> ReadList()
    {
        return new HashSet<string>(File.ReadAllLines(ListPath(), Encoding.UTF8)
            .Select(l => l.Trim().TrimStart('@').ToLowerInvariant())
            .Where(l => l.Length > 0 && !l.StartsWith("#")));
    }

    Dictionary<string, DateTime> LoadTimes()
    {
        try
        {
            if (File.Exists(TimesPath()))
                return JsonConvert.DeserializeObject<Dictionary<string, DateTime>>(File.ReadAllText(TimesPath()))
                       ?? new Dictionary<string, DateTime>();
        }
        catch { }
        return new Dictionary<string, DateTime>();
    }

    void SaveTimes(Dictionary<string, DateTime> t)
    {
        // forget anyone older than the cooldown (at least a day) so the file stays small
        var maxAge = TimeSpan.FromHours(Math.Max(24, COOLDOWN_HOURS));
        var keep = t.Where(kv => DateTime.UtcNow - kv.Value < maxAge).ToDictionary(kv => kv.Key, kv => kv.Value);
        File.WriteAllText(TimesPath(), JsonConvert.SerializeObject(keep, Formatting.Indented), Encoding.UTF8);
    }

    // ---------------- helpers ----------------

    string Arg(string key)
    {
        object v;
        return args != null && args.TryGetValue(key, out v) && v != null ? v.ToString() : "";
    }

    bool IsMod()
    {
        if (Arg("isModerator").Equals("true", StringComparison.OrdinalIgnoreCase)) return true;
        if (Arg("isBroadcaster").Equals("true", StringComparison.OrdinalIgnoreCase)) return true;
        return IsStreamer(Arg("userName").ToLowerInvariant());
    }

    void Say(string msg) { CPH.SendKickMessage(msg, SEND_AS_BOT); }
}
