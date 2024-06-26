﻿using maxrumsey.ozstrips.gui;
using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using vatsys;

namespace maxrumsey.ozstrips.controls
{
    public partial class StripBaseGUI : UserControl
    {
        public FDP2.FDR fdr;
        public Color defColor = Color.Empty;
        public Panel[] crossColourControls = new Panel[] { };
        public Panel[] cockColourControls = new Panel[] { };

        public StripController stripController;
        public Panel pickToggleControl;

        public Label lb_eobt;
        public Label lb_acid;
        public Label lb_ssr;
        public Label lb_type;
        public Label lb_frul;
        public Label lb_route;
        public Label lb_sid;
        public Label lb_ades;
        public Label lb_alt;
        public Label lb_hdg;
        public Label lb_rwy;
        public Label lb_wtc;
        public Label lb_std;
        public Label lb_clx;
        public Label lb_tot;
        public Label lb_remark;
        public Label lb_req;
        public Label lb_glop;
        public Label lb_ssricon;

        public StripBaseGUI()
        {
            InitializeComponent();
        }



        public void Cock(int _cockLevel, bool sync = true, bool update = true)
        {
            if (_cockLevel == -1)
            {
                _cockLevel = stripController.cockLevel + 1;
                if (_cockLevel >= 2) _cockLevel = 0;
            }
            if (update) stripController.cockLevel = _cockLevel;
            int marginLeft = 0;
            Color color = Color.Empty;
            if (stripController.cockLevel == 1)
            {
                marginLeft = 30;
                color = Color.Cyan;
            }
            foreach (Panel pl in cockColourControls)
            {
                pl.BackColor = color;
            }
            stripController.stripHolderControl.Margin = new Padding(marginLeft,0,0,0);
            if (sync) stripController.SyncStrip();
        }
        
        public void SetCross(bool sync = true)
        {
            Color color = defColor;
            if (stripController.Crossing == true)
            {
                color = Color.Salmon;
            }

            foreach (Control control in crossColourControls)
            {
                control.BackColor = color;
            }

            if (sync) stripController.SyncStrip();
        }

        public void UpdateStrip()
        {
            SuspendLayout();
            if (fdr == null) return;
            if (lb_eobt != null) lb_eobt.Text = stripController.Time;
            lb_acid.Text = fdr.Callsign;
            lb_ssr.Text = (fdr.AssignedSSRCode == -1) ? "XXXX" : Convert.ToString(fdr.AssignedSSRCode, 8).PadLeft(4, '0');
            lb_type.Text = fdr.AircraftType;
            lb_frul.Text = fdr.FlightRules;

            String rteItem = fdr.Route.Split(' ').ToList().Find(x => !x.Contains("/"));
            if (rteItem == null) rteItem = fdr.Route;

            if (lb_route != null) lb_route.Text = rteItem;

            if (lb_sid != null) lb_sid.Text = stripController.SID;
            if (lb_ades != null) lb_ades.Text = fdr.DesAirport;
            if (lb_alt != null) lb_alt.Text = stripController.CFL;
            if (lb_hdg != null) lb_hdg.Text = stripController.HDG == "" ? "" : "H" + stripController.HDG;
            if (lb_clx != null) lb_clx.Text = stripController.CLX;
            if (lb_std != null) lb_std.Text = stripController.GATE;
            if (lb_remark != null) lb_remark.Text = stripController.Remark;
            if (lb_tot != null && stripController.TakeOffTime != DateTime.MaxValue)
            {
                TimeSpan diff = DateTime.UtcNow - stripController.TakeOffTime;
                lb_tot.Text = diff.ToString(@"mm\:ss");
                lb_tot.ForeColor = Color.Green;
            }
            else if (lb_tot != null)
            {
                lb_tot.Text = "00:00";
                lb_tot.ForeColor = Color.Black;
            }
            if (lb_req != null) lb_req.Text = (stripController.fdr.RFL / 100).ToString();
            if (lb_glop != null) lb_glop.Text = stripController.fdr.GlobalOpData;
            if (lb_ssricon != null && stripController.SquawkCorrect) lb_ssricon.Text = "*";
            else if (lb_ssricon != null) lb_ssricon.Text = "";
            SetCross(false);
            Cock(0, false, false);
            lb_rwy.Text = stripController.RWY;
            lb_wtc.Text = fdr.AircraftWake;
            ResumeLayout();
        }


        public void OpenHdgAltModal()
        {
            AltHdgControl modalChild = new AltHdgControl(stripController);
            BaseModal bm = new BaseModal(modalChild, "ACD Menu :: " + stripController.fdr.Callsign);
            modalChild.bm = bm;
            bm.ReturnEvent += new ReturnEventHandler(HeadingAltReturned);
            bm.Show(MainForm.mainForm);

        }
        public void OpenCLXBayModal()
        {
            BayCLXControl modalChild = new BayCLXControl(stripController);
            BaseModal bm = new BaseModal(modalChild, "SMC Menu :: " + stripController.fdr.Callsign);
            modalChild.bm = bm;
            bm.ReturnEvent += new ReturnEventHandler(CLXBayReturned);
            bm.Show(MainForm.mainForm);
        }
        public void OpenVatsysFDRModMenu()
        {
            //MMI.OpenFPWindow(stripController.fdr);
            stripController.OpenVatsysFDR();
        }

        /*
         *  Returned event args from handler
         */
        public void HeadingAltReturned(object source, ModalReturnArgs args)
        {
            try
            {
                AltHdgControl control = (AltHdgControl)args.child;
                if (control.Alt != "") stripController.CFL = control.Alt;
                stripController.HDG = control.Hdg;
                if (control.Runway != "") stripController.RWY = control.Runway;
                if (control.SID != "")
                {
                    stripController.SID = control.SID;
                }

                stripController.SyncStrip();
            }
            catch (Exception ex) { }
        }
        public void CLXBayReturned(object source, ModalReturnArgs args)
        {
            BayCLXControl control = (BayCLXControl)args.child;
            stripController.CLX = control.CLX;
            stripController.GATE = control.GATE;
            stripController.Remark = control.Remark;
            FDP2.SetGlobalOps(stripController.fdr, control.glop);
            stripController.SyncStrip();
        }
        public void TogglePick()
        {
            if (stripController != null)
            {
                stripController.TogglePick();
            }
        }

        public void HMI_TogglePick(bool picked)
        {
            if (pickToggleControl != null)
            {
                Color color = Color.Empty;
                if (picked) color = Color.Silver;

                pickToggleControl.BackColor = color;
            }
        }
        public void Initialise()
        {
            this.Dock = DockStyle.Fill;

        }

        public void AssignSSR()
        {
            if (fdr.AssignedSSRCode == -1) FDP2.SetASSR(stripController.fdr);
        }
    }

}
