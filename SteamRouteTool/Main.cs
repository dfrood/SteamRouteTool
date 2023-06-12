using NetFwTypeLib;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading;
using System.Windows.Forms;

namespace SteamRouteTool
{
    public partial class Main : Form
    {
        public List<Route> routes = new List<Route>();
        int rowCount = 0;
        bool columnChecked = false;
        bool firstLoad = true;
        string networkconfigURL = @"https://api.steampowered.com/ISteamApps/GetSDRConfig/v1?appid=730";

        public Main()
        {
            InitializeComponent();
            ClearCSGORoutingToolRules();
            Thread populateRoutesThread = new Thread(new ThreadStart(PopulateRoutes));
            populateRoutesThread.Start();
        }

        private void ClearCSGORoutingToolRules()
        {
            Type tNetFwPolicy2 = Type.GetTypeFromProgID("HNetCfg.FwPolicy2");
            INetFwPolicy2 fwPolicy2 = (INetFwPolicy2)Activator.CreateInstance(tNetFwPolicy2);
            foreach (INetFwRule rule in fwPolicy2.Rules)
            {
                if (rule.Name.Contains("CSGORoutingTool-")) { fwPolicy2.Rules.Remove(rule.Name); }
            }
        }

        private void PopulateRoutes()
        {
            string raw = new WebClient().DownloadString(networkconfigURL);
            JObject jObj = JsonConvert.DeserializeObject<JObject>(raw);

            foreach (KeyValuePair<string, JToken> rc in (JObject)jObj["pops"])
            {
                if (rc.Value.ToString().Contains("relays") && !rc.Value.ToString().Contains("cloud-test"))
                {
                    Route route = new Route();
                    route.name = rc.Key;
                    if (rc.Value.ToString().Contains("\"desc\"")) { route.desc = rc.Value["desc"].ToString(); }
                    route.ranges = new Dictionary<string, string>();
                    route.row_index = new List<int>();
                    foreach (JObject range in rc.Value["relays"])
                    {
                        Console.WriteLine(range["port_range"][0].ToString() + "-" + range["port_range"][1].ToString());
                        route.ranges.Add(range["ipv4"].ToString(), range["port_range"].ToString());
                        route.row_index.Add(rowCount);
                        rowCount++;
                    }
                    if (rc.Value.ToString().Contains("partners\": 2")) { route.pw = true; }
                    else { route.pw = false; }
                    route.extended = false;
                    route.all_check = false;
                    routes.Add(route);
                }
            }

            btn_PingRoutes.BeginInvoke(new MethodInvoker(() =>
            {
                btn_PingRoutes.Enabled = true;
            }));
            lb_GettingRoutes.BeginInvoke(new MethodInvoker(() =>
            {
                lb_GettingRoutes.Visible = false;
            }));
            btn_About.BeginInvoke(new MethodInvoker(() =>
            {
                btn_About.Visible = true;
            }));

            if (InvokeRequired) { Invoke((Action)PopulateRouteDataGrid); }
            else { PopulateRouteDataGrid(); }
        }

        private void PopulateRouteDataGrid()
        {
            if (routeDataGrid.RowCount == 0)
            {
                for (int i = 0; i < rowCount; i++)
                {
                    routeDataGrid.Rows.Add();
                    routeDataGrid.Rows[i].Cells[2].Value = false;
                }
            }

            foreach (Route route in routes)
            {
                for (int i = 0; i < route.ranges.Count; i++)
                {
                    if (route.desc != null) { routeDataGrid.Rows[route.row_index[i]].Cells[0].Value = route.desc + " " + (i + 1); }
                    else { routeDataGrid.Rows[route.row_index[i]].Cells[0].Value = route.name + " " + (i + 1); }
                    if (route.extended == false)
                    {
                        if (route.desc != null) { routeDataGrid.Rows[route.row_index[0]].Cells[0].Value = route.desc; }
                        else { routeDataGrid.Rows[route.row_index[0]].Cells[0].Value = route.name; }
                    }
                    if (i > 0 && route.extended == false) { routeDataGrid.Rows[route.row_index[i]].Visible = false; }
                    else { routeDataGrid.Rows[route.row_index[i]].Visible = true; }

                }
            }

            if (firstLoad)
            {
                PingRoutes();
                GetCurrentBlocked();
            }
            firstLoad = false;
        }

