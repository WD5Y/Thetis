﻿/*  frmFinder.cs

This file is part of a program that implements a Software-Defined Radio.

This code/file can be found on GitHub : https://github.com/ramdor/Thetis

Copyright (C) 2020-2024 Richard Samphire MW0LGE

This program is free software; you can redistribute it and/or
modify it under the terms of the GNU General Public License
as published by the Free Software Foundation; either version 2
of the License, or (at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program; if not, write to the Free Software
Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

The author can be reached by email at

mw0lge@grange-lane.co.uk
*/
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.Threading;
using System.IO;
using System.Xml;
using System.Drawing.Drawing2D;
using System.Diagnostics;

namespace Thetis
{
    public partial class frmFinder : Form
    {
        private class SearchData
        {
            public Control Control { get; set; }
            public string Name { get; set; }
            public string FullName { get; set; }
            public string Text { get; set; }
            public string ToolTip { get; set; }
            public string ShortName {  get; set; }
            public string XMLReplacement { get; set; }
        }

        private Dictionary<string, SearchData> _searchData;
        private object _objLocker;
        private object _objWTLocker;
        private Dictionary<string, Thread> _workerThreads;
        private bool _fullDetails;
        private StringFormat _stringFormat;
        Dictionary<string, string> _xmlData = new Dictionary<string, string>();
        private bool _fullName;
        private bool _highlightReusults;

        public frmFinder()
        {
            _objLocker = new object();
            _objWTLocker = new object();
            _searchData = new Dictionary<string, SearchData>();
            _workerThreads = new Dictionary<string, Thread>();
            _fullName = false;
            _xmlData = new Dictionary<string, string>();

            _stringFormat = new StringFormat(StringFormat.GenericTypographic);
            _stringFormat.FormatFlags = StringFormatFlags.MeasureTrailingSpaces | StringFormatFlags.NoWrap | StringFormatFlags.NoClip;

            InitializeComponent();

            // match inital form state
            _highlightReusults = chkHighlight.Checked;
            _fullDetails = chkFullDetails.Checked;
        }
        public void GatherSearchData(Form frm, ToolTip tt)
        {
            if (_workerThreads.ContainsKey(frm.Name)) return;

            Thread worker = new Thread(() =>
            {
                gatherSearchDataThread(frm, tt);
            })
            {
                Name = "Finder Worker Thread for " + frm.Name,
                Priority = ThreadPriority.Highest,
                IsBackground = true,
            };

            lock (_objWTLocker)
            {
                _workerThreads.Add(frm.Name, worker);
            }

            worker.Start();
        }
        private void gatherSearchDataThread(Control frm, ToolTip tt)
        {
            getControlList(frm, tt);

            lock (_objWTLocker)
            {
                _workerThreads.Remove(frm.Name);
            }
        }
        private void getControlList(Control c, ToolTip tt)
        {
            if (c.Controls.Count > 0)
            {
                foreach (Control c2 in c.Controls)
                    getControlList(c2, tt);
            }

            if (c.GetType() == typeof(CheckBoxTS) || c.GetType() == typeof(CheckBox) ||
                c.GetType() == typeof(ComboBoxTS) || c.GetType() == typeof(ComboBox) ||
                c.GetType() == typeof(NumericUpDownTS) || c.GetType() == typeof(NumericUpDown) ||
                c.GetType() == typeof(RadioButtonTS) || c.GetType() == typeof(RadioButton) ||
                c.GetType() == typeof(TextBoxTS) || c.GetType() == typeof(TextBox) ||
                c.GetType() == typeof(TrackBarTS) || c.GetType() == typeof(TrackBar) ||
                c.GetType() == typeof(ColorButton) ||
                c.GetType() == typeof(ucLGPicker) ||
                c.GetType() == typeof(RichTextBox) ||
                c.GetType() == typeof(LabelTS) || c.GetType() == typeof(Label) ||
                c.GetType() == typeof(ButtonTS)// ||
                //c.GetType() == typeof(TabControl) ||
                //c.GetType() == typeof(TabPage)
                )
            {
                string sKey = c.GetFullName();

                bool bAdd = false;
                lock (_objLocker)
                {
                   bAdd = !_searchData.ContainsKey(sKey);
                }

                if (bAdd)
                {
                    string xmlReplace = "";
                    if(_xmlData.ContainsKey(sKey))
                        xmlReplace = _xmlData[sKey];
                    
                    string toolTip = "";
                    if (tt != null)
                        toolTip = tt.GetToolTip(c).Replace("\n", " ");

                    // pull off some junk from control names
                    string sShortName = c.Name;
                    if (sShortName.ToLower().StartsWith("lbl")) sShortName = sShortName.Substring(3);
                    else if (sShortName.ToLower().StartsWith("chk")) sShortName = sShortName.Substring(3);
                    else if (sShortName.ToLower().StartsWith("rad")) sShortName = sShortName.Substring(3);
                    else if (sShortName.ToLower().StartsWith("nud")) sShortName = sShortName.Substring(3);
                    else if (sShortName.ToLower().StartsWith("ud")) sShortName = sShortName.Substring(2);
                    else if (sShortName.ToLower().StartsWith("txt")) sShortName = sShortName.Substring(3);
                    else if (sShortName.ToLower().StartsWith("combo")) sShortName = sShortName.Substring(5);
                    else if (sShortName.ToLower().StartsWith("tb")) sShortName = sShortName.Substring(2);
                    else if (sShortName.ToLower().StartsWith("text")) sShortName = sShortName.Substring(4);
                    else if (sShortName.ToLower().StartsWith("btn")) sShortName = sShortName.Substring(3);
                    else if (sShortName.ToLower().StartsWith("clrbtn")) sShortName = sShortName.Substring(6);
                    else if (sShortName.ToLower().StartsWith("text")) sShortName = sShortName.Substring(4);
                    else if (sShortName.ToLower().StartsWith("label")) sShortName = sShortName.Substring(4);

                    SearchData sd = new SearchData()
                    {
                        Control = c,
                        Name = c.Name,
                        ShortName = sShortName,
                        Text = c.Text.Replace("\n", " "),
                        ToolTip = toolTip,
                        XMLReplacement = xmlReplace,
                        FullName = sKey
                    };

                    lock (_objLocker)
                    {
                        _searchData.Add(sKey, sd);
                    }
                }
            }
        }
        private bool _ignoreUpdateToList = false;
        private void txtSearch_TextChanged(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(txtSearch.Text))
            {
                lstResults.DataSource = null;
                lstResults.Items.Clear();
                return;
            }

