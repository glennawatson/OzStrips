﻿using System;
using System.Drawing;
using System.Windows.Forms;

namespace maxrumsey.ozstrips.gui
{

    public partial class BaseModal : Form
    {
        Control child;
        public event ReturnEventHandler ReturnEvent;
        public BaseModal(Control child, String text)
        {
            StartPosition = FormStartPosition.CenterScreen;
            InitializeComponent();
            this.child = child;
            gb_cont.Controls.Add(child);
            child.Anchor = AnchorStyles.Top;
            child.Location = new Point(6, 16);

            Text = text;
            BringToFront();
        }

        private void bt_canc_Click(object sender, EventArgs e)
        {
            ExitModal();
        }

        private void bt_acp_Click(object sender, EventArgs e)
        {
            // to add
            // child.confirm();
            ExitModal(true);
        }


        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            Button btn = this.ActiveControl as Button;
            if (btn != null)
            {
                if (keyData == Keys.Enter)
                {
                    ExitModal(true);
                    return true; // suppress default handling of space
                }
                else if (keyData == Keys.Escape)
                {
                    ExitModal();
                    return true; // suppress default handling of space
                }
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        public void ExitModal(bool senddata = false)
        {
            if (senddata && ReturnEvent != null) ReturnEvent(this, new ModalReturnArgs(this.child));
            this.Close();
        }
    }
    public class ModalReturnArgs : EventArgs
    {
        public Control child;
        public ModalReturnArgs(Object child)
        {
            this.child = (Control)child;
        }
    }
    public delegate void ReturnEventHandler(object source, ModalReturnArgs e);
}