        private void PingSingleRoute(Route route)
        {
            Thread thread = new Thread(() =>
            {
                for (int i = 0; i < route.ranges.Count; i++)
                {
                    routeDataGrid.Rows[route.row_index[i]].Cells[1].Style.BackColor = Color.Black;
                    string responseTime = PingHost(route.ranges.Keys.ToArray()[i]);
                    if (responseTime != "-1")
                    {
                        if (Convert.ToInt32(responseTime) <= 50) { routeDataGrid.Rows[route.row_index[i]].Cells[1].Style.ForeColor = Color.Green; }
                        if (Convert.ToInt32(responseTime) > 50 && Convert.ToInt32(responseTime) <= 100) { routeDataGrid.Rows[route.row_index[i]].Cells[1].Style.ForeColor = Color.Orange; }
                        if (Convert.ToInt32(responseTime) > 100) { routeDataGrid.Rows[route.row_index[i]].Cells[1].Style.ForeColor = Color.Red; }
                    }
                    else
                    {
                        routeDataGrid.Rows[route.row_index[i]].Cells[1].Style.ForeColor = Color.DarkRed;
                    }
                    routeDataGrid.Rows[route.row_index[i]].Cells[1].Value = responseTime;
                    routeDataGrid.Rows[route.row_index[i]].Cells[1].Style.BackColor = Color.White;
                }
            });

            thread.Start();
        }

        private void PingRoutes()
        {
            foreach (Route route in routes)
            {
                Thread thread = new Thread(() =>
                {
                    for (int i = 0; i < route.ranges.Count; i++)
                    {
                        routeDataGrid.Rows[route.row_index[i]].Cells[1].Style.BackColor = Color.Black;
                        string responseTime = PingHost(route.ranges.Keys.ToArray()[i]);
                        if (responseTime != "-1")
                        {
                            if (Convert.ToInt32(responseTime) <= 50) { routeDataGrid.Rows[route.row_index[i]].Cells[1].Style.ForeColor = Color.Green; }
                            if (Convert.ToInt32(responseTime) > 50 && Convert.ToInt32(responseTime) <= 100) { routeDataGrid.Rows[route.row_index[i]].Cells[1].Style.ForeColor = Color.Orange; }
                            if (Convert.ToInt32(responseTime) > 100) { routeDataGrid.Rows[route.row_index[i]].Cells[1].Style.ForeColor = Color.Red; }
                        }
                        else
                        {
                            routeDataGrid.Rows[route.row_index[i]].Cells[1].Style.ForeColor = Color.DarkRed;
                        }
                        routeDataGrid.Rows[route.row_index[i]].Cells[1].Value = responseTime;
                        routeDataGrid.Rows[route.row_index[i]].Cells[1].Style.BackColor = Color.White;
                    }
                });

                thread.Start();
            }
        }

        public static string PingHost(string host)
        {
            try
            {
                Ping ping = new Ping();
                PingReply pingreply = ping.Send(host);
                if (pingreply.RoundtripTime == 0) { return "-1"; }
                else { return pingreply.RoundtripTime.ToString(); }
            }
            catch (Exception ex) { return "-1"; }
        }

