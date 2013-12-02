using System;
using System.Windows.Forms;
using System.Net;
using System.IO;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace LoLOffline
{
    public partial class Form1 : Form
    {
        private String configFile = "";
        private String configFileBackup = "";
        private String xmppHost = "";
        private String xmppIp = "";

        public Form1()
        {
            InitializeComponent();
        }

        private void error(String message, Exception ex = null)
        {
            if (ex != null) message += Environment.NewLine + Environment.NewLine + ex.Message;
            MessageBox.Show(message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Environment.Exit(0);
        }

        private String getRADSDir()
        {
            try
            {
                RegistryKey regKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Riot Games\RADS\");
                return regKey.GetValue("LocalRootFolder").ToString();
            }
            catch (Exception ex)
            {
                error("Unable to get LoL install Directory", ex);
            }
            return "";
        }

        private String getConfigFile()
        {
            try
            {
                String radsDir = getRADSDir();
                String airDir = this.getConfigSetting(radsDir + @"\system\launcher.cfg", "airConfigProject");
                String[] files = Directory.GetFiles(radsDir + @"\projects\" + airDir + @"\releases", @"lol.properties", SearchOption.AllDirectories);
                Array.Sort(files, StringComparer.InvariantCulture);
                return files[files.Length - 1];
            }
            catch (Exception ex)
            {
                error("Error while trying to read config file", ex);
            }
            return "";
        }

        private String getConfigSetting(String path, String setting)
        {
            try
            {
                String content = File.ReadAllText(path);
                Regex myRegex = new Regex(setting + @"=(.*)");
                Match m = myRegex.Match(content);
                if (m.Success)
                {
                    return m.Groups[1].Value.ToString().Trim();
                }
            }
            catch (Exception ex)
            {
                error("Unable to read setting '" + setting + "' from file: " + path, ex);
            }
            return "";
        }

        private void setConfigSetting(String path, String setting, String value)
        {
            try
            {
                String content = File.ReadAllText(path);
                content = Regex.Replace(content, setting + "=.*" + Environment.NewLine, setting + "=" + value + Environment.NewLine);
                File.WriteAllText(path, content);
            }
            catch (Exception ex)
            {
                error("Unable to set setting '" + setting + "' in file: " + path, ex);
            }
        }

        private void createHostBackup()
        {
            try
            {
                String host = getConfigSetting(this.configFile, "xmpp_server_url");
                if (host.Contains("riotgames"))
                {
                    File.WriteAllText(configFileBackup, host);
                }
            }
            catch (Exception ex)
            {
                error("Unable to create host backup file: " + configFileBackup, ex);
            }
        }

        private String getHost()
        {
            return File.ReadAllText(this.configFileBackup).Trim();
        }

        private String getHostIp()
        {
            try
            {
                IPHostEntry hostEntry;
                hostEntry = Dns.GetHostEntry(this.xmppHost);
                if (hostEntry.AddressList.Length > 0)
                {
                    return hostEntry.AddressList[0].ToString();
                }
            }
            catch (Exception ex)
            {
                error("Error when trying to resolve " + this.xmppHost, ex);
            }
            return "";
        }

        private void executeCommand(String cmd)
        {
            Process process = new Process();
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.WindowStyle = ProcessWindowStyle.Hidden;
            startInfo.FileName = "cmd.exe";
            startInfo.Arguments = "/C " + cmd;
            process.StartInfo = startInfo;
            process.Start();
            process.WaitForExit();
        }

        private void goOffLineFirewall()
        {
            try
            {
                executeCommand("netsh advfirewall firewall delete rule name=\"LoLOffline\"");
                executeCommand("netsh advfirewall firewall add rule dir=in name=\"LoLOffline\" action=block enable=yes localip=any remoteip=" + this.xmppIp);
                executeCommand("netsh advfirewall firewall add rule dir=out name=\"LoLOffline\" action=block enable=yes localip=any remoteip=" + this.xmppIp);
            }
            catch (Exception ex)
            {
                error("Unable to set firewall rules", ex);
            }
        }

        private void goOnLineFirewall()
        {
            try
            {
                executeCommand("netsh advfirewall firewall delete rule name=\"LoLOffline\"");
            }
            catch (Exception ex)
            {
                error("Unable to set firewall rules", ex);
            }
        }

        private void goOffLineConfig()
        {
            setConfigSetting(this.configFile, "xmpp_server_url", "localhost");
        }

        private void goOnLineConfig()
        {
            setConfigSetting(this.configFile, "xmpp_server_url", this.xmppHost);
        }

        private void reset()
        {
            try { goOnLineFirewall(); } catch (Exception ex) { };
            try { goOnLineConfig(); } catch (Exception ex) { };
        }

        private void loadSettings()
        {
            rbFirewall.Checked = Properties.Settings.Default.FirewallOrConfig;
            rbConfig.Checked = !Properties.Settings.Default.FirewallOrConfig;
        }

        private void saveSettings()
        {
            Properties.Settings.Default.FirewallOrConfig = rbFirewall.Checked;
            Properties.Settings.Default.Save();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            loadSettings();

            this.configFile = getConfigFile();
            this.configFileBackup = this.configFile + ".loloffline";
            
            createHostBackup();

            this.xmppHost = this.getHost();
            this.xmppIp = this.getHostIp();

            lChatServer.Text = "Chat Server: " + this.xmppHost + " [" + this.xmppIp + "]";

            try { goOnLineFirewall(); } catch (Exception ex) { };
            goOnLineConfig();
        }


        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            saveSettings();
            reset();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            reset();
            if (rbFirewall.Checked) goOffLineFirewall();
            if (rbConfig.Checked) goOffLineConfig();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            reset();
        }
    }
}