            lock (_objLocker)
            {
                string sSearch = txtSearch.Text.ToLower();
                Dictionary<string, SearchData> filteredDictionary = _searchData
                    .Where(kv => 
                    kv.Value.Name.ToLower().Contains(sSearch) ||
                    kv.Value.Text.ToLower().Contains(sSearch) ||
                    kv.Value.ToolTip.ToLower().Contains(sSearch))
                    .ToDictionary(kv => kv.Key, kv => kv.Value);

                List<SearchData> searchDataList = filteredDictionary.Values.ToList();

                _ignoreUpdateToList = true;
                lstResults.DataSource = null;
                lstResults.Items.Clear();
               
                lstResults.DisplayMember = "Text";
                lstResults.DataSource = searchDataList;
                lstResults.ClearSelected();
                _ignoreUpdateToList = false;
            }
        }

        private SearchData _oldSelectedSearchResult = null;
        private void lstResults_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_oldSelectedSearchResult != null)
            {
                //clear old one
                Common.HightlightControl(_oldSelectedSearchResult.Control, false, true);
                _oldSelectedSearchResult = null;
            }

            if (lstResults.SelectedItems.Count == 0 || _ignoreUpdateToList) return;

            SearchData sd = lstResults.SelectedItem as SearchData;
            if (sd != null)
            {
                // take me to your leader
                showControl(sd.Control);

                // highlight this one
                Common.HightlightControl(sd.Control, true, true);
                _oldSelectedSearchResult = sd;
            }
        }

        private void lstResults_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0)
                return;

            ListBox listBox = (ListBox)sender;
            SearchData sd = listBox.Items[e.Index] as SearchData;

            Graphics g = e.Graphics;

            // Set the background color and text color
            Color backgroundColor = e.State.HasFlag(DrawItemState.Selected) ? SystemColors.Highlight : SystemColors.Window;
            Color textColor = e.State.HasFlag(DrawItemState.Selected) ? SystemColors.HighlightText : SystemColors.ControlText;

            // Fill the background
            if (!e.State.HasFlag(DrawItemState.Selected)) {
                backgroundColor = e.Index % 2 == 0 ? backgroundColor : applyTint(backgroundColor, Color.LightGray);
            }
            g.FillRectangle(new SolidBrush(backgroundColor), e.Bounds);

            int yPos = e.Bounds.Y;

            if (_fullDetails)
            {
                string sSearchLower = txtSearch.Text.ToLower();
                if (!string.IsNullOrEmpty(sd.Text))
                {
                    highlight(sSearchLower, sd.Text, listBox, e.Bounds.X, yPos, g);
                    g.DrawString(sd.Text, listBox.Font, new SolidBrush(textColor), e.Bounds.X, yPos, _stringFormat);
                    yPos += 20;
                }

                if (_xmlData.ContainsKey(sd.FullName.ToLower()))
                {
                    string sText = _xmlData[sd.FullName.ToLower()];
                    highlight(sSearchLower, sText, listBox, e.Bounds.X, yPos, g);
                    g.DrawString(sText, listBox.Font, new SolidBrush(textColor), e.Bounds.X, yPos, _stringFormat);
                    yPos += 20;
                }
                else if (!string.IsNullOrEmpty(sd.ToolTip))
                {
                    highlight(sSearchLower, sd.ToolTip, listBox, e.Bounds.X, yPos, g);
                    g.DrawString(sd.ToolTip, listBox.Font, new SolidBrush(textColor), e.Bounds.X, yPos, _stringFormat);
                    yPos += 20;
                }

                highlight(sSearchLower, _fullName ? sd.FullName : sd.Name, listBox, e.Bounds.X, yPos, g);
                g.DrawString(_fullName ? sd.FullName : sd.Name, listBox.Font, new SolidBrush(textColor), e.Bounds.X, yPos, _stringFormat);
            }
            else
            {
                string sText;
                if (_xmlData.ContainsKey(sd.FullName.ToLower()))
                {
                    sText = _xmlData[sd.FullName.ToLower()];
                }
                else if (!string.IsNullOrEmpty(sd.ToolTip))
                {
                    sText = sd.ToolTip;
                }
                //else if (!string.IsNullOrEmpty(sd.Text))
                //{
                //    sText = sd.Text;
                //}
                else
                {
                    string sTextAddition = "";
                    if (!string.IsNullOrEmpty(sd.Text))
                        sTextAddition = " [" + sd.Text + "]";
                    sText = sd.ShortName + sTextAddition;
                }
                highlight(txtSearch.Text.ToLower(), sText, listBox, e.Bounds.X, e.Bounds.Y, g);
                g.DrawString(sText, listBox.Font, new SolidBrush(textColor), e.Bounds.X, yPos, _stringFormat);
            }

            //g.DrawRectangle(Pens.Gray, e.Bounds.X, e.Bounds.Y, e.Bounds.Width - 1, e.Bounds.Height - 1);
        }
        private void highlight(string sSearchText, string sLineText, ListBox listBox, int xPos, int yPos, Graphics g)
        {
            if (!_highlightReusults) return;

            List<Tuple<int, int>> lst = findSubstringOccurrences(sLineText.ToLower(), txtSearch.Text.ToLower());
            foreach (Tuple<int, int> t in lst)
            {
                float start = g.MeasureString(sLineText.Substring(0, t.Item1), listBox.Font, int.MaxValue, _stringFormat).Width;
                float width = g.MeasureString(sLineText.Substring(t.Item1, txtSearch.Text.Length), listBox.Font, int.MaxValue, _stringFormat).Width;
                Rectangle newRect = new Rectangle(xPos + (int)start, yPos, (int)(width), 20);
                CompositingMode oldMode = g.CompositingMode;
                g.CompositingMode = CompositingMode.SourceOver;
                g.FillRectangle(new SolidBrush(Color.FromArgb(102,Color.Yellow)), newRect);
                g.CompositingMode = oldMode;
            }
        }
        private List<Tuple<int, int>> findSubstringOccurrences(string inputString, string searchString)
        {
            List<Tuple<int, int>> occurrences = new List<Tuple<int, int>>();
            int index = 0;

            while (index < inputString.Length)
            {
                index = inputString.IndexOf(searchString, index, StringComparison.Ordinal);

                if (index == -1)
                {
                    break;
                }

                int startIndex = index;
                int endIndex = index + searchString.Length - 1;

                occurrences.Add(new Tuple<int, int>(startIndex, endIndex));

                index += searchString.Length;
            }

            return occurrences;
        }
        private Color applyTint(Color baseColor, Color tintColor)
        {
            int r = (baseColor.R + tintColor.R) / 2;
            int g = (baseColor.G + tintColor.G) / 2;
            int b = (baseColor.B + tintColor.B) / 2;

            return Color.FromArgb(r, g, b);
        }

        private void frmFinder_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                this.Hide();
                e.Cancel = true;
            }

            txtSearch.Text = "";
            Common.SaveForm(this, this.Name);
        }
        public new void Show()
        {
            Common.RestoreForm(this, this.Name, true);
            if (string.IsNullOrEmpty(txtSearch.Text)) txtSearch.Text = "Search";
            base.Show();
            txtSearch.Focus();
        }

        private void lstResults_MeasureItem(object sender, MeasureItemEventArgs e)
        {
            if (e.Index < 0)
                return;

            ListBox listBox = (ListBox)sender;
            SearchData sd = listBox.Items[e.Index] as SearchData;

            int height = 20;

            if (sd != null && _fullDetails)
            {
                if (!string.IsNullOrEmpty(sd.Text)) height += 20;
                if (!string.IsNullOrEmpty(sd.ToolTip)) height += 20;
            }

            e.ItemHeight = height;
        }

        private void showControl(Control c)
        {
            if(c == null) return;

            Form f = c.FindForm();
            if(f == null) return;

            if(!f.Visible)
                f.Show();

            f.BringToFront();           

            selectRequiredTabs(f, c);

            this.SuspendLayout();
            this.BringToFront();
            lstResults.Focus();
            this.ResumeLayout();
        }

        private void selectRequiredTabs(Control parentControl, Control targetControl)
        {
            Control currentControl = targetControl;
            while (currentControl != parentControl)
            {
                currentControl = currentControl.Parent;
                if (currentControl is TabPage tabPage)
                {
                    if (tabPage.Parent is TabControl tabControl)
                    {
                        tabControl.SelectedTab = tabPage;
                    }
                }
            }
        }

        private void chkFullDetails_CheckedChanged(object sender, EventArgs e)
        {
            lock (_objLocker)
            {
                SearchData sd = lstResults.SelectedItem as SearchData;
                _fullDetails = chkFullDetails.Checked;
                txtSearch_TextChanged(this, EventArgs.Empty);
                if (sd != null)
                    lstResults.SelectedItem = sd;
            }
        }

        public void ReadXmlFinderFile(string directoryPath)
        {
            string filePath = Path.Combine(directoryPath, "Finder.xml");

            _xmlData.Clear();

            if (!File.Exists(filePath)) return;

            try
            {
                XmlDocument xmlDoc = new XmlDocument();
                xmlDoc.Load(filePath);                

                XmlNodeList nodes = xmlDoc.SelectNodes("//element");
                foreach (XmlNode node in nodes)
                {
                    string controlName = node.SelectSingleNode("control")?.InnerText;
                    string text = node.SelectSingleNode("text")?.InnerText;

                    if (!string.IsNullOrEmpty(controlName) && !string.IsNullOrEmpty(text))
                    {
                        _xmlData.Add(controlName.ToLower(), text); // to lower, make life easier in the xml
                    }
                }
            }
            catch { }
        }

        public void WriteXmlFinderFile(string directoryPath)
        {
            string filePath = Path.Combine(directoryPath, "Finder.xml");

            try
            {
                if (File.Exists(filePath)) return; // only do if not there

                int tries = 0;
                bool workersWorking = false;
                lock (_objWTLocker)
                {
                    workersWorking = _workerThreads.Count > 0;
                }
                while (workersWorking)
                {
                    Thread.Sleep(50);
                    lock (_objWTLocker)
                    {
                        workersWorking = _workerThreads.Count > 0;
                    }
                    tries++;
                    if (tries > 200) return; // give up after 10 seconds total time. If not done in 10 seconds then time to buy a new cpu !
                }

                XmlDocument xmlDoc = new XmlDocument();
                XmlDeclaration xmlDeclaration = xmlDoc.CreateXmlDeclaration("1.0", "UTF-8", null);
                xmlDoc.AppendChild(xmlDeclaration);

                XmlElement rootElement = xmlDoc.CreateElement("root");
                xmlDoc.AppendChild(rootElement);

                lock (_objLocker)
                {
                    foreach (KeyValuePair<string, SearchData> kvp in _searchData)
                    {
                        XmlElement elementElement = xmlDoc.CreateElement("element");
                        XmlElement controlElement = xmlDoc.CreateElement("control");
                        controlElement.InnerText = kvp.Key;
                        elementElement.AppendChild(controlElement);
                        XmlElement textElement = xmlDoc.CreateElement("text");
                        textElement.InnerText = null;
                        elementElement.AppendChild(textElement);
                        rootElement.AppendChild(elementElement);
                    }
                }

                xmlDoc.Save(filePath);
            }
            catch {}
        }
        private void frmFinder_KeyDown(object sender, KeyEventArgs e)
        {
            // alt key to show additional info when in full details mode
            // ctrl c to copy full control name to clipboard
            if(e.Alt && _fullDetails)
            {
                lock (_objLocker)
                {
                    _fullName = !_fullName;
                    SearchData sd = lstResults.SelectedItem as SearchData;
                    txtSearch_TextChanged(this, EventArgs.Empty);
                    if (sd != null)
                        lstResults.SelectedItem = sd;
                }
                e.Handled = true;
            }
            else if (e.Control && e.KeyCode == Keys.C)
            {
                SearchData sd = lstResults.SelectedItem as SearchData;
                if(sd != null)
                {
                    try
                    {
                        Clipboard.SetText(sd.FullName);
                        e.Handled = true;
                    }
                    catch { }
                }
            }
        }

        private void chkHighlight_CheckedChanged(object sender, EventArgs e)
        {
            lock (_objLocker)
            {
                SearchData sd = lstResults.SelectedItem as SearchData;
                _highlightReusults = chkHighlight.Checked;                
                txtSearch_TextChanged(this, EventArgs.Empty);
                if(sd != null)
                    lstResults.SelectedItem = sd; 
            }
        }
    }
}
