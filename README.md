# Streamer.bot Auto Shoutout

Automatic shoutouts for **Kick** in [Streamer.bot](https://streamer.bot). Support for other platforms is planned.

Two actions:

1. **Kick Raid Shoutout**: when another channel raids you, the bot thanks them in chat and posts a link to their channel.
2. **Kick Verified Shoutout**: when a chatter with Kick's verified badge talks in your chat, the bot posts a shoutout with a link to their channel. Each person gets one automatic shoutout per cooldown period (5 hours by default). It also adds a mod-only command: `!so name`.

You need Streamer.bot 1.0.0 or newer with your Kick account connected (**Platforms > Kick**). A Kick bot account is optional.

## Download

Download **[Kick-Shoutouts.sb](https://github.com/aosabisan/streamerbot-auto-shoutout/raw/main/Kick-Shoutouts.sb)**, the Streamer.bot import file. You can also use **Code > Download ZIP** at the top of this page.

| File | What it is |
| --- | --- |
| `Kick-Shoutouts.sb` | The import file. This is all you need. |
| `src/KickRaidShoutout.cs` | The raid action's C# code as plain text, to read it or build the action by hand. |
| `src/KickVerifiedShoutout.cs` | The verified shoutout action's C# code as plain text. |

## Install

1. Open Streamer.bot and click **Import** in the top bar.
2. Drag `Kick-Shoutouts.sb` into the **Import String** box. You can also open the file in Notepad, copy everything, and paste it in.
3. You should see 2 actions, 1 command and 1 WebSocket client listed. Click **Import**.

The actions appear under **Actions** in a group called **Kick Shoutouts**.

## Set up the raid shoutout (needs your two Kick IDs)

1. In your web browser, open this address with your own channel name at the end:

   ```
   https://kick.com/api/v2/channels/YOUR_CHANNEL_NAME
   ```

   A page of text appears. Find these two numbers:
   - `CHANNEL_ID`: the very first `"id"` on the page
   - `CHATROOM_ID`: the `"id"` inside the section that starts `"chatroom"`

2. In Streamer.bot go to **Actions > Kick Shoutouts > Kick Raid Shoutout**. In the Sub-Actions panel, double-click **Execute C# Code**.
3. Near the top, in the `SETTINGS` block, put your numbers between the quotes:

   ```csharp
   const string CHATROOM_ID = "12345678";
   const string CHANNEL_ID  = "23456789";
   ```

4. Click **Save and Compile**.
5. Go to **Servers/Clients > WebSocket Clients**. Right-click **Kick Events** and choose **Connect**. If it was already connected, disconnect it and connect again. After this it connects by itself every time Streamer.bot starts.

To confirm it is working, look in the Streamer.bot log for the line `[Kick raids] subscribed to Kick events`.

## Set up the verified shoutout (nothing required)

It works as soon as it is imported. Your own channel is detected automatically and is never shouted out.

Optional settings are at the top of the C# code in **Actions > Kick Shoutouts > Kick Verified Shoutout**:

| Setting | What it does |
| --- | --- |
| `COOLDOWN_HOURS` | Hours before the same person is shouted out again |
| `CHAT_TEXT` | The shoutout message (`{name}` and `{login}` are filled in) |
| `SHOUTOUT_VERIFIED_BADGE` | Turns the verified-badge shoutout on or off |
| `SHOUTOUT_LIST` | Turns the shoutout list on or off |
| `NEVER_SHOUT_OUT` | Accounts to skip, for example other chat bots |
| `TEST_MODE` | Set to `true` to test with your own account |

Click **Save and Compile** after any change.

**Shoutout list:** the first time the action runs, it creates

```
Documents\KickShoutouts\shoutout_list.txt
```

Add Kick usernames to that file, one per line. Those people are shouted out when they chat, even if they have no verified badge.

## Settings both actions share

| Setting | What it does |
| --- | --- |
| `SEND_AS_BOT` | `true`: the message is posted by your Kick bot account. `false`: it is posted by your own account. Set this to `false` if you have not connected a bot account. |

The raid message can be changed with `RAID_TEXT` in the raid action (`{name}`, `{login}` and `{viewers}` are filled in).

## The `!so` command

`!so name` or `!shoutout name` posts a shoutout right away and ignores the cooldown. Only you and your moderators can use it. If you already have your own `!so` command, disable this one under **Commands** (**Shoutout (!so)**) so chat does not get two messages.

## Testing

- **Verified shoutout:** set `TEST_MODE = true`, then Save and Compile. Type in your own chat. Your account needs the verified badge, or you can add your name to `shoutout_list.txt`. Set `TEST_MODE` back to `false` afterwards.
- **Raid shoutout:** this can only be tested by a real raid from another channel.

## If something goes wrong

- **Compile shows errors about missing references:** in the C# code window, open the **References** tab and click **Find Refs**, then **Save and Compile** again.
- **Nothing is posted:** check that Kick is connected under **Platforms > Kick**. Also check that both actions are enabled (right-click the action > **Enabled**).
- **No raid shoutout:** check that the **Kick Events** WebSocket client shows as connected and that both IDs are filled in.