        private void GetCurrentBlocked()
        {
            Type tNetFwPolicy2 = Type.GetTypeFromProgID("HNetCfg.FwPolicy2");
            INetFwPolicy2 fwPolicy2 = (INetFwPolicy2)Activator.CreateInstance(tNetFwPolicy2);

            foreach (INetFwRule rule in fwPolicy2.Rules)
            {
                if (rule.Name.Contains("SteamRouteTool-"))
                {
                    string name = rule.Name.Split('-')[1];

                    List<string> addr = new List<string>();
                    foreach (string tosplit in rule.RemoteAddresses.Split(',')) { addr.Add(tosplit.Split('/')[0]); }
                    foreach (Route route in routes)
                    {
                        if (route.name == name)
                        {
                            bool extended = true;
                            bool firstBlocked = false;
                            int blockedCount = 0;
                            for (int i = 0; i < route.ranges.Count; i++)
                            {
                                if (addr.Contains(route.ranges.Keys.ToArray()[i]))
                                {
                                    routeDataGrid.Rows[route.row_index[i]].Cells[2].Value = true;
                                    if (i != 0) { blockedCount++;  }
                                    if (i == 0) { firstBlocked = true; }
                                }
                            }
                            if (blockedCount == route.ranges.Count-1 && firstBlocked)
                            {
                                extended = false;
                            }
                            route.extended = extended;
                            if (extended)
                            {
                                foreach (int index in route.row_index) { routeDataGrid.Rows[index].Visible = true; }
                            }
                        }
                    }
                }
            }
        }

        private void Btn_ClearRules_Click(object sender, EventArgs e)
        {
            Type tNetFwPolicy2 = Type.GetTypeFromProgID("HNetCfg.FwPolicy2");
            INetFwPolicy2 fwPolicy2 = (INetFwPolicy2)Activator.CreateInstance(tNetFwPolicy2);
            foreach (INetFwRule rule in fwPolicy2.Rules)
            {
                if (rule.Name.Contains("SteamRouteTool-")) { fwPolicy2.Rules.Remove(rule.Name); }
                for (int i = 0; i < routes.Count; i++) { routeDataGrid.Rows[i].Cells[2].Value = false; }
            }
            MessageBox.Show("You have cleared all firewall rules created by this tool.", "Steam Route Tool - Rules Clear");
        }

        private void Btn_PingRoutes_Click(object sender, EventArgs e)
        {
            PingRoutes();
        }

        void RouteDataGrid_CurrentCellDirtyStateChanged(object sender, EventArgs e)
        {
            if (routeDataGrid.CurrentCell.ColumnIndex == 2 && routeDataGrid.IsCurrentCellDirty)
            {
                routeDataGrid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }
        }

        private void RouteDataGrid_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.ColumnIndex == 0 && e.RowIndex != -1)
            {
                Route currentRoute = routes.Where(x => x.row_index.Contains(e.RowIndex)).First();
                if (currentRoute.all_check && !currentRoute.extended)
                {
                    bool blocked = false;
                    for (int i = 0; i < currentRoute.row_index.Count; i++)
                    {
                        if (i == 0)
                        {
                            blocked = Convert.ToBoolean(routeDataGrid.Rows[currentRoute.row_index[i]].Cells[2].Value);
                        }
                        else
                        {
                            routeDataGrid.Rows[currentRoute.row_index[i]].Cells[2].Value = blocked;
                        }
                    }
                }

                currentRoute.extended ^= true;
                currentRoute.all_check ^= true;
                PopulateRouteDataGrid();
                PingSingleRoute(currentRoute);
            }

            if (e.ColumnIndex == 1 && e.RowIndex != -1)
            {
                Thread thread = new Thread(() =>
                {
                    Route currentRoute = routes.Where(x => x.row_index.Contains(e.RowIndex)).First();
                    for (int i = 0; i < currentRoute.row_index.Count; i++)
                    {
                        if (currentRoute.row_index[i] == e.RowIndex)
                        {
                            routeDataGrid.Rows[e.RowIndex].Cells[1].Style.BackColor = Color.Black;
                            string responseTime = PingHost(currentRoute.ranges.Keys.ToArray()[i]);

                            if (responseTime != "-1")
                            {
                                if (Convert.ToInt32(responseTime) <= 50)
                                {
                                    routeDataGrid.Rows[e.RowIndex].Cells[1].Style.ForeColor = Color.Green;
                                }
                                if (Convert.ToInt32(responseTime) > 50 && Convert.ToInt32(responseTime) <= 100)
                                {
                                    routeDataGrid.Rows[e.RowIndex].Cells[1].Style.ForeColor = Color.Orange;
                                }
                                if (Convert.ToInt32(responseTime) > 100)
                                {
                                    routeDataGrid.Rows[e.RowIndex].Cells[1].Style.ForeColor = Color.Red;
                                }
                            }
                            else
                            {
                                routeDataGrid.Rows[e.RowIndex].Cells[1].Style.ForeColor = Color.DarkRed;
                            }


                            routeDataGrid.Rows[e.RowIndex].Cells[1].Value = responseTime;
                            routeDataGrid.Rows[e.RowIndex].Cells[1].Style.BackColor = Color.White;
                        }
                    }
                });
                thread.Start();
            }

