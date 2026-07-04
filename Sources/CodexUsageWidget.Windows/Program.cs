using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Web.Script.Serialization;

namespace CodexU.Windows
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            if (args.Length > 0 && args[0] == "--dump-json")
            {
                UsageSnapshot snapshot = CodexUsageReader.Load();
                AttachParentConsole();
                WriteDiagnosticOutput(snapshot.ToJson() + Environment.NewLine);
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            UsageSnapshot initial = new UsageSnapshot();
            initial.Messages.Add(UiLabels.Loading);
            Application.Run(new WidgetContext(initial));
        }

        private static void AttachParentConsole()
        {
            try
            {
                AttachConsole(ATTACH_PARENT_PROCESS);
                StreamWriter writer = new StreamWriter(Console.OpenStandardOutput(), Encoding.UTF8);
                writer.AutoFlush = true;
                Console.SetOut(writer);
            }
            catch { }
        }

        private static void WriteDiagnosticOutput(string text)
        {
            try
            {
                Console.OutputEncoding = Encoding.UTF8;
                Console.Write(text);
            }
            catch { }

            try
            {
                byte[] bytes = Encoding.UTF8.GetBytes(text);
                IntPtr handle = GetStdHandle(STD_OUTPUT_HANDLE);
                int written;
                WriteFile(handle, bytes, bytes.Length, out written, IntPtr.Zero);
            }
            catch { }
        }

        private const int ATTACH_PARENT_PROCESS = -1;
        private const int STD_OUTPUT_HANDLE = -11;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AttachConsole(int dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetStdHandle(int nStdHandle);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool WriteFile(IntPtr hFile, byte[] lpBuffer, int nNumberOfBytesToWrite, out int lpNumberOfBytesWritten, IntPtr lpOverlapped);
    }

    internal sealed class WidgetContext : ApplicationContext
    {
        private readonly UsageWidgetForm form;
        private NotifyIcon notifyIcon;
        private System.Windows.Forms.Timer refreshTimer;
        private readonly System.Windows.Forms.Timer showTimer;

        public WidgetContext(UsageSnapshot initialSnapshot)
        {
            form = new UsageWidgetForm(skipShellInit: true);
            MainForm = form;
            form.SetSnapshot(initialSnapshot);
            form.FormClosed += delegate { ExitThread(); };

            notifyIcon = new NotifyIcon();
            notifyIcon.Icon = SystemIcons.Application;
            notifyIcon.Text = "codexU";
            notifyIcon.Visible = true;
            notifyIcon.DoubleClick += delegate { ToggleWidget(); };

            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Items.Add("显示/隐藏", null, delegate { ToggleWidget(); });
            menu.Items.Add("刷新", null, delegate { RefreshSnapshot(); });
            menu.Items.Add("退出", null, delegate { ExitThread(); });
            notifyIcon.ContextMenuStrip = menu;

            refreshTimer = new System.Windows.Forms.Timer();
            refreshTimer.Interval = 300000;
            refreshTimer.Tick += delegate { RefreshSnapshot(); };
            refreshTimer.Start();

            showTimer = new System.Windows.Forms.Timer();
            showTimer.Interval = 50;
            showTimer.Tick += delegate
            {
                showTimer.Stop();
                showTimer.Dispose();
                form.Show();
                form.WindowState = FormWindowState.Normal;
                form.Activate();
                form.BringToFront();
            };
            showTimer.Start();
        }

        protected override void ExitThreadCore()
        {
            refreshTimer.Stop();
            notifyIcon.Visible = false;
            notifyIcon.Dispose();
            form.Dispose();
            base.ExitThreadCore();
        }

        private void ToggleWidget()
        {
            if (form.Visible) form.Hide();
            else
            {
                form.Show();
                form.Activate();
            }
        }

        private void RefreshSnapshot()
        {
            form.RefreshData();
        }
    }

    internal sealed class UsageWidgetForm : Form
    {
        private readonly HeaderBar headerBar;
        private readonly DashboardSurface dashboard;
        private readonly bool skipShellInit;
        private NotifyIcon notifyIcon;
        private System.Windows.Forms.Timer refreshTimer;
        private UsageSnapshot snapshot;
        private Point dragOffset;
        private bool dragging;

        public UsageWidgetForm(bool skipShellInit = false)
        {
            this.skipShellInit = skipShellInit;
            Text = "codexU";
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = true;
            TopMost = true;
            Size = new Size(720, 620);
            BackColor = Color.FromArgb(190, 220, 230);   // fully opaque RGB — no ARGB to avoid layered-window deadlock
            Opacity = 0.97;
            DoubleBuffered = true;

            headerBar = new HeaderBar();
            headerBar.Bounds = new Rectangle(18, 14, Width - 36, 58);
            headerBar.RefreshRequested += delegate { RefreshData(); };
            headerBar.CloseRequested += delegate { Close(); };
            headerBar.MouseDown += StartDrag;
            headerBar.MouseMove += DragWindow;
            headerBar.MouseUp += StopDrag;

            dashboard = new DashboardSurface();
            dashboard.Bounds = new Rectangle(18, 86, Width - 36, 512);
            dashboard.MouseDown += StartDrag;
            dashboard.MouseMove += DragWindow;
            dashboard.MouseUp += StopDrag;

            Controls.Add(headerBar);
            Controls.Add(dashboard);
        }

        public void SetSnapshot(UsageSnapshot value)
        {
            snapshot = value ?? new UsageSnapshot();
            headerBar.SetSnapshot(snapshot);
            dashboard.SetSnapshot(snapshot);
            Invalidate();
        }

        public void SetLoading(bool loading)
        {
            headerBar.SetLoading(loading);
        }

        public void RefreshData()
        {
            SetLoading(true);
            ThreadPool.QueueUserWorkItem(delegate
            {
                UsageSnapshot value = CodexUsageReader.Load();
                if (!IsDisposed)
                {
                    BeginInvoke(new MethodInvoker(delegate
                    {
                        SetSnapshot(value);
                        SetLoading(false);
                    }));
                }
            });
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            // Full rounded window background — near-opaque blue-gray, blocks desktop content
            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = UiDrawing.RoundedRect(rect, 24))
            {
                // Solid near-opaque fill first — prevents any desktop bleed-through
                using (SolidBrush solidFill = new SolidBrush(Color.FromArgb(240, 190, 220, 230)))
                {
                    g.FillPath(solidFill, path);
                }
                // Subtle white haze on top
                using (SolidBrush haze = new SolidBrush(Color.FromArgb(48, Color.White)))
                {
                    g.FillPath(haze, path);
                }
                // Soft white border
                using (Pen border = new Pen(Color.FromArgb(130, Color.White), 1f))
                {
                    g.DrawPath(border, path);
                }
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            ApplyRoundedWindow();
        }

        protected override void Dispose(bool disposing)
        {
            UnregisterHotKey(Handle, 1);
            if (!skipShellInit)
            {
                if (refreshTimer != null) { refreshTimer.Stop(); refreshTimer.Dispose(); }
                if (notifyIcon != null) { notifyIcon.Visible = false; notifyIcon.Dispose(); }
            }
            base.Dispose(disposing);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == 1)
            {
                if (Visible) Hide();
                else
                {
                    Show();
                    Activate();
                }
                return;
            }
            base.WndProc(ref m);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            EnsureShellIntegrations();
            WindowState = FormWindowState.Normal;
            ApplyRoundedWindow();
            TryEnableBackdrop();
            TopMost = true;
            Activate();
            BringToFront();
            RefreshData();
        }

        private void EnsureShellIntegrations()
        {
            // When managed by WidgetContext, skip duplicate tray icon / timer creation
            if (skipShellInit) return;

            if (notifyIcon == null)
            {
                notifyIcon = new NotifyIcon();
                notifyIcon.Icon = SystemIcons.Application;
                notifyIcon.Text = "codexU";
                notifyIcon.Visible = true;
                notifyIcon.DoubleClick += delegate { ToggleVisible(); };
                ContextMenuStrip menu = new ContextMenuStrip();
                menu.Items.Add("显示/隐藏", null, delegate { ToggleVisible(); });
                menu.Items.Add("刷新", null, delegate { RefreshData(); });
                menu.Items.Add("退出", null, delegate { Close(); });
                notifyIcon.ContextMenuStrip = menu;
            }

            if (refreshTimer == null)
            {
                refreshTimer = new System.Windows.Forms.Timer();
                refreshTimer.Interval = 300000;
                refreshTimer.Tick += delegate { RefreshData(); };
                refreshTimer.Start();
            }

            RegisterHotKey(Handle, 1, MOD_CONTROL | MOD_ALT, (int)Keys.U);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
        }

        private void ToggleVisible()
        {
            if (Visible) Hide();
            else
            {
                Show();
                WindowState = FormWindowState.Normal;
                Activate();
                BringToFront();
            }
        }

        private void StartDrag(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            dragging = true;
            dragOffset = PointToClient(((Control)sender).PointToScreen(e.Location));
        }

        private void DragWindow(object sender, MouseEventArgs e)
        {
            if (!dragging) return;
            Point screen = ((Control)sender).PointToScreen(e.Location);
            Location = new Point(screen.X - dragOffset.X, screen.Y - dragOffset.Y);
        }

        private void StopDrag(object sender, MouseEventArgs e)
        {
            dragging = false;
        }

        private void ApplyRoundedWindow()
        {
            if (Width <= 0 || Height <= 0) return;
            using (GraphicsPath path = UiDrawing.RoundedRect(new Rectangle(0, 0, Width, Height), 24))
            {
                Region = new Region(path);
            }
        }

        private void TryEnableBackdrop()
        {
            try
            {
                int trueValue = 1;
                DwmSetWindowAttribute(Handle, 20, ref trueValue, Marshal.SizeOf(typeof(int)));
                int backdrop = 2;
                DwmSetWindowAttribute(Handle, 38, ref backdrop, Marshal.SizeOf(typeof(int)));
            }
            catch { }
        }

        private const int WM_HOTKEY = 0x0312;
        private const int MOD_ALT = 0x0001;
        private const int MOD_CONTROL = 0x0002;

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
    }

    internal sealed class HeaderBar : Control
    {
        public event EventHandler RefreshRequested;
        public event EventHandler CloseRequested;

        private readonly HeaderPillButton languageButton;
        private readonly HeaderPillButton planButton;
        private readonly HeaderPillButton refreshButton;
        private readonly HeaderPillButton closeButton;
        private UsageSnapshot snapshot = new UsageSnapshot();

        public HeaderBar()
        {
            SetStyle(ControlStyles.UserPaint |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);
            BackColor = Color.FromArgb(190, 220, 230);  // opaque blue-gray

            languageButton = new HeaderPillButton("中 / EN");
            languageButton.Font = UiTheme.Font(13f, FontStyle.Bold);
            planButton = new HeaderPillButton("--");
            planButton.Font = UiTheme.Font(13f, FontStyle.Bold);
            refreshButton = new HeaderPillButton("↻");
            refreshButton.Font = UiTheme.Font(15f, FontStyle.Bold);
            closeButton = new HeaderPillButton("×");
            closeButton.Font = UiTheme.Font(15f, FontStyle.Bold);
            refreshButton.Click += delegate { if (RefreshRequested != null) RefreshRequested(this, EventArgs.Empty); };
            closeButton.Click += delegate { if (CloseRequested != null) CloseRequested(this, EventArgs.Empty); };

            Controls.Add(languageButton);
            Controls.Add(planButton);
            Controls.Add(refreshButton);
            Controls.Add(closeButton);
        }

        public void SetSnapshot(UsageSnapshot value)
        {
            snapshot = value ?? new UsageSnapshot();
            planButton.Label = UiFormat.PlanLabel(snapshot.AccountPlan);
            Invalidate();
        }

        public void SetLoading(bool loading)
        {
            refreshButton.Enabled = !loading;
            refreshButton.Label = loading ? "..." : "↻";
        }

        protected override void OnLayout(LayoutEventArgs levent)
        {
            base.OnLayout(levent);
            const int gap = 8;
            const int top = 12;          // vertically centered in 58px header
            const int buttonHeight = 34;
            const int langWidth = 76;
            const int planWidth = 58;
            const int iconWidth = 34;
            int totalWidth = langWidth + planWidth + iconWidth + iconWidth + gap * 3;
            int x = Width - totalWidth - 8;

            languageButton.Bounds = new Rectangle(x, top, langWidth, buttonHeight);
            x += langWidth + gap;
            planButton.Bounds = new Rectangle(x, top, planWidth, buttonHeight);
            x += planWidth + gap;
            refreshButton.Bounds = new Rectangle(x, top, iconWidth, buttonHeight);
            x += iconWidth + gap;
            closeButton.Bounds = new Rectangle(x, top, iconWidth, buttonHeight);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            // Fill full header background first — opaque, no gaps
            e.Graphics.FillRectangle(new SolidBrush(Color.FromArgb(240, 190, 220, 230)), ClientRectangle);

            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            // Calculate safe title area (stop before the right-side buttons)
            const int paddingLeft = 22;
            const int gap = 8;
            const int langW = 76, planW = 58, iconW = 34;
            int actionsTotal = langW + planW + iconW + iconW + gap * 3;
            int actionsLeft = Width - actionsTotal - 8;  // right margin
            int titleMaxX = actionsLeft - 16;             // 16px safety gap before buttons

            // Logo icon
            Rectangle icon = new Rectangle(paddingLeft - 14, 9, 40, 40);
            using (GraphicsPath path = UiDrawing.RoundedRect(icon, 10))
            using (LinearGradientBrush brush = new LinearGradientBrush(icon, UiTheme.Blue, UiTheme.Purple, 135f))
            {
                g.FillPath(brush, path);
                using (SolidBrush shine = new SolidBrush(Color.FromArgb(90, Color.White)))
                {
                    g.FillEllipse(shine, icon.Left + 7, icon.Top + 7, 14, 14);
                }
            }

            // Title "codexU" — 28px, clipped to safe area
            int titleX = 60;
            int titleW = titleMaxX - titleX;
            using (Font title = UiTheme.Font(28f, FontStyle.Bold))
            using (SolidBrush textBrush = new SolidBrush(UiTheme.Text))
            {
                // Measure to ensure we don't draw beyond titleMaxX
                SizeF titleSize = g.MeasureString("codexU", title);
                int drawW = Math.Min(titleW, (int)Math.Ceiling(titleSize.Width));
                Rectangle titleRect = new Rectangle(titleX, 3, drawW, 34);
                g.DrawString("codexU", title, textBrush, titleRect);
            }

            // Refresh time below title — 11px
            using (Font sub = UiTheme.Font(11f, FontStyle.Regular))
            using (SolidBrush muted = new SolidBrush(UiTheme.Muted))
            {
                string refreshed = UiLabels.RefreshedPrefix + snapshot.RefreshedAt.ToString("HH:mm", CultureInfo.CurrentCulture);
                g.DrawString(refreshed, sub, muted, 62, 36);
            }
        }
    }

    internal sealed class DashboardSurface : Control
    {
        private UsageSnapshot snapshot = new UsageSnapshot();

        public DashboardSurface()
        {
            SetStyle(ControlStyles.UserPaint |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);
            // Opaque match to window base
            BackColor = Color.FromArgb(190, 220, 230);
        }

        public void SetSnapshot(UsageSnapshot value)
        {
            snapshot = value ?? new UsageSnapshot();
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            // Fill entire control background first — prevents transparent gaps
            e.Graphics.FillRectangle(new SolidBrush(Color.FromArgb(240, 190, 220, 230)), ClientRectangle);

            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            // Top panel: quota rings + token cards + value progress
            Rectangle top = new Rectangle(0, 0, Width, 255);
            // Bottom panel: task board
            Rectangle tasks = new Rectangle(0, 268, Width, 210);

            UiDrawing.DrawPanel(g, top, 20);
            UiDrawing.DrawPanel(g, tasks, 20);

            // Quota rings: left column, 185px wide
            QuotaRings.Draw(g, new Rectangle(22, 18, 185, 195), snapshot);
            // Token cards: right column, 3 cards spanning remaining width
            int tcLeft = 224;
            TokenSummaryCards.Draw(g, new Rectangle(tcLeft, 20, Width - tcLeft - 20, 140), snapshot);
            // Value progress: right column, below token cards
            ValueProgressCard.Draw(g, new Rectangle(tcLeft, 174, Width - tcLeft - 20, 60), snapshot);

            // Task board
            TaskBoardRenderer.Draw(g, tasks, snapshot);

            // Status footer
            StatusFooter.Draw(g, new Rectangle(12, Height - 22, Width - 24, 22), snapshot);
        }
    }

    // Custom-drawn pill button — inherits Control (NOT Button) to avoid WinForms
    // native rendering artifacts (black backgrounds, focus rects, theme interference).
    internal sealed class HeaderPillButton : Control
    {
        private bool hovering;
        private bool pressed;
        private string label;

        public HeaderPillButton(string text)
        {
            label = text ?? "";
            // Safe style flags only — no Selectable, no SupportsTransparentBackColor, no UpdateStyles
            SetStyle(ControlStyles.UserPaint |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);
            TabStop = false;
            Cursor = Cursors.Hand;
            Font = UiTheme.Font(10f, FontStyle.Bold);
        }

        public string Label
        {
            get { return label; }
            set { label = value ?? ""; Invalidate(); }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);

            // Background: opaque light fill, never black/transparent
            Color fill;
            if (!Enabled) fill = Color.FromArgb(180, 228, 235, 240);
            else if (pressed) fill = Color.FromArgb(245, 255, 255, 255);
            else if (hovering) fill = Color.FromArgb(235, 255, 255, 255);
            else fill = Color.FromArgb(210, 245, 250, 252);

            using (GraphicsPath path = UiDrawing.RoundedRect(rect, 12))
            using (SolidBrush brush = new SolidBrush(fill))
            using (Pen pen = new Pen(Color.FromArgb(120, 255, 255, 255)))
            {
                g.FillPath(brush, path);
                g.DrawPath(pen, path);
            }

            // Text - centered, single line
            TextRenderer.DrawText(g, label, Font, rect,
                Enabled ? UiTheme.Text : UiTheme.Muted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix |
                TextFormatFlags.SingleLine);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            hovering = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            hovering = false;
            pressed = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) { pressed = true; Invalidate(); }
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (pressed) { pressed = false; Invalidate(); OnClick(e); }
            base.OnMouseUp(e);
        }

        // Expose Click as a public event (inherited from Control, just re-expose cleanly)
    }

    internal static class QuotaRings
    {
        private static readonly Color RingTrackColor = Color.FromArgb(46, 148, 163, 184); // rgba(148,163,184,0.18)

        public static void Draw(Graphics g, Rectangle bounds, UsageSnapshot snapshot)
        {
            // Outer ring: 150x150, stroke 10
            Rectangle ring = new Rectangle(bounds.Left + (bounds.Width - 150) / 2, bounds.Top + 6, 150, 150);
            DrawGradientRing2(g, ring, 10, snapshot.PrimaryRemainingPercent, UiTheme.Blue, UiTheme.BlueLight, RingTrackColor);

            // Inner ring: 112x112, stroke 10
            Rectangle inner = new Rectangle(ring.Left + 19, ring.Top + 19, 112, 112);
            DrawGradientRing2(g, inner, 10, snapshot.SecondaryRemainingPercent, UiTheme.Teal, UiTheme.Purple, RingTrackColor);

            // Center text: shifted right 2px, value 24px, rows slightly further apart
            int centerX = ring.Left + ring.Width / 2 + 2;   // +2px right shift
            int centerY = ring.Top + ring.Height / 2;
            int row1Y = centerY - 30;   // first row
            int row2Y = centerY + 6;    // second row (8px more gap)

            using (Font labelFont = UiTheme.Font(13f, FontStyle.Bold))
            using (Font valueFont = UiTheme.Font(24f, FontStyle.Bold))
            using (SolidBrush blue = new SolidBrush(UiTheme.Blue))
            using (SolidBrush teal = new SolidBrush(UiTheme.Teal))
            using (SolidBrush textBrush = new SolidBrush(UiTheme.Text))
            {
                string five = UiFormat.Percent(snapshot.PrimaryRemainingPercent);
                string seven = UiFormat.Percent(snapshot.SecondaryRemainingPercent);

                // Row 1: "5h" + "X%"
                Rectangle label1 = new Rectangle(centerX - 44, row1Y, 28, 28);
                Rectangle value1 = new Rectangle(centerX - 14, row1Y, 58, 28);
                TextRenderer.DrawText(g, "5h", labelFont, label1, UiTheme.Blue,
                    TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
                TextRenderer.DrawText(g, five, valueFont, value1, UiTheme.Text,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);

                // Row 2: "7d" + "Y%"
                Rectangle label2 = new Rectangle(centerX - 44, row2Y, 28, 28);
                Rectangle value2 = new Rectangle(centerX - 14, row2Y, 58, 28);
                TextRenderer.DrawText(g, "7d", labelFont, label2, UiTheme.Teal,
                    TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
                TextRenderer.DrawText(g, seven, valueFont, value2, UiTheme.Text,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
            }

            // Reset time lines below the ring
            using (Font label = UiTheme.Font(9f, FontStyle.Bold))
            using (SolidBrush text = new SolidBrush(UiTheme.MutedStrong))
            using (SolidBrush blue = new SolidBrush(UiTheme.Blue))
            using (SolidBrush teal = new SolidBrush(UiTheme.Teal))
            {
                // Reset info area: 150px wide, aligned to the ring
                int resetLeft = ring.Left;
                int resetWidth = 150;
                int y = bounds.Top + 166;
                g.FillEllipse(blue, resetLeft + 4, y + 6, 7, 7);
                g.DrawString(UiLabels.Reset5h, label, text, resetLeft + 16, y);
                SizeF firstSize = g.MeasureString(UiFormat.ResetTime(snapshot.PrimaryResetsAt), label);
                g.DrawString(UiFormat.ResetTime(snapshot.PrimaryResetsAt), label, text,
                    resetLeft + resetWidth - firstSize.Width - 4, y);
                y += 24;
                g.FillEllipse(teal, resetLeft + 4, y + 6, 7, 7);
                g.DrawString(UiLabels.Reset7d, label, text, resetLeft + 16, y);
                SizeF secondSize = g.MeasureString(UiFormat.ResetTime(snapshot.SecondaryResetsAt), label);
                g.DrawString(UiFormat.ResetTime(snapshot.SecondaryResetsAt), label, text,
                    resetLeft + resetWidth - secondSize.Width - 4, y);
            }
        }

        // Custom ring draw with configurable track color (lighter track)
        private static void DrawGradientRing2(Graphics g, Rectangle rect, int width, double? percent,
            Color start, Color end, Color trackColor)
        {
            float sweep = (float)(360.0 * Math.Max(0, Math.Min(100, percent.HasValue ? percent.Value : 0)) / 100.0);
            using (Pen track = new Pen(trackColor, width))
            using (Pen ring = new Pen(start, width))
            {
                track.StartCap = LineCap.Round;
                track.EndCap = LineCap.Round;
                ring.StartCap = LineCap.Round;
                ring.EndCap = LineCap.Round;
                g.DrawArc(track, rect, -90, 360);
                using (LinearGradientBrush brush = new LinearGradientBrush(rect, start, end, 135f))
                {
                    ring.Brush = brush;
                    g.DrawArc(ring, rect, -90, sweep);
                }
            }
        }
    }

    internal static class TokenSummaryCards
    {
        public static void Draw(Graphics g, Rectangle bounds, UsageSnapshot snapshot)
        {
            int gap = 10;
            int cardW = (bounds.Width - gap * 2) / 3;
            DrawCard(g, new Rectangle(bounds.Left, bounds.Top, cardW, bounds.Height), UiLabels.Today, snapshot.TodayUsage);
            DrawCard(g, new Rectangle(bounds.Left + cardW + gap, bounds.Top, cardW, bounds.Height), UiLabels.Last7Days, snapshot.SevenDayUsage);
            DrawCard(g, new Rectangle(bounds.Left + (cardW + gap) * 2, bounds.Top, cardW, bounds.Height), UiLabels.Lifetime, snapshot.LifetimeUsage);
        }

        private static void DrawCard(Graphics g, Rectangle rect, string title, TokenUsageSummary usage)
        {
            UiDrawing.DrawCard(g, rect, 14);
            int pad = 11;
            using (Font titleFont = UiTheme.Font(14f, FontStyle.Bold))
            using (Font numberFont = UiTheme.Font(26f, FontStyle.Bold))
            using (Font smallFont = UiTheme.Font(11f, FontStyle.Bold))
            using (SolidBrush text = new SolidBrush(UiTheme.Text))
            using (SolidBrush muted = new SolidBrush(UiTheme.Muted))
            {
                // Title + cost on same row
                g.DrawString(title, titleFont, text, rect.Left + pad, rect.Top + 10);
                string cost = UiFormat.Usd(usage.EstimatedCostUSD);
                SizeF costSize = g.MeasureString(cost, smallFont);
                g.DrawString(cost, smallFont, text, rect.Right - costSize.Width - pad, rect.Top + 12);

                // Main number — 26px, line-height 1.05
                g.DrawString(UiFormat.Tokens(usage.Tokens.VisibleTotal), numberFont, text,
                    rect.Left + pad, rect.Top + 32);
            }

            // Progress bar — 7px tall
            Rectangle bar = new Rectangle(rect.Left + pad, rect.Top + 76, rect.Width - pad * 2, 7);
            RoundedProgressBar.Draw(g, bar, 1.0, UiTheme.Blue, UiTheme.Purple, UiTheme.Orange);

            // Token detail rows — 17px line height
            int y = rect.Top + 89;
            DrawLegend(g, rect.Left + pad + 2, y, UiTheme.Blue, UiLabels.Uncached,
                UiFormat.Tokens(usage.Tokens.UncachedInput), rect.Width - pad * 2 - 4);
            DrawLegend(g, rect.Left + pad + 2, y + 17, UiTheme.Purple, UiLabels.Cached,
                UiFormat.Tokens(usage.Tokens.BillableCachedInput), rect.Width - pad * 2 - 4);
            DrawLegend(g, rect.Left + pad + 2, y + 34, UiTheme.Orange, UiLabels.Output,
                UiFormat.Tokens(usage.Tokens.Output), rect.Width - pad * 2 - 4);
        }

        private static void DrawLegend(Graphics g, int x, int y, Color color, string label, string value, int width)
        {
            using (Font font = UiTheme.Font(11f, FontStyle.Bold))
            using (SolidBrush dot = new SolidBrush(color))
            using (SolidBrush text = new SolidBrush(UiTheme.Muted))
            {
                g.FillEllipse(dot, x, y + 4, 7, 7);
                g.DrawString(label, font, text, x + 12, y);
                SizeF size = g.MeasureString(value, font);
                g.DrawString(value, font, text, x + width - size.Width, y);
            }
        }
    }

    internal static class ValueProgressCard
    {
        private const double Plus = 20.0;
        private const double Pro100 = 100.0;
        private const double Pro200 = 200.0;
        private const double Max = 2000.0;

        public static void Draw(Graphics g, Rectangle rect, UsageSnapshot snapshot)
        {
            UiDrawing.DrawCard(g, rect, 14);
            using (Font title = UiTheme.Font(14f, FontStyle.Bold))
            using (Font value = UiTheme.Font(22f, FontStyle.Bold))    // reduced for breathing room
            using (Font small = UiTheme.Font(10f, FontStyle.Bold))
            using (SolidBrush text = new SolidBrush(UiTheme.Text))
            using (SolidBrush muted = new SolidBrush(Color.FromArgb(71, 85, 105)))  // #475569
            {
                // Title row — 24px tall
                g.DrawString(UiLabels.ValueProgress, title, text, rect.Left + 14, rect.Top + 8);
                string money = UiFormat.Usd(snapshot.MonthEstimatedCost) + " / $2.0K";
                SizeF moneySize = g.MeasureString(money, value);
                g.DrawString(money, value, text, rect.Right - moneySize.Width - 14, rect.Top + 5);

                // Progress bar — more gap from title
                Rectangle bar = new Rectangle(rect.Left + 14, rect.Top + 32, rect.Width - 28, 7);
                RoundedProgressBar.Draw(g, bar, Math.Min(1.0, snapshot.MonthEstimatedCost / Max),
                    UiTheme.Blue, UiTheme.Purple, UiTheme.Teal);

                // Tick row — 6px gap from bar
                DrawGridTicks(g, new Rectangle(rect.Left + 14, rect.Top + 45, rect.Width - 28, 14), small, muted);
            }
        }

        private static void DrawGridTicks(Graphics g, Rectangle rect, Font font, Brush brush)
        {
            // 4-column even grid — no overlap
            string[] labels = { UiLabels.Plus, UiLabels.Pro100, UiLabels.Pro200, UiLabels.Max };
            Color[] colors = { UiTheme.Blue, UiTheme.Purple, UiTheme.BlueLight, UiTheme.GrayDot };
            int colWidth = rect.Width / labels.Length;
            for (int i = 0; i < labels.Length; i++)
            {
                int centerX = rect.Left + colWidth * i + colWidth / 2;
                using (SolidBrush dot = new SolidBrush(colors[i]))
                {
                    g.FillEllipse(dot, centerX - 3, rect.Top + 1, 5, 5);  // 5px dot
                }
                SizeF labelSize = g.MeasureString(labels[i], font);
                g.DrawString(labels[i], font, brush, centerX - labelSize.Width / 2, rect.Top + 8);
            }
        }
    }

    internal static class TaskBoardRenderer
    {
        public static void Draw(Graphics g, Rectangle panel, UsageSnapshot snapshot)
        {
            // Title bar: 28px tall, title 20px font
            using (Font title = UiTheme.Font(20f, FontStyle.Bold))
            using (Font small = UiTheme.Font(12f, FontStyle.Bold))
            using (SolidBrush text = new SolidBrush(UiTheme.Text))
            using (SolidBrush muted = new SolidBrush(UiTheme.Muted))
            {
                g.DrawString(UiLabels.TaskBoard, title, text, panel.Left + 14, panel.Top + 12);
                int total = snapshot.ActiveTasks + snapshot.PendingTasks + snapshot.ScheduledTasks + snapshot.DoneTasks;
                string right = total.ToString(CultureInfo.InvariantCulture) + " 事项 · " + DateTime.Now.ToString("HH:mm", CultureInfo.CurrentCulture);
                SizeF size = g.MeasureString(right, small);
                g.DrawString(right, small, text, panel.Right - size.Width - 16, panel.Top + 14);
            }

            // Columns start at panel.Top + 38, height ~150px
            int top = panel.Top + 38;
            int gap = 10;
            int colW = (panel.Width - 28 - gap * 3) / 4;
            int colH = panel.Height - 50;
            DrawColumn(g, new Rectangle(panel.Left + 14, top, colW, colH), UiLabels.Active, snapshot.ActiveTasks, UiTheme.Orange, snapshot.Tasks, TaskColumnKind.Active);
            DrawColumn(g, new Rectangle(panel.Left + 14 + (colW + gap), top, colW, colH), UiLabels.Pending, snapshot.PendingTasks, UiTheme.GrayDot, snapshot.Tasks, TaskColumnKind.Pending);
            DrawColumn(g, new Rectangle(panel.Left + 14 + (colW + gap) * 2, top, colW, colH), UiLabels.Scheduled, snapshot.ScheduledTasks, UiTheme.Purple, snapshot.Tasks, TaskColumnKind.Scheduled);
            DrawColumn(g, new Rectangle(panel.Left + 14 + (colW + gap) * 3, top, colW, colH), UiLabels.Done, snapshot.DoneTasks, UiTheme.Green, snapshot.Tasks, TaskColumnKind.Done);
        }

        private static void DrawColumn(Graphics g, Rectangle rect, string title, int count, Color accent,
            List<MiniTaskSnapshot> tasks, TaskColumnKind kind)
        {
            UiDrawing.DrawSoftColumn(g, rect, accent, 12);
            // Column header
            using (Font titleFont = UiTheme.Font(10f, FontStyle.Bold))
            using (SolidBrush text = new SolidBrush(UiTheme.Text))
            using (SolidBrush dot = new SolidBrush(accent))
            {
                g.FillEllipse(dot, rect.Left + 10, rect.Top + 13, 6, 6);
                g.DrawString(title + "  " + count.ToString(CultureInfo.InvariantCulture), titleFont, text,
                    rect.Left + 22, rect.Top + 8);
            }

            // Task cards — 68px each, 6px gap
            int y = rect.Top + 30;
            int drawn = 0;
            foreach (MiniTaskSnapshot task in tasks)
            {
                if (task.Kind != kind) continue;
                MiniTaskCard.Draw(g, new Rectangle(rect.Left + 8, y, rect.Width - 16, 68), task, accent);
                y += 74;
                drawn++;
                if (drawn >= 2) break;
            }
            if (drawn == 0)
            {
                using (Font font = UiTheme.Font(12f, FontStyle.Bold))
                using (SolidBrush muted = new SolidBrush(UiTheme.Muted))
                {
                    string empty = UiLabels.Empty;
                    SizeF size = g.MeasureString(empty, font);
                    g.DrawString(empty, font, muted,
                        rect.Left + rect.Width / 2 - size.Width / 2,
                        rect.Top + rect.Height / 2 - size.Height / 2);
                }
            }
        }
    }

    internal static class MiniTaskCard
    {
        public static void Draw(Graphics g, Rectangle rect, MiniTaskSnapshot task, Color accent)
        {
            UiDrawing.DrawCard(g, rect, 9);
            using (Font code = UiTheme.Font(11f, FontStyle.Bold))
            using (Font title = UiTheme.Font(12f, FontStyle.Bold))
            using (Font small = UiTheme.Font(10f, FontStyle.Bold))
            using (SolidBrush text = new SolidBrush(UiTheme.Text))
            using (SolidBrush muted = new SolidBrush(UiTheme.Muted))
            {
                // ID code + relative time on same row
                g.DrawString(task.Code, code, muted, rect.Left + 9, rect.Top + 7);
                string age = UiFormat.RelativeTime(task.UpdatedAt);
                SizeF ageSize = g.MeasureString(age, small);
                g.DrawString(age, small, muted, rect.Right - ageSize.Width - 8, rect.Top + 8);

                // Title — single line, no overlap with chip
                TextRenderer.DrawText(g, task.Title, title,
                    new Rectangle(rect.Left + 9, rect.Top + 26, rect.Width - 18, 20),
                    UiTheme.Text,
                    TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix |
                    TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter);

                // Chip at bottom
                DrawChip(g, new Rectangle(rect.Left + 9, rect.Bottom - 24, 60, 17), task.Chip, accent);
            }
        }

        private static void DrawChip(Graphics g, Rectangle rect, string text, Color accent)
        {
            using (GraphicsPath path = UiDrawing.RoundedRect(rect, 8))
            using (SolidBrush fill = new SolidBrush(Color.FromArgb(36, accent)))
            using (SolidBrush fore = new SolidBrush(accent))
            using (Font font = UiTheme.Font(10f, FontStyle.Bold))
            {
                g.FillPath(fill, path);
                TextRenderer.DrawText(g, text, font, rect, accent,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            }
        }
    }

    internal static class StatusFooter
    {
        public static void Draw(Graphics g, Rectangle rect, UsageSnapshot snapshot)
        {
            using (Font font = UiTheme.Font(11f, FontStyle.Bold))
            {
                string left = snapshot.Messages.Count == 0 ? UiLabels.LocalReady : snapshot.Messages[snapshot.Messages.Count - 1];
                // Left-aligned status message
                TextRenderer.DrawText(g, left, font,
                    new Rectangle(rect.Left + 14, rect.Top, rect.Width - 230, rect.Height),
                    Color.FromArgb(180, 71, 85, 105),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

                // Right-aligned "刷新 HH:mm xU"
                string right = UiLabels.RefreshedPrefix + snapshot.RefreshedAt.ToString("HH:mm", CultureInfo.CurrentCulture) + "   xU";
                TextRenderer.DrawText(g, right, font,
                    new Rectangle(rect.Right - 180, rect.Top, 168, rect.Height),
                    Color.FromArgb(180, 71, 85, 105),   // darker, more readable
                    TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            }
        }
    }

    internal static class RoundedProgressBar
    {
        public static void Draw(Graphics g, Rectangle rect, double percent, Color start, Color mid, Color end)
        {
            percent = Math.Max(0, Math.Min(1, percent));
            using (GraphicsPath trackPath = UiDrawing.RoundedRect(rect, rect.Height / 2))
            using (SolidBrush track = new SolidBrush(Color.FromArgb(78, 148, 163, 175)))
            {
                g.FillPath(track, trackPath);
            }
            if (percent <= 0) return;
            Rectangle fillRect = new Rectangle(rect.Left, rect.Top, Math.Max(rect.Height, (int)Math.Round(rect.Width * percent)), rect.Height);
            using (GraphicsPath fillPath = UiDrawing.RoundedRect(fillRect, rect.Height / 2))
            using (LinearGradientBrush brush = new LinearGradientBrush(fillRect, start, end, 0f))
            {
                ColorBlend blend = new ColorBlend();
                blend.Positions = new float[] { 0f, 0.72f, 1f };
                blend.Colors = new Color[] { start, mid, end };
                brush.InterpolationColors = blend;
                g.FillPath(brush, fillPath);
            }
        }
    }

    internal static class UiDrawing
    {
        public static GraphicsPath RoundedRect(Rectangle rect, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(rect.Left, rect.Top, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Top, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.Left, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static void DrawPanel(Graphics g, Rectangle rect, int radius)
        {
            // Opaque panel — blocks desktop content behind it
            using (GraphicsPath path = RoundedRect(rect, radius))
            using (SolidBrush fill = new SolidBrush(Color.FromArgb(190, 235, 248, 252)))
            using (Pen border = new Pen(Color.FromArgb(100, 255, 255, 255)))
            {
                g.FillPath(fill, path);
                g.DrawPath(border, path);
            }
        }

        public static void DrawCard(Graphics g, Rectangle rect, int radius)
        {
            // Opaque card — blocks desktop content
            using (GraphicsPath path = RoundedRect(rect, radius))
            using (SolidBrush fill = new SolidBrush(Color.FromArgb(215, 248, 250, 252)))
            using (Pen border = new Pen(Color.FromArgb(100, 255, 255, 255)))
            {
                g.FillPath(fill, path);
                g.DrawPath(border, path);
            }
        }

        public static void DrawSoftColumn(Graphics g, Rectangle rect, Color accent, int radius)
        {
            // Column background — lightly tinted but opaque white base
            using (GraphicsPath path = RoundedRect(rect, radius))
            {
                // White base for opacity
                using (SolidBrush baseFill = new SolidBrush(Color.FromArgb(200, 248, 252, 254)))
                {
                    g.FillPath(baseFill, path);
                }
                // Accent tint overlay
                using (SolidBrush accentFill = new SolidBrush(Color.FromArgb(45, accent)))
                {
                    g.FillPath(accentFill, path);
                }
                using (Pen border = new Pen(Color.FromArgb(60, accent)))
                {
                    g.DrawPath(border, path);
                }
            }
        }

        public static void DrawGradientRing(Graphics g, Rectangle rect, int width, double? percent, Color start, Color end)
        {
            float sweep = (float)(360.0 * Math.Max(0, Math.Min(100, percent.HasValue ? percent.Value : 0)) / 100.0);
            using (Pen track = new Pen(Color.FromArgb(50, UiTheme.GrayDot), width))
            using (Pen ring = new Pen(start, width))
            {
                track.StartCap = LineCap.Round;
                track.EndCap = LineCap.Round;
                ring.StartCap = LineCap.Round;
                ring.EndCap = LineCap.Round;
                g.DrawArc(track, rect, -90, 360);
                using (LinearGradientBrush brush = new LinearGradientBrush(rect, start, end, 135f))
                {
                    ring.Brush = brush;
                    g.DrawArc(ring, rect, -90, sweep);
                }
            }
        }
    }

    internal static class UiTheme
    {
        public static readonly Color WindowBase = Color.FromArgb(240, 190, 220, 230);  // near-opaque
        public static readonly Color Text = Color.FromArgb(15, 23, 42);
        public static readonly Color Muted = Color.FromArgb(100, 116, 139);
        public static readonly Color MutedStrong = Color.FromArgb(71, 85, 105);
        public static readonly Color WeakText = Color.FromArgb(148, 163, 184);
        public static readonly Color Blue = Color.FromArgb(59, 130, 246);
        public static readonly Color BlueLight = Color.FromArgb(125, 156, 255);
        public static readonly Color Purple = Color.FromArgb(139, 92, 246);
        public static readonly Color Teal = Color.FromArgb(20, 184, 166);
        public static readonly Color Orange = Color.FromArgb(245, 158, 11);
        public static readonly Color Green = Color.FromArgb(34, 197, 94);
        public static readonly Color Red = Color.FromArgb(239, 68, 68);
        public static readonly Color GrayDot = Color.FromArgb(148, 163, 184);
        // More opaque card/button colors for WinForms stability
        public static readonly Color CardFill = Color.FromArgb(200, 245, 250, 252);
        public static readonly Color CardFillHover = Color.FromArgb(225, 248, 252, 254);
        public static readonly Color ButtonFill = Color.FromArgb(210, 245, 250, 252);
        public static readonly Color ButtonFillHover = Color.FromArgb(235, 255, 255, 255);
        public static readonly Color ButtonFillActive = Color.FromArgb(245, 255, 255, 255);
        public static readonly Color ButtonFillDisabled = Color.FromArgb(170, 228, 235, 240);

        public static Font Font(float size, FontStyle style)
        {
            return new Font(SystemFonts.MessageBoxFont.FontFamily, size, style, GraphicsUnit.Point);
        }
    }

    internal static class UiLabels
    {
        public const string Loading = "正在读取 codexU 数据";
        public const string RefreshedPrefix = "刷新 ";
        public const string Reset5h = "5h 重置";
        public const string Reset7d = "7d 重置";
        public const string Today = "今日";
        public const string Last7Days = "近 7 天";
        public const string Lifetime = "累计";
        public const string Uncached = "未缓存";
        public const string Cached = "缓存";
        public const string Output = "输出";
        public const string ValueProgress = "羊毛进度";
        public const string Plus = "Plus";
        public const string Pro100 = "Pro100";
        public const string Pro200 = "Pro200";
        public const string Max = "Max";
        public const string TaskBoard = "今日任务看板";
        public const string Active = "进行中";
        public const string Pending = "待处理";
        public const string Scheduled = "定时";
        public const string Done = "完成";
        public const string Empty = "暂无";
        public const string LocalReady = "本地读取完成";
    }

    internal static class UiFormat
    {
        public static string Percent(double? value)
        {
            if (!value.HasValue) return "--";
            if (value.Value > 0 && value.Value < 1) return "<1%";
            return Math.Round(value.Value).ToString(CultureInfo.InvariantCulture) + "%";
        }

        public static string ResetTime(DateTime? value)
        {
            if (!value.HasValue) return "--";
            return value.Value.ToLocalTime().ToString("M/d HH:mm", CultureInfo.CurrentCulture);
        }

        public static string Tokens(long value)
        {
            double abs = Math.Abs((double)value);
            if (abs >= 1000000000) return (value / 1000000000.0).ToString("0.0", CultureInfo.InvariantCulture) + "B";
            if (abs >= 1000000) return (value / 1000000.0).ToString("0.0", CultureInfo.InvariantCulture) + "M";
            if (abs >= 1000) return (value / 1000.0).ToString("0.0", CultureInfo.InvariantCulture) + "K";
            return value.ToString(CultureInfo.InvariantCulture);
        }

        public static string Usd(double value)
        {
            double abs = Math.Abs(value);
            if (abs >= 1000000) return "$" + (value / 1000000.0).ToString("0.0", CultureInfo.InvariantCulture) + "M";
            if (abs >= 1000) return "$" + (value / 1000.0).ToString("0.0", CultureInfo.InvariantCulture) + "K";
            return "$" + value.ToString(abs >= 100 ? "0" : "0.00", CultureInfo.InvariantCulture);
        }

        public static string RelativeTime(DateTime? value)
        {
            if (!value.HasValue) return "--";
            TimeSpan age = DateTime.Now - value.Value.ToLocalTime();
            if (age.TotalMinutes < 1) return "刚刚";
            if (age.TotalMinutes < 60) return ((int)age.TotalMinutes).ToString(CultureInfo.InvariantCulture) + " 分钟前";
            if (age.TotalHours < 24) return ((int)age.TotalHours).ToString(CultureInfo.InvariantCulture) + " 小时前";
            return ((int)age.TotalDays).ToString(CultureInfo.InvariantCulture) + " 天前";
        }

        public static string PlanLabel(string rawPlan)
        {
            string plan = (rawPlan ?? "").ToUpperInvariant();
            if (plan.Contains("PLUS")) return "PLUS";
            if (plan.Contains("PRO")) return "PRO";
            if (plan.Contains("TEAM")) return "PRO";
            return "PLUS";
        }
    }
    internal sealed class UsageSnapshot
    {
        public DateTime RefreshedAt = DateTime.Now;
        public string AccountPlan = "";
        public double? PrimaryRemainingPercent;
        public double? SecondaryRemainingPercent;
        public DateTime? PrimaryResetsAt;
        public DateTime? SecondaryResetsAt;
        public long TodayTokens;
        public long SevenDayTokens;
        public long LifetimeTokens;
        public double MonthEstimatedCost;
        public int ActiveTasks;
        public int PendingTasks;
        public int ScheduledTasks;
        public int DoneTasks;
        public TokenUsageSummary TodayUsage = new TokenUsageSummary();
        public TokenUsageSummary SevenDayUsage = new TokenUsageSummary();
        public TokenUsageSummary LifetimeUsage = new TokenUsageSummary();
        public TokenUsageSummary MonthUsage = new TokenUsageSummary();
        public readonly List<MiniTaskSnapshot> Tasks = new List<MiniTaskSnapshot>();
        public readonly List<string> Messages = new List<string>();

        public string ToJson()
        {
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            Dictionary<string, object> root = new Dictionary<string, object>();
            root["refreshedAt"] = RefreshedAt.ToString("o", CultureInfo.InvariantCulture);
            root["accountPlan"] = AccountPlan;
            root["primaryRemainingPercent"] = PrimaryRemainingPercent.HasValue ? (object)PrimaryRemainingPercent.Value : null;
            root["secondaryRemainingPercent"] = SecondaryRemainingPercent.HasValue ? (object)SecondaryRemainingPercent.Value : null;
            root["primaryResetsAt"] = PrimaryResetsAt.HasValue ? (object)PrimaryResetsAt.Value.ToString("o", CultureInfo.InvariantCulture) : null;
            root["secondaryResetsAt"] = SecondaryResetsAt.HasValue ? (object)SecondaryResetsAt.Value.ToString("o", CultureInfo.InvariantCulture) : null;
            root["todayTokens"] = TodayTokens;
            root["sevenDayTokens"] = SevenDayTokens;
            root["lifetimeTokens"] = LifetimeTokens;
            root["monthEstimatedCost"] = MonthEstimatedCost;
            root["todayEstimatedCost"] = TodayUsage.EstimatedCostUSD;
            root["sevenDayEstimatedCost"] = SevenDayUsage.EstimatedCostUSD;
            root["lifetimeEstimatedCost"] = LifetimeUsage.EstimatedCostUSD;
            root["tasks"] = new Dictionary<string, object>
            {
                { "active", ActiveTasks },
                { "pending", PendingTasks },
                { "scheduled", ScheduledTasks },
                { "done", DoneTasks }
            };
            root["messages"] = Messages;
            return serializer.Serialize(root);
        }
    }

    internal sealed class TokenUsageSummary
    {
        public TokenBreakdown Tokens;
        public double EstimatedCostUSD;

        public void Add(TokenBreakdown tokens, double cost)
        {
            Tokens.Add(tokens);
            EstimatedCostUSD += cost;
        }
    }

    internal enum TaskColumnKind
    {
        Active,
        Pending,
        Scheduled,
        Done
    }

    internal sealed class MiniTaskSnapshot
    {
        public TaskColumnKind Kind;
        public string Code = "COD-0000";
        public string Title = "鏆傛棤鏍囬";
        public string Detail = "";
        public string Chip = "Idle";
        public DateTime? UpdatedAt;
    }

    internal static class CodexUsageReader
    {
        private static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer();

        public static UsageSnapshot Load()
        {
            UsageSnapshot snapshot = new UsageSnapshot();
            ReadAppServer(snapshot);
            ReadLocalUsage(snapshot);
            ReadAutomations(snapshot);
            if (snapshot.TodayTokens == 0 && snapshot.LifetimeTokens == 0)
            {
                snapshot.Messages.Add("未找到可解析的 token_count session 日志");
            }
            return snapshot;
        }

        private static void ReadAppServer(UsageSnapshot snapshot)
        {
            string codexPath = FindExecutable("codex.exe");
            if (codexPath == null)
            {
                snapshot.Messages.Add("未找到 codex.exe");
                return;
            }

            Process process = new Process();
            process.StartInfo.FileName = codexPath;
            process.StartInfo.Arguments = "app-server";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardInput = true;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.StandardOutputEncoding = Encoding.UTF8;

            try { process.Start(); }
            catch
            {
                snapshot.Messages.Add("app-server 启动失败");
                return;
            }

            try
            {
                WriteJson(process, new Dictionary<string, object>
                {
                    { "id", 1 },
                    { "method", "initialize" },
                    { "params", new Dictionary<string, object>
                        {
                            { "clientInfo", new Dictionary<string, object>
                                {
                                    { "name", "codexu-win" },
                                    { "title", "codexU Windows" },
                                    { "version", "0.2.0" }
                                }
                            },
                            { "capabilities", new Dictionary<string, object>
                                {
                                    { "experimentalApi", true },
                                    { "optOutNotificationMethods", new object[0] }
                                }
                            }
                        }
                    }
                });

                Queue<string> outputLines = new Queue<string>();
                AutoResetEvent outputReady = new AutoResetEvent(false);
                Thread readerThread = new Thread(new ThreadStart(delegate
                {
                    try
                    {
                        string outputLine;
                        while ((outputLine = process.StandardOutput.ReadLine()) != null)
                        {
                            lock (outputLines) outputLines.Enqueue(outputLine);
                            outputReady.Set();
                        }
                    }
                    catch { }
                }));
                readerThread.IsBackground = true;
                readerThread.Start();

                DateTime deadline = DateTime.UtcNow.AddSeconds(12);
                bool sentRequests = false;
                HashSet<int> completed = new HashSet<int>();

                while (DateTime.UtcNow < deadline && completed.Count < 3)
                {
                    string line = ReadLineWithTimeout(outputLines, outputReady, 500);
                    if (line == null) continue;
                    Dictionary<string, object> obj = DeserializeObject(line);
                    if (obj == null || !obj.ContainsKey("id")) continue;
                    int id = Convert.ToInt32(obj["id"], CultureInfo.InvariantCulture);

                    if (id == 1 && !sentRequests)
                    {
                        sentRequests = true;
                        WriteJson(process, new Dictionary<string, object> { { "method", "initialized" } });
                        WriteJson(process, new Dictionary<string, object> { { "id", 2 }, { "method", "account/read" }, { "params", new Dictionary<string, object> { { "refreshToken", false } } } });
                        WriteJson(process, new Dictionary<string, object> { { "id", 3 }, { "method", "account/rateLimits/read" } });
                        WriteJson(process, new Dictionary<string, object> { { "id", 4 }, { "method", "account/usage/read" } });
                        continue;
                    }

                    if (obj.ContainsKey("error"))
                    {
                        snapshot.Messages.Add("app-server " + id.ToString(CultureInfo.InvariantCulture) + " 返回错误");
                        completed.Add(id);
                        continue;
                    }

                    Dictionary<string, object> result = obj.ContainsKey("result") ? obj["result"] as Dictionary<string, object> : null;
                    if (result == null)
                    {
                        completed.Add(id);
                        continue;
                    }

                    if (id == 2) ParseAccount(result, snapshot);
                    if (id == 3) ParseRateLimits(result, snapshot);
                    completed.Add(id);
                }

                if (completed.Count < 2) snapshot.Messages.Add("app-server 响应超时");
            }
            catch
            {
                snapshot.Messages.Add("app-server 读取失败");
            }
            finally
            {
                try { process.StandardInput.Close(); } catch { }
                try { if (!process.HasExited) process.Kill(); } catch { }
                process.Dispose();
            }
        }

        private static void ParseAccount(Dictionary<string, object> result, UsageSnapshot snapshot)
        {
            Dictionary<string, object> account = GetDict(result, "account");
            if (account == null) return;
            string type = GetString(account, "type");
            string plan = GetString(account, "planType");
            snapshot.AccountPlan = string.IsNullOrEmpty(plan) ? type : plan;
        }

        private static void ParseRateLimits(Dictionary<string, object> result, UsageSnapshot snapshot)
        {
            Dictionary<string, object> selected = null;
            Dictionary<string, object> byId = GetDict(result, "rateLimitsByLimitId");
            if (byId != null) selected = GetDict(byId, "codex");
            if (selected == null) selected = GetDict(result, "rateLimits");
            if (selected == null) return;

            ParseWindow(GetDict(selected, "primary"), out snapshot.PrimaryRemainingPercent, out snapshot.PrimaryResetsAt);
            ParseWindow(GetDict(selected, "secondary"), out snapshot.SecondaryRemainingPercent, out snapshot.SecondaryResetsAt);
        }

        private static void ParseWindow(Dictionary<string, object> window, out double? remaining, out DateTime? resetsAt)
        {
            remaining = null;
            resetsAt = null;
            if (window == null) return;

            double used;
            if (TryDouble(window, "usedPercent", out used) || TryDouble(window, "used_percent", out used))
            {
                remaining = Math.Max(0, Math.Min(100, 100 - used));
            }

            double unix;
            if (TryDouble(window, "resetsAt", out unix) || TryDouble(window, "resets_at", out unix))
            {
                resetsAt = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(unix);
            }
        }

        private static void ReadLocalUsage(UsageSnapshot snapshot)
        {
            string codexHome = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex");
            if (!Directory.Exists(codexHome))
            {
                snapshot.Messages.Add("未找到 %USERPROFILE%\\.codex");
                return;
            }

            string dbPath = FirstExistingPath(new string[]
            {
                Path.Combine(codexHome, "state_5.sqlite"),
                Path.Combine(codexHome, "sqlite", "state_5.sqlite")
            });
            if (dbPath == null)
            {
                snapshot.Messages.Add("未找到 Codex state_5.sqlite");
                return;
            }

            DateTime now = DateTime.Now;
            DateTime dayStart = now.Date;
            DateTime sevenDayStart = dayStart.AddDays(-6);
            DateTime monthStart = new DateTime(now.Year, now.Month, 1);
            long dayEpoch = ToUnixSeconds(dayStart);
            long sevenDayEpoch = ToUnixSeconds(sevenDayStart);
            long activeEpoch = ToUnixSeconds(now.AddHours(-2));

            try
            {
                List<Dictionary<string, object>> totals = SQLiteNative.Query(dbPath,
                    "SELECT " +
                    "COALESCE(SUM(tokens_used),0) AS lifetimeTokens, " +
                    "COALESCE(SUM(CASE WHEN updated_at >= " + dayEpoch + " THEN tokens_used ELSE 0 END),0) AS todayTokens, " +
                    "COALESCE(SUM(CASE WHEN updated_at >= " + sevenDayEpoch + " THEN tokens_used ELSE 0 END),0) AS sevenDayTokens " +
                    "FROM threads;");
                if (totals.Count > 0)
                {
                    snapshot.LifetimeTokens = GetLongAny(totals[0], new string[] { "lifetimeTokens" });
                    snapshot.TodayTokens = GetLongAny(totals[0], new string[] { "todayTokens" });
                    snapshot.SevenDayTokens = GetLongAny(totals[0], new string[] { "sevenDayTokens" });
                }

                List<Dictionary<string, object>> tasks = SQLiteNative.Query(dbPath,
                    "SELECT " +
                    "COALESCE(SUM(CASE WHEN archived = 0 AND recency_at >= " + activeEpoch + " THEN 1 ELSE 0 END),0) AS activeTasks, " +
                    "COALESCE(SUM(CASE WHEN archived = 0 AND recency_at < " + activeEpoch + " AND (updated_at >= " + dayEpoch + " OR recency_at >= " + dayEpoch + " OR created_at >= " + dayEpoch + ") THEN 1 ELSE 0 END),0) AS pendingTasks, " +
                    "COALESCE(SUM(CASE WHEN archived = 1 AND COALESCE(archived_at, updated_at) >= " + dayEpoch + " THEN 1 ELSE 0 END),0) AS doneTasks " +
                    "FROM threads;");
                if (tasks.Count > 0)
                {
                    snapshot.ActiveTasks = (int)GetLongAny(tasks[0], new string[] { "activeTasks" });
                    snapshot.PendingTasks = (int)GetLongAny(tasks[0], new string[] { "pendingTasks" });
                    snapshot.DoneTasks = (int)GetLongAny(tasks[0], new string[] { "doneTasks" });
                }

                ReadThreadTasks(dbPath, dayEpoch, activeEpoch, snapshot);
            }
            catch (Exception ex)
            {
                snapshot.Messages.Add("SQLite 查询失败: " + ex.Message);
            }

            List<SessionSource> sources = ReadSessionSources(dbPath, snapshot);
            if (sources.Count == 0)
            {
                snapshot.Messages.Add("未找到 Codex session 日志");
                return;
            }

            int parsedEvents = 0;
            int parsedFiles = 0;
            foreach (SessionSource source in sources)
            {
                if (!File.Exists(source.RolloutPath)) continue;
                bool fileHadTokenEvents = false;
                try
                {
                    TokenBreakdown previous = new TokenBreakdown();
                    using (FileStream stream = new FileStream(source.RolloutPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                    using (StreamReader reader = new StreamReader(stream, Encoding.UTF8, true))
                    {
                        string line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            if (line.IndexOf("\"type\":\"token_count\"", StringComparison.OrdinalIgnoreCase) < 0) continue;
                            TokenBreakdown current;
                            DateTime eventTime;
                            if (!TryParseTokenCount(line, out current, out eventTime)) continue;
                            TokenBreakdown delta = current.Delta(previous);
                            if (delta.HasNegativeValue) delta = current;
                            previous = current;
                            if (delta.IsZero) continue;

                            double cost = EstimateCost(delta, source.Model);
                            parsedEvents++;
                            fileHadTokenEvents = true;
                            snapshot.LifetimeUsage.Add(delta, cost);
                            if (eventTime >= dayStart) snapshot.TodayUsage.Add(delta, cost);
                            if (eventTime >= sevenDayStart) snapshot.SevenDayUsage.Add(delta, cost);
                            if (eventTime >= monthStart) snapshot.MonthUsage.Add(delta, cost);
                        }
                    }
                    if (fileHadTokenEvents) parsedFiles++;
                }
                catch
                {
                    snapshot.Messages.Add("读取 session 失败: " + Path.GetFileName(source.RolloutPath));
                }
            }

            if (parsedEvents > 0)
            {
                snapshot.TodayTokens = snapshot.TodayUsage.Tokens.VisibleTotal;
                snapshot.SevenDayTokens = snapshot.SevenDayUsage.Tokens.VisibleTotal;
                snapshot.LifetimeTokens = snapshot.LifetimeUsage.Tokens.VisibleTotal;
                snapshot.MonthEstimatedCost = snapshot.MonthUsage.EstimatedCostUSD;
                snapshot.Messages.Add("已解析 session " + parsedFiles.ToString(CultureInfo.InvariantCulture) + " 个，token_count 事件 " + parsedEvents.ToString(CultureInfo.InvariantCulture) + " 条");
            }
            else
            {
                snapshot.Messages.Add("未找到 Codex token_count 事件");
            }
        }

        private static void ReadThreadTasks(string dbPath, long dayEpoch, long activeEpoch, UsageSnapshot snapshot)
        {
            string todayQuery =
                "SELECT id, title, preview, cwd, tokens_used AS tokens, updated_at AS updatedAt, recency_at AS recencyAt, archived " +
                "FROM threads WHERE archived = 0 AND preview <> '' AND (updated_at >= " + dayEpoch + " OR recency_at >= " + dayEpoch + " OR created_at >= " + dayEpoch + ") " +
                "ORDER BY recency_at DESC, updated_at DESC LIMIT 12;";
            foreach (Dictionary<string, object> row in SQLiteNative.Query(dbPath, todayQuery))
            {
                long recency = GetLongAny(row, new string[] { "recencyAt", "updatedAt" });
                TaskColumnKind kind = recency >= activeEpoch ? TaskColumnKind.Active : TaskColumnKind.Pending;
                snapshot.Tasks.Add(MakeThreadTask(row, kind));
            }

            string doneQuery =
                "SELECT id, title, preview, cwd, tokens_used AS tokens, COALESCE(archived_at, updated_at) AS updatedAt, archived " +
                "FROM threads WHERE archived = 1 AND COALESCE(archived_at, updated_at) >= " + dayEpoch + " " +
                "ORDER BY COALESCE(archived_at, updated_at) DESC LIMIT 6;";
            foreach (Dictionary<string, object> row in SQLiteNative.Query(dbPath, doneQuery))
            {
                snapshot.Tasks.Add(MakeThreadTask(row, TaskColumnKind.Done));
            }
        }

        private static MiniTaskSnapshot MakeThreadTask(Dictionary<string, object> row, TaskColumnKind kind)
        {
            string id = GetString(row, "id");
            string title = FirstNonEmpty(GetString(row, "title"), GetString(row, "preview"), "Codex 会话");
            long tokens = GetLongAny(row, new string[] { "tokens" });
            long updated = GetLongAny(row, new string[] { "recencyAt", "updatedAt" });
            string compact = (id ?? "").Replace("-", "");
            if (compact.Length > 4) compact = compact.Substring(compact.Length - 4);
            MiniTaskSnapshot task = new MiniTaskSnapshot();
            task.Kind = kind;
            task.Code = "COD-" + compact.ToUpperInvariant();
            task.Title = title.Replace("\r", " ").Replace("\n", " ");
            task.Detail = UiFormat.Tokens(tokens);
            task.UpdatedAt = FromUnixSeconds(updated);
            task.Chip = kind == TaskColumnKind.Active ? (tokens >= 5000000 ? "High" : "Active")
                : kind == TaskColumnKind.Pending ? (tokens >= 2000000 ? "Medium" : "Idle")
                : "Done";
            return task;
        }

        private static List<SessionSource> ReadSessionSources(string dbPath, UsageSnapshot snapshot)
        {
            List<SessionSource> sources = new List<SessionSource>();
            try
            {
                List<Dictionary<string, object>> rows = SQLiteNative.Query(dbPath,
                    "SELECT rollout_path AS rolloutPath, model FROM threads " +
                    "WHERE rollout_path IS NOT NULL AND rollout_path <> '' AND tokens_used > 0 ORDER BY updated_at ASC;");
                HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (Dictionary<string, object> row in rows)
                {
                    string path = GetString(row, "rolloutPath");
                    if (path.Length == 0 || seen.Contains(path)) continue;
                    seen.Add(path);
                    sources.Add(new SessionSource(path, GetString(row, "model")));
                }
            }
            catch (Exception ex)
            {
                snapshot.Messages.Add("读取 session 索引失败: " + ex.Message);
            }
            return sources;
        }

        private static void ReadAutomations(UsageSnapshot snapshot)
        {
            string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex", "automations");
            if (!Directory.Exists(root)) return;
            try
            {
                string[] files = Directory.GetFiles(root, "automation.toml", SearchOption.AllDirectories);
                snapshot.ScheduledTasks += files.Length;
                foreach (string file in files)
                {
                    if (snapshot.Tasks.Count > 18) break;
                    MiniTaskSnapshot task = new MiniTaskSnapshot();
                    task.Kind = TaskColumnKind.Scheduled;
                    task.Code = "AUTO-" + Path.GetFileName(Path.GetDirectoryName(file)).Substring(0, Math.Min(4, Path.GetFileName(Path.GetDirectoryName(file)).Length)).ToUpperInvariant();
                    task.Title = Path.GetFileName(Path.GetDirectoryName(file));
                    task.Detail = "CRON";
                    task.Chip = "Cron";
                    task.UpdatedAt = File.GetLastWriteTime(file);
                    snapshot.Tasks.Add(task);
                }
            }
            catch
            {
                snapshot.Messages.Add("读取 automation 失败");
            }
        }

        private static bool TryParseTokenCount(string line, out TokenBreakdown tokens, out DateTime eventTime)
        {
            tokens = new TokenBreakdown();
            eventTime = DateTime.MinValue;
            Dictionary<string, object> root = DeserializeObject(line);
            if (root == null) return false;

            string timestamp = GetString(root, "timestamp");
            if (timestamp.Length == 0 || !TryParseIsoTime(timestamp, out eventTime)) return false;
            Dictionary<string, object> payload = GetDict(root, "payload");
            if (payload == null || GetString(payload, "type") != "token_count") return false;
            Dictionary<string, object> info = GetDict(payload, "info");
            if (info == null) return false;
            Dictionary<string, object> totalUsage = GetDict(info, "total_token_usage");
            if (totalUsage == null) return false;

            tokens.Input = GetLongAny(totalUsage, new string[] { "input_tokens", "inputTokens" });
            tokens.CachedInput = GetLongAny(totalUsage, new string[] { "cached_input_tokens", "cachedInputTokens" });
            tokens.Output = GetLongAny(totalUsage, new string[] { "output_tokens", "outputTokens" });
            tokens.ReasoningOutput = GetLongAny(totalUsage, new string[] { "reasoning_output_tokens", "reasoningOutputTokens" });
            tokens.Total = GetLongAny(totalUsage, new string[] { "total_tokens", "totalTokens" });
            if (tokens.Total == 0) tokens.Total = Math.Max(0, tokens.Input + tokens.Output);
            return tokens.Total > 0 || tokens.Input > 0 || tokens.Output > 0;
        }

        private static double EstimateCost(TokenBreakdown tokens, string model)
        {
            long cached = Math.Min(Math.Max(tokens.CachedInput, 0), Math.Max(tokens.Input, 0));
            long uncached = Math.Max(0, tokens.Input - cached);
            ModelTokenPrice price = ModelTokenPrice.For(model);
            return uncached / 1000000.0 * price.InputPerMillion
                + cached / 1000000.0 * price.CachedInputPerMillion
                + Math.Max(tokens.Output, 0) / 1000000.0 * price.OutputPerMillion;
        }

        private static string FirstExistingPath(string[] paths)
        {
            foreach (string path in paths)
            {
                if (File.Exists(path)) return path;
            }
            return null;
        }

        private static long ToUnixSeconds(DateTime value)
        {
            DateTime utc = value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
            return (long)(utc - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
        }

        private static DateTime? FromUnixSeconds(long value)
        {
            if (value <= 0) return null;
            return new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(value).ToLocalTime();
        }

        private static bool TryParseIsoTime(string value, out DateTime date)
        {
            return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out date);
        }

        private static string FindExecutable(string name)
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string[] candidates = new string[]
            {
                Path.Combine(localAppData, "Programs", "OpenAI", "Codex", "bin", name),
                Path.Combine(localAppData, "Programs", "codex", name)
            };
            foreach (string candidate in candidates)
            {
                if (File.Exists(candidate)) return candidate;
            }

            string path = Environment.GetEnvironmentVariable("PATH") ?? "";
            foreach (string part in path.Split(Path.PathSeparator))
            {
                try
                {
                    string candidate = Path.Combine(part.Trim(), name);
                    if (File.Exists(candidate)) return candidate;
                }
                catch { }
            }
            return null;
        }

        private static void WriteJson(Process process, Dictionary<string, object> request)
        {
            process.StandardInput.WriteLine(Serializer.Serialize(request));
            process.StandardInput.Flush();
        }

        private static string ReadLineWithTimeout(Queue<string> lines, AutoResetEvent ready, int timeoutMs)
        {
            lock (lines)
            {
                if (lines.Count > 0) return lines.Dequeue();
            }
            if (!ready.WaitOne(timeoutMs)) return null;
            lock (lines)
            {
                if (lines.Count > 0) return lines.Dequeue();
            }
            return null;
        }

        private static Dictionary<string, object> DeserializeObject(string json)
        {
            try { return Serializer.DeserializeObject(json) as Dictionary<string, object>; }
            catch { return null; }
        }

        private static Dictionary<string, object> GetDict(Dictionary<string, object> dict, string key)
        {
            if (dict == null || !dict.ContainsKey(key)) return null;
            return dict[key] as Dictionary<string, object>;
        }

        private static string GetString(Dictionary<string, object> dict, string key)
        {
            if (dict == null || !dict.ContainsKey(key) || dict[key] == null) return "";
            return Convert.ToString(dict[key], CultureInfo.InvariantCulture);
        }

        private static bool TryDouble(Dictionary<string, object> dict, string key, out double value)
        {
            value = 0;
            if (dict == null || !dict.ContainsKey(key) || dict[key] == null) return false;
            try
            {
                value = Convert.ToDouble(dict[key], CultureInfo.InvariantCulture);
                return true;
            }
            catch { return false; }
        }

        private static long GetLongAny(Dictionary<string, object> dict, string[] keys)
        {
            if (dict == null) return 0;
            foreach (string key in keys)
            {
                if (dict.ContainsKey(key) && dict[key] != null)
                {
                    try { return Convert.ToInt64(dict[key], CultureInfo.InvariantCulture); }
                    catch { }
                }
            }
            return 0;
        }

        private static string FirstNonEmpty(string a, string b, string fallback)
        {
            if (!string.IsNullOrEmpty(a)) return a;
            if (!string.IsNullOrEmpty(b)) return b;
            return fallback;
        }
    }

    internal struct TokenBreakdown
    {
        public long Input;
        public long CachedInput;
        public long Output;
        public long ReasoningOutput;
        public long Total;

        public long BillableCachedInput { get { return Math.Min(Math.Max(CachedInput, 0), Math.Max(Input, 0)); } }
        public long UncachedInput { get { return Math.Max(0, Input - BillableCachedInput); } }
        public long VisibleTotal { get { return Math.Max(Total, Input + Output); } }
        public bool IsZero { get { return Input == 0 && CachedInput == 0 && Output == 0 && ReasoningOutput == 0 && Total == 0; } }
        public bool HasNegativeValue { get { return Input < 0 || CachedInput < 0 || Output < 0 || ReasoningOutput < 0 || Total < 0; } }

        public void Add(TokenBreakdown other)
        {
            Input += other.Input;
            CachedInput += other.CachedInput;
            Output += other.Output;
            ReasoningOutput += other.ReasoningOutput;
            Total += other.Total;
        }

        public TokenBreakdown Delta(TokenBreakdown previous)
        {
            TokenBreakdown delta = new TokenBreakdown();
            delta.Input = Input - previous.Input;
            delta.CachedInput = CachedInput - previous.CachedInput;
            delta.Output = Output - previous.Output;
            delta.ReasoningOutput = ReasoningOutput - previous.ReasoningOutput;
            delta.Total = Total - previous.Total;
            if (delta.Total == 0) delta.Total = delta.Input + delta.Output;
            return delta;
        }
    }

    internal sealed class SessionSource
    {
        public readonly string RolloutPath;
        public readonly string Model;

        public SessionSource(string rolloutPath, string model)
        {
            RolloutPath = rolloutPath;
            Model = model;
        }
    }

    internal sealed class ModelTokenPrice
    {
        public readonly double InputPerMillion;
        public readonly double CachedInputPerMillion;
        public readonly double OutputPerMillion;

        private ModelTokenPrice(double input, double cachedInput, double output)
        {
            InputPerMillion = input;
            CachedInputPerMillion = cachedInput;
            OutputPerMillion = output;
        }

        public static ModelTokenPrice For(string model)
        {
            string normalized = (model ?? "").ToLowerInvariant();
            if (normalized.Contains("gpt-5")) return new ModelTokenPrice(1.25, 0.125, 10.0);
            if (normalized.Contains("o3")) return new ModelTokenPrice(2.0, 0.5, 8.0);
            if (normalized.Contains("gpt-4.1")) return new ModelTokenPrice(2.0, 0.5, 8.0);
            return new ModelTokenPrice(1.25, 0.125, 10.0);
        }
    }

    internal static class SQLiteNative
    {
        private const int SQLITE_OK = 0;
        private const int SQLITE_ROW = 100;
        private const int SQLITE_DONE = 101;
        private const int SQLITE_OPEN_READONLY = 0x00000001;

        public static List<Dictionary<string, object>> Query(string dbPath, string sql)
        {
            IntPtr db;
            int rc = sqlite3_open_v2(ToUtf8Bytes(dbPath), out db, SQLITE_OPEN_READONLY, null);
            if (rc != SQLITE_OK)
            {
                string message = db == IntPtr.Zero ? "open failed" : ErrorMessage(db);
                if (db != IntPtr.Zero) sqlite3_close(db);
                throw new InvalidOperationException(message);
            }

            try
            {
                IntPtr stmt;
                IntPtr tail;
                rc = sqlite3_prepare_v2(db, ToUtf8Bytes(sql), -1, out stmt, out tail);
                if (rc != SQLITE_OK) throw new InvalidOperationException(ErrorMessage(db));

                try
                {
                    List<Dictionary<string, object>> rows = new List<Dictionary<string, object>>();
                    int columnCount = sqlite3_column_count(stmt);
                    while (true)
                    {
                        rc = sqlite3_step(stmt);
                        if (rc == SQLITE_DONE) break;
                        if (rc != SQLITE_ROW) throw new InvalidOperationException(ErrorMessage(db));

                        Dictionary<string, object> row = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                        for (int i = 0; i < columnCount; i++)
                        {
                            string name = PtrToUtf8(sqlite3_column_name(stmt, i));
                            int type = sqlite3_column_type(stmt, i);
                            if (type == 1) row[name] = sqlite3_column_int64(stmt, i);
                            else if (type == 2) row[name] = sqlite3_column_double(stmt, i);
                            else if (type == 3) row[name] = PtrToUtf8(sqlite3_column_text(stmt, i));
                            else row[name] = null;
                        }
                        rows.Add(row);
                    }
                    return rows;
                }
                finally
                {
                    sqlite3_finalize(stmt);
                }
            }
            finally
            {
                sqlite3_close(db);
            }
        }

        private static byte[] ToUtf8Bytes(string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value);
            byte[] nul = new byte[bytes.Length + 1];
            Buffer.BlockCopy(bytes, 0, nul, 0, bytes.Length);
            return nul;
        }

        private static string ErrorMessage(IntPtr db)
        {
            return PtrToUtf8(sqlite3_errmsg(db));
        }

        private static string PtrToUtf8(IntPtr ptr)
        {
            if (ptr == IntPtr.Zero) return "";
            int length = 0;
            while (Marshal.ReadByte(ptr, length) != 0) length++;
            byte[] bytes = new byte[length];
            Marshal.Copy(ptr, bytes, 0, length);
            return Encoding.UTF8.GetString(bytes);
        }

        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_open_v2(byte[] filename, out IntPtr db, int flags, string vfs);
        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_close(IntPtr db);
        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr sqlite3_errmsg(IntPtr db);
        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_prepare_v2(IntPtr db, byte[] sql, int numBytes, out IntPtr stmt, out IntPtr tail);
        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_step(IntPtr stmt);
        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_finalize(IntPtr stmt);
        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_column_count(IntPtr stmt);
        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr sqlite3_column_name(IntPtr stmt, int iCol);
        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_column_type(IntPtr stmt, int iCol);
        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern long sqlite3_column_int64(IntPtr stmt, int iCol);
        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern double sqlite3_column_double(IntPtr stmt, int iCol);
        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr sqlite3_column_text(IntPtr stmt, int iCol);
    }
}

