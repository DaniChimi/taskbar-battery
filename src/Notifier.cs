using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace TaskbarBattery
{
    // One notification to show.
    sealed class Notice
    {
        public string Address;
        public string Title;
        public string Text;
        public ToolTipIcon Icon;
    }

    // Decides which notifications a new picture of the connected devices calls for, so the user hears about each
    // event once:
    // - low battery: once per discharge, re-armed when the device charges or rises RearmMargin above the threshold;
    // - fully charged: when the vendor protocol reports Full, not for a device already full when first seen;
    // - connected / disconnected: changes after the first scan only, not the devices present at start.
    sealed class Notifier
    {
        const int RearmMargin = 5;

        readonly Dictionary<string, State> states = new Dictionary<string, State>(StringComparer.OrdinalIgnoreCase);
        bool initialized;

        public List<Notice> Process(List<BatteryDevice> devices, Settings settings)
        {
            var notices = new List<Notice>();
            var present = new HashSet<string>(devices.Select(d => d.Address), StringComparer.OrdinalIgnoreCase);

            foreach (BatteryDevice device in devices)
            {
                State state;
                bool isNew = !states.TryGetValue(device.Address, out state);
                if (isNew)
                {
                    // A device already full when first seen does not get a "fully charged" notice.
                    state = new State { FullNotified = device.Charge == ChargeState.Full };
                    states[device.Address] = state;
                    if (initialized && settings.NotifyConnect)
                        notices.Add(Make(device, Strings.NoticeConnectedTitle, Strings.NoticeConnectedText, ToolTipIcon.Info));
                }
                state.Name = device.DisplayName;
                state.Percent = device.Percent;

                // Low battery.
                if (device.IsCharging || device.Percent > settings.LowThreshold + RearmMargin)
                {
                    state.LowNotified = false;
                }
                else if (device.IsLow && !state.LowNotified)
                {
                    state.LowNotified = true;
                    if (settings.NotifyLow)
                        notices.Add(Make(device, Strings.NoticeLowTitle, Strings.NoticeLowText, ToolTipIcon.Warning));
                }

                // Fully charged.
                if (device.Charge != ChargeState.Full)
                {
                    state.FullNotified = false;
                }
                else if (!state.FullNotified)
                {
                    state.FullNotified = true;
                    if (settings.NotifyFull)
                        notices.Add(Make(device, Strings.NoticeFullTitle, Strings.NoticeFullText, ToolTipIcon.Info));
                }
            }

            foreach (string address in states.Keys.Where(a => !present.Contains(a)).ToList())
            {
                State gone = states[address];
                states.Remove(address);
                if (initialized && settings.NotifyDisconnect)
                {
                    notices.Add(new Notice
                    {
                        Address = address,
                        Title = Strings.Format(Strings.NoticeDisconnectedTitle, gone.Name),
                        Text = Strings.Format(Strings.NoticeDisconnectedText, gone.Percent),
                        Icon = ToolTipIcon.Info,
                    });
                }
            }

            initialized = true;
            return notices;
        }

        static Notice Make(BatteryDevice device, string titleKey, string textKey, ToolTipIcon icon)
        {
            return new Notice
            {
                Address = device.Address,
                Title = Strings.Format(titleKey, device.DisplayName),
                Text = Strings.Format(textKey, device.Percent),
                Icon = icon,
            };
        }

        sealed class State
        {
            public string Name;
            public int Percent;
            public bool LowNotified;
            public bool FullNotified;
        }
    }
}
