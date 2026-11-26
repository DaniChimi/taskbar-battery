using System;
using System.Drawing;
using System.Windows.Forms;

namespace TaskbarBattery
{
    // Small dialog to give a device a custom name, themed like the flyout. An empty name, or "Restore",
    // brings back the name Windows reports.
    sealed class RenameDialog : Form
    {
        readonly TextBox nameBox;

        // The new custom name, or null to restore the original name. Valid when the dialog returns OK.
        public string CustomName { get; private set; }

        public RenameDialog(BatteryDevice device)
        {
            Palette p = Palette.Current;
            Text = Strings.Get(Strings.RenameTitle);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterScreen;
            TopMost = true;
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = new Font(Theme.TextFont, 9.5f);
            BackColor = p.Background;
            ForeColor = p.Text;
            Padding = new Padding(16);
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;

            var layout = new TableLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                Dock = DockStyle.Fill,
                BackColor = p.Background,
            };

            var prompt = new Label
            {
                AutoSize = true,
                Text = Strings.Format(Strings.RenamePrompt, device.Name),
                ForeColor = p.Secondary,
                Margin = new Padding(0, 0, 0, 8),
            };

            nameBox = new TextBox
            {
                Text = device.DisplayName,
                Width = 300,
                BackColor = p.Card,
                ForeColor = p.Text,
                BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(0, 0, 0, 16),
            };

            var buttons = new FlowLayoutPanel
            {
                AutoSize = true,
                FlowDirection = FlowDirection.RightToLeft,
                Dock = DockStyle.Fill,
                Margin = new Padding(0),
                BackColor = p.Background,
            };

            Button save = MakeButton(Strings.Get(Strings.Save), p, true);
            Button cancel = MakeButton(Strings.Get(Strings.Cancel), p, false);
            Button restore = MakeButton(Strings.Get(Strings.RestoreName), p, false);
            restore.Enabled = !string.IsNullOrEmpty(device.CustomName);

            save.Click += delegate
            {
                string name = nameBox.Text.Trim();
                CustomName = name.Length == 0 || name == device.Name ? null : name;
                DialogResult = DialogResult.OK;
            };
            restore.Click += delegate
            {
                CustomName = null;
                DialogResult = DialogResult.OK;
            };
            cancel.DialogResult = DialogResult.Cancel;

            buttons.Controls.Add(save);
            buttons.Controls.Add(cancel);
            buttons.Controls.Add(restore);

            layout.Controls.Add(prompt);
            layout.Controls.Add(nameBox);
            layout.Controls.Add(buttons);
            Controls.Add(layout);

            AcceptButton = save;
            CancelButton = cancel;
        }

        static Button MakeButton(string text, Palette p, bool primary)
        {
            var button = new Button
            {
                Text = text,
                AutoSize = true,
                MinimumSize = new Size(90, 30),
                FlatStyle = FlatStyle.Flat,
                BackColor = primary ? p.Bar : p.Card,
                ForeColor = primary ? (Theme.IsLight ? Color.White : Color.Black) : p.Text,
                Margin = new Padding(8, 0, 0, 0),
                UseVisualStyleBackColor = false,
            };
            button.FlatAppearance.BorderColor = primary ? p.Bar : p.CardBorder;
            return button;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            int dark = Theme.IsLight ? 0 : 1;
            Native.DwmSetWindowAttribute(Handle, Native.DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int));
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Activate();
            nameBox.Focus();
            nameBox.SelectAll();
        }
    }
}
