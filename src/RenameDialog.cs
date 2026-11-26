using System;
using System.Drawing;
using System.Windows.Forms;

namespace TaskbarBattery
{
    // Small dialog to give a device a custom name, built from the same themed controls as the settings window.
    // An empty name, or "Restore original", brings back the name Windows reports. Enter saves, Esc cancels.
    sealed class RenameDialog : Form
    {
        readonly float scale;
        readonly FluentTextBox nameBox;
        readonly BatteryDevice device;

        // The new custom name, or null to restore the original name. Valid when the dialog returns OK.
        public string CustomName { get; private set; }

        public RenameDialog(BatteryDevice device)
        {
            this.device = device;
            Palette p = Palette.Current;
            scale = DeviceDpi / 96f;

            Text = Strings.Get(Strings.RenameTitle);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowIcon = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            AutoScaleMode = AutoScaleMode.None;
            KeyPreview = true;
            BackColor = p.Background;
            ForeColor = p.Text;
            var font = new Font(Theme.TextFont, 9.5f);
            Font = font;

            int margin = Px(24);
            int width = Px(380);

            var prompt = new Label
            {
                AutoSize = false,
                Text = Strings.Format(Strings.RenamePrompt, device.Name),
                ForeColor = p.Secondary,
                BackColor = p.Background,
                Location = new Point(margin, margin),
                Size = new Size(width, Px(22)),
            };

            nameBox = new FluentTextBox(scale, new Font(Theme.TextFont, 10.5f), device.DisplayName, width)
            {
                BackColor = p.Background,
                Location = new Point(margin, prompt.Bottom + Px(8)),
            };
            nameBox.Box.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    Save();
                }
            };

            var save = new FlatButton(scale, font, Strings.Get(Strings.Save)) { Primary = true, BackColor = p.Background };
            var cancel = new FlatButton(scale, font, Strings.Get(Strings.Cancel)) { BackColor = p.Background };
            var restore = new FlatButton(scale, font, Strings.Get(Strings.RestoreName)) { BackColor = p.Background };
            restore.Enabled = !string.IsNullOrEmpty(device.CustomName);

            int buttonsTop = nameBox.Bottom + Px(24);
            save.Location = new Point(margin + width - save.Width, buttonsTop);
            cancel.Location = new Point(save.Left - Px(8) - cancel.Width, buttonsTop);
            restore.Location = new Point(margin, buttonsTop);

            save.Click += delegate { Save(); };
            cancel.Click += delegate { DialogResult = DialogResult.Cancel; };
            restore.Click += delegate
            {
                CustomName = null;
                DialogResult = DialogResult.OK;
            };

            Controls.Add(prompt);
            Controls.Add(nameBox);
            Controls.Add(restore);
            Controls.Add(cancel);
            Controls.Add(save);
            ClientSize = new Size(margin * 2 + width, save.Bottom + margin);
        }

        int Px(float logical)
        {
            return (int)Math.Round(logical * scale);
        }

        void Save()
        {
            string name = nameBox.Text.Trim();
            CustomName = name.Length == 0 || name == device.Name ? null : name;
            DialogResult = DialogResult.OK;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Escape)
                DialogResult = DialogResult.Cancel;
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
            nameBox.Box.Focus();
            nameBox.Box.SelectAll();
        }
    }
}
