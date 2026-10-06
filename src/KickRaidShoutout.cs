// =====================================================================
//  KICK RAID AUTO-SHOUTOUT for Streamer.bot
//  Posts a shoutout in your Kick chat when another channel raids (hosts)
//  you. It listens to Kick's live-event feed through the Streamer.bot
//  WebSocket Client called "Kick Events" that is imported with this action.
//
//  Trigger on this action:
//    Core > WebSocket > Client > Message   (client: Kick Events)
//
//  SETUP: fill in the two IDs in the SETTINGS block below.
//  To find them, open this address in your web browser (use your own
//  channel name at the end):
//        https://kick.com/api/v2/channels/YOUR_CHANNEL_NAME
//  CHANNEL_ID  is the first "id" on the page.
//  CHATROOM_ID is the "id" inside the "chatroom" section.
// =====================================================================
using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

public class CPHInline
{
    // ========================= SETTINGS =========================
    const string CHATROOM_ID = "";   // <-- your Kick chatroom id (numbers only)
    const string CHANNEL_ID  = "";   // <-- your Kick channel id (numbers only)

    // {name} = raider's display name, {login} = their channel address, {viewers} = raid size
    const string RAID_TEXT = "Thank you for the raid {name} with {viewers} viewers! Everyone go show them some love at https://kick.com/{login}";

    const bool SEND_AS_BOT = true;   // true = post from your bot account, false = from your own account
    const bool DEBUG_LOG   = false;  // true = write every Kick event to the Streamer.bot log
    // =============================================================

    static readonly Dictionary<string, DateTime> recent = new Dictionary<string, DateTime>();
    static bool warned = false;

    public bool Execute()
    {
        try { Run(); }
        catch (Exception ex) { CPH.LogError("[Kick raids] " + ex); }
        return true;
    }

    void Run()
    {
        int ws = 0;
        int.TryParse(Arg("wsIdx"), out ws);
        string msg = Arg("message");
        if (msg == "") return;

        if (DEBUG_LOG && msg.IndexOf("ChatMessage", StringComparison.Ordinal) < 0 && msg.IndexOf("pusher:ping", StringComparison.Ordinal) < 0)
            CPH.LogInfo("[Kick raids] event: " + (msg.Length > 400 ? msg.Substring(0, 400) : msg));

        var evt = HandleMessage(msg);

        // Kick accepted the connection: subscribe to the chatroom + channel feeds
        if (evt == "connected")
        {
            if (CHATROOM_ID.Trim() == "" || CHANNEL_ID.Trim() == "")
            {
                if (!warned) CPH.LogWarn("[Kick raids] CHATROOM_ID / CHANNEL_ID are empty. Open the 'Kick Raid Shoutout' action, edit the C# code and fill in the SETTINGS block.");
                warned = true;
                return;
            }
            foreach (var ch in new[] { "chatrooms." + CHATROOM_ID.Trim() + ".v2", "chatrooms." + CHATROOM_ID.Trim(), "channel." + CHANNEL_ID.Trim() })
                CPH.WebsocketSend("{\"event\":\"pusher:subscribe\",\"data\":{\"auth\":\"\",\"channel\":\"" + ch + "\"}}", ws);
            CPH.LogInfo("[Kick raids] subscribed to Kick events");
            return;
        }
        if (evt == "ping") { CPH.WebsocketSend("{\"event\":\"pusher:pong\",\"data\":{}}", ws); return; }
        if (evt == null) return;

        // evt = "login|display|viewers"
        var parts = evt.Split('|');
        string login = parts[0].ToLowerInvariant(), display = parts[1], viewers = parts[2];

        // Kick can send the same raid on two feeds - only shout once per minute per raider
        lock (recent)
        {
            DateTime last;
            if (recent.TryGetValue(login, out last) && DateTime.UtcNow - last < TimeSpan.FromMinutes(1)) return;
            recent[login] = DateTime.UtcNow;
        }

        // these can be used by sub-actions you add below this code (%raider%, %raiderLogin%, %raidViewers%)
        CPH.SetArgument("raider", display);
        CPH.SetArgument("raiderLogin", login);
        CPH.SetArgument("raidViewers", viewers);
        CPH.SendKickMessage(RAID_TEXT.Replace("{name}", display).Replace("{login}", login).Replace("{viewers}", viewers), SEND_AS_BOT);
    }

    // Returns "ping", "connected", "login|display|viewers" for a raid, or null
    public static string HandleMessage(string raw)
    {
        var j = JObject.Parse(raw);
        string ev = (string)j["event"] ?? "";
        if (ev == "pusher:ping") return "ping";
        if (ev == "pusher:connection_established") return "connected";
        if (ev.IndexOf("Host", StringComparison.OrdinalIgnoreCase) < 0 && ev.IndexOf("Raid", StringComparison.OrdinalIgnoreCase) < 0)
            return null;

        // Pusher sends "data" as a JSON string
        JToken d = j["data"];
        if (d != null && d.Type == JTokenType.String) d = JToken.Parse((string)d);
        if (d == null) return null;

        string name = (string)(d["host_username"] ?? d.SelectToken("user.username") ?? d.SelectToken("user.slug") ?? d["username"]);
        string login = (string)(d.SelectToken("user.slug") ?? d["slug"]) ?? name;
        string viewers = (string)(d["number_viewers"] ?? d.SelectToken("message.numberOfViewers") ?? d["numberOfViewers"]) ?? "?";
        if (string.IsNullOrWhiteSpace(name)) return null;
        login = (login ?? name).Trim().ToLowerInvariant().Replace("_", "-").Replace(" ", "-");   // Kick URLs use - for _
        return login + "|" + name + "|" + viewers;
    }

    string Arg(string key)
    {
        object v;
        return args != null && args.TryGetValue(key, out v) && v != null ? v.ToString() : "";
    }
}
