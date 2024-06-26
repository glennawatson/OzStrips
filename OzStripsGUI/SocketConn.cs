﻿using maxrumsey.ozstrips.gui.DTO;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Timers;
using System.Windows.Forms;
using vatsys;

namespace maxrumsey.ozstrips.gui
{
    public class SocketConn
    {
        SocketIOClient.SocketIO io;
        private BayManager bayManager;
        private bool isDebug = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("VisualStudioEdition"));
        public List<string> Messages = new List<string>();
        private bool versionShown = false;
        private bool freshClient = true;
        private System.Timers.Timer fifteensecTimer;
        private System.Timers.Timer oneMinTimer;
        private MainForm mainForm;
        public SocketConn(BayManager bayManager, MainForm mf)
        {
            mainForm = mf;
            this.bayManager = bayManager;
            io = new SocketIOClient.SocketIO(Config.socketioaddr);
            io.OnAny((sender, e) =>
            {
                MetadataDTO metaDTO = e.GetValue<MetadataDTO>(1);
                if (metaDTO.version != Config.version && !versionShown)
                {
                    versionShown = true;
                    if (mf.Visible) mf.Invoke((MethodInvoker)delegate ()
                    {
                        Util.ShowInfoBox("New Update Available: " + metaDTO.version);
                    });
                
                }
                if (metaDTO.apiversion != Config.apiversion)
                {
                    if (mf.Visible) mf.Invoke((MethodInvoker)delegate ()
                    {
                        Util.ShowErrorBox("OzStrips incompatible with current API version!");
                        mf.Close();
                        mf.Dispose();
                    });
                }
            });

            io.OnConnected += async (sender, e) =>
            {
                AddMessage("c: conn established");
                freshClient = true;
                await io.EmitAsync("client:aerodrome_subscribe", bayManager.AerodromeName, Network.Me.RealName);
                if (mf.Visible) mf.Invoke((MethodInvoker)delegate () { mf.SetConnStatus(true); });
                oneMinTimer = new System.Timers.Timer();
                oneMinTimer.AutoReset = false;
                oneMinTimer.Interval = 60000;
                oneMinTimer.Elapsed += ToggleFresh;
                oneMinTimer.Start();
            };
            io.OnDisconnected += (sender, e) =>
            {
                AddMessage("c: conn lost");
                mf.SetConnStatus(false);
            };
            io.OnError += (sender, e) =>
            {
                AddMessage("c: error" + e);
                mf.SetConnStatus(false);
                MMI.InvokeOnGUI(delegate () { Errors.Add(new Exception(e), "OzStrips"); });
            };
            io.OnReconnected += (sender, e) =>
            {
                if (io.Connected) io.EmitAsync("client:aerodrome_subscribe", bayManager.AerodromeName);
                mf.SetConnStatus(true);
            };
            io.OnReconnectError += (sender, e) =>
            {
                AddMessage("recon error");
            };
            io.On("server:sc_change", sc =>
            {
                StripControllerDTO scDTO = sc.GetValue<StripControllerDTO>();
                AddMessage("s:sc_change: " + JsonSerializer.Serialize(scDTO));

                if (mf.Visible) mf.Invoke((MethodInvoker)delegate () { StripController.UpdateFDR(scDTO, bayManager); });

            });
            io.On("server:sc_cache", sc =>
            {
                CacheDTO scDTO = sc.GetValue<CacheDTO>();
                AddMessage("s:sc_cache: " + JsonSerializer.Serialize(scDTO));

                if (mf.Visible && freshClient)
                {
                    mf.Invoke((MethodInvoker)delegate () { StripController.LoadCache(scDTO); });
                }
            });
            io.On("server:order_change", bdto =>
            {
                BayDTO bayDTO = bdto.GetValue<BayDTO>();
                AddMessage("s:order_change: " + JsonSerializer.Serialize(bayDTO));

                if (mf.Visible) mf.Invoke((MethodInvoker)delegate () { bayManager.UpdateOrder(bayDTO); });
            });
            io.On("server:metar", metarRaw =>
            {
                String metar = metarRaw.GetValue<string>();

                if (mf.Visible) mf.Invoke((System.Windows.Forms.MethodInvoker)delegate () { mainForm.SetMetar(metar); });
            });
            io.On("server:atis", codeRaw =>
            {
                String code = codeRaw.GetValue<string>();

                if (mf.Visible) mf.Invoke((System.Windows.Forms.MethodInvoker)delegate () { mainForm.SetATISCode(code); });
            });
            io.On("server:update_cache", (args) =>
            {
                AddMessage("s:update_cache: ");
                if (io.Connected) io.EmitAsync("client:request_metar");
                if (!freshClient) SendCache();
            });
            if (Network.IsConnected) Connect();
            bayManager.socketConn = this;
        }

