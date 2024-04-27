using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Linq;
using Deceive.Properties;

namespace Deceive;

public class FriendStatus
    {
        public string Jid { get; set; }
        public string Status { get; set; }
    }

internal class MainController : ApplicationContext
{
    internal MainController()
    {
        TrayIcon = new NotifyIcon
        {
            Icon = Resources.DeceiveIcon,
            Visible = true,
            BalloonTipTitle = StartupHandler.DeceiveTitle,
            BalloonTipText = "Deceive is currently masking your status. Right-click the tray icon for more options."
        };
        TrayIcon.BalloonTipClicked += TrayIcon_BalloonTipClicked;
        TrayIcon.ShowBalloonTip(5000);

        LoadStatus();
        UpdateTray();
    }

    private NotifyIcon TrayIcon { get; }
    private NotificationManager notificationManager = new NotificationManager();
    private Dictionary<string, FriendStatus> friendsList = new Dictionary<string, FriendStatus>();
    public bool Enabled { get; set; } = true;
    public string Status { get; set; } = null!;
    private string StatusFile { get; } = Path.Combine(Persistence.DataDir, "status");
    public bool ConnectToMuc { get; set; } = true;
    private bool SentIntroductionText { get; set; } = false;
    private CancellationTokenSource? ShutdownToken { get; set; } = null;

    private ToolStripMenuItem EnabledMenuItem { get; set; } = null!;
    private ToolStripMenuItem ChatStatus { get; set; } = null!;
    private ToolStripMenuItem OfflineStatus { get; set; } = null!;
    private ToolStripMenuItem MobileStatus { get; set; } = null!;

    private List<ProxiedConnection> Connections { get; } = new();

    public void StartServingClients(TcpListener server, string chatHost, int chatPort)
    {
        Task.Run(() => ServeClientsAsync(server, chatHost, chatPort));
    }

    private void TrayIcon_BalloonTipClicked(object sender, EventArgs e)
    {
        // Assuming the sender can be cast to NotifyIcon and has a tag or similar with the ID
        notificationManager.AcknowledgeNotification((sender as NotifyIcon)?.Tag.ToString());
    }
    
    public void SomeMethodTriggeringNotifications(string message)
    {
        string notificationId = Guid.NewGuid().ToString(); // Unique ID for each notification
        notificationManager.ShowNotification(notificationId, message, TrayIcon);
    }

    public async Task SomeAsyncOperation()
    {
        await Task.Delay(1000); // Simulating async operation
        SomeMethodTriggeringNotifications("Operation Completed");
    }

    private async Task ServeClientsAsync(TcpListener server, string chatHost, int chatPort)
    {    
        var cert = new X509Certificate2(Resources.Certificate);

        while (true)
        {
            try
            {
                var incoming = await server.AcceptTcpClientAsync();
                var sslIncoming = new SslStream(incoming.GetStream(), leaveInnerStreamOpen: false);
                await sslIncoming.AuthenticateAsServerAsync(cert);

                TcpClient outgoing = null;
                int retryCount = 0;
                const int maxRetries = 3;
                while (retryCount < maxRetries)
                {
                    try
                    {
                        outgoing = new TcpClient();
                        await outgoing.ConnectAsync(chatHost, chatPort);
                        break;
                    }
                    catch (SocketException e)
                    {
                        retryCount++;
                        Trace.WriteLine($"Retry {retryCount} for connecting to chat server failed: {e}");
                        if (retryCount >= maxRetries)
                        {
                            MessageBox.Show(
                                "Unable to connect to the chat server after several attempts. Please check your network connection.",
                                StartupHandler.DeceiveTitle,
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Error
                            );
                            break;
                        }
                        await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, retryCount)));  // Exponential back-off
                    }
                }