            if (e.ColumnIndex == 2 && e.RowIndex != -1)
            {
                Route currentRoute = routes.Where(x => x.row_index.Contains(e.RowIndex)).First();
                if (!currentRoute.extended) { currentRoute.all_check = true; }
                else { currentRoute.all_check = false; }
                SetRule(currentRoute);
            }

            if (e.ColumnIndex == 2 && e.RowIndex == -1)
           { 
                if (!columnChecked)
                {
                    for (int i = 0; i < routeDataGrid.Rows.Count; i++)
                    {
                        routeDataGrid.Rows[i].Cells[2].Value = true;
                    }
                    foreach (Route route in routes)
                    {
                        SetRule(route);
                    }
                    columnChecked = true;
                }
                else if (columnChecked)
                {
                    for (int i = 0; i < routeDataGrid.Rows.Count; i++)
                    {
                        routeDataGrid.Rows[i].Cells[2].Value = false;
                    }
                    columnChecked = false;
                }
            }
        }

        private void SetRule(Route route)
        {
            Type tNetFwPolicy2 = Type.GetTypeFromProgID("HNetCfg.FwPolicy2");
            INetFwPolicy2 fwPolicy2 = (INetFwPolicy2)Activator.CreateInstance(tNetFwPolicy2);
            try { fwPolicy2.Rules.Remove("SteamRouteTool-" + route.name); }
            catch { }

            INetFwRule fwRule = (INetFwRule2)Activator.CreateInstance(Type.GetTypeFromProgID("HNetCfg.FWRule"));

            fwRule.Enabled = true;
            fwRule.Direction = NET_FW_RULE_DIRECTION_.NET_FW_RULE_DIR_OUT;
            fwRule.Action = NET_FW_ACTION_.NET_FW_ACTION_BLOCK;

            string remoteAddresses = "";

            int index = 0;
            for (int i = 0; i < route.ranges.Count; i++)
            {
                if ((bool)routeDataGrid.Rows[route.row_index[i]].Cells[2].Value)
                {
                    if (index == 0 && route.all_check)
                    {
                        foreach (KeyValuePair<string,string> range in route.ranges)
                        {
                            remoteAddresses += range.Key + ",";
                        }
                        break;
                    }
                    else
                    {
                        remoteAddresses += route.ranges.Keys.ToArray()[index] + ",";
                    }
                }
                index++;
            }
            if (remoteAddresses != "")
            {
                remoteAddresses = remoteAddresses.Substring(0, remoteAddresses.Length - 1);
                fwRule.RemoteAddresses = remoteAddresses;
                fwRule.Protocol = 17;
                fwRule.RemotePorts = "27015-27068";
                fwRule.Name = "SteamRouteTool-" + route.name;
                INetFwPolicy2 firewallPolicy = (INetFwPolicy2)Activator.CreateInstance(Type.GetTypeFromProgID("HNetCfg.FwPolicy2"));
                firewallPolicy.Rules.Add(fwRule);
            }
        }

        private void Btn_About_Click(object sender, EventArgs e)
        {
            MessageBox.Show("Version: " + ProductVersion + Environment.NewLine + "Steam Route Tool is created by Froody.", "About", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
