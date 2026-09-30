using System.Diagnostics;
using System.Reflection;

namespace LuajitDecompilerGui;

public sealed class AboutForm : Form
{
    private static readonly Color AppBackground = Color.FromArgb(245, 247, 250);
    private static readonly Color CardBackground = Color.White;
    private static readonly Color TextPrimary = Color.FromArgb(31, 41, 55);
    private static readonly Color TextSecondary = Color.FromArgb(107, 114, 128);
    private static readonly Color Accent = Color.FromArgb(0, 120, 212);

    public AboutForm()
    {
        string version = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion ?? "1.0.0";

        Text = "About LuaJIT Decompiler v2 GUI";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(540, 355);
        BackColor = AppBackground;
        Font = new Font("Segoe UI", 9F);

        var card = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = CardBackground,
            Padding = new Padding(26)
        };
        Controls.Add(card);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 7,
            BackColor = CardBackground
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        card.Controls.Add(layout);

        layout.Controls.Add(new Label
        {
            AutoSize = true,
            Text = "LuaJIT Decompiler v2 GUI",
            Font = new Font("Segoe UI Semibold", 16F),
            ForeColor = TextPrimary,
            Margin = Padding.Empty
        }, 0, 0);

        layout.Controls.Add(new Label
        {
            AutoSize = true,
            Text = $"Version {version}",
            ForeColor = TextSecondary,
            Margin = new Padding(1, 0, 0, 0)
        }, 0, 1);

        layout.Controls.Add(new Label
        {
            AutoSize = false,
            Dock = DockStyle.Fill,
            Text = "A Windows 10/11 GUI front-end for LuaJIT Decompiler v2 with recursive batch processing, LuaJIT 2.0/2.1 detection, preview, logging, and cancellation.",
            ForeColor = TextPrimary,
            Margin = new Padding(0, 8, 0, 0)
        }, 0, 2);

        layout.Controls.Add(CreateLink(
            "GUI project on GitHub",
            "https://github.com/mhsharif7/Luajit-Decompiler-v2-GUI"), 0, 3);

        layout.Controls.Add(CreateLink(
            "LuaJIT Decompiler v2 upstream project",
            "https://github.com/marsinator358/luajit-decompiler-v2"), 0, 4);

        layout.Controls.Add(new Label
        {
            AutoSize = false,
            Dock = DockStyle.Fill,
            Text = "LuaJIT Decompiler v2 GUI is MIT licensed. The bundled native decompiler is third-party software distributed under its upstream MIT License. See LICENSE.md and THIRD_PARTY_NOTICES.md.",
            ForeColor = TextSecondary,
            Margin = new Padding(0, 8, 0, 0)
        }, 0, 5);

        layout.Controls.Add(new Label
        {
            AutoSize = false,
            Dock = DockStyle.Fill,
            Text = "By mhsharif7",
            ForeColor = TextPrimary,
            Margin = new Padding(0, 8, 0, 0)
        }, 0, 5);

        var close = new Button
        {
            Text = "Close",
            Width = 86,
            Height = 32,
            Anchor = AnchorStyles.Right,
            FlatStyle = FlatStyle.Flat,
            BackColor = Accent,
            ForeColor = Color.White,
            UseVisualStyleBackColor = false
        };
        close.FlatAppearance.BorderColor = Accent;
        close.Click += (_, _) => Close();

        var buttonHost = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(0, 4, 0, 0),
            Margin = Padding.Empty
        };
        buttonHost.Controls.Add(close);
        layout.Controls.Add(buttonHost, 0, 6);

        AcceptButton = close;
        CancelButton = close;
    }

    private static LinkLabel CreateLink(string text, string url)
    {
        var link = new LinkLabel
        {
            AutoSize = true,
            Text = text,
            LinkColor = Accent,
            ActiveLinkColor = Accent,
            VisitedLinkColor = Accent,
            Margin = new Padding(0, 8, 0, 0)
        };

        link.LinkClicked += (_, _) =>
        {
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch
            {
                // Ignore browser-launch failures in the About dialog.
            }
        };

        return link;
    }
}