                if (outgoing?.Connected == true)
                {
                    var sslOutgoing = new SslStream(outgoing.GetStream(), leaveInnerStreamOpen: false);
                    await sslOutgoing.AuthenticateAsClientAsync(chatHost);

                    var proxiedConnection = new ProxiedConnection(this, sslIncoming, sslOutgoing);
                    proxiedConnection.Start();
                    Connections.Add(proxiedConnection);
                }
            }
            catch (Exception e)
            {
                Trace.WriteLine("Failed to handle incoming connection: " + e.Message);
            }
        }
    }

    public void UpdateFriendStatus(FriendStatus status)
    {
        if (friendsList.ContainsKey(status.Jid))
        {
            friendsList[status.Jid] = status;
        }
        else
        {
            friendsList.Add(status.Jid, status);
        }

        // Notify UI or other components that a friend's status has updated
        NotifyStatusChanged(status);
    }

    private void UpdateTray()
    {
        var aboutMenuItem = new ToolStripMenuItem(StartupHandler.DeceiveTitle) { Enabled = false };

        EnabledMenuItem = new ToolStripMenuItem("Enabled", null, async (_, _) =>
        {
            Enabled = !Enabled;
            await UpdateStatusAsync(Enabled ? Status : "chat");
            await SendMessageFromFakePlayerAsync(Enabled ? "Deceive is now enabled." : "Deceive is now disabled.");
            UpdateTray();
        })
        { Checked = Enabled };

        var mucMenuItem = new ToolStripMenuItem("Enable lobby chat", null, (_, _) =>
        {
            ConnectToMuc = !ConnectToMuc;
            UpdateTray();
        })
        { Checked = ConnectToMuc };

        ChatStatus = new ToolStripMenuItem("Online", null, async (_, _) =>
        {
            await UpdateStatusAsync(Status = "chat");
            Enabled = true;
            UpdateTray();
        })
        { Checked = Status.Equals("chat") };

        OfflineStatus = new ToolStripMenuItem("Offline", null, async (_, _) =>
        {
            await UpdateStatusAsync(Status = "offline");
            Enabled = true;
            UpdateTray();
        })
        { Checked = Status.Equals("offline") };

        MobileStatus = new ToolStripMenuItem("Mobile", null, async (_, _) =>
        {
            await UpdateStatusAsync(Status = "mobile");
            Enabled = true;
            UpdateTray();
        })
        { Checked = Status.Equals("mobile") };

        var typeMenuItem = new ToolStripMenuItem("Status Type", null, ChatStatus, OfflineStatus, MobileStatus);

        var restartWithDifferentGameItem = new ToolStripMenuItem("Restart and launch a different game", null, (_, _) =>
        {
            var result = MessageBox.Show(
                "Restart Deceive to launch a different game? This will also stop related games if they are running.",
                StartupHandler.DeceiveTitle,
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button1
            );

            if (result is not DialogResult.Yes)
                return;

            Utils.KillProcesses();
            Thread.Sleep(2000);

            Persistence.SetDefaultLaunchGame(LaunchGame.Prompt);
            Process.Start(Application.ExecutablePath);
            Environment.Exit(0);
        });

        var quitMenuItem = new ToolStripMenuItem("Quit", null, (_, _) =>
        {
            var result = MessageBox.Show(
                "Are you sure you want to stop Deceive? This will also stop related games if they are running.",
                StartupHandler.DeceiveTitle,
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button1
            );

            if (result is not DialogResult.Yes)
                return;

            Utils.KillProcesses();
            SaveStatus();
            Application.Exit();
        });

        TrayIcon.ContextMenuStrip = new ContextMenuStrip();

#if DEBUG
        var sendTestMsg = new ToolStripMenuItem("Send message", null, async (_, _) => { await SendMessageFromFakePlayerAsync("Test"); });

        TrayIcon.ContextMenuStrip.Items.AddRange(new ToolStripItem[]
        {
            aboutMenuItem, EnabledMenuItem, typeMenuItem, mucMenuItem, sendTestMsg, restartWithDifferentGameItem, quitMenuItem
        });
#else
        TrayIcon.ContextMenuStrip.Items.AddRange(new ToolStripItem[] { aboutMenuItem, EnabledMenuItem, typeMenuItem, mucMenuItem, restartWithDifferentGameItem, quitMenuItem });
#endif
    }

    public async Task HandleChatMessage(string content)
    {
        if (content.ToLower().Contains("offline"))
        {
            if (!Enabled)
                await SendMessageFromFakePlayerAsync("Deceive is now enabled.");
            OfflineStatus.PerformClick();
        }
        else if (content.ToLower().Contains("mobile"))
        {
            if (!Enabled)
                await SendMessageFromFakePlayerAsync("Deceive is now enabled.");
            MobileStatus.PerformClick();
        }
        else if (content.ToLower().Contains("online"))
        {
            if (!Enabled)
                await SendMessageFromFakePlayerAsync("Deceive is now enabled.");
            ChatStatus.PerformClick();
        }
        else if (content.ToLower().Contains("enable"))
        {
            if (Enabled)
                await SendMessageFromFakePlayerAsync("Deceive is already enabled.");
            else
                EnabledMenuItem.PerformClick();
        }
        else if (content.ToLower().Contains("disable"))
        {
            if (!Enabled)
                await SendMessageFromFakePlayerAsync("Deceive is already disabled.");
            else
                EnabledMenuItem.PerformClick();
        }
        else if (content.ToLower().Contains("status"))
        {
            if (Status == "chat")
                await SendMessageFromFakePlayerAsync("You are appearing online.");
            else
                await SendMessageFromFakePlayerAsync("You are appearing " + Status + ".");
        }
        else if (content.ToLower().Contains("help"))
        {
            await SendMessageFromFakePlayerAsync("You can send the following messages to quickly change Deceive settings: online/offline/mobile/enable/disable/status");
        }
    }
    
    public class NotificationManager
    {
        private Dictionary<string, Notification> activeNotifications = new Dictionary<string, Notification>();

        public void ShowNotification(string id, string message, NotifyIcon trayIcon)
        {
            if (!activeNotifications.ContainsKey(id))
            {
                trayIcon.BalloonTipText = message;
                trayIcon.ShowBalloonTip(5000);
                activeNotifications[id] = new Notification { Id = id, Message = message, IsAcknowledged = false };
            }
        }

        public void AcknowledgeNotification(string id)
        {
            if (activeNotifications.ContainsKey(id))
            {
                activeNotifications[id].IsAcknowledged = true;
                // Additional logic to handle re-triggering if required
            }
        }
    }

    public class Notification
    {
        public string Id { get; set; }
        public string Message { get; set; }
        public bool IsAcknowledged { get; set; }
    }

    private async Task SendIntroductionTextAsync()
    {
        SentIntroductionText = true;
        await SendMessageFromFakePlayerAsync("Welcome! Deceive is running and you are currently appearing " + Status +
                                             ". Despite what the game client may indicate, you are appearing offline to your friends unless you manually disable Deceive.");
        await Task.Delay(200);
        await SendMessageFromFakePlayerAsync(
            "If you want to invite others while being offline, you may need to disable Deceive for them to accept. You can enable Deceive again as soon as they are in your lobby.");
        await Task.Delay(200);
        await SendMessageFromFakePlayerAsync("To enable or disable Deceive, or to configure other settings, find Deceive in your tray icons.");
        await Task.Delay(200);
        await SendMessageFromFakePlayerAsync("Have fun!");
    }

    private async Task SendMessageFromFakePlayerAsync(string message)
    {
        foreach (var connection in Connections)
            await connection.SendMessageFromFakePlayerAsync(message);
    }

    private async Task UpdateStatusAsync(string newStatus)
    {
        foreach (var connection in Connections)
            await connection.UpdateStatusAsync(newStatus);

        if (newStatus == "chat")
            await SendMessageFromFakePlayerAsync("You are now appearing online.");
        else
            await SendMessageFromFakePlayerAsync("You are now appearing " + newStatus + ".");
    }

    private void LoadStatus()
    {
        if (File.Exists(StatusFile))
            Status = File.ReadAllText(StatusFile) == "mobile" ? "mobile" : "offline";
        else
            Status = "offline";
    }

    private async Task ShutdownIfNoReconnect()
    {
        if (ShutdownToken == null)
            ShutdownToken = new CancellationTokenSource();
        await Task.Delay(60_000, ShutdownToken.Token);

        Trace.WriteLine("Received no new connections after 60s, shutting down.");
        Environment.Exit(0);
    }

    private void SaveStatus() => File.WriteAllText(StatusFile, Status);
}