        public void SyncSC(StripController sc)
        {
            StripControllerDTO scDTO = CreateStripDTO(sc);
            AddMessage("c:sc_change: " + JsonSerializer.Serialize(scDTO));
            if (scDTO.acid == "") return; // prevent bug
            if (CanSendDTO) io.EmitAsync("client:sc_change", scDTO);
        }
        public void SyncBay(Bay bay)
        {
            BayDTO bayDTO = CreateBayDTO(bay);
            AddMessage("c:order_change: " + JsonSerializer.Serialize(bayDTO));

            if (CanSendDTO) io.EmitAsync("client:order_change", bayDTO);
        }
        public void SetAerodrome()
        {
            freshClient = true;
            oneMinTimer = new System.Timers.Timer();
            oneMinTimer.AutoReset = false;
            oneMinTimer.Interval = 60000;
            oneMinTimer.Elapsed += ToggleFresh;
            oneMinTimer.Start();
            if (io.Connected) io.EmitAsync("client:aerodrome_subscribe", bayManager.AerodromeName);
        }

        public BayDTO CreateBayDTO(Bay bay)
        {
            BayDTO bayDTO = new BayDTO { bay = bay.BayTypes.First() };
            List<string> childList = new List<string>();
            foreach (StripListItem item in bay.Strips)
            {
                if (item.Type == StripItemType.STRIP) childList.Add(item.StripController.fdr.Callsign);
                else if (item.Type == StripItemType.QUEUEBAR) childList.Add("\a"); // indicates q-bar
            }
            bayDTO.list = childList;
            return bayDTO;
        }
        public StripControllerDTO CreateStripDTO(StripController sc)
        {
            StripControllerDTO scDTO = new StripControllerDTO { acid = sc.fdr.Callsign, bay = sc.currentBay, CLX = sc.CLX, GATE = sc.GATE, cockLevel = sc.cockLevel, crossing = sc.Crossing, remark = sc.Remark };
            if (sc.TakeOffTime != DateTime.MaxValue)
            {
                scDTO.TOT = sc.TakeOffTime.ToString(CultureInfo.InvariantCulture);
            }
            else
            {
                scDTO.TOT = "\0";
            }
            return scDTO;
        }
        public CacheDTO CreateCacheDTO()
        {
            List<StripControllerDTO> strips = new List<StripControllerDTO>();

            foreach (StripController strip in StripController.stripControllers)
            {
                strips.Add(CreateStripDTO(strip));
            }

            return new CacheDTO() { strips = strips };
        }

        public async void SendCache()
        {
            CacheDTO cacheDTO = CreateCacheDTO();
            AddMessage("c:sc_cache: " + JsonSerializer.Serialize(cacheDTO));
            if (CanSendDTO) await io.EmitAsync("client:sc_cache", cacheDTO);
        }

        public void Close()
        {
            io.DisconnectAsync();
            io.Dispose();
        }

        /// <summary>
        /// Whether the user has permission to send data to server
        /// </summary>
        private bool CanSendDTO
        {
            get
            {
                if (!(Network.Me.IsRealATC || isDebug)) AddMessage("c: DTO Rejected!");
                return io.Connected && (Network.Me.IsRealATC || isDebug);
            }
        }

        /// <summary>
        /// Starts a fifteen second timer, ensures FDRs have loaded in before requesting SCs from server.
        /// </summary>
        public void Connect()
        {
            fifteensecTimer = new System.Timers.Timer();
            fifteensecTimer.AutoReset = false;
            fifteensecTimer.Interval = 15000;
            fifteensecTimer.Elapsed += ConnectIO;
            fifteensecTimer.Start();
            mainForm.SetAerodrome(bayManager.AerodromeName);
        }

        private async void ConnectIO(object sender, ElapsedEventArgs e)
        {
            try
            {
                AddMessage("c: Attempting connection " + Config.socketioaddr);
                await io.ConnectAsync();
            }
            catch (Exception ex)
            {
                Errors.Add(ex, "OzStrips");
            }
        }

        private void ToggleFresh(object sender, ElapsedEventArgs e)
        {
            try
            {
                freshClient = false;
            }
            catch (Exception ex)
            {
                
            }
        }

        public void Disconnect()
        {
            io.DisconnectAsync();
        }

        private void AddMessage(string message)
        {
            lock (Messages)
            {
                Messages.Add(message);
            }
        }
    }
}
