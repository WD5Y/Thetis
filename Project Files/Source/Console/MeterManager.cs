﻿/*  MeterManager.cs

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
using System.Collections.Concurrent;
using System.Linq;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.IO;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Security.Cryptography;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using System.Xml;
using System.Threading.Tasks;
using System.ComponentModel;
using System.IO.Ports;

//directX
using SharpDX;
using SharpDX.Direct2D1;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using SharpDX.Mathematics.Interop;
using RawInput_dll;

namespace Thetis
{
    // this enum is similar to MeterRXMode & MeterTXMode
    [Serializable]
    public enum Reading
    {
        NONE = -1,
        //RX
        SIGNAL_STRENGTH = 0,
        AVG_SIGNAL_STRENGTH,
        ADC_PK,
        ADC_AV,
        AGC_PK,
        AGC_AV,
        AGC_GAIN,
        ESTIMATED_PBSNR,
        //TX
        MIC,
        PWR,
        ALC,
        EQ,
        LEVELER,
        COMP,
        //CPDR, //CPDR is the same as comp
        ALC_G,
        ALC_GROUP,
        LVL_G,
        MIC_PK,
        ALC_PK,
        EQ_PK,
        LEVELER_PK,
        COMP_PK,
        //CPDR_PK, //CPDR is the same as comp
        CFC_PK = 24,
        CFC_AV,
        CFC_G,

        //additional to MeterRXMode & MeterTXMode
        REVERSE_PWR,
        SWR,

        //pa
        DRIVE_FWD_ADC,
        FWD_ADC,
        REV_ADC,
        DRIVE_PWR,
        PA_FWD_PWR,
        PA_REV_PWR,
        CAL_FWD_PWR,
        REV_VOLT,
        FWD_VOLT,

        // volts/amps
        VOLTS,
        AMPS,
        AZ,
        ELE,

        // special
        //EYE_PERCENT,
        //// private used in metermanager only
        //VFOA,
        //VFOB,
        //VFOSUB,
        //DSPMODE,
        //BANDVFOA,
        //BANDVFOB,
        //SPLIT,
        //TXVFOB,

        //special these are not floats, only used by clsDataOut
        VFOA_FREQ,
        VFOB_FREQ,
        VFOSUBA_FREQ,
        TX_FREQ,
        TIME_UTC,
        DATE_UTC,
        TIME_LOC,
        DATE_LOC,
        VFOA_BAND,
        VFOB_BAND,
        VFOSUBA_BAND,
        VFOA_FILTER_NAME,
        VFOB_FILTER_NAME,
        TX_BAND,
        VFOA_MODE,
        VFOB_MODE,
        SPLIT,
        RX2_ENABLED,
        VFOB_TX,
        SUB_RX,
        //

        LAST
    }

    public enum MeterType
    {
        NONE = 0,
        //rx
        SIGNAL_STRENGTH,
        AVG_SIGNAL_STRENGTH,
        SIGNAL_TEXT,
        ADC,
        AGC,
        AGC_GAIN,
        ESTIMATED_PBSNR,
        //tx
        MIC,
        EQ,
        LEVELER,
        LEVELER_GAIN,
        ALC,
        ALC_GAIN,
        ALC_GROUP,
        CFC,
        CFC_GAIN,
        COMP,
        //--
        PWR,
        REVERSE_PWR,
        SWR,
        //CPDR, //CPDR is the same as comp
        //special
        MAGIC_EYE,
        ANANMM,
        CROSS,
        //HISTORY,
        VFO_DISPLAY,
        CLOCK,
        SPACER,
        TEXT_OVERLAY,
        DATA_OUT,
        ROTATOR,
        LED,
        WEB_IMAGE,
        //SPECTRUM,
        LAST
    }
    public class Globals
    {
        public Dictionary<string, object> Variables;
    }
    internal static class MeterManager
    {
        #region MeterManager
        // member variables
        private static Console _console;
        private static bool _delegatesAdded;
        private static bool _finishedSetup;
        //private static bool _power; //not used yet
        private static Dictionary<int, clsReadings> _readings;
        private static Dictionary<string, clsMeter> _meters;
        private static Dictionary<int, bool> _readingIgnore;
        private static Thread _meterThread;
        private static bool _meterThreadRunning;
        //private static object _readingsLock = new object();        
        private static bool _rx1VHForAbove;
        private static bool _rx2VHForAbove;

        private static HPSDRModel _currentHPSDRmodel;
        private static bool _alexPresent;
        private static bool _paPresent;
        private static bool _apolloPresent;
        private static int _transverterIndex;

        private static Object _imageLock = new Object();
        private static Object _metersLock = new Object();

        private static Dictionary<string, DataStream> _pooledStreamData;
        private static Dictionary<string, System.Drawing.Bitmap> _pooledImages;

        private static string _openHPSDR_appdatapath;

        private static CustomReadings _custom_readings;

        private static ImageFetcher _image_fetcher;

        //public static float[] _newSpectrumPassband;
        //public static float[] _currentSpectrumPassband;
        //private static bool _spectrumReady;

        internal class CustomReadings
        {
            private ConcurrentDictionary<Reading, float> _readings_values;
            private ConcurrentDictionary<string, object> _readings_text_objects;

            public CustomReadings()
            {
                _readings_values = new ConcurrentDictionary<Reading, float>();
                _readings_text_objects = new ConcurrentDictionary<string, object>();
            }
            private string formatNumber(double number)
            {
                string numberString = number.ToString("F6", CultureInfo.InvariantCulture);
                int decimalPointIndex = numberString.IndexOf('.');
                if (decimalPointIndex != -1 && numberString.Length > decimalPointIndex + 3)
                {
                    numberString = numberString.Insert(decimalPointIndex + 4, ".");
                }
                return numberString;
            }
            private string formatElapsedTimeCompact(long elapsedSeconds)
            {
                long seconds = elapsedSeconds % 60;
                long minutes = (elapsedSeconds / 60) % 60;
                long hours = (elapsedSeconds / 3600) % 24;
                long days = (elapsedSeconds / 86400) % 365;
                long years = elapsedSeconds / 31536000;

                if (years > 0)
                {
                    return $"{years}yr {hours:D2}:{minutes:D2}:{seconds:D2}";
                }
                else if (days > 0)
                {
                    return $"{days}d {hours:D2}:{minutes:D2}:{seconds:D2}";
                }
                else
                {
                    return $"{hours:D2}:{minutes:D2}:{seconds:D2}";
                }
            }
            private string formatElapsedTime(long elapsedSeconds)
            {
                long seconds = elapsedSeconds % 60;
                long minutes = (elapsedSeconds / 60) % 60;
                long hours = (elapsedSeconds / 3600) % 24;
                long days = (elapsedSeconds / 86400) % 365;
                long years = elapsedSeconds / 31536000;

                if (years > 0)
                {
                    return $"{years} year{(years > 1 ? "s" : "")} {days} day{(days > 1 ? "s" : "")} {hours} hour{(hours > 1 ? "s" : "")}";
                }
                else if (days > 0)
                {
                    return $"{days} day{(days > 1 ? "s" : "")} {hours} hour{(hours > 1 ? "s" : "")} {minutes} minute{(minutes > 1 ? "s" : "")}";
                }
                else if (hours > 0)
                {
                    return $"{hours} hour{(hours > 1 ? "s" : "")} {minutes} minute{(minutes > 1 ? "s" : "")} {seconds} second{(seconds > 1 ? "s" : "")}";
                }
                else if (minutes > 0)
                {
                    return $"{minutes} minute{(minutes > 1 ? "s" : "")} {seconds} second{(seconds > 1 ? "s" : "")}";
                }
                else
                {
                    return $"{seconds} second{(seconds > 1 ? "s" : "")}";
                }
            }
            public List<string> GetPlaceholders(string text)
            {
                List<string> result = new List<string>();
                int length = text.Length;
                for (int i = 0; i < length; i++)
                {
                    if (text[i] == '%')
                    {
                        if (i + 1 < length) // Ensure there's at least one character after the current %
                        {
                            int end = text.IndexOf('%', i + 1);
                            if (end != -1)
                            {
                                if (end - i > 1) // Ensure there's something between the %
                                {
                                    result.Add(text.Substring(i + 1, end - i - 1));
                                }
                                i = end;
                            }
                        }
                    }
                }
                return result;
            }
            public void TakeReading(int rx, Reading reading)
            {
                if (_readings_values.ContainsKey(reading))
                {
                    _readings_values[reading] = getReading(rx, reading, true);
                }
            }
            public bool IsCustomString(string custom)
            {
                bool bRet = false;
                switch (custom)
                {
                    case "time_utc":
                    case "time_utc_int":
                    case "time_loc":
                    case "time_loc_int":
                    case "date_utc":
                    case "date_utc_int":
                    case "date_loc":
                    case "date_loc_int":
                    case "vfoa":
                    case "vfob":
                    case "vfoasub":
                    case "vfoa_double":
                    case "vfob_double":
                    case "vfoasub_double":
                    case "band_vfoa":
                    case "band_vfob":
                    case "band_vfoasub":
                    case "mode_vfoa":
                    case "mode_vfob":
                    case "subrx":
                    case "filter_vfoa":
                    case "filter_vfob":
                    case "filter_vfoa_name":
                    case "filter_vfob_name":
                    case "split":
                    case "qso_time":
                    case "qso_time_short":
                    case "qso_time_int":
                    case "tb_qso_time":
                    case "tb_qso_time_short":
                    case "tb_qso_time_int":
                    case "mox":
                    case "cfc":
                    case "comp":
                    case "lev":
                    case "rx2":
                    case "tx_eq":
                        bRet = true;
                        break;
                }
                return bRet;
            }
            public object GetReading(string reading, clsMeter owningMeter, int rx)
            {
                if (!IsCustomString(reading.ToLower()))
                {
                    bool ok = Enum.TryParse<Reading>(reading.ToUpper(), out Reading tmpReading);
                    if (ok)
                    {
                        ok = _readings_values.TryGetValue(tmpReading, out float value);
                        if (ok)
                        {
                            _readings[rx].UseReading(tmpReading);
                            return value;
                        }
                        else
                        {
                            return 0;
                        }
                    }
                }

                // additional string based custom readings such as time_utc etc, we can just update these
                // as they are obtained
                DateTime now = DateTime.Now;
                DateTime UTCnow = DateTime.UtcNow;
                string key = reading.ToLower();
                switch (key)
                {
                    case "time_utc":
                        _readings_text_objects[key] = UTCnow.ToString("HH:mm:ss");
                        break;
                    case "time_loc":
                        _readings_text_objects[key] = now.ToString("HH:mm:ss");
                        break;
                    case "date_utc":
                        _readings_text_objects[key] = UTCnow.ToString("ddd d MMM yyyy");
                        break;
                    case "date_loc":
                        _readings_text_objects[key] = now.ToString("ddd d MMM yyyy");
                        break;
                    case "time_utc_int":
                        _readings_text_objects[key] = (int)(UTCnow.Hour * 10000 + UTCnow.Minute * 100 + UTCnow.Second);
                        break;
                    case "time_loc_int":
                        _readings_text_objects[key] = (int)(now.Hour * 10000 + now.Minute * 100 + now.Second);
                        break;
                    case "date_utc_int":
                        _readings_text_objects[key] = (int)(UTCnow.Hour * 10000 + UTCnow.Minute * 100 + UTCnow.Second);
                        break;
                    case "date_loc_int":
                        _readings_text_objects[key] = (int)(now.Hour * 10000 + now.Minute * 100 + now.Second);
                        break;
                    case "vfoa":
                        if (rx == 1)
                            _readings_text_objects[key] = formatNumber(owningMeter.VfoA);
                        else
                            _readings_text_objects[key] = "";
                        break;
                    case "vfob":
                        if (owningMeter.RX2Enabled && rx == 1)
                            _readings_text_objects[key] = "";
                        else
                            _readings_text_objects[key] = formatNumber(owningMeter.VfoB);
                        break;
                    case "vfoasub":
                        if (owningMeter.VfoSub >= 0 && rx == 1 && owningMeter.RX2Enabled && (owningMeter.Split || owningMeter.MultiRxEnabled)) // when -999.999
                            _readings_text_objects[key] = formatNumber(owningMeter.VfoSub);
                        else
                            _readings_text_objects[key] = "";
                        break;
                    case "vfoa_double":
                        if (rx == 1)
                            _readings_text_objects[key] = Math.Round(owningMeter.VfoA, 6);
                        else
                            _readings_text_objects[key] = "";
                        break;
                    case "vfob_double":
                        if (owningMeter.RX2Enabled && rx == 1)
                            _readings_text_objects[key] = "";
                        else
                            _readings_text_objects[key] = Math.Round(owningMeter.VfoB, 6);
                        break;
                    case "vfoasub_double":
                        if (owningMeter.VfoSub >= 0 && rx == 1 && owningMeter.RX2Enabled && (owningMeter.Split || owningMeter.MultiRxEnabled)) // when -999.999
                            _readings_text_objects[key] = Math.Round(owningMeter.VfoSub, 6);
                        else
                            _readings_text_objects[key] = "";
                        break;
                    case "band_vfoa":
                        _readings_text_objects[key] = BandStackManager.BandToString(owningMeter.BandVfoA).ToLower();
                        break;
                    case "band_vfob":
                        _readings_text_objects[key] = BandStackManager.BandToString(owningMeter.BandVfoA).ToLower();
                        break;
                    case "band_vfoasub":
                        _readings_text_objects[key] = BandStackManager.BandToString(owningMeter.BandVfoASub).ToLower();
                        break;
                    case "mode_vfoa":
                        _readings_text_objects[key] = owningMeter.ModeVfoA.ToString();
                        break;
                    case "mode_vfob":
                        _readings_text_objects[key] = owningMeter.ModeVfoB.ToString();
                        break;
                    case "subrx":
                        _readings_text_objects[key] = owningMeter.MultiRxEnabled ? "SubRX" : "";
                        break;
                    case "filter_vfoa":
                        _readings_text_objects[key] = owningMeter.FilterVfoA.ToString();
                        break;
                    case "filter_vfob":
                        _readings_text_objects[key] = owningMeter.FilterVfoB.ToString();
                        break;
                    case "filter_vfoa_name":
                        _readings_text_objects[key] = owningMeter.FilterVfoAName;
                        break;
                    case "filter_vfob_name":
                        _readings_text_objects[key] = owningMeter.FilterVfoBName;
                        break;
                    case "split":
                        _readings_text_objects[key] = owningMeter.Split ? (owningMeter.QuickSplitEnabled ? "QSPLIT" : "SPLIT") : "";
                        break;
                    case "qso_time":
                        _readings_text_objects[key] = formatElapsedTime(owningMeter.QsoDurationSeconds);
                        break;
                    case "qso_time_short":
                        _readings_text_objects[key] = formatElapsedTimeCompact(owningMeter.QsoDurationSeconds);
                        break;
                    case "tb_qso_time":
                        _readings_text_objects[key] = _console.QSOTimerEnabled ? formatElapsedTime(_console.QSOTimerSeconds) : "";
                        break;
                    case "tb_qso_time_short":
                        _readings_text_objects[key] = _console.QSOTimerEnabled ? formatElapsedTimeCompact(_console.QSOTimerSeconds) : "";
                        break;
                    case "qso_time_int":
                        {
                            TimeSpan time = TimeSpan.FromSeconds(owningMeter.QsoDurationSeconds);
                            int hours = time.Hours;
                            int minutes = time.Minutes;
                            int seconds = time.Seconds;
                            _readings_text_objects[key] = (int)(hours * 10000 + minutes * 100 + seconds);
                        }
                        break;
                    case "tb_qso_time_int":
                        {
                            TimeSpan time = TimeSpan.FromSeconds(_console.QSOTimerSeconds);
                            int hours = time.Hours;
                            int minutes = time.Minutes;
                            int seconds = time.Seconds;
                            _readings_text_objects[key] = (int)(hours * 10000 + minutes * 100 + seconds);
                        }
                        break;
                    case "mox":
                        _readings_text_objects[key] = owningMeter.MOX ? "MOX" : "";
                        break;
                    case "cfc":
                        _readings_text_objects[key] = owningMeter.CFCEnabled ? "CFC" : "";
                        break;
                    case "comp":
                        _readings_text_objects[key] = owningMeter.CompandEnabled ? "COMP" : "";
                        break;
                    case "lev":
                        _readings_text_objects[key] = owningMeter.LevelerEnabled ? "LEVELER" : "";
                        break;
                    case "rx2":
                        _readings_text_objects[key] = owningMeter.RX2Enabled ? "RX2 On" : "";
                        break;
                    case "tx_eq":
                        _readings_text_objects[key] = owningMeter.TXEQEnabled ? "TXEQ" : "";
                        break;
                }

                if (_readings_text_objects.ContainsKey(key))
                {
                    bool ok = _readings_text_objects.TryGetValue(key, out object tmp);
                    if (ok)
                        return tmp;
                    else
                        return "";
                }
                return "";
            }
            public void UpdateReadings(string text)
            {
                //add readings required
                addReading(Reading.SWR, text);
                addReading(Reading.SIGNAL_STRENGTH, text);
                addReading(Reading.AVG_SIGNAL_STRENGTH, text);
                addReading(Reading.PWR, text);
                addReading(Reading.REVERSE_PWR, text);
                addReading(Reading.MIC, text);
                addReading(Reading.MIC_PK, text);
                addReading(Reading.ADC_PK, text);
                addReading(Reading.ADC_AV, text);
                addReading(Reading.AGC_PK, text);
                addReading(Reading.AGC_AV, text);
                addReading(Reading.AGC_GAIN, text);
                addReading(Reading.LEVELER, text);
                addReading(Reading.LEVELER_PK, text);
                addReading(Reading.LVL_G, text);
                addReading(Reading.ALC, text);
                addReading(Reading.ALC_PK, text);
                addReading(Reading.ALC_G, text);
                addReading(Reading.ALC_GROUP, text);
                addReading(Reading.CFC_AV, text);
                addReading(Reading.CFC_PK, text);
                addReading(Reading.CFC_G, text);
                addReading(Reading.COMP, text);
                addReading(Reading.COMP_PK, text);
                addReading(Reading.ESTIMATED_PBSNR, text);
                addReading(Reading.VOLTS, text);
                addReading(Reading.AMPS, text);

                addReadingText("time_utc", text);
                addReadingText("time_loc", text);
                addReadingText("date_utc", text);
                addReadingText("date_loc", text);
                addReadingText("time_utc_int", text);
                addReadingText("time_loc_int", text);
                addReadingText("date_utc_int", text);
                addReadingText("date_loc_int", text);
                addReadingText("vfoa", text);
                addReadingText("vfob", text);
                addReadingText("vfoasub", text);
                addReadingText("vfoa_double", text);
                addReadingText("vfob_double", text);
                addReadingText("vfoasub_double", text);
                addReadingText("band_vfoa", text);
                addReadingText("band_vfob", text);
                addReadingText("band_vfoasub", text);
                addReadingText("mode_vfoa", text);
                addReadingText("mode_vfob", text);
                addReadingText("subrx", text);
                addReadingText("filter_vfoa", text);
                addReadingText("filter_vfob", text);
                addReadingText("filter_vfoa_name", text);
                addReadingText("filter_vfob_name", text);
                addReadingText("split", text);
                addReadingText("qso_time", text);
                addReadingText("qso_time_short", text);
                addReadingText("qso_time_int", text);
                addReadingText("tb_qso_time", text);
                addReadingText("tb_qso_time_short", text);
                addReadingText("tb_qso_time_int", text);
                addReadingText("mox", text);
                addReadingText("cfc", text);
                addReadingText("comp", text);
                addReadingText("lev", text);
                addReadingText("rx2", text);
                addReadingText("tx_eq", text);
            }
            private void addReading(Reading reading, string text)
            {
                if (!_readings_values.ContainsKey(reading) && text.Contains("%" + reading.ToString().ToLower() + "%")) _readings_values.TryAdd(reading, 0f);
            }
            private void addReadingText(string reading, string text)
            {
                if (!_readings_text_objects.ContainsKey(reading) && text.Contains("%" + reading + "%")) _readings_text_objects.TryAdd(reading, "");
            }
        }
        public class clsIGSettings
        {
            // TODO change all these unique settings to a Dictionary collection   _settings = new Dictionary<string, object>
            private ConcurrentDictionary<string, object> _settings;
            //

            private int _updateInterval;
            private float _decay;
            private float _attack;
            private int _historyDuration;
            private bool _shadow;
            private bool _showHistory;
            private System.Drawing.Color _historyColor;
            private bool _peakHold;
            private System.Drawing.Color _peakHoldMarkerColor;
            private System.Drawing.Color _lowColor;
            private System.Drawing.Color _highColor;
            private System.Drawing.Color _titleColor;
            private Reading _readingSource;
            private System.Drawing.Color _colour;
            private System.Drawing.Color _markerColour;
            private System.Drawing.Color _subMarkerColour;
            private clsBarItem.BarStyle _barStyle;
            private string _text_1;
            private string _text_2;
            private bool _fadeOnRx;
            private bool _fadeOnTx;
            private bool _showType;
            private System.Drawing.Color _segmentedSolidLowColour;
            private System.Drawing.Color _segmentedSolidHighColour;
            private bool _peakValue;
            private System.Drawing.Color _peakValueColour;
            private float _eyeScale;
            private float _spacerPadding;
            private bool _back_panel;
            private float _eyeBezelScale;
            private bool _average;
            private string _text_overlay;
            private bool _darkMode;
            private float _maxPower;
            private System.Drawing.Color _powerScaleColour;
            private clsBarItem.Units _units;
            private bool _showMarker;
            private bool _showSubMarker;
            private bool _hasSubIndicators;
            private int _ignoreHistoryDuration;
            private string _font_family_1;
            private FontStyle _font_style_1;
            private float _font_size_1;
            private string _font_family_2;
            private FontStyle _font_style_2;
            private float _font_size_2;

            private Guid[] _mmio_guid;
            private string[] _mmio_variable;

            public clsIGSettings()
            {
                //
                _settings = new ConcurrentDictionary<string, object>();
                //

                _hasSubIndicators = false;
                _readingSource = Reading.NONE;
                _barStyle = clsBarItem.BarStyle.None;
                _maxPower = CurrentPowerRating;
                _units = clsBarItem.Units.DBM;
                _ignoreHistoryDuration = 2000;
                _historyDuration = 50;
                _text_1 = "";
                _text_2 = "";
                _font_family_1 = "";
                _font_family_2 = "";

                _mmio_guid = new Guid[10];
                _mmio_variable = new string[10];

                for (int i = 0; i < _mmio_guid.Length; i++)
                {
                    _mmio_guid[i] = Guid.Empty;
                    _mmio_variable[i] = "--DEFAULT--";
                }
            }

            public void SetSetting(string setting, object value)
            {
                if (_settings.ContainsKey(setting))
                    _settings[setting] = value;
                else
                    _settings.TryAdd(setting, value);
            }
            public object GetSetting(string setting, Type type)
            {
                if (_settings.ContainsKey(setting))
                    return _settings[setting];
                else
                {
                    if (type == typeof(int))
                        return (int)0;
                    else if (type == typeof(float))
                        return (float)0;
                    else if (type == typeof(double))
                        return (double)0;
                    else if (type == typeof(bool))
                        return (bool)false;
                    else if (type == typeof(System.Drawing.Color))
                        return System.Drawing.Color.Gray;
                    else if (type == typeof(Guid))
                        return Guid.Empty;
                    else if (type == typeof(Reading))
                        return Reading.NONE;
                    else if (type == typeof(clsBarItem.BarStyle))
                        return clsBarItem.BarStyle.None;
                    else if (type == typeof(clsBarItem.Units))
                        return clsBarItem.Units.DBM;
                    else if (type == typeof(FontStyle))
                        return FontStyle.Regular;
                    else
                        return "";
                }
            }
            public string ToString2()
            {
                try
                {
                    return "1|" + Common.SerializeToBase64<ConcurrentDictionary<string, object>>(_settings); // 1| signifies version 1 of the serialize for future proofing
                }
                catch
                {
                    return "";
                }
                //string ret = "";

                //foreach(KeyValuePair<string, object> pair in _settings)
                //{
                //    object val = pair.Value;

                //    ret += pair.Key + "|";

                //    ret += val.GetType().AssemblyQualifiedName + "|";

                //    if (val is int i)
                //    {
                //        ret += i.ToString() + "|";
                //    }
                //    else if (val is System.Drawing.Color clr)
                //    {
                //        ret += ColorTranslator.ToHtml(clr) + "|";
                //    }
                //    else if (val is float flt)
                //    {
                //        ret += flt.ToString("f2") + "|";
                //    }
                //    else if (val is double dbl)
                //    {
                //        ret += dbl.ToString("f4") + "|";
                //    }
                //    else if (val is bool bl)
                //    {
                //        ret += bl.ToString().ToLower() + "|";
                //    }
                //    else
                //    {
                //        ret += (val.ToString()).Replace("|", "_+>>++<<+_") + "|";
                //    }
                //}

                //if (!string.IsNullOrEmpty(ret))
                //{
                //    //drop last |
                //    ret = ret.Substring(0, ret.Length - 1);
                //}

                //return ret;
            }
            public bool TryParse2(string str)
            {
                try
                {
                    string[] parts = str.Split('|');
                    if (parts.Length != 2) return false;

                    if (parts[0] == "1") // 1 signifies version 1 of the serialize for future proofing
                        _settings = Common.DeserializeFromBase64<ConcurrentDictionary<string, object>>(parts[1]);

                    return true;
                }
                catch
                {
                    return false;
                }

                //bool ok = true;

                //string[] settings = str.Split('|');
                //if (settings.Length < 3 && settings.Length % 3 != 0) ok = false;

                //if(ok)
                //{
                //    for (int i = 0; i < settings.Length; i += 3) 
                //    { 
                //        string setting = settings[i];
                //        string type = settings[i + 1];
                //        string val = settings[i + 2];

                //        Type tpe = Type.GetType(type);
                //        object typedValue = Common.ConvertToType(val, tpe);

                //        SetSetting(setting, typedValue);
                //    }
                //}

                //return ok;
            }

            public override string ToString()
            {
                string sRet = _updateInterval.ToString() + "|" +
                    _decay.ToString("f4") + "|" +
                    _attack.ToString("f4") + "|" +
                    _historyDuration.ToString() + "|" +
                    _shadow.ToString() + "|" +
                    _showHistory.ToString() + "|" +
                    Common.ColourToString(_historyColor) + "|" +
                    _peakHold.ToString() + "|" +
                    Common.ColourToString(_peakHoldMarkerColor) + "|" +
                    Common.ColourToString(_lowColor) + "|" +
                    Common.ColourToString(_highColor) + "|" +
                    Common.ColourToString(_titleColor) + "|" +
                    _readingSource.ToString() + "|" +
                    Common.ColourToString(_colour) + "|" +
                    Common.ColourToString(_markerColour) + "|" +
                    _barStyle.ToString() + "|" +
                    _text_1.Replace("|", "") + "|" +
                    _fadeOnRx.ToString() + "|" +
                    _fadeOnTx.ToString() + "|" +
                    _showType.ToString() + "|" +
                    Common.ColourToString(_segmentedSolidLowColour) + "|" +
                    _peakValue.ToString() + "|" +
                    Common.ColourToString(_peakValueColour) + "|" +
                    _eyeScale.ToString("f4") + "|" +
                    _average.ToString() + "|" +
                    _darkMode.ToString() + "|" +
                    _maxPower.ToString("f2") + "|" +
                    _units.ToString() + "|" +
                    Common.ColourToString(_segmentedSolidHighColour) + "|" +
                    _showMarker.ToString() + "|" +
                    Common.ColourToString(_subMarkerColour) + "|" +
                    _showSubMarker.ToString() + "|" +
                    _eyeBezelScale.ToString("f4") + "|" +
                    Common.ColourToString(_powerScaleColour) + "|" +
                    _ignoreHistoryDuration.ToString() + "|" +
                    _spacerPadding.ToString("f4") + "|" +
                    _back_panel.ToString() + "|" +
                    _text_2.Replace("|", "") + "|" +
                    _font_family_1.Replace("|", "++><++") + "|" +
                    _font_family_2.Replace("|", "++><++") + "|" +
                    _font_style_1.ToString() + "|" +
                    _font_style_2.ToString() + "|" +
                    _font_size_1.ToString("f4") + "|" +
                    _font_size_2.ToString("f4") + "|";

                for (int i = 0; i < _mmio_guid.Length; i++)
                {
                    sRet += _mmio_guid[i].ToString() + "|";
                    sRet += _mmio_variable[i] + "|";
                }

                if (!string.IsNullOrEmpty(sRet))
                {
                    //drop last |
                    sRet = sRet.Substring(0, sRet.Length - 1);
                }

                return sRet;
            }
            public bool TryParse(string str)
            {
                if (str == "") return false;

                int tmpInt = 0;
                float tmpFloat = 0;
                System.Drawing.Color tmpColour = System.Drawing.Color.White;
                bool tmpBool = false;
                Reading tmpReading = Reading.NONE;
                FontStyle tmpFontStyle = FontStyle.Regular;
                clsBarItem.BarStyle tmpBarStyle = clsBarItem.BarStyle.None;
                clsBarItem.Units tmpUnit = clsBarItem.Units.DBM;
                Guid tmpGuid = Guid.Empty;

                bool bOk = false;

                string[] tmp = str.Split('|');

                if (tmp.Length >= 32)
                {
                    bOk = int.TryParse(tmp[0], out tmpInt); if (bOk) { _updateInterval = tmpInt; }
                    if (bOk) bOk = float.TryParse(tmp[1], out tmpFloat); if (bOk) { _decay = tmpFloat; }
                    if (bOk) bOk = float.TryParse(tmp[2], out tmpFloat); if (bOk) { _attack = tmpFloat; }
                    if (bOk) bOk = int.TryParse(tmp[3], out tmpInt); if (bOk) { _historyDuration = tmpInt; }
                    if (bOk) bOk = bool.TryParse(tmp[4], out tmpBool); if (bOk) { _shadow = tmpBool; }
                    if (bOk) bOk = bool.TryParse(tmp[5], out tmpBool); if (bOk) { _showHistory = tmpBool; }
                    if (bOk) tmpColour = Common.ColourFromString(tmp[6]); bOk = tmpColour != System.Drawing.Color.Empty; if (bOk) { _historyColor = tmpColour; }
                    if (bOk) bOk = bool.TryParse(tmp[7], out tmpBool); if (bOk) { _peakHold = tmpBool; }
                    if (bOk) tmpColour = Common.ColourFromString(tmp[8]); bOk = tmpColour != System.Drawing.Color.Empty; if (bOk) { _peakHoldMarkerColor = tmpColour; }
                    if (bOk) tmpColour = Common.ColourFromString(tmp[9]); bOk = tmpColour != System.Drawing.Color.Empty; if (bOk) { _lowColor = tmpColour; }
                    if (bOk) tmpColour = Common.ColourFromString(tmp[10]); bOk = tmpColour != System.Drawing.Color.Empty; if (bOk) { _highColor = tmpColour; }
                    if (bOk) tmpColour = Common.ColourFromString(tmp[11]); bOk = tmpColour != System.Drawing.Color.Empty; if (bOk) { _titleColor = tmpColour; }
                    if (bOk) bOk = Enum.TryParse<Reading>(tmp[12], out tmpReading); if (bOk) { _readingSource = tmpReading; }
                    if (bOk) tmpColour = Common.ColourFromString(tmp[13]); bOk = tmpColour != System.Drawing.Color.Empty; if (bOk) { _colour = tmpColour; }
                    if (bOk) tmpColour = Common.ColourFromString(tmp[14]); bOk = tmpColour != System.Drawing.Color.Empty; if (bOk) { _markerColour = tmpColour; }
                    if (bOk) bOk = Enum.TryParse<clsBarItem.BarStyle>(tmp[15], out tmpBarStyle); if (bOk) { _barStyle = tmpBarStyle; }
                    if (bOk) _text_1 = tmp[16].Replace("|", "");
                    if (bOk) bOk = bool.TryParse(tmp[17], out tmpBool); if (bOk) { _fadeOnRx = tmpBool; }
                    if (bOk) bOk = bool.TryParse(tmp[18], out tmpBool); if (bOk) { _fadeOnTx = tmpBool; }
                    if (bOk) bOk = bool.TryParse(tmp[19], out tmpBool); if (bOk) { _showType = tmpBool; }
                    if (bOk) tmpColour = Common.ColourFromString(tmp[20]); bOk = tmpColour != System.Drawing.Color.Empty; if (bOk) { _segmentedSolidLowColour = tmpColour; }
                    if (bOk) bOk = bool.TryParse(tmp[21], out tmpBool); if (bOk) { _peakValue = tmpBool; }
                    if (bOk) tmpColour = Common.ColourFromString(tmp[22]); bOk = tmpColour != System.Drawing.Color.Empty; if (bOk) { _peakValueColour = tmpColour; }
                    if (bOk) bOk = float.TryParse(tmp[23], out tmpFloat); if (bOk) { _eyeScale = tmpFloat; _eyeBezelScale = _eyeScale; } // both incase recoving old data
                    if (bOk) bOk = bool.TryParse(tmp[24], out tmpBool); if (bOk) { _average = tmpBool; }
                    if (bOk) bOk = bool.TryParse(tmp[25], out tmpBool); if (bOk) { _darkMode = tmpBool; }
                    if (bOk) bOk = float.TryParse(tmp[26], out tmpFloat); if (bOk) { _maxPower = tmpFloat; }
                    if (bOk) bOk = Enum.TryParse<clsBarItem.Units>(tmp[27], out tmpUnit); if (bOk) { _units = tmpUnit; }
                    if (bOk) tmpColour = Common.ColourFromString(tmp[28]); bOk = tmpColour != System.Drawing.Color.Empty; if (bOk) { _segmentedSolidHighColour = tmpColour; }
                    if (bOk) bOk = bool.TryParse(tmp[29], out tmpBool); if (bOk) { _showMarker = tmpBool; }
                    if (bOk) tmpColour = Common.ColourFromString(tmp[30]); bOk = tmpColour != System.Drawing.Color.Empty; if (bOk) { _subMarkerColour = tmpColour; }
                    if (bOk) bOk = bool.TryParse(tmp[31], out tmpBool); if (bOk) { _showSubMarker = tmpBool; }
                }

                // this is due to new versions requiring more and more settings
                if (bOk && tmp.Length >= 33)
                {
                    if (bOk) bOk = float.TryParse(tmp[32], out tmpFloat); if (bOk) { _eyeBezelScale = tmpFloat; }
                }
                if (bOk && tmp.Length >= 34)
                {
                    if (bOk) tmpColour = Common.ColourFromString(tmp[33]); bOk = tmpColour != System.Drawing.Color.Empty; if (bOk) { _powerScaleColour = tmpColour; }
                }
                if(bOk && tmp.Length >= 35)
                {
                    if (bOk) bOk = int.TryParse(tmp[34], out tmpInt); if (bOk) { _ignoreHistoryDuration = tmpInt; }
                }
                if (bOk && tmp.Length >= 44) //[2.10.3.6]MW0LGE added for dev_6
                {
                    if (bOk) bOk = float.TryParse(tmp[35], out tmpFloat); if (bOk) { _spacerPadding = tmpFloat; }
                    if (bOk) bOk = bool.TryParse(tmp[36], out tmpBool); if (bOk) { _back_panel = tmpBool; }
                    if (bOk) _text_2 = tmp[37].Replace("|", "");
                    if (bOk) _font_family_1 = tmp[38].Replace("++><++", "|");
                    if (bOk) _font_family_2 = tmp[39].Replace("++><++", "|");
                    if (bOk) bOk = Enum.TryParse<FontStyle>(tmp[40], out tmpFontStyle); if (bOk) { _font_style_1 = tmpFontStyle; }
                    if (bOk) bOk = Enum.TryParse<FontStyle>(tmp[41], out tmpFontStyle); if (bOk) { _font_style_2 = tmpFontStyle; }
                    if (bOk) bOk = float.TryParse(tmp[42], out tmpFloat); if (bOk) { _font_size_1 = tmpFloat; }
                    if (bOk) bOk = float.TryParse(tmp[43], out tmpFloat); if (bOk) { _font_size_2 = tmpFloat; }
                }
                if (bOk && tmp.Length >= 64)
                {
                    for (int i = 0; i < _mmio_guid.Length; i++) //44,45, 46,47, 48,49, 50,51, 52,53, 54,55, 56,57, 58,59, 60,61, 62,63
                    {
                        if (bOk) bOk = Guid.TryParse(tmp[44 + (i*2)], out tmpGuid); if (bOk) { _mmio_guid[i] = tmpGuid; }
                        if (bOk) _mmio_variable[i] = tmp[45 + (i*2)];
                    }
                }

                //
                SetSetting("_updateInterval", _updateInterval);
                SetSetting("_decay", _decay);
                SetSetting("_attack", _attack);
                SetSetting("_historyDuration", _historyDuration);
                SetSetting("_shadow", _shadow);
                SetSetting("_showHistory", _showHistory);
                SetSetting("_historyColor", _historyColor);
                SetSetting("_peakHold", _peakHold);
                SetSetting("_peakHoldMarkerColor", _peakHoldMarkerColor);
                SetSetting("_lowColor", _lowColor);
                SetSetting("_highColor", _highColor);
                SetSetting("_titleColor", _titleColor);
                SetSetting("_readingSource", _readingSource);
                SetSetting("_colour", _colour);
                SetSetting("_markerColour", _markerColour);
                SetSetting("_barStyle", _barStyle);
                SetSetting("_text_1", _text_1);
                SetSetting("_fadeOnRx", _fadeOnRx);
                SetSetting("_fadeOnTx", _fadeOnTx);
                SetSetting("_showType", _showType);
                SetSetting("_segmentedSolidLowColour", _segmentedSolidLowColour);
                SetSetting("_peakValue", _peakValue);
                SetSetting("_peakValueColour", _peakValueColour);
                SetSetting("_eyeScale", _eyeScale);
                SetSetting("_average", _average);
                SetSetting("_darkMode", _darkMode);
                SetSetting("_maxPower", _maxPower);
                SetSetting("_units", _units);
                SetSetting("_segmentedSolidHighColour", _segmentedSolidHighColour);
                SetSetting("_showMarker", _showMarker);
                SetSetting("_subMarkerColour", _subMarkerColour);
                SetSetting("_showSubMarker", _showSubMarker);

                SetSetting("_eyeBezelScale", _eyeBezelScale);
                SetSetting("_powerScaleColour", _powerScaleColour);
                SetSetting("_ignoreHistoryDuration", _ignoreHistoryDuration);
                SetSetting("_spacerPadding", _spacerPadding);
                SetSetting("_back_panel", _back_panel);
                SetSetting("_text_2", _text_2);
                SetSetting("_font_family_1", _font_family_1);
                SetSetting("_font_family_2", _font_family_2);
                SetSetting("_font_style_1", _font_style_1);
                SetSetting("_font_style_2", _font_style_2);
                SetSetting("_font_size_1", _font_size_1);
                SetSetting("_font_size_2", _font_size_2);

                for (int i = 0; i < _mmio_guid.Length; i++)
                {
                    SetMMIOGuid(i, _mmio_guid[i]);
                    SetMMIOVariable(i, _mmio_variable[i]);
                }
                //

                return bOk;
            }
            public bool SubIndicators { get { return (bool)GetSetting("_hasSubIndicators", typeof(bool)); } set { SetSetting("_hasSubIndicators", value); } }
            public int UpdateInterval { get { return (int)GetSetting("_updateInterval", typeof(int)); } set { SetSetting("_updateInterval", value); } }
            public float DecayRatio { get { return (float)GetSetting("_decay", typeof(float)); } set { SetSetting("_decay", value); } }
            public float AttackRatio { get { return (float)GetSetting("_attack", typeof(float)); } set { SetSetting("_attack", value); } }
            public int HistoryDuration { get { return (int)GetSetting("_historyDuration", typeof(int)); } set { SetSetting("_historyDuration", value); } }
            public int IgnoreHistoryDuration { get { return (int)GetSetting("_ignoreHistoryDuration", typeof(int)); } set { SetSetting("_ignoreHistoryDuration", value); } }
            public bool Shadow { get { return (bool)GetSetting("_shadow", typeof(bool)); } set { SetSetting("_shadow", value); } }
            public bool ShowHistory { get { return (bool)GetSetting("_showHistory", typeof(bool)); } set { SetSetting("_showHistory", value); } }
            public System.Drawing.Color HistoryColor { get { return (System.Drawing.Color)GetSetting("_historyColor", typeof(System.Drawing.Color)); } set { SetSetting("_historyColor", value); } }
            public bool PeakHold { get { return (bool)GetSetting("_peakHold", typeof(bool)); } set { SetSetting("_peakHold", value); } }
            public System.Drawing.Color PeakHoldMarkerColor { get { return (System.Drawing.Color)GetSetting("_peakHoldMarkerColor", typeof(System.Drawing.Color)); } set { SetSetting("_peakHoldMarkerColor", value); } }
            public System.Drawing.Color LowColor { get { return (System.Drawing.Color)GetSetting("_lowColor", typeof(System.Drawing.Color)); } set { SetSetting("_lowColor", value); } }
            public System.Drawing.Color HighColor { get { return (System.Drawing.Color)GetSetting("_highColor", typeof(System.Drawing.Color)); } set { SetSetting("_highColor", value); } }
            public System.Drawing.Color TitleColor { get { return (System.Drawing.Color)GetSetting("_titleColor", typeof(System.Drawing.Color)); } set { SetSetting("_titleColor", value); } }
            public Reading ReadingSource { get { return (Reading)GetSetting("_readingSource", typeof(Reading)); } set { SetSetting("_readingSource", value); } }
            public System.Drawing.Color Colour { get { return (System.Drawing.Color)GetSetting("_colour", typeof(System.Drawing.Color)); } set { SetSetting("_colour", value); } }
            public System.Drawing.Color MarkerColour { get { return (System.Drawing.Color)GetSetting("_markerColour", typeof(System.Drawing.Color)); } set { SetSetting("_markerColour", value); } }
            public System.Drawing.Color SubMarkerColour { get { return (System.Drawing.Color)GetSetting("_subMarkerColour", typeof(System.Drawing.Color)); } set { SetSetting("_subMarkerColour", value); } }
            public bool ShowMarker { get { return (bool)GetSetting("_showMarker", typeof(bool)); } set { SetSetting("_showMarker", value); } }
            public bool ShowSubMarker { get { return (bool)GetSetting("_showSubMarker", typeof(bool)); } set { SetSetting("_showSubMarker", value); } }
            public clsBarItem.BarStyle BarStyle { get { return (clsBarItem.BarStyle)GetSetting("_barStyle", typeof(clsBarItem.BarStyle)); } set { SetSetting("_barStyle", value); } }
            public string Text1 { get { return ((string)GetSetting("_text_1", typeof(string))).Replace("|", ""); } set { SetSetting("_text_1", value.Replace("|", "")); } }
            public string Text2 { get { return ((string)GetSetting("_text_2", typeof(string))).Replace("|", ""); } set { SetSetting("_text_2", value.Replace("|", "")); } }
            public bool FadeOnRx { get { return (bool)GetSetting("_fadeOnRx", typeof(bool)); } set { SetSetting("_fadeOnRx", value); } }
            public bool FadeOnTx { get { return (bool)GetSetting("_fadeOnTx", typeof(bool)); } set { SetSetting("_fadeOnTx", value); } }
            public bool ShowType { get { return (bool)GetSetting("_showType", typeof(bool)); } set { SetSetting("_showType", value); } }
            public System.Drawing.Color SegmentedSolidLowColour { get { return (System.Drawing.Color)GetSetting("_segmentedSolidLowColour", typeof(System.Drawing.Color)); } set { SetSetting("_segmentedSolidLowColour", value); } }
            public System.Drawing.Color SegmentedSolidHighColour { get { return (System.Drawing.Color)GetSetting("_segmentedSolidHighColour", typeof(System.Drawing.Color)); } set { SetSetting("_segmentedSolidHighColour", value); } }
            public bool PeakValue { get { return (bool)GetSetting("_peakValue", typeof(bool)); } set { SetSetting("_peakValue", value); } }
            public System.Drawing.Color PeakValueColour { get { return (System.Drawing.Color)GetSetting("_peakValueColour", typeof(System.Drawing.Color)); } set { SetSetting("_peakValueColour", value); } }
            public float EyeScale { get { return (float)GetSetting("_eyeScale", typeof(float)); } set { SetSetting("_eyeScale", value); } }
            public float SpacerPadding { get { return (float)GetSetting("_spacerPadding", typeof(float)); } set { SetSetting("_spacerPadding", value); } }
            public bool BackPanel { get { return (bool)GetSetting("_back_panel", typeof(bool)); } set { SetSetting("_back_panel", value); } }
            public float EyeBezelScale { get { return (float)GetSetting("_eyeBezelScale", typeof(float)); } set { SetSetting("_eyeBezelScale", value); } }
            public bool Average { get { return (bool)GetSetting("_average", typeof(bool)); } set { SetSetting("_average", value); } }
            public bool DarkMode { get { return (bool)GetSetting("_darkMode", typeof(bool)); } set { SetSetting("_darkMode", value); } }
            public float MaxPower { get { return (float)GetSetting("_maxPower", typeof(float)); } set { SetSetting("_maxPower", value); } }
            public System.Drawing.Color PowerScaleColour { get { return (System.Drawing.Color)GetSetting("_powerScaleColour", typeof(System.Drawing.Color)); } set { SetSetting("_powerScaleColour", value); } }
            public clsBarItem.Units Unit { get { return (clsBarItem.Units)GetSetting("_units", typeof(clsBarItem.Units)); } set { SetSetting("_units", value); } }
            public string FontFamily1 { get { return ((string)GetSetting("_font_family_1", typeof(string))).Replace("|", "++><++"); } set { SetSetting("_font_family_1", value.Replace("++><++", "|")); } }
            public string FontFamily2 { get { return ((string)GetSetting("_font_family_2", typeof(string))).Replace("|", "++><++"); } set { SetSetting("_font_family_2", value.Replace("++><++", "|")); } }
            public FontStyle FontStyle1 { get { return (FontStyle)GetSetting("_font_style_1", typeof(FontStyle)); } set { SetSetting("_font_style_1", value); } }
            public FontStyle FontStyle2 { get { return (FontStyle)GetSetting("_font_style_2", typeof(FontStyle)); } set { SetSetting("_font_style_2", value); } }
            public float FontSize1 { get { return (float)GetSetting("_font_size_1", typeof(float)); } set { SetSetting("_font_size_1", value); } }
            public float FontSize2 { get { return (float)GetSetting("_font_size_2", typeof(float)); } set { SetSetting("_font_size_2", value); } }


            //public bool SubIndicators { get { return _hasSubIndicators; } set { _hasSubIndicators = value; } }
            //public int UpdateInterval { get { return _updateInterval; } set { _updateInterval = value; } }
            //public float DecayRatio { get { return _decay; } set { _decay = value; } }
            //public float AttackRatio { get { return _attack; } set { _attack = value; } }
            //public int HistoryDuration { get { return _historyDuration; } set { _historyDuration = value; } }
            //public int IgnoreHistoryDuration { get { return _ignoreHistoryDuration; } set { _ignoreHistoryDuration = value; } }
            //public bool Shadow { get { return _shadow; } set { _shadow = value; } }
            //public bool ShowHistory { get { return _showHistory; } set { _showHistory = value; } }
            //public System.Drawing.Color HistoryColor { get { return _historyColor; } set { _historyColor = value; } }
            //public bool PeakHold { get { return _peakHold; } set { _peakHold = value; } }
            //public System.Drawing.Color PeakHoldMarkerColor { get { return _peakHoldMarkerColor; } set { _peakHoldMarkerColor = value; } }
            //public System.Drawing.Color LowColor { get { return _lowColor; } set { _lowColor = value; } }
            //public System.Drawing.Color HighColor { get { return _highColor; } set { _highColor = value; } }
            //public System.Drawing.Color TitleColor { get { return _titleColor; } set { _titleColor = value; } }
            //public Reading ReadingSource { get { return _readingSource; } set { _readingSource = value; } }
            //public System.Drawing.Color Colour { get { return _colour; } set { _colour = value; } }
            //public System.Drawing.Color MarkerColour { get { return _markerColour; } set { _markerColour = value; } }
            //public System.Drawing.Color SubMarkerColour { get { return _subMarkerColour; } set { _subMarkerColour = value; } }
            //public bool ShowMarker { get { return _showMarker; } set { _showMarker = value; } }
            //public bool ShowSubMarker { get { return _showSubMarker; } set { _showSubMarker = value; } }
            //public clsBarItem.BarStyle BarStyle { get { return _barStyle; } set { _barStyle = value; } }
            //public string Text1 { get { return _text_1.Replace("|", ""); } set { _text_1 = value.Replace("|", ""); } }
            //public string Text2 { get { return _text_2.Replace("|", ""); } set { _text_2 = value.Replace("|", ""); } }
            //public bool FadeOnRx { get { return _fadeOnRx; } set { _fadeOnRx = value; } }
            //public bool FadeOnTx { get { return _fadeOnTx; } set { _fadeOnTx = value; } }
            //public bool ShowType { get { return _showType; } set { _showType = value; } }
            //public System.Drawing.Color SegmentedSolidLowColour { get { return _segmentedSolidLowColour; } set { _segmentedSolidLowColour = value; } }
            //public System.Drawing.Color SegmentedSolidHighColour { get { return _segmentedSolidHighColour; } set { _segmentedSolidHighColour = value; } }
            //public bool PeakValue { get { return _peakValue; } set { _peakValue = value; } }
            //public System.Drawing.Color PeakValueColour { get { return _peakValueColour; } set { _peakValueColour = value; } }
            //public float EyeScale { get { return _eyeScale; } set { _eyeScale = value; } }
            //public float SpacerPadding { get { return _spacerPadding; } set { _spacerPadding = value; } }
            //public bool BackPanel { get { return _back_panel; } set { _back_panel = value; } }
            //public float EyeBezelScale { get { return _eyeBezelScale; } set { _eyeBezelScale = value; } }
            //public bool Average { get { return _average; } set { _average = value; } }
            //public bool DarkMode { get { return _darkMode; } set { _darkMode = value; } }
            //public float MaxPower { get { return _maxPower; } set { _maxPower = value; } }
            //public System.Drawing.Color PowerScaleColour { get { return _powerScaleColour; } set { _powerScaleColour = value; } }
            //public clsBarItem.Units Unit { get { return _units; } set { _units = value; } }
            //public string FontFamily1 { get { return _font_family_1.Replace("|", "++><++"); } set { _font_family_1 = value.Replace("++><++", "|"); } }
            //public string FontFamily2 { get { return _font_family_2.Replace("|", "++><++"); } set { _font_family_2 = value.Replace("++><++", "|"); } }
            //public FontStyle FontStyle1 { get { return _font_style_1; } set { _font_style_1 = value; } }
            //public FontStyle FontStyle2 { get { return _font_style_2; } set { _font_style_2 = value; } }
            //public float FontSize1 { get { return _font_size_1; } set { _font_size_1 = value; } }
            //public float FontSize2 { get { return _font_size_2; } set { _font_size_2 = value; } }

            public Guid GetMMIOGuid(int index)
            {
                return (Guid)GetSetting($"_mmio_guid_{index}", typeof(Guid));
            }
            public void SetMMIOGuid(int index, Guid g)
            {
                SetSetting($"_mmio_guid_{index}", g);
            }
            public string GetMMIOVariable(int index)
            {
                return (string)GetSetting($"_mmio_variable_{index}", typeof(string));
            }
            public void SetMMIOVariable(int index, string variable)
            {
                SetSetting($"_mmio_variable_{index}", variable);
            }
            //public Guid GetMMIOGuid(int index)
            //{
            //    return _mmio_guid[index];
            //}
            //public void SetMMIOGuid(int index, Guid g)
            //{
            //    _mmio_guid[index] = g;
            //}
            //public string GetMMIOVariable(int index)
            //{
            //    return _mmio_variable[index];
            //}
            //public void SetMMIOVariable(int index, string variable)
            //{
            //    _mmio_variable[index] = variable;
            //}
        }
        static MeterManager()
        {
            // readings used by varius meter items such as Text Overlay
            _custom_readings = new CustomReadings();

            // image fetcher
            _image_fetcher = new ImageFetcher();

            // static constructor
            _rx1VHForAbove = false;
            _rx2VHForAbove = false;
            _delegatesAdded = false;
            _console = null;
            _finishedSetup = false;
            _readings = new Dictionary<int, clsReadings>();
            _meters = new Dictionary<string, clsMeter>();
            _readingIgnore = new Dictionary<int, bool>();

            //_power = false;
            _currentHPSDRmodel = HPSDRModel.HERMES;
            _alexPresent = false;
            _paPresent = false;
            _apolloPresent = false;
            _transverterIndex = -1; // no transverter
            //_spectrumReady = false;

            _pooledImages = new Dictionary<string, System.Drawing.Bitmap>();
            _pooledStreamData = new Dictionary<string, DataStream>();

            // two sets of readings, for each trx
            _readings.Add(1, new clsReadings());
            _readings.Add(2, new clsReadings());

            // two trx reading ignore flags
            _readingIgnore.Add(1, false);
            _readingIgnore.Add(2, false);

            _meterThreadRunning = false;

            _openHPSDR_appdatapath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\OpenHPSDR";
        }
        public static ImageFetcher ImgFetch
        {
            get { return _image_fetcher; }
        }
        public static CustomReadings ReadingsCustom
        {
            get { return _custom_readings; }
        }
        // zero reading
        public static void ZeroReading(out float value, int rx, Reading reading)
        {
            value = 0;

            switch (reading)
            {
                case Reading.SIGNAL_STRENGTH:
                case Reading.AVG_SIGNAL_STRENGTH:
                    if (IsAboveS9Frequency(rx))
                        value = -153; //S0
                    else
                        value = -133; //S0
                    break;
                case Reading.ADC_PK:
                case Reading.ADC_AV:
                    value = -120.0f;
                    break;
                case Reading.AGC_AV:
                case Reading.AGC_PK:
                    value = -125.0f;
                    break;
                case Reading.AGC_GAIN:
                    value = -50.0f;
                    break;
                case Reading.MIC:
                case Reading.MIC_PK:
                    value = -120.0f;
                    break;
                case Reading.LEVELER:
                case Reading.LEVELER_PK:
                    value = -30.0f;
                    break;
                case Reading.EQ:
                case Reading.EQ_PK:
                    value = -30.0f;
                    break;
                case Reading.LVL_G:
                    value = 0f;
                    break;
                case Reading.ALC:
                case Reading.ALC_PK:
                    value = -120.0f;
                    break;
                case Reading.ALC_G: //alc comp
                    value = 0f;
                    break;
                case Reading.ALC_GROUP:
                    value = -30.0f;
                    break;
                case Reading.CFC_AV:
                case Reading.CFC_PK:
                    value = -30.0f;
                    break;
                case Reading.CFC_G:
                    value = 0f;
                    break;
                case Reading.COMP:
                case Reading.COMP_PK:
                    value = -30.0f;
                    break;
                //case Reading.CPDR: //CPDR is the same as comp
                //case Reading.CPDR_PK:
                    //value = -30.0f;
                    //break;
                case Reading.PWR:
                case Reading.REVERSE_PWR:
                    value = 0f;
                    break;
                case Reading.SWR:
                    value = 1.0f;
                    break;
                case Reading.ESTIMATED_PBSNR:
                    value = 0f;
                    break;
                case Reading.VOLTS:
                    value = 0f;
                    break;
                case Reading.AMPS:
                    value = 0f;
                    break;
            }
            setReadingForced(rx, reading, value);
        }
        //

        //private static object _spectrumArrayLock = new object();
        //public static void ResizeSpectrum(int len)
        //{
        //    lock (_spectrumArrayLock)
        //    {
        //        if (_newSpectrumPassband == null || _newSpectrumPassband.Length != len)
        //        {
        //            _newSpectrumPassband = new float[len];

        //            if (_currentSpectrumPassband == null)
        //            {
        //                _currentSpectrumPassband = new float[len];
        //            }
        //            else
        //            {
        //                float[] tmp = new float[len];
        //                int stop = Math.Min(tmp.Length, _currentSpectrumPassband.Length);

        //                for (int i = 0; i < stop; i++)
        //                {
        //                    tmp[i] = _currentSpectrumPassband[i];
        //                }
        //                for (int i = stop; i < len; i++)
        //                {
        //                    tmp[i] = Display.SpectrumGridMin;
        //                }

        //                _currentSpectrumPassband = tmp;
        //            }
        //        }
        //    }
        //}
        //public static bool SpectrumReady
        //{
        //    get { return _spectrumReady; }
        //    set { _spectrumReady = value; }
        //}
        public static int GetMeterTXRXType(MeterType meter)
        {
            // 0 - rx
            // 1 - tx
            // 2 - other
            switch (meter)
            {
                case MeterType.SIGNAL_STRENGTH: return 0;
                case MeterType.AVG_SIGNAL_STRENGTH: return 0;
                case MeterType.SIGNAL_TEXT: return 0;
                case MeterType.ADC: return 0;
                case MeterType.AGC: return 0;
                case MeterType.AGC_GAIN: return 0;
                case MeterType.ESTIMATED_PBSNR: return 0;

                case MeterType.MIC: return 1;
                case MeterType.PWR: return 1;
                case MeterType.REVERSE_PWR: return 1;
                case MeterType.ALC: return 1;
                case MeterType.EQ: return 1;
                case MeterType.LEVELER: return 1;
                case MeterType.COMP: return 1;
                //case MeterType.CPDR: return 1;//CPDR is the same as comp
                case MeterType.ALC_GAIN: return 1;
                case MeterType.ALC_GROUP: return 1;
                case MeterType.LEVELER_GAIN: return 1;
                case MeterType.CFC: return 1;
                case MeterType.CFC_GAIN: return 1;
                case MeterType.SWR: return 1;

                case MeterType.MAGIC_EYE: return 2;
                case MeterType.ANANMM: return 2;
                case MeterType.CROSS: return 2;
                //case MeterType.HISTORY: return 2;
                case MeterType.VFO_DISPLAY: return 2;
                case MeterType.CLOCK: return 2;
                case MeterType.SPACER: return 2;
                case MeterType.TEXT_OVERLAY: return 2;
                case MeterType.DATA_OUT: return 2;
                case MeterType.ROTATOR: return 2;
                case MeterType.LED: return 2;
                case MeterType.WEB_IMAGE: return 2;
                    //case MeterType.SPECTRUM: return 2;
            }

            return 0;
        }
        public static string MeterName(MeterType meter)
        {
            switch (meter)
            {
                case MeterType.SIGNAL_STRENGTH: return "Signal Strength";
                case MeterType.AVG_SIGNAL_STRENGTH: return "Avg Signal Strength";
                case MeterType.SIGNAL_TEXT: return "Signal Strength Text";
                case MeterType.ADC: return "ADC";
                case MeterType.AGC: return "AGC";
                case MeterType.AGC_GAIN: return "AGC Gain";
                case MeterType.MIC: return "Mic";
                case MeterType.PWR: return "ForwardPower";
                case MeterType.REVERSE_PWR: return "Reverse Power";
                case MeterType.ALC: return "ALC";
                case MeterType.EQ: return "EQ";
                case MeterType.LEVELER: return "Leveler";
                case MeterType.COMP: return "Compression";
                //case MeterType.CPDR: return "Compander";//CPDR is the same as comp
                case MeterType.ALC_GAIN: return "ALC Compression";
                case MeterType.ALC_GROUP: return "ALC Group";
                case MeterType.LEVELER_GAIN: return "Leveler Gain";
                case MeterType.CFC: return "CFC Compression";
                case MeterType.CFC_GAIN: return "CFC Compression Gain";
                case MeterType.MAGIC_EYE: return "Magic Eye";
                case MeterType.ESTIMATED_PBSNR: return "Estimated PBSNR";
                case MeterType.ANANMM: return "Anan Multi Meter";
                case MeterType.CROSS: return "Cross Meter";
                case MeterType.SWR: return "SWR";
                case MeterType.SPACER: return "Spacer";
                case MeterType.TEXT_OVERLAY: return "Text Overlay";
                case MeterType.LED: return "Led Indicator";
                case MeterType.WEB_IMAGE: return "Web Image";
                case MeterType.DATA_OUT: return "Data Out Node";
                case MeterType.ROTATOR: return "Rotator";
                //case MeterType.HISTORY: return "History";
                case MeterType.VFO_DISPLAY: return "Vfo Display";
                case MeterType.CLOCK: return "Clock";
            }

            return meter.ToString();
        }
        public static string ReadingName(Reading reading)
        {
            switch (reading)
            {
                case Reading.ADC_AV: return "ADC Average";
                case Reading.ADC_PK: return "ADC Peak";
                case Reading.AGC_AV: return "AGC Average";
                case Reading.AGC_PK: return "AGC Peak";
                case Reading.AGC_GAIN: return "AGC Gain";
                //case Reading.EYE_PERCENT: return "Magic Eye";
                case Reading.ALC: return "ALC";
                case Reading.ALC_G: return "ALC Compression";
                case Reading.ALC_GROUP: return "ALC Group";
                case Reading.ESTIMATED_PBSNR: return "Estimated PBSNR";
                case Reading.ALC_PK: return "ALC (av/pk)";// Peak";
                case Reading.AMPS: return "Amps";
                case Reading.AVG_SIGNAL_STRENGTH: return "Signal Average";
                case Reading.CAL_FWD_PWR: return "Calibrated FWD Power";
                case Reading.CFC_G: return "CFC Compression";
                case Reading.CFC_PK: return "CFC Compression (av/pk)";// Peak";
                case Reading.CFC_AV: return "CFC Compression Average";
                case Reading.COMP: return "Compression";
                case Reading.COMP_PK: return "Compression (av/pk)";// Peak";
                //case Reading.CPDR: return "Compander"; //CPDR is the same as comp
                //case Reading.CPDR_PK: return "Compander Peak"; //CPDR is the same as comp
                case Reading.DRIVE_FWD_ADC: return "Drive Forward ADC";
                case Reading.DRIVE_PWR: return "Drive Power";
                case Reading.EQ: return "EQ";
                case Reading.EQ_PK: return "EQ (av/pk)";// Peak";
                case Reading.FWD_ADC: return "Forward ADC";
                case Reading.FWD_VOLT: return "Forward Volt";
                case Reading.LEVELER: return "Leveler";
                case Reading.LEVELER_PK: return "Leveler (av/pk)";// Peak";
                case Reading.LVL_G: return "Leveler Gain";
                case Reading.MIC: return "MIC";
                case Reading.MIC_PK: return "MIC (av/pk)";// Peak";
                case Reading.PA_FWD_PWR: return "PA Forward Power";
                case Reading.PA_REV_PWR: return "PA Reverse Power";
                case Reading.PWR: return "Power";
                case Reading.REVERSE_PWR: return "Reverse Power";
                case Reading.REV_ADC: return "Reverse ADC";
                case Reading.REV_VOLT: return "Reverse VOLT";
                case Reading.SIGNAL_STRENGTH: return "Signal";
                case Reading.SWR: return "SWR";
                case Reading.VOLTS: return "Volts";
                case Reading.AZ: return "Azimuth";
                case Reading.ELE: return "Elevation";
            }

            return reading.ToString();
        }
        public static string ReadingUnits(Reading reading)
        {
            switch (reading)
            {
                case Reading.ADC_AV: return "dBFS";
                case Reading.ADC_PK: return "dBFS";
                case Reading.AGC_PK: return "dB";
                case Reading.AGC_AV: return "dB";
                case Reading.AGC_GAIN: return "dB";
                //case Reading.EYE_PERCENT: return "?";
                case Reading.ALC: return "dB";
                case Reading.ALC_G: return "dB";
                case Reading.ALC_GROUP: return "dB";
                case Reading.ESTIMATED_PBSNR: return "dB";
                case Reading.ALC_PK: return "dB";
                case Reading.AMPS: return "A";
                case Reading.AVG_SIGNAL_STRENGTH: return "dBm";
                case Reading.CAL_FWD_PWR: return "W";
                case Reading.CFC_G: return "dB";
                case Reading.CFC_PK: return "dB";
                case Reading.CFC_AV: return "dB";
                case Reading.COMP: return "dB";
                case Reading.COMP_PK: return "dB";
                //case Reading.CPDR: return "dB"; //CPDR is the same as comp
                //case Reading.CPDR_PK: return "dB"; //CPDR is the same as comp
                case Reading.DRIVE_FWD_ADC: return "?";
                case Reading.DRIVE_PWR: return "W";
                case Reading.EQ: return "dB";
                case Reading.EQ_PK: return "dB";
                case Reading.FWD_ADC: return "FWD ADC";
                case Reading.FWD_VOLT: return "?";
                case Reading.LEVELER: return "dB";
                case Reading.LEVELER_PK: return "dB";
                case Reading.LVL_G: return "dB";
                case Reading.MIC: return "dB";
                case Reading.MIC_PK: return "dB";
                case Reading.PA_FWD_PWR: return "W";
                case Reading.PA_REV_PWR: return "W";
                case Reading.PWR: return "W";
                case Reading.REVERSE_PWR: return "W";
                case Reading.REV_ADC: return "?";
                case Reading.REV_VOLT: return "V";
                case Reading.SIGNAL_STRENGTH: return "dBm";
                case Reading.SWR: return ":1";
                case Reading.VOLTS: return "V";
                case Reading.AZ: return "°";
                case Reading.ELE: return "°";
            }

            return reading.ToString();
        }

        private static void UpdateMeters()
        {
            List<Reading> readingsUsed = new List<Reading>();

            _meterThreadRunning = true;
            while (_meterThreadRunning)
            {
                int nDelay = int.MaxValue;
                lock (_metersLock)
                {
                    // udpate any meter
                    foreach (KeyValuePair<string, clsMeter> kvp in _meters)
                    {
                        clsMeter m = kvp.Value;

                        if (m.Enabled)
                        {
                            readingsUsed.Clear();

                            m.Update(ref readingsUsed);

                            int nTmp = m.DelayForUpdate();
                            if (nTmp < nDelay) nDelay = nTmp;

                            //lock (_readingsLock)
                            {
                                // use/invalidate any readings that have been used, so that they can be updated
                                foreach (Reading rt in readingsUsed)
                                {
                                    _readings[m.RX].UseReading(rt);
                                }
                            }
                        }
                    }
                }
                if (nDelay == int.MaxValue) nDelay = 250;
                Thread.Sleep(nDelay);
            }
        }
        //image caching, used by dxrenderer
        internal static void ClearAllCachedImageData(bool bOnlySkins = false)
        {
            if (_pooledImages == null) return;

            lock (_imageLock)
            {
                List<string> keys = _pooledImages.Keys.ToList();

                foreach(string sKey in keys)
                {
                    RemoveImageCacheData(sKey, bOnlySkins);
                }

                if (!bOnlySkins)
                {
                    _pooledImages.Clear();
                    _pooledStreamData.Clear();
                }
            }
        }
        internal static bool RemoveImageCacheData(string sKey, bool bOnlySkins = false)
        {
            lock (_imageLock)
            {
                if (_pooledImages == null) return false;
                if (!_pooledImages.ContainsKey(sKey)) return false;

                // remove the bitmap, tag = true then skin image
                if (bOnlySkins && !(bool)_pooledImages[sKey].Tag) return false;

                _pooledImages[sKey].Dispose();
                _pooledImages.Remove(sKey);

                if (_pooledStreamData != null)
                {
                    if(_pooledStreamData.ContainsKey(sKey))
                    {
                        // remove the stream related to the bitmap
                        _pooledStreamData[sKey].Dispose();
                        _pooledStreamData.Remove(sKey);
                    }
                }
            }
            return true;
        }
        internal static bool AddBitmap(string sKey, System.Drawing.Bitmap image)
        {
            bool bRet = true;
            lock (_imageLock)
            {
                if (_pooledImages == null) bRet = false;
                if (_pooledImages.ContainsKey(sKey)) bRet = false;

                _pooledImages.Add(sKey, image);
            }
            return bRet;
        }
        internal static System.Drawing.Bitmap GetBitmap(string sKey)
        {
            lock (_imageLock)
            {
                if (_pooledImages == null) return null;
                if (!_pooledImages.ContainsKey(sKey)) return null;

                return _pooledImages[sKey];
            }
        }
        internal static bool ContainsBitmap(string sKey)
        {
            lock (_imageLock)
            {
                if (_pooledImages == null) return false;
                return _pooledImages.ContainsKey(sKey);
            }
        }
        internal static void AddStreamData(string sId, DataStream tempStream)
        {
            lock (_imageLock)
            {
                if (_pooledStreamData == null) return;
                if (_pooledStreamData.ContainsKey(sId)) return;

                _pooledStreamData.Add(sId, tempStream);
            }
        }
        internal static DataStream GetStreamData(string sKey)
        {
            lock (_imageLock)
            {
                if (_pooledStreamData == null) return null;
                if (!_pooledStreamData.ContainsKey(sKey)) return null;

                return _pooledStreamData[sKey];
            }
        }
        internal static bool ContainsStreamData(string sKey)
        {
            lock (_imageLock)
            {
                if (_pooledStreamData == null) return false;
                return _pooledStreamData.ContainsKey(sKey);
            }
        }
        internal static void RemoveStreamData(string sKey)
        {
            lock (_imageLock)
            {
                if (_pooledStreamData == null) return;
                if (_pooledStreamData.ContainsKey(sKey))
                {
                    DataStream ds = _pooledStreamData[sKey];
                    
                    Utilities.Dispose(ref ds);
                    ds = null;

                    _pooledStreamData.Remove(sKey);
                }
            }
        }
        public static void ContainerBorder(string sId, bool border)
        {
            lock (_metersLock) {
                if (_lstUCMeters == null) return;
                if (!_lstUCMeters.ContainsKey(sId)) return;

                ucMeter uc = _lstUCMeters[sId];
                uc.UCBorder = border;
            }
        }
        public static void NoTitle(string sId, bool noTitle)
        {
            lock (_metersLock)
            {
                if (_lstUCMeters == null) return;
                if (!_lstUCMeters.ContainsKey(sId)) return;

                ucMeter uc = _lstUCMeters[sId];
                uc.NoTitle = noTitle;
            }
        }
        public static void ContainerMinimises(string sId, bool minimises)
        {
            lock (_metersLock)
            {
                if (_lstUCMeters == null) return;
                if (!_lstUCMeters.ContainsKey(sId)) return;

                ucMeter uc = _lstUCMeters[sId];

                if (_lstMeterDisplayForms.ContainsKey(uc.ID))
                {
                    frmMeterDisplay f = _lstMeterDisplayForms[uc.ID];

                    if(minimises != uc.ContainerMinimises)
                    {
                        uc.ContainerMinimises = minimises;
                        f.ContainerMinimises = minimises;
                    }
                }
            }
        }
        public static void EnableContainer(string sId, bool enabled)
        {
            lock (_metersLock)
            {
                if (_lstUCMeters == null) return;
                if (!_lstUCMeters.ContainsKey(sId)) return;
                
                ucMeter uc = _lstUCMeters[sId];
                bool bOldState = uc.MeterEnabled;
                uc.MeterEnabled = enabled;

                if (enabled != bOldState && _lstMeterDisplayForms.ContainsKey(uc.ID))
                {
                    frmMeterDisplay f = _lstMeterDisplayForms[uc.ID];
                    f.FormEnabled = enabled;

                    if (uc.Floating)
                    {
                        if (!enabled)
                            f.Hide();
                        else
                            setMeterFloating(uc, f);
                    }
                    else
                    {
                        if (!enabled)
                            uc.Hide();
                        else
                            returnMeterFromFloating(uc, f);
                    }

                    // meter
                    if(_meters != null && _meters.ContainsKey(sId))
                        _meters[sId].Enabled = enabled;

                    // DXrenderer
                    if (_DXrenderers != null && _DXrenderers.ContainsKey(uc.ID))
                        _DXrenderers[sId].Enabled = enabled;
                }
            }
        }
        public static bool ContainerHasBorder(string sId)
        {
            lock (_metersLock)
            {
                if (_lstUCMeters == null) return false;
                if (!_lstUCMeters.ContainsKey(sId)) return false;

                ucMeter uc = _lstUCMeters[sId];
                return uc.UCBorder;
            }
        }
        public static bool ContainerNoTitleBar(string sId)
        {
            lock (_metersLock)
            {
                if (_lstUCMeters == null) return false;
                if (!_lstUCMeters.ContainsKey(sId)) return false;

                ucMeter uc = _lstUCMeters[sId];
                return uc.NoTitle;
            }
        }
        public static bool ContainerShow(string sId)
        {
            lock (_metersLock)
            {
                if (_lstUCMeters == null) return false;
                if (!_lstUCMeters.ContainsKey(sId)) return false;

                ucMeter uc = _lstUCMeters[sId];
                return uc.MeterEnabled;
            }
        }
        public static bool ContainerMinimises(string sId)
        {
            lock (_metersLock)
            {
                if (_lstUCMeters == null) return false;
                if (!_lstUCMeters.ContainsKey(sId)) return false;

                ucMeter uc = _lstUCMeters[sId];
                return uc.ContainerMinimises;
            }
        }
        public static void ContainerNotes(string sId, string notes)
        {
            lock (_metersLock)
            {
                if (_lstUCMeters == null) return;
                if (!_lstUCMeters.ContainsKey(sId)) return;
                if (!_DXrenderers.ContainsKey(sId)) return;

                ucMeter uc = _lstUCMeters[sId];
                uc.Notes = notes;
            }
        }
        public static string GetContainerNotes(string sId)
        {
            lock (_metersLock)
            {
                if (_lstUCMeters == null) return "";
                if (!_lstUCMeters.ContainsKey(sId)) return "";

                ucMeter uc = _lstUCMeters[sId];
                return uc.Notes;
            }
        }
        public static void ContainerBackgroundColour(string sId, System.Drawing.Color c)
        {
            lock (_metersLock)
            {
                if (_lstUCMeters == null) return;
                if (!_lstUCMeters.ContainsKey(sId)) return;
                if (!_DXrenderers.ContainsKey(sId)) return;

                ucMeter uc = _lstUCMeters[sId];
                uc.BackColor = c;
                DXRenderer r = _DXrenderers[sId];
                r.BackgroundColour = c;
            }
        }
        public static System.Drawing.Color GetContainerBackgroundColour(string sId)
        {
            lock (_metersLock)
            {
                if (_lstUCMeters == null) return System.Drawing.Color.Empty;
                if (!_lstUCMeters.ContainsKey(sId)) return System.Drawing.Color.Empty;

                ucMeter uc = _lstUCMeters[sId];
                return uc.BackColor;
            }
        }
        public static void HighlightContainer(string sId)
        {
            if (_DXrenderers == null) return;

            foreach (KeyValuePair<string, DXRenderer> kvp in _DXrenderers)
            {
                kvp.Value.HighlightEdge = false;
            }

            if (_DXrenderers.ContainsKey(sId))
            {
                _DXrenderers[sId].HighlightEdge = true;
            }
        }
        public static void DisposeImageData()
        {
            foreach (KeyValuePair<string, DataStream> kvp in _pooledStreamData)
            {
                DataStream tempStream = kvp.Value;
                Utilities.Dispose(ref tempStream);
                tempStream = null;
            }
            _pooledStreamData.Clear();

            foreach (KeyValuePair<string, System.Drawing.Bitmap> kvp in _pooledImages)
            {
                System.Drawing.Bitmap tempBmp = kvp.Value;
                tempBmp.Dispose();
            }
            _pooledImages.Clear();
        }
        public static void Init(Console c)
        {
            _console = c;
            //_power = _console.PowerOn;
            _rx1VHForAbove = _console.VFOAFreq >= _console.S9Frequency;
            _rx2VHForAbove = _console.RX2Enabled && _console.VFOBFreq >= _console.S9Frequency;
            _currentHPSDRmodel = _console.CurrentHPSDRModel;
            _apolloPresent = _console.ApolloPresent;
            _alexPresent = _console.AlexPresent;
            _paPresent = _console.PAPresent;
            _transverterIndex = _console.TXXVTRIndex;
            
            addDelegates();

            _meterThread = new Thread(new ThreadStart(UpdateMeters))
            {
                Name = "Multimeter Thread",
                Priority = ThreadPriority.Lowest,
                IsBackground = true
            };
            _meterThread.Start();
        }
        private static Dictionary<string, DXRenderer> _DXrenderers = new Dictionary<string, DXRenderer>();
        private static void addRenderer(string sId, int rx, PictureBox target, clsMeter meter, System.Drawing.Color backColour)
        {
            DXRenderer renderer = new DXRenderer(sId, rx, target, _console, meter);
            renderer.BackgroundColour = backColour;

            _DXrenderers.Add(sId, renderer);
        }
        public static void UpdateS9()
        {
            _rx1VHForAbove = _console.VFOAFreq >= _console.S9Frequency;
            _rx2VHForAbove = _console.RX2Enabled && _console.VFOBFreq >= _console.S9Frequency;
            zeroAllMeters();
        }
        public static void RefreshAllImages()
        {
            if (_DXrenderers.Count < 1) return;

            //remove skins from cache
            ClearAllCachedImageData();

            foreach (KeyValuePair<string, DXRenderer> kvp in _DXrenderers)
            {
                DXRenderer r = kvp.Value;
                r.RemoveAllDXImages();
            }

            foreach (KeyValuePair<string, DXRenderer> kvp in _DXrenderers)
            {
                DXRenderer r = kvp.Value;
                r.LoadDXImages(_openHPSDR_appdatapath, _current_skin_path);
            }
        }
        private static void LoadDXSkinImages()
        {
            if (_DXrenderers.Count < 1) return;

            //remove skins from cache
            ClearAllCachedImageData(true);

            foreach (KeyValuePair<string, DXRenderer> kvp in _DXrenderers)
            {
                DXRenderer r = kvp.Value;
                r.RemoveAnySkinImages();              
            }

            foreach (KeyValuePair<string, DXRenderer> kvp in _DXrenderers)
            {
                DXRenderer r = kvp.Value;
                r.LoadDXImages(_openHPSDR_appdatapath, _current_skin_path);
            }
        }
        public static void RunRendererDisplay(string sId)
        {
            if (!_DXrenderers.ContainsKey(sId)) return;

            DXRenderer r = _DXrenderers[sId];
            r.RunDisplay();
            r.LoadDXImages(_openHPSDR_appdatapath, _current_skin_path);
        }
        public static void RunAllRendererDisplays()
        {
            if (_DXrenderers.Count < 1) return;

            foreach(KeyValuePair <string, DXRenderer> kvp in _DXrenderers)
            {
                RunRendererDisplay(kvp.Key);
            }
        }
        private static string _current_skin = "IK3VIG Special"; // matches selectSkin() fn in setup.cs
        private static string _current_skin_path = "";
        public static string CurrentSkinPath
        {
            get { return _current_skin_path; }
        }
        public static bool AlwaysUpdateSkin { get; set; }
        public static string CurrentSkin
        {
            get { return _current_skin; }
            set
            {
                _current_skin_path = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) +
                    "\\OpenHPSDR\\Skins\\" + value;

                if (value != _current_skin || AlwaysUpdateSkin)
                {
                    LoadDXSkinImages();
                    _current_skin = value;
                }
            }
        }
        private static void removeRenderer(string sId)
        {
            if (!_DXrenderers.ContainsKey(sId)) return;

            DXRenderer r = _DXrenderers[sId];
            r.ShutdownDX();

            _DXrenderers.Remove(sId);
        }
        public static void Shutdown()
        {
            if (_image_fetcher != null)
                _image_fetcher.Shutdown();                
                
            MultiMeterIO.StopConnections();

            removeDelegates();

            foreach (KeyValuePair<string, DXRenderer> kvp in _DXrenderers)
            {
                kvp.Value.ShutdownDX();
            }

            DisposeImageData();

            _meterThreadRunning = false;
            if (_meterThread != null && _meterThread.IsAlive)
            {
                int nWait = 0;
                lock (_metersLock)
                {
                    foreach (KeyValuePair<string, clsMeter> kvp in _meters)
                    {
                        clsMeter m = kvp.Value;

                        int nTmp = m.DelayForUpdate();
                        if (nTmp > nWait) nWait = nTmp;
                    }
                }
                _meterThread.Join(nWait + 100); // slightly longer
            }

            foreach (KeyValuePair<string, frmMeterDisplay> kvp in _lstMeterDisplayForms)
            {
                kvp.Value.Close();
            }
        }
        private static void addDelegates()
        {
            if (_delegatesAdded || _console == null) return;

            _console.MeterReadingsChangedHandlers += OnMeterReadings;
            _console.MoxPreChangeHandlers += OnPreMox;
            _console.MoxChangeHandlers += OnMox;
            _console.PowerChangeHanders += OnPower;
            _console.VFOAFrequencyChangeHandlers += OnVFOA;
            _console.VFOBFrequencyChangeHandlers += OnVFOB;
            _console.BandChangeHandlers += OnBandChange;
            _console.BandPreChangeHandlers += OnPreBandChange;
            _console.ModeChangeHandlers += OnModeChangeHandler;
            _console.SplitChangedHandlers += OnSplitChanged;
            _console.VFOTXChangedHandlers += OnVFOTXChanged;
            _console.TXBandChangeHandlers += OnTXBandChanged;
            _console.FilterChangedHandlers += OnFilterChanged;
            _console.MultiRxHandlers += OnMultiRxChanged;
            _console.VFOASubFrequencyChangeHandlers += OnVFOASub;

            _console.AlexPresentChangedHandlers += OnAlexPresentChanged;
            _console.PAPresentChangedHandlers += OnPAPresentChanged;
            _console.ApolloPresentChangedHandlers += OnApolloPresentChanged;
            _console.CurrentModelChangedHandlers += OnCurrentModelChanged;
            _console.TransverterIndexChangedHandlers += OnTransverterIndexChanged;

            _console.RX2EnabledChangedHandlers += OnRX2EnabledChanged;
            _console.RX2EnabledPreChangedHandlers += OnRX2EnabledPreChanged;

            _console.EQChangedHandlers += OnEQChanged;
            _console.LevelerChangedHandlers += OnLevelerChanged;
            _console.CFCChangedHandlers += OnCFCChanged;
            _console.CompandChangedHandlers += OnCompandChanged;

            _console.QuickSplitChangedHandlers += OnQuickSplitChanged;

            _delegatesAdded = true;
        }
        private static void removeDelegates()
        {
            if (!_delegatesAdded || _console == null) return;

            _console.MeterReadingsChangedHandlers -= OnMeterReadings;
            _console.MoxPreChangeHandlers -= OnPreMox;
            _console.MoxChangeHandlers -= OnMox;
            _console.PowerChangeHanders -= OnPower;
            _console.VFOAFrequencyChangeHandlers -= OnVFOA;
            _console.VFOBFrequencyChangeHandlers -= OnVFOB;
            _console.BandChangeHandlers -= OnBandChange;
            _console.BandPreChangeHandlers -= OnPreBandChange;
            _console.ModeChangeHandlers -= OnModeChangeHandler;
            _console.SplitChangedHandlers -= OnSplitChanged;
            _console.VFOTXChangedHandlers -= OnVFOTXChanged;
            _console.TXBandChangeHandlers -= OnTXBandChanged;
            _console.FilterChangedHandlers -= OnFilterChanged;
            _console.MultiRxHandlers -= OnMultiRxChanged;
            _console.VFOASubFrequencyChangeHandlers -= OnVFOASub;

            _console.AlexPresentChangedHandlers -= OnAlexPresentChanged;
            _console.PAPresentChangedHandlers -= OnPAPresentChanged;
            _console.ApolloPresentChangedHandlers -= OnApolloPresentChanged;
            _console.CurrentModelChangedHandlers -= OnCurrentModelChanged;
            _console.TransverterIndexChangedHandlers -= OnTransverterIndexChanged;

            _console.RX2EnabledChangedHandlers -= OnRX2EnabledChanged;
            _console.RX2EnabledPreChangedHandlers -= OnRX2EnabledPreChanged;

            _console.EQChangedHandlers -= OnEQChanged;
            _console.LevelerChangedHandlers -= OnLevelerChanged;
            _console.CFCChangedHandlers -= OnCFCChanged;
            _console.CompandChangedHandlers -= OnCompandChanged;

            _console.QuickSplitChangedHandlers -= OnQuickSplitChanged;

            foreach (KeyValuePair<string, ucMeter> kvp in _lstUCMeters)
            {
                kvp.Value.RemoveDelegates();
            }

            _delegatesAdded = false;
        }
        private static void OnSplitChanged(int rx, bool oldSplit, bool newSplit)
        {
            lock (_metersLock)
            {
                bool updateVFOASub = _console.VFOASubInUse; //[2.10.1.0] MW0LGE needed because at init rx2 might not be enabled, and the init function will have been given -999.999 from console.vfoasubfreq
                double freq = _console.VFOASubFreq;

                foreach (KeyValuePair<string, clsMeter> mkvp in _meters)
                {
                    clsMeter m = mkvp.Value;
                    m.Split = rx == m.RX && newSplit;

                    if (rx == m.RX && updateVFOASub) m.VfoSub = freq;
                }
            }
        }
        private static void OnFilterChanged(int rx, Filter oldFilter, Filter newFilter, Band band, int low, int high, string sName)
        {
            lock (_metersLock)
            {
                foreach (KeyValuePair<string, clsMeter> ms in _meters.Where(o => o.Value.RX == rx))
                {
                    clsMeter m = ms.Value;

                    m.FilterVfoA = newFilter;
                    m.FilterVfoB = newFilter;

                    m.FilterVfoAName = sName;
                    m.FilterVfoBName = sName;
                }
            }
        }
        private static void OnMultiRxChanged(bool newState, bool oldState, double vfoASubFrequency, Band b, bool bRx2Enabled)
        {
            lock (_metersLock)
            {
                foreach (KeyValuePair<string, clsMeter> ms in _meters)
                {
                    clsMeter m = ms.Value;
                    m.MultiRxEnabled = newState;
                    m.VfoSub = vfoASubFrequency;
                    m.BandVfoASub = b;
                }
            }
        }

        private static void OnTXBandChanged(Band oldBand, Band newBand)
        {
        }
        private static void OnVFOTXChanged(bool vfoB, bool oldState, bool newState)
        {
            lock (_metersLock)
            {
                foreach (KeyValuePair<string, clsMeter> ms in _meters)
                {
                    clsMeter m = ms.Value;
                    m.TXVFOb = vfoB && newState;
                }
            }
        }
        private static void OnModeChangeHandler(int rx, DSPMode oldMode, DSPMode newMode, Band oldBand, Band newBand)
        {
            lock (_metersLock)
            {
                foreach (KeyValuePair<string, clsMeter> ms in _meters.Where(o => o.Value.RX == rx))
                {
                    clsMeter m = ms.Value;
                    if (rx == 1)
                    {
                        m.ModeVfoA = newMode;
                        m.ModeVfoB = newMode;
                    }
                    else
                    {
                        m.ModeVfoB = newMode;
                    }
                }
            }
        }
        private static void OnBandChange(int rx, Band oldBand, Band newBand)
        {
            lock (_metersLock)
            {
                foreach (KeyValuePair<string, clsMeter> ms in _meters.Where(o => o.Value.RX == rx))
                {
                    clsMeter m = ms.Value;

                    if (rx == 1)
                        m.BandVfoA = newBand;
                    else
                        m.BandVfoB = newBand;

                    m.ZeroOut(true, false);
                }
            }
        }
        private static void OnPreBandChange(int rx, Band currentBand)
        {
            OnBandChange(rx, currentBand, currentBand);
        }
        private static void OnTransverterIndexChanged(int oldIndex, int newIndex)
        {
            _transverterIndex = newIndex;
        }
        public static void OnAlexPresentChanged(bool oldSetting, bool newSetting)
        {
            _alexPresent = newSetting;
        }
        public static void OnPAPresentChanged(bool oldSetting, bool newSetting)
        {
            _paPresent = newSetting;
        }
        public static void OnApolloPresentChanged(bool oldSetting, bool newSetting)
        {
            _apolloPresent = newSetting;
        }
        public static void OnCurrentModelChanged(HPSDRModel oldModel, HPSDRModel newModel)
        {
            _currentHPSDRmodel = newModel;
        }
        private static void OnVFOA(Band oldBand, Band newBand, DSPMode oldMode, DSPMode newMode, Filter oldFilter, Filter newFilter, double oldFreq, double newFreq, double oldCentreF, double newCentreF, bool oldCTUN, bool newCTUN, int oldZoomSlider, int newZoomSlider, double offset, int rx)
        {
            if(rx == 1)
                _rx1VHForAbove = newFreq >= _console.S9Frequency;

            lock (_metersLock)
            {
                foreach (KeyValuePair<string, clsMeter> mkvp in _meters.Where(o => o.Value.RX == rx))
                {
                    clsMeter m = mkvp.Value;
                    m.VfoA = newFreq;
                    m.ModeVfoA = newMode;
                    m.BandVfoA = newBand;
                }
            }
        }
        private static void OnVFOB(Band oldBand, Band newBand, DSPMode oldMode, DSPMode newMode, Filter oldFilter, Filter newFilter, double oldFreq, double newFreq, double oldCentreF, double newCentreF, bool oldCTUN, bool newCTUN, int oldZoomSlider, int newZoomSlider, double offset, int rx)
        {
            //_rx2VHForAbove = _console.RX2Enabled && newFreq/*_console.VFOBFreq*/ >= 30;
            if (rx == 2)
                _rx2VHForAbove = _console.RX2Enabled && newFreq >= _console.S9Frequency;

            lock (_metersLock)
            {
                foreach (KeyValuePair<string, clsMeter> mkvp in _meters.Where(o => o.Value.RX == rx))
                {
                    clsMeter m = mkvp.Value;
                    m.VfoB = newFreq;
                    m.ModeVfoB = newMode;
                    m.BandVfoB = newBand;
                }
            }
        }
        public static void OnVFOASub(Band oldBand, Band newBand, DSPMode newMode, Filter newFilter, double oldFreq, double newFreq, double newCentreF, bool newCTUN, int newZoomSlider, double offset, int rx)
        {
            lock (_metersLock)
            {
                foreach (KeyValuePair<string, clsMeter> mkvp in _meters.Where(o => o.Value.RX == 1)) // only applies to rx1
                {
                    clsMeter m = mkvp.Value;
                    m.VfoSub = newFreq;
                    //m.ModeVfoA = newMode;
                    m.BandVfoASub = newBand;
                }
            }
        }
        private static void initAllConsoleData()
        {
            // list used so that not limited to rx1/rx2
            List<int> rxNumbers = new List<int>();

            lock (_metersLock)
            {
                foreach (KeyValuePair<string, clsMeter> mkvp in _meters)
                {
                    // get rx numbers
                    if (!rxNumbers.Contains(mkvp.Value.RX)) rxNumbers.Add(mkvp.Value.RX);
                }
            }

            // gather all the data again
            foreach (int rx in rxNumbers)
            {
                initConsoleData(rx);
            }

        }
        private static void initConsoleData(int rx)
        {
            if (_console == null) return;

            lock (_metersLock)
            {
                foreach (KeyValuePair<string, clsMeter> mkvp in _meters.Where(o => o.Value.RX == rx))
                {
                    clsMeter m = mkvp.Value;

                    m.MOX = _console.MOX;
                    m.Split = _console.VFOSplit;
                    m.TXVFOb = _console.VFOBTX;
                    m.RX2Enabled = _console.RX2Enabled;
                    m.MultiRxEnabled = _console.chkEnableMultiRX.Checked;

                    m.VfoA = _console.VFOAFreq;
                    m.ModeVfoA = _console.RX1DSPMode;
                    m.BandVfoA = _console.RX1Band;
                    m.FilterVfoA = _console.RX1Filter;
                    m.FilterVfoAName = getFilterName(1);// _console.rx1_filters[(int)_console.RX1DSPMode].GetName(_console.RX1Filter);
                    
                    m.VfoB = _console.VFOBFreq;

                    m.VfoSub = _console.VFOASubFreq;

                    if (!_console.RX2Enabled)
                    {
                        // same as rx1 unless rx2 enabled
                        m.ModeVfoB = _console.RX1DSPMode;
                        m.BandVfoB = _console.GetRX1BandForVFOb();//_console.RX1Band;
                        m.FilterVfoB = _console.RX1Filter;
                        m.FilterVfoBName = getFilterName(1);//_console.rx1_filters[(int)_console.RX1DSPMode].GetName(_console.RX1Filter);
                    }
                    else
                    {
                        m.ModeVfoB = _console.RX2DSPMode;
                        m.BandVfoB = _console.RX2Band;
                        m.FilterVfoB = _console.RX2Filter;
                        m.FilterVfoBName = getFilterName(2);//_console.rx2_filters[(int)_console.RX2DSPMode].GetName(_console.RX2Filter);
                    }

                    m.TXEQEnabled = _console.TXEQ;
                    m.LevelerEnabled = !_console.IsSetupFormNull && _console.SetupForm.TXLevelerOn;
                    m.CFCEnabled = _console.CFCEnabled;
                    m.CompandEnabled = _console.CPDR;

                    m.QuickSplitEnabled = _console.GetQuickSplitEnabled;
                }
            }
        }
        private static string getFilterName(int rx)
        {
            try
            {
                if (rx == 1)
                {
                    if (_console.RX1DSPMode == DSPMode.FIRST || _console.RX1DSPMode == DSPMode.LAST ||
                        _console.RX1Filter == Filter.FIRST || _console.RX1Filter == Filter.LAST) return "";

                    return _console.rx1_filters[(int)_console.RX1DSPMode].GetName(_console.RX1Filter);
                }
                else if (rx == 2)
                {
                    if (_console.RX2DSPMode == DSPMode.FIRST || _console.RX2DSPMode == DSPMode.LAST ||
                        _console.RX2Filter == Filter.FIRST || _console.RX2Filter == Filter.LAST) return "";

                    return _console.rx2_filters[(int)_console.RX2DSPMode].GetName(_console.RX2Filter);
                }
                else
                {
                    return "";
                }
            }
            catch
            {
                return "";
            }
        }
        private static void OnPower(bool oldPower, bool newPower)
        {
            //_power = newPower;

            if(oldPower != newPower)
            {
                lock (_metersLock)
                {
                    foreach (KeyValuePair<string, clsMeter> ms in _meters)
                    {
                        clsMeter m = ms.Value;

                        m.ZeroOut(true, true);
                    }
                }
            }
        }
        private static void OnPreMox(int rx, bool oldMox, bool newMox)
        {
            lock (_metersLock)
            {
                // ignores any readings that comes in after the premox event, is reset in OnMox which happens at end of console mox transition code
                // prevents inflight readings from causing glitches/errors in readings
                if (_readingIgnore != null && _readingIgnore.ContainsKey(rx))
                    _readingIgnore[rx] = true;
            }
        }
        private static void OnMox(int rx, bool oldMox, bool newMox)
        {
            lock (_metersLock)
            {
                foreach (KeyValuePair<string, clsMeter> mkvp in _meters)
                {
                    clsMeter m = mkvp.Value;

                    m.MOX = rx == m.RX && newMox;

                    if (newMox && !oldMox)
                        // now tx from rx
                        m.ZeroOut(true, false); // reset rx readings
                    else if (!newMox && oldMox)
                        // now rx from tx
                        m.ZeroOut(false, true); // reset tx readings
                }

                if (_readingIgnore != null && _readingIgnore.ContainsKey(rx))
                    _readingIgnore[rx] = false;
            }
        }
        private static void OnEQChanged(bool oldState, bool newState)
        {
            lock (_metersLock)
            {
                foreach (KeyValuePair<string, clsMeter> mkvp in _meters)
                {
                    clsMeter m = mkvp.Value;
                    m.TXEQEnabled = newState;

                    m.DisableMeterType(MeterType.EQ, !newState);
                }
            }
        }
        private static void OnLevelerChanged(bool oldState, bool newState)
        {
            lock (_metersLock)
            {
                foreach (KeyValuePair<string, clsMeter> mkvp in _meters)
                {
                    clsMeter m = mkvp.Value;
                    m.LevelerEnabled = newState;

                    m.DisableMeterType(MeterType.LEVELER, !newState);
                    m.DisableMeterType(MeterType.LEVELER_GAIN, !newState);
                }
            }
        }
        private static void OnCFCChanged(bool oldState, bool newState)
        {
            lock (_metersLock)
            {
                foreach (KeyValuePair<string, clsMeter> mkvp in _meters)
                {
                    clsMeter m = mkvp.Value;
                    m.CFCEnabled = newState;

                    m.DisableMeterType(MeterType.CFC, !newState);
                    m.DisableMeterType(MeterType.CFC_GAIN, !newState);
                }
            }
        }
        private static void OnCompandChanged(bool oldState, bool newState)
        {
            lock (_metersLock)
            {
                foreach (KeyValuePair<string, clsMeter> mkvp in _meters)
                {
                    clsMeter m = mkvp.Value;
                    m.CompandEnabled = newState;

                    m.DisableMeterType(MeterType.COMP, !newState);
                }
            }
        }
        private static void OnQuickSplitChanged(bool oldState, bool newState)
        {
            lock (_metersLock)
            {
                foreach (KeyValuePair<string, clsMeter> mkvp in _meters)
                {
                    clsMeter m = mkvp.Value;
                    m.QuickSplitEnabled = newState;
                }
            }
        }
        public static int CurrentPowerRating
        {
            get
            {
                // power choice based on code from console.getMeterPixelPosAndDrawScales
                // TODO: 8000mk2  !!!!!!!!!!!!!!!!!!!!
                // TODO: ANAN_G2_1K

                int nWatts = 500;

                if (_alexPresent && 
                    ((_currentHPSDRmodel == HPSDRModel.ORIONMKII || _currentHPSDRmodel == HPSDRModel.ANAN8000D || _currentHPSDRmodel == HPSDRModel.ANAN_G2) 
                    && _transverterIndex < 0))
                {
                    nWatts = 200;
                }
                else if ((_alexPresent || _paPresent) &&
                                (_currentHPSDRmodel != HPSDRModel.ANAN10 &&
                                 _currentHPSDRmodel != HPSDRModel.ANAN10E &&
                                !_apolloPresent))
                {
                    nWatts = 100;
                }
                else if (_currentHPSDRmodel == HPSDRModel.ANAN10 ||
                                _currentHPSDRmodel == HPSDRModel.ANAN10E)
                {
                    nWatts = 30;
                }
                else if (_apolloPresent)
                {
                    nWatts = 15;
                }
                else
                {
                    nWatts = 1;
                }
                
                return nWatts;
            }
        }
        private static float dbmOffsetForAboveS9Frequency(int rx)
        {
            if (IsAboveS9Frequency(rx))
                return 20f;
            else
                return 0f;
        }
        private static bool IsAboveS9Frequency(int rx)
        {
            return (rx == 1 && _rx1VHForAbove) || (rx == 2 && _rx2VHForAbove);
        }
        //private static float normalisePower()
        //{
        //    return a factor to apply to power values to bring them to 100w
        //     this is needed because power meter scaling is based on 100w at full deflection
        //    switch (CurrentPowerRating)
        //    {
        //        case 500: return 1 / 5f;
        //        case 200: return 1 / 2f;
        //        case 100: return 1f;
        //        case 30: return 100 / 30f;
        //        case 15: return 100 / 15f;
        //        case 1: return 100f;
        //    }

        //    return 1f;

        //}
        private static float getReading(int rx, Reading rt, bool bUseReading = false)
        {
            if (rt == Reading.NONE) return -200f;

            //lock (_readingsLock)
            {
                return _readings[rx].GetReading(rt, bUseReading);
            }
        }
        private static void setReading(int rx, Reading rt, ref Dictionary<Reading, float> readings, bool bChangeOverride = false)
        {
            // locked outside
            if (_readings[rx].RequiresUpdate(rt) && (!_readingIgnore[rx] || bChangeOverride)) _readings[rx].SetReading(rt, readings[rt]);
        }
        private static void setReadingForced(int rx, Reading rt, float reading)
        {
            //lock (_readingsLock)
            {
                _readings[rx].SetReading(rt, reading);
            }
        }
        private static void OnMeterReadings(int rx, bool mox, ref Dictionary<Reading, float> readings)
        {
            //lock (_readingsLock)
            {
                //
                if (!mox)
                {
                    setReading(rx, Reading.SIGNAL_STRENGTH, ref readings);
                    setReading(rx, Reading.AVG_SIGNAL_STRENGTH, ref readings);
                    setReading(rx, Reading.ADC_PK, ref readings);
                    setReading(rx, Reading.ADC_AV, ref readings);
                    setReading(rx, Reading.AGC_PK, ref readings);
                    setReading(rx, Reading.AGC_AV, ref readings);
                    setReading(rx, Reading.AGC_GAIN, ref readings);
                    //setReading(rx, Reading.EYE_PERCENT, ref readings);

                    setReading(rx, Reading.ESTIMATED_PBSNR, ref readings);
                }
                else
                {
                    setReading(rx, Reading.MIC, ref readings);
                    setReading(rx, Reading.MIC_PK, ref readings);
                    setReading(rx, Reading.EQ_PK, ref readings);
                    setReading(rx, Reading.EQ, ref readings);
                    setReading(rx, Reading.LEVELER_PK, ref readings);
                    setReading(rx, Reading.LEVELER, ref readings);
                    setReading(rx, Reading.LVL_G, ref readings);
                    setReading(rx, Reading.CFC_PK, ref readings);
                    setReading(rx, Reading.CFC_AV, ref readings);
                    setReading(rx, Reading.CFC_G, ref readings);
                    //setReading(rx, Reading.CPDR_PK, ref readings);
                    //setReading(rx, Reading.CPDR, ref readings);

                    setReading(rx, Reading.ALC_PK, ref readings);
                    setReading(rx, Reading.ALC, ref readings);
                    setReading(rx, Reading.ALC_G, ref readings);

                    setReading(rx, Reading.COMP, ref readings);
                    setReading(rx, Reading.COMP_PK, ref readings);
                    setReading(rx, Reading.ALC_GROUP, ref readings);

                    //setReading(rx, Reading.CPDR, ref readings);//CPDR is the same as comp
                    //setReading(rx, Reading.CPDR_PK, ref readings);//CPDR is the same as comp

                    setReading(rx, Reading.PWR, ref readings);
                    setReading(rx, Reading.REVERSE_PWR, ref readings);
                    setReading(rx, Reading.SWR, ref readings);

                    if (rx == 1)
                    { // only rx1 data
                        setReading(rx, Reading.DRIVE_FWD_ADC, ref readings);
                        setReading(rx, Reading.FWD_ADC, ref readings);
                        setReading(rx, Reading.REV_ADC, ref readings);
                        setReading(rx, Reading.DRIVE_PWR, ref readings);
                        setReading(rx, Reading.PA_FWD_PWR, ref readings);
                        setReading(rx, Reading.PA_REV_PWR, ref readings);
                        setReading(rx, Reading.CAL_FWD_PWR, ref readings);
                    }
                }
                setReading(rx, Reading.VOLTS, ref readings);
                setReading(rx, Reading.AMPS, ref readings);    
            }
        }
        public static bool RequiresUpdate(int rx, Reading rt)
        {
            //lock (_readingsLock)
            {
                return _readings[rx].RequiresUpdate(rt);
            }
        }

        // meters
        public static int QuickestUpdateInterval(int rx, bool mox)
        {
            int updateRate = int.MaxValue;

            lock (_metersLock)
            {
                foreach (KeyValuePair<string, clsMeter> kvp in _meters.Where(kvp => kvp.Value.RX == rx))
                {
                    int ui = kvp.Value.QuickestUpdateInterval(mox);
                    if (ui < updateRate) updateRate = ui;
                }
            }

            if (updateRate == int.MaxValue) updateRate = 250; // none found, use 250mS

            return updateRate;
        }

        private static Dictionary<string, frmMeterDisplay> _lstMeterDisplayForms = new Dictionary<string, frmMeterDisplay>();
        private static Dictionary<string, ucMeter> _lstUCMeters = new Dictionary<string, ucMeter>();
        public static clsMeter MeterFromId(string sId)
        {
            lock (_metersLock)
            {
                if (_meters == null) return null;
                if (!_meters.ContainsKey(sId)) return null;

                return _meters[sId];
            }
        }
        public static bool MeterExists(string sId)
        {
            lock (_metersLock)
            {
                if (_meters == null) return false;
                return _meters.ContainsKey(sId);
            }
        }
        public static void GlobalKeyDown(Keys keycode)
        {
            lock (_metersLock)
            {
                foreach (KeyValuePair<string, clsMeter> kvp in _meters)
                {
                    clsMeter m = kvp.Value;
                    m.KeyDown(keycode);
                }
            }
        }
        public static void GlobalKeyUp(Keys keycode)
        {
            lock (_metersLock)
            {
                foreach (KeyValuePair<string, clsMeter> kvp in _meters)
                {
                    clsMeter m = kvp.Value;
                    m.KeyUp(keycode);
                }
            }
        }
        public static void AddMeterContainer(ucMeter ucM, bool bFromRestore = false)
        {
            if (_console == null) return;

            lock (_metersLock)
            {
                bool bEnabled = ucM.MeterEnabled;

                ucM.Console = _console;
                ucM.FloatingDockedClicked += ucMeter_FloatingDockedClicked;
                ucM.DockedMoved += ucMeter_FloatingDockedMoved;
                ucM.SettingsClicked += ucMeter_SettingsClicked;

                // float form
                frmMeterDisplay f = new frmMeterDisplay(_console, ucM.RX);
                f.ID = ucM.ID;

                // meter items
                clsMeter meter = new clsMeter(ucM.RX, ucM.Name, 1f, 1f);
                meter.ID = ucM.ID;
                meter.Enabled = bEnabled;                

                // a renderer
                addRenderer(ucM.ID, ucM.RX, ucM.DisplayContainer, meter, ucM.BackColor);

                lock (_metersLock)
                {
                    // store everything
                    _lstMeterDisplayForms.Add(f.ID, f);
                    _lstUCMeters.Add(ucM.ID, ucM);
                    _meters.Add(meter.ID, meter);
                }

                // init all the meter info from console
                initConsoleData(ucM.RX);

                if (bEnabled && ucM.MeterEnabled && !bFromRestore) //ignore if we are restoring from db. Meters are shown by FinishSetupAndDisplay() from console in this case
                {
                    if (ucM.Floating)
                    {
                        setMeterFloating(ucM, f);
                    }
                    else
                        returnMeterFromFloating(ucM, f);
                }
            }
        }
        public static void FinishSetupAndDisplay()
        {
            _finishedSetup = true;

            if (_lstUCMeters == null || _lstUCMeters.Count == 0) return;

            initAllConsoleData(); //[2.10.3.6]MW0LGE get all console info here, as everything will be at the correct state
            zeroAllMeters();

            lock (_metersLock)
            {
                foreach (KeyValuePair<string, ucMeter> ucms in _lstUCMeters)
                {
                    ucMeter ucm = ucms.Value;
                    if (_lstMeterDisplayForms.ContainsKey(ucm.ID) && ucm.MeterEnabled)
                    {
                        // setup
                        frmMeterDisplay f = _lstMeterDisplayForms[ucm.ID];

                        if (ucm.Floating)
                        {
                            setMeterFloating(ucm, f);
                        }
                        else
                            returnMeterFromFloating(ucm, f);
                    }
                }
            }
        }
        public static void BringToFront()
        {
            if (_lstUCMeters == null || _lstUCMeters.Count == 0) return;

            lock (_metersLock)
            {
                foreach (KeyValuePair<string, ucMeter> ucms in _lstUCMeters)
                {
                    ucMeter ucm = ucms.Value;
                    if (!ucm.Floating)
                        ucm.BringToFront();
                }
            }
        }
        public static string AddMeterContainer(int nRx, bool bFloating)//, bool bEnabled = false)
        {
            ucMeter ucM = new ucMeter();
            ucM.RX = nRx;
            ucM.Floating = bFloating;

            AddMeterContainer(ucM);//, bEnabled);
            RunRendererDisplay(ucM.ID);

            return ucM.ID;
        }
        public static int TotalMeterContainers
        {
            get 
            {
                lock (_metersLock)
                {
                    return _lstUCMeters.Count;
                }
            }
        }
        private static void ucMeter_SettingsClicked(object sender, EventArgs e)
        {
            ucMeter ucM = (ucMeter)sender;

            if (!_console.IsSetupFormNull)
                _console.SetupForm.ShowMultiMeterSetupTab(ucM.ID);
        }

        private static void ucMeter_FloatingDockedClicked(object sender, EventArgs e)
        {
            ucMeter ucM = (ucMeter)sender;

            if (!_lstMeterDisplayForms.ContainsKey(ucM.ID)) return;

            if (ucM.Floating)
                returnMeterFromFloating(ucM, _lstMeterDisplayForms[ucM.ID]);
            else
                setMeterFloating(ucM, _lstMeterDisplayForms[ucM.ID]);
        }
        private static void ucMeter_FloatingDockedMoved(object sender, EventArgs e)
        {
            ucMeter ucM = (ucMeter)sender;

            ucM.Delta = new System.Drawing.Point(_console.HDelta, _console.VDelta);
        }
        public static void SetPositionOfDockedMeters()
        {
            lock (_metersLock)
            {
                foreach (KeyValuePair<string, ucMeter> kvp in _lstUCMeters)
                {
                    setPoisitionOfDockedMeter(kvp.Value);
                }
            }
        }
        private static void setPoisitionOfDockedMeter(ucMeter m)
        {
            if (_console == null) return;

            int HDelta = _console.HDelta;
            int VDelta = _console.VDelta;

            if (!m.Floating)
            {
                System.Drawing.Point newLocation = new System.Drawing.Point();

                switch (m.AxisLock)
                {
                    case Axis.RIGHT:
                    case Axis.BOTTOMRIGHT:
                        //case Axis.NONE:
                        newLocation = new System.Drawing.Point(m.DockedLocation.X - m.Delta.X + HDelta, m.DockedLocation.Y - m.Delta.Y + VDelta);
                        break;
                    case Axis.BOTTOMLEFT:
                    case Axis.LEFT:
                        newLocation = new System.Drawing.Point(m.DockedLocation.X, m.DockedLocation.Y - m.Delta.Y + VDelta);
                        break;
                    case Axis.TOPLEFT:
                        newLocation = new System.Drawing.Point(m.DockedLocation.X, m.DockedLocation.Y);
                        break;
                    case Axis.TOP:
                        newLocation = new System.Drawing.Point(m.DockedLocation.X - m.Delta.X + HDelta, m.DockedLocation.Y);
                        break;
                    case Axis.BOTTOM:
                        newLocation = new System.Drawing.Point(m.DockedLocation.X - m.Delta.X + HDelta, m.DockedLocation.Y - m.Delta.Y + VDelta);
                        break;
                    case Axis.TOPRIGHT:
                        newLocation = new System.Drawing.Point(m.DockedLocation.X - m.Delta.X + HDelta, m.DockedLocation.Y);
                        break;
                }

                //limit to client area
                if (newLocation.X + m.Width > _console.Width) newLocation.X = _console.Width - m.Width;
                if (newLocation.Y + m.Height > _console.Height) newLocation.Y = _console.Height - m.Height;
                if (newLocation.X < 0) newLocation.X = 0;
                if (newLocation.Y < 0) newLocation.Y = 0;

                m.Location = newLocation;
            }
        }
        private static void returnMeterFromFloating(ucMeter m, frmMeterDisplay frm)
        {
            if (_console == null) return;

            frm.Hide();
            m.Hide();
            m.Parent = _console;
            m.Anchor = AnchorStyles.None;// AnchorStyles.Right | AnchorStyles.Bottom;
            m.RestoreLocation();
            m.Floating = false;
            setPoisitionOfDockedMeter(m);
            m.BringToFront();

            if (m.RX == 2 && !_console.RX2Enabled) return;

            if (!_finishedSetup) return;
            m.Show();
        }
        private static void setMeterFloating(ucMeter m, frmMeterDisplay frm)
        {
            if (_console == null) return;

            if (m.RX == 2 && !_console.RX2Enabled) return;

            m.Hide();
            frm.TakeOwner(m);
            m.Floating = true;

            if (m.RX == 2 && !_console.RX2Enabled) return;

            if (!_finishedSetup) return;

            frm.Opacity = 0f;
            frm.Show();
            Common.FadeIn(frm, 100);
        }
        private static void OnRX2EnabledChanged(bool enabled)
        {
            lock (_metersLock)
            {
                if (enabled)
                {
                    foreach (KeyValuePair<string, ucMeter> kvp in _lstUCMeters.Where(o => o.Value.RX == 2))
                    {
                        ucMeter ucM = kvp.Value;

                        if (!_lstMeterDisplayForms.ContainsKey(ucM.ID) || !ucM.MeterEnabled) return;

                        if (ucM.Floating)
                            setMeterFloating(ucM, _lstMeterDisplayForms[ucM.ID]);
                        else
                            returnMeterFromFloating(ucM, _lstMeterDisplayForms[ucM.ID]);
                    }
                }
            }

            initAllConsoleData();
        }
        private static void OnRX2EnabledPreChanged(bool enabled)
        {
            if (!enabled)
            {
                lock (_metersLock)
                {
                    foreach (KeyValuePair<string, ucMeter> kvp in _lstUCMeters.Where(o => o.Value.RX == 2))
                    {
                        ucMeter ucM = kvp.Value;

                        if (!_lstMeterDisplayForms.ContainsKey(ucM.ID)) return;

                        if (ucM.Floating)
                            _lstMeterDisplayForms[ucM.ID].Hide();
                        else
                            ucM.Hide();
                    }
                }
            }
        }
        private static void zeroAllMeters()
        {
            lock(_metersLock)
            {
                foreach(KeyValuePair<string, clsMeter> kvp in _meters)
                {
                    clsMeter m = kvp.Value;
                    m.ZeroOut(true, true);
                }
            }
        }
        public static bool RestoreSettings(ref Dictionary<string, string> settings)
        {
            bool bRestoreOk = true;
            try
            {
                foreach (KeyValuePair<string, string> kvp in settings.Where(o => o.Key.StartsWith("meterContData_")))
                {
                    ucMeter ucM = new ucMeter();
                    bool bUcMeterOk = ucM.TryParse(kvp.Value);

                    if (bUcMeterOk)
                    {
                        if (!MeterExists(ucM.ID))
                        {
                            AddMeterContainer(ucM, true);

                            clsMeter m = MeterFromId(ucM.ID);

                            if (m != null)
                            {
                                // now the meter
                                IEnumerable<KeyValuePair<string, string>> meterData = settings.Where(o => o.Key.StartsWith("meterData_" + m.ID));
                                if (meterData != null && meterData.Count() == 1)
                                {
                                    KeyValuePair<string, string> md = meterData.First();

                                    clsMeter tmpMeter = new clsMeter(1, ucM.Name); // dummy init data, will get replaced by tryparse below
                                    tmpMeter.TryParse(md.Value);

                                    // copy to actual meter
                                    // id will be the same
                                    m.Name = tmpMeter.Name;
                                    m.RX = tmpMeter.RX;
                                    m.XRatio = tmpMeter.XRatio;
                                    m.YRatio = tmpMeter.YRatio;
                                    m.DisplayGroup = tmpMeter.DisplayGroup;
                                    m.PadX = tmpMeter.PadX;
                                    m.PadY = tmpMeter.PadY;
                                    m.Height = tmpMeter.Height;
                                }

                                // finally the groups
                                IEnumerable<KeyValuePair<string, string>> meterIGData = settings.Where(o => o.Key.StartsWith("meterIGData_") && o.Value.Contains(m.ID)); // parent id, stored in value
                                foreach (KeyValuePair<string, string> igd in meterIGData)
                                {
                                    clsItemGroup ig = new clsItemGroup();
                                    bool bOk = ig.TryParse(igd.Value);

                                    if (bOk)
                                    {
                                        m.AddMeter(ig.MeterType, ig);

                                        //and the settings
                                        //let us check if version 2 and use that
                                        IEnumerable<KeyValuePair<string, string>> meterIGSettings;
                                        meterIGSettings = settings.Where(o => o.Key.StartsWith("meterIGSettings_2_" + ig.ID));
                                        if (meterIGSettings != null && meterIGSettings.Count() == 1)
                                        {
                                            clsIGSettings igs = new clsIGSettings();
                                            bool bIGSok = igs.TryParse2(meterIGSettings.First().Value);
                                            if (bIGSok) m.ApplySettingsForMeterGroup(ig.MeterType, igs, ig.Order);
                                        }
                                        else
                                        {
                                            meterIGSettings = settings.Where(o => o.Key.StartsWith("meterIGSettings_" + ig.ID) && !o.Key.StartsWith("meterIGSettings_2_"));
                                            if (meterIGSettings != null && meterIGSettings.Count() == 1)
                                            {
                                                clsIGSettings igs = new clsIGSettings();
                                                bool bIGSok = igs.TryParse(meterIGSettings.First().Value);
                                                if (bIGSok) m.ApplySettingsForMeterGroup(ig.MeterType, igs, ig.Order);
                                            }
                                        }
                                    }
                                }
                                m.ZeroOut(true, true);

                                m.Rebuild();
                            }
                        }
                    }
                }
            }
            catch
            {
                bRestoreOk = false;
            }
            return bRestoreOk;
        }        
        public static List<string> GetFormGuidList()
        {
            List<string> sGuidList = new List<string>();

            lock (_metersLock)
            {
                foreach(KeyValuePair<string, frmMeterDisplay> kvp in _lstMeterDisplayForms)
                {
                    frmMeterDisplay fmd = kvp.Value;

                    sGuidList.Add(fmd.ID);
                }
            }

            return sGuidList;
        }
        public static bool StoreSettings2(ref Dictionary<string, string> a)
        {
            if (a == null) return false;

            bool bStoreOk = true;

            try
            {
                lock (_metersLock)
                {
                    // meter container data
                    foreach (KeyValuePair<string, ucMeter> kvp in _lstUCMeters)
                    {
                        a.Add("meterContData_" + kvp.Value.ID, kvp.Value.ToString());

                        // then the meter itself which holds multiple meter items
                        clsMeter m = MeterFromId(kvp.Value.ID);
                        if (m != null)
                        {
                            a.Add("meterData_" + kvp.Value.ID, m.ToString());

                            Dictionary<string, clsItemGroup> groupItems = m.getMeterGroups();

                            // then each meter item which are groups in this case
                            foreach (KeyValuePair<string, clsItemGroup> ig in groupItems)
                            {
                                Dictionary<string, clsMeterItem> mis = m.itemsFromID(ig.Value.ID);
                                if (mis != null)
                                {
                                    foreach (KeyValuePair<string, clsMeterItem> kvp2 in mis.Where(o => o.Value.StoreSettings == true))
                                    {
                                        clsMeterItem mi = kvp2.Value;
                                        if (mi != null)
                                        {
                                            a.Add("meterIGData_" + kvp2.Value.ID, mi.ToString());
                                        }
                                    }
                                }

                                clsIGSettings igs = m.GetSettingsForMeterGroup(ig.Value.MeterType, ig.Value.Order);
                                if (igs != null)
                                {
                                    //a.Add("meterIGSettings_" + ig.Value.ID, igs.ToString()); //[2.10.3.6]MW0LGE not used
                                    a.Add("meterIGSettings_2_" + ig.Value.ID, igs.ToString2());
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
                bStoreOk = false;
            }

            return bStoreOk;
        }
        //public static bool StoreSettings(ref ArrayList a)
        //{
        //    if (a == null) return false;

        //    bool bStoreOk = true;

        //    try
        //    {
        //        lock (_metersLock)
        //        {
        //            // meter container data
        //            foreach (KeyValuePair<string, ucMeter> kvp in _lstUCMeters)
        //            {
        //                a.Add("meterContData_" + kvp.Value.ID + "/" + kvp.Value.ToString());

        //                // then the meter itself which holds multiple meter items
        //                clsMeter m = MeterFromId(kvp.Value.ID);
        //                if (m != null)
        //                {
        //                    a.Add("meterData_" + kvp.Value.ID + "/" + m.ToString());

        //                    Dictionary<string, clsItemGroup> groupItems = m.getMeterGroups();

        //                    // then each meter item which are groups in this case
        //                    foreach (KeyValuePair<string, clsItemGroup> ig in groupItems)
        //                    {
        //                        Dictionary<string, clsMeterItem> mis = m.itemsFromID(ig.Value.ID);
        //                        if (mis != null)
        //                        {
        //                            foreach (KeyValuePair<string, clsMeterItem> kvp2 in mis.Where(o => o.Value.StoreSettings == true))
        //                            {
        //                                clsMeterItem mi = kvp2.Value;
        //                                if (mi != null)
        //                                {
        //                                    a.Add("meterIGData_" + kvp2.Value.ID + "/" + mi.ToString());
        //                                }
        //                            }
        //                        }

        //                        clsIGSettings igs = m.GetSettingsForMeterGroup(ig.Value.MeterType);
        //                        if (igs != null)
        //                        {
        //                            a.Add("meterIGSettings_" + ig.Value.ID + "/" + igs.ToString());
        //                        }
        //                    }
        //                }
        //            }
        //        }
        //    }
        //    catch
        //    {
        //        bStoreOk = false;
        //    }

        //    return bStoreOk;
        //}
        public static Dictionary<string, ucMeter> MeterContainers
        {
            get 
            {
                lock (_metersLock)
                {
                    return _lstUCMeters;
                }
            }
        }
        public static void RemoveMeterContainer(string sId)
        {
            lock (_metersLock)
            {
                if (!_lstUCMeters.ContainsKey(sId)) return;
                if (!_lstMeterDisplayForms.ContainsKey(sId)) return;

                frmMeterDisplay f = _lstMeterDisplayForms[sId];
                f.Hide();

                ucMeter uc = _lstUCMeters[sId];
                uc.Hide();
                // unreg delegates
                uc.FloatingDockedClicked -= ucMeter_FloatingDockedClicked;
                uc.DockedMoved -= ucMeter_FloatingDockedMoved;
                uc.SettingsClicked -= ucMeter_SettingsClicked;

                removeRenderer(sId);

                f.Close();

                _lstMeterDisplayForms.Remove(sId);
                _lstUCMeters.Remove(sId);

                if (_meters.ContainsKey(sId))
                {
                    clsMeter m = _meters[sId];
                    m.RemoveAllMeterTypes();
                    _meters.Remove(sId);
                }
            }
        }
        #endregion
        #region clsMeterItem
        public class clsMeterItem
        {
            public enum MeterItemType
            {
                BASE = 0,
                H_BAR,
                V_BAR,
                H_SCALE,
                V_SCALE,
                NEEDLE,
                NEEDLE_SCALE_PWR,
                TEXT,
                IMAGE,
                SOLID_COLOUR,
                CLICKBOX,
                MAGIC_EYE,
                HISTORY,
                ITEM_GROUP,
                VFO_DISPLAY,
                CLOCK,
                SIGNAL_TEXT_DISPLAY,
                FADE_COVER,
                SPACER,
                TEXT_OVERLAY,
                DATA_OUT,
                ROTATOR,
                LED,
                WEB_IMAGE
                //SPECTRUM
            }

            public class clsPercCache
            {
                public float _value;
                public float _percX;
                public float _percY;
                public PointF _min;
                public PointF _max;
            }
            private Dictionary<float, clsPercCache> _percCache;
            private Queue<float> _percCacheKey;

            private string _sId;
            private string _sParentId;

            private MeterItemType _ItemType;

            private bool _storeSettings;

            private PointF _topLeft; // 0-1,0-1
            private PointF _displayTopLeft; // 0-1,0-1
            private SizeF _size;  // 0-1,0-1
            private int _zOrder; // lower first
            private int _msUpdateInterval; //ms
            private float _attackRatio; // 0f - 1f
            private float _decayRatio; // 0f - 1f
            private float _value;
            private System.Drawing.Color _historyColour;
            private Reading _readingType;
            private Stopwatch _updateStopwatch;
            private Dictionary<float, PointF> _scaleCalibration;
            private int _displayGroup;
            private bool _bNormaliseTo100W;
            private bool _bOnlyWhenTx;
            private bool _bOnlyWhenRx;
            private bool _bFadeOnRx;
            private bool _bFadeOnTx;
            private bool _bPrimary;
            private float _maxPower;
            private string _readingName;

            private int _fadeValue;
            private bool _disabled;
            private bool _mox;

            //private Guid[] _mmio_guid;
            //private string[] _mmio_variable;
            private Guid _mmio_guid;
            private string _mmio_variable;
            private int _mmio_variable_index;

            private PointF _mouseDownPoint;
            private PointF _mouseUpPoint;
            private PointF _mouseMovePoint;
            private bool _mouse_entered;
            private bool _mouseButtonDown;
            private MouseButtons _mouseButton;

            public clsMeterItem(clsMeter owningMeter = null)
            {
                // constructor
                _sId = System.Guid.NewGuid().ToString();
                _sParentId = "";
                _ItemType = MeterItemType.BASE;
                _storeSettings = false;
                _readingType = Reading.NONE;
                _readingName = "";
                _topLeft.X = 0;
                _topLeft.Y = 0;
                _displayTopLeft.X = _topLeft.X;
                _displayTopLeft.Y = _topLeft.Y;
                _zOrder = 0;
                _msUpdateInterval = 5000; //ms
                _attackRatio = 0.8f;
                _decayRatio = 0.2f;
                _value = -200f;
                _historyColour = System.Drawing.Color.FromArgb(128, 255, 255, 255);
                _scaleCalibration = new Dictionary<float, PointF>();
                _size.Width = 1f;
                _size.Height = 1f;
                _displayGroup = 0;
                _bNormaliseTo100W = false;
                _bOnlyWhenTx = false;
                _bOnlyWhenRx = false;
                _bFadeOnRx = false;
                _bFadeOnTx = false;
                _bPrimary = false;
                _maxPower = CurrentPowerRating;//100f;
                _percCache = new Dictionary<float, clsPercCache>();
                _percCacheKey = new Queue<float>();
                _updateStopwatch = new Stopwatch();
                _fadeValue = 255;
                _disabled = false;
                _mox = false;

                //_mmio_guid = new Guid[10];
                //_mmio_variable = new string[10];

                //for (int i = 0; i < _mmio_guid.Length; i++)
                //{
                //    _mmio_guid[i] = Guid.Empty;
                //    _mmio_variable[i] = "--DEFAULT--";
                //}
                _mmio_guid = Guid.Empty;
                _mmio_variable = "--DEFAULT--";
                _mmio_variable_index = -1;

                _mouseDownPoint = new PointF(0, 0);
                _mouseUpPoint = new PointF(0, 0);
                _mouseMovePoint = new PointF(0, 0);
                _mouse_entered = false;
                _mouseButtonDown = false;
                _mouseButton = MouseButtons.None;
            }
            //public Guid GetMMIOGuid(int index)
            //{
            //    return _mmio_guid[index];
            //}
            //public void SetMMIOGuid(int index, Guid g)
            //{
            //    _mmio_guid[index] = g;
            //}
            //public string GetMMIOVariable(int index)
            //{
            //    return _mmio_variable[index];
            //}
            //public void SetMMIOVariable(int index, string variable)
            //{
            //    _mmio_variable[index] = variable;
            //}
            public Guid MMIOGuid
            {
                get { return _mmio_guid; }
                set { _mmio_guid = value; }
            }
            public string MMIOVariable
            {
                get { return _mmio_variable; }
                set { _mmio_variable = value; }
            }
            public int MMIOVariableIndex
            {
                get { return _mmio_variable_index; }
                set { _mmio_variable_index = value; }
            }
            public bool MOX
            {
                get { return _mox; }
                set { _mox = value; }
            }
            public int FadeValue //[2.10.1.0] MW0LGE used for on rx/tx fading
            {
                get { return _fadeValue; }
                set { _fadeValue = value; }
            }
            public bool Disabled //[2.10.1.0] MW0LGE used when certain features turned off such as eq,leveler,cfc
            {
                get { return _disabled; }
                set { _disabled = value; }
            }
            public void AddPerc(clsPercCache pc)
            {
                if (_percCache.ContainsKey(pc._value)) return;

                _percCache.Add(pc._value, pc);
                _percCacheKey.Enqueue(pc._value);
                if (_percCache.Count > 500)
                {
                    float sOldestKey = _percCacheKey.Dequeue();
                    _percCache.Remove(sOldestKey);
                }
            }
            public clsPercCache GetPerc(float value)
            {
                if (!_percCache.ContainsKey(value)) return null;
                return _percCache[value];
            }
            public bool HasPerc(float value)
            {
                return _percCache.ContainsKey(value);
            }
            public string ID { 
                get { return _sId; } 
                set { _sId = value; }
            }
            public string ParentID
            {
                get { return _sParentId; }
                set { _sParentId = value; }
            }
            public bool Primary
            {
                get { return _bPrimary; }
                set { _bPrimary = value; }
            }
            public bool StoreSettings
            {
                get { return _storeSettings; }
                set { _storeSettings = value; }
            }
            public bool FadeOnRx
            {
                get { return _bFadeOnRx; }
                set { _bFadeOnRx = value; }
            }
            public bool FadeOnTx
            {
                get { return _bFadeOnTx; }
                set { _bFadeOnTx = value; }
            }
            public PointF TopLeft {
                get { return _topLeft; }
                set { 
                    _topLeft = value;
                    _displayTopLeft = new PointF(_topLeft.X, _topLeft.Y);
                }
            }
            public PointF DisplayTopLeft
            {
                get { return _displayTopLeft; }
                set { _displayTopLeft = value; }
            }
            public SizeF Size
            {
                get { return _size; }
                set 
                { _size = value; }
            }
            public int DisplayGroup
            {
                get { return _displayGroup; }
                set { _displayGroup = value; }
            }
            public bool NormaliseTo100W
            {
                get { return _bNormaliseTo100W; }
                set { _bNormaliseTo100W = value; }
            }
            public int UpdateInterval
            {
                get { return _msUpdateInterval; }
                set
                {
                    _msUpdateInterval = value;
                    if (_msUpdateInterval < 1) _msUpdateInterval = 1;
                }
            }
            public float AttackRatio
            {
                get { return _attackRatio; }
                set {
                    _attackRatio = value;
                    if (_attackRatio < 0) _attackRatio = 0;
                    if (_attackRatio > 1f) _attackRatio = 1f;
                }
            }
            public float DecayRatio
            {
                get { return _decayRatio; }
                set
                {
                    _decayRatio = value;
                    if (_decayRatio < 0) _decayRatio = 0;
                    if (_decayRatio > 1f) _decayRatio = 1f;
                }
            }
            public MeterItemType ItemType
            {
                get { return _ItemType; }
                set { _ItemType = value; }
            }
            public Reading ReadingSource
            {
                get { return _readingType; }
                set 
                { 
                    _readingType = value;
                    _readingName = MeterManager.ReadingName(_readingType);
                }
            }
            public string ReadingName
            {
                get { return _readingName; }
            }

            public bool RequiresUpdate
            {
                get
                {
                    //if (!_updateStopwatch.IsRunning) _updateStopwatch.Restart();
                    //bool bNeedsUpdate = _updateStopwatch.ElapsedMilliseconds >= _msUpdateInterval;
                    //if(bNeedsUpdate) _updateStopwatch.Stop();

                    bool bNeedsUpdate = DelayUntilNextUpdate <= 0;

                    return bNeedsUpdate;
                }
            }
            public int DelayUntilNextUpdate
            {
                // returns the delay that is needed before we need an update in ms
                get
                {
                    if (!_updateStopwatch.IsRunning) _updateStopwatch.Restart();
                    long nDelay = _msUpdateInterval - _updateStopwatch.ElapsedMilliseconds;
                    if (nDelay <= 0)
                    {
                        _updateStopwatch.Stop();
                        nDelay = 0;
                    }

                    return (int)nDelay;
                }
            }
            public virtual void Update(int rx, ref List<Reading> readingsUsed, Dictionary<Reading, object> all_list_item_readings = null)
            {
                // can be overriden by derived

                //// get latest reading
                //float reading = MeterManager.GetReading(rx, ReadingSource);

                //Value = reading;

                //// this reading has been used
                //if (!readingsUsed.Contains(ReadingSource))
                //    readingsUsed.Add(ReadingSource);
            }
            public virtual float Value
            {
                get { return _value; }
                set { _value = value; }
            }
            public int ZOrder
            {
                get { return _zOrder; }
                set { _zOrder = value; }
            }
            public virtual void ClearHistory()
            {
            }
            public virtual void History(out float minHistory, out float maxHistory)
            {
                minHistory = 0f;
                maxHistory = 0f;
            }
            public virtual float MinHistory
            {
                get
                {
                    return 0f;
                }
            }
            public virtual float MaxHistory
            {
                get
                {
                    return 0f;
                }
            }
            public virtual bool ShowHistory
            {
                get { return false; }
                set { }
            }
            public virtual System.Drawing.Color HistoryColour
            {
                get { return _historyColour; }
                set { _historyColour = value; }
            }
            public virtual Dictionary<float, PointF> ScaleCalibration
            {
                get { return _scaleCalibration; }
                set { }
            }            
            public bool OnlyWhenTX
            {
                get { return _bOnlyWhenTx; }
                set { _bOnlyWhenTx = value; }
            }
            public bool OnlyWhenRX
            {
                get { return _bOnlyWhenRx; }
                set { _bOnlyWhenRx = value; }
            }
            public override string ToString()
            {
                return base.ToString();
            }
            public virtual bool TryParse(string val)
            {
                return false;
            }            
            public float MaxPower
            {
                get { return _maxPower; }
                set {                    
                    _maxPower = value;
                    if (_maxPower < 0.1f) _maxPower = CurrentPowerRating; // incase of 0 recovery from db
                }
            }
            public virtual void HandleIncrement()
            {
            }
            public virtual void HandleDecrement()
            {
            }
            public virtual bool ZeroOut(out float value, int rx)
            {
                value = 0;
                return false;
            }

            // mouse
            public virtual void MouseClick(MouseEventArgs e)
            {

            }
            public virtual void MouseDown(MouseEventArgs e)
            {

            }
            public virtual void MouseUp(MouseEventArgs e)
            {

            }
            public virtual MouseButtons MouseButton
            {
                get { return _mouseButton; }
                set { _mouseButton = value; }
            }
            public virtual bool MouseButtonDown
            {
                get { return _mouseButtonDown; }
                set { _mouseButtonDown = value; }
            }
            public virtual PointF MouseDownPoint
            {
                get { return _mouseDownPoint; }
                set { _mouseDownPoint = value; }
            }
            public virtual PointF MouseUpPoint
            {
                get { return _mouseUpPoint; }
                set { _mouseUpPoint = value; }
            }
            public virtual PointF MouseMovePoint
            {
                get { return _mouseMovePoint; }
                set { _mouseMovePoint = value; }
            }
            public virtual bool MouseEntered
            {
                get { return _mouse_entered; }
                set { _mouse_entered = value; }
            }
            public virtual void KeyDown(Keys keycode)
            {

            }
            public virtual void KeyUp(Keys keycode)
            {

            }
            public virtual void MouseWheel(int number_of_moves)
            {

            }
            //
            public virtual void Removing()
            {

            }
        }
        //
        internal class clsItemGroup : clsMeterItem
        {
            private MeterType _meterType;
            private int _order; // 0 = topmost

            public clsItemGroup()
            {
                ItemType = MeterItemType.ITEM_GROUP;
                _meterType = MeterType.NONE;
                StoreSettings = true;
                ZOrder = 999;
            }
            public MeterType MeterType
            {
                get { return _meterType; }
                set { _meterType = value; }
            }
            public int Order
            {
                get { return _order; }
                set { _order = value; }
            }
            public override string ToString()
            {
                string sRet;

                sRet = ID + "|" +
                    ParentID + "|" +
                    ItemType.ToString() + "|" +
                    TopLeft.X.ToString("f4") + "|" +
                    TopLeft.Y.ToString("f4") + "|" +
                    Size.Width.ToString("f4") + "|" +
                    Size.Height.ToString("f4") + "|" +
                    MeterType.ToString() + "|" +
                    Order.ToString();
                    //ZOrder.ToString() + "|" +
                    //UpdateInterval.ToString() + "|" +
                    //AttackRatio.ToString("f4") + "|" +
                    //DecayRatio.ToString("f4") + "|" +
                    //HistoryColour.ToString() + "|" +
                    //ReadingSource.ToString();

                return sRet;
            }
            public override bool TryParse(string str)
            {
                bool bOk = false;
                float x = 0, y = 0, w = 0, h = 0;
                int order = 0;
                MeterType mt = MeterType.LAST;

                if (str != "")
                {
                    string[] tmp = str.Split('|');
                    if (tmp.Length == 9)
                    {
                        bOk = tmp[0] != "";
                        if (bOk) ID = tmp[0];
                        if (bOk) ParentID = tmp[1];
                        //if (bOk) bOk = Enum.TryParse<MeterItemType>(tmp[2], out it);  //ignore

                        if (bOk) bOk = float.TryParse(tmp[3], out x);
                        if (bOk) bOk = float.TryParse(tmp[4], out y);
                        if (bOk) bOk = float.TryParse(tmp[5], out w);
                        if (bOk) bOk = float.TryParse(tmp[6], out h);
                        if (bOk) bOk = Enum.TryParse<MeterType>(tmp[7], out mt);
                        if (bOk)
                            MeterType = mt;
                        if (bOk) bOk = int.TryParse(tmp[8], out order);
                        if (bOk) Order = order;
                    }
                }

                return bOk;
            }
        }
        internal class clsClickBox : clsMeterItem
        {
            private MouseButtons _button;
            private int _gotoGroup;
            private bool _changesGroup;
            private bool _performsIncDec;
            private clsMeterItem _relatedMeterItem = null;

            public clsClickBox()
            {
                ItemType = MeterItemType.CLICKBOX;

                _button = MouseButtons.Left | MouseButtons.Right | MouseButtons.Middle;
                _gotoGroup = 0;
                _changesGroup = true;
                _performsIncDec = false;
                StoreSettings = false;
            }
            public MouseButtons Button
            {
                get { return _button; }
                set { _button = value; }
            }
            public int GotoGroup 
            {
                get { return _gotoGroup; }
                set { _gotoGroup = value; }
            }
            public bool ChangesGroup
            {
                get { return _changesGroup; }
                set { _changesGroup = value; }
            }
            public bool PerformsIncDec
            {
                get { return _performsIncDec; }
                set { _performsIncDec = value; }
            }
            public clsMeterItem RelatedMeterItem
            {
                get { return _relatedMeterItem; }
                set { _relatedMeterItem = value; }
            }
            //public override string ToString()
            //{
            //    string sRet;

            //    sRet = ID + "|" +
            //        ParentID + "|" +
            //        ItemType.ToString() + "|" +
            //        TopLeft.X.ToString("f4") + "|" +
            //        TopLeft.Y.ToString("f4") + "|" +
            //        Size.Width.ToString("f4") + "|" +
            //        Size.Height.ToString("f4") + "|" +
            //        Button.ToString() + "|" +
            //        GotoGroup.ToString("f4") + "|";

            //    return sRet;
            //}
        }
        internal class clsSolidColour : clsMeterItem
        {
            private System.Drawing.Color _colour;
            public clsSolidColour()
            {
                ItemType = MeterItemType.SOLID_COLOUR;
                _colour = System.Drawing.Color.Black;
                StoreSettings = false;
            }
            public System.Drawing.Color Colour
            {
                get { return _colour; }
                set
                { 
                    _colour = value;
                }
            }
            //public override string ToString()
            //{
            //    string sRet;

            //    sRet = ID + "|" +
            //        ParentID + "|" +
            //        ItemType.ToString() + "|" +
            //        TopLeft.X.ToString("f4") + "|" +
            //        TopLeft.Y.ToString("f4") + "|" +
            //        Size.Width.ToString("f4") + "|" +
            //        Size.Height.ToString("f4") + "|" +
            //        //
            //        _colour.ToString();

            //    return sRet;
            //}
        }
        internal class clsFadeCover : clsMeterItem
        {
            public clsFadeCover()
            {
                ItemType = MeterItemType.FADE_COVER;
                StoreSettings = false;
                ZOrder = int.MaxValue;
            }
        }
        internal class clsVfoDisplay : clsMeterItem
        {
            public enum renderState
            {
                VFO = 0,
                BAND,
                MODE,
                FILTER,
            }
            public enum buttonState
            {   
                NONE = 0,
                VFO_SCREEN,
                BAND_SCREEN,
                FILTER_SCREEN,
                MODE_SCREEN,
                VFO,
                BAND,
                VHF,
                HF,
                SWL,
                MODE,
                FILTER
            }
            public enum DSPModeForModeDisplay
            {
                LSB = 0,
                USB,
                DSB,
                CWL,
                CWU,
                FM,
                AM,
                SAM,
                DIGL,
                DIGU,
                SPEC,
                DRM
            }
            private renderState _render_state_vfoA;
            private renderState _render_state_vfoB;

            private System.Drawing.Color _colour;
            private string _fontFamily;
            private FontStyle _fontStyle;
            private float _fontSize;

            private bool _showType;
            private System.Drawing.Color _typeColor;

            private System.Drawing.Color _frequencyColour;
            private System.Drawing.Color _modeColour;
            private System.Drawing.Color _splitBackColour;
            private System.Drawing.Color _splitColour;
            private System.Drawing.Color _rxColour;
            private System.Drawing.Color _txColour;
            private System.Drawing.Color _filterColour;
            private System.Drawing.Color _bandColour;
            private System.Drawing.Color _digitHighlightColour;

            private double _adjust_step;
            //private bool _adjust_enabled;
            private bool _mouse_over_vfoB;
            private clsMeter _owningmeter;
            private DateTime _render_state_vfoA_change_time;
            private DateTime _render_state_vfoB_change_time;
            private buttonState _render_button_vfoA;
            private buttonState _render_button_vfoB;
            private int _button_grid_index_vfoA;
            private int _button_grid_index_vfoB;

            public clsVfoDisplay(clsMeter owningmeter)
            {
                _fontFamily = "Trebuchet MS";
                _fontStyle = FontStyle.Regular;
                _fontSize = 18f;
                _showType = true;
                _typeColor = System.Drawing.Color.Gray;
                _frequencyColour = System.Drawing.Color.Orange;
                _modeColour = System.Drawing.Color.Gray;
                _splitBackColour = System.Drawing.Color.FromArgb(64, 64, 64);
                _splitColour = System.Drawing.Color.Orange;
                _rxColour = System.Drawing.Color.LimeGreen;
                _txColour = System.Drawing.Color.Red;
                _filterColour = System.Drawing.Color.Gray;
                _bandColour = System.Drawing.Color.White;
                _digitHighlightColour = System.Drawing.Color.FromArgb(128, 128, 128);
                _adjust_step = 0;
                //_adjust_enabled = false;
                _owningmeter = owningmeter;
                _mouse_over_vfoB = false;

                ItemType = MeterItemType.VFO_DISPLAY;
                _colour = System.Drawing.Color.White;
                StoreSettings = false;
                UpdateInterval = 50; // fixed

                _render_state_vfoA = renderState.VFO;
                _render_state_vfoB = renderState.VFO;
                _render_state_vfoA_change_time = DateTime.Now;
                _render_state_vfoB_change_time = DateTime.Now;
                _render_button_vfoA = buttonState.VFO_SCREEN;
                _render_button_vfoB = buttonState.VFO_SCREEN;
                _button_grid_index_vfoA = -1;
                _button_grid_index_vfoB = -1;
            }
            private double getVfo()
            {
                double ret = -1;
                if (_owningmeter.RX == 1 && !_owningmeter.RX2Enabled)
                {
                    //vfoA
                    //vfoB
                    if (!_mouse_over_vfoB)
                        ret = _owningmeter.VfoA;
                    else
                        ret = _owningmeter.VfoB;
                }
                else if (_owningmeter.RX == 1 && _owningmeter.RX2Enabled)
                {
                    //vfoA
                    //no vfoB   if split / subrx then it becomes subvfo
                    if (!_mouse_over_vfoB)
                        ret = _owningmeter.VfoA;
                    else
                    {
                        if (_owningmeter.MultiRxEnabled || _owningmeter.Split)
                            ret = _owningmeter.VfoSub;
                    }
                }
                else if (_owningmeter.RX == 2 && _owningmeter.RX2Enabled)
                {
                    //no vfoA
                    //vfoB
                    if (_mouse_over_vfoB)
                        ret = _owningmeter.VfoB;
                }
                return ret;
            }
            private void setVfo(double value)
            {
                if (_owningmeter.RX == 1 && !_owningmeter.RX2Enabled)
                {
                    //vfoA
                    //vfoB
                    if (!_mouse_over_vfoB)
                        _console.VFOAFreq = value;
                    else
                        _console.VFOBFreq = value;
                }
                else if (_owningmeter.RX == 1 && _owningmeter.RX2Enabled)
                {
                    //vfoA
                    //no vfoB   if split / subrx then it becomes subvfo
                    if (!_mouse_over_vfoB)
                        _console.VFOAFreq = value;
                    else
                    {
                        if (_owningmeter.MultiRxEnabled || _owningmeter.Split)
                            _console.VFOASubFreq = value;
                    }
                }
                else if (_owningmeter.RX == 2 && _owningmeter.RX2Enabled)
                {
                    //no vfoA
                    //vfoB
                    if (_mouse_over_vfoB)
                        _console.VFOBFreq = value;
                }
            }
            private void adjustVfo(double adjustment)
            {
                if (_owningmeter.RX == 1 && !_owningmeter.RX2Enabled)
                {
                    //vfoA
                    //vfoB
                    if (!_mouse_over_vfoB)
                        _console.VFOAFreq += adjustment;
                    else
                        _console.VFOBFreq += adjustment;
                }
                else if (_owningmeter.RX == 1 && _owningmeter.RX2Enabled)
                {
                    //vfoA
                    //no vfoB   if split / subrx then it becomes subvfo
                    if (!_mouse_over_vfoB)
                        _console.VFOAFreq += adjustment;
                    else
                    {
                        if (_owningmeter.MultiRxEnabled || _owningmeter.Split)
                            _console.VFOASubFreq += adjustment;
                    }
                }
                else if (_owningmeter.RX == 2 && _owningmeter.RX2Enabled)
                {
                    //no vfoA
                    //vfoB
                    if (_mouse_over_vfoB)
                        _console.VFOBFreq += adjustment;
                }
            }
            public override void KeyDown(Keys keycode)
            {
                if (!MouseEntered) return;
                if ((int)_adjust_step == 0) return;

                if ((keycode >= Keys.D0 && keycode <= Keys.D9) ||
                    (keycode >= Keys.NumPad0 && keycode <= Keys.NumPad9))
                {
                    int digit = 0;
                    if (keycode >= Keys.D0 && keycode <= Keys.D9)
                        digit = keycode - Keys.D0;
                    else if (keycode >= Keys.NumPad0 && keycode <= Keys.NumPad9)
                        digit = keycode - Keys.NumPad0;

                    double freq = getVfo();
                    if(freq >= 0)
                    {
                        string freqs = freq.ToString("F6", CultureInfo.InvariantCulture);
                        int pos = freqs.Length - findOnePosition(_adjust_step * 1e-6) - 1;
                        string before = freqs.Substring(0, pos);
                        string after = freqs.Substring(pos + 1);
                        string newFreqs = $"{before}{digit}{after}";
                        bool ok = double.TryParse(newFreqs, out double newFreq);
                        if (ok)
                        {
                            _console.BeginInvoke(new MethodInvoker(() =>
                            {
                                setVfo(newFreq);
                            }));
                        }
                    }
                }
            }
            private int findOnePosition(double number)
            {
                string numberStr = number.ToString("F6", CultureInfo.InvariantCulture);
                for (int i = numberStr.Length - 1; i >= 0; i--)
                {
                    if (numberStr[i] == '1')
                    {
                        return numberStr.Length - 1 - i; // Return the 0-based index from the right
                    }
                }
                return -1; // Return -1 if '1' is not found
            }
            public override void MouseWheel(int number_of_moves)
            {
                if (!MouseEntered) return;
                //if (!_adjust_enabled) return;

                int sign = Math.Sign(number_of_moves);

                _console.BeginInvoke(new MethodInvoker(() =>
                {
                    adjustVfo(sign * _adjust_step * 1e-6);
                }));                
            }
            //public override bool MouseButtonDown
            //{
            //    get
            //    {
            //        return base.MouseButtonDown;
            //    }
            //    set
            //    {
            //        if (!MouseEntered) return;
            //        if (base.MouseButtonDown == value) return; // ignore same state

            //        if (value && _adjust_enabled)
            //        {
            //            if (MouseButton == MouseButtons.Left)
            //            {
            //                _console.BeginInvoke(new MethodInvoker(() =>
            //                {
            //                    adjustVfo(_adjust_step * 1e-6);
            //                }));                            
            //            }
            //            else if (MouseButton == MouseButtons.Right)
            //            {
            //                _console.BeginInvoke(new MethodInvoker(() =>
            //                {
            //                    adjustVfo(-_adjust_step * 1e-6);
            //                }));                            
            //            }
            //        }

            //        base.MouseButtonDown = value;
            //    }
            //}
            public override void MouseUp(MouseEventArgs e)
            {
                if (!MouseEntered) return;
                switch (_mouse_over_vfoB ? _render_button_vfoB : _render_button_vfoA) // in mouse up, as dont get mouseclicks when we mash the button
                {
                    case buttonState.VFO:
                        {
                            if (e.Button == MouseButtons.Left)
                            {
                                _console.BeginInvoke(new MethodInvoker(() =>
                                {
                                    adjustVfo(_adjust_step * 1e-6);
                                }));
                            }
                            else if (e.Button == MouseButtons.Right)
                            {
                                _console.BeginInvoke(new MethodInvoker(() =>
                                {
                                    adjustVfo(-_adjust_step * 1e-6);
                                }));
                            }
                        }
                        break;
                }
            }
            public override void MouseClick(MouseEventArgs e)
            {
                if (!MouseEntered) return;

                switch (_mouse_over_vfoB ? _render_button_vfoB : _render_button_vfoA)
                {
                    case buttonState.BAND_SCREEN:
                        if(_mouse_over_vfoB)
                            VFOBRenderState = renderState.BAND;
                        else
                            VFOARenderState = renderState.BAND;
                        break;
                    case buttonState.VFO_SCREEN:
                        if (_mouse_over_vfoB)
                            VFOBRenderState = renderState.VFO;
                        else
                            VFOARenderState = renderState.VFO;
                        break;
                    case buttonState.FILTER_SCREEN:
                        if (_mouse_over_vfoB)
                            VFOBRenderState = renderState.FILTER;
                        else
                            VFOARenderState = renderState.FILTER;
                        break;
                    case buttonState.MODE_SCREEN:
                        if (_mouse_over_vfoB)
                            VFOBRenderState = renderState.MODE;
                        else
                            VFOARenderState = renderState.MODE;
                        break;
                    //////////
                    case buttonState.BAND:
                        if (_mouse_over_vfoB) 
                        {
                            if (setBand(true))
                                VFOBRenderState = renderState.VFO;
                            else
                                _render_state_vfoB_change_time = DateTime.Now;
                        }
                        else
                        {
                            if (setBand(false))
                                VFOARenderState = renderState.VFO;
                            else
                                _render_state_vfoA_change_time = DateTime.Now;
                        }
                        break;
                    case buttonState.MODE:
                        if (_mouse_over_vfoB)
                        {
                            if (setMode(true))
                                VFOBRenderState = renderState.VFO;
                            else
                                _render_state_vfoB_change_time = DateTime.Now;
                        }
                        else
                        {
                            if (setMode(false))
                                VFOARenderState = renderState.VFO;
                            else
                                _render_state_vfoA_change_time = DateTime.Now;
                        }
                        break;
                    case buttonState.FILTER:
                        if (_mouse_over_vfoB)
                        {
                            if (setFilter(true))
                                VFOBRenderState = renderState.VFO;
                            else
                                _render_state_vfoB_change_time = DateTime.Now;
                        }
                        else
                        {
                            if (setFilter(false))
                                VFOARenderState = renderState.VFO;
                            else
                                _render_state_vfoA_change_time = DateTime.Now;
                        }
                        break;
                }

                //base.MouseClick(e);
            }
            private bool setFilter(bool vfoB)
            {
                if (vfoB)
                {
                    int f = _button_grid_index_vfoB;
                    if (f < 0) return true;
                    Filter fltr = (Filter)((int)Filter.F1 + f);

                    if (_owningmeter.RX2Enabled)
                    {
                        // special for rx2 as only F1-F7 + VAR1 + VAR2, so, 0,1,2,3,4,5,6,7,8
                        if (f > 8) return true;
                        if(f > 6)
                        {
                            f += 3;
                            fltr = (Filter)((int)Filter.F1 + f);
                        }
                        _console.BeginInvoke(new MethodInvoker(() =>
                        {
                            _console.RX2Filter = fltr;
                        }));
                    }
                    else
                    {
                        _console.BeginInvoke(new MethodInvoker(() =>
                        {
                            _console.RX1Filter = fltr;
                        }));
                    }
                }
                else
                {
                    int f = _button_grid_index_vfoA;
                    if (f < 0) return true;
                    Filter fltr = (Filter)((int)Filter.F1 + f);

                    _console.BeginInvoke(new MethodInvoker(() =>
                    {
                        _console.RX1Filter = fltr;
                    }));
                }
                return true;
            }
            private bool setMode(bool vfoB)
            {
                if (vfoB)
                {
                    int m = _button_grid_index_vfoB;
                    if (m < 0) return true;
                    DSPModeForModeDisplay dm = (DSPModeForModeDisplay)((int)DSPModeForModeDisplay.LSB + m);
                    Enum.TryParse<DSPMode>(dm.ToString(), out DSPMode dspMode);

                    if (_owningmeter.RX2Enabled)
                    {
                        _console.BeginInvoke(new MethodInvoker(() =>
                        {
                            _console.RX2DSPMode = dspMode;
                        }));
                    }
                    else
                    {
                        _console.BeginInvoke(new MethodInvoker(() =>
                        {
                            _console.RX1DSPMode = dspMode;
                        }));
                    }
                }
                else
                {
                    int m = _button_grid_index_vfoA;
                    if (m < 0) return true;
                    DSPModeForModeDisplay dm = (DSPModeForModeDisplay)((int)DSPModeForModeDisplay.LSB + m);
                    Enum.TryParse<DSPMode>(dm.ToString(), out DSPMode dspMode);

                    _console.BeginInvoke(new MethodInvoker(() =>
                    {
                        _console.RX1DSPMode = dspMode;
                    }));
                }
                return true;
            }
            private bool setBand(bool vfoB)
            {
                if (vfoB)
                {
                    int b = _button_grid_index_vfoB;
                    if (b < 0) return true;
                    Band band;
                    if (_console.BandGENSelected)
                    {
                        if(b > (int)Band.B11M - (int)Band.B120M)
                        {
                            if (b == 13)
                            {
                                setBandPanel(false, true, false); // HF
                                return false;
                            }
                            return true;
                        }
                        band = (Band)((int)Band.B120M + b);
                    }
                    else if (_console.BandHFSelected)
                    {
                        if (b > (int)Band.B6M - (int)Band.B160M)
                        {
                            if (b == 11)
                            {
                                setBandPanel(false, false, true); // VHF
                                return false;
                            }
                            if (b == 13)
                            {
                                setBandPanel(true, false, false); // GEN(SWL)
                                return false;
                            }
                            if (b == 12) // WWV
                                band = Band.WWV;
                            else
                                return true;
                        }
                        band = (Band)((int)Band.B160M + b);
                    }
                    else if (_console.BandVHFSelected)
                    {
                        if (b > (int)Band.VHF13 - (int)Band.VHF0)
                        {
                            if (b == 14)
                            {
                                setBandPanel(false, true, false); // HF
                                return false;
                            }
                            return true;
                        }
                        band = (Band)((int)Band.VHF0 + b);
                    }
                    else
                        return true;
                    _console.BeginInvoke(new MethodInvoker(() =>
                    {
                        _console.SetupRX2Band(band, !_owningmeter.RX2Enabled); // we tell setuprx2band that we only want to change freq for vfob if only rx1
                    }));
                }
                else
                {
                    int b = _button_grid_index_vfoA;
                    if (b < 0) return true;
                    Band band;
                    if (_console.BandGENSelected)
                    {
                        if (b > (int)Band.B11M - (int)Band.B120M)
                        {
                            if (b == 13)
                            {
                                setBandPanel(false, true, false); // HF
                                return false;
                            }
                            return true;
                        }
                        band = (Band)((int)Band.B120M + b);
                    }
                    else if (_console.BandHFSelected)
                    {
                        if (b > (int)Band.B6M - (int)Band.B160M)
                        {
                            if (b == 11)
                            {
                                setBandPanel(false, false, true); // VHF
                                return false;
                            }
                            if (b == 13)
                            {
                                setBandPanel(true, false, false); // GEN(SWL)
                                return false;
                            }
                            if (b == 12) // WWV
                                band = Band.WWV;
                            else
                                return true;
                        }
                        band = (Band)((int)Band.B160M + b);
                    }
                    else if (_console.BandVHFSelected)
                    {
                        if (b > (int)Band.VHF13 - (int)Band.VHF0)
                        {
                            if (b == 14)
                            {
                                setBandPanel(false, true, false); // HF
                                return false;
                            }
                            return true;
                        }

                        band = (Band)((int)Band.VHF0 + b);
                    }
                    else
                        return true;

                    _console.BeginInvoke(new MethodInvoker(() =>
                    {
                        _console.BandPreChangeHandlers?.Invoke(1, band);
                    }));
                }
                return true;
            }
            private void setBandPanel(bool gen, bool hf, bool vhf)
            {
                if (gen)
                {
                    hf = false;
                    vhf = false;
                }
                else if (hf)
                {
                    gen = false;
                    vhf = false;
                }
                else if (vhf)
                {
                    gen = false;
                    hf = false;
                }
                _console.BeginInvoke(new MethodInvoker(() =>
                {
                    if (gen)
                        _console.BandGENSelected = true;
                    else if (hf)
                        _console.BandHFSelected = true;
                    else if (vhf)
                        _console.BandVHFSelected = true;
                }));
            }
            public int ButtonGridIndexVFOa
            {
                get { return _button_grid_index_vfoA; }
                set { _button_grid_index_vfoA = value; }
            }
            public int ButtonGridIndexVFOb
            {
                get { return _button_grid_index_vfoB; }
                set { _button_grid_index_vfoB = value; }
            }
            public double AdjustStep
            {
                get { return _adjust_step; }
                set { _adjust_step = value; }
            }
            //public bool VfoAdjustEnabled
            //{
            //    get { return _adjust_enabled; }
            //    set { _adjust_enabled = value; }
            //}
            public bool MouseOverVfoB
            {
                get { return _mouse_over_vfoB; }
                set { _mouse_over_vfoB = value; }
            }
            public renderState VFOARenderState
            {
                get 
                {
                    if (_render_state_vfoA != renderState.VFO && (DateTime.Now - _render_state_vfoA_change_time).Seconds > 5) VFOARenderState = renderState.VFO;
                    return _render_state_vfoA; 
                }
                set 
                {
                    if (_render_state_vfoA != value) _render_state_vfoA_change_time = DateTime.Now;
                    _render_state_vfoA = value; 
                }
            }
            public renderState VFOBRenderState
            {
                get
                {
                    if (_render_state_vfoB != renderState.VFO && (DateTime.Now - _render_state_vfoB_change_time).Seconds > 5) VFOBRenderState = renderState.VFO;
                    return _render_state_vfoB; 
                }
                set 
                {
                    if (_render_state_vfoB != value) _render_state_vfoB_change_time = DateTime.Now;
                    _render_state_vfoB = value; 
                }
            }
            public buttonState VFOAButtonState
            {
                get { return _render_button_vfoA; }
                set { _render_button_vfoA = value; }
            }
            public buttonState VFOBButtonState
            {
                get { return _render_button_vfoB; }
                set { _render_button_vfoB = value; }
            }
            public System.Drawing.Color FrequencyColour
            {
                get { return _frequencyColour; }
                set { _frequencyColour = value; }
            }
            public System.Drawing.Color ModeColour
            {
                get { return _modeColour; }
                set { _modeColour = value; }
            }
            public System.Drawing.Color SplitBackColour
            {
                get { return _splitBackColour; }
                set { _splitBackColour = value; }
            }
            public System.Drawing.Color SplitColour
            {
                get { return _splitColour; }
                set { _splitColour = value; }
            }
            public System.Drawing.Color RxColour
            {
                get { return _rxColour; }
                set { _rxColour = value; }
            }
            public System.Drawing.Color TxColour
            {
                get { return _txColour; }
                set { _txColour = value; }
            }
            public System.Drawing.Color FilterColour
            {
                get { return _filterColour; }
                set { _filterColour = value; }
            }
            public System.Drawing.Color BandColour
            {
                get { return _bandColour; }
                set { _bandColour = value; }
            }
            public System.Drawing.Color DigitHighlightColour
            {
                get { return _digitHighlightColour; }
                set { _digitHighlightColour = value; }
            }
            public bool ShowType
            {
                get { return _showType; }
                set { _showType = value; }
            }
            public System.Drawing.Color TypeColour
            {
                get { return _typeColor; }
                set { _typeColor = value; }
            }
            public string FontFamily
            {
                get { return _fontFamily; }
                set { _fontFamily = value; }
            }
            public FontStyle Style
            {
                get { return _fontStyle; }
                set { _fontStyle = value; }
            }
            public System.Drawing.Color Colour
            {
                get { return _colour; }
                set { _colour = value; }
            }
            public float FontSize
            {
                get { return _fontSize; }
                set { _fontSize = value; }
            }

            //public override string ToString()
            //{
            //    string sRet;

            //    sRet = ID + "|" +
            //        ParentID + "|" +
            //        ItemType.ToString() + "|" +
            //        TopLeft.X.ToString("f4") + "|" +
            //        TopLeft.Y.ToString("f4") + "|" +
            //        Size.Width.ToString("f4") + "|" +
            //        Size.Height.ToString("f4") + "|" +
            //        //
            //        _colour.ToString();

            //    return sRet;
            //}
        }
        internal class clsClock : clsMeterItem
        {
            private System.Drawing.Color _colour;
            private string _fontFamily;
            private FontStyle _fontStyle;
            private float _fontSize;
            private System.Drawing.Color _timeColour;
            private System.Drawing.Color _dateColour;
            private System.Drawing.Color _typeTitleColour;
            private bool _showType;
            private bool _24HourClock;

            public clsClock()
            {
                _fontFamily = "Trebuchet MS";
                _fontStyle = FontStyle.Regular;
                _fontSize = 18f;
                _showType = true;
                _timeColour = System.Drawing.Color.Orange;
                _dateColour = System.Drawing.Color.Gray;
                _typeTitleColour = System.Drawing.Color.Gray;
                _24HourClock = true;

                ItemType = MeterItemType.CLOCK;
                _colour = System.Drawing.Color.White;
                StoreSettings = false;
                UpdateInterval = 250; // fixed
            }
            public bool Show24HourCLock
            {
                get { return _24HourClock; }
                set { _24HourClock = value; }
            }
            public bool ShowType
            {
                get { return _showType; }
                set { _showType = value; }
            }
            public System.Drawing.Color TimeColour
            {
                get { return _timeColour; }
                set { _timeColour = value; }
            }
            public System.Drawing.Color DateColour
            {
                get { return _dateColour; }
                set { _dateColour = value; }
            }
            public System.Drawing.Color TypeTitleColour
            {
                get { return _typeTitleColour; }
                set { _typeTitleColour = value; }
            }
            public string FontFamily
            {
                get { return _fontFamily; }
                set { _fontFamily = value; }
            }
            public FontStyle Style
            {
                get { return _fontStyle; }
                set { _fontStyle = value; }
            }
            public System.Drawing.Color Colour
            {
                get { return _colour; }
                set { _colour = value; }
            }
            public float FontSize
            {
                get { return _fontSize; }
                set { _fontSize = value; }
            }
            //public override string ToString()
            //{
            //    string sRet;

            //    sRet = ID + "|" +
            //        ParentID + "|" +
            //        ItemType.ToString() + "|" +
            //        TopLeft.X.ToString("f4") + "|" +
            //        TopLeft.Y.ToString("f4") + "|" +
            //        Size.Width.ToString("f4") + "|" +
            //        Size.Height.ToString("f4") + "|" +
            //        //
            //        _colour.ToString();

            //    return sRet;
            //}
        }
        internal class clsWebImage : clsMeterItem
        {
            private PointF _clipTopLeft;
            private SizeF _clipSize;
            private bool _clipped;
            private bool _darkMode;
            private Guid _image_fetcher_guid;
            private Guid _bitmap_guid;
            private string _url;
            private int _secs_interval;
            private System.Drawing.Bitmap _bitmap;
            private clsMeter _owningMeter;
            private readonly object _bitmap_lock = new object();
            private clsItemGroup _ig;
            private float _width_scale;
            private float _size;
            private ImageFetcher.State _state;

            public clsWebImage(clsMeter owningMeter, clsItemGroup ig)
            {
                _owningMeter = owningMeter;

                ItemType = MeterItemType.WEB_IMAGE;

                _clipTopLeft = new PointF(0f, 0f);
                _clipSize = new SizeF(1f, 1f);
                _clipped = false;
                _darkMode = false;
                StoreSettings = false;
                _image_fetcher_guid = Guid.Empty;
                _bitmap_guid = Guid.Empty;
                _bitmap = null;
                _url = "";
                _secs_interval = 120;
                _ig = ig;
                _width_scale = 1f;
                _size = 0.1f;
                UpdateInterval = 100;
                _state = ImageFetcher.State.IDLE;
            }
            public override void Removing()
            {
                if(_image_fetcher_guid != Guid.Empty)
                {
                    MeterManager.ImgFetch.ImagesObtained -= OnImage;
                    MeterManager.ImgFetch.StateChanged -= OnState;
                    MeterManager.ImgFetch.StopFetching(_image_fetcher_guid);
                }

                string key = "";
                lock (_bitmap_lock)
                {
                    if (_bitmap != null)
                    {                        
                        _bitmap.Dispose();
                        key = "webimg_" + _image_fetcher_guid.ToString();
                    }

                    if(!string.IsNullOrEmpty(key)) MeterManager.RemoveStreamData(key);
                }
            }

            public System.Drawing.Bitmap Bitmap
            {
                get 
                {
                    lock (_bitmap_lock)
                    {
                        return _bitmap;
                    }
                }
            }
            public int SecondsInterval
            {
                get { return _secs_interval; }
                set 
                { 
                    _secs_interval = value; 

                    // change the related image fetcher if one exists
                    if(_image_fetcher_guid != Guid.Empty)
                    {
                        MeterManager.ImgFetch.UpdateInterval(_image_fetcher_guid, _secs_interval);
                    }
                }
            }
            public Guid BitmapGuid
            {
                get                 
                {
                    lock (_bitmap_lock)
                    {
                        return _bitmap_guid;
                    }
                }
            }
            public Guid FetcherGuid
            {
                get
                {
                    lock (_bitmap_lock)
                    {
                        return _image_fetcher_guid;
                    }
                }
            }
            public string URL
            {
                get { return _url; }
                set
                {
                    if (value == _url) return;
                    if(value != _url && _image_fetcher_guid != Guid.Empty)
                    {
                        MeterManager.ImgFetch.ImagesObtained -= OnImage;
                        MeterManager.ImgFetch.StateChanged -= OnState;
                        MeterManager.ImgFetch.StopFetching(_image_fetcher_guid);
                        _image_fetcher_guid = Guid.Empty;
                    }
                    //
                    bool isFile = false;
                    string sPath = "";
                    if (!Common.IsValidUri(value))
                    {
                        // check if it is a file
                        if(value.ToLower().IndexOf("file://") >= 0)
                        {
                            try
                            {
                                Uri file = new Uri(value);
                                // does this path exist?
                                if (!File.Exists(file.LocalPath)) return;
                                isFile = true;
                                sPath = file.LocalPath;
                            }
                            catch 
                            {
                                return;
                            }
                        }
                        else
                            return;
                    }
                    //
                    _url = value;
                    string url = isFile ? sPath : _url;
                    try
                    {
                        MeterManager.ImgFetch.StateChanged += OnState;
                        MeterManager.ImgFetch.ImagesObtained += OnImage;
                        _image_fetcher_guid = MeterManager.ImgFetch.RegisterURL(url, _secs_interval, 1, isFile);
                    }
                    catch
                    {
                        MeterManager.ImgFetch.ImagesObtained -= OnImage;
                        MeterManager.ImgFetch.StateChanged -= OnState;
                        _image_fetcher_guid = Guid.Empty;
                    }
                }
            }
            private void OnState(object sender, ImageFetcher.StateEventArgs e)
            {
                if (e.Guid != _image_fetcher_guid) return;

                _state = e.WebImageState;
                //Debug.Print(_state.ToString());
                if (!_console.IsSetupFormNull)
                {
                    _console.SetupForm.SetWebImageState(ParentID, _state);
                }
            }
            public ImageFetcher.State WebImageState
            {
                get { return _state; }
            }
            private void OnImage(object sender, Guid guid)
            {
                if (guid != _image_fetcher_guid) return;

                lock (_bitmap_lock)
                {
                    List<System.Drawing.Image> imgs = MeterManager.ImgFetch.LatestImages(guid);
                    if (imgs.Count == 1)
                    {
                        Debug.Print(">>>>>>> ONE IMAGE <<<<<<<<");

                        if (_bitmap != null)
                            _bitmap.Dispose();

                        try
                        {
                            _bitmap_guid = Guid.NewGuid();
                            _bitmap = new System.Drawing.Bitmap(imgs[0]);
                        }
                        catch
                        {
                            _bitmap_guid = Guid.Empty;
                            _bitmap = null;
                        }

                        if (_bitmap != null)
                        {
                            _size = (_bitmap.Height / (float)_bitmap.Width) * _width_scale;
                        }
                    }
                }

                if (_size >= 0 && this.Size.Height != _size)
                {
                    this.Size = new SizeF(/*1f*/ _width_scale, _size);

                    if (_ig != null)
                    {
                        float fPadY = 0.05f;
                        float fHeight = 0.05f;
                        _ig.Size = new SizeF(_ig.Size.Width, _size + (fPadY - (fHeight * 0.75f)));
                        _owningMeter.Rebuild();
                    }
                }
            }
            public float ScaleSize
            {
                get { return _size; }
            }
            public float WidthScale
            {
                get { return _width_scale; }
                set 
                { 
                    _width_scale = value;
                    lock (_bitmap_lock)
                    {
                        if (_bitmap != null)
                        {
                            _size = (_bitmap.Height / (float)_bitmap.Width) * _width_scale;
                        }
                    }
                }
            }
            public PointF ClipTopLeft
            {
                get { return _clipTopLeft; }
                set { _clipTopLeft = value; }
            }
            public SizeF ClipSize
            {
                get { return _clipSize; }
                set { _clipSize = value; }
            }
            public bool Clipped
            {
                get { return _clipped; }
                set { _clipped = value; }
            }
            public bool DarkMode
            {
                get { return _darkMode; }
                set { _darkMode = value; }
            }
        }
        internal class clsImage : clsMeterItem
        {
            private string _name;
            private PointF _clipTopLeft;
            private SizeF _clipSize;
            private bool _clipped;
            private bool _darkMode;
            public clsImage()
            {
                ItemType = MeterItemType.IMAGE;

                _name = "";
                _clipTopLeft = new PointF(0f, 0f);
                _clipSize = new SizeF(1f, 1f);
                _clipped = false;
                _darkMode = false;
                StoreSettings = false;
            }
            public string ImageName
            {
                get { return _name; }
                set
                {
                    _name = value;
                }
            }
            public PointF ClipTopLeft
            {
                get { return _clipTopLeft; }
                set { _clipTopLeft = value; }
            }
            public SizeF ClipSize
            {
                get { return _clipSize; }
                set { _clipSize = value; }
            }
            public bool Clipped
            {
                get { return _clipped; }
                set { _clipped = value; }
            }
            public bool DarkMode
            {
                get { return _darkMode; }
                set { _darkMode = value; }
            }
            //public override string ToString()
            //{
            //    string sRet;

            //    sRet = ID + "|" +
            //        ParentID + "|" +
            //        ItemType.ToString() + "|" +
            //        TopLeft.X.ToString("f4") + "|" +
            //        TopLeft.Y.ToString("f4") + "|" +
            //        Size.Width.ToString("f4") + "|" +
            //        Size.Height.ToString("f4") + "|";

            //    return sRet;
            //}
        }
        internal class clsScaleItem : clsMeterItem
        {
            private System.Drawing.Color _lowColour;
            private System.Drawing.Color _highColour;
            private string _fontFamily;
            private FontStyle _fontStyle;
            private System.Drawing.Color _fontColorLow;
            private System.Drawing.Color _fontColorHigh;
            private System.Drawing.Color _fontColourMeterType;
            private float _fontSize;
            private bool _showType;
            private bool _showMarkers;

            public clsScaleItem()
            {
                _lowColour = System.Drawing.Color.White;
                _highColour = System.Drawing.Color.Red;
                _fontColourMeterType = System.Drawing.Color.DarkGray;

                _showType = false;
                _showMarkers = true;

                _fontFamily = "Trebuchet MS";
                _fontStyle = FontStyle.Regular;
                _fontColorLow = System.Drawing.Color.White;
                _fontColorHigh = System.Drawing.Color.Red;
                _fontColourMeterType = System.Drawing.Color.DarkGray;
                _fontSize = 20f;               

                ItemType = MeterItemType.H_SCALE;
                ReadingSource = Reading.NONE;
                UpdateInterval = 100;
                AttackRatio = 0.8f;
                DecayRatio = 0.2f;
                StoreSettings = false;
            }           
            public System.Drawing.Color LowColour
            {
                get { return _lowColour; }
                set { _lowColour = value; }
            }
            public System.Drawing.Color HighColour
            {
                get { return _highColour; }
                set { _highColour = value; }
            }
            public System.Drawing.Color FontColourType
            {
                get { return _fontColourMeterType; }
                set { _fontColourMeterType = value; }
            }
            public string FontFamily
            {
                get { return _fontFamily; }
                set { _fontFamily = value; }
            }
            public FontStyle FntStyle
            {
                get { return _fontStyle; }
                set { _fontStyle = value; }
            }
            public System.Drawing.Color FontColourLow
            {
                get { return _fontColorLow; }
                set { _fontColorLow = value; }
            }
            public System.Drawing.Color FontColourHigh
            {
                get { return _fontColorHigh; }
                set { _fontColorHigh = value; }
            }
            public float FontSize
            {
                get { return _fontSize; }
                set { _fontSize = value; }
            }
            public bool ShowType
            {
                get { return _showType; }
                set { _showType = value; }
            }  
            public bool ShowMarkers
            {
                get { return _showMarkers; }
                set { _showMarkers = value; }
            }
            public override string ToString()
            {
                string sRet;

                sRet = ID + "|" +
                    ParentID + "|" +
                    ItemType.ToString() + "|" +
                    TopLeft.X.ToString("f4") + "|" +
                    TopLeft.Y.ToString("f4") + "|" +
                    Size.Width.ToString("f4") + "|" +
                    Size.Height.ToString("f4");
                    //


                return sRet;
            }            
        }
        internal class clsNeedleScalePwrItem : clsMeterItem
        {
            private System.Drawing.Color _lowColour;
            private System.Drawing.Color _highColour;
            private string _fontFamily;
            private FontStyle _fontStyle;
            private System.Drawing.Color _fontColorLow;
            private System.Drawing.Color _fontColorHigh;
            private System.Drawing.Color _fontColourType;
            private float _fontSize;
            private bool _showType;
            private bool _showMarkers;
            private int _marks;
            private bool _darkMode;

            private clsNeedleItem.NeedleDirection _needleDirection;
            private float _lengthFactor;
            private PointF _radiusRatio;
            private clsNeedleItem.NeedlePlacement _placement;
            private PointF _needleOffset;

            public clsNeedleScalePwrItem()
            {
                _lowColour = System.Drawing.Color.White;
                _highColour = System.Drawing.Color.Red;
                _fontColourType = System.Drawing.Color.DarkGray;

                _showType = false;
                _showMarkers = true;

                _fontFamily = "Trebuchet MS";
                _fontStyle = FontStyle.Regular;
                _fontColorLow = System.Drawing.Color.White;
                _fontColorHigh = System.Drawing.Color.Red;
                _fontColourType = System.Drawing.Color.DarkGray;
                _fontSize = 20f;

                ItemType = MeterItemType.NEEDLE_SCALE_PWR;
                ReadingSource = Reading.NONE;
                UpdateInterval = 100;
                AttackRatio = 0.8f;
                DecayRatio = 0.2f;
                StoreSettings = false;

                _radiusRatio.X = 1f;
                _radiusRatio.Y = 1f;
                _lengthFactor = 1f;
                _needleDirection = clsNeedleItem.NeedleDirection.Clockwise;
                _placement = clsNeedleItem.NeedlePlacement.Bottom;
                _needleOffset.X = 0f;
                _needleOffset.Y = 0.5f;
                _darkMode = false;
            }            
            public System.Drawing.Color LowColour
            {
                get { return _lowColour; }
                set { _lowColour = value; }
            }
            public System.Drawing.Color HighColour
            {
                get { return _highColour; }
                set { _highColour = value; }
            }
            public System.Drawing.Color FontColourType
            {
                get { return _fontColourType; }
                set { _fontColourType = value; }
            }
            public string FontFamily
            {
                get { return _fontFamily; }
                set { _fontFamily = value; }
            }
            public FontStyle FntStyle
            {
                get { return _fontStyle; }
                set { _fontStyle = value; }
            }
            public System.Drawing.Color FontColourLow
            {
                get { return _fontColorLow; }
                set { _fontColorLow = value; }
            }
            public System.Drawing.Color FontColourHigh
            {
                get { return _fontColorHigh; }
                set { _fontColorHigh = value; }
            }
            public float FontSize
            {
                get { return _fontSize; }
                set { _fontSize = value; }
            }
            public bool ShowType
            {
                get { return _showType; }
                set { _showType = value; }
            }
            public bool ShowMarkers
            {
                get { return _showMarkers; }
                set { _showMarkers = value; }
            }
            public override string ToString()
            {
                string sRet;

                sRet = ID + "|" +
                    ParentID + "|" +
                    ItemType.ToString() + "|" +
                    TopLeft.X.ToString("f4") + "|" +
                    TopLeft.Y.ToString("f4") + "|" +
                    Size.Width.ToString("f4") + "|" +
                    Size.Height.ToString("f4");
                //

                return sRet;
            }
            public clsNeedleItem.NeedlePlacement Placement
            {
                get { return _placement; }
                set { _placement = value; }
            }
            public clsNeedleItem.NeedleDirection Direction
            {
                get { return _needleDirection; }
                set { _needleDirection = value; }
            }
            public PointF NeedleOffset
            {
                get { return _needleOffset; }
                set { _needleOffset = value; }
            }
            public PointF RadiusRatio
            {
                get { return _radiusRatio; }
                set { _radiusRatio = value; }
            }
            public float LengthFactor
            {
                get { return _lengthFactor; }
                set { _lengthFactor = value; }
            }
            public int Marks
            {
                get { return _marks; }
                set { _marks = value; }
            }
            public bool DarkMode
            {
                get { return _darkMode; }
                set { _darkMode = value; }
            }
        }
        internal class clsRotatorItem : clsMeterItem
        {
            public enum RotatorMode
            {
                AZ = 0,
                ELE = 1,
                BOTH = 2
            }
            private string _fontFamily;
            private FontStyle _fontStyle;
            private float _fontSize;
            private bool _showValue;
            private bool _darkMode;
            private RotatorMode _rotator_mode;

            private Guid _data_out_mmio_guid;

            //private PointF _mouseDownPoint;
            //private PointF _mouseUpPoint;
            //private PointF _mouseMovePoint;
            //private bool _mouseDown;

            private bool _alow_control;
            private string _control_string_AZ;
            private string _control_string_ELE;

            private readonly object _historyLock = new object();

            private System.Drawing.Color _big_blob_colour;
            private System.Drawing.Color _small_blob_colour;
            private System.Drawing.Color _outer_text_colour;
            private System.Drawing.Color _arrow_colour;
            private System.Drawing.Color _beam_width_colour;
            private System.Drawing.Color _background_colour;
            private System.Drawing.Color _control_colour;

            //private bool _show_elevation;
            private bool _show_cardinals;
            private bool _show_beam_width;
            private float _beam_width;
            private float _padding;

            public clsRotatorItem()
            {
                _fontFamily = "Trebuchet MS";
                _fontStyle = FontStyle.Regular;
                _fontSize = 18f;
                _showValue = true;
                //_show_elevation = false;
                _show_cardinals = false;
                _show_beam_width = false;
                _beam_width = 30f;
                _darkMode = false;
                _rotator_mode = RotatorMode.BOTH;
                _padding = 0.5f;

                _data_out_mmio_guid = Guid.Empty;

                //_mouseDownPoint = new PointF(0, 0);
                //_mouseUpPoint = new PointF(0, 0);
                //_mouseMovePoint = new PointF(0, 0);
                //_mouseDown = false;

                _big_blob_colour = System.Drawing.Color.Red;
                _small_blob_colour = System.Drawing.Color.White;
                _outer_text_colour = System.Drawing.Color.FromArgb(255, 128, 128, 128);
                _arrow_colour = System.Drawing.Color.White;
                _beam_width_colour = System.Drawing.Color.FromArgb(255, 64, 64, 64);
                _background_colour = System.Drawing.Color.FromArgb(32, 32, 32);
                _control_colour = System.Drawing.Color.LimeGreen;

                _alow_control = false;
                _control_string_AZ = "<PST><AZIMUTH>%AZ%</AZIMUTH></PST>";
                _control_string_ELE = "<PST><ELEVATION>%ELE%</ELEVATION></PST>";

                ItemType = MeterItemType.ROTATOR;
                ReadingSource = Reading.AZ;
                UpdateInterval = 1000;
                StoreSettings = false;
            }
            public RotatorMode ViewMode
            {
                get { return _rotator_mode; }
                set { _rotator_mode = value; }
            }
            public Guid DataOutMMIOGuid
            {
                get { return _data_out_mmio_guid; }
                set { _data_out_mmio_guid = value; }
            }
            public bool AllowControl
            {
                get { return _alow_control; }
                set { _alow_control = value; }
            }
            public System.Drawing.Color ControlColour
            {
                get { return _control_colour; }
                set { _control_colour = value; }
            }
            public string AZControlString
            {
                get { return _control_string_AZ; }
                set { _control_string_AZ = value; }
            }
            public string ELEControlString
            {
                get { return _control_string_ELE; }
                set { _control_string_ELE = value; }
            }
            public void SendRotatorMessage(bool dragging_rotator_ele, float dragging_rotator_degrees)
            {
                if (DataOutMMIOGuid == Guid.Empty) return;
                if (!_alow_control) return;

                if (MultiMeterIO.Data.ContainsKey(DataOutMMIOGuid))
                {
                    MultiMeterIO.clsMMIO mmio = MultiMeterIO.Data[DataOutMMIOGuid];
                    if (mmio == null) return;

                    string data;
                    if (dragging_rotator_ele)
                    {
                        data = _control_string_ELE.Replace("%ELE%", ((int)dragging_rotator_degrees).ToString("00"));                        
                    }
                    else
                    {
                        data = _control_string_AZ.Replace("%AZ%", ((int)dragging_rotator_degrees).ToString("000"));
                    }
                    data = data.Replace(@"\n", "\n"); // use @ so that it is a literal verbatim string
                    data = data.Replace(@"\r", "\r");
                    data = data.Replace(@"\0", "\0");
                    mmio.EnqueueOutbound(data);
                }
            }
            //public bool MouseDown
            //{
            //    get { return _mouseDown; }
            //    set { _mouseDown = value; }
            //}
            //public PointF MouseDownPoint
            //{
            //    get { return _mouseDownPoint; }
            //    set { _mouseDownPoint = value; }
            //}
            //public PointF MouseUpPoint
            //{
            //    get { return _mouseUpPoint; }
            //    set { _mouseUpPoint = value; }
            //}
            //public PointF MouseMovePoint
            //{
            //    get { return _mouseMovePoint; }
            //    set { _mouseMovePoint = value; }
            //}
            public string ImageName
            {
                get
                {
                    switch(_rotator_mode)
                    {
                        case RotatorMode.AZ:
                            return "rotator_az-bg";
                        case RotatorMode.ELE:
                            return "rotator_ele-bg";
                        case RotatorMode.BOTH:
                            return "rotator_both-bg";
                    }
                    return "";
                }
            }
            public float Padding
            {
                get { return _padding; }
                set {
                    if (value < 0.001f) value = 0.001f;
                    _padding = value; 
                }
            }
            public bool DarkMode
            {
                get { return _darkMode; }
                set { _darkMode = value; }
            }
            public float BeamWidth
            {
                get { return _beam_width; }
                set { _beam_width = value; }
            }
            //public bool ShowElevation
            //{
            //    get { return _show_elevation; }
            //    set { _show_elevation = value; }
            //}
            public override void Update(int rx, ref List<Reading> readingsUsed, Dictionary<Reading, object> all_list_item_readings = null)
            {
                // get latest reading
                float reading;
                bool use;
                //if (GetMMIOGuid(0) == Guid.Empty)// && GetMMIOVariable(0) == "-- DEFAULT --") 
                if (MMIOGuid == Guid.Empty)
                {
                    reading = MeterManager.getReading(rx, ReadingSource);
                    use = true;
                }
                else
                {
                    reading = 0;
                    if (MultiMeterIO.Data.ContainsKey(MMIOGuid))
                    {
                        MultiMeterIO.clsMMIO mmio = MultiMeterIO.Data[MMIOGuid];
                        if (mmio == null) return;

                        object val = mmio.GetVariable(MMIOVariable);
                        if (val is int)
                        {
                            int intVal = (int)val;
                            reading = (float)intVal;
                        }
                        else if (val is float)
                        {
                            reading = (float)val;
                        }
                        else if (val is double)
                        {
                            double doubleVal = (double)val;
                            reading = (float)doubleVal;
                        }
                    }
                    use = false;
                }

                lock (_historyLock)
                {
                    if (ReadingSource == Reading.ELE)
                    {
                        if (reading < 0) reading = 0;
                        if (reading > 90) reading = 90;
                    }

                    float normalizedValue = Value % 360;
                    if (normalizedValue < 0) normalizedValue += 360;

                    float normalizedReading = reading % 360;
                    if (normalizedReading < 0) normalizedReading += 360;

                    float difference = normalizedReading - normalizedValue;
                    if (difference > 180)
                        difference -= 360;
                    else if (difference < -180)
                        difference += 360;


                    float adjustmentSpeed = 0.2f * Math.Abs(difference);

                    if (Math.Abs(difference) < adjustmentSpeed)
                        Value = reading;
                    else
                        Value += Math.Sign(difference) * adjustmentSpeed;

                    Value = Value % 360;
                    if (Value < 0) Value += 360;
                }

                if (use)
                {
                    // this reading has been used
                    if (!readingsUsed.Contains(ReadingSource))
                        readingsUsed.Add(ReadingSource);
                }
            }
            public bool ShowValue
            {
                get { return _showValue; }
                set { _showValue = value; }
            }
            public bool ShowCardinals
            {
                get { return _show_cardinals; }
                set { _show_cardinals = value; }
            }
            public bool ShowBeamWidth
            {
                get { return _show_beam_width; }
                set { _show_beam_width = value; }
            }
            public string FontFamily
            {
                get { return _fontFamily; }
                set { _fontFamily = value; }
            }
            public FontStyle Style
            {
                get { return _fontStyle; }
                set { _fontStyle = value; }
            }
            public System.Drawing.Color BigBlobColour
            {
                get { return _big_blob_colour; }
                set { _big_blob_colour = value; }
            }
            public System.Drawing.Color SmallBlobColour
            {
                get { return _small_blob_colour; }
                set { _small_blob_colour = value; }
            }
            public System.Drawing.Color OuterTextColour
            {
                get { return _outer_text_colour; }
                set { _outer_text_colour = value; }
            }
            public System.Drawing.Color ArrowColour
            {
                get { return _arrow_colour; }
                set { _arrow_colour = value; }
            }
            public System.Drawing.Color BeamWidthColour
            {
                get { return _beam_width_colour; }
                set { _beam_width_colour = value; }
            }
            public float FontSize
            {
                get { return _fontSize; }
                set { _fontSize = value; }
            }
            public override bool ZeroOut(out float value, int rx)
            {
                value = 0;
                return true;
            }
        }
        internal class clsMagicEyeItem : clsMeterItem
        {
            private List<float> _history;
            private int _msHistoryDuration; //ms
            private int _msIgnoreHistoryDuration;
            private bool _showHistory;
            private bool _showValue;
            private readonly object _historyLock = new object();
            private System.Drawing.Color _colour;
            private int _nIgnoringNext;

            private Dictionary<float, PointF> _scaleCalibration;

            public clsMagicEyeItem()
            {
                _history = new List<float>();
                _msHistoryDuration = 500;
                _msIgnoreHistoryDuration = 2000;
                _showHistory = false;
                _showValue = true;

                _colour = System.Drawing.Color.Lime;
                _nIgnoringNext = 0;

                _scaleCalibration = new Dictionary<float, PointF>();

                ItemType = MeterItemType.MAGIC_EYE;
                ReadingSource = Reading.NONE;
                UpdateInterval = 100;
                AttackRatio = 0.8f;
                DecayRatio = 0.2f;
                StoreSettings = false;
            }           
            public override void Update(int rx, ref List<Reading> readingsUsed, Dictionary<Reading, object> all_list_item_readings = null)
            {
                // get latest reading
                //float reading = MeterManager.getReading(rx, ReadingSource);
                bool use;
                float reading;
                if (MMIOGuid == Guid.Empty)
                {
                    reading = MeterManager.getReading(rx, ReadingSource);
                    use = true;
                }
                else
                {
                    reading = 0;
                    //if (MultiMeterIO.Data.ContainsKey(GetMMIOGuid(0)))
                    if (MultiMeterIO.Data.ContainsKey(MMIOGuid))
                    {
                        //MultiMeterIO.clsMMIO mmio = MultiMeterIO.Data[GetMMIOGuid(0)];
                        MultiMeterIO.clsMMIO mmio = MultiMeterIO.Data[MMIOGuid];

                        //object val = mmio.GetVariable(GetMMIOVariable(0));
                        object val = mmio.GetVariable(MMIOVariable);
                        if (val is int)
                        {
                            int intVal = (int)val;
                            reading = (float)intVal;
                        }
                        else if (val is float)
                        {
                            reading = (float)val;
                        }
                        else if (val is double)
                        {
                            double doubleVal = (double)val;
                            reading = (float)doubleVal;
                        }
                    }
                    use = false;
                }

                lock (_historyLock)
                {
                    if (reading > Value)
                        Value = (reading * AttackRatio) + (Value * (1f - AttackRatio));
                    else
                        Value = (reading * DecayRatio) + (Value * (1f - DecayRatio));

                    if (_nIgnoringNext <= 0)
                    {
                        // signal history
                        _history.Add(Value); // adds to end of the list
                        int numberToRemove = _history.Count - (_msHistoryDuration / UpdateInterval);
                        // the list is sized based on delay
                        if (numberToRemove > 0) _history.RemoveRange(0, numberToRemove); // remove the oldest, the head of the list
                    }
                    else
                    {
                        _nIgnoringNext--;
                    }
                }

                if (use)
                {
                    // this reading has been used
                    if (!readingsUsed.Contains(ReadingSource))
                        readingsUsed.Add(ReadingSource);
                }
            }
            public override bool ShowHistory
            {
                get { return _showHistory; }
                set { _showHistory = value; }
            }
            public bool ShowValue
            {
                get { return _showValue; }
                set { _showValue = value; }
            }
            public int IgnoreHistoryDuration
            {
                get { return _msIgnoreHistoryDuration; }
                set { _msIgnoreHistoryDuration = value; }
            }
            public int HistoryDuration
            {
                get { return _msHistoryDuration; }
                set
                {
                    _msHistoryDuration = value;
                    if (_msHistoryDuration < UpdateInterval) _msHistoryDuration = UpdateInterval;
                }
            }
            public override float MinHistory
            {
                get
                {
                    lock (_historyLock)
                    {
                        if (_history.Count == 0) return Value;
                        return _history.Min();
                    }
                }
            }
            public override float MaxHistory
            {
                get
                {
                    lock (_historyLock)
                    {
                        if (_history.Count == 0) return Value;
                        return _history.Max();
                    }
                }
            }
            public override void ClearHistory()
            {
                lock (_historyLock)
                {
                    _history.Clear();

                    _nIgnoringNext = _msIgnoreHistoryDuration / UpdateInterval;  // ignore next N readings to make up 2 second of ignore
                }
            }
            public override void History(out float minHistory, out float maxHistory)
            {
                if (_history.Count == 0)
                {
                    minHistory = Value;
                    maxHistory = Value;
                }
                else
                {
                    minHistory = _history.Min();
                    maxHistory = _history.Max();
                }
            }
            public System.Drawing.Color Colour
            {
                get { return _colour; }
                set { _colour = value; }
            }
            public override Dictionary<float, PointF> ScaleCalibration
            {
                get { return _scaleCalibration; }
                set { }
            }
            public override bool ZeroOut(out float value, int rx)
            {
                if (_scaleCalibration != null || _scaleCalibration.Count > 0)
                {
                    value = _scaleCalibration.OrderBy(p => p.Key).First().Key;
                    //switch (ReadingSource)
                    //{
                    //    case Reading.SIGNAL_STRENGTH:
                    //    case Reading.AVG_SIGNAL_STRENGTH:
                            ZeroReading(out value, rx, ReadingSource);
                    //        {
                    //            if (IsAboveS9Frequency(rx))
                    //                value = -153; //S0
                    //            else
                    //               value = -133; //S0
                    //        }
                    //        break;
                    //}
                    return true;
                }
                value = 0;
                return false;
            }
        }
        internal class clsDataOut : clsMeterItem
        {
            private string _mmio_4char;

            public clsDataOut()
            {
                _mmio_4char = "";

                ItemType = MeterItemType.DATA_OUT;
                ReadingSource = Reading.NONE;

                UpdateInterval = 1000;
            }
            public string MMIOFourChar
            {
                get { return _mmio_4char; }
                set { _mmio_4char = value; }
            }
            public override void Update(int rx, ref List<Reading> readingsUsed, Dictionary<Reading, object> all_list_item_readings = null)
            {
                if (all_list_item_readings != null)
                {
                    if (MultiMeterIO.Data.ContainsKey(MMIOGuid))
                    {
                        MultiMeterIO.clsMMIO mmio = MultiMeterIO.Data[MMIOGuid];
                        switch(mmio.FormatOut)
                        {
                            case MultiMeterIO.MMIOFormat.JSON:
                                Dictionary<string, string> out_data_converted = new Dictionary<string, string>();
                                out_data_converted.Add("rx", rx.ToString());
                                foreach (KeyValuePair<Reading, object> kvp in all_list_item_readings)
                                {
                                    if(kvp.Value is float flt)
                                        out_data_converted.Add(kvp.Key.ToString().ToLower(), flt.ToString("0.0#####"));
                                    if (kvp.Value is double dbl)
                                        out_data_converted.Add(kvp.Key.ToString().ToLower(), dbl.ToString("0.0#####"));
                                    else if (kvp.Value is string strng)
                                        out_data_converted.Add(kvp.Key.ToString().ToLower(), strng);
                                }
                                string jsonString = JsonConvert.SerializeObject(out_data_converted, Newtonsoft.Json.Formatting.None);
                                MultiMeterIO.SendDataMMIO(MMIOGuid, jsonString);
                                break;
                            case MultiMeterIO.MMIOFormat.XML:
                                XElement root = new XElement("data");
                                XElement entry = new XElement("rx", rx.ToString());
                                root.Add(entry);
                                foreach (KeyValuePair<Reading, object> kvp in all_list_item_readings)
                                {
                                    if (kvp.Value is float flt)
                                        entry = new XElement(kvp.Key.ToString().ToLower(), flt.ToString("0.0#####"));
                                    if (kvp.Value is double dbl)
                                        entry = new XElement(kvp.Key.ToString().ToLower(), dbl.ToString("0.0#####"));
                                    else if (kvp.Value is string strng)
                                        entry = new XElement(kvp.Key.ToString().ToLower(), strng);
                                    root.Add(entry);
                                }
                                MultiMeterIO.SendDataMMIO(MMIOGuid, root.ToString(SaveOptions.DisableFormatting));
                                break;
                            case MultiMeterIO.MMIOFormat.RAW:
                                string out_data = "rx:" + rx.ToString() + ":";
                                foreach (KeyValuePair<Reading, object> kvp in all_list_item_readings)
                                {
                                    Reading reading = kvp.Key;
                                    out_data += reading.ToString().ToLower() + ":";
                                    if (kvp.Value is float flt)
                                        out_data += flt.ToString("0.0#####") + ":";
                                    if (kvp.Value is double dbl)
                                        out_data += dbl.ToString("0.0#####") + ":";
                                    else if (kvp.Value is string strng)
                                        out_data += strng.Replace(":", "") + ":";
                                }
                                MultiMeterIO.SendDataMMIO(MMIOGuid, out_data);
                                break;
                        }
                    }
                }
            }
        }
        internal class clsSpacerItem : clsMeterItem
        {
            private System.Drawing.Color _colour1;
            private System.Drawing.Color _colour2;
            private float _padding;
            public clsSpacerItem()
            {
                _colour1 = System.Drawing.Color.FromArgb(32, 32, 32);
                _colour2 = System.Drawing.Color.FromArgb(32, 32, 32);

                _padding = 0.1f;

                ItemType = MeterItemType.SPACER;
                ReadingSource = Reading.NONE;

                UpdateInterval = 100;
            }
            public System.Drawing.Color Colour1
            {
                get { return _colour1; }
                set { _colour1 = value; }
            }
            public System.Drawing.Color Colour2
            {
                get { return _colour2; }
                set { _colour2 = value; }
            }
            public float Padding
            {
                get { return _padding; }
                set { _padding = value; }
            }
        }
        internal class clsTextOverlay : clsMeterItem
        {
            private System.Drawing.Color _text_colour1;
            private System.Drawing.Color _text_colour2;
            private System.Drawing.Color _text_back_colour1;
            private System.Drawing.Color _text_back_colour2;
            private System.Drawing.Color _panel_back_colour1;
            private System.Drawing.Color _panel_back_colour2;
            private string _text_1;
            private string _text_2;
            private bool _show_text_back_colour1;
            private bool _show_text_back_colour2;
            private bool _show_back_panel;
            private float _x_offset_1;
            private float _y_offset_1;
            private float _x_offset_2;
            private float _y_offset_2;
            private string _fontFamily_1;
            private FontStyle _fontStyle_1;
            private float _fontSize_1;
            private string _fontFamily_2;
            private FontStyle _fontStyle_2;
            private float _fontSize_2;
            private float _padding;

            private clsMeter _owningMeter;
            private bool _ignore_measure_cache_1;
            private bool _ignore_measure_cache_2;
            private List<string> _list_placeholders_strings_1;
            private List<string> _list_placeholders_strings_2;
            private List<Reading> _list_placeholders_readings_1;
            private List<Reading> _list_placeholders_readings_2;
            private readonly object _list_placeholders_1_lock = new object();
            private readonly object _list_placeholders_2_lock = new object();
            public clsTextOverlay(clsMeter owningMeter)
            {
                _list_placeholders_strings_1 = new List<string>();
                _list_placeholders_strings_2 = new List<string>();
                _list_placeholders_readings_1 = new List<Reading>();
                _list_placeholders_readings_2 = new List<Reading>();

                _text_colour1 = System.Drawing.Color.FromArgb(255, 255, 255);
                _text_colour2 = System.Drawing.Color.FromArgb(255, 255, 255);
                _text_back_colour1 = System.Drawing.Color.FromArgb(64, 64, 64);
                _text_back_colour2 = System.Drawing.Color.FromArgb(64, 64, 64);
                _panel_back_colour1 = System.Drawing.Color.FromArgb(32, 32, 32);
                _panel_back_colour2 = System.Drawing.Color.FromArgb(32, 32, 32);
                _text_1 = "";
                _text_2 = "";
                _show_text_back_colour1 = false;
                _show_text_back_colour2 = false;
                _show_back_panel = true;
                _x_offset_1 = 0;
                _y_offset_1 = 0;
                _x_offset_2 = 0;
                _y_offset_2 = 0;
                ItemType = MeterItemType.TEXT_OVERLAY;
                ReadingSource = Reading.NONE;

                _fontFamily_1 = "Trebuchet MS";
                _fontStyle_1 = FontStyle.Regular;
                _fontSize_1 = 18f;
                _fontFamily_2 = "Trebuchet MS";
                _fontStyle_2 = FontStyle.Regular;
                _fontSize_2 = 18f;

                _padding = 0.1f;

                _ignore_measure_cache_1 = false;
                _ignore_measure_cache_2 = false;

                _owningMeter = owningMeter;

                UpdateInterval = 100;
            }
            public System.Drawing.Color TextColour1
            {
                get { return _text_colour1; }
                set { _text_colour1 = value; }
            }
            public System.Drawing.Color TextColour2
            {
                get { return _text_colour2; }
                set { _text_colour2 = value; }
            }
            public System.Drawing.Color TextBackColour1
            {
                get { return _text_back_colour1; }
                set { _text_back_colour1 = value; }
            }
            public System.Drawing.Color TextBackColour2
            {
                get { return _text_back_colour2; }
                set { _text_back_colour2 = value; }
            }
            public string Text1
            {
                get { return _text_1.Replace("|", ""); }
                set
                {
                    _ignore_measure_cache_1 = _text_1 != value;
                    _text_1 = value.Replace("|", "");
                    ReadingsCustom.UpdateReadings(_text_1);
                    lock (_list_placeholders_1_lock)
                    {
                        _list_placeholders_readings_1.Clear();
                        _list_placeholders_strings_1.Clear();
                        List<string> placeholders = ReadingsCustom.GetPlaceholders(_text_1);
                        foreach (string placeholder in placeholders)
                        {
                            if (ReadingsCustom.IsCustomString(placeholder))
                                _list_placeholders_strings_1.Add(placeholder);
                            else
                            {
                                bool ok = Enum.TryParse<Reading>(placeholder.ToUpper(), out Reading tmpReading);
                                if (ok) _list_placeholders_readings_1.Add(tmpReading);
                            }
                        }
                    }
                }
            }
            public string Text2
            {
                get { return _text_2.Replace("|", ""); }
                set 
                {
                    _ignore_measure_cache_2 = _text_2 != value;
                    _text_2 = value.Replace("|", "");
                    ReadingsCustom.UpdateReadings(_text_2);
                    lock (_list_placeholders_2_lock)
                    {
                        _list_placeholders_readings_2.Clear();
                        _list_placeholders_strings_2.Clear();
                        List<string> placeholders = ReadingsCustom.GetPlaceholders(_text_2);
                        foreach(string placeholder in placeholders)
                        {
                            if (ReadingsCustom.IsCustomString(placeholder))
                                _list_placeholders_strings_2.Add(placeholder);
                            else
                            {
                                bool ok = Enum.TryParse<Reading>(placeholder.ToUpper(), out Reading tmpReading);
                                if (ok) _list_placeholders_readings_2.Add(tmpReading);
                            }
                        }
                    }
                }
            }
            public bool ShowTextBackColour1
            {
                get { return _show_text_back_colour1; }
                set { _show_text_back_colour1 = value; }
            }
            public bool ShowTextBackColour2
            {
                get { return _show_text_back_colour2; }
                set { _show_text_back_colour2 = value; }
            }
            public bool ShowBackPanel
            {
                get { return _show_back_panel; }
                set { _show_back_panel = value; }
            }
            public System.Drawing.Color PanelBackColour1
            {
                get { return _panel_back_colour1; }
                set { _panel_back_colour1 = value; }
            }
            public System.Drawing.Color PanelBackColour2
            {
                get { return _panel_back_colour2; }
                set { _panel_back_colour2 = value; }
            }
            public float TextXOffset1
            {
                get { return _x_offset_1; }
                set { _x_offset_1 = value; }
            }
            public float TextYOffset1
            {
                get { return _y_offset_1; }
                set { _y_offset_1 = value; }
            }
            public float TextXOffset2
            {
                get { return _x_offset_2; }
                set { _x_offset_2 = value; }
            }
            public float TextYOffset2
            {
                get { return _y_offset_2; }
                set { _y_offset_2 = value; }
            }
            public string FontFamily1
            {
                get { return _fontFamily_1; }
                set { _fontFamily_1 = value; }
            }
            public FontStyle Style1
            {
                get { return _fontStyle_1; }
                set { _fontStyle_1 = value; }
            }
            public float FontSize1
            {
                get { return _fontSize_1; }
                set { _fontSize_1 = value; }
            }
            public string FontFamily2
            {
                get { return _fontFamily_2; }
                set { _fontFamily_2 = value; }
            }
            public FontStyle Style2
            {
                get { return _fontStyle_2; }
                set { _fontStyle_2 = value; }
            }
            public float FontSize2
            {
                get { return _fontSize_2; }
                set { _fontSize_2 = value; }
            }
            public float Padding
            {
                get { return _padding; }
                set { _padding = value; }
            }
            public bool IgnoreMeasureCache1
            {
                get { return _ignore_measure_cache_1; }
                set { _ignore_measure_cache_1 = value; }
            }
            public bool IgnoreMeasureCache2
            {
                get { return _ignore_measure_cache_2; }
                set { _ignore_measure_cache_2 = value; }
            }
            public override void Update(int rx, ref List<Reading> readingsUsed, Dictionary<Reading, object> all_list_item_readings = null)
            {
                lock (_list_placeholders_1_lock)
                {
                    foreach(Reading reading in _list_placeholders_readings_1)
                    {
                        ReadingsCustom.TakeReading(rx, reading);

                        //if (!readingsUsed.Contains(reading))
                        //    readingsUsed.Add(reading);
                    }
                }
                lock (_list_placeholders_2_lock)
                {
                    foreach (Reading reading in _list_placeholders_readings_2)
                    {
                        ReadingsCustom.TakeReading(rx, reading);

                        //if (!readingsUsed.Contains(reading))
                        //    readingsUsed.Add(reading);
                    }
                }
            }
            public string ParsedText1(int rx)
            {
                string sTmp = _text_1;
                string lower;

                lock (_list_placeholders_1_lock)
                {
                    foreach (Reading r in _list_placeholders_readings_1)
                    {
                        lower = "%" + r.ToString().ToLower() + "%";
                        if (sTmp.IndexOf(lower) >= 0)
                        {
                            object reading = ReadingsCustom.GetReading(r.ToString(), _owningMeter, rx);
                            sTmp = sTmp.Replace(lower, ((float)reading).ToString("0.0#####"));
                        }
                    }
                    foreach (string placeholder in _list_placeholders_strings_1)
                    {
                        lower = "%" + placeholder.ToLower() + "%";
                        if (sTmp.IndexOf(lower) >= 0)
                        {
                            string decFormat = placeholder.IndexOf("_double", StringComparison.OrdinalIgnoreCase) >= 0 ? "f6" : "0.0#####";
                            object reading = ReadingsCustom.GetReading(placeholder, _owningMeter, rx);
                            if (reading is int)
                                sTmp = sTmp.Replace(lower, ((int)reading).ToString());
                            else if (reading is float)
                                sTmp = sTmp.Replace(lower, ((float)reading).ToString(decFormat));
                            else if (reading is double)
                                sTmp = sTmp.Replace(lower, ((double)reading).ToString(decFormat));
                            else if (reading is bool)
                                sTmp = sTmp.Replace(lower, ((bool)reading).ToString());
                            else
                                sTmp = sTmp.Replace(lower, (string)reading);
                        }
                    }
                }

                if (sTmp.IndexOf("%nl%") >= 0)
                    sTmp = sTmp.Replace("%nl%", "\n");

                // MultiMeter IO
                foreach (KeyValuePair<Guid, MultiMeterIO.clsMMIO> mmios in MultiMeterIO.Data)
                {
                    MultiMeterIO.clsMMIO mmio = mmios.Value;
                    foreach (KeyValuePair<string, object> kvp in mmio.Variables())
                    {
                        lower = "%" + kvp.Key + "%";
                        if (sTmp.IndexOf(lower) >= 0)
                        {
                            object val = mmio.GetVariable(kvp.Key);

                            string tmp = mmio.VariableValueType(val);

                            sTmp = sTmp.Replace(lower, tmp);
                        }
                    }
                }
                //

                return sTmp;
            }
            public string ParsedText2(int rx)
            {
                string sTmp = _text_2;
                string lower;

                lock (_list_placeholders_2_lock)
                {
                    foreach (Reading r in _list_placeholders_readings_2)
                    {
                        lower = "%" + r.ToString().ToLower() + "%";
                        if (sTmp.IndexOf(lower) >= 0)
                        {
                            object reading = ReadingsCustom.GetReading(r.ToString(), _owningMeter, rx);
                            sTmp = sTmp.Replace(lower, ((float)reading).ToString("0.0#####"));
                        }
                    }
                    foreach (string placeholder in _list_placeholders_strings_2)
                    {
                        lower = "%" + placeholder.ToLower() + "%";
                        if (sTmp.IndexOf(lower) >= 0)
                        {
                            string decFormat = placeholder.ToLower().Contains("_double") ? "f6" : "0.0#####";
                            object reading = ReadingsCustom.GetReading(placeholder, _owningMeter, rx);
                            if (reading is int)
                                sTmp = sTmp.Replace(lower, ((int)reading).ToString());
                            else if (reading is float)
                                sTmp = sTmp.Replace(lower, ((float)reading).ToString(decFormat));
                            else if (reading is double)
                                sTmp = sTmp.Replace(lower, ((double)reading).ToString(decFormat));
                            else if (reading is bool)
                                sTmp = sTmp.Replace(lower, ((bool)reading).ToString());
                            else
                                sTmp = sTmp.Replace(lower, (string)reading);
                        }
                    }
                }

                if (sTmp.IndexOf("%nl%") >= 0)
                    sTmp = sTmp.Replace("%nl%", "\n");

                // MultiMeter IO
                foreach (KeyValuePair<Guid, MultiMeterIO.clsMMIO> mmios in MultiMeterIO.Data)
                {
                    MultiMeterIO.clsMMIO mmio = mmios.Value;
                    foreach (KeyValuePair<string, object> kvp in mmio.Variables())
                    {
                        lower = "%" + kvp.Key + "%";
                        if (sTmp.IndexOf(lower) >= 0)
                        {
                            object val = mmio.GetVariable(kvp.Key);

                            string tmp = mmio.VariableValueType(val);

                            sTmp = sTmp.Replace(lower, tmp);
                        }
                    }
                }
                //

                return sTmp;
            }
            public override bool ZeroOut(out float value, int rx)
            {
                value = 0;
                lock (_list_placeholders_1_lock)
                {
                    foreach(Reading reading in _list_placeholders_readings_1)
                    {
                        ZeroReading(out value, rx, reading);
                        MeterManager.setReadingForced(rx, reading, value);
                    }
                }
                lock (_list_placeholders_2_lock)
                {
                    foreach (Reading reading in _list_placeholders_readings_2)
                    {
                        ZeroReading(out value, rx, reading);
                        MeterManager.setReadingForced(rx, reading, value);
                    }
                }
                return false; // false as we do our own setReadingForced just above
            }
        }
        internal class clsLed : clsMeterItem
        {
            public enum Led_Shape
            {
                SQUARE = 0,
                ROUND = 1,
                TRIANGLE = 2
            }
            public enum Led_Style
            {
                FLAT = 0,
                THREE_D = 1
            }
            private System.Drawing.Color _true_colour;
            private System.Drawing.Color _false_colour;
            private System.Drawing.Color _panel_back_colour_1;
            private System.Drawing.Color _panel_back_colour_2;
            private string _condition;            
            private bool _show_back_panel;
            private float _x_offset;
            private float _y_offset;
            private float _x_size;
            private float _y_size;
            private float _padding;
            private clsMeter _owningMeter;
            private Led_Shape _led_shape;
            private Led_Style _led_style;
            private bool _old_result;
            private bool _result;
            private bool _valid;
            private List<string> _list_placeholders_strings;
            private List<Reading> _list_placeholders_readings;
            private readonly object _list_placeholders_lock = new object();
            private Script _script;
            private Dictionary<string, object> _variable_substitutions;
            private bool _busy = false;
            private bool _error = false;
            CancellationTokenSource _cts;
            private bool _forceRecompile;

            private bool _blink;
            private bool _pulsate;
            private bool _pulsate_up;
            private int _transition_fade;
            private float _color_fade;
            private int _blink_count;
            private bool _show_false;
            private bool _show_true;

            public clsLed(clsMeter owningMeter)
            {
                _list_placeholders_strings = new List<string>();
                _list_placeholders_readings = new List<Reading>();

                _true_colour = System.Drawing.Color.FromArgb(255, 255, 255);
                _false_colour = System.Drawing.Color.FromArgb(192, 192, 192);
                _panel_back_colour_1 = System.Drawing.Color.FromArgb(32, 32, 32);
                _panel_back_colour_2 = System.Drawing.Color.FromArgb(32, 32, 32);
                _show_back_panel = true;
                _x_offset = 0.5f;
                _y_offset = 0.05f;
                _condition = "";
                _x_size = 0.05f;
                _y_size = 0.05f;
                _padding = 0.1f;
                _led_shape = Led_Shape.SQUARE;
                _led_style = Led_Style.FLAT;
                _old_result = false;
                _result = false;
                _valid = false;
                _busy = false;
                _error = false;

                ItemType = MeterItemType.LED;
                ReadingSource = Reading.NONE;

                _owningMeter = owningMeter;

                UpdateInterval = 50;

                _cts = null;
                _script = null;
                _variable_substitutions = new Dictionary<string, object>();

                _forceRecompile = false;

                _pulsate_up = true;
                _pulsate = false;
                _blink = false;
                _transition_fade = 0;
                _color_fade = 0;
                _blink_count = 0;

                _show_true = true;
                _show_false = true;
            }
            //private string substituteVariables(string expression, Dictionary<string, object> variables)
            //{
            //    foreach (KeyValuePair<string, object> variable in variables)
            //    {
            //        string placeholder = $"{variable.Key}";
            //        expression = expression.Replace(placeholder, variable.Value.ToString());
            //    }
            //    return expression;
            //}
            private bool validateExpression(string expression, Dictionary<string, object> variables)
            {
                string tmp = $"bool result = (bool)({expression});";
                try
                {
                    SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(tmp);
                    CSharpCompilation compilation = CSharpCompilation.Create("ExpressionValidation")
                        .AddReferences(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
                        //.AddReferences(MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location))
                        //.AddReferences(MetadataReference.CreateFromFile(typeof(Console).Assembly.Location))
                        .AddSyntaxTrees(syntaxTree);
                    IEnumerable<Diagnostic> diagnostics = compilation.GetDiagnostics();
                    foreach (Diagnostic diagnostic in diagnostics)
                    {
                        if (diagnostic.Severity == DiagnosticSeverity.Error)
                        {
                            return false;
                        }
                    }
                    return true;
                }
                catch (Exception ex)
                {
                    //MessageBox.Show($"Exception: {ex.Message}");
                    //MessageBox.Show($"String passed to Parse Text:\n{tmp}\n\nStack Trace: {ex.StackTrace}");
                    return false;
                }
            }
            private async System.Threading.Tasks.Task<bool> evaluateExpression(CancellationToken cancellationToken)
            {
                if (!_valid || _script == null || _busy) return false;
                _busy = true;
                bool bRet = false;
                try
                {
                    Globals globals = new Globals();
                    globals.Variables = _variable_substitutions;
                    ScriptState ss = await _script.RunAsync(globals, cancellationToken);
                    if (ss.Variables.Length == 1)
                    {
                        ScriptVariable sv = ss.Variables[0];
                        bRet = (bool)sv.Value;
                        _error = false;
                    }
                    else
                    {
                        _valid = false;
                        _error = true;
                        _script = null;
                    }
                }
                catch (Exception ex)
                { 
                    _valid = false;
                    _script = null;
                    _error = true;
                }
                _busy = false;
                return bRet;
            }
            public bool ScriptError
            {
                get { return _error; }
            }
            public bool ScriptValid
            {
                get { return _valid; }
            }
            public bool ConditionResult
            {
                get { return _result; }
            }
            public bool OldConditionResult
            {
                get { return _old_result; }
            }
            public System.Drawing.Color PanelBackColour1
            {
                get { return _panel_back_colour_1; }
                set { _panel_back_colour_1 = value; }
            }
            public System.Drawing.Color PanelBackColour2
            {
                get { return _panel_back_colour_2; }
                set { _panel_back_colour_2 = value; }
            }
            public bool ShowTrue
            {
                get { return _show_true; }
                set { _show_true = value; }
            }
            public bool ShowFalse
            {
                get { return _show_false; }
                set { _show_false = value; }
            }
            public Led_Shape LedShape
            {
                get { return _led_shape; }
                set { _led_shape = value; }
            }
            public Led_Style LedStyle
            {
                get { return _led_style; }
                set { _led_style = value; }
            }
            public string Condition
            {
                get { return _condition; }
                set {
                    if (value == _condition && !_forceRecompile) return;
                    _forceRecompile = false;

                    //_condition = value.Replace("|", "");
                    _condition = value;
                    ReadingsCustom.UpdateReadings(_condition);
                    lock (_list_placeholders_lock)
                    {
                        _list_placeholders_readings.Clear();
                        _list_placeholders_strings.Clear();
                        List<string> placeholders = ReadingsCustom.GetPlaceholders(_condition);
                        foreach (string placeholder in placeholders)
                        {
                            if (ReadingsCustom.IsCustomString(placeholder))
                                _list_placeholders_strings.Add(placeholder);
                            else
                            {
                                bool ok = Enum.TryParse<Reading>(placeholder.ToUpper(), out Reading tmpReading);
                                if (ok) _list_placeholders_readings.Add(tmpReading);
                            }
                        }

                        _variable_substitutions.Clear();                        
                        string expression = _condition;
                        string script_expression = _condition;
                        string lower;
                        foreach (Reading r in _list_placeholders_readings)
                        {
                            object reading = ReadingsCustom.GetReading(r.ToString(), _owningMeter, _owningMeter.RX);
                            lower = "%" + r.ToString().ToLower() + "%";
                            if (expression.IndexOf(lower) >= 0)
                                expression = expression.Replace(lower, reading.ToString());
                            if (script_expression.IndexOf(lower) >= 0)
                                script_expression = script_expression.Replace(lower, "(float)Variables[\"" + r.ToString().ToLower() + "\"]");

                            if (!_variable_substitutions.ContainsKey(r.ToString().ToLower()))
                                _variable_substitutions.Add(r.ToString().ToLower(), reading);
                        }
                        foreach (string placeholder in _list_placeholders_strings)
                        {
                            object reading = ReadingsCustom.GetReading(placeholder, _owningMeter, _owningMeter.RX);
                            string type;
                            if (reading is int)
                                type = "int";
                            else if (reading is float)
                                type = "float";
                            else if (reading is double)
                                type = "double";
                            else if (reading is bool)
                                type = "bool";
                            else
                                type = "string";

                            lower = "%" + placeholder.ToLower() + "%";
                            if (expression.IndexOf(lower) >= 0)
                                expression = expression.Replace(lower, "(" + type + ")(" + (type == "string" ? "\"" : "") + reading.ToString() + (type == "string" ? "\"" : "") + ")");
                            if (script_expression.IndexOf(lower) >= 0)
                                script_expression = script_expression.Replace(lower, "(" + type + ")(Variables[\"" + placeholder.ToLower() + "\"])");

                            if (!_variable_substitutions.ContainsKey(placeholder.ToLower()))
                                _variable_substitutions.Add(placeholder.ToLower(), reading);
                        }

                        // MultiMeter IO
                        foreach (KeyValuePair<Guid, MultiMeterIO.clsMMIO> mmios in MultiMeterIO.Data)
                        {
                            MultiMeterIO.clsMMIO mmio = mmios.Value;
                            foreach (KeyValuePair<string, object> kvp in mmio.Variables())
                            {
                                object val = mmio.GetVariable(kvp.Key);

                                string tmp = mmio.VariableValueType(val);
                                lower = "%" + kvp.Key + "%";
                                if (script_expression.IndexOf(lower) >= 0)
                                {
                                    string type;
                                    if (val is int)
                                        type = "int";
                                    else if (val is float)
                                        type = "float";
                                    else if (val is double)
                                        type = "double";
                                    else if (val is bool)
                                        type = "bool";
                                    else
                                        type = "string";

                                    if (expression.IndexOf(lower) >= 0)
                                        expression = expression.Replace(lower, (type == "string" ? "\"" : "") + tmp + (type == "string" ? "\"" : ""));
                                    if (script_expression.IndexOf(lower) >= 0)
                                        script_expression = script_expression.Replace(lower, "(" + type + ")(Variables[\"" + kvp.Key + "\"])");

                                    if (!_variable_substitutions.ContainsKey(kvp.Key))
                                        _variable_substitutions.Add(kvp.Key, val);
                                }
                            }
                        }
                        //

                        bool okExp = validateExpression(expression, _variable_substitutions);
                        if (okExp)
                        {
                            try
                            {
                                ScriptOptions options = ScriptOptions.Default.AddReferences(typeof(object).Assembly);
                                _script = CSharpScript.Create($"bool result = (bool)({script_expression});", options, typeof(Globals));
                                _script.Compile();

                                _valid = true;
                            }
                            catch
                            {
                                _script = null;
                                _valid = false;
                            }
                        }
                    }                    
                }
            }
            public float OffsetX
            {
                get { return _x_offset; }
                set { _x_offset = value; }
            }
            public float OffsetY
            {
                get { return _y_offset; }
                set { _y_offset = value; }
            }
            public float SizeX
            {
                get { return _x_size; }
                set { _x_size = value; }
            }
            public float SizeY
            {
                get { return _y_size; }
                set { _y_size = value; }
            }
            public float ColorFade
            {
                get { return _color_fade; }
                set {
                    if (value < 0) value = 0;
                    if (value > 1) value = 1;
                    _color_fade = value; 
                }
            }
            public int TransitionFade
            {
                get { return _transition_fade; }
                set { _transition_fade = value; }
            }
            public bool Pulsate
            {
                get { return _pulsate; }
                set { _pulsate = value; }
            }
            public bool PulsateUp
            {
                get { return _pulsate_up; }
                set { _pulsate_up = value; }
            }
            public bool Blink
            {
                get { return _blink; }
                set { _blink = value; }
            }
            public int BlinkCount
            {
                get { return _blink_count; }
                set { _blink_count = value; }
            }
            public System.Drawing.Color TrueColour
            {
                get { return _true_colour; }
                set { _true_colour = value; }
            }
            public System.Drawing.Color FalseColour
            {
                get { return _false_colour; }
                set { _false_colour = value; }
            }
            public bool ShowBackPanel
            {
                get { return _show_back_panel; }
                set { _show_back_panel = value; }
            }
            public float Padding
            {
                get { return _padding; }
                set { _padding = value; }
            }
            public override void Update(int rx, ref List<Reading> readingsUsed, Dictionary<Reading, object> all_list_item_readings = null)
            {
                if (_valid && _script != null)
                {
                    bool typesChanged = false;

                    lock (_list_placeholders_lock)
                    {
                        // regular readings
                        foreach (Reading r in _list_placeholders_readings)
                        {
                            if (_variable_substitutions.ContainsKey(r.ToString().ToLower()))
                            {
                                ReadingsCustom.TakeReading(rx, r);
                                object reading = ReadingsCustom.GetReading(r.ToString(), _owningMeter, rx);
                                _variable_substitutions[r.ToString().ToLower()] = (float)reading;
                            }
                        }
                        // readings with string as ID
                        foreach (string placeholder in _list_placeholders_strings)
                        {
                            if (_variable_substitutions.ContainsKey(placeholder.ToLower()))
                            {
                                object reading = ReadingsCustom.GetReading(placeholder, _owningMeter, rx);
                                _variable_substitutions[placeholder.ToLower()] = reading;
                            }
                        }
                    }

                    // MultiMeter IO readings
                    foreach (KeyValuePair<Guid, MultiMeterIO.clsMMIO> mmios in MultiMeterIO.Data)
                    {
                        MultiMeterIO.clsMMIO mmio = mmios.Value;
                        foreach (KeyValuePair<string, object> kvp in mmio.Variables())
                        {
                            if (_variable_substitutions.ContainsKey(kvp.Key))
                            {
                                object var = mmio.GetVariable(kvp.Key);
                                if(var.GetType() != _variable_substitutions[kvp.Key].GetType())
                                {
                                    typesChanged = true;
                                    break;
                                }
                                _variable_substitutions[kvp.Key] = var;
                            }
                        }
                    }
                    //

                    if(_cts != null)
                    {
                        _cts.Cancel();
                        _cts.Dispose();
                        _cts = null;
                    }
                    if (_busy) return;

                    if (typesChanged)
                    {
                        Debug.Print(">>>> RECOMPILE");
                        _forceRecompile = true;
                        Condition = Condition; // force recompile
                    }
                    else
                    {
                        _cts = new CancellationTokenSource();
                        _old_result = _result;
                        _result = evaluateExpression(_cts.Token).Result;
                    }
                }
            }
            public override bool ZeroOut(out float value, int rx)
            {
                value = 0;
                lock (_list_placeholders_lock)
                {
                    foreach (Reading reading in _list_placeholders_readings)
                    {
                        ZeroReading(out value, rx, reading);
                        MeterManager.setReadingForced(rx, reading, value);
                    }
                }
                return false; // false as we do our own setReadingForced just above
            }
        }
        //internal class clsHistoryItem : clsMeterItem
        //{
        //    //public enum BarStyle
        //    //{
        //    //    None = 0,
        //    //    Line,
        //    //    SolidFilled,
        //    //    GradientFilled,
        //    //    Segments,
        //    //}
        //    private List<float> _history;
        //    private int _msHistoryDuration; //ms
        //    private bool _showHistory;
        //    //private bool _showValue;
        //    //private bool _showPeakValue;
        //    private readonly object _historyLock = new object();
        //    //private BarStyle _style;
        //    //private System.Drawing.Color _colour;
        //    //private System.Drawing.Color _markerColour;
        //    //private System.Drawing.Color _peakHoldMarkerColour;
        //    //private System.Drawing.Color _peakValueColour;
        //    private float _strokeWidth;
        //    //private bool _peakHold;
        //    //private bool _showMarker;

        //    private string _fontFamily;
        //    private FontStyle _fontStyle;
        //    private System.Drawing.Color _fontColor;
        //    private float _fontSize;

        //    private Dictionary<float, PointF> _scaleCalibration;

        //    public clsHistoryItem()
        //    {
        //        _history = new List<float>();
        //        _msHistoryDuration = 2000;
        //        _showHistory = false;
        //        //_showValue = true;
        //        //_showPeakValue = true;

        //        //_style = BarStyle.Line;
        //        //_colour = System.Drawing.Color.Red;
        //        //_markerColour = System.Drawing.Color.Yellow;
        //        //_peakHoldMarkerColour = System.Drawing.Color.Red;
        //        //_peakValueColour = System.Drawing.Color.Red;
        //        _strokeWidth = 3f;
        //        //_peakHold = false;
        //        //_showMarker = true;

        //        _scaleCalibration = new Dictionary<float, PointF>();

        //        _fontFamily = "Trebuchet MS";
        //        _fontStyle = FontStyle.Regular;
        //        _fontColor = System.Drawing.Color.DarkGray;
        //        _fontSize = 20f;

        //        ItemType = MeterItemType.HISTORY;
        //        ReadingSource = Reading.NONE;
        //        UpdateInterval = 100;
        //        //AttackRatio = 0.8f;
        //        //DecayRatio = 0.2f;
        //        StoreSettings = false;
        //    }        
        //    public override void Update(int rx, ref List<Reading> readingsUsed)
        //    {
        //        // get latest reading
        //        float reading = MeterManager.getReading(rx, ReadingSource);

        //        lock (_historyLock)
        //        {
        //            //if (reading > Value)
        //            //    Value = (reading * AttackRatio) + (Value * (1f - AttackRatio));
        //            //else
        //            //    Value = (reading * DecayRatio) + (Value * (1f - DecayRatio));
        //            Value = reading;

        //            // signal history
        //            _history.Add(Value); // adds to end of the list
        //            int numberToRemove = _history.Count - (_msHistoryDuration / UpdateInterval);
        //            // the list is sized based on delay
        //            if (numberToRemove > 0) _history.RemoveRange(0, numberToRemove); // remove the oldest, the head of the list
        //        }

        //        // this reading has been used
        //        if (!readingsUsed.Contains(ReadingSource))
        //            readingsUsed.Add(ReadingSource);
        //    }

        //    public List<float> GetHistory()
        //    {
        //        lock (_historyLock)
        //        {
        //            // float not ref type, so can create unique copy
        //            List<float> newList = new List<float>(_history);
        //            return newList;
        //        }
        //    }
        //    //public BarStyle Style
        //    //{
        //    //    get { return _style; }
        //    //    set { _style = value; }
        //    //}
        //    public override bool ShowHistory
        //    {
        //        get { return _showHistory; }
        //        set { _showHistory = value; }
        //    }
        //    //public bool ShowValue
        //    //{
        //    //    get { return _showValue; }
        //    //    set { _showValue = value; }
        //    //}
        //    //public bool ShowPeakValue
        //    //{
        //    //    get { return _showPeakValue; }
        //    //    set { _showPeakValue = value; }
        //    //}
        //    public int HistoryDuration
        //    {
        //        get { return _msHistoryDuration; }
        //        set
        //        {
        //            _msHistoryDuration = value;
        //            if (_msHistoryDuration < UpdateInterval) _msHistoryDuration = UpdateInterval;
        //        }
        //    }
        //    public override float MinHistory
        //    {
        //        get
        //        {
        //            lock (_historyLock)
        //            {
        //                if (_history.Count == 0) return Value;
        //                return _history.Min();
        //            }
        //        }
        //    }
        //    public override float MaxHistory
        //    {
        //        get
        //        {
        //            lock (_historyLock)
        //            {
        //                if (_history.Count == 0) return Value;
        //                return _history.Max();
        //            }
        //        }
        //    }
        //    public override void History(out float minHistory, out float maxHistory)
        //    {
        //        if (_history.Count == 0)
        //        {
        //            minHistory = Value;
        //            maxHistory = Value;
        //        }
        //        else
        //        {
        //            minHistory = _history.Min();
        //            maxHistory = _history.Max();
        //        }
        //    }
        //    //public System.Drawing.Color Colour
        //    //{
        //    //    get { return _colour; }
        //    //    set { _colour = value; }
        //    //}
        //    //public System.Drawing.Color MarkerColour
        //    //{
        //    //    get { return _markerColour; }
        //    //    set { _markerColour = value; }
        //    //}
        //    //public System.Drawing.Color PeakHoldMarkerColour
        //    //{
        //    //    get { return _peakHoldMarkerColour; }
        //    //    set { _peakHoldMarkerColour = value; }
        //    //}
        //    //public System.Drawing.Color PeakValueColour
        //    //{
        //    //    get { return _peakValueColour; }
        //    //    set { _peakValueColour = value; }
        //    //}
        //    //public bool PeakHold
        //    //{
        //    //    get { return _peakHold; }
        //    //    set { _peakHold = value; }
        //    //}
        //    //public bool ShowMarker
        //    //{
        //    //    get { return _showMarker; }
        //    //    set { _showMarker = value; }
        //    //}
        //    public float StrokeWidth
        //    {
        //        get { return _strokeWidth; }
        //        set { _strokeWidth = value; }
        //    }
        //    //public override Dictionary<float, PointF> ScaleCalibration
        //    //{
        //    //    get { return _scaleCalibration; }
        //    //    set { }
        //    //}

        //    public string FontFamily
        //    {
        //        get { return _fontFamily; }
        //        set { _fontFamily = value; }
        //    }
        //    public FontStyle FntStyle
        //    {
        //        get { return _fontStyle; }
        //        set { _fontStyle = value; }
        //    }
        //    public System.Drawing.Color FontColour
        //    {
        //        get { return _fontColor; }
        //        set { _fontColor = value; }
        //    }
        //    public float FontSize
        //    {
        //        get { return _fontSize; }
        //        set { _fontSize = value; }
        //    }
        //}
        internal class clsBarItem : clsMeterItem
        {
            [Serializable]
            public enum Units
            {
                DBM = 0,
                S_UNTS,
                U_V
            }
            [Serializable]
            public enum BarStyle
            {
                None = 0,
                Line,
                SolidFilled,
                GradientFilled,
                Segments,
            }
            private List<float> _history;
            private int _msHistoryDuration; //ms
            private int _msIgnoreHistoryDuration;
            private bool _showHistory;
            private bool _showValue;
            private bool _showPeakValue;
            private readonly object _historyLock = new object();
            private BarStyle _style;
            private System.Drawing.Color _colour;
            private System.Drawing.Color _colourHigh;
            private System.Drawing.Color _markerColour;
            private System.Drawing.Color _peakHoldMarkerColour;
            private System.Drawing.Color _peakValueColour;
            private float _strokeWidth;
            private bool _peakHold;
            private bool _showMarker;
            private Units _units;

            private string _fontFamily;
            private FontStyle _fontStyle;
            private System.Drawing.Color _fontColor;
            private float _fontSize;
            private int _nIgnoringNext;

            private Dictionary<float, PointF> _scaleCalibration;
            private PointF _highPoint;

            private clsBarItem _postDrawItem;

            public clsBarItem()
            {
                _history = new List<float>();
                _msHistoryDuration = 2000;
                _msIgnoreHistoryDuration = 2000;
                _showHistory = false;
                _showValue = true;
                _showPeakValue = true;
                _nIgnoringNext = 0;

                _style = BarStyle.Line;
                _colour = System.Drawing.Color.Red;
                _colourHigh = System.Drawing.Color.Orange;
                _markerColour = System.Drawing.Color.Yellow;
                _peakHoldMarkerColour = System.Drawing.Color.Red;
                _peakValueColour = System.Drawing.Color.Red;
                _strokeWidth = 3f;
                _peakHold = false;
                _showMarker = true;
                _highPoint = PointF.Empty;
                
                _units = Units.DBM;

                _scaleCalibration = new Dictionary<float, PointF>();

                _fontFamily = "Trebuchet MS";
                _fontStyle = FontStyle.Regular;
                _fontColor = System.Drawing.Color.DarkGray;
                _fontSize = 20f;

                ItemType = MeterItemType.H_BAR;
                ReadingSource = Reading.NONE;
                UpdateInterval = 100;
                AttackRatio = 0.8f;
                DecayRatio = 0.2f;
                StoreSettings = false;

                _postDrawItem = null; // used in render loop to cause another bar's marker to be re-drawn after this bar
            }            
            public override void Update(int rx, ref List<Reading> readingsUsed, Dictionary<Reading, object> all_list_item_readings = null)
            {
                // get latest reading
                float reading;
                bool use;
                //if (GetMMIOGuid(0) == Guid.Empty)// && GetMMIOVariable(0) == "-- DEFAULT --") 
                if(MMIOGuid == Guid.Empty)
                {
                    reading = MeterManager.getReading(rx, ReadingSource);
                    use = true;
                }
                else
                {
                    reading = 0;
                    //if (MultiMeterIO.Data.ContainsKey(GetMMIOGuid(0)))
                    if (MultiMeterIO.Data.ContainsKey(MMIOGuid))
                    {
                        //MultiMeterIO.clsMMIO mmio = MultiMeterIO.Data[GetMMIOGuid(0)];
                        MultiMeterIO.clsMMIO mmio = MultiMeterIO.Data[MMIOGuid];

                        //object val = mmio.GetVariable(GetMMIOVariable(0));
                        object val = mmio.GetVariable(MMIOVariable);
                        if (val is int)
                        {
                            int intVal = (int)val;
                            reading = (float)intVal;
                        }
                        else if (val is float)
                        {
                            reading = (float)val;
                        }
                        else if (val is double)
                        {
                            double doubleVal = (double)val;
                            reading = (float)doubleVal;
                        }
                    }
                    use = false;
                }

                lock (_historyLock)
                {
                    if (reading > Value)
                        Value = (reading * AttackRatio) + (Value * (1f - AttackRatio));
                    else
                        Value = (reading * DecayRatio) + (Value * (1f - DecayRatio));

                    if (_nIgnoringNext <= 0)
                    {
                        // signal history
                        _history.Add(Value); // adds to end of the list
                        int numberToRemove = _history.Count - (_msHistoryDuration / UpdateInterval);
                        // the list is sized based on delay
                        if (numberToRemove > 0) _history.RemoveRange(0, numberToRemove); // remove the oldest, the head of the list
                    }
                    else
                    {
                        _nIgnoringNext--;
                    }
                }

                if (use)
                {
                    // this reading has been used
                    if (!readingsUsed.Contains(ReadingSource))
                        readingsUsed.Add(ReadingSource);
                }
            }
            public clsBarItem PostDrawItem
            {
                get { return _postDrawItem; }
                set { _postDrawItem = value; }
            }
            public BarStyle Style
            {
                get { return _style; }
                set { _style = value; }
            }
            public override bool ShowHistory
            {
                get { return _showHistory; }
                set { _showHistory = value; }
            }
            public bool ShowValue
            {
                get { return _showValue; }
                set { _showValue = value; }
            }
            public bool ShowPeakValue
            {
                get { return _showPeakValue; }
                set { _showPeakValue = value; }
            }
            public int HistoryDuration
            {
                get { return _msHistoryDuration; }
                set
                {
                    _msHistoryDuration = value;
                    if (_msHistoryDuration < UpdateInterval) _msHistoryDuration = UpdateInterval;
                }
            }
            public int IgnoreHistoryDuration
            {
                get { return _msIgnoreHistoryDuration; }
                set { _msIgnoreHistoryDuration = value; }
            }
            public override float MinHistory
            {
                get
                {
                    lock (_historyLock)
                    {
                        if (_history.Count == 0) return Value;
                        return _history.Min();
                    }
                }
            }
            public override float MaxHistory
            {
                get
                {
                    lock (_historyLock)
                    {
                        if (_history.Count == 0) return Value;
                        return _history.Max();
                    }
                }
            }
            public override void ClearHistory()
            {
                lock (_historyLock)
                {
                    _history.Clear();

                    _nIgnoringNext = _msIgnoreHistoryDuration / UpdateInterval;  // ignore next N readings to make up 2 second of ignore
                }
            }
            public override void History(out float minHistory, out float maxHistory)
            {
                if (_history.Count == 0)
                {
                    minHistory = Value;
                    maxHistory = Value;
                }
                else
                {
                    minHistory = _history.Min();
                    maxHistory = _history.Max();
                }
            }
            public System.Drawing.Color Colour
            {
                get { return _colour; }
                set { _colour = value; }
            }
            public System.Drawing.Color ColourHigh
            {
                // used for segments and solid bar
                get { return _colourHigh; }
                set { _colourHigh = value; }
            }
            public System.Drawing.Color MarkerColour
            {
                get { return _markerColour; }
                set { _markerColour = value; }
            }
            public System.Drawing.Color PeakHoldMarkerColour
            {
                get { return _peakHoldMarkerColour; }
                set { _peakHoldMarkerColour = value; }
            }
            public System.Drawing.Color PeakValueColour
            {
                get { return _peakValueColour; }
                set { _peakValueColour = value; }
            }
            public bool PeakHold
            {
                get { return _peakHold; }
                set { _peakHold = value; }
            }
            public bool ShowMarker
            {
                get { return _showMarker; }
                set { _showMarker = value; }
            }
            public float StrokeWidth
            {
                get { return _strokeWidth; }
                set { _strokeWidth = value; }
            }
            public override Dictionary<float, PointF> ScaleCalibration
            {
                get { return _scaleCalibration; }
                set { }
            }

            public string FontFamily
            {
                get { return _fontFamily; }
                set { _fontFamily = value; }
            }
            public FontStyle FntStyle
            {
                get { return _fontStyle; }
                set { _fontStyle = value; }
            }
            public System.Drawing.Color FontColour
            {
                get { return _fontColor; }
                set { _fontColor = value; }
            }
            public float FontSize
            {
                get { return _fontSize; }
                set { _fontSize = value; }
            }

            public override void HandleIncrement()
            {
                int n = (int)_units;
                n++;

                if (ReadingSource != Reading.ESTIMATED_PBSNR)
                {
                    if (n > (int)Units.U_V) n = (int)Units.DBM;
                }
                else
                {
                    if (n > (int)Units.S_UNTS) n = (int)Units.DBM;
                }

                _units = (Units)n;

                Debug.Print("Units = " + _units.ToString());
            }
            public override void HandleDecrement()
            {
                int n = (int)_units;
                n--;
                if (ReadingSource != Reading.ESTIMATED_PBSNR)
                {
                    if (n < (int)Units.DBM) n = (int)Units.U_V;
                }
                else
                {
                    if (n < (int)Units.DBM) n = (int)Units.S_UNTS;
                }

                _units = (Units)n;

                Debug.Print("Units = " + _units.ToString());
            }
            public Units Unit
            {
                get { return _units; }
                set
                {
                    int n = (int)value;
                    if (n < (int)Units.DBM || n > (int)Units.U_V) value = Units.DBM;
                    _units = value;
                }
            }
            public override bool ZeroOut(out float value, int rx)
            {
                if (_scaleCalibration != null || _scaleCalibration.Count > 0)
                {
                    value = _scaleCalibration.OrderBy(p => p.Key).First().Key;
                    ZeroReading(out value, rx, ReadingSource);
                    //switch (ReadingSource)
                    //{
                    //    case Reading.SIGNAL_STRENGTH:
                    //    case Reading.AVG_SIGNAL_STRENGTH:
                    //        {
                    //            if (IsAboveS9Frequency(rx))
                    //                value = -153; //S0
                    //            else
                    //                value = -133; //S0
                    //        }
                    //        break;
                    //    case Reading.ADC_PK:
                    //    case Reading.ADC_AV:
                    //        {
                    //            value = -120.0f;
                    //        }
                    //        break;
                    //    case Reading.AGC_AV:
                    //    case Reading.AGC_PK:
                    //        value = -125.0f;
                    //        break;
                    //    case Reading.AGC_GAIN:
                    //        {
                    //            value = -50.0f;
                    //        }
                    //        break;
                    //    case Reading.MIC:
                    //    case Reading.MIC_PK:
                    //        {
                    //            value = -120.0f;
                    //        }
                    //        break;
                    //    case Reading.LEVELER:
                    //    case Reading.LEVELER_PK:
                    //        {
                    //            value = -30.0f;
                    //        }
                    //        break;
                    //    case Reading.LVL_G:
                    //        {
                    //            value = 0f;
                    //        }
                    //        break;
                    //    case Reading.ALC:
                    //    case Reading.ALC_PK:
                    //        {
                    //            value = -120.0f;
                    //        }
                    //        break;
                    //    case Reading.ALC_G: //alc comp
                    //        {
                    //            value = 0f;
                    //        }
                    //        break;
                    //    case Reading.ALC_GROUP:
                    //        {
                    //            value = -30.0f;
                    //        }
                    //        break;
                    //    case Reading.CFC_AV:
                    //    case Reading.CFC_PK:
                    //        {
                    //            value = -30.0f;
                    //        }
                    //        break;
                    //    case Reading.CFC_G:
                    //        {
                    //            value = 0f;
                    //        }
                    //        break;
                    //    case Reading.COMP:
                    //    case Reading.COMP_PK:
                    //        {
                    //            value = -30.0f;
                    //        }
                    //        break;
                    //    case Reading.PWR:
                    //    case Reading.REVERSE_PWR:
                    //        {
                    //            value = 0f;
                    //        }
                    //        break;
                    //    case Reading.SWR:
                    //        {
                    //            value = 1.0f;
                    //        }
                    //        break;
                    //    case Reading.ESTIMATED_PBSNR:
                    //        {
                    //            value = 0f;
                    //        }
                    //        break;
                    //}                    
                    return true;
                }
                value = 0;
                return false;
            }
            public PointF HighPoint
            {
                get { return _highPoint; }
                set { _highPoint = value; }
            }
        }
        internal class clsSignalText : clsMeterItem
        {
            public enum Units
            {
                DBM = 0,
                S_UNTS,
                U_V
            }
            public enum BarStyle
            {
                None = 0,
                Line,
                SolidFilled,
                GradientFilled,
                Segments,
            }
            private List<float> _history;
            private int _msHistoryDuration; //ms
            private int _msIgnoreHistoryDuration;
            private bool _showValue;
            private bool _showPeakValue;
            private readonly object _historyLock = new object();
            private System.Drawing.Color _colour;
            private System.Drawing.Color _markerColour;
            private System.Drawing.Color _peakValueColour;
            private bool _peakHold;
            private bool _showMarker;
            private bool _showSubMarker;
            private Units _units;

            private string _fontFamily;
            private FontStyle _fontStyle;
            private System.Drawing.Color _fontColor;
            private float _fontSize;
            private int _nIgnoringNext;

            public clsSignalText()
            {
                _history = new List<float>();
                _msHistoryDuration = 2000;
                _msIgnoreHistoryDuration = 2000;
                _showValue = true;
                _showPeakValue = true;
                _nIgnoringNext = 0;

                _colour = System.Drawing.Color.Red;
                _markerColour = System.Drawing.Color.Yellow;
                _peakValueColour = System.Drawing.Color.Red;
                _peakHold = false;
                _showMarker = true;
                _showSubMarker = true;

                _units = Units.DBM;

                _fontFamily = "Trebuchet MS";
                _fontStyle = FontStyle.Regular;
                _fontColor = System.Drawing.Color.DarkGray;
                _fontSize = 20f;

                ItemType = MeterItemType.SIGNAL_TEXT_DISPLAY;
                ReadingSource = Reading.SIGNAL_STRENGTH;
                UpdateInterval = 100;
                AttackRatio = 0.8f;
                DecayRatio = 0.2f;
                StoreSettings = false;
            }
            public override void Update(int rx, ref List<Reading> readingsUsed, Dictionary<Reading, object> all_list_item_readings = null)
            {
                // get latest reading
                float reading = MeterManager.getReading(rx, ReadingSource);

                lock (_historyLock)
                {
                    if (reading > Value)
                        Value = (reading * AttackRatio) + (Value * (1f - AttackRatio));
                    else
                        Value = (reading * DecayRatio) + (Value * (1f - DecayRatio));

                    if (_nIgnoringNext <= 0)
                    {
                        // signal history
                        _history.Add(Value); // adds to end of the list
                        int numberToRemove = _history.Count - (_msHistoryDuration / UpdateInterval);
                        // the list is sized based on delay
                        if (numberToRemove > 0) _history.RemoveRange(0, numberToRemove); // remove the oldest, the head of the list
                    }
                    else
                    {
                        _nIgnoringNext--;
                    }
                }

                // this reading has been used
                if (!readingsUsed.Contains(ReadingSource))
                    readingsUsed.Add(ReadingSource);
            }
            public bool ShowValue
            {
                get { return _showValue; }
                set { _showValue = value; }
            }
            public bool ShowPeakValue
            {
                get { return _showPeakValue; }
                set { _showPeakValue = value; }
            }
            public int IgnoreHistoryDuration
            {
                get { return _msIgnoreHistoryDuration; }
                set { _msIgnoreHistoryDuration = value; }
            }
            public int HistoryDuration
            {
                get { return _msHistoryDuration; }
                set
                {
                    _msHistoryDuration = value;
                    if (_msHistoryDuration < UpdateInterval) _msHistoryDuration = UpdateInterval;
                }
            }
            public override float MinHistory
            {
                get
                {
                    lock (_historyLock)
                    {
                        if (_history.Count == 0) return Value;
                        return _history.Min();
                    }
                }
            }
            public override float MaxHistory
            {
                get
                {
                    lock (_historyLock)
                    {
                        if (_history.Count == 0) return Value;
                        return _history.Max();
                    }
                }
            }
            public override void ClearHistory()
            {
                lock (_historyLock)
                {
                    _history.Clear();

                    _nIgnoringNext = _msIgnoreHistoryDuration / UpdateInterval;  // ignore next N readings to make up 2 second of ignore
                }
            }
            public override void History(out float minHistory, out float maxHistory)
            {
                if (_history.Count == 0)
                {
                    minHistory = Value;
                    maxHistory = Value;
                }
                else
                {
                    minHistory = _history.Min();
                    maxHistory = _history.Max();
                }
            }
            public System.Drawing.Color Colour
            {
                get { return _colour; }
                set { _colour = value; }
            }
            public System.Drawing.Color MarkerColour
            {
                get { return _markerColour; }
                set { _markerColour = value; }
            }
            public System.Drawing.Color PeakValueColour
            {
                get { return _peakValueColour; }
                set { _peakValueColour = value; }
            }
            public bool PeakHold
            {
                get { return _peakHold; }
                set { _peakHold = value; }
            }
            public bool ShowMarker
            {
                get { return _showMarker; }
                set { _showMarker = value; }
            }
            public bool ShowSubMarker
            {
                get { return _showSubMarker; }
                set { _showSubMarker = value; }
            }
            public string FontFamily
            {
                get { return _fontFamily; }
                set { _fontFamily = value; }
            }
            public FontStyle FntStyle
            {
                get { return _fontStyle; }
                set { _fontStyle = value; }
            }
            public System.Drawing.Color FontColour
            {
                get { return _fontColor; }
                set { _fontColor = value; }
            }
            public float FontSize
            {
                get { return _fontSize; }
                set { _fontSize = value; }
            }

            public override void HandleIncrement()
            {
                int n = (int)_units;
                n++;

                if (ReadingSource != Reading.ESTIMATED_PBSNR)
                {
                    if (n > (int)Units.U_V) n = (int)Units.DBM;
                }
                else
                {
                    if (n > (int)Units.S_UNTS) n = (int)Units.DBM;
                }

                _units = (Units)n;
            }
            public override void HandleDecrement()
            {
                int n = (int)_units;
                n--;
                if (n < (int)Units.DBM) n = (int)Units.S_UNTS;

                _units = (Units)n;
            }
            public Units Unit
            {
                get { return _units; }
                set
                {
                    int n = (int)value;
                    if (n < (int)Units.DBM || n > (int)Units.U_V) value = Units.DBM;
                    _units = value;
                }
            }
            public override bool ZeroOut(out float value, int rx)
            {
                //switch (ReadingSource)
                //{
                //    case Reading.SIGNAL_STRENGTH:
                //    case Reading.AVG_SIGNAL_STRENGTH:
                //        {
                //            if (IsAboveS9Frequency(rx))
                //                value = -153; //S0
                //            else
                //                value = -133; //S0
                //            return true;
                //        }
                //        break;
                //}
                if (ReadingSource == Reading.SIGNAL_STRENGTH || ReadingSource == Reading.AVG_SIGNAL_STRENGTH)
                {
                    ZeroReading(out value, rx, ReadingSource);
                    return true;
                }
                value = 0;
                return false;
            }
        }
        internal class clsNeedleItem : clsMeterItem
        {
            public enum NeedlePlacement
            {
                Bottom = 0,
                Top,
                Left,
                Right
            }
            public enum NeedleStyle
            {
                Line = 0,
                Arrow,
                Hollow
            }
            public enum NeedleDirection
            {
                Clockwise = 0,
                CounterClockwise
            }
            private List<float> _history;
            private int _msHistoryDuration; //ms
            private int _msIgnoreHistoryDuration;
            private bool _showHistory;
            private readonly object _historyLock = new object();
            private NeedleStyle _style;
            private PointF _needleOffset;
            private NeedlePlacement _placement;
            private System.Drawing.Color _colour;
            private float _strokeWidth;
            private bool _scaleStrokeWidth;
            private NeedleDirection _needleDirection;
            private float _lengthFactor;
            private bool _setup;
            private Dictionary<float, PointF> _scaleCalibration;
            private PointF _radiusRatio;
            private bool _shadow;
            private bool _peakHold;
            private System.Drawing.Color _peakHoldMarkerColour;
            private int _peakNeedleFadeIn;
            private int _nIgnoringNext;

            public clsNeedleItem()
            {
                _history = new List<float>();
                _msHistoryDuration = 500;
                _msIgnoreHistoryDuration = 2000;
                _showHistory = false;
                _placement = NeedlePlacement.Bottom;
                _needleOffset.X = 0f;
                _needleOffset.Y = 0.5f;
                _lengthFactor = 1f;
                _setup = false;
                _radiusRatio.X = 1f;
                _radiusRatio.Y = 1f;
                _style = NeedleStyle.Line;
                _colour = System.Drawing.Color.White;
                _strokeWidth = 3f;
                _scaleStrokeWidth = false;
                _needleDirection = NeedleDirection.Clockwise;
                _scaleCalibration = new Dictionary<float, PointF>();
                _shadow = true;
                _peakHold = false;
                _peakHoldMarkerColour = System.Drawing.Color.Red;
                _nIgnoringNext = 0;

                ItemType = MeterItemType.NEEDLE;
                ReadingSource = Reading.NONE;
                UpdateInterval = 100;
                AttackRatio = 0.8f;
                DecayRatio = 0.2f;
                StoreSettings = false;
                _peakNeedleFadeIn = 0;
            }           
            public override void Update(int rx, ref List<Reading> readingsUsed, Dictionary<Reading, object> all_list_item_readings = null)
            {
                // get latest reading
                //float reading = MeterManager.getReading(rx, ReadingSource);
                bool use;
                float reading;
                if (MMIOGuid == Guid.Empty)
                {
                    reading = MeterManager.getReading(rx, ReadingSource);
                    use = true;
                }
                else
                {
                    reading = 0;
                    //if (MultiMeterIO.Data.ContainsKey(GetMMIOGuid(0)))
                    if (MultiMeterIO.Data.ContainsKey(MMIOGuid))
                    {
                        //MultiMeterIO.clsMMIO mmio = MultiMeterIO.Data[GetMMIOGuid(0)];
                        MultiMeterIO.clsMMIO mmio = MultiMeterIO.Data[MMIOGuid];

                        //object val = mmio.GetVariable(GetMMIOVariable(0));
                        object val = mmio.GetVariable(MMIOVariable);
                        if (val is int)
                        {
                            int intVal = (int)val;
                            reading = (float)intVal;
                        }
                        else if (val is float)
                        {
                            reading = (float)val;
                        }
                        else if (val is double)
                        {
                            double doubleVal = (double)val;
                            reading = (float)doubleVal;
                        }
                    }
                    use = false;
                }

                lock (_historyLock)
                {
                    if (reading > Value)
                       Value = (reading * AttackRatio) + (Value * (1f - AttackRatio));
                    else
                        Value = (reading * DecayRatio) + (Value * (1f - DecayRatio));

                    if (_nIgnoringNext <= 0)
                    {
                        // signal history
                        _history.Add(Value); // adds to end of the list
                        int numberToRemove = _history.Count - (_msHistoryDuration / UpdateInterval);
                        // the list is sized based on delay
                        if (numberToRemove > 0) _history.RemoveRange(0, numberToRemove); // remove the oldest, the head of the list
                    }
                    else
                    {
                        _nIgnoringNext--;
                    }
                }

                if (use)
                {
                    // this reading has been used
                    if (!readingsUsed.Contains(ReadingSource))
                        readingsUsed.Add(ReadingSource);
                }
            }
            public float StrokeWidth
            {
                get { return _strokeWidth; }
                set { _strokeWidth = value; }
            }
            public bool ScaleStrokeWidth
            {
                get { return _scaleStrokeWidth; }
                set { _scaleStrokeWidth = value; }
            }
            public System.Drawing.Color Colour
            {
                get { return _colour; }
                set
                { _colour = value; }
            }
            public bool Setup
            {
                get { return _setup; }
                set { _setup = value; }
            }
            public NeedlePlacement Placement
            {
                get { return _placement; }
                set { _placement = value; }
            }
            public NeedleDirection Direction
            {
                get { return _needleDirection; }
                set { _needleDirection = value; }
            }
            public PointF NeedleOffset
            {
                get { return _needleOffset; }
                set { _needleOffset = value; }
            }
            public NeedleStyle Style
            {
                get { return _style; }
                set { _style = value; }
            }
            public System.Drawing.Color PeakHoldMarkerColour
            {
                get { return _peakHoldMarkerColour; }
                set { _peakHoldMarkerColour = value; }
            }
            public bool PeakHold
            {
                get { return _peakHold; }
                set { _peakHold = value; }
            }
            public override bool ShowHistory
            {
                get { return _showHistory; }
                set { _showHistory = value; }
            }
            public int IgnoreHistoryDuration
            {
                get { return _msIgnoreHistoryDuration; }
                set { _msIgnoreHistoryDuration = value; }
            }
            public int HistoryDuration
            {
                get { return _msHistoryDuration; }
                set
                {
                    _msHistoryDuration = value;
                    if (_msHistoryDuration < UpdateInterval) _msHistoryDuration = UpdateInterval;
                }
            }
            public override float MinHistory
            {
                get
                {
                    lock (_historyLock)
                    {
                        if(_history.Count == 0) return Value;
                        return _history.Min();
                    }
                }
            }
            public override float MaxHistory
            {
                get
                {
                    lock (_historyLock)
                    {
                        if (_history.Count == 0) return Value;
                        return _history.Max();
                    }
                }
            }
            public override void ClearHistory()
            {
                lock (_historyLock)
                {
                    _history.Clear();

                    _nIgnoringNext = _msIgnoreHistoryDuration / UpdateInterval;  // ignore next N readings to make up 2 second of ignore
                }
            }
            public override void History(out float minHistory, out float maxHistory)
            {
                lock (_historyLock)
                {
                    if (_history.Count == 0)
                    {
                        minHistory = Value;
                        maxHistory = Value;
                    }
                    else
                    {
                        minHistory = _history.Min();
                        maxHistory = _history.Max();
                    }
                }
            }
            public float LengthFactor
            {
                get { return _lengthFactor; }
                set { _lengthFactor = value; }
            }
            public override Dictionary<float, PointF> ScaleCalibration
            {
                get { return _scaleCalibration; }
                set { }
            }
            public PointF RadiusRatio
            {
                get { return _radiusRatio; }
                set { _radiusRatio = value; }
            }
            public bool Shadow
            {
                get { return _shadow; }
                set { _shadow = value; }
            }
            public int PeakNeedleShadowFade
            {
                get { return _peakNeedleFadeIn; }
                set {
                    _peakNeedleFadeIn = value;
                    if (_peakNeedleFadeIn > 12) _peakNeedleFadeIn = 12;
                    if (_peakNeedleFadeIn < 0) _peakNeedleFadeIn = 0;
                }
            }
            public override bool ZeroOut(out float value, int rx)
            {
                if(_scaleCalibration != null || _scaleCalibration.Count > 0)
                {
                    value = _scaleCalibration.OrderBy(p => p.Key).First().Key;
                    ZeroReading(out value, rx, ReadingSource);
                    //switch (ReadingSource)
                    //{
                    //    case Reading.SIGNAL_STRENGTH:
                    //    case Reading.AVG_SIGNAL_STRENGTH:
                    //        {
                    //            if (IsAboveS9Frequency(rx))
                    //                value = -153; //S0
                    //            else
                    //                value = -133; //S0

                    //        }
                    //        break;
                    //    case Reading.PWR:
                    //    case Reading.REVERSE_PWR:
                    //        {
                    //            value = 0f;
                    //        }
                    //        break;
                    //    case Reading.VOLTS:
                    //        {
                    //            value = 10f;
                    //        }
                    //        break;
                    //    case Reading.AMPS:
                    //        {
                    //            value = 10f;
                    //        }
                    //        break;
                    //    case Reading.SWR:
                    //        {
                    //            value = 1.0f;
                    //        }
                    //        break;
                    //    case Reading.ALC_G: //alc comp
                    //        {
                    //            value = 0f;
                    //        }
                    //        break;
                    //    case Reading.ALC_GROUP:
                    //        {
                    //            value = -30.0f;
                    //        }
                    //        break;
                    //}
                    return true;
                }
                value = 0;
                return false;
            }
        }
        internal class clsText : clsMeterItem
        {
            private string _sText;
            private string _fontFamily;
            private FontStyle _fontStyle;
            private System.Drawing.Color _color;
            private bool _fixedSize;
            private float _fontSize;
            public clsText()
            {
                _sText = "";
                _fontFamily = "Trebuchet MS";
                _fontStyle = FontStyle.Regular;
                _color = System.Drawing.Color.White;
                _fixedSize = false;
                _fontSize = 9f;
                _readingText = "";

                ItemType = MeterItemType.TEXT;
                ReadingSource = Reading.NONE;
                UpdateInterval = int.MaxValue; // very slow update
                StoreSettings = false;
            }            
            private void updateReadingText(float reading)
            {
                switch (_sText)
                {
                    case "%valueWithType%":
                        {
                            _readingText = reading.ToString("0.0") + MeterManager.ReadingUnits(ReadingSource);
                        }
                        break;
                    case "%value%":
                        {
                            _readingText = reading.ToString("0.0");
                        }
                        break;
                    default:
                        _readingText = "";
                        break;
                }
            }
            public override void Update(int rx, ref List<Reading> readingsUsed, Dictionary<Reading, object> all_list_item_readings = null)
            {
                // get latest reading
                float reading = MeterManager.getReading(rx, ReadingSource);

                updateReadingText(reading);

                // this reading has been used
                if (!readingsUsed.Contains(ReadingSource))
                    readingsUsed.Add(ReadingSource);
            }
            private string _readingText;
            public string Text
            {
                get
                {
                    string sText = _sText;
                    switch(_sText)
                    { 
                        case "%valueWithType%":
                        case "%value%":
                            sText = _readingText;
                        break; 
                    }

                    return sText; 
                }
                set { _sText = value; }
            }
            public string FontFamily
            {
                get { return _fontFamily; }
                set { _fontFamily = value; }
            }
            public FontStyle Style
            {
                get { return _fontStyle; }
                set { _fontStyle = value; }
            }
            public System.Drawing.Color Colour
            {
                get { return _color; }
                set { _color = value; }
            }
            public bool FixedSize
            {
                get { return _fixedSize; }
                set { _fixedSize = value; }
            }
            public float FontSize
            {
                get { return _fontSize; }
                set { _fontSize = value; }
            }
            public override float Value
            {
                get { return base.Value; }
                set { 
                    base.Value = value;
                    updateReadingText(value);
                }
            }
        }
        //internal class clsSpectrum : clsMeterItem
        //{
        //    private System.Drawing.Color _color;

        //    public clsSpectrum()
        //    {
        //        ItemType = MeterItemType.SPECTRUM;
        //        ReadingSource = Reading.NONE;
        //        UpdateInterval = 50;
        //        StoreSettings = false;
        //    }
        //}

        #endregion
        #region clsMeterItems
        public class clsMeter
        {
            // the single meter, that may contain many bars/labels/analog/images/etc
            private string _sId;
            private string _name;            
            private int _rx;
            private bool _enabled;

            private float _XRatio; // 0-1
            private float _YRatio; // 0-1

            private Dictionary<string, clsMeterItem> _meterItems;
            //private Dictionary<string, clsMeterItem> _sortedMeterItemsForZOrder;
            private List<clsMeterItem> _sortedMeterItemsForZOrder;
            private int _displayGroup;
            private List<string> _displayGroups;
            private bool _mox;
            private float _fPadX = 0.02f;
            private float _fPadY = 0.05f;
            private float _fHeight = 0.05f;

            private bool _split;
            private double _vfoA;
            private double _vfoB;
            private double _vfoSub;
            private bool _txVfoB;
            private DSPMode _modeVfoA;
            private DSPMode _modeVfoB;
            private Band _bandVfoA;
            private Band _bandVfoB;
            private Band _bandVfoASub;
            private string _filterVfoAname;
            private string _filterVfoBname;
            private bool _rx2Enabled;
            private bool _multiRxEnabled;
            private DateTime _qso_start;
            private DateTime _qso_end;

            private bool _txeqEnabled;
            private bool _levelerEnabled;
            private bool _cfcEnabled;
            private bool _compandEnabled;

            private bool _quickSplitEnabled;

            private Filter _filterVfoA;
            private Filter _filterVfoB;

            private int _quickestRXUpdate;
            private int _quickestTXUpdate;
            internal readonly object _meterItemsLock = new object();

            private void addMeterItem(clsMeterItem mi)
            {
                lock (_meterItemsLock)
                {
                    _meterItems.Add(mi.ID, mi);
                }
            }
            public Reading MeterVariablesReading(MeterType meter, int variable_index)
            {
                switch (meter)
                {
                    case MeterType.SIGNAL_STRENGTH: return Reading.SIGNAL_STRENGTH;
                    case MeterType.AVG_SIGNAL_STRENGTH: return Reading.AVG_SIGNAL_STRENGTH;
                    case MeterType.SIGNAL_TEXT: return Reading.SIGNAL_STRENGTH;
                    case MeterType.ADC: return variable_index == 0 ? Reading.ADC_PK : Reading.ADC_AV;
                    case MeterType.AGC: return variable_index == 0 ? Reading.AGC_PK : Reading.AGC_AV;
                    case MeterType.AGC_GAIN: return Reading.AGC_GAIN;
                    case MeterType.MIC: return variable_index == 0 ? Reading.MIC : Reading.MIC_PK;
                    case MeterType.PWR: return Reading.PWR;
                    case MeterType.REVERSE_PWR: return Reading.REVERSE_PWR;
                    case MeterType.ALC: return variable_index == 0 ? Reading.ALC : Reading.ALC_PK;
                    case MeterType.EQ: return variable_index == 0 ? Reading.EQ : Reading.EQ_PK;
                    case MeterType.LEVELER: return variable_index == 0 ? Reading.LEVELER : Reading.LEVELER_PK;
                    case MeterType.COMP: return variable_index == 0 ? Reading.COMP : Reading.COMP_PK;
                    //case MeterType.CPDR: return variable_index == 0 ? Reading.CPDR : Reading.CPDR_PK; //CPDR is the same as comp
                    //case MeterType.CPDR: break;
                    case MeterType.ALC_GAIN: return Reading.ALC_G;
                    case MeterType.ALC_GROUP: return Reading.ALC_GROUP;
                    case MeterType.LEVELER_GAIN: return Reading.LVL_G;
                    case MeterType.CFC: return variable_index == 0 ? Reading.CFC_AV : Reading.CFC_PK;
                    case MeterType.CFC_GAIN: return Reading.CFC_G;
                    case MeterType.MAGIC_EYE: return Reading.SIGNAL_STRENGTH;
                    case MeterType.ESTIMATED_PBSNR: return Reading.ESTIMATED_PBSNR;
                    //TODO !!!case MeterType.ANANMM: return 7;
                    case MeterType.CROSS: return variable_index == 0 ? Reading.PWR : Reading.REVERSE_PWR;
                    case MeterType.SWR: return Reading.SWR;
                    //case MeterType.HISTORY: AddHistory(nDelay, 0, out bBottom, restoreIg); break;
                    case MeterType.VFO_DISPLAY: return Reading.NONE;
                    case MeterType.CLOCK: return Reading.NONE;
                    case MeterType.SPACER: return Reading.NONE;
                    case MeterType.TEXT_OVERLAY: return Reading.NONE;
                    case MeterType.LED: return Reading.NONE;
                    case MeterType.WEB_IMAGE: return Reading.NONE;
                    case MeterType.DATA_OUT: return Reading.NONE;
                    case MeterType.ROTATOR: return variable_index == 0 ? Reading.AZ : Reading.ELE;
                        //case MeterType.SPECTRUM: AddSpectrum(nDelay, 0, out bBottom, restoreIg); break;
                }
                return Reading.NONE;
            }
            public int MeterVariables(MeterType meter)
            {
                switch (meter)
                {
                    case MeterType.SIGNAL_STRENGTH: return 1;
                    case MeterType.AVG_SIGNAL_STRENGTH: return 1;
                    case MeterType.SIGNAL_TEXT: return 1;
                    case MeterType.ADC: return 2;
                    case MeterType.AGC: return 2;
                    case MeterType.AGC_GAIN: return 1;
                    case MeterType.MIC: return 2;
                    case MeterType.PWR: return 1;
                    case MeterType.REVERSE_PWR: return 1;
                    case MeterType.ALC: return 2;
                    case MeterType.EQ: return 2;
                    case MeterType.LEVELER: return 2;
                    case MeterType.COMP: return 2;
                    //case MeterType.CPDR: break;
                    case MeterType.ALC_GAIN: return 1;
                    case MeterType.ALC_GROUP: return 1;
                    case MeterType.LEVELER_GAIN: return 1;
                    case MeterType.CFC: return 2;
                    case MeterType.CFC_GAIN: return 1;
                    case MeterType.MAGIC_EYE: return 1;
                    case MeterType.ESTIMATED_PBSNR: return 1;
                    case MeterType.ANANMM: return 7;
                    case MeterType.CROSS: return 2;
                    case MeterType.SWR: return 1;
                    //case MeterType.HISTORY: AddHistory(nDelay, 0, out bBottom, restoreIg); break;
                    case MeterType.VFO_DISPLAY: return 0;
                    case MeterType.CLOCK: return 0;
                    case MeterType.SPACER: return 0;
                    case MeterType.TEXT_OVERLAY: return 0;
                    case MeterType.LED: return 0;
                    case MeterType.WEB_IMAGE: return 0;
                    case MeterType.DATA_OUT: return 0;
                    case MeterType.ROTATOR: return 2;
                    //case MeterType.SPECTRUM: AddSpectrum(nDelay, 0, out bBottom, restoreIg); break;
                }
                return 0;
            }
            public void AddMeter(MeterType meter, clsItemGroup restoreIg = null)
            {
                //restoreIg is passed in when used by restoreSettings, so that the item group can be ordered
                //correctly and the ID copied over
                float bBottom = 0;
                int nDelay = 50;

                switch (meter)
                {
                    case MeterType.SIGNAL_STRENGTH: AddSMeterBarSignal(nDelay, 0, out bBottom, restoreIg); break;
                    case MeterType.AVG_SIGNAL_STRENGTH: AddSMeterBarSignalAvg(nDelay, 0, out bBottom, restoreIg); break;
                    case MeterType.SIGNAL_TEXT: AddSMeterBarText(nDelay, 0, out bBottom, restoreIg); break;
                    case MeterType.ADC: AddADCBar(nDelay, 0, out bBottom, restoreIg); break;
                    case MeterType.AGC: AddAGCBar(nDelay, 0, out bBottom, restoreIg); break;
                    case MeterType.AGC_GAIN: AddAGCGainBar(nDelay, 0, out bBottom, restoreIg); break;
                    case MeterType.MIC: AddMicBar(nDelay, 0, out bBottom, restoreIg); break;
                    case MeterType.PWR: AddPWRBar(nDelay, 0, out bBottom, restoreIg); break;
                    case MeterType.REVERSE_PWR: AddREVPWRBar(nDelay, 0, out bBottom, restoreIg); break;
                    case MeterType.ALC: AddALCBar(nDelay, 0, out bBottom, restoreIg); break;
                    case MeterType.EQ: AddEQBar(nDelay, 0, out bBottom, restoreIg); break;
                    case MeterType.LEVELER: AddLevelerBar(nDelay, 0, out bBottom, restoreIg); break;
                    case MeterType.COMP: AddCompBar(nDelay, 0, out bBottom, restoreIg); break;
                    //case MeterType.CPDR: AddCompanderBar(nDelay, 0, out bBottom, restoreIg); break;//CPDR is the same as comp
                    case MeterType.ALC_GAIN: AddALCGainBar(nDelay, 0, out bBottom, restoreIg); break;
                    case MeterType.ALC_GROUP: AddALCGroupBar(nDelay, 0, out bBottom, restoreIg); break;
                    case MeterType.LEVELER_GAIN: AddLevelerGainBar(nDelay, 0, out bBottom, restoreIg); break;
                    case MeterType.CFC: AddCFCBar(nDelay, 0, out bBottom, restoreIg); break;
                    case MeterType.CFC_GAIN: AddCFCGainBar(nDelay, 0, out bBottom, restoreIg); break;
                    case MeterType.MAGIC_EYE: AddMagicEye(nDelay, 0, out bBottom, 0.2f, restoreIg); break;
                    case MeterType.ESTIMATED_PBSNR: AddPBSNRBar(nDelay, 0, out bBottom, restoreIg); break;
                    case MeterType.ANANMM: AddAnanMM(nDelay, 0, out bBottom, restoreIg); break;
                    case MeterType.CROSS: AddCrossNeedle(nDelay, 0, out bBottom, restoreIg); break;
                    case MeterType.SWR: AddSWRBar(nDelay, 0, out bBottom, restoreIg); break;
                    //case MeterType.HISTORY: AddHistory(nDelay, 0, out bBottom, restoreIg); break;
                    case MeterType.VFO_DISPLAY: AddVFODisplay(nDelay, 0, out bBottom, restoreIg); break;
                    case MeterType.CLOCK: AddClock(nDelay, 0, out bBottom, restoreIg); break;
                    case MeterType.SPACER: AddSpacer(nDelay, 0, out bBottom, 0.1f, restoreIg); break;
                    case MeterType.TEXT_OVERLAY: AddTextOverlay(nDelay, 0, out bBottom, 0.1f, restoreIg); break;
                    case MeterType.LED: AddLed(nDelay, 0, out bBottom, 0.1f, restoreIg); break;
                    case MeterType.WEB_IMAGE: AddWebImage(nDelay, 0, out bBottom, 0.1f, restoreIg); break;
                    case MeterType.DATA_OUT: AddDataOut(nDelay, 0, out bBottom, restoreIg); break;
                    case MeterType.ROTATOR: AddRotator(nDelay, 0, out bBottom, restoreIg); break;
                        //case MeterType.SPECTRUM: AddSpectrum(nDelay, 0, out bBottom, restoreIg); break;
                }

                // update state of items
                switch (meter)
                {
                    case MeterType.EQ:
                        OnEQChanged(TXEQEnabled, TXEQEnabled);
                        break;
                    case MeterType.LEVELER:
                    case MeterType.LEVELER_GAIN:
                        OnLevelerChanged(LevelerEnabled, LevelerEnabled);
                        break;
                    case MeterType.CFC:
                    case MeterType.CFC_GAIN:
                        OnCFCChanged(CFCEnabled, CFCEnabled);
                        break;
                    case MeterType.COMP:
                        OnCompandChanged(CompandEnabled, CompandEnabled);
                        break;
                }
            }
            #region meterDefs
            public string AddSMeterBarSignal(int nMSupdate, float fTop, out float fBottom, clsItemGroup restoreIg = null)
            { 
                return addSMeterBar(nMSupdate, Reading.SIGNAL_STRENGTH, fTop, out fBottom, restoreIg);
            }
            public string AddSMeterBarSignalAvg(int nMSupdate, float fTop, out float fBottom, clsItemGroup restoreIg = null)
            {
                return addSMeterBar(nMSupdate, Reading.AVG_SIGNAL_STRENGTH, fTop, out fBottom, restoreIg);
            }
            private clsFadeCover getFadeCover(string sId)
            {
                System.Drawing.RectangleF boundRect = getBounds(sId);
                if(boundRect.IsEmpty) return null;

                clsFadeCover fc = new clsFadeCover();
                fc.TopLeft = new PointF(boundRect.X, boundRect.Y);
                fc.Size = new SizeF(boundRect.Width, boundRect.Height);
                fc.ParentID = sId;

                return fc;
            }
            private string addSMeterBar(int nMSupdate, Reading reading, float fTop, out float fBottom,  clsItemGroup restoreIg = null)
            {
                clsItemGroup ig = new clsItemGroup();
                if(restoreIg != null) ig.ID = restoreIg.ID;
                ig.ParentID = ID;

                clsBarItem cb = new clsBarItem();
                cb.ParentID = ig.ID;
                cb.Primary = true;
                cb.TopLeft = new PointF(_fPadX, fTop + _fPadY);
                cb.Size = new SizeF(1f - _fPadX * 2f, _fHeight);
                cb.ReadingSource = reading;
                cb.MMIOVariableIndex = 0;
                cb.AttackRatio = 0.8f;
                cb.DecayRatio = 0.2f;
                cb.UpdateInterval = nMSupdate;
                cb.HistoryDuration = 4000;
                cb.ShowHistory = true;
                cb.ShowValue = true;
                cb.Colour = System.Drawing.Color.CadetBlue;
                cb.ShowMarker = true;
                cb.PeakHoldMarkerColour = System.Drawing.Color.Red;
                cb.HistoryColour = System.Drawing.Color.FromArgb(128, System.Drawing.Color.Red);
                cb.Style = clsBarItem.BarStyle.Line;
                cb.ScaleCalibration.Add(-133, new PointF(0, 0)); // position for S0 or below  // -133 is the edge, as S0 (-127) is the first small tick
                cb.ScaleCalibration.Add(-73, new PointF(0.5f, 0)); // position for S9
                cb.ScaleCalibration.Add(-13, new PointF(0.99f, 0)); // position for S9+60dB or above
                cb.FontColour = System.Drawing.Color.Yellow;
                cb.Value = cb.ScaleCalibration.OrderBy(p => p.Key).First().Key;
                if (IsAboveS9Frequency(_rx)) cb.Value -= 20;
                cb.ZOrder = 2;
                cb.HighPoint = cb.ScaleCalibration.OrderBy(p => p.Key).ElementAt(1).Value;
                addMeterItem(cb);

                clsScaleItem cs = new clsScaleItem();
                cs.ParentID = ig.ID;
                cs.TopLeft = cb.TopLeft;
                cs.Size = cb.Size;
                cs.ReadingSource = cb.ReadingSource;
                cs.ZOrder = 3;
                cs.ShowType = true;
                addMeterItem(cs);

                clsSolidColour sc;
                sc = new clsSolidColour();
                sc.ParentID = ig.ID;
                sc.TopLeft = new PointF(cb.TopLeft.X, cb.TopLeft.Y - _fHeight * 0.75f);
                sc.Size = new SizeF(cb.Size.Width, _fHeight + _fHeight * 0.75f);
                sc.Colour = System.Drawing.Color.FromArgb(32, 32, 32);
                sc.ZOrder = 1;
                addMeterItem(sc);

                clsClickBox clb = new clsClickBox();
                clb.ParentID = ig.ID;
                clb.TopLeft = sc.TopLeft;
                clb.Size = sc.Size;
                clb.ChangesGroup = false;
                clb.PerformsIncDec = true;
                clb.RelatedMeterItem = cb;
                addMeterItem(clb);

                fBottom = cb.TopLeft.Y + cb.Size.Height;

                ig.TopLeft = cb.TopLeft;
                ig.Size = new SizeF(cb.Size.Width, fBottom);
                if (reading == Reading.SIGNAL_STRENGTH)
                    ig.MeterType = MeterType.SIGNAL_STRENGTH;
                else
                    ig.MeterType = MeterType.AVG_SIGNAL_STRENGTH;
                //lock (_meterItemLock)
                //{
                    ig.Order = restoreIg == null ? numberOfMeterGroups() : restoreIg.Order;
                //}

                clsFadeCover fc = getFadeCover(ig.ID);
                if(fc != null) addMeterItem(fc);

                addMeterItem(ig);

                return cb.ID;
            }            
            private string AddSMeterBarText(int nMSupdate, float fTop, out float fBottom, clsItemGroup restoreIg = null)
            {
                clsItemGroup ig = new clsItemGroup();
                if (restoreIg != null) ig.ID = restoreIg.ID;
                ig.ParentID = ID;
                
                clsSolidColour sc;
                sc = new clsSolidColour();
                sc.ParentID = ig.ID;
                sc.TopLeft = new PointF(_fPadX, fTop + _fPadY - _fHeight * 0.75f);
                sc.Size = new SizeF(1f - _fPadX * 2f, (_fHeight + _fHeight * 0.75f) * 1.5f);
                sc.Colour = System.Drawing.Color.FromArgb(32, 32, 32);
                sc.ZOrder = 1;
                addMeterItem(sc);

                clsSignalText cst = new clsSignalText();
                cst.ParentID = ig.ID;
                cst.Primary = true;
                cst.TopLeft = sc.TopLeft;
                cst.Size = sc.Size;
                cst.ReadingSource = Reading.AVG_SIGNAL_STRENGTH;
                cst.MMIOVariableIndex = 0;
                cst.AttackRatio = 0.8f;
                cst.DecayRatio = 0.2f;
                cst.UpdateInterval = nMSupdate;
                cst.HistoryDuration = 4000;
                cst.ShowHistory = false;
                cst.HistoryColour = System.Drawing.Color.Gray; // sub values uv/dbm
                cst.ShowValue = true;
                cst.ShowPeakValue = true;
                cst.FontColour = System.Drawing.Color.Yellow;
                cst.FontSize = 56f;
                cst.ZOrder = 2;
                cst.Value = -133; //S0
                addMeterItem(cst);

                //clsClickBox clb = new clsClickBox();
                //clb.ParentID = ig.ID;
                //clb.TopLeft = sc.TopLeft;
                //clb.Size = sc.Size;
                //clb.ChangesGroup = false;
                //clb.PerformsIncDec = true;
                //clb.RelatedMeterItem = cst;
                //addMeterItem(clb);

                fBottom = sc.TopLeft.Y + sc.Size.Height;

                ig.TopLeft = sc.TopLeft;
                ig.Size = new SizeF(sc.Size.Width, fBottom);
                ig.MeterType = MeterType.SIGNAL_TEXT;
                //lock (_meterItemLock)
                //{
                    ig.Order = restoreIg == null ? numberOfMeterGroups() : restoreIg.Order;
                //}
                clsFadeCover fc = getFadeCover(ig.ID);
                if (fc != null) addMeterItem(fc);
                
                addMeterItem(ig);

                return cst.ID;
            }
            
            public string AddADCBar(int nMSupdate, float fTop, out float fBottom,  clsItemGroup restoreIg = null)
            {
                clsItemGroup ig = new clsItemGroup();
                if(restoreIg != null) ig.ID = restoreIg.ID;
                ig.ParentID = ID;

                clsBarItem cb = new clsBarItem();
                cb.ParentID = ig.ID;
                cb.Primary = true;
                cb.TopLeft = new PointF(_fPadX, fTop + _fPadY);
                cb.Size = new SizeF(1f - _fPadX * 2f, _fHeight);
                cb.ReadingSource = Reading.ADC_PK;
                cb.MMIOVariableIndex = 0;
                cb.AttackRatio = 0.2f;
                cb.DecayRatio = 0.05f;
                cb.UpdateInterval = nMSupdate;
                cb.HistoryDuration = 4000;
                cb.ShowHistory = true;
                cb.MarkerColour = System.Drawing.Color.Orange;
                cb.HistoryColour = System.Drawing.Color.FromArgb(128, System.Drawing.Color.CornflowerBlue);
                cb.Style = clsBarItem.BarStyle.Line;
                cb.ScaleCalibration.Add(-120, new PointF(0, 0));
                cb.ScaleCalibration.Add(-20, new PointF(0.8333f, 0));
                cb.ScaleCalibration.Add(0, new PointF(0.99f, 0));
                cb.FontColour = System.Drawing.Color.Yellow;
                cb.ZOrder = 2;
                cb.Value = cb.ScaleCalibration.OrderBy(p => p.Key).First().Key;
                cb.HighPoint = cb.ScaleCalibration.OrderBy(p => p.Key).ElementAt(1).Value;
                addMeterItem(cb);

                //avg on top
                clsBarItem cb2 = new clsBarItem();
                cb2.ParentID = ig.ID;
                cb2.TopLeft = cb.TopLeft;
                cb2.Size = cb.Size;
                cb2.ReadingSource = Reading.ADC_AV;
                cb2.MMIOVariableIndex = 1;
                cb2.ShowPeakValue = false;
                cb2.AttackRatio = 0.2f;
                cb2.DecayRatio = 0.05f;
                cb2.UpdateInterval = nMSupdate;
                cb2.HistoryDuration = 0;
                cb2.ShowHistory = false;
                cb2.MarkerColour = System.Drawing.Color.DarkGray;
                cb2.HistoryColour = System.Drawing.Color.FromArgb(128, System.Drawing.Color.CornflowerBlue);
                cb2.Style = clsBarItem.BarStyle.Line;
                cb2.ScaleCalibration.Add(-120, new PointF(0, 0));
                cb2.ScaleCalibration.Add(-20, new PointF(0.8333f, 0));
                cb2.ScaleCalibration.Add(0, new PointF(0.99f, 0));
                cb2.FontColour = System.Drawing.Color.Yellow;
                cb2.ShowValue = false;
                cb2.ZOrder = 3;
                cb2.Value = cb2.ScaleCalibration.OrderBy(p => p.Key).First().Key;
                cb2.HighPoint = cb2.ScaleCalibration.OrderBy(p => p.Key).ElementAt(1).Value;
                cb2.PostDrawItem = cb;
                addMeterItem(cb2);

                clsScaleItem cs = new clsScaleItem();
                cs.ParentID = ig.ID;
                cs.TopLeft = cb.TopLeft;
                cs.Size = cb.Size;
                cs.ReadingSource = cb.ReadingSource;
                cs.ZOrder = 4;
                cs.ShowType = true;
                addMeterItem(cs);

                clsSolidColour sc = new clsSolidColour();
                sc.ParentID = ig.ID;
                sc.TopLeft = new PointF(cb.TopLeft.X, cb.TopLeft.Y - _fHeight * 0.75f);
                sc.Size = new SizeF(cb.Size.Width, _fHeight + _fHeight * 0.75f);
                sc.Colour = System.Drawing.Color.FromArgb(32, 32, 32);
                sc.ZOrder = 1;
                addMeterItem(sc);

                fBottom = cb.TopLeft.Y + cb.Size.Height;

                ig.TopLeft = cb.TopLeft;
                ig.Size = new SizeF(cb.Size.Width, fBottom);
                ig.MeterType = MeterType.ADC;
                ig.Order = restoreIg == null ? numberOfMeterGroups() : restoreIg.Order;

                clsFadeCover fc = getFadeCover(ig.ID);
                if (fc != null) addMeterItem(fc);
                
                addMeterItem(ig);

                return cb.ID;
            }
            public string AddPBSNRBar(int nMSupdate, float fTop, out float fBottom,  clsItemGroup restoreIg = null)
            {
                clsItemGroup ig = new clsItemGroup();
                if(restoreIg != null) ig.ID = restoreIg.ID;
                ig.ParentID = ID;

                clsBarItem cb = new clsBarItem();
                cb.ParentID = ig.ID;
                cb.Primary = true;
                cb.TopLeft = new PointF(_fPadX, fTop + _fPadY);
                cb.Size = new SizeF(1f - _fPadX * 2f, _fHeight);
                cb.ReadingSource = Reading.ESTIMATED_PBSNR;
                cb.MMIOVariableIndex = 0;
                cb.AttackRatio = 0.2f;
                cb.DecayRatio = 0.05f;
                cb.UpdateInterval = nMSupdate;
                cb.HistoryDuration = 4000;
                cb.ShowHistory = false;
                cb.PeakHold = true;
                cb.Colour = System.Drawing.Color.DarkCyan;
                cb.HistoryColour = System.Drawing.Color.FromArgb(128, System.Drawing.Color.Violet);
                cb.Style = clsBarItem.BarStyle.Segments;
                cb.ScaleCalibration.Add(0, new PointF(0, 0));
                cb.ScaleCalibration.Add(50, new PointF(0.8333f, 0));
                cb.ScaleCalibration.Add(60, new PointF(0.99f, 0));
                cb.FontColour = System.Drawing.Color.Yellow;
                cb.ZOrder = 2;
                cb.Value = cb.ScaleCalibration.OrderBy(p => p.Key).First().Key;
                cb.HighPoint = cb.ScaleCalibration.OrderBy(p => p.Key).ElementAt(1).Value;
                addMeterItem(cb);

                clsScaleItem cs = new clsScaleItem();
                cs.ParentID = ig.ID;
                cs.TopLeft = cb.TopLeft;
                cs.Size = cb.Size;
                cs.ReadingSource = cb.ReadingSource;
                cs.ZOrder = 3;
                cs.ShowType = true;
                addMeterItem(cs);

                clsSolidColour sc = new clsSolidColour();
                sc.ParentID = ig.ID;
                sc.TopLeft = new PointF(cb.TopLeft.X, cb.TopLeft.Y - _fHeight * 0.75f);
                sc.Size = new SizeF(cb.Size.Width, _fHeight + _fHeight * 0.75f);
                sc.Colour = System.Drawing.Color.FromArgb(32, 32, 32);
                sc.ZOrder = 1;
                addMeterItem(sc);

                clsClickBox clb = new clsClickBox();
                clb.ParentID = ig.ID;
                clb.TopLeft = sc.TopLeft;
                clb.Size = sc.Size;
                clb.ChangesGroup = false;
                clb.PerformsIncDec = true;
                clb.RelatedMeterItem = cb;
                addMeterItem(clb);

                fBottom = cb.TopLeft.Y + cb.Size.Height;

                ig.TopLeft = cb.TopLeft;
                ig.Size = new SizeF(cb.Size.Width, fBottom);
                ig.MeterType = MeterType.ESTIMATED_PBSNR;
                ig.Order = restoreIg == null ? numberOfMeterGroups() : restoreIg.Order;

                clsFadeCover fc = getFadeCover(ig.ID);
                if (fc != null) addMeterItem(fc);
                
                addMeterItem(ig);

                return cb.ID;
            }
            public string AddAGCGainBar(int nMSupdate, float fTop, out float fBottom,  clsItemGroup restoreIg = null)
            {
                clsItemGroup ig = new clsItemGroup();
                if(restoreIg != null) ig.ID = restoreIg.ID;
                ig.ParentID = ID;

                clsBarItem cb = new clsBarItem();
                cb.ParentID = ig.ID;
                cb.Primary = true;
                cb.TopLeft = new PointF(_fPadX, fTop + _fPadY);
                cb.Size = new SizeF(1f - _fPadX * 2f, _fHeight);
                cb.ReadingSource = Reading.AGC_GAIN;
                cb.MMIOVariableIndex = 0;
                cb.AttackRatio = 0.2f;
                cb.DecayRatio = 0.05f;
                cb.UpdateInterval = nMSupdate;
                cb.HistoryDuration = 4000;
                cb.ShowHistory = true;
                cb.PeakHold = false;
                cb.Colour = System.Drawing.Color.DarkCyan;
                cb.HistoryColour = System.Drawing.Color.FromArgb(128, System.Drawing.Color.Violet);
                cb.Style = clsBarItem.BarStyle.Line;
                cb.ScaleCalibration.Add(-50, new PointF(0, 0));
                cb.ScaleCalibration.Add(100, new PointF(0.857f, 0));
                cb.ScaleCalibration.Add(125, new PointF(0.99f, 0));
                cb.FontColour = System.Drawing.Color.Yellow;
                cb.ZOrder = 2;
                cb.Value = cb.ScaleCalibration.OrderBy(p => p.Key).First().Key;
                cb.HighPoint = cb.ScaleCalibration.OrderBy(p => p.Key).ElementAt(1).Value;
                addMeterItem(cb);

                clsScaleItem cs = new clsScaleItem();
                cs.ParentID = ig.ID;
                cs.TopLeft = cb.TopLeft;
                cs.Size = cb.Size;
                cs.ReadingSource = cb.ReadingSource;
                cs.ZOrder = 3;
                cs.ShowType = true;
                addMeterItem(cs);

                clsSolidColour sc = new clsSolidColour();
                sc.ParentID = ig.ID;
                sc.TopLeft = new PointF(cb.TopLeft.X, cb.TopLeft.Y - _fHeight * 0.75f);
                sc.Size = new SizeF(cb.Size.Width, _fHeight + _fHeight * 0.75f);
                sc.Colour = System.Drawing.Color.FromArgb(32, 32, 32);
                sc.ZOrder = 1;
                addMeterItem(sc);

                fBottom = cb.TopLeft.Y + cb.Size.Height;

                ig.TopLeft = cb.TopLeft;
                ig.Size = new SizeF(cb.Size.Width, fBottom);
                ig.MeterType = MeterType.AGC_GAIN;
                ig.Order = restoreIg == null ? numberOfMeterGroups() : restoreIg.Order;

                clsFadeCover fc = getFadeCover(ig.ID);
                if (fc != null) addMeterItem(fc);
                
                addMeterItem(ig);

                return cb.ID;
            }
            public string AddAGCBar(int nMSupdate, float fTop, out float fBottom,  clsItemGroup restoreIg = null)
            {
                clsItemGroup ig = new clsItemGroup();
                if(restoreIg != null) ig.ID = restoreIg.ID;
                ig.ParentID = ID;

                clsBarItem cb = new clsBarItem();
                cb.ParentID = ig.ID;
                cb.Primary = true;
                cb.TopLeft = new PointF(_fPadX, fTop + _fPadY);
                cb.Size = new SizeF(1f - _fPadX * 2f, _fHeight);
                cb.ReadingSource = Reading.AGC_PK;
                cb.MMIOVariableIndex = 0;
                cb.AttackRatio = 0.2f;
                cb.DecayRatio = 0.05f;
                cb.UpdateInterval = nMSupdate;
                cb.HistoryDuration = 4000;
                cb.ShowHistory = true;
                cb.PeakHold = false;
                cb.Colour = System.Drawing.Color.DarkCyan;
                cb.HistoryColour = System.Drawing.Color.FromArgb(128, System.Drawing.Color.Violet);
                cb.Style = clsBarItem.BarStyle.Line;
                cb.ScaleCalibration.Add(-125, new PointF(0, 0));
                cb.ScaleCalibration.Add(0, new PointF(0.5f, 0));
                cb.ScaleCalibration.Add(125, new PointF(0.99f, 0));
                cb.FontColour = System.Drawing.Color.Yellow;
                cb.ZOrder = 2;
                cb.Value = cb.ScaleCalibration.OrderBy(p => p.Key).First().Key;
                cb.HighPoint = cb.ScaleCalibration.OrderBy(p => p.Key).ElementAt(1).Value;
                addMeterItem(cb);

                clsBarItem cb2 = new clsBarItem();
                cb2.ParentID = ig.ID;
                cb2.TopLeft = cb.TopLeft;
                cb2.Size = cb.Size;
                cb2.ReadingSource = Reading.AGC_AV;
                cb2.MMIOVariableIndex = 1;
                cb2.ShowPeakValue = false;
                cb2.AttackRatio = 0.2f;
                cb2.DecayRatio = 0.05f;
                cb2.UpdateInterval = nMSupdate;
                cb2.HistoryDuration = 0;
                cb2.ShowHistory = false;
                cb2.PeakHold = false;
                cb2.Colour = System.Drawing.Color.DarkCyan;
                cb2.HistoryColour = System.Drawing.Color.FromArgb(128, System.Drawing.Color.Violet);
                cb2.MarkerColour = System.Drawing.Color.DarkGray;
                cb2.Style = clsBarItem.BarStyle.Line;
                cb2.ScaleCalibration.Add(-125, new PointF(0, 0));
                cb2.ScaleCalibration.Add(0, new PointF(0.5f, 0));
                cb2.ScaleCalibration.Add(125, new PointF(0.99f, 0));
                cb2.FontColour = System.Drawing.Color.Yellow;
                cb2.ShowValue = false;
                cb2.ZOrder = 3;
                cb2.Value = cb2.ScaleCalibration.OrderBy(p => p.Key).First().Key;
                cb2.HighPoint = cb2.ScaleCalibration.OrderBy(p => p.Key).ElementAt(1).Value;
                cb2.PostDrawItem = cb;
                addMeterItem(cb2);

                clsScaleItem cs = new clsScaleItem();
                cs.ParentID = ig.ID;
                cs.TopLeft = cb.TopLeft;
                cs.Size = cb.Size;
                cs.ReadingSource = cb.ReadingSource;
                cs.ZOrder = 4;
                cs.ShowType = true;
                addMeterItem(cs);

                clsSolidColour sc = new clsSolidColour();
                sc.ParentID = ig.ID;
                sc.TopLeft = new PointF(cb.TopLeft.X, cb.TopLeft.Y - _fHeight * 0.75f);
                sc.Size = new SizeF(cb.Size.Width, _fHeight + _fHeight * 0.75f);
                sc.Colour = System.Drawing.Color.FromArgb(32, 32, 32);
                sc.ZOrder = 1;
                addMeterItem(sc);

                fBottom = cb.TopLeft.Y + cb.Size.Height;

                ig.TopLeft = cb.TopLeft;
                ig.Size = new SizeF(cb.Size.Width, fBottom);
                ig.MeterType = MeterType.AGC;
                ig.Order = restoreIg == null ? numberOfMeterGroups() : restoreIg.Order;

                clsFadeCover fc = getFadeCover(ig.ID);
                if (fc != null) addMeterItem(fc);
                
                addMeterItem(ig);

                return cb.ID;
            }
            public string AddRotator(int nMSupdate, float fTop, out float fBottom, clsItemGroup restoreIg = null)
            {
                clsItemGroup ig = new clsItemGroup();
                if (restoreIg != null) ig.ID = restoreIg.ID;
                ig.ParentID = ID;

                float fSize = 1f;

                //az
                clsRotatorItem ri = new clsRotatorItem();
                if (ri.ViewMode == clsRotatorItem.RotatorMode.BOTH)// use constructor value
                    fSize = 0.5f;
                else
                    fSize = 1f;

                ri.Padding = fSize;
                ri.ParentID = ig.ID;
                ri.Primary = true;
                ri.TopLeft = new PointF(0f, _fPadY - (_fHeight * 0.75f));
                ri.Size = new SizeF(1f, fSize);
                ri.ZOrder = 3;
                ri.MMIOVariableIndex = 0;
                ri.ReadingSource = Reading.AZ;
                ri.UpdateInterval = 100;
                addMeterItem(ri);                                   

                //ele
                clsRotatorItem ri2 = new clsRotatorItem();
                ri2.Padding = fSize;
                ri2.ParentID = ig.ID;
                ri2.Primary = false;
                ri2.TopLeft = new PointF(0f, _fPadY - (_fHeight * 0.75f));
                ri2.Size = new SizeF(1f, fSize);
                ri2.ZOrder = 3;
                ri2.MMIOVariableIndex = 1;
                ri2.ReadingSource = Reading.ELE;
                ri2.UpdateInterval = 100;
                addMeterItem(ri2);

                clsImage img = new clsImage();
                img.ParentID = ig.ID;
                //img.TopLeft = ri.TopLeft;
                //img.Size = ri.Size;
                if (ri.ViewMode == clsRotatorItem.RotatorMode.BOTH)
                {
                    img.TopLeft = new PointF(0.5f - (fSize / 2f), _fPadY - (_fHeight * 0.75f)/* + ((fSize - fSize) * 0.5f)*/);
                    img.Size = new SizeF(fSize, fSize);
                }
                else
                {
                    img.TopLeft = new PointF(0.5f - (fSize / 2f), _fPadY - (_fHeight * 0.75f)/* + ((fSize - fSize) * 0.5f)*/);
                    img.Size = new SizeF(fSize, fSize);
                }
                //
                img.ZOrder = 2;
                img.ImageName = ri.ImageName;
                addMeterItem(img);

                clsSolidColour sc = new clsSolidColour();
                sc.ParentID = ig.ID;
                sc.TopLeft = ri.TopLeft;
                sc.Size = ri.Size;
                sc.Colour = System.Drawing.Color.FromArgb(32, 32, 32);
                sc.ZOrder = 1;
                addMeterItem(sc);

                fBottom = ri.TopLeft.Y + ri.Size.Height;

                ig.TopLeft = ri.TopLeft;
                ig.Size = new SizeF(ri.Size.Width, fBottom);
                ig.MeterType = MeterType.ROTATOR;
                ig.Order = restoreIg == null ? numberOfMeterGroups() : restoreIg.Order;

                clsFadeCover fc = getFadeCover(ig.ID);
                if (fc != null) addMeterItem(fc);

                addMeterItem(ig);

                return ri.ID;
            }
            public string AddMagicEye(int nMSupdate, float fTop, out float fBottom, float fSize,  clsItemGroup restoreIg = null)
            {
                clsItemGroup ig = new clsItemGroup();
                if(restoreIg != null) ig.ID = restoreIg.ID;
                ig.ParentID = ID;

                clsMagicEyeItem me = new clsMagicEyeItem();
                me.ParentID = ig.ID;
                me.Primary = true;
                me.TopLeft = new PointF(0.5f - (fSize / 2f), fTop + _fPadY - (_fHeight * 0.75f));
                me.Size = new SizeF(fSize, fSize);
                me.ZOrder = 2;
                me.MMIOVariableIndex = 0;
                me.AttackRatio = 0.2f;
                me.DecayRatio = 0.05f;
                me.UpdateInterval = nMSupdate;
                me.Colour = System.Drawing.Color.Lime;
                me.ReadingSource = Reading.AVG_SIGNAL_STRENGTH;
                me.ScaleCalibration.Add(-127f, new PointF(0, 0));
                me.ScaleCalibration.Add(-73f, new PointF(0.85f, 0));
                me.ScaleCalibration.Add(-13f, new PointF(1f, 0));
                me.Value = me.ScaleCalibration.OrderBy(p => p.Key).First().Key;
                if (IsAboveS9Frequency(_rx)) me.Value -= 20;
                addMeterItem(me);

                clsImage img = new clsImage();
                img.ParentID = ig.ID;
                img.TopLeft = me.TopLeft;
                img.Size = me.Size;
                img.ZOrder = 3;
                img.ImageName = "eye-bezel";
                addMeterItem(img);

                fBottom = me.TopLeft.Y + me.Size.Height;

                ig.TopLeft = me.TopLeft;
                ig.Size = new SizeF(me.Size.Width, fBottom);
                ig.MeterType = MeterType.MAGIC_EYE;
                ig.Order = restoreIg == null ? numberOfMeterGroups() : restoreIg.Order;

                clsFadeCover fc = getFadeCover(ig.ID);
                if (fc != null) addMeterItem(fc);
                
                addMeterItem(ig);

                return me.ID;
            }
            public string AddSpacer(int nMSupdate, float fTop, out float fBottom, float fSize, clsItemGroup restoreIg = null)
            {
                clsItemGroup ig = new clsItemGroup();
                if (restoreIg != null) ig.ID = restoreIg.ID;
                ig.ParentID = ID;

                clsSpacerItem me = new clsSpacerItem();
                me.ParentID = ig.ID;
                me.Primary = true;
                me.TopLeft = new PointF(0f, _fPadY - (_fHeight * 0.75f));
                me.Size = new SizeF(1f, fSize);
                me.ZOrder = 2;
                addMeterItem(me);

                fBottom = me.TopLeft.Y + me.Size.Height;

                ig.TopLeft = me.TopLeft;
                ig.Size = new SizeF(me.Size.Width, fBottom);
                ig.MeterType = MeterType.SPACER;
                ig.Order = restoreIg == null ? numberOfMeterGroups() : restoreIg.Order;

                clsFadeCover fc = getFadeCover(ig.ID);
                if (fc != null) addMeterItem(fc);

                addMeterItem(ig);

                return me.ID;
            }

            public string AddWebImage(int nMSupdate, float fTop, out float fBottom, float fSize, clsItemGroup restoreIg = null)
            {
                clsItemGroup ig = new clsItemGroup();
                if (restoreIg != null) ig.ID = restoreIg.ID;
                ig.ParentID = ID;

                clsWebImage me = new clsWebImage(this, ig);
                me.ParentID = ig.ID;
                me.Primary = true;
                me.TopLeft = new PointF(0f, _fPadY - (_fHeight * 0.75f));
                me.Size = new SizeF(1f, fSize);
                me.ZOrder = 2;
                addMeterItem(me);

                fBottom = me.TopLeft.Y + me.Size.Height;

                ig.TopLeft = me.TopLeft;
                ig.Size = new SizeF(me.Size.Width, fBottom);
                ig.MeterType = MeterType.WEB_IMAGE;
                ig.Order = restoreIg == null ? numberOfMeterGroups() : restoreIg.Order;

                clsFadeCover fc = getFadeCover(ig.ID);
                if (fc != null) addMeterItem(fc);

                addMeterItem(ig);

                return me.ID;
            }
            public string AddDataOut(int nMSupdate, float fTop, out float fBottom, clsItemGroup restoreIg = null)
            {
                clsItemGroup ig = new clsItemGroup();
                if (restoreIg != null) ig.ID = restoreIg.ID;
                ig.ParentID = ID;

                clsDataOut me = new clsDataOut();
                me.ParentID = ig.ID;
                me.Primary = true;
                me.TopLeft = new PointF(0f, _fPadY - (_fHeight * 0.75f));
                me.Size = new SizeF(1f, 0);
                addMeterItem(me);

                fBottom = me.TopLeft.Y + me.Size.Height;

                ig.TopLeft = me.TopLeft;
                ig.Size = new SizeF(me.Size.Width, fBottom);
                ig.MeterType = MeterType.DATA_OUT;
                ig.Order = restoreIg == null ? numberOfMeterGroups() : restoreIg.Order;

                addMeterItem(ig);

                return me.ID;
            }
            public string AddTextOverlay(int nMSupdate, float fTop, out float fBottom, float fSize, clsItemGroup restoreIg = null)
            {
                clsItemGroup ig = new clsItemGroup();
                if (restoreIg != null) ig.ID = restoreIg.ID;
                ig.ParentID = ID;

                clsTextOverlay me = new clsTextOverlay(this);
                me.ParentID = ig.ID;
                me.Primary = true;
                me.TopLeft = new PointF(0f, _fPadY - (_fHeight * 0.75f));
                me.Size = new SizeF(1f, fSize);
                me.ZOrder = 999; // on top of everything
                addMeterItem(me);

                fBottom = me.TopLeft.Y + me.Size.Height;

                ig.TopLeft = me.TopLeft;
                ig.Size = new SizeF(me.Size.Width, fBottom);
                ig.MeterType = MeterType.TEXT_OVERLAY;
                ig.Order = restoreIg == null ? numberOfMeterGroups() : restoreIg.Order;

                clsFadeCover fc = getFadeCover(ig.ID);
                if (fc != null) addMeterItem(fc);

                addMeterItem(ig);

                return me.ID;
            }
            public string AddLed(int nMSupdate, float fTop, out float fBottom, float fSize, clsItemGroup restoreIg = null)
            {
                clsItemGroup ig = new clsItemGroup();
                if (restoreIg != null) ig.ID = restoreIg.ID;
                ig.ParentID = ID;

                clsLed me = new clsLed(this);
                me.ParentID = ig.ID;
                me.Primary = true;
                me.TopLeft = new PointF(0f, _fPadY - (_fHeight * 0.75f));
                me.Size = new SizeF(1f, fSize);
                me.ZOrder = 998; // on top of everything, but under text
                addMeterItem(me);

                fBottom = me.TopLeft.Y + me.Size.Height;

                ig.TopLeft = me.TopLeft;
                ig.Size = new SizeF(me.Size.Width, fBottom);
                ig.MeterType = MeterType.LED;
                ig.Order = restoreIg == null ? numberOfMeterGroups() : restoreIg.Order;

                clsFadeCover fc = getFadeCover(ig.ID);
                if (fc != null) addMeterItem(fc);

                addMeterItem(ig);

                return me.ID;
            }
            public string AddAnanMM(int nMSupdate, float fTop, out float fBottom,  clsItemGroup restoreIg = null)
            {
                clsItemGroup ig = new clsItemGroup();
                if(restoreIg != null) ig.ID = restoreIg.ID;
                ig.ParentID = ID;

                //anan multi meter
                clsNeedleItem ni = new clsNeedleItem();
                ni.ParentID = ig.ID;
                ni.Primary = true;
                ni.TopLeft = new PointF(0f, fTop + _fPadY - (_fHeight * 0.75f));
                ni.Size = new SizeF(1f, 0.441f); // image x to y ratio

                ni.OnlyWhenRX = true;
                ni.ReadingSource = Reading.AVG_SIGNAL_STRENGTH;
                ni.MMIOVariableIndex = 0;
                ni.AttackRatio = 0.8f;//0.1f;
                ni.DecayRatio = 0.2f;// 0.05f;
                ni.UpdateInterval = nMSupdate;
                ni.HistoryDuration = 4000;
                ni.ShowHistory = true;
                ni.Style = clsNeedleItem.NeedleStyle.Line;
                ni.Colour = System.Drawing.Color.FromArgb(255, 233, 51, 50);
                ni.HistoryColour = System.Drawing.Color.FromArgb(64, 233, 51, 50);
                ni.ZOrder = 4;
                ni.NeedleOffset = new PointF(0.004f, 0.736f);
                ni.RadiusRatio = new PointF(1f, 0.58f);
                ni.LengthFactor = 1.65f;
                ni.ScaleStrokeWidth = true;

                ni.ScaleCalibration.Add(-127f, new PointF(0.076f, 0.31f));
                ni.ScaleCalibration.Add(-121f, new PointF(0.131f, 0.272f));
                ni.ScaleCalibration.Add(-115f, new PointF(0.189f, 0.254f));
                ni.ScaleCalibration.Add(-109f, new PointF(0.233f, 0.211f));
                ni.ScaleCalibration.Add(-103f, new PointF(0.284f, 0.207f));
                ni.ScaleCalibration.Add(-97f, new PointF(0.326f, 0.177f));
                ni.ScaleCalibration.Add(-91f, new PointF(0.374f, 0.177f));
                ni.ScaleCalibration.Add(-85f, new PointF(0.414f, 0.151f));
                ni.ScaleCalibration.Add(-79f, new PointF(0.459f, 0.168f));
                ni.ScaleCalibration.Add(-73f, new PointF(0.501f, 0.142f));
                ni.ScaleCalibration.Add(-63f, new PointF(0.564f, 0.172f));
                ni.ScaleCalibration.Add(-53f, new PointF(0.63f, 0.164f));
                ni.ScaleCalibration.Add(-43f, new PointF(0.695f, 0.203f));
                ni.ScaleCalibration.Add(-33f, new PointF(0.769f, 0.211f));
                ni.ScaleCalibration.Add(-23f, new PointF(0.838f, 0.272f));
                ni.ScaleCalibration.Add(-13f, new PointF(0.926f, 0.31f));
                //ni.Value = -127f;
                //MeterManager.setReading(rx, ni.ReadingSource, ni.Value);
                ni.Value = ni.ScaleCalibration.OrderBy(p => p.Key).First().Key;
                if (IsAboveS9Frequency(_rx)) ni.Value -= 20;
                addMeterItem(ni);

                //volts
                clsNeedleItem ni2 = new clsNeedleItem();
                ni2.ParentID = ig.ID;
                ni2.TopLeft = ni.TopLeft;
                ni2.Size = ni.Size;
                ni2.ReadingSource = Reading.VOLTS;
                ni2.MMIOVariableIndex = 1;
                ni2.AttackRatio = 0.2f;
                ni2.DecayRatio = 0.2f;
                ni2.UpdateInterval = nMSupdate;
                ni2.HistoryDuration = 500;
                ni2.ShowHistory = false;
                ni2.Style = clsNeedleItem.NeedleStyle.Line;
                ni2.Colour = System.Drawing.Color.Black;
                ni2.ZOrder = 3;
                ni2.NeedleOffset = new PointF(0.004f, 0.736f);
                ni2.RadiusRatio = new PointF(1f, 0.58f);
                ni2.LengthFactor = 0.75f;
                ni2.ScaleStrokeWidth = true;
                ni2.ScaleCalibration.Add(10f, new PointF(0.559f, 0.756f));
                ni2.ScaleCalibration.Add(12.5f, new PointF(0.605f, 0.772f));
                ni2.ScaleCalibration.Add(15f, new PointF(0.665f, 0.784f));
                ni2.Value = 0f;
                //MeterManager.setReading(rx, ni.ReadingSource, ni.Value);
                ni2.Value = ni2.ScaleCalibration.OrderBy(p => p.Key).First().Key;
                addMeterItem(ni2);

                //amps
                clsNeedleItem ni3 = new clsNeedleItem(); ;
                ni3.ParentID = ig.ID;
                ni3.TopLeft = ni.TopLeft;
                ni3.Size = ni.Size;
                ni3.OnlyWhenTX = true;
                ni3.ReadingSource = Reading.AMPS;
                ni3.MMIOVariableIndex = 2;
                ni3.AttackRatio = 0.2f;
                ni3.DecayRatio = 0.2f;
                ni3.UpdateInterval = nMSupdate;
                ni3.HistoryDuration = 500;
                ni3.ShowHistory = false;
                ni3.Style = clsNeedleItem.NeedleStyle.Line;
                ni3.Colour = System.Drawing.Color.Black;
                ni3.ZOrder = 3;
                ni3.NeedleOffset = new PointF(0.004f, 0.736f);
                ni3.RadiusRatio = new PointF(1f, 0.58f);
                ni3.LengthFactor = 1.15f;
                ni3.ScaleStrokeWidth = true;
                ni3.DisplayGroup = 4;
                ni3.ScaleCalibration.Add(0f, new PointF(0.199f, 0.576f));
                ni3.ScaleCalibration.Add(2f, new PointF(0.27f, 0.54f));
                ni3.ScaleCalibration.Add(4f, new PointF(0.333f, 0.516f));
                ni3.ScaleCalibration.Add(6f, new PointF(0.393f, 0.504f));
                ni3.ScaleCalibration.Add(8f, new PointF(0.448f, 0.492f));
                ni3.ScaleCalibration.Add(10f, new PointF(0.499f, 0.492f));
                ni3.ScaleCalibration.Add(12f, new PointF(0.554f, 0.488f));
                ni3.ScaleCalibration.Add(14f, new PointF(0.608f, 0.5f));
                ni3.ScaleCalibration.Add(16f, new PointF(0.667f, 0.516f));
                ni3.ScaleCalibration.Add(18f, new PointF(0.728f, 0.54f));
                ni3.ScaleCalibration.Add(20f, new PointF(0.799f, 0.576f));
                ni3.Value = 0f;
                //MeterManager.setReading(rx, ni.ReadingSource, ni.Value);
                ni3.Value = ni3.ScaleCalibration.OrderBy(p => p.Key).First().Key;
                addMeterItem(ni3);

                //
                clsClickBox clb = new clsClickBox();
                clb.ParentID = ig.ID;
                clb.TopLeft = new PointF(ni.TopLeft.X + 0.76f, ni.TopLeft.Y + 0.46f); // tl;
                clb.Size = new SizeF(0.18f, 0.044f);
                clb.OnlyWhenTX = true;
                clb.Button = MouseButtons.Left;
                addMeterItem(clb);

                //clsSolidColour sc = new clsSolidColour();
                //sc.ParentID = ig.ID;
                //sc.TopLeft = clb.TopLeft;
                //sc.Size = clb.Size;
                //sc.OnlyWhenTX = true;
                //sc.ZOrder = 6;
                //sc.Colour = System.Drawing.Color.FromArgb(48, System.Drawing.Color.White);
                //addMeterItem(sc);

                clsText tx = new clsText();
                tx.ParentID = ig.ID;
                tx.TopLeft = new PointF(clb.TopLeft.X + 0.02f, clb.TopLeft.Y + 0.005f);
                tx.Size = clb.Size;
                tx.OnlyWhenTX = true;
                tx.Text = "%group%";
                tx.ZOrder = 10;
                tx.Style = FontStyle.Bold;
                tx.Colour = System.Drawing.Color.DarkGray;
                tx.FixedSize = true;
                tx.FontSize = 8f;
                addMeterItem(tx);

                clsNeedleItem ni4 = new clsNeedleItem();
                ni4.ParentID = ig.ID;
                ni4.Primary = true;
                ni4.TopLeft = ni.TopLeft;
                ni4.Size = ni.Size;
                ni4.OnlyWhenTX = true;
                ni4.NormaliseTo100W = true;
                ni4.ReadingSource = Reading.PWR;
                ni4.MMIOVariableIndex = 3;
                ni4.AttackRatio = 0.2f;//0.325f;
                ni4.DecayRatio = 0.1f;//0.5f;
                ni4.UpdateInterval = nMSupdate;
                ni4.HistoryDuration = 4000;
                ni4.ShowHistory = true;
                ni4.Style = clsNeedleItem.NeedleStyle.Line;
                ni4.Colour = System.Drawing.Color.FromArgb(255, 233, 51, 50);
                ni4.HistoryColour = System.Drawing.Color.FromArgb(64, 233, 51, 50);
                ni4.ZOrder = 2;
                ni4.NeedleOffset = new PointF(0.004f, 0.736f);
                ni4.RadiusRatio = new PointF(1f, 0.58f);
                ni4.LengthFactor = 1.55f;
                ni4.ScaleStrokeWidth = true;
                ni4.DisplayGroup = 1;
                //ni.Setup = true;
                ni4.ScaleCalibration.Add(0f, new PointF(0.099f, 0.352f));
                ni4.ScaleCalibration.Add(5f, new PointF(0.164f, 0.312f));
                ni4.ScaleCalibration.Add(10f, new PointF(0.224f, 0.28f));
                ni4.ScaleCalibration.Add(25f, new PointF(0.335f, 0.236f));
                ni4.ScaleCalibration.Add(30f, new PointF(0.367f, 0.228f));
                ni4.ScaleCalibration.Add(40f, new PointF(0.436f, 0.22f));
                ni4.ScaleCalibration.Add(50f, new PointF(0.499f, 0.212f));
                ni4.ScaleCalibration.Add(60f, new PointF(0.559f, 0.216f));
                ni4.ScaleCalibration.Add(100f, new PointF(0.751f, 0.272f));
                ni4.ScaleCalibration.Add(150f, new PointF(0.899f, 0.352f));
                //ni4.Value = 0f;
                //MeterManager.setReading(rx, ni.ReadingSource, ni.Value);
                ni4.Value = ni4.ScaleCalibration.OrderBy(p => p.Key).First().Key;
                addMeterItem(ni4);

                clsNeedleScalePwrItem nspi = new clsNeedleScalePwrItem();
                nspi.ParentID = ig.ID;
                nspi.TopLeft = ni.TopLeft;
                nspi.Size = ni.Size;
                nspi.Primary = true;
                nspi.Marks = 7;
                nspi.ReadingSource = Reading.PWR;
                nspi.NeedleOffset = new PointF(0.004f, 0.736f);
                nspi.RadiusRatio = new PointF(1f, 0.55f);
                nspi.LengthFactor = 1.51f;
                nspi.ZOrder = 2;
                nspi.LowColour = System.Drawing.Color.Gray;
                nspi.FntStyle = FontStyle.Bold;
                nspi.FontSize = 22;
                nspi.ScaleCalibration.Add(0f, new PointF(0.099f, 0.352f));
                nspi.ScaleCalibration.Add(5f, new PointF(0.164f, 0.312f));
                nspi.ScaleCalibration.Add(10f, new PointF(0.224f, 0.28f));
                nspi.ScaleCalibration.Add(25f, new PointF(0.335f, 0.236f));
                nspi.ScaleCalibration.Add(30f, new PointF(0.367f, 0.228f));
                nspi.ScaleCalibration.Add(40f, new PointF(0.436f, 0.22f));
                nspi.ScaleCalibration.Add(50f, new PointF(0.499f, 0.212f));
                nspi.ScaleCalibration.Add(60f, new PointF(0.559f, 0.216f));
                nspi.ScaleCalibration.Add(100f, new PointF(0.751f, 0.272f));
                nspi.ScaleCalibration.Add(150f, new PointF(0.899f, 0.352f));
                addMeterItem(nspi);

                clsNeedleItem ni5 = new clsNeedleItem();
                ni5.ParentID = ig.ID;
                ni5.TopLeft = ni.TopLeft;
                ni5.Size = ni.Size;
                ni5.OnlyWhenTX = true;
                ni5.ReadingSource = Reading.SWR;
                ni5.MMIOVariableIndex = 4;
                ni5.AttackRatio = 0.2f;//0.325f;
                ni5.DecayRatio = 0.1f;//0.5f;
                ni5.UpdateInterval = nMSupdate;
                ni5.HistoryDuration = 4000;
                ni5.ShowHistory = true;
                ni5.Style = clsNeedleItem.NeedleStyle.Line;
                ni5.Colour = System.Drawing.Color.Black;
                ni5.HistoryColour = System.Drawing.Color.FromArgb(64, System.Drawing.Color.CornflowerBlue);
                ni5.ZOrder = 3;
                ni5.NeedleOffset = new PointF(0.004f, 0.736f);
                ni5.RadiusRatio = new PointF(1f, 0.58f);
                ni5.LengthFactor = 1.36f;
                ni5.ScaleStrokeWidth = true;
                ni5.DisplayGroup = 1;
                ni5.ScaleCalibration.Add(1f, new PointF(0.152f, 0.468f));
                ni5.ScaleCalibration.Add(1.5f, new PointF(0.28f, 0.404f));
                ni5.ScaleCalibration.Add(2f, new PointF(0.393f, 0.372f));
                ni5.ScaleCalibration.Add(2.5f, new PointF(0.448f, 0.36f));
                ni5.ScaleCalibration.Add(3f, new PointF(0.499f, 0.36f));
                ni5.ScaleCalibration.Add(10f, new PointF(0.847f, 0.476f));
                //ni5.Value = 1f;
                //MeterManager.setReading(rx, ni.ReadingSource, ni.Value);
                ni5.Value = ni5.ScaleCalibration.OrderBy(p => p.Key).First().Key;
                addMeterItem(ni5);

                clsNeedleItem ni6 = new clsNeedleItem();
                ni6.ParentID = ig.ID;
                ni6.TopLeft = ni.TopLeft;
                ni6.Size = ni.Size;
                ni6.OnlyWhenTX = true;
                ni6.ReadingSource = Reading.ALC_G; // alc_comp
                ni6.MMIOVariableIndex = 5;
                ni6.AttackRatio = 0.2f;//0.325f;
                ni6.DecayRatio = 0.1f;//0.5f;
                ni6.UpdateInterval = nMSupdate;
                ni6.HistoryDuration = 4000;
                ni6.ShowHistory = false;
                ni6.Style = clsNeedleItem.NeedleStyle.Line;
                ni6.Colour = System.Drawing.Color.Black;
                ni6.ZOrder = 3;
                ni6.NeedleOffset = new PointF(0.004f, 0.736f);
                ni6.RadiusRatio = new PointF(1f, 0.58f);
                ni6.LengthFactor = 0.96f;
                ni6.ScaleStrokeWidth = true;
                ni6.DisplayGroup = 2;
                ni6.ScaleCalibration.Add(0f, new PointF(0.249f, 0.68f));
                ni6.ScaleCalibration.Add(5f, new PointF(0.342f, 0.64f));
                ni6.ScaleCalibration.Add(10f, new PointF(0.425f, 0.624f));
                ni6.ScaleCalibration.Add(15f, new PointF(0.499f, 0.62f));
                ni6.ScaleCalibration.Add(20f, new PointF(0.571f, 0.628f));
                ni6.ScaleCalibration.Add(25f, new PointF(0.656f, 0.64f));
                ni6.ScaleCalibration.Add(30f, new PointF(0.751f, 0.688f));
                //ni6.Value = 0f;
                //MeterManager.setReading(rx, ni.ReadingSource, ni.Value);
                ni6.Value = ni6.ScaleCalibration.OrderBy(p => p.Key).First().Key;
                addMeterItem(ni6);

                clsNeedleItem ni7 = new clsNeedleItem();
                ni7.ParentID = ig.ID;
                ni7.TopLeft = ni.TopLeft;
                ni7.Size = ni.Size;
                ni7.OnlyWhenTX = true;
                ni7.ReadingSource = Reading.ALC_GROUP;
                ni7.MMIOVariableIndex = 6;
                ni7.AttackRatio = 0.2f;//0.325f;
                ni7.DecayRatio = 0.1f;//0.5f;
                ni7.UpdateInterval = nMSupdate;
                ni7.HistoryDuration = 4000;
                ni7.ShowHistory = false;
                ni7.Style = clsNeedleItem.NeedleStyle.Line;
                ni7.Colour = System.Drawing.Color.Black;
                //ni.HistoryColour = System.Drawing.Color.FromArgb(64, 233, 51, 50);
                ni7.ZOrder = 3;
                ni7.NeedleOffset = new PointF(0.004f, 0.736f);
                ni7.RadiusRatio = new PointF(1f, 0.58f);
                ni7.LengthFactor = 0.75f;
                ni7.ScaleStrokeWidth = true;
                ni7.DisplayGroup = 3;
                //ni.Setup = true;
                ni7.ScaleCalibration.Add(-30f, new PointF(0.295f, 0.804f));
                ni7.ScaleCalibration.Add(0f, new PointF(0.332f, 0.784f));
                ni7.ScaleCalibration.Add(25f, new PointF(0.499f, 0.756f));
                //ni7.Value = -30f;
                //MeterManager.setReading(rx, ni.ReadingSource, ni.Value);
                ni7.Value = ni7.ScaleCalibration.OrderBy(p => p.Key).First().Key;
                addMeterItem(ni7);
                //

                clsImage img = new clsImage();
                img.ParentID = ig.ID;
                img.Primary = true;
                img.TopLeft = ni.TopLeft;
                img.Size = ni.Size;
                img.ZOrder = 1;
                img.ImageName = "ananMM";
                addMeterItem(img);

                img = new clsImage();
                img.ParentID = ig.ID;
                img.OnlyWhenRX = true;
                img.TopLeft = new PointF(ni.TopLeft.X, ni.TopLeft.Y + ni.Size.Height);
                img.Size = new SizeF(1f, 75 / 900f);//0.101f); // image x to y ratio : 75 pixels y, 900 x
                img.ZOrder = 5;
                img.ImageName = "ananMM-bg";
                addMeterItem(img);

                img = new clsImage();
                img.ParentID = ig.ID;
                img.Primary = true;
                img.OnlyWhenTX = true;
                img.TopLeft = new PointF(ni.TopLeft.X, ni.TopLeft.Y + ni.Size.Height);
                img.Size = new SizeF(1f, 75 / 900f);//0.101f); // image x to y ratio : 75 pixels y, 900 x
                img.ZOrder = 5;
                img.ImageName = "ananMM-bg-tx";
                addMeterItem(img);

                fBottom = img.TopLeft.Y + img.Size.Height;

                ig.TopLeft = ni.TopLeft;
                ig.Size = new SizeF(ni.Size.Width, fBottom);
                ig.MeterType = MeterType.ANANMM;
                ig.Order = restoreIg == null ? numberOfMeterGroups() : restoreIg.Order;

                clsFadeCover fc = getFadeCover(ig.ID);
                if (fc != null) addMeterItem(fc);
                
                addMeterItem(ig);

                AddDisplayGroup("ALL"); //0
                AddDisplayGroup("PWR/SWR"); //1
                AddDisplayGroup("Comp"); //2
                AddDisplayGroup("ALC"); //3
                AddDisplayGroup("Volts/Amps"); //4
                DisplayGroup = 1;

                return ni.ID;
            }
            public string AddCrossNeedle(int nMSupdate, float fTop, out float fBottom,  clsItemGroup restoreIg = null)
            {
                clsItemGroup ig = new clsItemGroup();
                if(restoreIg != null) ig.ID = restoreIg.ID;
                ig.ParentID = ID;

                clsNeedleItem ni = new clsNeedleItem();
                ni.ParentID = ig.ID;
                ni.Primary = true;
                ni.TopLeft = new PointF(0f, fTop + _fPadY - (_fHeight * 0.75f));
                ni.Size = new SizeF(1f, 0.782f); // image x to y ratio
                ni.ReadingSource = Reading.PWR;
                ni.MMIOVariableIndex = 0;
                ni.AttackRatio = 0.2f;
                ni.DecayRatio = 0.1f;
                ni.UpdateInterval = nMSupdate;
                ni.HistoryDuration = 4000;
                ni.ShowHistory = true;
                ni.StrokeWidth = 2.5f;
                ni.Style = clsNeedleItem.NeedleStyle.Line;
                ni.Colour = System.Drawing.Color.Black;
                ni.HistoryColour = System.Drawing.Color.FromArgb(96, System.Drawing.Color.Red);
                ni.ZOrder = 4;
                ni.NeedleOffset = new PointF(0.322f, 0.611f);
                ni.LengthFactor = 1.62f;
                ni.ScaleStrokeWidth = true;
                ni.ScaleCalibration.Add(0f, new PointF(0.052f, 0.732f));
                ni.ScaleCalibration.Add(5f, new PointF(0.146f, 0.528f));
                ni.ScaleCalibration.Add(10f, new PointF(0.188f, 0.434f));
                ni.ScaleCalibration.Add(15f, new PointF(0.235f, 0.387f));
                ni.ScaleCalibration.Add(20f, new PointF(0.258f, 0.338f));
                ni.ScaleCalibration.Add(25f, new PointF(0.303f, 0.313f));
                ni.ScaleCalibration.Add(30f, new PointF(0.321f, 0.272f));
                ni.ScaleCalibration.Add(35f, new PointF(0.361f, 0.257f));
                ni.ScaleCalibration.Add(40f, new PointF(0.381f, 0.223f));
                ni.ScaleCalibration.Add(50f, new PointF(0.438f, 0.181f));
                ni.ScaleCalibration.Add(60f, new PointF(0.483f, 0.155f));
                ni.ScaleCalibration.Add(70f, new PointF(0.532f, 0.13f));
                ni.ScaleCalibration.Add(80f, new PointF(0.577f, 0.111f));
                ni.ScaleCalibration.Add(90f, new PointF(0.619f, 0.098f));
                ni.ScaleCalibration.Add(100f, new PointF(0.662f, 0.083f));
                ni.NormaliseTo100W = true;
                //ni.Value = 0f;
                //MeterManager.setReading(rx, ni.ReadingSource, ni.Value);
                ni.Value = ni.ScaleCalibration.OrderBy(p => p.Key).First().Key;
                addMeterItem(ni);

                clsNeedleScalePwrItem nspi = new clsNeedleScalePwrItem();
                nspi.ParentID = ig.ID;
                nspi.TopLeft = ni.TopLeft;
                nspi.Size = ni.Size;
                nspi.Primary = true;
                nspi.Marks = 8;
                nspi.ReadingSource = Reading.PWR;
                nspi.NeedleOffset = new PointF(0.318f, 0.611f);
                nspi.LengthFactor = 1.685f;
                nspi.ZOrder = 5;
                nspi.LowColour = System.Drawing.Color.Gray;
                nspi.FntStyle = FontStyle.Bold;
                nspi.FontSize = 16;
                nspi.ScaleCalibration.Add(0f, new PointF(0.052f, 0.732f));
                nspi.ScaleCalibration.Add(5f, new PointF(0.146f, 0.528f));
                nspi.ScaleCalibration.Add(10f, new PointF(0.188f, 0.434f));
                nspi.ScaleCalibration.Add(15f, new PointF(0.235f, 0.387f));
                nspi.ScaleCalibration.Add(20f, new PointF(0.258f, 0.338f));
                nspi.ScaleCalibration.Add(25f, new PointF(0.303f, 0.313f));
                nspi.ScaleCalibration.Add(30f, new PointF(0.321f, 0.272f));
                nspi.ScaleCalibration.Add(35f, new PointF(0.361f, 0.257f));
                nspi.ScaleCalibration.Add(40f, new PointF(0.381f, 0.223f));
                nspi.ScaleCalibration.Add(50f, new PointF(0.438f, 0.181f));
                nspi.ScaleCalibration.Add(60f, new PointF(0.483f, 0.155f));
                nspi.ScaleCalibration.Add(70f, new PointF(0.532f, 0.13f));
                nspi.ScaleCalibration.Add(80f, new PointF(0.577f, 0.111f));
                nspi.ScaleCalibration.Add(90f, new PointF(0.619f, 0.098f));
                nspi.ScaleCalibration.Add(100f, new PointF(0.662f, 0.083f));
                addMeterItem(nspi);

                clsNeedleItem ni2 = new clsNeedleItem();
                ni2.ParentID = ig.ID;
                ni2.TopLeft = ni.TopLeft;
                ni2.Size = ni.Size;
                ni2.ReadingSource = Reading.REVERSE_PWR;
                ni2.MMIOVariableIndex = 1;
                ni2.AttackRatio = 0.2f;//0.325f;
                ni2.DecayRatio = 0.1f;//0.5f;
                ni2.UpdateInterval = nMSupdate;
                ni2.HistoryDuration = 4000;
                ni2.ShowHistory = true;
                ni2.StrokeWidth = 2.5f;
                ni2.Direction = clsNeedleItem.NeedleDirection.CounterClockwise;
                ni2.Style = clsNeedleItem.NeedleStyle.Line;
                ni2.Colour = System.Drawing.Color.Black;
                ni2.HistoryColour = System.Drawing.Color.FromArgb(96, System.Drawing.Color.CornflowerBlue);
                ni2.ZOrder = 3;
                ni2.NeedleOffset = new PointF(-0.322f, 0.611f);
                ni2.LengthFactor = 1.62f;
                ni2.ScaleStrokeWidth = true;
                ni2.ScaleCalibration.Add(0f, new PointF(0.948f, 0.74f));
                ni2.ScaleCalibration.Add(0.25f, new PointF(0.913f, 0.7f));
                ni2.ScaleCalibration.Add(0.5f, new PointF(0.899f, 0.638f));
                ni2.ScaleCalibration.Add(0.75f, new PointF(0.875f, 0.594f));
                ni2.ScaleCalibration.Add(1f, new PointF(0.854f, 0.538f));
                ni2.ScaleCalibration.Add(2f, new PointF(0.814f, 0.443f));
                ni2.ScaleCalibration.Add(3f, new PointF(0.769f, 0.4f));
                ni2.ScaleCalibration.Add(4f, new PointF(0.744f, 0.351f));
                ni2.ScaleCalibration.Add(5f, new PointF(0.702f, 0.321f));
                ni2.ScaleCalibration.Add(6f, new PointF(0.682f, 0.285f));
                ni2.ScaleCalibration.Add(7f, new PointF(0.646f, 0.268f));
                ni2.ScaleCalibration.Add(8f, new PointF(0.626f, 0.234f));
                ni2.ScaleCalibration.Add(9f, new PointF(0.596f, 0.228f));
                ni2.ScaleCalibration.Add(10f, new PointF(0.569f, 0.196f));
                ni2.ScaleCalibration.Add(12f, new PointF(0.524f, 0.166f));
                ni2.ScaleCalibration.Add(14f, new PointF(0.476f, 0.14f));
                ni2.ScaleCalibration.Add(16f, new PointF(0.431f, 0.121f));
                ni2.ScaleCalibration.Add(18f, new PointF(0.393f, 0.109f));
                ni2.ScaleCalibration.Add(20f, new PointF(0.349f, 0.098f));
                ni2.NormaliseTo100W = true;
                //ni2.Value = 0f;
                //MeterManager.setReading(rx, ni.ReadingSource, ni.Value);
                ni2.Value = ni2.ScaleCalibration.OrderBy(p => p.Key).First().Key;
                addMeterItem(ni2);

                clsNeedleScalePwrItem nspi2 = new clsNeedleScalePwrItem();
                nspi2.ParentID = ig.ID;
                nspi2.TopLeft = ni.TopLeft;
                nspi2.Size = ni.Size;
                nspi2.Marks = 8;
                nspi2.ReadingSource = Reading.PWR;
                nspi2.Direction = clsNeedleItem.NeedleDirection.CounterClockwise;
                nspi2.NeedleOffset = new PointF(-0.322f, 0.611f);
                nspi2.LengthFactor = 1.685f;
                nspi2.ZOrder = 5;
                nspi2.LowColour = System.Drawing.Color.Gray;
                nspi2.FntStyle = FontStyle.Bold;
                nspi2.FontSize = 16;
                nspi2.ScaleCalibration.Add(0f, new PointF(0.948f, 0.74f));
                nspi2.ScaleCalibration.Add(0.25f, new PointF(0.913f, 0.7f));
                nspi2.ScaleCalibration.Add(0.5f, new PointF(0.899f, 0.638f));
                nspi2.ScaleCalibration.Add(0.75f, new PointF(0.875f, 0.594f));
                nspi2.ScaleCalibration.Add(1f, new PointF(0.854f, 0.538f));
                nspi2.ScaleCalibration.Add(2f, new PointF(0.814f, 0.443f));
                nspi2.ScaleCalibration.Add(3f, new PointF(0.769f, 0.4f));
                nspi2.ScaleCalibration.Add(4f, new PointF(0.744f, 0.351f));
                nspi2.ScaleCalibration.Add(5f, new PointF(0.702f, 0.321f));
                nspi2.ScaleCalibration.Add(6f, new PointF(0.682f, 0.285f));
                nspi2.ScaleCalibration.Add(7f, new PointF(0.646f, 0.268f));
                nspi2.ScaleCalibration.Add(8f, new PointF(0.626f, 0.234f));
                nspi2.ScaleCalibration.Add(9f, new PointF(0.596f, 0.228f));
                nspi2.ScaleCalibration.Add(10f, new PointF(0.569f, 0.196f));
                nspi2.ScaleCalibration.Add(12f, new PointF(0.524f, 0.166f));
                nspi2.ScaleCalibration.Add(14f, new PointF(0.476f, 0.14f));
                nspi2.ScaleCalibration.Add(16f, new PointF(0.431f, 0.121f));
                nspi2.ScaleCalibration.Add(18f, new PointF(0.393f, 0.109f));
                nspi2.ScaleCalibration.Add(20f, new PointF(0.349f, 0.098f));
                addMeterItem(nspi2);

                clsImage img = new clsImage();
                img.ParentID = ig.ID;
                img.Primary = true;
                img.TopLeft = ni.TopLeft;
                img.Size = ni.Size;
                img.ZOrder = 1;
                img.ImageName = "cross-needle";
                addMeterItem(img);

                clsImage img2 = new clsImage();
                img2.ParentID = ig.ID;
                img2.TopLeft = new PointF(ni.TopLeft.X, ni.TopLeft.Y + ni.Size.Height - 0.180f); // it goes too high if we move it up by 0.217f
                img2.Size = new SizeF(1f, 0.217f);
                img2.ZOrder = 5;
                img2.ImageName = "cross-needle-bg";
                addMeterItem(img2);

                fBottom = img2.TopLeft.Y + img2.Size.Height;

                ig.TopLeft = ni.TopLeft;
                ig.Size = new SizeF(ni.Size.Width, fBottom);
                ig.MeterType = MeterType.CROSS;
                ig.Order = restoreIg == null ? numberOfMeterGroups() : restoreIg.Order;

                clsFadeCover fc = getFadeCover(ig.ID);
                if (fc != null) addMeterItem(fc);
                
                addMeterItem(ig);

                return ni.ID;
            }
            public string AddMicBar(int nMSupdate, float fTop, out float fBottom,  clsItemGroup restoreIg = null)
            {
                clsItemGroup ig = new clsItemGroup();
                if(restoreIg != null) ig.ID = restoreIg.ID;
                ig.ParentID = ID;

                clsBarItem cb2 = new clsBarItem();
                cb2.ParentID = ig.ID;
                cb2.Primary = true;
                cb2.TopLeft = new PointF(_fPadX, fTop + _fPadY);
                cb2.Size = new SizeF(1f - _fPadX * 2f, _fHeight);
                cb2.ReadingSource = Reading.MIC_PK;
                cb2.MMIOVariableIndex = 1;
                cb2.AttackRatio = 0.8f;
                cb2.DecayRatio = 0.1f;
                cb2.UpdateInterval = nMSupdate;
                cb2.HistoryDuration = 2000;
                cb2.ShowHistory = true;
                cb2.MarkerColour = System.Drawing.Color.Yellow;
                cb2.HistoryColour = System.Drawing.Color.FromArgb(128, System.Drawing.Color.Red);
                cb2.Style = clsBarItem.BarStyle.Line;
                cb2.ScaleCalibration.Add(-30, new PointF(0, 0));
                cb2.ScaleCalibration.Add(0, new PointF(0.665f, 0));
                cb2.ScaleCalibration.Add(12, new PointF(0.99f, 0));
                cb2.ZOrder = 2;
                cb2.FontColour = System.Drawing.Color.Yellow;
                cb2.Value = cb2.ScaleCalibration.OrderBy(p => p.Key).First().Key;
                cb2.HighPoint = cb2.ScaleCalibration.OrderBy(p => p.Key).ElementAt(1).Value;
                addMeterItem(cb2);

                clsBarItem cb = new clsBarItem();
                cb.ParentID = ig.ID;
                cb.TopLeft = cb2.TopLeft;
                cb.Size = cb2.Size;
                cb.ReadingSource = Reading.MIC;
                cb.MMIOVariableIndex = 0;
                cb.ShowPeakValue = false;
                cb.AttackRatio = 0.8f;
                cb.DecayRatio = 0.1f;
                cb.UpdateInterval = nMSupdate;
                cb.HistoryDuration = 2000;
                cb.ShowHistory = false;
                cb.MarkerColour = System.Drawing.Color.DarkGray;
                cb.HistoryColour = System.Drawing.Color.FromArgb(128, System.Drawing.Color.Red);
                cb.Style = clsBarItem.BarStyle.Line;
                cb.ScaleCalibration.Add(-30, new PointF(0, 0));
                cb.ScaleCalibration.Add(0, new PointF(0.665f, 0));
                cb.ScaleCalibration.Add(12, new PointF(0.99f, 0));
                cb.ShowValue = false;
                cb.FontColour = System.Drawing.Color.Yellow;
                cb.ZOrder = 3;
                cb.Value = cb.ScaleCalibration.OrderBy(p => p.Key).First().Key;
                cb.HighPoint = cb.ScaleCalibration.OrderBy(p => p.Key).ElementAt(1).Value;
                cb.PostDrawItem = cb2;
                addMeterItem(cb);

                clsScaleItem cs = new clsScaleItem();
                cs.ParentID = ig.ID;
                cs.TopLeft = cb.TopLeft;
                cs.Size = cb.Size;
                cs.ReadingSource = cb.ReadingSource;
                cs.ZOrder = 4;
                cs.ShowType = true;
                addMeterItem(cs);

                clsSolidColour sc = new clsSolidColour();
                sc.ParentID = ig.ID;
                sc.TopLeft = new PointF(cb.TopLeft.X, cb.TopLeft.Y - _fHeight * 0.75f);
                sc.Size = new SizeF(cb.Size.Width, _fHeight + _fHeight * 0.75f);
                sc.Colour = System.Drawing.Color.FromArgb(32, 32, 32);
                sc.ZOrder = 1;
                addMeterItem(sc);

                fBottom = cb.TopLeft.Y + cb.Size.Height;

                ig.TopLeft = cb.TopLeft;
                ig.Size = new SizeF(cb.Size.Width, fBottom);
                ig.MeterType = MeterType.MIC;
                ig.Order = restoreIg == null ? numberOfMeterGroups() : restoreIg.Order;

                clsFadeCover fc = getFadeCover(ig.ID);
                if (fc != null) addMeterItem(fc);
                
                addMeterItem(ig);

                return cb.ID;
            }
            public string AddEQBar(int nMSupdate, float fTop, out float fBottom,  clsItemGroup restoreIg = null)
            {
                clsItemGroup ig = new clsItemGroup();
                if(restoreIg != null) ig.ID = restoreIg.ID;
                ig.ParentID = ID;

                clsBarItem cb2 = new clsBarItem();
                cb2.ParentID = ig.ID;
                cb2.Primary = true;
                cb2.TopLeft = new PointF(_fPadX, fTop + _fPadY);
                cb2.Size = new SizeF(1f - _fPadX * 2f, _fHeight);
                cb2.ReadingSource = Reading.EQ_PK;
                cb2.MMIOVariableIndex = 1;
                cb2.AttackRatio = 0.8f;
                cb2.DecayRatio = 0.1f;
                cb2.UpdateInterval = nMSupdate;
                cb2.HistoryDuration = 2000;
                cb2.ShowHistory = true;
                cb2.MarkerColour = System.Drawing.Color.Yellow;
                cb2.HistoryColour = System.Drawing.Color.FromArgb(128, System.Drawing.Color.CornflowerBlue);
                cb2.Style = clsBarItem.BarStyle.Line;
                cb2.ScaleCalibration.Add(-30, new PointF(0, 0));
                cb2.ScaleCalibration.Add(0, new PointF(0.665f, 0));
                cb2.ScaleCalibration.Add(12, new PointF(0.99f, 0));
                cb2.ZOrder = 2;
                cb2.FontColour = System.Drawing.Color.Yellow;
                cb2.ShowValue = true;
                cb2.Value = cb2.ScaleCalibration.OrderBy(p => p.Key).First().Key;
                cb2.HighPoint = cb2.ScaleCalibration.OrderBy(p => p.Key).ElementAt(1).Value;                
                addMeterItem(cb2);

                clsBarItem cb = new clsBarItem();
                cb.ParentID = ig.ID;
                cb.TopLeft = cb2.TopLeft;
                cb.Size = cb2.Size;
                cb.ReadingSource = Reading.EQ;
                cb.MMIOVariableIndex = 0;
                cb.ShowPeakValue = false;
                cb.AttackRatio = 0.8f;
                cb.DecayRatio = 0.1f;
                cb.UpdateInterval = nMSupdate;
                cb.HistoryDuration = 2000;
                cb.ShowHistory = false;
                cb.MarkerColour = System.Drawing.Color.DarkGray;
                cb.HistoryColour = System.Drawing.Color.FromArgb(128, System.Drawing.Color.CornflowerBlue);
                cb.Style = clsBarItem.BarStyle.Line;
                cb.ScaleCalibration.Add(-30, new PointF(0, 0));
                cb.ScaleCalibration.Add(0, new PointF(0.665f, 0));
                cb.ScaleCalibration.Add(12, new PointF(0.99f, 0));
                cb.ShowValue = false;
                cb.ZOrder = 3;
                cb.FontColour = System.Drawing.Color.Yellow;
                cb.Value = cb.ScaleCalibration.OrderBy(p => p.Key).First().Key;
                cb.HighPoint = cb.ScaleCalibration.OrderBy(p => p.Key).ElementAt(1).Value;
                cb.PostDrawItem = cb2;
                addMeterItem(cb);

                clsScaleItem cs = new clsScaleItem();
                cs.ParentID = ig.ID;
                cs.TopLeft = cb.TopLeft;
                cs.Size = cb.Size;
                cs.ReadingSource = cb.ReadingSource;
                cs.ZOrder = 4;
                cs.ShowType = true;
                addMeterItem(cs);

                clsSolidColour sc = new clsSolidColour();
                sc.TopLeft = new PointF(cb.TopLeft.X, cb.TopLeft.Y - _fHeight * 0.75f);
                sc.ParentID = ig.ID;
                sc.Size = new SizeF(cb.Size.Width, _fHeight + _fHeight * 0.75f);
                sc.Colour = System.Drawing.Color.FromArgb(32, 32, 32);
                sc.ZOrder = 1;
                addMeterItem(sc);

                fBottom = cb.TopLeft.Y + cb.Size.Height;

                ig.TopLeft = cb.TopLeft;
                ig.Size = new SizeF(cb.Size.Width, fBottom);
                ig.MeterType = MeterType.EQ;
                ig.Order = restoreIg == null ? numberOfMeterGroups() : restoreIg.Order;

                clsFadeCover fc = getFadeCover(ig.ID);
                if (fc != null) addMeterItem(fc);
                
                addMeterItem(ig);

                return cb.ID;
            }
            public string AddLevelerBar(int nMSupdate, float fTop, out float fBottom,  clsItemGroup restoreIg = null)
            {
                clsItemGroup ig = new clsItemGroup();
                if(restoreIg != null) ig.ID = restoreIg.ID;
                ig.ParentID = ID;

                clsBarItem cb2 = new clsBarItem();
                cb2.ParentID = ig.ID;
                cb2.Primary = true;
                cb2.TopLeft = new PointF(_fPadX, fTop + _fPadY);
                cb2.Size = new SizeF(1f - _fPadX * 2f, _fHeight);
                cb2.ReadingSource = Reading.LEVELER_PK;
                cb2.MMIOVariableIndex = 1;
                cb2.AttackRatio = 0.8f;
                cb2.DecayRatio = 0.1f;
                cb2.UpdateInterval = nMSupdate;
                cb2.HistoryDuration = 2000;
                cb2.ShowHistory = true;
                cb2.MarkerColour = System.Drawing.Color.Yellow;
                cb2.HistoryColour = System.Drawing.Color.FromArgb(128, System.Drawing.Color.Purple);
                cb2.Style = clsBarItem.BarStyle.Line;
                cb2.ScaleCalibration.Add(-30, new PointF(0, 0));
                cb2.ScaleCalibration.Add(0, new PointF(0.665f, 0));
                cb2.ScaleCalibration.Add(12, new PointF(0.99f, 0));
                cb2.ZOrder = 2;
                cb2.FontColour = System.Drawing.Color.Yellow;
                cb2.Value = cb2.ScaleCalibration.OrderBy(p => p.Key).First().Key;
                cb2.HighPoint = cb2.ScaleCalibration.OrderBy(p => p.Key).ElementAt(1).Value;
                addMeterItem(cb2);

                clsBarItem cb = new clsBarItem();
                cb.ParentID = ig.ID;
                cb.TopLeft = cb2.TopLeft;
                cb.Size = cb2.Size;
                cb.ReadingSource = Reading.LEVELER;
                cb.MMIOVariableIndex = 0;
                cb.ShowPeakValue = false;
                cb.AttackRatio = 0.8f;
                cb.DecayRatio = 0.1f;
                cb.UpdateInterval = nMSupdate;
                cb.HistoryDuration = 2000;
                cb.ShowHistory = false;
                cb.MarkerColour = System.Drawing.Color.DarkGray;
                cb.HistoryColour = System.Drawing.Color.FromArgb(128, System.Drawing.Color.Purple);
                cb.Style = clsBarItem.BarStyle.Line;
                cb.ScaleCalibration.Add(-30, new PointF(0, 0));
                cb.ScaleCalibration.Add(0, new PointF(0.665f, 0));
                cb.ScaleCalibration.Add(12, new PointF(0.99f, 0));
                cb.ZOrder = 3;
                cb.ShowValue = false;
                cb.Value = cb.ScaleCalibration.OrderBy(p => p.Key).First().Key;
                cb.HighPoint = cb.ScaleCalibration.OrderBy(p => p.Key).ElementAt(1).Value;
                cb.PostDrawItem = cb2;
                addMeterItem(cb);

                clsScaleItem cs = new clsScaleItem();
                cs.ParentID = ig.ID;
                cs.TopLeft = cb.TopLeft;
                cs.Size = cb.Size;
                cs.ReadingSource = cb.ReadingSource;
                cs.ZOrder = 4;
                cs.ShowType = true;
                addMeterItem(cs);

                clsSolidColour sc = new clsSolidColour();
                sc.ParentID = ig.ID;
                sc.TopLeft = new PointF(cb.TopLeft.X, cb.TopLeft.Y - _fHeight * 0.75f);
                sc.Size = new SizeF(cb.Size.Width, _fHeight + _fHeight * 0.75f);
                sc.Colour = System.Drawing.Color.FromArgb(32, 32, 32);
                sc.ZOrder = 1;
                addMeterItem(sc);

                fBottom = cb.TopLeft.Y + cb.Size.Height;

                ig.TopLeft = cb.TopLeft;
                ig.Size = new SizeF(cb.Size.Width, fBottom);
                ig.MeterType = MeterType.LEVELER;
                ig.Order = restoreIg == null ? numberOfMeterGroups() : restoreIg.Order;

                clsFadeCover fc = getFadeCover(ig.ID);
                if (fc != null) addMeterItem(fc);
                
                addMeterItem(ig);

                return cb.ID;
            }
            public string AddLevelerGainBar(int nMSupdate, float fTop, out float fBottom,  clsItemGroup restoreIg = null)
            {
                clsItemGroup ig = new clsItemGroup();
                if(restoreIg != null) ig.ID = restoreIg.ID;
                ig.ParentID = ID;

                clsBarItem cb = new clsBarItem();
                cb.ParentID = ig.ID;
                cb.Primary = true;
                cb.TopLeft = new PointF(_fPadX, fTop + _fPadY);
                cb.Size = new SizeF(1f - _fPadX * 2f, _fHeight);
                cb.ReadingSource = Reading.LVL_G;
                cb.MMIOVariableIndex = 0;
                cb.AttackRatio = 0.8f;
                cb.DecayRatio = 0.1f;
                cb.UpdateInterval = nMSupdate;
                cb.HistoryDuration = 2000;
                cb.ShowHistory = true;
                cb.MarkerColour = System.Drawing.Color.Yellow;
                cb.HistoryColour = System.Drawing.Color.FromArgb(128, System.Drawing.Color.Purple);
                cb.Style = clsBarItem.BarStyle.Line;
                cb.ScaleCalibration.Add(0, new PointF(0, 0));
                cb.ScaleCalibration.Add(20, new PointF(0.8f, 0));
                cb.ScaleCalibration.Add(25, new PointF(0.99f, 0));
                cb.ZOrder = 2;
                cb.FontColour = System.Drawing.Color.Yellow;
                cb.Value = cb.ScaleCalibration.OrderBy(p => p.Key).First().Key;
                cb.HighPoint = cb.ScaleCalibration.OrderBy(p => p.Key).ElementAt(1).Value;
                addMeterItem(cb);

                clsScaleItem cs = new clsScaleItem();
                cs.ParentID = ig.ID;
                cs.TopLeft = cb.TopLeft;
                cs.Size = cb.Size;
                cs.ReadingSource = cb.ReadingSource;
                cs.ZOrder = 4;
                cs.ShowType = true;
                addMeterItem(cs);

                clsSolidColour sc = new clsSolidColour();
                sc.ParentID = ig.ID;
                sc.TopLeft = new PointF(cb.TopLeft.X, cb.TopLeft.Y - _fHeight * 0.75f);
                sc.Size = new SizeF(cb.Size.Width, _fHeight + _fHeight * 0.75f);
                sc.Colour = System.Drawing.Color.FromArgb(32, 32, 32);
                sc.ZOrder = 1;
                addMeterItem(sc);

                fBottom = cb.TopLeft.Y + cb.Size.Height;

                ig.TopLeft = cb.TopLeft;
                ig.Size = new SizeF(cb.Size.Width, fBottom);
                ig.MeterType = MeterType.LEVELER_GAIN;
                ig.Order = restoreIg == null ? numberOfMeterGroups() : restoreIg.Order;

                clsFadeCover fc = getFadeCover(ig.ID);
                if (fc != null) addMeterItem(fc);
                
                addMeterItem(ig);

                return cb.ID;
            }
            public string AddALCBar(int nMSupdate, float fTop, out float fBottom,  clsItemGroup restoreIg = null)
            {
                clsItemGroup ig = new clsItemGroup();
                if(restoreIg != null) ig.ID = restoreIg.ID;
                ig.ParentID = ID;

                clsBarItem cb2 = new clsBarItem();
                cb2.ParentID = ig.ID;
                cb2.Primary = true;
                cb2.TopLeft = new PointF(_fPadX, fTop + _fPadY);
                cb2.Size = new SizeF(1f - _fPadX * 2f, _fHeight);
                cb2.ReadingSource = Reading.ALC_PK;
                cb2.MMIOVariableIndex = 1;
                cb2.AttackRatio = 0.8f;
                cb2.DecayRatio = 0.1f;
                cb2.UpdateInterval = nMSupdate;
                cb2.HistoryDuration = 2000;
                cb2.ShowHistory = true;
                cb2.MarkerColour = System.Drawing.Color.Yellow;
                cb2.HistoryColour = System.Drawing.Color.FromArgb(128, System.Drawing.Color.LemonChiffon);
                cb2.Style = clsBarItem.BarStyle.Line;
                cb2.ScaleCalibration.Add(-30, new PointF(0, 0));
                cb2.ScaleCalibration.Add(0, new PointF(0.665f, 0));
                cb2.ScaleCalibration.Add(12, new PointF(0.99f, 0));
                cb2.ZOrder = 2;
                cb2.FontColour = System.Drawing.Color.Yellow;
                cb2.Value = cb2.ScaleCalibration.OrderBy(p => p.Key).First().Key;
                cb2.HighPoint = cb2.ScaleCalibration.OrderBy(p => p.Key).ElementAt(1).Value;
                addMeterItem(cb2);

                clsBarItem cb = new clsBarItem();
                cb.ParentID = ig.ID;
                cb.TopLeft = cb2.TopLeft;
                cb.Size = cb2.Size;
                cb.ReadingSource = Reading.ALC;
                cb.MMIOVariableIndex = 0;
                cb.ShowPeakValue = false;
                cb.AttackRatio = 0.8f;
                cb.DecayRatio = 0.1f;
                cb.UpdateInterval = nMSupdate;
                cb.HistoryDuration = 2000;
                cb.ShowHistory = false;
                cb.MarkerColour = System.Drawing.Color.DarkGray;
                cb.HistoryColour = System.Drawing.Color.FromArgb(128, System.Drawing.Color.LemonChiffon);
                cb.Style = clsBarItem.BarStyle.Line;
                cb.ScaleCalibration.Add(-30, new PointF(0, 0));
                cb.ScaleCalibration.Add(0, new PointF(0.665f, 0));
                cb.ScaleCalibration.Add(12, new PointF(0.99f, 0));
                cb.ZOrder = 3;
                cb.ShowValue = false;
                cb.Value = cb.ScaleCalibration.OrderBy(p => p.Key).First().Key;
                cb.HighPoint = cb.ScaleCalibration.OrderBy(p => p.Key).ElementAt(1).Value;
                cb.PostDrawItem = cb2;
                addMeterItem(cb);

                clsScaleItem cs = new clsScaleItem();
                cs.ParentID = ig.ID;
                cs.TopLeft = cb.TopLeft;
                cs.Size = cb.Size;
                cs.ReadingSource = cb.ReadingSource;
                cs.ZOrder = 4;
                cs.ShowType = true;
                addMeterItem(cs);

                clsSolidColour sc = new clsSolidColour();
                sc.ParentID = ig.ID;
                sc.TopLeft = new PointF(cb.TopLeft.X, cb.TopLeft.Y - _fHeight * 0.75f);
                sc.Size = new SizeF(cb.Size.Width, _fHeight + _fHeight * 0.75f);
                sc.Colour = System.Drawing.Color.FromArgb(32, 32, 32);
                sc.ZOrder = 1;
                addMeterItem(sc);

                fBottom = cb.TopLeft.Y + cb.Size.Height;

                ig.TopLeft = cb.TopLeft;
                ig.Size = new SizeF(cb.Size.Width, fBottom);
                ig.MeterType = MeterType.ALC;
                ig.Order = restoreIg == null ? numberOfMeterGroups() : restoreIg.Order;

                clsFadeCover fc = getFadeCover(ig.ID);
                if (fc != null) addMeterItem(fc);
                
                addMeterItem(ig);

                return cb.ID;
            }
            public string AddALCGainBar(int nMSupdate, float fTop, out float fBottom,  clsItemGroup restoreIg = null)
            {
                clsItemGroup ig = new clsItemGroup();
                if(restoreIg != null) ig.ID = restoreIg.ID;
                ig.ParentID = ID;

                clsBarItem cb = new clsBarItem();
                cb.ParentID = ig.ID;
                cb.Primary = true;
                cb.TopLeft = new PointF(_fPadX, fTop + _fPadY);
                cb.Size = new SizeF(1f - _fPadX * 2f, _fHeight);
                cb.ReadingSource = Reading.ALC_G;
                cb.MMIOVariableIndex = 0;
                cb.AttackRatio = 0.8f;
                cb.DecayRatio = 0.1f;
                cb.UpdateInterval = nMSupdate;
                cb.HistoryDuration = 2000;
                cb.ShowHistory = true;
                cb.MarkerColour = System.Drawing.Color.Yellow;
                cb.HistoryColour = System.Drawing.Color.FromArgb(128, System.Drawing.Color.LemonChiffon);
                cb.Style = clsBarItem.BarStyle.Line;
                cb.ScaleCalibration.Add(0, new PointF(0, 0));
                cb.ScaleCalibration.Add(20, new PointF(0.8f, 0));
                cb.ScaleCalibration.Add(25, new PointF(0.99f, 0));
                cb.ZOrder = 2;
                cb.FontColour = System.Drawing.Color.Yellow;
                cb.Value = cb.ScaleCalibration.OrderBy(p => p.Key).First().Key;
                cb.HighPoint = cb.ScaleCalibration.OrderBy(p => p.Key).ElementAt(1).Value;
                addMeterItem(cb);

                clsScaleItem cs = new clsScaleItem();
                cs.ParentID = ig.ID;
                cs.TopLeft = cb.TopLeft;
                cs.Size = cb.Size;
                cs.ReadingSource = cb.ReadingSource;
                cs.ZOrder = 4;
                cs.ShowType = true;
                addMeterItem(cs);

                clsSolidColour sc = new clsSolidColour();
                sc.ParentID = ig.ID;
                sc.TopLeft = new PointF(cb.TopLeft.X, cb.TopLeft.Y - _fHeight * 0.75f);
                sc.Size = new SizeF(cb.Size.Width, _fHeight + _fHeight * 0.75f);
                sc.Colour = System.Drawing.Color.FromArgb(32, 32, 32);
                sc.ZOrder = 1;
                addMeterItem(sc);

                fBottom = cb.TopLeft.Y + cb.Size.Height;

                ig.TopLeft = cb.TopLeft;
                ig.Size = new SizeF(cb.Size.Width, fBottom);
                ig.MeterType = MeterType.ALC_GAIN;
                ig.Order = restoreIg == null ? numberOfMeterGroups() : restoreIg.Order;

                clsFadeCover fc = getFadeCover(ig.ID);
                if (fc != null) addMeterItem(fc);
                
                addMeterItem(ig);

                return cb.ID;
            }
            public string AddALCGroupBar(int nMSupdate, float fTop, out float fBottom,  clsItemGroup restoreIg = null)
            {
                clsItemGroup ig = new clsItemGroup();
                if(restoreIg != null) ig.ID = restoreIg.ID;
                ig.ParentID = ID;

                clsBarItem cb = new clsBarItem();
                cb.ParentID = ig.ID;
                cb.Primary = true;
                cb.TopLeft = new PointF(_fPadX, fTop + _fPadY);
                cb.Size = new SizeF(1f - _fPadX * 2f, _fHeight);
                cb.ReadingSource = Reading.ALC_GROUP;
                cb.MMIOVariableIndex = 0;
                cb.AttackRatio = 0.8f;
                cb.DecayRatio = 0.1f;
                cb.UpdateInterval = nMSupdate;
                cb.HistoryDuration = 2000;
                cb.ShowHistory = true;
                cb.MarkerColour = System.Drawing.Color.Yellow;
                cb.HistoryColour = System.Drawing.Color.FromArgb(128, System.Drawing.Color.LemonChiffon);
                cb.Style = clsBarItem.BarStyle.Line;
                cb.ScaleCalibration.Add(-30, new PointF(0, 0));
                cb.ScaleCalibration.Add(0, new PointF(0.5f, 0));
                cb.ScaleCalibration.Add(25, new PointF(0.99f, 0));
                cb.ZOrder = 2;
                cb.FontColour = System.Drawing.Color.Yellow;
                cb.Value = cb.ScaleCalibration.OrderBy(p => p.Key).First().Key;
                cb.HighPoint = cb.ScaleCalibration.OrderBy(p => p.Key).ElementAt(1).Value;
                addMeterItem(cb);

                clsScaleItem cs = new clsScaleItem();
                cs.ParentID = ig.ID;
                cs.TopLeft = cb.TopLeft;
                cs.Size = cb.Size;
                cs.ReadingSource = cb.ReadingSource;
                cs.ZOrder = 4;
                cs.ShowType = true;
                addMeterItem(cs);

                clsSolidColour sc = new clsSolidColour();
                sc.ParentID = ig.ID;
                sc.TopLeft = new PointF(cb.TopLeft.X, cb.TopLeft.Y - _fHeight * 0.75f);
                sc.Size = new SizeF(cb.Size.Width, _fHeight + _fHeight * 0.75f);
                sc.Colour = System.Drawing.Color.FromArgb(32, 32, 32);
                sc.ZOrder = 1;
                addMeterItem(sc);

                fBottom = cb.TopLeft.Y + cb.Size.Height;

                ig.TopLeft = cb.TopLeft;
                ig.Size = new SizeF(cb.Size.Width, fBottom);
                ig.MeterType = MeterType.ALC_GROUP;
                ig.Order = restoreIg == null ? numberOfMeterGroups() : restoreIg.Order;

                clsFadeCover fc = getFadeCover(ig.ID);
                if (fc != null) addMeterItem(fc);
                
                addMeterItem(ig);

                return cb.ID;
            }
            public string AddCFCBar(int nMSupdate, float fTop, out float fBottom,  clsItemGroup restoreIg = null)
            {
                clsItemGroup ig = new clsItemGroup();
                if(restoreIg != null) ig.ID = restoreIg.ID;
                ig.ParentID = ID;

                clsBarItem cb2 = new clsBarItem();
                cb2.ParentID = ig.ID;
                cb2.Primary = true;
                cb2.TopLeft = new PointF(_fPadX, fTop + _fPadY);
                cb2.Size = new SizeF(1f - _fPadX * 2f, _fHeight);
                cb2.ReadingSource = Reading.CFC_PK;
                cb2.MMIOVariableIndex = 1;
                cb2.AttackRatio = 0.8f;
                cb2.DecayRatio = 0.1f;
                cb2.UpdateInterval = nMSupdate;
                cb2.HistoryDuration = 2000;
                cb2.ShowHistory = true;
                cb2.MarkerColour = System.Drawing.Color.Yellow;
                cb2.HistoryColour = System.Drawing.Color.FromArgb(128, System.Drawing.Color.PaleTurquoise);
                cb2.Style = clsBarItem.BarStyle.Line;
                cb2.ScaleCalibration.Add(-30, new PointF(0, 0));
                cb2.ScaleCalibration.Add(0, new PointF(0.665f, 0));
                cb2.ScaleCalibration.Add(12, new PointF(0.99f, 0));
                cb2.ZOrder = 2;
                cb2.FontColour = System.Drawing.Color.Yellow;                
                cb2.Value = cb2.ScaleCalibration.OrderBy(p => p.Key).First().Key;
                cb2.HighPoint = cb2.ScaleCalibration.OrderBy(p => p.Key).ElementAt(1).Value;
                addMeterItem(cb2);

                clsBarItem cb = new clsBarItem();
                cb.ParentID = ig.ID;
                cb.TopLeft = cb2.TopLeft;
                cb.Size = cb2.Size;
                cb.ReadingSource = Reading.CFC_AV;
                cb.MMIOVariableIndex = 0;
                cb.ShowPeakValue = false;
                cb.AttackRatio = 0.8f;
                cb.DecayRatio = 0.1f;
                cb.UpdateInterval = nMSupdate;
                cb.HistoryDuration = 0;
                cb.ShowHistory = false;
                cb.MarkerColour = System.Drawing.Color.DarkGray;
                cb.HistoryColour = System.Drawing.Color.FromArgb(128, System.Drawing.Color.PaleTurquoise);
                cb.Style = clsBarItem.BarStyle.Line;
                cb.ScaleCalibration.Add(-30, new PointF(0, 0));
                cb.ScaleCalibration.Add(0, new PointF(0.665f, 0));
                cb.ScaleCalibration.Add(12, new PointF(0.99f, 0));
                cb.ZOrder = 3;
                cb.ShowValue = false;
                cb.Value = cb.ScaleCalibration.OrderBy(p => p.Key).First().Key;
                cb.HighPoint = cb.ScaleCalibration.OrderBy(p => p.Key).ElementAt(1).Value;
                cb.PostDrawItem = cb2;
                addMeterItem(cb);

                clsScaleItem cs = new clsScaleItem();
                cs.ParentID = ig.ID;
                cs.TopLeft = cb.TopLeft;
                cs.Size = cb.Size;
                cs.ReadingSource = cb.ReadingSource;
                cs.ZOrder = 4;
                cs.ShowType = true;
                addMeterItem(cs);

                clsSolidColour sc = new clsSolidColour();
                sc.ParentID = ig.ID;
                sc.TopLeft = new PointF(cb.TopLeft.X, cb.TopLeft.Y - _fHeight * 0.75f);
                sc.Size = new SizeF(cb.Size.Width, _fHeight + _fHeight * 0.75f);
                sc.Colour = System.Drawing.Color.FromArgb(32, 32, 32);
                sc.ZOrder = 1;
                addMeterItem(sc);

                fBottom = cb.TopLeft.Y + cb.Size.Height;

                ig.TopLeft = cb.TopLeft;
                ig.Size = new SizeF(cb.Size.Width, fBottom);
                ig.MeterType = MeterType.CFC;
                ig.Order = restoreIg == null ? numberOfMeterGroups() : restoreIg.Order;

                clsFadeCover fc = getFadeCover(ig.ID);
                if (fc != null) addMeterItem(fc);
                
                addMeterItem(ig);

                return cb.ID;
            }
            public string AddCFCGainBar(int nMSupdate, float fTop, out float fBottom, clsItemGroup restoreIg = null)
            {
                clsItemGroup ig = new clsItemGroup();
                if (restoreIg != null) ig.ID = restoreIg.ID;
                ig.ParentID = ID;

                clsBarItem cb = new clsBarItem();
                cb.ParentID = ig.ID;
                cb.Primary = true;
                cb.TopLeft = new PointF(_fPadX, fTop + _fPadY);
                cb.Size = new SizeF(1f - _fPadX * 2f, _fHeight);
                cb.ReadingSource = Reading.CFC_G;
                cb.MMIOVariableIndex = 0;
                cb.AttackRatio = 0.8f;
                cb.DecayRatio = 0.1f;
                cb.UpdateInterval = nMSupdate;
                cb.HistoryDuration = 2000;
                cb.ShowHistory = true;
                cb.MarkerColour = System.Drawing.Color.Yellow;
                cb.HistoryColour = System.Drawing.Color.FromArgb(128, System.Drawing.Color.PaleTurquoise);
                cb.Style = clsBarItem.BarStyle.Line;
                cb.ScaleCalibration.Add(0, new PointF(0, 0));
                cb.ScaleCalibration.Add(20, new PointF(0.8f, 0));
                cb.ScaleCalibration.Add(25, new PointF(0.99f, 0));
                cb.ZOrder = 3;
                cb.FontColour = System.Drawing.Color.Yellow;
                cb.Value = cb.ScaleCalibration.OrderBy(p => p.Key).First().Key;
                cb.HighPoint = cb.ScaleCalibration.OrderBy(p => p.Key).ElementAt(1).Value;
                addMeterItem(cb);               

                clsScaleItem cs = new clsScaleItem();
                cs.ParentID = ig.ID;
                cs.TopLeft = cb.TopLeft;
                cs.Size = cb.Size;
                cs.ReadingSource = cb.ReadingSource;
                cs.ZOrder = 4;
                cs.ShowType = true;
                addMeterItem(cs);

                clsSolidColour sc = new clsSolidColour();
                sc.ParentID = ig.ID;
                sc.TopLeft = new PointF(cb.TopLeft.X, cb.TopLeft.Y - _fHeight * 0.75f);
                sc.Size = new SizeF(cb.Size.Width, _fHeight + _fHeight * 0.75f);
                sc.Colour = System.Drawing.Color.FromArgb(32, 32, 32);
                sc.ZOrder = 1;
                addMeterItem(sc);

                fBottom = cb.TopLeft.Y + cb.Size.Height;

                ig.TopLeft = cb.TopLeft;
                ig.Size = new SizeF(cb.Size.Width, fBottom);
                ig.MeterType = MeterType.CFC_GAIN;
                ig.Order = restoreIg == null ? numberOfMeterGroups() : restoreIg.Order;

                clsFadeCover fc = getFadeCover(ig.ID);
                if (fc != null) addMeterItem(fc);
                
                addMeterItem(ig);

                return cb.ID;
            }
            public string AddCompBar(int nMSupdate, float fTop, out float fBottom,  clsItemGroup restoreIg = null)
            {
                clsItemGroup ig = new clsItemGroup();
                if(restoreIg != null) ig.ID = restoreIg.ID;
                ig.ParentID = ID;

                clsBarItem cb2 = new clsBarItem();
                cb2.ParentID = ig.ID;
                cb2.Primary = true;
                cb2.TopLeft = new PointF(_fPadX, fTop + _fPadY);
                cb2.Size = new SizeF(1f - _fPadX * 2f, _fHeight);
                cb2.ReadingSource = Reading.COMP_PK;
                cb2.MMIOVariableIndex = 1;
                cb2.AttackRatio = 0.8f;
                cb2.DecayRatio = 0.1f;
                cb2.UpdateInterval = nMSupdate;
                cb2.HistoryDuration = 2000;
                cb2.ShowHistory = true;
                cb2.MarkerColour = System.Drawing.Color.Yellow;
                cb2.HistoryColour = System.Drawing.Color.FromArgb(128, System.Drawing.Color.PeachPuff);
                cb2.Style = clsBarItem.BarStyle.Line;
                cb2.ScaleCalibration.Add(-30, new PointF(0, 0));
                cb2.ScaleCalibration.Add(0, new PointF(0.665f, 0));
                cb2.ScaleCalibration.Add(12, new PointF(0.99f, 0));
                cb2.ZOrder = 2;
                cb2.FontColour = System.Drawing.Color.Yellow;
                cb2.Value = cb2.ScaleCalibration.OrderBy(p => p.Key).First().Key;
                cb2.HighPoint = cb2.ScaleCalibration.OrderBy(p => p.Key).ElementAt(1).Value;
                addMeterItem(cb2);

                clsBarItem cb = new clsBarItem();
                cb.ParentID = ig.ID;
                cb.TopLeft = cb2.TopLeft;
                cb.Size = cb2.Size;
                cb.ReadingSource = Reading.COMP;
                cb.MMIOVariableIndex = 0;
                cb.ShowPeakValue = false;
                cb.AttackRatio = 0.8f;
                cb.DecayRatio = 0.1f;
                cb.UpdateInterval = nMSupdate;
                cb.HistoryDuration = 2000;
                cb.ShowHistory = false;
                cb.MarkerColour = System.Drawing.Color.DarkGray;
                cb.HistoryColour = System.Drawing.Color.FromArgb(128, System.Drawing.Color.PeachPuff);
                cb.Style = clsBarItem.BarStyle.Line;
                cb.ScaleCalibration.Add(-30, new PointF(0, 0));
                cb.ScaleCalibration.Add(0, new PointF(0.665f, 0));
                cb.ScaleCalibration.Add(12, new PointF(0.99f, 0));
                cb.ZOrder = 3;
                cb.ShowValue = false;
                cb.Value = cb.ScaleCalibration.OrderBy(p => p.Key).First().Key;
                cb.HighPoint = cb.ScaleCalibration.OrderBy(p => p.Key).ElementAt(1).Value;
                cb.PostDrawItem = cb2;
                addMeterItem(cb);

                clsScaleItem cs = new clsScaleItem();
                cs.ParentID = ig.ID;
                cs.TopLeft = cb.TopLeft;
                cs.Size = cb.Size;
                cs.ReadingSource = cb.ReadingSource;
                cs.ZOrder = 4;
                cs.ShowType = true;
                addMeterItem(cs);

                clsSolidColour sc = new clsSolidColour();
                sc.ParentID = ig.ID;
                sc.TopLeft = new PointF(cb.TopLeft.X, cb.TopLeft.Y - _fHeight * 0.75f);
                sc.Size = new SizeF(cb.Size.Width, _fHeight + _fHeight * 0.75f);
                sc.Colour = System.Drawing.Color.FromArgb(32, 32, 32);
                sc.ZOrder = 1;
                addMeterItem(sc);

                fBottom = cb.TopLeft.Y + cb.Size.Height;

                ig.TopLeft = cb.TopLeft;
                ig.Size = new SizeF(cb.Size.Width, fBottom);
                ig.MeterType = MeterType.COMP;
                ig.Order = restoreIg == null ? numberOfMeterGroups() : restoreIg.Order;

                clsFadeCover fc = getFadeCover(ig.ID);
                if (fc != null) addMeterItem(fc);
                
                addMeterItem(ig);

                return cb.ID;
            }
            //CPDR is the same as comp
            //public string AddCompanderBar(int nMSupdate, float fTop, out float fBottom, clsItemGroup restoreIg = null)
            //{
            //    clsItemGroup ig = new clsItemGroup();
            //    if (restoreIg != null) ig.ID = restoreIg.ID;
            //    ig.ParentID = ID;

            //    clsBarItem cb2 = new clsBarItem();
            //    cb2.ParentID = ig.ID;
            //    cb2.Primary = true;
            //    cb2.TopLeft = new PointF(_fPadX, fTop + _fPadY);
            //    cb2.Size = new SizeF(1f - _fPadX * 2f, _fHeight);
            //    cb2.ReadingSource = Reading.CPDR_PK;
            //    cb2.MMIOVariableIndex = 1;
            //    cb2.AttackRatio = 0.8f;
            //    cb2.DecayRatio = 0.1f;
            //    cb2.UpdateInterval = nMSupdate;
            //    cb2.HistoryDuration = 2000;
            //    cb2.ShowHistory = true;
            //    cb2.MarkerColour = System.Drawing.Color.Yellow;
            //    cb2.HistoryColour = System.Drawing.Color.FromArgb(128, System.Drawing.Color.PeachPuff);
            //    cb2.Style = clsBarItem.BarStyle.Line;
            //    cb2.ScaleCalibration.Add(-30, new PointF(0, 0));
            //    cb2.ScaleCalibration.Add(0, new PointF(0.665f, 0));
            //    cb2.ScaleCalibration.Add(12, new PointF(0.99f, 0));
            //    cb2.ZOrder = 2;
            //    cb2.FontColour = System.Drawing.Color.Yellow;
            //    cb2.Value = cb2.ScaleCalibration.OrderBy(p => p.Key).First().Key;
            //    cb2.HighPoint = cb2.ScaleCalibration.OrderBy(p => p.Key).ElementAt(1).Value;
            //    addMeterItem(cb2);

            //    clsBarItem cb = new clsBarItem();
            //    cb.ParentID = ig.ID;
            //    cb.TopLeft = cb2.TopLeft;
            //    cb.Size = cb2.Size;
            //    cb.ReadingSource = Reading.CPDR;
            //    cb.MMIOVariableIndex = 0;
            //    cb.ShowPeakValue = false;
            //    cb.AttackRatio = 0.8f;
            //    cb.DecayRatio = 0.1f;
            //    cb.UpdateInterval = nMSupdate;
            //    cb.HistoryDuration = 2000;
            //    cb.ShowHistory = false;
            //    cb.MarkerColour = System.Drawing.Color.DarkGray;
            //    cb.HistoryColour = System.Drawing.Color.FromArgb(128, System.Drawing.Color.PeachPuff);
            //    cb.Style = clsBarItem.BarStyle.Line;
            //    cb.ScaleCalibration.Add(-30, new PointF(0, 0));
            //    cb.ScaleCalibration.Add(0, new PointF(0.665f, 0));
            //    cb.ScaleCalibration.Add(12, new PointF(0.99f, 0));
            //    cb.ZOrder = 3;
            //    cb.ShowValue = false;
            //    cb.Value = cb.ScaleCalibration.OrderBy(p => p.Key).First().Key;
            //    cb.HighPoint = cb.ScaleCalibration.OrderBy(p => p.Key).ElementAt(1).Value;
            //    cb.PostDrawItem = cb2;
            //    addMeterItem(cb);

            //    clsScaleItem cs = new clsScaleItem();
            //    cs.ParentID = ig.ID;
            //    cs.TopLeft = cb.TopLeft;
            //    cs.Size = cb.Size;
            //    cs.ReadingSource = cb.ReadingSource;
            //    cs.ZOrder = 4;
            //    cs.ShowType = true;
            //    addMeterItem(cs);

            //    clsSolidColour sc = new clsSolidColour();
            //    sc.ParentID = ig.ID;
            //    sc.TopLeft = new PointF(cb.TopLeft.X, cb.TopLeft.Y - _fHeight * 0.75f);
            //    sc.Size = new SizeF(cb.Size.Width, _fHeight + _fHeight * 0.75f);
            //    sc.Colour = System.Drawing.Color.FromArgb(32, 32, 32);
            //    sc.ZOrder = 1;
            //    addMeterItem(sc);

            //    fBottom = cb.TopLeft.Y + cb.Size.Height;

            //    ig.TopLeft = cb.TopLeft;
            //    ig.Size = new SizeF(cb.Size.Width, fBottom);
            //    ig.MeterType = MeterType.CPDR;
            //    ig.Order = restoreIg == null ? numberOfMeterGroups() : restoreIg.Order;

            //    clsFadeCover fc = getFadeCover(ig.ID);
            //    if (fc != null) addMeterItem(fc);

            //    addMeterItem(ig);

            //    return cb.ID;
            //}
            public string AddPWRBar(int nMSupdate, float fTop, out float fBottom,  clsItemGroup restoreIg = null)
            {
                return AddPWRBar(nMSupdate, fTop, out fBottom, Reading.PWR, restoreIg);
            }
            public string AddREVPWRBar(int nMSupdate, float fTop, out float fBottom,  clsItemGroup restoreIg = null)
            {
                return AddPWRBar(nMSupdate, fTop, out fBottom, Reading.REVERSE_PWR, restoreIg);
            }
            public string AddPWRBar(int nMSupdate, float fTop, out float fBottom, Reading reading,  clsItemGroup restoreIg = null)
            {
                clsItemGroup ig = new clsItemGroup();
                if(restoreIg != null) ig.ID = restoreIg.ID;
                ig.ParentID = ID;

                clsBarItem cb = new clsBarItem();
                cb.ParentID = ig.ID;
                cb.Primary = true;
                cb.TopLeft = new PointF(_fPadX, fTop + _fPadY);
                cb.Size = new SizeF(1f - _fPadX * 2f, _fHeight);
                cb.ReadingSource = reading;
                cb.MMIOVariableIndex = 0;
                cb.AttackRatio = 0.8f;
                cb.DecayRatio = 0.1f;
                cb.UpdateInterval = nMSupdate;
                cb.HistoryDuration = 2000;
                cb.ShowHistory = true;
                cb.MarkerColour = System.Drawing.Color.Yellow;
                cb.HistoryColour = System.Drawing.Color.FromArgb(128, System.Drawing.Color.Red);
                cb.Style = clsBarItem.BarStyle.Line;

                //switch (CurrentPowerRating)
                //{
                //    case 500:
                //        {
                //            cb.ScaleCalibration.Add(0, new PointF(0, 0));
                //            cb.ScaleCalibration.Add(50, new PointF(0.1875f, 0));
                //            cb.ScaleCalibration.Add(100, new PointF(0.375f, 0));
                //            cb.ScaleCalibration.Add(250, new PointF(0.5625f, 0));
                //            cb.ScaleCalibration.Add(500, new PointF(0.75f, 0));
                //            cb.ScaleCalibration.Add(600, new PointF(0.99f, 0));
                //        }
                //        break;
                //    case 200:
                //        {
                //            cb.ScaleCalibration.Add(0, new PointF(0, 0));
                //            cb.ScaleCalibration.Add(10, new PointF(0.1875f, 0));
                //            cb.ScaleCalibration.Add(20, new PointF(0.375f, 0));
                //            cb.ScaleCalibration.Add(100, new PointF(0.5625f, 0));
                //            cb.ScaleCalibration.Add(200, new PointF(0.75f, 0));
                //            cb.ScaleCalibration.Add(240, new PointF(0.99f, 0));
                //        }
                //        break;
                    //case 100:
                    //    {
                            cb.ScaleCalibration.Add(0, new PointF(0, 0));
                            cb.ScaleCalibration.Add(5, new PointF(0.1875f, 0));
                            cb.ScaleCalibration.Add(10, new PointF(0.375f, 0));
                            cb.ScaleCalibration.Add(50, new PointF(0.5625f, 0));
                            cb.ScaleCalibration.Add(100, new PointF(0.75f, 0));
                            cb.ScaleCalibration.Add(120, new PointF(0.99f, 0));
                    //    }
                //        break;
                //    case 30:
                //        {
                //            cb.ScaleCalibration.Add(0, new PointF(0, 0));
                //            cb.ScaleCalibration.Add(5, new PointF(0.1875f, 0));
                //            cb.ScaleCalibration.Add(10, new PointF(0.375f, 0));
                //            cb.ScaleCalibration.Add(20, new PointF(0.5625f, 0));
                //            cb.ScaleCalibration.Add(30, new PointF(0.75f, 0));
                //            cb.ScaleCalibration.Add(50, new PointF(0.99f, 0));
                //        }
                //        break;
                //    case 15:
                //        {
                //            cb.ScaleCalibration.Add(0, new PointF(0, 0));
                //            cb.ScaleCalibration.Add(1, new PointF(0.1875f, 0));
                //            cb.ScaleCalibration.Add(5, new PointF(0.375f, 0));
                //            cb.ScaleCalibration.Add(10, new PointF(0.5625f, 0));
                //            cb.ScaleCalibration.Add(15, new PointF(0.75f, 0));
                //            cb.ScaleCalibration.Add(25, new PointF(0.99f, 0));
                //        }
                //        break;
                //    case 1:
                //        {
                //            cb.ScaleCalibration.Add(0, new PointF(0, 0));
                //            cb.ScaleCalibration.Add(0.1f, new PointF(0.1875f, 0));
                //            cb.ScaleCalibration.Add(0.25f, new PointF(0.375f, 0));
                //            cb.ScaleCalibration.Add(0.5f, new PointF(0.5625f, 0));
                //            cb.ScaleCalibration.Add(0.8f, new PointF(0.75f, 0));
                //            cb.ScaleCalibration.Add(1f, new PointF(0.99f, 0));

                //        }
                //        break;
                //}

                cb.NormaliseTo100W = true;
                cb.FontColour = System.Drawing.Color.Yellow;
                cb.ZOrder = 2;
                cb.Value = cb.ScaleCalibration.OrderBy(p => p.Key).First().Key;
                cb.HighPoint = cb.ScaleCalibration.OrderBy(p => p.Key).ElementAt(4).Value;
                addMeterItem(cb);

                clsScaleItem cs = new clsScaleItem();
                cs.ParentID = ig.ID;
                cs.TopLeft = cb.TopLeft;
                cs.Size = cb.Size;
                cs.ReadingSource = cb.ReadingSource;
                cs.ZOrder = 4;
                cs.ShowType = true;
                addMeterItem(cs);

                clsSolidColour sc = new clsSolidColour();
                sc.ParentID = ig.ID;
                sc.TopLeft = new PointF(cb.TopLeft.X, cb.TopLeft.Y - _fHeight * 0.75f);
                sc.Size = new SizeF(cb.Size.Width, _fHeight + _fHeight * 0.75f);
                sc.Colour = System.Drawing.Color.FromArgb(32, 32, 32);
                sc.ZOrder = 1;
                addMeterItem(sc);

                fBottom = cb.TopLeft.Y + cb.Size.Height;

                ig.TopLeft = cb.TopLeft;
                ig.Size = new SizeF(cb.Size.Width, fBottom);
                if (reading == Reading.PWR)
                    ig.MeterType = MeterType.PWR;
                else
                    ig.MeterType = MeterType.REVERSE_PWR;
                ig.Order = restoreIg == null ? numberOfMeterGroups() : restoreIg.Order;

                clsFadeCover fc = getFadeCover(ig.ID);
                if (fc != null) addMeterItem(fc);
                
                addMeterItem(ig);

                return cb.ID;
            }
            public string AddSWRBar(int nMSupdate, float fTop, out float fBottom,  clsItemGroup restoreIg = null)
            {
                clsItemGroup ig = new clsItemGroup();
                if(restoreIg != null) ig.ID = restoreIg.ID;
                ig.ParentID = ID;

                clsBarItem cb = new clsBarItem();
                cb.ParentID = ig.ID;
                cb.Primary = true;
                cb.TopLeft = new PointF(_fPadX, fTop + _fPadY);
                cb.Size = new SizeF(1f - _fPadX * 2f, _fHeight);
                cb.ReadingSource = Reading.SWR;
                cb.MMIOVariableIndex = 0;
                cb.AttackRatio = 0.8f;
                cb.DecayRatio = 0.1f;
                cb.UpdateInterval = nMSupdate;
                cb.HistoryDuration = 2000;
                cb.ShowHistory = true;
                cb.MarkerColour = System.Drawing.Color.Yellow;
                cb.HistoryColour = System.Drawing.Color.FromArgb(128, System.Drawing.Color.Orange);
                cb.Style = clsBarItem.BarStyle.Line;
                cb.ScaleCalibration.Add(1, new PointF(0, 0));
                cb.ScaleCalibration.Add(1.5f, new PointF(0.25f, 0));
                cb.ScaleCalibration.Add(2, new PointF(0.5f, 0));
                cb.ScaleCalibration.Add(3, new PointF(0.75f, 0));
                cb.ScaleCalibration.Add(5, new PointF(0.99f, 0));
                cb.FontColour = System.Drawing.Color.Yellow;
                cb.ZOrder = 2;
                cb.Value = cb.ScaleCalibration.OrderBy(p => p.Key).First().Key;
                cb.HighPoint = cb.ScaleCalibration.OrderBy(p => p.Key).ElementAt(3).Value;
                addMeterItem(cb);

                clsScaleItem cs = new clsScaleItem();
                cs.ParentID = ig.ID;
                cs.TopLeft = cb.TopLeft;
                cs.Size = cb.Size;
                cs.ReadingSource = cb.ReadingSource;
                cs.ZOrder = 4;
                cs.ShowType = true;
                addMeterItem(cs);

                clsSolidColour sc = new clsSolidColour();
                sc.ParentID = ig.ID;
                sc.TopLeft = new PointF(cb.TopLeft.X, cb.TopLeft.Y - _fHeight * 0.75f);
                sc.Size = new SizeF(cb.Size.Width, _fHeight + _fHeight * 0.75f);
                sc.Colour = System.Drawing.Color.FromArgb(32, 32, 32);
                sc.ZOrder = 1;
                addMeterItem(sc);

                fBottom = cb.TopLeft.Y + cb.Size.Height;

                ig.TopLeft = cb.TopLeft;
                ig.Size = new SizeF(cb.Size.Width, fBottom);
                ig.MeterType = MeterType.SWR;
                ig.Order = restoreIg == null ? numberOfMeterGroups() : restoreIg.Order;

                clsFadeCover fc = getFadeCover(ig.ID);
                if (fc != null) addMeterItem(fc);
                
                addMeterItem(ig);

                return cb.ID;
            }
            public string AddVFODisplay(int nMSupdate, float fTop, out float fBottom, clsItemGroup restoreIg = null)
            {
                clsItemGroup ig = new clsItemGroup();
                if (restoreIg != null) ig.ID = restoreIg.ID;
                ig.ParentID = ID;

                clsSolidColour sc = new clsSolidColour();
                sc.ParentID = ig.ID;
                sc.TopLeft = new PointF(_fPadX, fTop + _fPadY - _fHeight * 0.75f);
                sc.Size = new SizeF(0.5f - _fPadX - (_fPadX * 0.5f), _fHeight + _fHeight * 0.75f);
                sc.Colour = System.Drawing.Color.FromArgb(32, 32, 32);
                sc.ZOrder = 1;
                addMeterItem(sc);

                clsSolidColour sc2 = new clsSolidColour();
                sc2.ParentID = ig.ID;
                sc2.TopLeft = new PointF(0.5f + (_fPadX * 0.5f), fTop + _fPadY - _fHeight * 0.75f);
                sc2.Size = new SizeF(0.5f - _fPadX - (_fPadX * 0.5f), _fHeight + _fHeight * 0.75f);
                sc2.Colour = System.Drawing.Color.FromArgb(32, 32, 32);
                sc2.ZOrder = 1;
                addMeterItem(sc2);

                //
                clsVfoDisplay vfo = new clsVfoDisplay(this);
                vfo.ParentID = ig.ID;
                vfo.TopLeft = sc.TopLeft;
                vfo.Size = new SizeF(1f - _fPadX * 2f, _fHeight + _fHeight * 0.75f);
                //vfo.UpdateInterval = nMSupdate;  //Fixed by constructor, should not be changed by user
                //vfo.Primary = true;
                vfo.ZOrder = 2;
                addMeterItem(vfo);
                //

                fBottom = sc.TopLeft.Y + sc.Size.Height;

                ig.TopLeft = sc.TopLeft;
                ig.Size = new SizeF(sc.Size.Width + sc2.Size.Width + _fPadX, fBottom);
                ig.MeterType = MeterType.VFO_DISPLAY;
                ig.Order = restoreIg == null ? numberOfMeterGroups() : restoreIg.Order;

                clsFadeCover fc = getFadeCover(ig.ID);
                if (fc != null) addMeterItem(fc);
                
                addMeterItem(ig);

                return sc.ID;
            }
            public string AddClock(int nMSupdate, float fTop, out float fBottom, clsItemGroup restoreIg = null)
            {
                clsItemGroup ig = new clsItemGroup();
                if (restoreIg != null) ig.ID = restoreIg.ID;
                ig.ParentID = ID;

                clsSolidColour sc = new clsSolidColour();
                sc.ParentID = ig.ID;
                sc.TopLeft = new PointF(_fPadX, fTop + _fPadY - _fHeight * 0.75f);
                sc.Size = new SizeF(0.5f - _fPadX - (_fPadX * 0.5f), _fHeight + _fHeight * 0.75f);
                sc.Colour = System.Drawing.Color.FromArgb(32, 32, 32);
                sc.ZOrder = 1;
                addMeterItem(sc);

                clsSolidColour sc2 = new clsSolidColour();
                sc2.ParentID = ig.ID;
                sc2.TopLeft = new PointF(0.5f + (_fPadX * 0.5f), fTop + _fPadY - _fHeight * 0.75f);
                sc2.Size = new SizeF(0.5f - _fPadX - (_fPadX * 0.5f), _fHeight + _fHeight * 0.75f);
                sc2.Colour = System.Drawing.Color.FromArgb(32, 32, 32);
                sc2.ZOrder = 1;
                addMeterItem(sc2);

                //
                clsClock clk = new clsClock();
                clk.ParentID = ig.ID;
                clk.TopLeft = sc.TopLeft;
                clk.Size = new SizeF(1f - _fPadX * 2f, _fHeight + _fHeight * 0.75f);
                //vfo.UpdateInterval = nMSupdate;  //Fixed by constructor, can not be changed by user
                clk.Primary = true;
                clk.ZOrder = 2;
                addMeterItem(clk);
                //

                fBottom = sc.TopLeft.Y + sc.Size.Height;

                ig.TopLeft = sc.TopLeft;
                ig.Size = new SizeF(sc.Size.Width + sc2.Size.Width + _fPadX, fBottom);
                ig.MeterType = MeterType.CLOCK;
                ig.Order = restoreIg == null ? numberOfMeterGroups() : restoreIg.Order;

                clsFadeCover fc = getFadeCover(ig.ID);
                if (fc != null) addMeterItem(fc);
                
                addMeterItem(ig);

                return sc.ID;
            }
            //public string AddSpectrum(int nMSupdate, float fTop, out float fBottom, clsItemGroup restoreIg = null)
            //{
            //    clsItemGroup ig = new clsItemGroup();
            //    if (restoreIg != null) ig.ID = restoreIg.ID;
            //    ig.ParentID = ID;

            //    clsSolidColour sc;
            //    sc = new clsSolidColour();
            //    sc.ParentID = ig.ID;
            //    sc.TopLeft = new PointF(_fPadX, fTop + _fPadY - _fHeight * 0.75f);
            //    sc.Size = new SizeF(1f - _fPadX * 2f, (_fHeight + _fHeight * 0.75f) * 8f);
            //    sc.Colour = System.Drawing.Color.FromArgb(32, 32, 32);
            //    sc.ZOrder = 1;
            //    addMeterItem(sc);

            //    clsSpectrum cst = new clsSpectrum();
            //    cst.ParentID = ig.ID;
            //    cst.Primary = true;
            //    cst.TopLeft = sc.TopLeft;
            //    cst.Size = sc.Size;
            //    cst.UpdateInterval = nMSupdate;
            //    cst.ZOrder = 2;
            //    addMeterItem(cst);

            //    //clsClickBox clb = new clsClickBox();
            //    //clb.ParentID = ig.ID;
            //    //clb.TopLeft = sc.TopLeft;
            //    //clb.Size = sc.Size;
            //    //clb.ChangesGroup = false;
            //    //clb.PerformsIncDec = true;
            //    //clb.RelatedMeterItem = cst;
            //    //addMeterItem(clb);

            //    fBottom = sc.TopLeft.Y + sc.Size.Height;

            //    ig.TopLeft = sc.TopLeft;
            //    ig.Size = new SizeF(sc.Size.Width, fBottom);
            //    ig.MeterType = MeterType.SPECTRUM;
            //    ig.Order = restoreIg == null ? numberOfMeterGroups() : restoreIg.Order;

            //    clsFadeCover fc = getFadeCover(ig.ID);
            //    if (fc != null) addMeterItem(fc);

            //    addMeterItem(ig);

            //    return sc.ID;
            //}

            //private string AddHistory(int nMSupdate, float fTop, out float fBottom, clsItemGroup restoreIg = null)
            //{
            //    clsItemGroup ig = new clsItemGroup();
            //    if (restoreIg != null) ig.ID = restoreIg.ID;
            //    ig.ParentID = ID;

            //    //
            //    clsHistoryItem hi = new clsHistoryItem();
            //    hi.ParentID = ig.ID;
            //    hi.Primary = true;
            //    hi.TopLeft = new PointF(_fPadX, fTop + _fPadY - (_fHeight * 0.75f));
            //    hi.Size = new SizeF(1f - _fPadX * 2f, _fHeight * 4f);
            //    hi.ReadingSource = Reading.AVG_SIGNAL_STRENGTH;
            //    hi.UpdateInterval = nMSupdate;
            //    hi.HistoryDuration = 4000;
            //    hi.ShowHistory = true;
            //    hi.HistoryColour = System.Drawing.Color.FromArgb(128, System.Drawing.Color.Orange);
            //    hi.ZOrder = 1;
            //    hi.ScaleCalibration.Add(-127f, new PointF(0, 0));
            //    hi.ScaleCalibration.Add(-13f, new PointF(0, 1f));
            //    addMeterItem(hi);
            //    //

            //    fBottom = hi.TopLeft.Y + hi.Size.Height;

            //    ig.TopLeft = hi.TopLeft;
            //    ig.Size = new SizeF(hi.Size.Width, fBottom);
            //    ig.MeterType = MeterType.HISTORY;
            //    ig.Order = restoreIg == null ? numberOfMeterGroups() : restoreIg.Order;

            //    clsFadeCover fc = getFadeCover(ig.ID);
            //    if (fc != null) addMeterItem(fc);

            //    addMeterItem(ig);

            //    return hi.ID;
            //}
            #endregion
            public clsMeter(int rx, string sName, float XRatio = 1f, float YRatio = 1f)
            {
                // constructor
                _sId = Guid.NewGuid().ToString();
                _rx = rx;
                _name = sName;
                _enabled = true;
                _quickestRXUpdate = 250;
                _quickestTXUpdate = 250;
                _split = false;
                _vfoA = 0;
                _vfoB = 0;
                _vfoSub = 0;
                _txVfoB = false;
                _bandVfoA = Band.FIRST;
                _bandVfoB = Band.FIRST;
                _bandVfoASub = Band.FIRST;
                _modeVfoA = DSPMode.FIRST;
                _modeVfoB = DSPMode.FIRST;
                _filterVfoA = Filter.FIRST;
                _filterVfoB = Filter.FIRST;
                _filterVfoAname = "";
                _filterVfoBname = "";
                _rx2Enabled = false;
                _multiRxEnabled = false;
                _qso_start = DateTime.Now;
                _qso_end = DateTime.Now;

                _txeqEnabled = true;
                _levelerEnabled = true;
                _cfcEnabled = true;
                _compandEnabled = true;

                _quickSplitEnabled = false;

                _fPadX = 0.02f;
                _fPadY = 0.05f;
                _fHeight = 0.05f;

                _XRatio = XRatio;
                _YRatio = YRatio;

                _meterItems = new Dictionary<string, clsMeterItem>();
                _sortedMeterItemsForZOrder = null; // only after setupSortedZOrder is called
                _displayGroups = new List<string>();
                _displayGroup = 0;
            }
            public void ZeroOut(bool bRxReadings, bool bTxReadings)
            {
                lock (_meterItemsLock)
                {
                    foreach (KeyValuePair<string, clsMeterItem> mis in _meterItems)
                    {
                        clsMeterItem mi = mis.Value;

                        mi.ClearHistory();

                        bool bOk = mi.ZeroOut(out float value, _rx); // get value to zero out the meter, returns false if no scale

                        if (bOk)
                        {
                            // //[2.10.1.0] MW0LGE only want to do this if value is lower than current reading
                            //float reading = getReading(RX, mi.ReadingSource);
                            //bool bUpdate = (value < reading) || (reading == -200f); // -200f is init or no value state
                            //if (bUpdate)
                            //{
                                if(bRxReadings || bTxReadings) setReadingForced(RX, mi.ReadingSource, value);
                            //}
                        }
                    }
                }
            }
            public void KeyDown(Keys keycode)
            {
                lock (_meterItemsLock)
                {
                    foreach (KeyValuePair<string, clsMeterItem> mis in _meterItems)
                    {
                        clsMeterItem mi = mis.Value;
                        mi.KeyDown(keycode);
                    }
                }
            }
            public void KeyUp(Keys keycode)
            {
                lock (_meterItemsLock)
                {
                    foreach (KeyValuePair<string, clsMeterItem> mis in _meterItems)
                    {
                        clsMeterItem mi = mis.Value;
                        mi.KeyUp(keycode);
                    }
                }
            }
            public bool TryParse(string str)
            {
                bool bOk = false;
                int rx=0, displaygroup=0;
                float xRatio=0, yRatio=0, fPadX=0, fPadY=0, fHeight=0;

                if (str != "")
                {
                    string[] tmp = str.Split('|');
                    if (tmp.Length == 9)
                    {
                        bOk = tmp[0] != "";
                        if (bOk) ID = tmp[0];
                        if (bOk) _name = tmp[1];
                        if (bOk) int.TryParse(tmp[2], out rx);
                        if (bOk) RX = rx;
                        if (bOk) bOk = float.TryParse(tmp[3], out xRatio);
                        if (bOk) bOk = float.TryParse(tmp[4], out yRatio);
                        if (bOk)
                        {
                            _XRatio = xRatio;
                            _YRatio = yRatio;
                        }
                        if (bOk) int.TryParse(tmp[5], out displaygroup);
                        if (bOk) _displayGroup = displaygroup;
                        if (bOk) bOk = float.TryParse(tmp[6], out fPadX);
                        if (bOk) bOk = float.TryParse(tmp[7], out fPadY);
                        if (bOk) bOk = float.TryParse(tmp[8], out fHeight);
                        if (bOk)
                        {
                            _fPadX = fPadX;
                            _fPadY = fPadY;
                            _fHeight = fHeight;
                        }

                    }
                }

                return bOk;
            }
            private int numberOfMeterGroups()
            {
                lock (_meterItemsLock)
                {
                    if (_meterItems == null) return 0;

                    Dictionary<string, clsMeterItem> items = _meterItems.Where(o => o.Value.ItemType == clsMeterItem.MeterItemType.ITEM_GROUP).ToDictionary(x => x.Key, x => x.Value);

                    return items.Count;
                }
            }
            private void removeMeterItem(string sId, bool bRebuild = false)
            {
                lock (_meterItemsLock)
                {
                    // remove this item, plus all items that are children

                    Dictionary<string, clsMeterItem> items = itemsFromID(sId);
                    if (items == null || items.Count == 0) return;

                    List<string> toRemove = new List<string>();
                    foreach (KeyValuePair<string, clsMeterItem> kvp in items)
                    {
                        toRemove.Add(kvp.Value.ID);
                    }
                    items.Clear();

                    foreach (string id in toRemove)
                    {
                        _meterItems[id].Removing();
                        _meterItems.Remove(id);
                    }
                    toRemove.Clear();

                    if (bRebuild) Rebuild();
                }
            }
            public void RemoveMeterType(MeterType mt, int order, bool bRebuild = false)
            {
                lock (_meterItemsLock)
                {
                    if (_meterItems == null) return;

                    //special for ananMM
                    if(mt == MeterType.ANANMM)
                    {
                        RemoveDisplayGroup("ALL");
                        RemoveDisplayGroup("PWR/SWR");
                        RemoveDisplayGroup("Comp");
                        RemoveDisplayGroup("ALC");
                        RemoveDisplayGroup("Volts/Amps");
                    }
                    //
                    Dictionary<string, clsMeterItem> items = _meterItems.Where(o => o.Value.ItemType == clsMeterItem.MeterItemType.ITEM_GROUP).ToDictionary(x => x.Key, x => x.Value);
                    foreach(KeyValuePair<string, clsMeterItem> kvp in items)
                    {
                        int nOrder = -1;

                        clsItemGroup ig = kvp.Value as clsItemGroup;
                        if (ig != null && ig.MeterType == mt && ig.Order == order) {
                            nOrder = ig.Order;
                            removeMeterItem(ig.ID, false);
                            //if (mt == MeterType.SPACER || mt == MeterType.TEXT_OVERLAY) order = -9999; // only remove the single
                            order = -9999; // prevents any more from being removed
                        }

                        if(nOrder >= 0)
                        {
                            Dictionary<string, clsMeterItem> tmpItems = _meterItems.Where(o => o.Value.ItemType == clsMeterItem.MeterItemType.ITEM_GROUP).ToDictionary(x => x.Key, x => x.Value);
                            foreach(KeyValuePair<string, clsMeterItem> tmpKvp in tmpItems){
                                clsItemGroup tmpIg = tmpKvp.Value as clsItemGroup;
                                if (tmpIg != null && tmpIg.Order > nOrder)
                                    tmpIg.Order--;
                            }
                        }
                    }

                    if (bRebuild) Rebuild();
                }
            }
            public void RemoveAllMeterTypes(bool bRebuild = false)
            {
                lock (_meterItemsLock)
                {
                    if (_meterItems == null) return;

                    Dictionary<string, clsMeterItem> items = _meterItems.Where(o => o.Value.ItemType == clsMeterItem.MeterItemType.ITEM_GROUP).ToDictionary(x => x.Key, x => x.Value);
                    foreach (KeyValuePair<string, clsMeterItem> kvp in items)
                    {
                        clsItemGroup ig = kvp.Value as clsItemGroup;
                        if (ig != null)
                        {
                            removeMeterItem(ig.ID, false);
                        }
                    }

                    if (bRebuild) Rebuild();
                }
            }
            public bool HasMeterType(MeterType mt)
            {
                lock (_meterItemsLock)
                { 
                    if (_meterItems == null) return false;

                    //IEnumerable<KeyValuePair<string, clsMeterItem>> items = _meterItems.Where(o => o.Value.ItemType == MeterItemType.ITEM_GROUP);
                    Dictionary<string, clsMeterItem> items = _meterItems.Where(o => o.Value.ItemType == clsMeterItem.MeterItemType.ITEM_GROUP).ToDictionary(x => x.Key, x => x.Value);
                    foreach (KeyValuePair<string, clsMeterItem> kvp in items)
                    {
                        clsItemGroup ig = kvp.Value as clsItemGroup;
                        if (ig != null && ig.MeterType == mt) return true;
                    }

                    return false;
                }
            }
            public void DisableMeterType(MeterType mt, bool bDisable)
            {
                lock (_meterItemsLock)
                {
                    Dictionary<string, clsMeterItem> items = _meterItems.Where(o => o.Value.ItemType == clsMeterItem.MeterItemType.ITEM_GROUP).ToDictionary(x => x.Key, x => x.Value);
                    
                    foreach (KeyValuePair<string, clsMeterItem> kvp in items)
                    {
                        clsItemGroup ig = kvp.Value as clsItemGroup;

                        if (ig.MeterType == mt)
                        {
                            Dictionary<string, clsMeterItem> mtItems = itemsFromID(ig.ID, false, true);

                            foreach (KeyValuePair<string, clsMeterItem> kvpmi in mtItems)
                            {
                                clsMeterItem mi = kvpmi.Value as clsMeterItem;
                                mi.Disabled = bDisable;
                            }
                        }
                    }
                }
            }
            public string MeterGroupID(MeterType mt, int order = -1)
            {
                lock (_meterItemsLock)
                {
                    if (_meterItems == null) return "";

                    Dictionary<string, clsMeterItem> items = _meterItems.Where(o => o.Value.ItemType == clsMeterItem.MeterItemType.ITEM_GROUP).ToDictionary(x => x.Key, x => x.Value);
                    foreach (KeyValuePair<string, clsMeterItem> kvp in items)
                    {
                        clsItemGroup ig = kvp.Value as clsItemGroup;
                        if (ig != null && ig.MeterType == mt && (order == -1 || ig.Order == order)) return ig.ID;
                    }

                    return "";
                }
            }
            public void ApplySettingsForMeterGroup(MeterType mt, clsIGSettings igs, int order = -1)
            {
                lock (_meterItemsLock)
                {
                    if (_meterItems == null) return;

                    bool bRebuild = false;

                    Dictionary<string, clsMeterItem> itemGroups = _meterItems.Where(o => o.Value.ItemType == clsMeterItem.MeterItemType.ITEM_GROUP).ToDictionary(x => x.Key, x => x.Value);
                    foreach (KeyValuePair<string, clsMeterItem> kvp in itemGroups)
                    {
                        clsItemGroup ig = kvp.Value as clsItemGroup;
                        if (ig != null && ig.MeterType == mt && (order == -1 || ig.Order == order))
                        {
                            switch (mt)
                            {
                                case MeterType.WEB_IMAGE:
                                    {
                                        bRebuild = true; // alwayys cause a rebuild

                                        float padding = 0f;

                                        Dictionary<string, clsMeterItem> items = itemsFromID(ig.ID, false);
                                        foreach (KeyValuePair<string, clsMeterItem> me in items.Where(o => o.Value.ItemType == clsMeterItem.MeterItemType.WEB_IMAGE))
                                        {
                                            clsWebImage webimg = me.Value as clsWebImage;
                                            if (webimg == null) continue;

                                            webimg.FadeOnRx = igs.FadeOnRx;
                                            webimg.FadeOnTx = igs.FadeOnTx;
                                            webimg.URL = igs.Text1;
                                            webimg.SecondsInterval = igs.UpdateInterval;
                                            webimg.WidthScale = igs.EyeScale;

                                            //webimg.TopLeft = new PointF(ig.TopLeft.X, _fPadY - (_fHeight * 0.75f));
                                            webimg.TopLeft = new PointF(0.5f - (igs.EyeScale / 2f), _fPadY - (_fHeight * 0.75f));
                                            webimg.Size = new SizeF(/*ig.Size.Width*/ igs.EyeScale, webimg.ScaleSize/*ig.Size.Height * igs.EyeScale*//*igs.SpacerPadding*/);

                                            padding += webimg.ScaleSize;// ig.Size.Height * igs.EyeScale;//igs.SpacerPadding;
                                        }
                                        ig.Size = new SizeF(ig.Size.Width, padding + (_fPadY - (_fHeight * 0.75f)));

                                        // recalc bounds for fade overlay cover as these will change as spacer changes
                                        System.Drawing.RectangleF eyeBounds = getBounds(ig.ID);
                                        if (!eyeBounds.IsEmpty)
                                        {
                                            foreach (KeyValuePair<string, clsMeterItem> fcs in items.Where(o => o.Value.ItemType == clsMeterItem.MeterItemType.FADE_COVER))
                                            {
                                                clsFadeCover fc = fcs.Value as clsFadeCover;
                                                if (fc == null) continue;

                                                fc.TopLeft = new PointF(ig.TopLeft.X, _fPadY - (_fHeight * 0.75f));
                                                fc.Size = new SizeF(ig.Size.Width, padding);

                                                fc.FadeOnRx = igs.FadeOnRx;
                                                fc.FadeOnTx = igs.FadeOnTx;
                                            }
                                        }
                                    }
                                    break;
                                case MeterType.ROTATOR:
                                    {
                                        bRebuild = true;
                                        float padding = 0f;
                                        string imageName = "";
                                        Dictionary<string, clsMeterItem> items = itemsFromID(ig.ID, false);
                                        //one image, and the me
                                        foreach (KeyValuePair<string, clsMeterItem> me in items.Where(o => o.Value.ItemType == clsMeterItem.MeterItemType.ROTATOR))
                                        {
                                            clsRotatorItem rotator = me.Value as clsRotatorItem;
                                            if (rotator == null) continue; // skip

                                            if (rotator.MMIOVariableIndex != -1)
                                            {
                                                rotator.MMIOGuid = igs.GetMMIOGuid(rotator.MMIOVariableIndex);
                                                rotator.MMIOVariable = igs.GetMMIOVariable(rotator.MMIOVariableIndex);
                                            }
                                            else
                                            {
                                                rotator.MMIOGuid = Guid.Empty;
                                                rotator.MMIOVariable = "--DEFAULT--";
                                            }

                                            rotator.UpdateInterval = igs.UpdateInterval;
                                            rotator.ArrowColour = igs.TitleColor;
                                            rotator.BigBlobColour = igs.MarkerColour;
                                            rotator.SmallBlobColour = igs.SubMarkerColour;
                                            rotator.ShowBeamWidth = igs.ShowMarker;
                                            rotator.BeamWidthColour = igs.LowColor;
                                            rotator.OuterTextColour = igs.HighColor;
                                            rotator.ShowCardinals = igs.ShowHistory;
                                            rotator.ViewMode = (clsRotatorItem.RotatorMode)igs.HistoryDuration;
                                            rotator.FadeOnRx = igs.FadeOnRx;
                                            rotator.FadeOnTx = igs.FadeOnTx;
                                            rotator.BeamWidth = igs.AttackRatio;
                                            rotator.Padding = igs.EyeScale;
                                            rotator.AllowControl = igs.ShowType;
                                            rotator.ControlColour = igs.HistoryColor;
                                            rotator.AZControlString = igs.Text1;
                                            rotator.ELEControlString = igs.Text2;
                                            rotator.DataOutMMIOGuid = igs.GetMMIOGuid(2);
                                            imageName = rotator.ImageName;

                                            if (rotator.ViewMode == clsRotatorItem.RotatorMode.BOTH)
                                            {
                                                padding = 0.5f;
                                                rotator.TopLeft = new PointF(0f, _fPadY - (_fHeight * 0.75f));
                                                rotator.Size = new SizeF(1f, padding);
                                            }
                                            else
                                            {
                                                padding = rotator.Padding;
                                                rotator.TopLeft = new PointF(0.5f - (padding / 2f), _fPadY - (_fHeight * 0.75f));
                                                rotator.Size = new SizeF(padding, padding);
                                            }
                                        }
                                        ig.Size = new SizeF(ig.Size.Width, padding + (_fPadY - (_fHeight * 0.75f)));
                                        foreach (KeyValuePair<string, clsMeterItem> img in items.Where(o => o.Value.ItemType == clsMeterItem.MeterItemType.IMAGE))
                                        {
                                            clsImage image = img.Value as clsImage;
                                            if (image == null) continue;

                                            if ((clsRotatorItem.RotatorMode)igs.HistoryDuration == clsRotatorItem.RotatorMode.BOTH)
                                            {
                                                image.TopLeft = new PointF(0.5f - (0.5f / 2f), _fPadY - (_fHeight * 0.75f) /*+ ((0.5f - 0.5f) * 0.5f)*/);
                                                image.Size = new SizeF(0.5f, 0.5f);
                                                //image.TopLeft = new PointF(ig.TopLeft.X, _fPadY - (_fHeight * 0.75f));
                                                //image.Size = new SizeF(ig.Size.Width, padding);
                                            }
                                            else
                                            {
                                                image.TopLeft = new PointF(0.5f - (igs.EyeScale / 2f), _fPadY - (_fHeight * 0.75f) /*+ ((igs.EyeScale - igs.EyeScale) * 0.5f)*/);
                                                image.Size = new SizeF(igs.EyeScale, igs.EyeScale);
                                            }

                                            image.ImageName = imageName;
                                            image.FadeOnRx = igs.FadeOnRx;
                                            image.FadeOnTx = igs.FadeOnTx;
                                            image.DarkMode = igs.DarkMode;
                                        }
                                        foreach (KeyValuePair<string, clsMeterItem> sc in items.Where(o => o.Value.ItemType == clsMeterItem.MeterItemType.SOLID_COLOUR))
                                        {
                                            clsSolidColour solidColour = sc.Value as clsSolidColour;
                                            if (solidColour == null) continue;

                                            solidColour.TopLeft = new PointF(ig.TopLeft.X, _fPadY - (_fHeight * 0.75f));
                                            solidColour.Size = new SizeF(ig.Size.Width, padding);

                                            solidColour.FadeOnRx = igs.FadeOnRx;
                                            solidColour.FadeOnTx = igs.FadeOnTx;
                                            solidColour.Colour = igs.Colour;
                                        }
                                        foreach (KeyValuePair<string, clsMeterItem> fcs in items.Where(o => o.Value.ItemType == clsMeterItem.MeterItemType.FADE_COVER))
                                        {
                                            clsFadeCover fc = fcs.Value as clsFadeCover;
                                            if (fc == null) continue;

                                            fc.TopLeft = new PointF(ig.TopLeft.X, _fPadY - (_fHeight * 0.75f));
                                            fc.Size = new SizeF(ig.Size.Width, padding);

                                            fc.FadeOnRx = igs.FadeOnRx;
                                            fc.FadeOnTx = igs.FadeOnTx;
                                        }
                                    }
                                    break;
                                case MeterType.LED:
                                    {
                                        bRebuild = true; // alwayys cause a rebuild as we relocate the text overlay each time

                                        float padding = 0f;

                                        Dictionary<string, clsMeterItem> items = itemsFromID(ig.ID, false);
                                        foreach (KeyValuePair<string, clsMeterItem> me in items.Where(o => o.Value.ItemType == clsMeterItem.MeterItemType.LED))
                                        {
                                            clsLed led = me.Value as clsLed;
                                            if (led == null) continue;

                                            led.FadeOnRx = igs.FadeOnRx;
                                            led.FadeOnTx = igs.FadeOnTx;
                                            led.TrueColour = igs.Colour;
                                            led.FalseColour = igs.MarkerColour;

                                            led.PanelBackColour1 = igs.TitleColor;
                                            led.PanelBackColour2 = igs.HistoryColor;
                                            led.ShowBackPanel = igs.ShowSubMarker;

                                            led.OffsetX = igs.EyeScale;
                                            led.OffsetY = igs.EyeBezelScale;
                                            led.SizeX = igs.AttackRatio;
                                            led.SizeY = igs.DecayRatio;

                                            led.Condition = igs.Text1;

                                            led.Padding = igs.SpacerPadding;

                                            led.ShowTrue = igs.PeakHold;
                                            led.ShowFalse = igs.ShowMarker;
                                            switch (igs.IgnoreHistoryDuration)
                                            {
                                                case 0:
                                                    led.Blink = false;
                                                    led.Pulsate = false;
                                                    break;
                                                case 1:
                                                    led.Blink = true;
                                                    led.Pulsate = false;
                                                    break;
                                                case 2:
                                                    led.Blink = false;
                                                    led.Pulsate = true;
                                                    break;
                                            }

                                            if (igs.ShowSubMarker)
                                            {
                                                led.TopLeft = new PointF(ig.TopLeft.X, _fPadY - (_fHeight * 0.75f));
                                                led.Size = new SizeF(ig.Size.Width, igs.SpacerPadding);
                                                padding += igs.SpacerPadding;
                                            }
                                            else
                                            {
                                                led.TopLeft = new PointF(ig.TopLeft.X, _fPadY - (_fHeight * 0.75f));
                                                led.Size = new SizeF(0, 0);
                                            }
                                        }
                                        ig.Size = new SizeF(ig.Size.Width, padding == 0f ? 0 : padding + (_fPadY - (_fHeight * 0.75f)));

                                        // recalc bounds for fade overlay cover as these will change as spacer changes
                                        System.Drawing.RectangleF bounds = getBounds(ig.ID);
                                        if (!bounds.IsEmpty)
                                        {
                                            foreach (KeyValuePair<string, clsMeterItem> fcs in items.Where(o => o.Value.ItemType == clsMeterItem.MeterItemType.FADE_COVER))
                                            {
                                                clsFadeCover fc = fcs.Value as clsFadeCover;
                                                if (fc == null) continue;

                                                fc.TopLeft = new PointF(ig.TopLeft.X, _fPadY - (_fHeight * 0.75f));
                                                fc.Size = new SizeF(ig.Size.Width, padding);

                                                fc.FadeOnRx = igs.FadeOnRx;
                                                fc.FadeOnTx = igs.FadeOnTx;
                                            }
                                        }
                                    }
                                    break;
                                case MeterType.TEXT_OVERLAY:
                                    {
                                        bRebuild = true; // alwayys cause a rebuild as we relocate the text overlay each time

                                        float padding = 0f;

                                        Dictionary<string, clsMeterItem> items = itemsFromID(ig.ID, false);
                                        foreach (KeyValuePair<string, clsMeterItem> me in items.Where(o => o.Value.ItemType == clsMeterItem.MeterItemType.TEXT_OVERLAY))
                                        {
                                            clsTextOverlay text_overlay = me.Value as clsTextOverlay;
                                            if (text_overlay == null) continue;

                                            text_overlay.FadeOnRx = igs.FadeOnRx;
                                            text_overlay.FadeOnTx = igs.FadeOnTx;
                                            text_overlay.TextColour1 = igs.Colour;
                                            text_overlay.TextColour2 = igs.MarkerColour;
                                            text_overlay.TextBackColour1 = igs.SubMarkerColour;
                                            text_overlay.ShowTextBackColour1 = igs.ShowMarker;
                                            text_overlay.TextBackColour2 = igs.PeakValueColour;
                                            text_overlay.ShowTextBackColour2 = igs.ShowType;

                                            text_overlay.PanelBackColour1 = igs.TitleColor;
                                            text_overlay.PanelBackColour2 = igs.HistoryColor;
                                            text_overlay.ShowBackPanel = igs.ShowSubMarker;

                                            text_overlay.TextXOffset1 = igs.EyeScale;
                                            text_overlay.TextYOffset1 = igs.EyeBezelScale;
                                            text_overlay.TextXOffset2 = igs.AttackRatio;
                                            text_overlay.TextYOffset2 = igs.DecayRatio;

                                            text_overlay.Text1 = igs.Text1;
                                            text_overlay.Text2 = igs.Text2;

                                            text_overlay.FontFamily1 = igs.FontFamily1;
                                            text_overlay.Style1 = igs.FontStyle1;
                                            text_overlay.FontSize1 = igs.FontSize1;
                                            text_overlay.FontFamily2 = igs.FontFamily2;
                                            text_overlay.Style2 = igs.FontStyle2;
                                            text_overlay.FontSize2 = igs.FontSize2;

                                            text_overlay.Padding = igs.SpacerPadding;

                                            if (igs.ShowSubMarker)
                                            {
                                                text_overlay.TopLeft = new PointF(ig.TopLeft.X, _fPadY - (_fHeight * 0.75f));
                                                text_overlay.Size = new SizeF(ig.Size.Width, igs.SpacerPadding);
                                                padding += igs.SpacerPadding;
                                            }
                                            else
                                            {
                                                text_overlay.TopLeft = new PointF(ig.TopLeft.X, _fPadY - (_fHeight * 0.75f));
                                                text_overlay.Size = new SizeF(0, 0);
                                            }
                                        }
                                        ig.Size = new SizeF(ig.Size.Width, padding == 0f ? 0 : padding + (_fPadY - (_fHeight * 0.75f)));

                                        // recalc bounds for fade overlay cover as these will change as spacer changes
                                        System.Drawing.RectangleF bounds = getBounds(ig.ID);
                                        if (!bounds.IsEmpty)
                                        {
                                            foreach (KeyValuePair<string, clsMeterItem> fcs in items.Where(o => o.Value.ItemType == clsMeterItem.MeterItemType.FADE_COVER))
                                            {
                                                clsFadeCover fc = fcs.Value as clsFadeCover;
                                                if (fc == null) continue;

                                                fc.TopLeft = new PointF(ig.TopLeft.X, _fPadY - (_fHeight * 0.75f));
                                                fc.Size = new SizeF(ig.Size.Width, padding);

                                                fc.FadeOnRx = igs.FadeOnRx;
                                                fc.FadeOnTx = igs.FadeOnTx;
                                            }
                                        }
                                    }
                                    break;
                                case MeterType.DATA_OUT:
                                    {
                                        Dictionary<string, clsMeterItem> items = itemsFromID(ig.ID, false);
                                        foreach (KeyValuePair<string, clsMeterItem> me in items.Where(o => o.Value.ItemType == clsMeterItem.MeterItemType.DATA_OUT))
                                        {
                                            clsDataOut data_out = me.Value as clsDataOut;
                                            if (data_out == null) continue;

                                            data_out.MMIOGuid = igs.GetMMIOGuid(0);
                                            data_out.UpdateInterval = igs.UpdateInterval;
                                        }
                                        ig.Size = new SizeF(ig.Size.Width, 0);
                                    }
                                    break;
                                case MeterType.SPACER:
                                    {
                                        bRebuild = true; // alwayys cause a rebuild as we relocate the spacer each time

                                        float padding = 0f;

                                        Dictionary<string, clsMeterItem> items = itemsFromID(ig.ID, false);
                                        foreach (KeyValuePair<string, clsMeterItem> me in items.Where(o => o.Value.ItemType == clsMeterItem.MeterItemType.SPACER))
                                        {
                                            clsSpacerItem spacer = me.Value as clsSpacerItem;
                                            if (spacer == null) continue;

                                            spacer.FadeOnRx = igs.FadeOnRx;
                                            spacer.FadeOnTx = igs.FadeOnTx;
                                            spacer.Colour1 = igs.Colour;
                                            spacer.Colour2 = igs.MarkerColour;
                                            spacer.Padding = igs.SpacerPadding;
                                            
                                            spacer.TopLeft = new PointF(ig.TopLeft.X, _fPadY - (_fHeight * 0.75f));
                                            spacer.Size = new SizeF(ig.Size.Width, igs.SpacerPadding);

                                            padding += igs.SpacerPadding;
                                        }
                                        ig.Size = new SizeF(ig.Size.Width, padding + (_fPadY - (_fHeight * 0.75f)));
                                        
                                        // recalc bounds for fade overlay cover as these will change as spacer changes
                                        System.Drawing.RectangleF eyeBounds = getBounds(ig.ID);
                                        if (!eyeBounds.IsEmpty)
                                        {
                                            foreach (KeyValuePair<string, clsMeterItem> fcs in items.Where(o => o.Value.ItemType == clsMeterItem.MeterItemType.FADE_COVER))
                                            {
                                                clsFadeCover fc = fcs.Value as clsFadeCover;
                                                if (fc == null) continue;

                                                fc.TopLeft = new PointF(ig.TopLeft.X, _fPadY - (_fHeight * 0.75f));
                                                fc.Size = new SizeF(ig.Size.Width, padding);

                                                fc.FadeOnRx = igs.FadeOnRx;
                                                fc.FadeOnTx = igs.FadeOnTx;
                                            }
                                        }
                                    }
                                    break;
                                case MeterType.MAGIC_EYE:
                                    {
                                        float fLargest = Math.Max(igs.EyeScale, igs.EyeBezelScale);
                                        
                                        if (fLargest != ig.Size.Width)
                                        {
                                            ig.TopLeft = new PointF(0.5f - (fLargest / 2f), _fPadY - (_fHeight * 0.75f));
                                            ig.Size = new SizeF(fLargest, ig.TopLeft.Y + fLargest);                                           
                                        }

                                        bRebuild = true; // alwayys cause a rebuild as we relocate the eye + image each time

                                        Dictionary<string, clsMeterItem> items = itemsFromID(ig.ID, false);
                                        //one image, and the me
                                        foreach (KeyValuePair<string, clsMeterItem> me in items.Where(o => o.Value.ItemType == clsMeterItem.MeterItemType.MAGIC_EYE))
                                        {
                                            clsMagicEyeItem magicEye = me.Value as clsMagicEyeItem;
                                            if (magicEye == null) continue;

                                            if (magicEye.MMIOVariableIndex != -1)
                                            {
                                                magicEye.MMIOGuid = igs.GetMMIOGuid(magicEye.MMIOVariableIndex);
                                                magicEye.MMIOVariable = igs.GetMMIOVariable(magicEye.MMIOVariableIndex);
                                            }
                                            else
                                            {
                                                magicEye.MMIOGuid = Guid.Empty;
                                                magicEye.MMIOVariable = "--DEFAULT--";
                                            }

                                            magicEye.UpdateInterval = igs.UpdateInterval;
                                            magicEye.AttackRatio = igs.AttackRatio;
                                            magicEye.DecayRatio = igs.DecayRatio;
                                            magicEye.HistoryDuration = igs.HistoryDuration;
                                            magicEye.HistoryColour = igs.HistoryColor;
                                            magicEye.ShowHistory = igs.ShowHistory;
                                            magicEye.FadeOnRx = igs.FadeOnRx;
                                            magicEye.FadeOnTx = igs.FadeOnTx;
                                            //bi.Style = igs.BarStyle;
                                            //bi.MarkerColour = igs.MarkerColour;
                                            //bi.PeakHold = igs.PeakHold;
                                            //bi.PeakHoldMarkerColour = igs.PeakHoldMarkerColor;
                                            magicEye.Colour = igs.MarkerColour;
                                            magicEye.ReadingSource = igs.Average ? Reading.AVG_SIGNAL_STRENGTH : Reading.SIGNAL_STRENGTH;

                                            magicEye.TopLeft = new PointF(0.5f - (igs.EyeScale / 2f), _fPadY - (_fHeight * 0.75f) + ((fLargest - igs.EyeScale) * 0.5f));
                                            magicEye.Size = new SizeF(igs.EyeScale, igs.EyeScale);
                                        }
                                        foreach (KeyValuePair<string, clsMeterItem> img in items.Where(o => o.Value.ItemType == clsMeterItem.MeterItemType.IMAGE))
                                        {
                                            clsImage image = img.Value as clsImage;
                                            if (image == null) continue;

                                            image.FadeOnRx = igs.FadeOnRx;
                                            image.FadeOnTx = igs.FadeOnTx;

                                            image.TopLeft = new PointF(0.5f - (igs.EyeBezelScale / 2f), _fPadY - (_fHeight * 0.75f) + ((fLargest - igs.EyeBezelScale) * 0.5f));
                                            image.Size = new SizeF(igs.EyeBezelScale, igs.EyeBezelScale);
                                        }
                                        foreach (KeyValuePair<string, clsMeterItem> fcs in items.Where(o => o.Value.ItemType == clsMeterItem.MeterItemType.FADE_COVER))
                                        {
                                            clsFadeCover fc = fcs.Value as clsFadeCover;
                                            if (fc == null) continue;

                                            fc.FadeOnRx = igs.FadeOnRx;
                                            fc.FadeOnTx = igs.FadeOnTx;
                                        }

                                        // recalc bounds for fade overlay cover as these will change as eye scale changes
                                        System.Drawing.RectangleF eyeBounds = getBounds(ig.ID);
                                        if (!eyeBounds.IsEmpty)
                                        {
                                            foreach (KeyValuePair<string, clsMeterItem> fcs in items.Where(o => o.Value.ItemType == clsMeterItem.MeterItemType.FADE_COVER))
                                            {
                                                clsFadeCover fc = fcs.Value as clsFadeCover;
                                                if (fc == null) continue;

                                                fc.TopLeft = new PointF(0.5f - (fLargest / 2f), _fPadY - (_fHeight * 0.75f));
                                                fc.Size = new SizeF(fLargest, fLargest);
                                            }
                                        }
                                    }
                                    break;
                                case MeterType.ANANMM:
                                case MeterType.CROSS:
                                    {
                                        Dictionary<string, clsMeterItem> items = itemsFromID(ig.ID, false);
                                        //lots of ni objects, and images

                                        foreach (KeyValuePair<string, clsMeterItem> needle in items.Where(o => o.Value.ItemType == clsMeterItem.MeterItemType.NEEDLE))
                                        {
                                            clsNeedleItem ni = needle.Value as clsNeedleItem;
                                            if (ni == null) continue;

                                            if (mt == MeterType.CROSS) //TODO ananmm not done yet
                                            {
                                                if (ni.MMIOVariableIndex != -1)
                                                {
                                                    ni.MMIOGuid = igs.GetMMIOGuid(ni.MMIOVariableIndex);
                                                    ni.MMIOVariable = igs.GetMMIOVariable(ni.MMIOVariableIndex);
                                                }
                                                else
                                                {
                                                    ni.MMIOGuid = Guid.Empty;
                                                    ni.MMIOVariable = "--DEFAULT--";
                                                }
                                            }

                                            if (ni.Primary)
                                            {
                                                //primary needle, as has been added first
                                                ni.UpdateInterval = igs.UpdateInterval;
                                                ni.AttackRatio = igs.AttackRatio;
                                                ni.DecayRatio = igs.DecayRatio;
                                                ni.HistoryDuration = igs.HistoryDuration;
                                                ni.IgnoreHistoryDuration = igs.IgnoreHistoryDuration;
                                                ni.HistoryColour = igs.HistoryColor;
                                                ni.ShowHistory = igs.ShowHistory;
                                                ni.FadeOnRx = igs.FadeOnRx;
                                                ni.FadeOnTx = igs.FadeOnTx;
                                                ni.Colour = igs.MarkerColour;
                                                //bi.Style = igs.BarStyle;
                                                //bi.MarkerColour = igs.MarkerColour;
                                                //bi.PeakHold = igs.PeakHold;
                                                //bi.PeakHoldMarkerColour = igs.PeakHoldMarkerColor;
                                                ni.Shadow = igs.Shadow;
                                                if (mt == MeterType.ANANMM) 
                                                {
                                                    if (ni.ReadingSource == Reading.AVG_SIGNAL_STRENGTH || ni.ReadingSource == Reading.SIGNAL_STRENGTH)
                                                    {
                                                        ni.PeakHold = igs.PeakHold;
                                                        ni.PeakHoldMarkerColour = igs.PeakHoldMarkerColor;
                                                        ni.ReadingSource = igs.Average ? Reading.AVG_SIGNAL_STRENGTH : Reading.SIGNAL_STRENGTH;
                                                    }
                                                    else if(ni.ReadingSource == Reading.PWR)
                                                    {
                                                        ni.PeakHold = igs.PeakHold;
                                                        ni.PeakHoldMarkerColour = igs.PeakHoldMarkerColor;
                                                    }
                                                }
                                                ni.MaxPower = igs.MaxPower;
                                            }
                                            else
                                            {
                                                // not the primary needle
                                                ni.UpdateInterval = igs.UpdateInterval;
                                                ni.AttackRatio = igs.AttackRatio;
                                                ni.DecayRatio = igs.DecayRatio;
                                                ni.FadeOnRx = igs.FadeOnRx;
                                                ni.FadeOnTx = igs.FadeOnTx;
                                                //bi.SubMarkerColour = igs.SubMarkerColour;
                                                ni.Shadow = igs.Shadow;

                                                if (mt == MeterType.ANANMM && ni.ReadingSource == Reading.SWR)
                                                {
                                                    ni.ShowHistory = igs.ShowHistory;
                                                    ni.HistoryDuration = igs.HistoryDuration;
                                                    ni.IgnoreHistoryDuration = igs.IgnoreHistoryDuration;
                                                }
                                                else if (mt == MeterType.CROSS && ni.ReadingSource == Reading.REVERSE_PWR)
                                                {
                                                    //ni.Colour = igs.MarkerColour;
                                                    ni.ShowHistory = igs.ShowHistory;
                                                    ni.HistoryDuration = igs.HistoryDuration;
                                                    ni.IgnoreHistoryDuration = igs.IgnoreHistoryDuration;
                                                    ni.MaxPower = igs.MaxPower;
                                                }
                                                ni.Colour = igs.SubMarkerColour;
                                            }
                                        }
                                        foreach (KeyValuePair<string, clsMeterItem> img in items.Where(o => o.Value.ItemType == clsMeterItem.MeterItemType.IMAGE))
                                        {
                                            clsImage image = img.Value as clsImage;
                                            if (image == null) continue;

                                            image.FadeOnRx = igs.FadeOnRx;
                                            image.FadeOnTx = igs.FadeOnTx;
                                            if(image.Primary) image.DarkMode = igs.DarkMode;
                                        }
                                        foreach (KeyValuePair<string, clsMeterItem> sc in items.Where(o => o.Value.ItemType == clsMeterItem.MeterItemType.SOLID_COLOUR))
                                        {
                                            clsSolidColour solidColour = sc.Value as clsSolidColour;
                                            if (solidColour == null) continue;

                                            solidColour.FadeOnRx = igs.FadeOnRx;
                                            solidColour.FadeOnTx = igs.FadeOnTx;
                                        }
                                        foreach (KeyValuePair<string, clsMeterItem> nsi in items.Where(o => o.Value.ItemType == clsMeterItem.MeterItemType.NEEDLE_SCALE_PWR))
                                        {
                                            clsNeedleScalePwrItem nspi = nsi.Value as clsNeedleScalePwrItem;
                                            if (nspi == null) continue;

                                            nspi.FadeOnRx = igs.FadeOnRx;
                                            nspi.FadeOnTx = igs.FadeOnTx;
                                            nspi.MaxPower = igs.MaxPower;
                                            nspi.DarkMode = igs.DarkMode;
                                            nspi.LowColour = igs.PowerScaleColour;
                                        }
                                        foreach (KeyValuePair<string, clsMeterItem> txts in items.Where(o => o.Value.ItemType == clsMeterItem.MeterItemType.TEXT))
                                        {
                                            clsText txt = txts.Value as clsText;
                                            if (txt == null) continue;

                                            txt.FadeOnRx = igs.FadeOnRx;
                                            txt.FadeOnTx = igs.FadeOnTx;
                                        }
                                        foreach (KeyValuePair<string, clsMeterItem> fcs in items.Where(o => o.Value.ItemType == clsMeterItem.MeterItemType.FADE_COVER))
                                        {
                                            clsFadeCover fc = fcs.Value as clsFadeCover;
                                            if (fc == null) continue;

                                            fc.FadeOnRx = igs.FadeOnRx;
                                            fc.FadeOnTx = igs.FadeOnTx;
                                        }
                                    }
                                    break;
                                //case MeterType.HISTORY:
                                //    {
                                //        Dictionary<string, clsMeterItem> items = itemsFromID(ig.ID, false);

                                //        foreach (KeyValuePair<string, clsMeterItem> history in items.Where(o => o.Value.ItemType == clsMeterItem.MeterItemType.HISTORY))
                                //        {
                                //            clsHistoryItem hi = history.Value as clsHistoryItem;
                                //            if (hi == null) continue;

                                //            if (hi.Primary)
                                //            {
                                //                //primary needle, as has been added first
                                //                hi.UpdateInterval = igs.UpdateInterval;
                                //                //igs.AttackRatio = hi.AttackRatio;
                                //                //igs.DecayRatio = hi.DecayRatio;
                                //                hi.HistoryDuration = igs.HistoryDuration;
                                //                hi.HistoryColour = igs.HistoryColor;
                                //                hi.ShowHistory = igs.ShowHistory;
                                //                hi.FadeOnRx = igs.FadeOnRx;
                                //                hi.FadeOnTx = igs.FadeOnTx;
                                //                //igs.BarStyle = bi.Style;
                                //                //igs.MarkerColour = bi.MarkerColour;
                                //                //igs.PeakHold = bi.PeakHold;
                                //                //igs.PeakHoldMarkerColor = bi.PeakHoldMarkerColour;
                                //            }
                                //            else
                                //            {
                                //                //igs.SubMarkerColour = bi.SubMarkerColour;
                                //            }
                                //        }
                                //    }
                                //    break;
                                case MeterType.VFO_DISPLAY:
                                    {
                                        Dictionary<string, clsMeterItem> items = itemsFromID(ig.ID, false);

                                        foreach (KeyValuePair<string, clsMeterItem> sc in items.Where(o => o.Value.ItemType == clsMeterItem.MeterItemType.SOLID_COLOUR))
                                        {
                                            clsSolidColour solidColor = sc.Value as clsSolidColour;
                                            if (solidColor == null) continue;

                                            solidColor.FadeOnRx = igs.FadeOnRx;
                                            solidColor.FadeOnTx = igs.FadeOnTx;
                                            solidColor.Colour = igs.Colour;
                                        }
                                        foreach (KeyValuePair<string, clsMeterItem> vfos in items.Where(o => o.Value.ItemType == clsMeterItem.MeterItemType.VFO_DISPLAY))
                                        {
                                            clsVfoDisplay vfo = vfos.Value as clsVfoDisplay;
                                            if (vfo == null) continue;

                                            //vfo.UpdateInterval = igs.UpdateInterval;
                                            vfo.FadeOnRx = igs.FadeOnRx;
                                            vfo.FadeOnTx = igs.FadeOnTx;

                                            vfo.TypeColour = igs.TitleColor;
                                            vfo.FrequencyColour = igs.MarkerColour;
                                            vfo.ModeColour = igs.SubMarkerColour;
                                            vfo.SplitBackColour = igs.LowColor;
                                            vfo.SplitColour = igs.HighColor;
                                            vfo.RxColour = igs.PeakValueColour;
                                            vfo.TxColour = igs.PeakHoldMarkerColor;
                                            vfo.FilterColour = igs.HistoryColor;
                                            vfo.BandColour = igs.SegmentedSolidLowColour;
                                            vfo.DigitHighlightColour = igs.PowerScaleColour;
                                        }
                                    }
                                    break;
                                case MeterType.CLOCK:
                                    {
                                        Dictionary<string, clsMeterItem> items = itemsFromID(ig.ID, false);

                                        foreach (KeyValuePair<string, clsMeterItem> sc in items.Where(o => o.Value.ItemType == clsMeterItem.MeterItemType.SOLID_COLOUR))
                                        {
                                            clsSolidColour solidColor = sc.Value as clsSolidColour;
                                            if (solidColor == null) continue;

                                            solidColor.FadeOnRx = igs.FadeOnRx;
                                            solidColor.FadeOnTx = igs.FadeOnTx;
                                            solidColor.Colour = igs.Colour;
                                        }
                                        foreach (KeyValuePair<string, clsMeterItem> clks in items.Where(o => o.Value.ItemType == clsMeterItem.MeterItemType.CLOCK))
                                        {
                                            clsClock clk = clks.Value as clsClock;
                                            if (clk == null) continue;

                                            //vfo.UpdateInterval = igs.UpdateInterval;
                                            //clk.Show24HourCLock = igs.Clock24Hr;
                                            clk.FadeOnRx = igs.FadeOnRx;
                                            clk.FadeOnTx = igs.FadeOnTx;
                                            clk.TimeColour = igs.MarkerColour;
                                            clk.DateColour = igs.SubMarkerColour;
                                            clk.ShowType = igs.ShowType;
                                            clk.TypeTitleColour = igs.TitleColor;
                                            clk.Show24HourCLock = igs.ShowMarker; // showmarker is used for the bool
                                        }
                                    }
                                    break;
                                case MeterType.SIGNAL_TEXT:
                                    {
                                        Dictionary<string, clsMeterItem> items = itemsFromID(ig.ID, false);

                                        foreach (KeyValuePair<string, clsMeterItem> stds in items.Where(o => o.Value.ItemType == clsMeterItem.MeterItemType.SIGNAL_TEXT_DISPLAY))
                                        {
                                            clsSignalText cst = stds.Value as clsSignalText;
                                            if (cst == null) continue;

                                            cst.UpdateInterval = igs.UpdateInterval;
                                            cst.AttackRatio = igs.AttackRatio;
                                            cst.DecayRatio = igs.DecayRatio;
                                            cst.HistoryDuration = igs.HistoryDuration;
                                            cst.IgnoreHistoryDuration = igs.IgnoreHistoryDuration;
                                            cst.FadeOnRx = igs.FadeOnRx;
                                            cst.FadeOnTx = igs.FadeOnTx;
                                            //cst.Colour = igs.Colour;
                                            cst.FontColour = igs.MarkerColour;
                                            cst.HistoryColour = igs.SubMarkerColour;
                                            cst.PeakValueColour = igs.PeakValueColour;
                                            cst.ShowPeakValue = igs.PeakValue;
                                            cst.ReadingSource = igs.Average ? Reading.AVG_SIGNAL_STRENGTH : Reading.SIGNAL_STRENGTH;
                                            cst.ShowSubMarker = igs.ShowSubMarker;

                                            ig.TopLeft = new PointF(ig.TopLeft.X, _fPadY - (_fHeight * 0.75f));
                                            if (cst.ShowPeakValue)
                                                ig.Size = new SizeF(ig.Size.Width, ig.TopLeft.Y + (_fHeight + _fHeight * 0.75f) * 1.5f);
                                            else
                                                ig.Size = new SizeF(ig.Size.Width, ig.TopLeft.Y + (_fHeight + _fHeight * 0.75f) * 1.1f);

                                            bRebuild = true;
                                        }
                                        foreach (KeyValuePair<string, clsMeterItem> sc in items.Where(o => o.Value.ItemType == clsMeterItem.MeterItemType.SOLID_COLOUR))
                                        {
                                            clsSolidColour solidColor = sc.Value as clsSolidColour;
                                            if (solidColor == null) continue;

                                            solidColor.FadeOnRx = igs.FadeOnRx;
                                            solidColor.FadeOnTx = igs.FadeOnTx;
                                            solidColor.Colour = igs.Colour;

                                            solidColor.Size = new SizeF(solidColor.Size.Width, ig.Size.Height - ig.TopLeft.Y);
                                        }
                                        foreach (KeyValuePair<string, clsMeterItem> fcs in items.Where(o => o.Value.ItemType == clsMeterItem.MeterItemType.FADE_COVER))
                                        {
                                            clsFadeCover fc = fcs.Value as clsFadeCover;
                                            if (fc == null) continue;

                                            fc.FadeOnRx = igs.FadeOnRx;
                                            fc.FadeOnTx = igs.FadeOnTx;
                                        }
                                    }
                                    break;
                                default:
                                    {
                                        Dictionary<string, clsMeterItem> items = itemsFromID(ig.ID, false);
                                        // a bar can be up to 2 hbars (peak/av), back colour, and scale

                                        foreach (KeyValuePair<string, clsMeterItem> hbar in items.Where(o => o.Value.ItemType == clsMeterItem.MeterItemType.H_BAR))
                                        {
                                            clsBarItem bi = hbar.Value as clsBarItem;
                                            if (bi == null) continue;

                                            //
                                            //for (int i = 0; i < 10; i++)
                                            //{
                                            //    bi.SetMMIOGuid(i, igs.GetMMIOGuid(i));
                                            //    bi.SetMMIOVariable(i, igs.GetMMIOVariable(i));
                                            //}
                                            if (bi.MMIOVariableIndex != -1)
                                            {
                                                bi.MMIOGuid = igs.GetMMIOGuid(bi.MMIOVariableIndex);
                                                bi.MMIOVariable = igs.GetMMIOVariable(bi.MMIOVariableIndex);
                                            }
                                            else
                                            {
                                                bi.MMIOGuid = Guid.Empty;
                                                bi.MMIOVariable = "--DEFAULT--";
                                            }
                                            //

                                            if (bi.Primary)
                                            {
                                                //primary bar, as has been added first
                                                bi.UpdateInterval = igs.UpdateInterval;
                                                bi.AttackRatio = igs.AttackRatio;
                                                bi.DecayRatio = igs.DecayRatio;
                                                bi.FadeOnRx = igs.FadeOnRx;
                                                bi.FadeOnTx = igs.FadeOnTx;
                                                bi.HistoryDuration = igs.HistoryDuration;
                                                bi.IgnoreHistoryDuration = igs.IgnoreHistoryDuration;
                                                bi.HistoryColour = igs.HistoryColor;
                                                bi.ShowHistory = igs.ShowHistory;
                                                bi.Style = igs.BarStyle;
                                                bi.MarkerColour = igs.MarkerColour;
                                                bi.ShowMarker = igs.ShowMarker;
                                                bi.PeakHold = igs.PeakHold;
                                                bi.PeakHoldMarkerColour = igs.PeakHoldMarkerColor;
                                                bi.Colour = igs.SegmentedSolidLowColour;
                                                bi.ColourHigh = igs.SegmentedSolidHighColour;
                                                bi.ShowPeakValue = igs.PeakValue;
                                                bi.PeakValueColour = igs.PeakValueColour;
                                                bi.Unit = igs.Unit;                                                
                                            }
                                            else
                                            {
                                                bi.UpdateInterval = igs.UpdateInterval;
                                                bi.AttackRatio = igs.AttackRatio;
                                                bi.DecayRatio = igs.DecayRatio;
                                                bi.FadeOnRx = igs.FadeOnRx;
                                                bi.FadeOnTx = igs.FadeOnTx;
                                                bi.MarkerColour = igs.SubMarkerColour;
                                                bi.ShowMarker = igs.ShowSubMarker;
                                                //igs.SubMarkerColour = bi.SubMarkerColour;
                                            }
                                            if (bi.ReadingSource == Reading.PWR || bi.ReadingSource == Reading.REVERSE_PWR) bi.MaxPower = igs.MaxPower;
                                        }
                                        foreach (KeyValuePair<string, clsMeterItem> sc in items.Where(o => o.Value.ItemType == clsMeterItem.MeterItemType.SOLID_COLOUR))
                                        {
                                            clsSolidColour solidColor = sc.Value as clsSolidColour;
                                            if (solidColor == null) continue;

                                            solidColor.FadeOnRx = igs.FadeOnRx;
                                            solidColor.FadeOnTx = igs.FadeOnTx;
                                            solidColor.Colour = igs.Colour;
                                        }
                                        foreach (KeyValuePair<string, clsMeterItem> si in items.Where(o => o.Value.ItemType == clsMeterItem.MeterItemType.H_SCALE))
                                        {
                                            clsScaleItem scaleItem = si.Value as clsScaleItem;
                                            if (scaleItem == null) continue;

                                            scaleItem.FadeOnRx = igs.FadeOnRx;
                                            scaleItem.FadeOnTx = igs.FadeOnTx;
                                            scaleItem.LowColour = igs.LowColor;
                                            scaleItem.HighColour = igs.HighColor;
                                            scaleItem.ShowType = igs.ShowType;
                                            scaleItem.FontColourType = igs.TitleColor;
                                            scaleItem.FontColourLow = igs.LowColor;
                                            scaleItem.FontColourHigh = igs.HighColor;
                                            if(scaleItem.ReadingSource == Reading.PWR || scaleItem.ReadingSource == Reading.REVERSE_PWR) scaleItem.MaxPower = igs.MaxPower;
                                        }
                                        foreach (KeyValuePair<string, clsMeterItem> fcs in items.Where(o => o.Value.ItemType == clsMeterItem.MeterItemType.FADE_COVER))
                                        {
                                            clsFadeCover fc = fcs.Value as clsFadeCover;
                                            if (fc == null) continue;

                                            fc.FadeOnRx = igs.FadeOnRx;
                                            fc.FadeOnTx = igs.FadeOnTx;
                                        }
                                    }
                                    break;
                            }

                            if (bRebuild)
                            {
                                Rebuild();
                            }
                            else
                            {
                                UpdateIntervals();
                            }

                            return;
                        }
                    }

                    return;
                }
            }
            public clsIGSettings GetSettingsForMeterGroup(MeterType mt, int order = -1)
            {
                lock (_meterItemsLock)
                {
                    if (_meterItems == null) return null;

                    Dictionary<string, clsMeterItem> itemGroups = _meterItems.Where(o => o.Value.ItemType == clsMeterItem.MeterItemType.ITEM_GROUP).ToDictionary(x => x.Key, x => x.Value);
                    foreach (KeyValuePair<string, clsMeterItem> kvp in itemGroups)
                    {
                        clsItemGroup ig = kvp.Value as clsItemGroup;
                        if (ig != null && ig.MeterType == mt && (order == -1 || order == ig.Order))
                        {
                            clsIGSettings igs = new clsIGSettings();
                            switch (mt)
                            {
                                case MeterType.ROTATOR:
                                    {
                                        Dictionary<string, clsMeterItem> items = itemsFromID(ig.ID, false);
                                        //one image, and the me
                                        foreach (KeyValuePair<string, clsMeterItem> me in items.Where(o => o.Value.ItemType == clsMeterItem.MeterItemType.ROTATOR))
                                        {
                                            clsRotatorItem rotator = me.Value as clsRotatorItem;
                                            if (rotator == null) continue; // skip

                                            if (rotator.MMIOVariableIndex != -1)
                                            {
                                                igs.SetMMIOGuid(rotator.MMIOVariableIndex, rotator.MMIOGuid);
                                                igs.SetMMIOVariable(rotator.MMIOVariableIndex, rotator.MMIOVariable);
                                            }

                                            if (rotator.Primary)
                                            {
                                                igs.UpdateInterval = rotator.UpdateInterval;
                                                igs.TitleColor = rotator.ArrowColour;
                                                igs.MarkerColour = rotator.BigBlobColour;
                                                igs.SubMarkerColour = rotator.SmallBlobColour;
                                                igs.ShowMarker = rotator.ShowBeamWidth;
                                                igs.LowColor = rotator.BeamWidthColour;
                                                igs.HighColor = rotator.OuterTextColour;
                                                igs.ShowHistory = rotator.ShowCardinals;
                                                igs.FadeOnRx = rotator.FadeOnRx;
                                                igs.FadeOnTx = rotator.FadeOnTx;
                                                igs.AttackRatio = rotator.BeamWidth;
                                                igs.EyeScale = rotator.Padding;
                                                igs.HistoryDuration = (int)rotator.ViewMode;
                                                igs.SetMMIOGuid(2, rotator.DataOutMMIOGuid);
                                                igs.ShowType = rotator.AllowControl;
                                                igs.HistoryColor = rotator.ControlColour;
                                                igs.Text1 = rotator.AZControlString;
                                                igs.Text2 = rotator.ELEControlString;
                                            }
                                        }
                                        foreach (KeyValuePair<string, clsMeterItem> sc in items.Where(o => o.Value.ItemType == clsMeterItem.MeterItemType.SOLID_COLOUR))
                                        {
                                            clsSolidColour solidcolor = sc.Value as clsSolidColour;
                                            if (solidcolor == null) continue; // skip the sc

                                            igs.Colour = solidcolor.Colour;
                                        }
                                        foreach (KeyValuePair<string, clsMeterItem> img in items.Where(o => o.Value.ItemType == clsMeterItem.MeterItemType.IMAGE))
                                        {
                                            clsImage image = img.Value as clsImage;
                                            if (image == null) continue; // skip

                                            igs.DarkMode = image.DarkMode;
                                        }
                                        foreach (KeyValuePair<string, clsMeterItem> fcs in items.Where(o => o.Value.ItemType == clsMeterItem.MeterItemType.FADE_COVER))
                                        {
                                            clsFadeCover fc = fcs.Value as clsFadeCover;
                                            if (fc == null) continue; // skip

                                            igs.FadeOnRx = fc.FadeOnRx;
                                            igs.FadeOnTx = fc.FadeOnTx;
                                        }
                                    }
                                    break;
                                case MeterType.LED:
                                    {
                                        Dictionary<string, clsMeterItem> items = itemsFromID(ig.ID, false);
                                        //one image, and the me
                                        foreach (KeyValuePair<string, clsMeterItem> me in items.Where(o => o.Value.ItemType == clsMeterItem.MeterItemType.LED))
                                        {
                                            clsLed led = me.Value as clsLed;
                                            if (led == null) continue; // skip

                                            igs.FadeOnRx = led.FadeOnRx;
                                            igs.FadeOnTx = led.FadeOnTx;
                                            igs.Colour = led.TrueColour;
                                            igs.MarkerColour = led.FalseColour;

                                            igs.TitleColor = led.PanelBackColour1;
                                            igs.HistoryColor = led.PanelBackColour2;
                                            igs.ShowSubMarker = led.ShowBackPanel;

                                            igs.EyeScale = led.OffsetX;
                                            igs.EyeBezelScale = led.OffsetY;
                                            igs.AttackRatio = led.SizeX;
                                            igs.DecayRatio = led.SizeY;

                                            igs.Text1 = led.Condition;

                                            igs.SpacerPadding = led.Padding;

                                            igs.ShowHistory = led.ScriptError;
                                            igs.ShowType = led.ScriptValid;

                                            igs.PeakHold = led.ShowTrue;
                                            igs.ShowMarker = led.ShowFalse;
                                            if (!led.Blink && !led.Pulsate)
                                                igs.IgnoreHistoryDuration = 0;
                                            else if (led.Blink && !led.Pulsate)
                                                igs.IgnoreHistoryDuration = 1;
                                            else if (!led.Blink && led.Pulsate)
                                                igs.IgnoreHistoryDuration = 2;
                                        }
                                        foreach (KeyValuePair<string, clsMeterItem> fcs in items.Where(o => o.Value.ItemType == clsMeterItem.MeterItemType.FADE_COVER))
                                        {
                                            clsFadeCover fc = fcs.Value as clsFadeCover;
                                            if (fc == null) continue; // skip

                                            igs.FadeOnRx = fc.FadeOnRx;
                                            igs.FadeOnTx = fc.FadeOnTx;
                                        }
                                    }
                                    break;
                                case MeterType.TEXT_OVERLAY:
                                    {
                                        Dictionary<string, clsMeterItem> items = itemsFromID(ig.ID, false);
                                        //one image, and the me
                                        foreach (KeyValuePair<string, clsMeterItem> me in items.Where(o => o.Value.ItemType == clsMeterItem.MeterItemType.TEXT_OVERLAY))
                                        {
                                            clsTextOverlay text_overlay = me.Value as clsTextOverlay;
                                            if (text_overlay == null) continue; // skip

                                            igs.FadeOnRx = text_overlay.FadeOnRx;
                                            igs.FadeOnTx = text_overlay.FadeOnTx;
                                            igs.Colour = text_overlay.TextColour1;
                                            igs.MarkerColour = text_overlay.TextColour2;
                                            igs.SubMarkerColour = text_overlay.TextBackColour1;
                                            igs.ShowMarker = text_overlay.ShowTextBackColour1;
                                            igs.PeakValueColour = text_overlay.TextBackColour2;
                                            igs.ShowType = text_overlay.ShowTextBackColour2;

                                            igs.TitleColor = text_overlay.PanelBackColour1;
                                            igs.HistoryColor = text_overlay.PanelBackColour2;
                                            igs.ShowSubMarker = text_overlay.ShowBackPanel;

                                            igs.EyeScale = text_overlay.TextXOffset1;
                                            igs.EyeBezelScale = text_overlay.TextYOffset1;
                                            igs.AttackRatio = text_overlay.TextXOffset2;
                                            igs.DecayRatio = text_overlay.TextYOffset2;

                                            igs.Text1 = text_overlay.Text1;
                                            igs.Text2 = text_overlay.Text2;

                                            igs.FontFamily1 = text_overlay.FontFamily1;
                                            igs.FontStyle1 = text_overlay.Style1;
                                            igs.FontSize1 = text_overlay.FontSize1;
                                            igs.FontFamily2 = text_overlay.FontFamily2;
                                            igs.FontStyle2 = text_overlay.Style2;
                                            igs.FontSize2 = text_overlay.FontSize2;

                                            igs.SpacerPadding = text_overlay.Padding;
                                        }
                                        foreach (KeyValuePair<string, clsMeterItem> fcs in items.Where(o => o.Value.ItemType == clsMeterItem.MeterItemType.FADE_COVER))
                                        {
                                            clsFadeCover fc = fcs.Value as clsFadeCover;
                                            if (fc == null) continue; // skip

                                            igs.FadeOnRx = fc.FadeOnRx;
                                            igs.FadeOnTx = fc.FadeOnTx;
                                        }
                                    }
                                    break;
                                case MeterType.DATA_OUT:
                                    { 
                                    Dictionary<string, clsMeterItem> items = itemsFromID(ig.ID, false);
                                        //one image, and the me
                                        foreach (KeyValuePair<string, clsMeterItem> me in items.Where(o => o.Value.ItemType == clsMeterItem.MeterItemType.DATA_OUT))
                                        {
                                            clsDataOut data_out = me.Value as clsDataOut;
                                            if (data_out == null) continue; // skip

                                            igs.SetMMIOGuid(0, data_out.MMIOGuid);
                                            igs.UpdateInterval = data_out.UpdateInterval;
                                        }  
                                    }
                                    break;
                                case MeterType.WEB_IMAGE:
                                    {
                                        Dictionary<string, clsMeterItem> items = itemsFromID(ig.ID, false);
                                        //one image, and the me
                                        foreach (KeyValuePair<string, clsMeterItem> me in items.Where(o => o.Value.ItemType == clsMeterItem.MeterItemType.WEB_IMAGE))
                                        {
                                            clsWebImage webimg = me.Value as clsWebImage;
                                            if (webimg == null) continue; // skip

                                            igs.FadeOnRx = webimg.FadeOnRx;
                                            igs.FadeOnTx = webimg.FadeOnTx;
                                            igs.Text1 = webimg.URL;
                                            igs.UpdateInterval = webimg.SecondsInterval;
                                            igs.EyeScale = webimg.WidthScale;
                                            igs.HistoryDuration = (int)webimg.WebImageState;
                                        }
                                        foreach (KeyValuePair<string, clsMeterItem> fcs in items.Where(o => o.Value.ItemType == clsMeterItem.MeterItemType.FADE_COVER))
                                        {
                                            clsFadeCover fc = fcs.Value as clsFadeCover;
                                            if (fc == null) continue; // skip

                                            igs.FadeOnRx = fc.FadeOnRx;
                                            igs.FadeOnTx = fc.FadeOnTx;
                                        }
                                    }
                                    break;
                                case MeterType.SPACER:
                                    {
                                        Dictionary<string, clsMeterItem> items = itemsFromID(ig.ID, false);
                                        //one image, and the me
                                        foreach (KeyValuePair<string, clsMeterItem> me in items.Where(o => o.Value.ItemType == clsMeterItem.MeterItemType.SPACER))
                                        {
                                            clsSpacerItem spacer = me.Value as clsSpacerItem;
                                            if (spacer == null) continue; // skip

                                            igs.Colour = spacer.Colour1;
                                            igs.MarkerColour = spacer.Colour2;
                                            igs.SpacerPadding = spacer.Padding;
                                        }
                                        foreach (KeyValuePair<string, clsMeterItem> fcs in items.Where(o => o.Value.ItemType == clsMeterItem.MeterItemType.FADE_COVER))
                                        {
                                            clsFadeCover fc = fcs.Value as clsFadeCover;
                                            if (fc == null) continue; // skip

                                            igs.FadeOnRx = fc.FadeOnRx;
                                            igs.FadeOnTx = fc.FadeOnTx;
                                        }
                                    }
                                    break;
                                case MeterType.MAGIC_EYE:
                                    {
                                        Dictionary<string, clsMeterItem> items = itemsFromID(ig.ID, false);
                                        //one image, and the me
                                        foreach (KeyValuePair<string, clsMeterItem> me in items.Where(o => o.Value.ItemType == clsMeterItem.MeterItemType.MAGIC_EYE))
                                        {
                                            clsMagicEyeItem magicEye = me.Value as clsMagicEyeItem;
                                            if (magicEye == null) continue; // skip the img

                                            if (magicEye.MMIOVariableIndex != -1)
                                            {
                                                igs.SetMMIOGuid(magicEye.MMIOVariableIndex, magicEye.MMIOGuid);
                                                igs.SetMMIOVariable(magicEye.MMIOVariableIndex, magicEye.MMIOVariable);
                                            }

                                            igs.UpdateInterval = magicEye.UpdateInterval;
                                            igs.AttackRatio = magicEye.AttackRatio;
                                            igs.DecayRatio = magicEye.DecayRatio;
                                            igs.HistoryDuration = magicEye.HistoryDuration;
                                            igs.IgnoreHistoryDuration = magicEye.IgnoreHistoryDuration;
                                            igs.HistoryColor = magicEye.HistoryColour;
                                            igs.ShowHistory = magicEye.ShowHistory;
                                            //igs.FadeOnRx = magicEye.FadeOnRx;
                                            //igs.FadeOnTx = magicEye.FadeOnTx;
                                            //igs.BarStyle = bi.Style;
                                            //igs.MarkerColour = bi.MarkerColour;
                                            //igs.PeakHold = bi.PeakHold;
                                            //igs.PeakHoldMarkerColor = bi.PeakHoldMarkerColour;
                                            igs.MarkerColour = magicEye.Colour;
                                            igs.EyeScale = magicEye.Size.Height;
                                            
                                            igs.Average = magicEye.ReadingSource == Reading.AVG_SIGNAL_STRENGTH;
                                        }
                                        foreach (KeyValuePair<string, clsMeterItem> img in items.Where(o => o.Value.ItemType == clsMeterItem.MeterItemType.IMAGE))
                                        {
                                            clsImage image = img.Value as clsImage;
                                            if (image == null) continue; // skip the img

                                            igs.EyeBezelScale = image.Size.Width;
                                            //igs.FadeOnRx = image.FadeOnRx;
                                            //igs.FadeOnTx = image.FadeOnTx;
                                        }
                                        foreach (KeyValuePair<string, clsMeterItem> fcs in items.Where(o => o.Value.ItemType == clsMeterItem.MeterItemType.FADE_COVER))
                                        {
                                            clsFadeCover fc = fcs.Value as clsFadeCover;
                                            if (fc == null) continue; // skip

                                            igs.FadeOnRx = fc.FadeOnRx;
                                            igs.FadeOnTx = fc.FadeOnTx;
                                        }
                                    }
                                    break;
                                case MeterType.ANANMM:
                                case MeterType.CROSS:
                                    {
                                        Dictionary<string, clsMeterItem> items = itemsFromID(ig.ID, false);
                                        //lots of ni objects, and images

                                        foreach (KeyValuePair<string, clsMeterItem> needle in items.Where(o => o.Value.ItemType == clsMeterItem.MeterItemType.NEEDLE))
                                        {
                                            clsNeedleItem ni = needle.Value as clsNeedleItem;
                                            if (ni == null) continue; // skip the images

                                            if (mt == MeterType.CROSS) //TODO ananmm not done yet
                                            {
                                                if (ni.MMIOVariableIndex != -1)
                                                {
                                                    igs.SetMMIOGuid(ni.MMIOVariableIndex, ni.MMIOGuid);
                                                    igs.SetMMIOVariable(ni.MMIOVariableIndex, ni.MMIOVariable);
                                                }
                                            }

                                            if (ni.Primary)
                                            {
                                                //primary needle, as has been added first
                                                igs.UpdateInterval = ni.UpdateInterval;
                                                igs.AttackRatio = ni.AttackRatio;
                                                igs.DecayRatio = ni.DecayRatio;
                                                igs.HistoryDuration = ni.HistoryDuration;
                                                igs.IgnoreHistoryDuration = ni.IgnoreHistoryDuration;
                                                igs.HistoryColor = ni.HistoryColour;
                                                igs.ShowHistory = ni.ShowHistory;
                                                //igs.FadeOnRx = ni.FadeOnRx;
                                                //igs.FadeOnTx = ni.FadeOnTx;
                                                //igs.BarStyle = bi.Style;
                                                //igs.MarkerColour = bi.MarkerColour;
                                                //igs.PeakHold = bi.PeakHold;
                                                //igs.PeakHoldMarkerColor = bi.PeakHoldMarkerColour;
                                                igs.Shadow = ni.Shadow;
                                                igs.MarkerColour = ni.Colour;
                                                if (mt == MeterType.ANANMM) {
                                                    if (ni.ReadingSource == Reading.SIGNAL_STRENGTH || ni.ReadingSource == Reading.AVG_SIGNAL_STRENGTH)
                                                    {
                                                        igs.PeakHold = ni.PeakHold;
                                                        igs.PeakHoldMarkerColor = ni.PeakHoldMarkerColour;
                                                        igs.Average = ni.ReadingSource == Reading.AVG_SIGNAL_STRENGTH;
                                                    }
                                                }
                                                igs.MaxPower = ni.MaxPower;
                                            }
                                            else
                                            {
                                                //igs.SubMarkerColour = bi.SubMarkerColour;
                                                //if(mt == MeterType.CROSS) igs.MarkerColour = ni.Colour;
                                                igs.SubMarkerColour = ni.Colour;
                                                igs.SubIndicators = true;
                                                //igs.ShowSubMarker = ni
                                            }
                                        }
                                        foreach (KeyValuePair<string, clsMeterItem> img in items.Where(o => o.Value.ItemType == clsMeterItem.MeterItemType.IMAGE))
                                        {
                                            clsImage image = img.Value as clsImage;
                                            if (image == null) continue; // skip the img

                                            //igs.FadeOnRx = image.FadeOnRx;
                                            //igs.FadeOnTx = image.FadeOnTx;
                                            if (image.Primary) igs.DarkMode = image.DarkMode;
                                        }
                                        foreach (KeyValuePair<string, clsMeterItem> sc in items.Where(o => o.Value.ItemType == clsMeterItem.MeterItemType.NEEDLE_SCALE_PWR))
                                        {
                                            clsNeedleScalePwrItem nspi = sc.Value as clsNeedleScalePwrItem;
                                            if (nspi == null) continue;

                                            //nspi.FadeOnRx = igs.FadeOnRx;
                                            //nspi.FadeOnTx = igs.FadeOnTx;
                                            if (nspi.Primary)
                                            {
                                                igs.MaxPower = nspi.MaxPower;
                                                igs.PowerScaleColour = nspi.LowColour;
                                            }
                                        }
                                        foreach (KeyValuePair<string, clsMeterItem> fcs in items.Where(o => o.Value.ItemType == clsMeterItem.MeterItemType.FADE_COVER))
                                        {
                                            clsFadeCover fc = fcs.Value as clsFadeCover;
                                            if (fc == null) continue; // skip

                                            igs.FadeOnRx = fc.FadeOnRx;
                                            igs.FadeOnTx = fc.FadeOnTx;
                                        }
                                    }
                                    break;
                                case MeterType.VFO_DISPLAY:
                                    {
                                        Dictionary<string, clsMeterItem> items = itemsFromID(ig.ID, false);

                                        foreach (KeyValuePair<string, clsMeterItem> vfos in items.Where(o => o.Value.ItemType == clsMeterItem.MeterItemType.VFO_DISPLAY))
                                        {
                                            clsVfoDisplay vfo = vfos.Value as clsVfoDisplay;
                                            if (vfo == null) continue;

                                            igs.FadeOnRx = vfo.FadeOnRx;
                                            igs.FadeOnTx = vfo.FadeOnTx;

                                            igs.TitleColor = vfo.TypeColour;
                                            igs.MarkerColour = vfo.FrequencyColour;
                                            igs.SubMarkerColour = vfo.ModeColour;
                                            igs.LowColor = vfo.SplitBackColour;
                                            igs.HighColor = vfo.SplitColour;
                                            igs.PeakValueColour = vfo.RxColour;
                                            igs.PeakHoldMarkerColor = vfo.TxColour;
                                            igs.HistoryColor = vfo.FilterColour;
                                            igs.SegmentedSolidLowColour = vfo.BandColour;
                                            igs.PowerScaleColour = vfo.DigitHighlightColour;
                                        }
                                        foreach (KeyValuePair<string, clsMeterItem> sc in items.Where(o => o.Value.ItemType == clsMeterItem.MeterItemType.SOLID_COLOUR))
                                        {
                                            clsSolidColour solidColor = sc.Value as clsSolidColour;
                                            if (solidColor == null) continue;

                                            //igs.FadeOnRx = solidColor.FadeOnRx;
                                            //solidColor.FadeOnTx = igs.FadeOnTx;
                                            igs.Colour = solidColor.Colour;
                                        }
                                    }
                                    break;
                                case MeterType.CLOCK:
                                    {
                                        Dictionary<string, clsMeterItem> items = itemsFromID(ig.ID, false);

                                        foreach (KeyValuePair<string, clsMeterItem> vfos in items.Where(o => o.Value.ItemType == clsMeterItem.MeterItemType.CLOCK))
                                        {
                                            clsClock clk = vfos.Value as clsClock;
                                            if (clk == null) continue;

                                            //igs.UpdateInterval = vfo.UpdateInterval;
                                            igs.FadeOnRx = clk.FadeOnRx;
                                            igs.FadeOnTx = clk.FadeOnTx;

                                            //igs.Clock24Hr = clk.Show24HourCLock;
                                            igs.MarkerColour = clk.TimeColour;
                                            igs.SubMarkerColour = clk.DateColour;
                                            igs.ShowType = clk.ShowType;
                                            igs.TitleColor = clk.TypeTitleColour;
                                            igs.ShowMarker = clk.Show24HourCLock; // showmarker is used for the bool
                                        }
                                        foreach (KeyValuePair<string, clsMeterItem> sc in items.Where(o => o.Value.ItemType == clsMeterItem.MeterItemType.SOLID_COLOUR))
                                        {
                                            clsSolidColour solidColor = sc.Value as clsSolidColour;
                                            if (solidColor == null) continue;

                                            //igs.FadeOnRx = solidColor.FadeOnRx;
                                            //solidColor.FadeOnTx = igs.FadeOnTx;
                                            igs.Colour = solidColor.Colour;
                                        }
                                    }
                                    break;
                                case MeterType.SIGNAL_TEXT:
                                    {
                                        Dictionary<string, clsMeterItem> items = itemsFromID(ig.ID, false);

                                        foreach (KeyValuePair<string, clsMeterItem> sc in items.Where(o => o.Value.ItemType == clsMeterItem.MeterItemType.SOLID_COLOUR))
                                        {
                                            clsSolidColour solidColor = sc.Value as clsSolidColour;
                                            if (solidColor == null) continue;

                                            //igs.FadeOnRx = solidColor.FadeOnRx;
                                            //igs.FadeOnTx = solidColor.FadeOnTx;
                                            igs.Colour = solidColor.Colour;
                                        }
                                        foreach (KeyValuePair<string, clsMeterItem> stds in items.Where(o => o.Value.ItemType == clsMeterItem.MeterItemType.SIGNAL_TEXT_DISPLAY))
                                        {
                                            clsSignalText cst = stds.Value as clsSignalText;
                                            if (cst == null) continue;

                                            igs.UpdateInterval = cst.UpdateInterval;
                                            igs.AttackRatio = cst.AttackRatio;
                                            igs.DecayRatio = cst.DecayRatio;
                                            igs.HistoryDuration = cst.HistoryDuration;
                                            igs.IgnoreHistoryDuration = cst.IgnoreHistoryDuration;
                                            //igs.FadeOnRx = cst.FadeOnRx;
                                            //igs.FadeOnTx = cst.FadeOnTx;
                                            //igs.Colour = cst.Colour;
                                            igs.MarkerColour = cst.FontColour;
                                            igs.SubMarkerColour = cst.HistoryColour;
                                            igs.PeakValueColour = cst.PeakValueColour;
                                            igs.PeakValue = cst.ShowPeakValue;
                                            igs.Average = cst.ReadingSource == Reading.AVG_SIGNAL_STRENGTH;
                                            igs.ShowSubMarker = cst.ShowSubMarker;
                                        }
                                        foreach (KeyValuePair<string, clsMeterItem> fcs in items.Where(o => o.Value.ItemType == clsMeterItem.MeterItemType.FADE_COVER))
                                        {
                                            clsFadeCover fc = fcs.Value as clsFadeCover;
                                            if (fc == null) continue; // skip

                                            igs.FadeOnRx = fc.FadeOnRx;
                                            igs.FadeOnTx = fc.FadeOnTx;
                                        }
                                    }
                                    break;
                                //case MeterType.HISTORY:
                                //{
                                //    Dictionary<string, clsMeterItem> items = itemsFromID(ig.ID, false);

                                //    foreach (KeyValuePair<string, clsMeterItem> history in items.Where(o => o.Value.ItemType == clsMeterItem.MeterItemType.HISTORY))
                                //    {
                                //        clsHistoryItem hi = history.Value as clsHistoryItem;
                                //        if (hi == null) continue;

                                //        if (hi.Primary)
                                //        {
                                //            //primary needle, as has been added first
                                //            igs.UpdateInterval = hi.UpdateInterval;
                                //            //igs.AttackRatio = hi.AttackRatio;
                                //            //igs.DecayRatio = hi.DecayRatio;
                                //            igs.HistoryDuration = hi.HistoryDuration;
                                //            igs.HistoryColor = hi.HistoryColour;
                                //            igs.ShowHistory = hi.ShowHistory;
                                //            igs.FadeOnRx = hi.FadeOnRx;
                                //            igs.FadeOnTx = hi.FadeOnTx;
                                //            //igs.BarStyle = bi.Style;
                                //            //igs.MarkerColour = bi.MarkerColour;
                                //            //igs.PeakHold = bi.PeakHold;
                                //            //igs.PeakHoldMarkerColor = bi.PeakHoldMarkerColour;
                                //        }
                                //        else
                                //        {
                                //            //igs.SubMarkerColour = bi.SubMarkerColour;
                                //        }
                                //    }
                                //}
                                //break;
                                default:
                                    {
                                        Dictionary<string, clsMeterItem> items = itemsFromID(ig.ID, false);
                                        // a bar can be up to 2 hbars (peak/av), back colour, and scale

                                        foreach(KeyValuePair<string, clsMeterItem> hbar in items.Where(o => o.Value.ItemType == clsMeterItem.MeterItemType.H_BAR))
                                        {
                                            clsBarItem bi = hbar.Value as clsBarItem;
                                            if (bi == null) continue;

                                            //
                                            //for (int i = 0; i < 10; i++)
                                            //{
                                            //    igs.SetMMIOGuid(i, bi.GetMMIOGuid(i));
                                            //    igs.SetMMIOVariable(i, bi.GetMMIOVariable(i));
                                            //}
                                            if (bi.MMIOVariableIndex != -1)
                                            {
                                                igs.SetMMIOGuid(bi.MMIOVariableIndex, bi.MMIOGuid);
                                                igs.SetMMIOVariable(bi.MMIOVariableIndex, bi.MMIOVariable);
                                            }
                                            //

                                            if (bi.Primary)
                                            {
                                                //primary bar, as has been added first
                                                igs.UpdateInterval = bi.UpdateInterval;
                                                igs.AttackRatio = bi.AttackRatio;
                                                igs.DecayRatio = bi.DecayRatio;
                                                igs.HistoryDuration = bi.HistoryDuration;
                                                igs.IgnoreHistoryDuration = bi.IgnoreHistoryDuration;
                                                igs.HistoryColor = bi.HistoryColour;
                                                igs.ShowHistory = bi.ShowHistory;
                                                //igs.FadeOnRx = bi.FadeOnRx;
                                                //igs.FadeOnTx = bi.FadeOnTx;
                                                igs.BarStyle = bi.Style;
                                                igs.MarkerColour = bi.MarkerColour;
                                                igs.ShowMarker = bi.ShowMarker;
                                                igs.PeakHold = bi.PeakHold;
                                                igs.PeakHoldMarkerColor = bi.PeakHoldMarkerColour;
                                                igs.SegmentedSolidLowColour = bi.Colour;
                                                igs.SegmentedSolidHighColour = bi.ColourHigh;
                                                igs.PeakValue = bi.ShowPeakValue;
                                                igs.PeakValueColour = bi.PeakValueColour;
                                                igs.Unit = bi.Unit;
                                                if (bi.ReadingSource == Reading.PWR || bi.ReadingSource == Reading.REVERSE_PWR) igs.MaxPower = bi.MaxPower;
                                            }
                                            else
                                            {
                                                //igs.FadeOnRx = bi.FadeOnRx;
                                                //igs.FadeOnTx = bi.FadeOnTx;
                                                //igs.SubMarkerColour = bi.SubMarkerColour;
                                                //if (bi.ReadingSource == Reading.PWR || bi.ReadingSource == Reading.REVERSE_PWR) igs.PowerLimit = bi.MaxPower;
                                                igs.SubMarkerColour = bi.MarkerColour;
                                                igs.ShowSubMarker = bi.ShowMarker;
                                                igs.SubIndicators = true;
                                            }                                            
                                        }
                                        foreach (KeyValuePair<string, clsMeterItem> sc in items.Where(o => o.Value.ItemType == clsMeterItem.MeterItemType.SOLID_COLOUR))
                                        {
                                            clsSolidColour solidcolor = sc.Value as clsSolidColour;
                                            if (solidcolor == null) continue; // skip the sc

                                            igs.Colour = solidcolor.Colour;
                                            //igs.FadeOnRx = solidcolor.FadeOnRx;
                                            //igs.FadeOnTx = solidcolor.FadeOnTx;
                                        }
                                        foreach (KeyValuePair<string, clsMeterItem> si in items.Where(o => o.Value.ItemType == clsMeterItem.MeterItemType.H_SCALE))
                                        {
                                            clsScaleItem scaleItem = si.Value as clsScaleItem;
                                            if (scaleItem == null) continue;

                                            //igs.FadeOnRx = scaleItem.FadeOnRx;
                                            //igs.FadeOnTx = scaleItem.FadeOnTx;
                                            igs.LowColor = scaleItem.LowColour;
                                            igs.HighColor = scaleItem.HighColour;
                                            //igs.show = scaleItem.ShowType; 
                                            igs.TitleColor = scaleItem.FontColourType;
                                            igs.ShowType = scaleItem.ShowType;
                                            if (scaleItem.ReadingSource == Reading.PWR || scaleItem.ReadingSource == Reading.REVERSE_PWR) igs.MaxPower = scaleItem.MaxPower;
                                        }
                                        foreach (KeyValuePair<string, clsMeterItem> fcs in items.Where(o => o.Value.ItemType == clsMeterItem.MeterItemType.FADE_COVER))
                                        {
                                            clsFadeCover fc = fcs.Value as clsFadeCover;
                                            if (fc == null) continue; // skip

                                            igs.FadeOnRx = fc.FadeOnRx;
                                            igs.FadeOnTx = fc.FadeOnTx;
                                        }
                                    }
                                    break;
                            }

                            return igs;
                        }
                    }

                    return null;
                }
            }
            public List<int> GetOrderForMeterType(MeterType mt)
            {
                lock (_meterItemsLock)
                {
                    List<int> orders = new List<int>();

                    if (_meterItems == null) return orders;

                    Dictionary<string, clsMeterItem> items = _meterItems.Where(o => o.Value.ItemType == clsMeterItem.MeterItemType.ITEM_GROUP).ToDictionary(x => x.Key, x => x.Value);
                    foreach (KeyValuePair<string, clsMeterItem> kvp in items)
                    {
                        clsItemGroup ig = kvp.Value as clsItemGroup;
                        if (ig != null && ig.MeterType == mt) orders.Add(ig.Order);
                    }

                    return orders;
                }
            }
            public void SetOrderForMeterType(MeterType mt, int nOrder, bool bRebuild, bool bUp, int order = -1)
            {
                // only works for up/down 1 place
                lock (_meterItemsLock)
                {
                    if (_meterItems == null) return;

                    if (nOrder < 0) nOrder = 0;
                    int nTmp = numberOfMeterGroups() - 1;
                    if (nOrder > nTmp) nOrder = nTmp;
                    bool done = false;

                    Dictionary<string, clsMeterItem> items = _meterItems.Where(o => o.Value.ItemType == clsMeterItem.MeterItemType.ITEM_GROUP).ToDictionary(x => x.Key, x => x.Value);
                    foreach (KeyValuePair<string, clsMeterItem> kvp in items)
                    {
                        clsItemGroup ig = kvp.Value as clsItemGroup;
                        if (ig != null)
                        {
                            if (ig.MeterType == mt && (order == -1 || ig.Order == order) && !done)
                            {
                                ig.Order = nOrder;
                                done = true;
                            }
                            else if (ig.Order == nOrder)
                            {
                                if (bUp)
                                    ig.Order++;
                                else
                                    ig.Order--;
                            }
                        }
                    }
                }
                if (bRebuild) Rebuild();
            }
            public bool Enabled
            {
                get { return _enabled; }
                set { _enabled = value; }
            }
            private clsMeterItem itemFromID(string sId)
            {
                lock (_meterItemsLock)
                {
                    if (_meterItems == null) return null;
                    if (!_meterItems.ContainsKey(sId)) return null;

                    return _meterItems[sId];
                }
            }
            internal System.Drawing.RectangleF getBounds(string sId)
            {
                lock (_meterItemsLock)
                {
                    if (_meterItems == null) return System.Drawing.RectangleF.Empty;
                    Dictionary<string, clsMeterItem> items = itemsFromID(sId, false, true);
                    if (items == null) return System.Drawing.RectangleF.Empty;

                    float x = float.MaxValue;
                    float y = float.MaxValue;
                    float brx = float.MinValue;
                    float bry = float.MinValue;

                    foreach (KeyValuePair<string, clsMeterItem> kvp in items)
                    {
                        clsMeterItem mi = kvp.Value;

                        if(mi.TopLeft.X < x) x = mi.TopLeft.X;
                        if(mi.TopLeft.Y < y) y = mi.TopLeft.Y;
                        if (mi.TopLeft.X + mi.Size.Width > brx) brx = mi.TopLeft.X + mi.Size.Width;
                        if (mi.TopLeft.Y + mi.Size.Height > bry) bry = mi.TopLeft.Y + mi.Size.Height;
                    }

                    return new System.Drawing.RectangleF(x, y, brx - x, bry - y);
                }
            }
            internal Dictionary<string, clsMeterItem> itemsFromID(string sId, bool bIncludeTheParent = true, bool bOnlyChildren = false)
            {
                // obtains all items that have given ID and also the parent
                // note: the id must exist as a non parent unless the onlychildren flag is used
                lock (_meterItemsLock)
                {
                    if (_meterItems == null) return null;
                    if (!bOnlyChildren && !_meterItems.ContainsKey(sId)) return null;

                    Dictionary<string, clsMeterItem> lst = new Dictionary<string, clsMeterItem>();

                    foreach (KeyValuePair<string, clsMeterItem> kvp in _meterItems.Where(o => (o.Value.ParentID == sId) || (!bOnlyChildren && bIncludeTheParent && o.Value.ID == sId)))
                        lst.Add(kvp.Key, kvp.Value);

                    return lst;
                }
            }
            private bool hasReading(Reading reading)
            {
                bool bRet = false;
                foreach (KeyValuePair<string, clsMeterItem> kvp in _meterItems)
                {
                    if(kvp.Value.ReadingSource == reading)
                    {
                        bRet = true;
                        break;
                    }
                }
                return bRet;
            }
            public float GetBottom()
            {
                lock (_meterItemsLock)
                {
                    float fBottom = 0;

                    foreach (KeyValuePair<string, clsMeterItem> kvp in _meterItems)
                    {
                        float fTmp = kvp.Value.TopLeft.Y + kvp.Value.Size.Height;
                        if (fTmp > fBottom) fBottom = fTmp;
                    }

                    return fBottom;
                }
            }
            //public clsItemGroup GetItemGroup(string id, bool checkParent = false)
            //{
            //    lock (_meterItemsLock)
            //    {
            //        Dictionary<string, clsMeterItem> meterItems;
            //        if (checkParent)
            //        {
            //            meterItems = _meterItems.Where(o => o.Value.ItemType == clsMeterItem.MeterItemType.ITEM_GROUP && (o.Value.ID == id || o.Value.ParentID == id)).ToDictionary(x => x.Key, x => x.Value);
            //        }
            //        else
            //        {
            //            meterItems = _meterItems.Where(o => o.Value.ItemType == clsMeterItem.MeterItemType.ITEM_GROUP && o.Value.ID == id).ToDictionary(x => x.Key, x => x.Value);
            //        }

            //        if (meterItems.Count == 1)
            //        {
            //            return (clsItemGroup)meterItems.First().Value;
            //        }
            //        return null;
            //    }
            //}
            public int QuickestRXUpdate
            {
                get { return _quickestRXUpdate; }
            }
            public int QuickestTXUpdate
            {
                get { return _quickestTXUpdate; }
            }
            internal Dictionary<string, clsItemGroup> getMeterGroups()
            {
                lock (_meterItemsLock)
                {
                    Dictionary<string, clsMeterItem> meterItems = _meterItems.Where(o => o.Value.ItemType == clsMeterItem.MeterItemType.ITEM_GROUP).ToDictionary(x => x.Key, x => x.Value);
                    Dictionary<string, clsItemGroup> groupItems = new Dictionary<string, clsItemGroup>();

                    foreach (KeyValuePair<string, clsMeterItem> kvp in meterItems)
                    {
                        groupItems.Add(kvp.Value.ID, (clsItemGroup)kvp.Value);
                    }
                    return groupItems;
                }                
            }
            public void UpdateIntervals()
            {
                _quickestRXUpdate = QuickestUpdateInterval(false);
                _quickestTXUpdate = QuickestUpdateInterval(true);
            }
            public void Rebuild()
            {
                UpdateIntervals();

                setupSortedZOrder();

                lock (_meterItemsLock)
                {
                    // shift top
                    //Dictionary<string, clsMeterItem> meterItems = _meterItems.Where(o => o.Value.ItemType == clsMeterItem.MeterItemType.ITEM_GROUP).ToDictionary(x => x.Key, x => x.Value);
                    Dictionary<string, clsItemGroup> groupItems = getMeterGroups();

                    float fTop = 0;

                    foreach (KeyValuePair<string, clsItemGroup> kvp in groupItems.OrderBy(o => o.Value.Order))
                    {
                        clsItemGroup ig = kvp.Value;

                        if (ig != null)
                        {
                            ig.TopLeft = new PointF(ig.TopLeft.X, fTop);

                            Dictionary<string, clsMeterItem> itemsInGroup = itemsFromID(ig.ID, false);

                            foreach (KeyValuePair<string, clsMeterItem> kvp2 in itemsInGroup)
                            {
                                clsMeterItem mi = kvp2.Value;
                                mi.DisplayTopLeft = new PointF(mi.TopLeft.X, mi.TopLeft.Y + fTop);
                            }

                            fTop += ig.Size.Height;
                        }
                    }
                }
            }
            private void setupSortedZOrder()
            {
                lock (_meterItemsLock)
                {
                    //_sortedMeterItemsForZOrder = _meterItems.OrderBy(o => o.Value.ZOrder).ToDictionary(x => x.Key, x => x.Value);
                    _sortedMeterItemsForZOrder = _meterItems.OrderBy(o => o.Value.ZOrder).Select(x => x.Value).ToList();
                }
            }
            internal void MouseUp(System.Windows.Forms.MouseEventArgs e, clsMeter m, clsClickBox cb)
            {
                if (cb.GotoGroup > 0)
                {
                    m.DisplayGroup = cb.GotoGroup;
                }
                else
                {
                    if (cb.ChangesGroup)
                    {
                        if (e.Button == MouseButtons.Left)
                            incrementDisplayGroup();
                        else if (e.Button == MouseButtons.Right)
                            decrementDisplayGroup();
                    }
                    if (cb.PerformsIncDec)
                    {
                        if (e.Button == MouseButtons.Left)
                        {
                            lock (_meterItemsLock)
                            {
                                incrementMeterItem(cb);
                            }
                        }
                        else if (e.Button == MouseButtons.Right)
                        {
                            lock (_meterItemsLock)
                            {
                                decrementMeterItem(cb);
                            }
                        }
                    }
                }
            }
            public bool MOX
            {
                get { return _mox; }
                set 
                {
                    bool changed = _mox != value;
                    _mox = value;
                    if (changed)
                    {
                        if (_mox)
                            _qso_start = DateTime.Now;
                        else
                            _qso_end = DateTime.Now;
                    }
                }
            }
            public long QsoDurationSeconds
            {
                get 
                { 
                    if (_mox) _qso_end = DateTime.Now;
                    return (long)(_qso_end - _qso_start).TotalSeconds; 
                }
            }
            public bool Split
            {
                get { return _split; }
                set { _split = value; }
            }
            public bool TXVFOb
            {
                get { return _txVfoB; }
                set { _txVfoB = value; }
            }
            public double VfoA
            {
                get { return _vfoA; }
                set 
                { 
                    _vfoA = value;
                    if(_rx == 1) _rx1VHForAbove = _vfoA >= _console.S9Frequency;
                }
            }
            public double VfoB
            {
                get { return _vfoB; }
                set 
                { 
                    _vfoB = value;
                    if (_rx == 2) _rx2VHForAbove = _vfoB >= _console.S9Frequency;
                }
            }
            public double VfoSub
            {
                get { return _vfoSub; }
                set { _vfoSub = value; }
            }
            public DSPMode ModeVfoA
            {
                get { return _modeVfoA; }
                set { _modeVfoA = value; }
            }
            public DSPMode ModeVfoB
            {
                get { return _modeVfoB; }
                set { _modeVfoB = value; }
            }
            public Band BandVfoA
            {
                get { return _bandVfoA; }
                set { _bandVfoA = value; }
            }
            public Band BandVfoB
            {
                get { return _bandVfoB; }
                set { _bandVfoB = value; }
            }
            public Band BandVfoASub
            {
                get { return _bandVfoASub; }
                set { _bandVfoASub = value; }
            }
            public Filter FilterVfoA
            {
                get { return _filterVfoA; }
                set { _filterVfoA = value; }
            }
            public Filter FilterVfoB
            {
                get { return _filterVfoB; }
                set { _filterVfoB = value; }
            }
            public string FilterVfoAName
            {
                get { return _filterVfoAname; }
                set { _filterVfoAname = value; }
            }
            public string FilterVfoBName
            {
                get { return _filterVfoBname; }
                set { _filterVfoBname = value; }
            }
            public bool RX2Enabled
            {
                get { return _rx2Enabled; }
                set { _rx2Enabled = value; }
            }
            public bool MultiRxEnabled
            {
                get { return _multiRxEnabled; }
                set { _multiRxEnabled = value; }
            }
            public bool TXEQEnabled
            {
                get { return _txeqEnabled; }
                set { _txeqEnabled = value; }
            }
            public bool LevelerEnabled
            {
                get { return _levelerEnabled; }
                set { _levelerEnabled = value; }
            }
            public bool CFCEnabled
            {
                get { return _cfcEnabled; }
                set { _cfcEnabled = value; }
            }
            public bool CompandEnabled
            {
                get { return _compandEnabled; }
                set { _compandEnabled = value; }
            }
            public bool QuickSplitEnabled
            {
                get { return _quickSplitEnabled; }
                set { _quickSplitEnabled = value; }
            }
            public float PadX { get { return _fPadX; } set { _fPadX = value; } }
            public float PadY { get { return _fPadY; } set { _fPadY = value; } }
            public float Height { get { return _fHeight; } set { _fHeight = value; } }

            public float XRatio { get { return _XRatio; } set { _XRatio = value; } }
            public float YRatio { get { return _YRatio; } set { _YRatio = value; } }
            public string Name { get { return _name; } set { _name = value; } }            
            public string ID
            { 
                get { return _sId; } 
                set { _sId = value; }
            }
            public int RX { get { return _rx; } set { _rx = value; } }
            public int QuickestUpdateInterval(bool mox)
            {
                lock (_meterItemsLock)
                {
                    int updateInterval = int.MaxValue;
                    foreach (var kvp in _meterItems.Where(o => (o.Value.ItemType == clsMeterItem.MeterItemType.NEEDLE || 
                                                                o.Value.ItemType == clsMeterItem.MeterItemType.H_BAR ||
                                                                o.Value.ItemType == clsMeterItem.MeterItemType.MAGIC_EYE ||
                                                                o.Value.ItemType == clsMeterItem.MeterItemType.VFO_DISPLAY ||
                                                                o.Value.ItemType == clsMeterItem.MeterItemType.CLOCK ||
                                                                o.Value.ItemType == clsMeterItem.MeterItemType.SIGNAL_TEXT_DISPLAY ||
                                                                o.Value.ItemType == clsMeterItem.MeterItemType.SPACER ||
                                                                o.Value.ItemType == clsMeterItem.MeterItemType.TEXT_OVERLAY ||
                                                                o.Value.ItemType == clsMeterItem.MeterItemType.LED ||
                                                                o.Value.ItemType == clsMeterItem.MeterItemType.WEB_IMAGE ||
                                                                o.Value.ItemType == clsMeterItem.MeterItemType.ROTATOR
                                                                //o.Value.ItemType == clsMeterItem.MeterItemType.SPECTRUM
                                                                /*o.Value.ItemType == clsMeterItem.MeterItemType.HISTORY*/) &&
                                                                (((mox && o.Value.OnlyWhenTX) || (!mox && o.Value.OnlyWhenRX)) || (!o.Value.OnlyWhenTX && !o.Value.OnlyWhenRX))))
                    {
                        clsMeterItem mi = kvp.Value;
                        if (mi.UpdateInterval < updateInterval) updateInterval = mi.UpdateInterval;
                    }
                    return updateInterval;
                }
            }
            private void addUpdateReading(ref Dictionary<Reading, object> all_readings, Reading reading, object value)
            {
                if (all_readings.ContainsKey(reading))
                    all_readings[reading] = value;
                else
                    all_readings.Add(reading, value);
            }
            public void Update(ref List<Reading> readingsUsed)
            {
                lock (_meterItemsLock)
                {
                    //build dictionary of all readings
                    Dictionary<Reading, object> all_readings = new Dictionary<Reading, object>();
                    List<clsMeterItem> data_nodes = new List<clsMeterItem>();

                    // this is called for each meter from the meter manager thread
                    foreach (KeyValuePair<string, clsMeterItem> kvp in _meterItems)
                    {
                        // now we need to update each item in the meter
                        clsMeterItem mi = kvp.Value;
                        if (mi.RequiresUpdate)
                        {
                            if (mi.ItemType != clsMeterItem.MeterItemType.DATA_OUT)
                            {
                                mi.Update(_rx, ref readingsUsed);
                            }
                            else
                            {
                                clsDataOut dat = mi as clsDataOut;
                                if (dat != null)
                                {
                                    if(dat.MMIOGuid != Guid.Empty) data_nodes.Add(mi);
                                }
                            }
                        }
                    }

                    if (data_nodes.Count > 0)
                    {
                        //copy all readings
                        foreach (KeyValuePair<string, clsMeterItem> kvp in _meterItems)
                        {
                            clsMeterItem mi = kvp.Value;
                            if (mi.ReadingSource != Reading.NONE && (mi.ItemType == clsMeterItem.MeterItemType.H_BAR ||
                                mi.ItemType == clsMeterItem.MeterItemType.NEEDLE || mi.ItemType == clsMeterItem.MeterItemType.MAGIC_EYE))
                            {
                                addUpdateReading(ref all_readings, mi.ReadingSource, mi.Value);
                            }
                            else if (mi.ReadingSource == Reading.NONE && mi.ItemType == clsMeterItem.MeterItemType.CLOCK)
                            {
                                DateTime now = DateTime.Now;
                                DateTime UTCnow = DateTime.UtcNow;

                                clsClock clk = mi as clsClock;
                                if (clk == null) continue; //skip

                                string sUtc;
                                string sUtcAmPm = "";
                                if (clk.Show24HourCLock)
                                    sUtc = UTCnow.ToString("HH:mm:ss");
                                else
                                {
                                    sUtc = UTCnow.ToString("h:mm:ss");
                                    sUtcAmPm = UTCnow.Hour >= 12 ? "pm" : "am";
                                }

                                string sLoc;
                                string sLocAmPm = "";
                                if (clk.Show24HourCLock)
                                    sLoc = now.ToString("HH:mm:ss");
                                else
                                {
                                    sLoc = now.ToString("h:mm:ss");
                                    sLocAmPm = now.Hour >= 12 ? "pm" : "am";
                                }

                                string sUtcDate = UTCnow.ToString("ddd d MMM yyyy");
                                string sLocDate = now.ToString("ddd d MMM yyyy");

                                addUpdateReading(ref all_readings, Reading.TIME_UTC, sUtc + sUtcAmPm);
                                addUpdateReading(ref all_readings, Reading.DATE_UTC, sUtcDate);
                                addUpdateReading(ref all_readings, Reading.TIME_LOC, sLoc + sLocAmPm);
                                addUpdateReading(ref all_readings, Reading.DATE_LOC, sLocDate);
                            }
                            else if (mi.ReadingSource == Reading.NONE && mi.ItemType == clsMeterItem.MeterItemType.VFO_DISPLAY)
                            {
                                addUpdateReading(ref all_readings, Reading.VFOA_FREQ, VfoA.ToString("f6"));
                                addUpdateReading(ref all_readings, Reading.VFOB_FREQ, VfoB.ToString("f6"));
                                bool vfoSub = RX == 1 && VfoSub >= 0 && RX2Enabled && (MultiRxEnabled || Split);
                                if (vfoSub) addUpdateReading(ref all_readings, Reading.VFOSUBA_FREQ, VfoSub.ToString("f6"));
                                addUpdateReading(ref all_readings, Reading.TX_FREQ, TXVFOb ? VfoB.ToString("f6") : VfoA.ToString("f6"));
                                addUpdateReading(ref all_readings, Reading.VFOA_BAND, clearB(BandVfoA.ToString().ToLower()));
                                addUpdateReading(ref all_readings, Reading.VFOB_BAND, clearB(BandVfoB.ToString().ToLower()));
                                if (vfoSub) addUpdateReading(ref all_readings, Reading.VFOSUBA_BAND, clearB(BandVfoASub.ToString().ToLower()));
                                addUpdateReading(ref all_readings, Reading.VFOA_FILTER_NAME, FilterVfoAName.ToLower());
                                addUpdateReading(ref all_readings, Reading.VFOB_FILTER_NAME, FilterVfoBName.ToLower());
                                addUpdateReading(ref all_readings, Reading.TX_BAND, clearB(TXVFOb ? BandVfoB.ToString().ToLower() : BandVfoA.ToString().ToLower()));
                                addUpdateReading(ref all_readings, Reading.VFOA_MODE, ModeVfoA.ToString().ToLower());
                                addUpdateReading(ref all_readings, Reading.VFOB_MODE, ModeVfoB.ToString().ToLower());
                                addUpdateReading(ref all_readings, Reading.SPLIT, Split.ToString().ToLower());
                                addUpdateReading(ref all_readings, Reading.RX2_ENABLED, RX2Enabled.ToString().ToLower());
                                addUpdateReading(ref all_readings, Reading.VFOB_TX, TXVFOb.ToString().ToLower());
                                if (RX == 1) addUpdateReading(ref all_readings, Reading.SUB_RX, MultiRxEnabled.ToString().ToLower());
                            }
                        }

                        // now just the datanodes
                        if (_rx == 1 || (_rx == 2 && _rx2Enabled))
                        {
                            foreach (clsMeterItem midatanode in data_nodes)
                            {
                                midatanode.Update(_rx, ref readingsUsed, all_readings);
                            }
                        }
                    }
                }
            }
            private string clearB(string b)
            {
                if (b.Left(1) == "b" || b.Left(1) == "B") return b.Substring(1);
                return b;
            }
            public int DelayForUpdate()
            {
                lock (_meterItemsLock)
                {
                    // this is called for each meter from the meter manager thread
                    int nDelay = int.MaxValue;
                    foreach (KeyValuePair<string, clsMeterItem> kvp in _meterItems)
                    {
                        clsMeterItem mi = kvp.Value;
                        int nTmp = mi.DelayUntilNextUpdate;
                        if (nTmp < nDelay) nDelay = nTmp;
                    }
                    if (nDelay == int.MaxValue) nDelay = 250;
                    return nDelay;
                }
            }
            //public Dictionary<string, clsMeterItem> SortedMeterItemsForZOrder
            public List<clsMeterItem> SortedMeterItemsForZOrder
            {
                get {
                    lock (_meterItemsLock)
                    {
                        return _sortedMeterItemsForZOrder;
                    }
                }
            }
            private void incrementDisplayGroup()
            {
                _displayGroup++;
                if (_displayGroup > _displayGroups.Count - 1) _displayGroup = 0;
            }
            private void decrementDisplayGroup()
            {
                _displayGroup--;
                if (_displayGroup < 0) _displayGroup = _displayGroups.Count - 1;
            }
            private void incrementMeterItem(clsClickBox cb)
            {
                if (cb == null || cb.RelatedMeterItem == null) return;

                cb.RelatedMeterItem.HandleIncrement();
            }
            private void decrementMeterItem(clsClickBox cb)
            {
                if (cb == null || cb.RelatedMeterItem == null) return;

                cb.RelatedMeterItem.HandleDecrement();
            }
            public int DisplayGroups
            {
                get { return _displayGroups.Count; }
            }
            public int DisplayGroup
            {
                get { return _displayGroup; }
                set
                {
                    _displayGroup = value;
                    if (_displayGroup < 0) _displayGroup = 0;
                    if (_displayGroup > _displayGroups.Count - 1) _displayGroup = _displayGroups.Count - 1;
                }
            }
            public void AddDisplayGroup(string sName)
            {
                if (_displayGroups.Contains(sName)) return;

                _displayGroups.Add(sName);
                _displayGroup = 0; // 0 is all
            }
            public void RemoveDisplayGroup(string sName)
            {
                if (!_displayGroups.Contains(sName)) return;

                _displayGroups.Remove(sName);
                _displayGroup = 0; // 0 is all
            }
            public string DisplayGroupText
            {
                get
                {
                    if (_displayGroup < 0) return "";
                    if (_displayGroup > _displayGroups.Count - 1) return "";

                    return _displayGroups[_displayGroup];
                }
            }
            public override string ToString()
            {
                string sRet;

                sRet = _sId + "|" +
                    _name + "|" +
                    _rx.ToString() + "|" +
                    _XRatio.ToString("f4") + "|" +
                    _YRatio.ToString("f4") + "|" +
                    _displayGroup.ToString() + "|" +
                    _fPadX.ToString("f4") + "|" +
                    _fPadY.ToString("f4") + "|" +
                    _fHeight.ToString("f4");

                return sRet;
            }
        }
        #endregion
        #region clsReading
        private class clsReading
        {
            public Reading Type;
            public float Reading;
            public bool Updated;
        }
        private class clsReadings
        {
            private Dictionary<Reading, clsReading> _latestReading;
            public clsReadings()
            {
                _latestReading = new Dictionary<Reading, clsReading>();

                for (int n = 0; n < (int)Reading.LAST; n++)
                {
                    clsReading cr = new clsReading() { Type = (Reading)n, Reading = -200f, Updated = false };
                    _latestReading.Add((Reading)n, cr);
                }
            }
            public float GetReading(Reading rt, bool useReading = false)
            {
                if (rt == Reading.NONE) return -200f;
                if (useReading) UseReading(rt);
                return _latestReading[rt].Reading;
            }
            public void SetReading(Reading rt, float value)
            {
                if (rt == Reading.NONE) return;
                _latestReading[rt].Reading = value;
                _latestReading[rt].Updated = true;
            }
            public bool RequiresUpdate(Reading rt)
            {
                if (rt == Reading.NONE) return false;
                return !_latestReading[rt].Updated;
            }
            public void UseReading(Reading rt)
            {
                if (rt == Reading.NONE) return;
                _latestReading[rt].Updated = false;
            }
        }
        #endregion
        
        #region DirectX
        //based on display.cs
        private class DXRenderer
        {
            private const SharpDX.Direct2D1.AlphaMode _ALPHA_MODE = SharpDX.Direct2D1.AlphaMode.Premultiplied;
            private bool _bDXSetup;
            private bool _dxDisplayThreadRunning;
            private Thread _dxRenderThread;
            private PresentFlags _NoVSYNCpresentFlag;
            private SharpDX.DXGI.Factory1 _factory1;
            private SharpDX.Direct3D11.Device _device;
            private int _nBufferCount;
            private Surface _surface;
            private SwapChain _swapChain;
            private SwapChain1 _swapChain1;
            private RenderTarget _renderTarget;
            private SharpDX.Direct2D1.Factory _factory;
            private bool _bAntiAlias;
            private Vector2 _pixelShift;
            private int _nVBlanks;
            private PictureBox _displayTarget;
            private int _oldRedrawDelay;
            private object _DXlock = new object();
            //
            private Dictionary<System.Drawing.Color, SharpDX.Direct2D1.Brush> _DXBrushes;
            private Color4 _backColour;
            private System.Drawing.Color _backgroundColour;
            private Dictionary<string, SharpDX.Direct2D1.Bitmap> _images;
            //
            private Dictionary<string, SharpDX.DirectWrite.TextFormat> _textFormats; // fonts
            private SharpDX.DirectWrite.Factory _fontFactory;
            private Dictionary<string, SizeF> _stringMeasure;
            private Queue<string> _stringMeasureKeys;
            //

            private int _rx;
            private Console _console;

            //fps          
            //private int _nFps = 0;
            //private int _nFrameCount = 0;
            //private HiPerfTimer _objFrameStartTimer = new HiPerfTimer();
            //private double _fLastTime;
            //private double _dElapsedFrameStart;
            //

            private string _sId;
            private clsMeter _meter;
            private bool _highlightEdge;
            private int _targetWidth;
            private int _targetHeight;
            private int _newTargetWidth;
            private int _newTargetHeight;
            private bool _targetVisible;
            private bool _enabled;
            private ColorInterpolator _color_interp;

            public DXRenderer(string sId, int rx, PictureBox target, Console c, clsMeter meter)
            {
                if (c == null || target == null) return;

                _sId = sId;
                _rx = rx;
                _console = c;
                _meter = meter;
                _highlightEdge = false;
                _enabled = meter.Enabled;

                //dx
                _DXBrushes = new Dictionary<System.Drawing.Color, SharpDX.Direct2D1.Brush>();
                _textFormats = new Dictionary<string, SharpDX.DirectWrite.TextFormat>();
                _stringMeasure = new Dictionary<string, SizeF>();
                _stringMeasureKeys = new Queue<string>();

                _dxDisplayThreadRunning = false;
                _bAntiAlias = true;
                _bDXSetup = false;
                _NoVSYNCpresentFlag = PresentFlags.None;
                _pixelShift = new Vector2(0.5f, 0.5f);
                _nVBlanks = 0;
                _oldRedrawDelay = -1;
                _displayTarget = target;
                _displayTarget.Tag = sId; // use the tag to hold sId, we can then use this in mouse event OnMouseUp

                // targetWidth properties limit min size which prevents errors in font size calcs as one example
                targetWidth = target.Width;
                targetHeight = target.Height;
                _newTargetWidth = targetWidth;
                _newTargetHeight = targetHeight;
                _targetVisible = target.Visible;
                //

                _backgroundColour = System.Drawing.Color.Black;
                _backColour = convertColour(_backgroundColour);
                
                _images = new Dictionary<string, SharpDX.Direct2D1.Bitmap>();

                //fps
                //_fLastTime = _objFrameStartTimer.ElapsedMsec;
                //_dElapsedFrameStart = _objFrameStartTimer.ElapsedMsec;

                _displayTarget.Resize += target_Resize;
                _displayTarget.MouseUp += OnMouseUp;
                _displayTarget.MouseDown += OnMouseDown;
                _displayTarget.MouseWheel += OnMouseWheel;
                _displayTarget.MouseMove += OnMouseMove;
                _displayTarget.VisibleChanged += target_VisibleChanged;
                _displayTarget.MouseLeave += OnMouseLeave;
                _displayTarget.MouseEnter += OnMouseEnter;
                _displayTarget.Click += OnClick;
                _displayTarget.MouseClick += OnMouseClick;
            }
            public void RunDisplay()
            {
                dxInit();

                _dxDisplayThreadRunning = false;
                _dxRenderThread = new Thread(new ThreadStart(dxRender))
                {
                    Name = "Multimeter DX Render Thread",
                    Priority = ThreadPriority.Lowest,
                    IsBackground = false
                };
                _dxRenderThread.SetApartmentState(ApartmentState.STA); //[2.10.3]
                _dxRenderThread.Start();
            }
            public string ID
            {
                get { return _sId; }
            }
            public bool HighlightEdge
            {
                get { return _highlightEdge; }
                set { _highlightEdge = value; }
            }
            public bool Enabled
            {
                get { return _enabled; }
                set { _enabled = value; }
            }
            public System.Drawing.Color BackgroundColour
            {
                get { return _backgroundColour; }
                set 
                {
                    _backgroundColour = value;
                    _backColour = convertColour(_backgroundColour);
                }
            }

            //DIRECTX
            internal void RemoveAnySkinImages()
            {
                lock (_DXlock)
                {
                    List<string> keysWithBoolTag = _images
                                .Where(pair => pair.Value.Tag is bool)  // note: we need is bool because we also store GUID in here for webimage
                                .Select(pair => pair.Key)
                                .ToList();

                    foreach (string sKey in keysWithBoolTag)
                    {
                        RemoveDXImage(sKey);
                    }
                }
            }
            internal void LoadDXImages(string sDefaultPath, string sSkinPath)
            {
                string[] imageFileNames = { "ananMM", "ananMM-bg", "ananMM-bg-tx", "cross-needle", "cross-needle-bg", "eye-bezel", "rotator_az-bg", "rotator_ele-bg", "rotator_both-bg" };
                string[] imageFileNameParts = { "", "-small", "-large", "-dark", "-dark-small", "-dark-large" };

                if (!_bDXSetup) return;
                
                lock (_DXlock)
                {
                    // load
                    if (!sDefaultPath.EndsWith("\\")) sDefaultPath += "\\";
                    if (System.IO.Directory.Exists(sDefaultPath))
                    {
                        for (int n = 0; n < imageFileNames.Length; n++)
                        {
                            string sSkinFileName = sSkinPath + "\\Meters\\" + imageFileNames[n];
                            string sDefaultFileName = sDefaultPath + "\\Meters\\" + imageFileNames[n];
                            for (int i = 0; i < imageFileNameParts.Length; i++)
                            {
                                if (File.Exists(sSkinFileName + imageFileNameParts[i] + ".png"))
                                {
                                    string sRemove = imageFileNames[n] + imageFileNameParts[i];
                                    //remove it
                                    if (_images.ContainsKey(sRemove))
                                    {
                                        RemoveImageCacheData(sRemove);
                                        RemoveDXImage(sRemove);
                                    }

                                    loadImage(sSkinFileName + imageFileNameParts[i] + ".png", true);
                                }
                                else
                                {
                                    loadImage(sDefaultFileName + imageFileNameParts[i] + ".png", false);
                                }                                    
                            }
                        }
                    }
                }
            }
            private void loadImage(string sFilePath, bool isSkinImage)
            {
                string sID = System.IO.Path.GetFileNameWithoutExtension(sFilePath);

                if (!_images.ContainsKey(sID)) // check contains incase of filename dupe somehow
                {
                    if (System.IO.File.Exists(sFilePath))
                    {
                        System.Drawing.Image image = null;

                        try
                        {
                            if (!MeterManager.ContainsBitmap(sID))
                            {
                                image = System.Drawing.Image.FromFile(sFilePath);
                                System.Drawing.Bitmap bmp2 = new System.Drawing.Bitmap(image);
                                bmp2.Tag = isSkinImage;
                                MeterManager.AddBitmap(sID, bmp2);

                                Debug.Print("Loaded image : " + sFilePath);
                            }

                            System.Drawing.Bitmap bmp = MeterManager.GetBitmap(sID);

                            if (bmp != null)
                            {
                                SharpDX.Direct2D1.Bitmap img = bitmapFromSystemBitmap(_renderTarget, bmp, sID);
                                img.Tag = isSkinImage; // bool for a skin image, note we also use this as a guid for web image
                                _images.Add(sID, img);
                            }
                        }
                        catch { }
                        finally
                        {
                            if (image != null) image.Dispose();
                        }
                    }
                }
            }
            private int getMaxSamples()
            {
                //D3D11_MAX_MULTISAMPLE_SAMPLE_COUNT 32u32
                //NOTE: can not be used with FlipDiscard atm unfortunately
                for (int n = 32; n>0; n /= 2)
                {
                    if(_device.CheckMultisampleQualityLevels(Format.B8G8R8A8_UNorm, n) > 0)
                    {
                        return n;
                    }
                }
                return 1;
            }
            private void dxInit(DriverType driverType = DriverType.Hardware)
            {
                // code based on display.cs
                if (_bDXSetup) return;

                try
                {
                    DeviceCreationFlags debug = DeviceCreationFlags.None;

                    // to get this to work, need to target the os
                    // https://www.prugg.at/2019/09/09/properly-detect-windows-version-in-c-net-even-windows-10/
                    // you need to enable operating system support in the app1.manifest file, otherwise majVers will not report 10+
                    // note: windows 10, 11, server 2016, server 2019, server 2022 all use the windows 10 os id in the manifest file at this current time
                    int majVers = Environment.OSVersion.Version.Major;
                    int minVers = Environment.OSVersion.Version.Minor;

                    SharpDX.Direct3D.FeatureLevel[] featureLevels;

                    if (majVers >= 10) // win10 + 11
                    {
                        featureLevels = new SharpDX.Direct3D.FeatureLevel[] {
                            SharpDX.Direct3D.FeatureLevel.Level_12_1,
                            SharpDX.Direct3D.FeatureLevel.Level_12_0,
                            SharpDX.Direct3D.FeatureLevel.Level_11_1, // windows 8 and up
                            SharpDX.Direct3D.FeatureLevel.Level_11_0, // windows 7 and up (level 11 was only partial on 7, not 11_1)
                            SharpDX.Direct3D.FeatureLevel.Level_10_1,
                            SharpDX.Direct3D.FeatureLevel.Level_10_0,
                            SharpDX.Direct3D.FeatureLevel.Level_9_3,
                            SharpDX.Direct3D.FeatureLevel.Level_9_2,
                            SharpDX.Direct3D.FeatureLevel.Level_9_1
                        };
                        _NoVSYNCpresentFlag = PresentFlags.DoNotWait;
                    }
                    else if (majVers == 6 && minVers >= 2) // windows 8, windows 8.1
                    {
                        featureLevels = new SharpDX.Direct3D.FeatureLevel[] {
                            SharpDX.Direct3D.FeatureLevel.Level_11_1, // windows 8 and up
                            SharpDX.Direct3D.FeatureLevel.Level_11_0, // windows 7 and up (level 11 was only partial on 7, not 11_1)
                            SharpDX.Direct3D.FeatureLevel.Level_10_1,
                            SharpDX.Direct3D.FeatureLevel.Level_10_0,
                            SharpDX.Direct3D.FeatureLevel.Level_9_3,
                            SharpDX.Direct3D.FeatureLevel.Level_9_2,
                            SharpDX.Direct3D.FeatureLevel.Level_9_1
                        };
                        _NoVSYNCpresentFlag = PresentFlags.DoNotWait;
                    }
                    else if (majVers == 6 && minVers < 2) // windows 7, 2008 R2, 2008, vista
                    {
                        featureLevels = new SharpDX.Direct3D.FeatureLevel[] {
                            SharpDX.Direct3D.FeatureLevel.Level_11_0, // windows 7 and up (level 11 was only partial on 7, not 11_1)
                            SharpDX.Direct3D.FeatureLevel.Level_10_1,
                            SharpDX.Direct3D.FeatureLevel.Level_10_0,
                            SharpDX.Direct3D.FeatureLevel.Level_9_3,
                            SharpDX.Direct3D.FeatureLevel.Level_9_2,
                            SharpDX.Direct3D.FeatureLevel.Level_9_1
                        };
                        _NoVSYNCpresentFlag = PresentFlags.None;
                    }
                    else
                    {
                        featureLevels = new SharpDX.Direct3D.FeatureLevel[] {
                            SharpDX.Direct3D.FeatureLevel.Level_9_1
                        };
                        _NoVSYNCpresentFlag = PresentFlags.None;
                    }

                    _factory1 = new SharpDX.DXGI.Factory1();
                    
                    _device = new SharpDX.Direct3D11.Device(driverType, debug | DeviceCreationFlags.PreventAlteringLayerSettingsFromRegistry | DeviceCreationFlags.BgraSupport/* | DeviceCreationFlags.SingleThreaded*/, featureLevels);

                    SharpDX.DXGI.Device1 device1 = _device.QueryInterfaceOrNull<SharpDX.DXGI.Device1>();
                    if (device1 != null)
                    {
                        device1.MaximumFrameLatency = 1;
                        Utilities.Dispose(ref device1);
                        device1 = null;
                    }

                    //this code should ideally be used to prevent use of flip if vsync is 0
                    //but is not used at this time
                    //SharpDX.DXGI.Factory5 f5 = factory.QueryInterfaceOrNull<SharpDX.DXGI.Factory5>();
                    //bool bAllowTearing = false;
                    //if(f5 != null)
                    //{
                    //    int size = Marshal.SizeOf(typeof(bool));
                    //    IntPtr pBool = Marshal.AllocHGlobal(size);

                    //    f5.CheckFeatureSupport(SharpDX.DXGI.Feature.PresentAllowTearing, pBool, size);

                    //    bAllowTearing = Marshal.ReadInt32(pBool) == 1;

                    //    Marshal.FreeHGlobal(pBool);
                    //}
                    //

                    // check if the device has a factory4 interface
                    // if not, then we need to use old bitplit swapeffect
                    SwapEffect swapEffect;

                    SharpDX.DXGI.Factory4 factory4 = _factory1.QueryInterfaceOrNull<SharpDX.DXGI.Factory4>();
                    bool bFlipPresent = false;
                    if (factory4 != null)
                    {
                        /*if (!_bUseLegacyBuffers)*/ bFlipPresent = true;
                        Utilities.Dispose(ref factory4);
                        factory4 = null;
                    }

                    //https://walbourn.github.io/care-and-feeding-of-modern-swapchains/
                    swapEffect = bFlipPresent ? SwapEffect.FlipDiscard : SwapEffect.Discard; //NOTE: FlipSequential should work, but is mostly used for storeapps
                    _nBufferCount = bFlipPresent ? 2 : 1;

                    //int multiSample = 2; // eg 2 = MSAA_2, 2 times multisampling
                    //int maxQuality = _device.CheckMultisampleQualityLevels(Format.B8G8R8A8_UNorm, multiSample) - 1;
                    //maxQuality = Math.Max(0, maxQuality);

                    //20 fps is the fastest that any meter can be defined as 50ms is update limit
                    _oldRedrawDelay = 20;
                    ModeDescription md = new ModeDescription(targetWidth, targetHeight,
                                                               new Rational(_oldRedrawDelay, 1)/*console.DisplayFPS, 1)*/, Format.B8G8R8A8_UNorm);

                    md.ScanlineOrdering = DisplayModeScanlineOrder.Progressive;
                    md.Scaling = DisplayModeScaling.Centered;
                    
                    SwapChainDescription desc = new SwapChainDescription()
                    {
                        BufferCount = _nBufferCount,
                        ModeDescription = md,
                        IsWindowed = true,
                        OutputHandle = _displayTarget.Handle,
                        //SampleDescription = new SampleDescription(multiSample, maxQuality),
                        SampleDescription = new SampleDescription(1, 0), // no multi sampling (1 sample), no antialiasing
                        SwapEffect = swapEffect,
                        Usage = Usage.RenderTargetOutput,// | Usage.BackBuffer,  // dont need usage.backbuffer as it is implied
                        Flags = SwapChainFlags.None                        
                    };

                    _factory1.MakeWindowAssociation(_displayTarget.Handle, WindowAssociationFlags.IgnoreAll);
                    _swapChain = new SwapChain(_factory1, _device, desc);
                    _swapChain1 = _swapChain.QueryInterface<SwapChain1>();

                    _factory = new SharpDX.Direct2D1.Factory(FactoryType.SingleThreaded, DebugLevel.None);
                    
                    _surface = _swapChain1.GetBackBuffer<Surface>(0);

                    RenderTargetProperties rtp = new RenderTargetProperties(new SharpDX.Direct2D1.PixelFormat(_swapChain.Description.ModeDescription.Format, _ALPHA_MODE));                    
                    _renderTarget = new RenderTarget(_factory, _surface, rtp);

                    if (debug == DeviceCreationFlags.Debug)
                    {
                        _device.DebugName = "MeterDeviceDB_" + _rx.ToString();
                        _swapChain.DebugName = "MeterSwapChainDB_" + _rx.ToString();
                        _swapChain1.DebugName = "MeterSwapChain1DB_" + _rx.ToString();
                        _surface.DebugName = "MeterSurfaceDB_" + _rx.ToString();
                    }
                    else
                    {
                        _device.DebugName = ""; // used in shutdown
                    }

                    _bDXSetup = true;

                    setupAliasing();

                    buildDXFonts();
                }
                catch (Exception e)
                {
                    // issue setting up dx
                    ShutdownDX();
                    MessageBox.Show("Problem initialising Meter DirectX !" + System.Environment.NewLine + System.Environment.NewLine + "[" + e.ToString() + "]", "DirectX", MessageBoxButtons.OK, MessageBoxIcon.Exclamation, MessageBoxDefaultButton.Button1, Common.MB_TOPMOST);
                }
            }
            private void resetDX2DModeDescription(int fps)
            {
                // used to reset the FPS on the swapChain
                try
                {
                    lock (_DXlock)
                    {
                        if (!_bDXSetup) return;

                        ModeDescription modeDesc = new ModeDescription(targetWidth, targetHeight,
                                                           new Rational(fps, 1), Format.B8G8R8A8_UNorm);
                        _swapChain1.ResizeTarget(ref modeDesc);

                        //dont need to resize here, as targetwidth and targetheight will not be different
                        //to the rendertarget

                        //Debug.Print(">> dx is resizing from resetDX2DModeDescription <<");
                        //resizeDX();
                    }
                }
                catch (Exception e)
                {

                }
            }
            public void ShutdownDX(bool bFromRenderThread = false)
            {
                if (_displayTarget != null)
                {
                    _displayTarget.Resize -= target_Resize;
                    _displayTarget.MouseUp -= OnMouseUp;
                    _displayTarget.MouseDown -= OnMouseDown;
                    _displayTarget.MouseWheel -= OnMouseWheel;
                    _displayTarget.MouseMove -= OnMouseMove;
                    _displayTarget.MouseLeave -= OnMouseLeave;
                    _displayTarget.MouseEnter -= OnMouseEnter;
                    _displayTarget.VisibleChanged -= target_VisibleChanged;
                    _displayTarget.Click -= OnClick;
                    _displayTarget.MouseClick -= OnMouseClick;
                }

                if (!_bDXSetup) return;

                _dxDisplayThreadRunning = false;
                if (_dxRenderThread != null && _dxRenderThread.IsAlive && !bFromRenderThread)
                {
                    int nWait = 0;
                    clsMeter m = _meter;
                    lock (m._meterItemsLock)
                    {
                        int nTmp = m.MOX ? m.QuickestTXUpdate : m.QuickestRXUpdate;
                        if (nTmp > nWait) nWait = nTmp;
                    }
                    if (nWait > 250) nWait = 250; // maxint can be returned if no meteritem entries

                    _dxRenderThread.Join(nWait + 100); // slightly longer
                }

                try
                {
                    if (_device != null && _device.ImmediateContext != null)
                    {
                        _device.ImmediateContext.ClearState();
                        _device.ImmediateContext.Flush();
                    }

                    RemoveAllDXImages();

                    releaseDXFonts();
                    releaseDXResources();

                    Utilities.Dispose(ref _renderTarget);
                    Utilities.Dispose(ref _swapChain1);
                    Utilities.Dispose(ref _swapChain);
                    Utilities.Dispose(ref _surface);
                    Utilities.Dispose(ref _factory);
                    Utilities.Dispose(ref _factory1);

                    _renderTarget = null;
                    _swapChain1 = null;
                    _swapChain = null;
                    _surface = null;
                    _factory = null;
                    _factory1 = null;

                    if (_device != null && _device.ImmediateContext != null)
                    {
                        SharpDX.Direct3D11.DeviceContext dc = _device.ImmediateContext;
                        Utilities.Dispose(ref dc);
                        dc = null;
                    }

                    SharpDX.Direct3D11.DeviceDebug ddb = null;
                    if (_device != null && _device.DebugName != "")
                    {
                        ddb = new SharpDX.Direct3D11.DeviceDebug(_device);
                        ddb.ReportLiveDeviceObjects(ReportingLevel.Detail);
                    }

                    if (ddb != null)
                    {
                        Utilities.Dispose(ref ddb);
                        ddb = null;
                    }
                    Utilities.Dispose(ref _device);
                    _device = null;

                    _bDXSetup = false;
                }
                catch (Exception e)
                {
                    MessageBox.Show("Problem Shutting Down Meter DirectX !" + System.Environment.NewLine + System.Environment.NewLine + "[" + e.ToString() + "]", "DirectX", MessageBoxButtons.OK, MessageBoxIcon.Exclamation, MessageBoxDefaultButton.Button1, Common.MB_TOPMOST);
                }
            }
            internal void RemoveAllDXImages()
            {
                if (_images == null || _images.Count == 0) return;

                lock (_DXlock)
                {
                    foreach (KeyValuePair<string, SharpDX.Direct2D1.Bitmap> kvp in _images)
                    {
                        SharpDX.Direct2D1.Bitmap b = kvp.Value;
                        Utilities.Dispose(ref b);
                    }
                    _images.Clear();
                }
            }
            internal void RemoveDXImage(string sKey)
            {
                if (_images == null || _images.Count == 0) return;

                lock (_DXlock)
                {
                    if (_images.ContainsKey(sKey))
                    {
                        SharpDX.Direct2D1.Bitmap b = _images[sKey];
                        Utilities.Dispose(ref b);
                        _images.Remove(sKey);
                    }
                }
            }
            //private void calcFps()
            //{
            //    _nFrameCount++;
            //    if (_dElapsedFrameStart >= _fLastTime + 1000)
            //    {
            //        double late = _dElapsedFrameStart - (_fLastTime + 1000);
            //        if (late > 2000 || late < 0) late = 0; // ignore if too late
            //        _nFps = _nFrameCount;
            //        _nFrameCount = 0;
            //        _fLastTime = _dElapsedFrameStart - late;
            //    }
            //}
            private void dxRender()
            {
                if (!_bDXSetup) return;

                HiPerfTimer objStopWatch = new HiPerfTimer();

                try
                {
                    _dxDisplayThreadRunning = true;

                    while (_dxDisplayThreadRunning)
                    {
                        int nSleepTime = int.MaxValue;

                        objStopWatch.Reset();

                        if (_enabled && _targetVisible)
                        {
                            //fps
                            //_dElapsedFrameStart = _objFrameStartTimer.ElapsedMsec;

                            lock (_DXlock)
                            {
                                if (targetWidth != _newTargetWidth || targetHeight != _newTargetHeight)
                                {
                                    targetWidth = _newTargetWidth;
                                    targetHeight = _newTargetHeight;

                                    //Debug.Print(">> dx is resizing from dxRender <<");
                                    bool bOk = resizeDX();
                                    if (!bOk) break; // exit do while as resizeDx will have thrown an exception and called shutdowndx
                                }

                                _renderTarget.BeginDraw();

                                // middle pixel align shift
                                Matrix3x2 t = _renderTarget.Transform;
                                t.TranslationVector = _pixelShift;
                                _renderTarget.Transform = t;

                                // background for entire form/area?
                                _renderTarget.Clear(_backColour);

                                nSleepTime = drawMeters();
                                if (nSleepTime > 250) nSleepTime = 250; // sleep max of 250ms for some sensible redraw
                                                                        // maxint can be returned if no meteritem entries

                                if (_highlightEdge)
                                {
                                    SharpDX.RectangleF rect = new SharpDX.RectangleF(0, 0, targetWidth - 1f, targetHeight - 1f);
                                    rect.Inflate(-8, -8);
                                    _renderTarget.DrawRectangle(rect, getDXBrushForColour(System.Drawing.Color.FromArgb(192, System.Drawing.Color.DarkOrange)), 16f);
                                }

                                //calcFps();
                                //_renderTarget.DrawText(_nFps.ToString(), getDXTextFormatForFont("Trebuchet MS", 18, FontStyle.Regular), new SharpDX.RectangleF(10, 0, float.PositiveInfinity, float.PositiveInfinity), getDXBrushForColour(System.Drawing.Color.White), DrawTextOptions.None);

                                // undo the translate
                                _renderTarget.Transform = Matrix3x2.Identity;

                                _renderTarget.EndDraw();

                                // render
                                // note: the only way to have Present non block when using vsync number of blanks 0 , is to use DoNotWait
                                // however the gpu will error if it is busy doing something and the data can not be queued
                                // It will error and just ignore everything, we try present and ignore the 0x887A000A error
                                PresentFlags pf = _nVBlanks == 0 ? _NoVSYNCpresentFlag : PresentFlags.None;
                                Result r = _swapChain1.TryPresent(_nVBlanks, pf);

                                // check fps delay
                                if (nSleepTime != _oldRedrawDelay)
                                {
                                    if (_oldRedrawDelay != -1) resetDX2DModeDescription(1000 / nSleepTime);
                                    _oldRedrawDelay = nSleepTime;
                                }
                                //

                                if (r != Result.Ok && r != 0x887A000A)
                                {
                                    string sMsg = "";
                                    if (r == 0x887A0001) sMsg = "Present Device Invalid Call" + Environment.NewLine + "" + Environment.NewLine + "[ " + r.ToString() + " ]";    //DXGI_ERROR_INVALID_CALL
                                    if (r == 0x887A0007) sMsg = "Present Device Reset" + Environment.NewLine + "" + Environment.NewLine + "[ " + r.ToString() + " ]";           //DXGI_ERROR_DEVICE_RESET
                                    if (r == 0x887A0005) sMsg = "Present Device Removed" + Environment.NewLine + "" + Environment.NewLine + "[ " + r.ToString() + " ]";         //DXGI_ERROR_DEVICE_REMOVED
                                    if (r == 0x88760870) sMsg = "Present Device DD3DDI Removed" + Environment.NewLine + "" + Environment.NewLine + "[ " + r.ToString() + " ]";  //D3DDDIERR_DEVICEREMOVED
                                                                                                                                                                                //if (r == 0x087A0001) sMsg = "Present Device Occluded" + Environment.NewLine + "" + Environment.NewLine + "[ " + r.ToString() + " ]";      //DXGI_STATUS_OCCLUDED
                                                                                                                                                                                //(ignored in the preceding if statement) if (r == 0x887A000A) sMsg = "Present Device Still Drawping" + Environment.NewLine + "" + Environment.NewLine + "[ " + r.ToString() + " ]"; //DXGI_ERROR_WAS_STILL_DRAWING

                                    if (sMsg != "") throw (new Exception(sMsg));
                                }
                            }

                            int nMs = (int)objStopWatch.ElapsedMsec; // dont worry about fractions of ms
                            nSleepTime -= nMs;
                            if (nSleepTime < 1) nSleepTime = 1;
                            Thread.Sleep(nSleepTime);
                        }
                        else
                        {
                            Thread.Sleep(250); // if not visible, sleep for quarter second
                        }
                    }
                }
                catch (Exception e)
                {
                    ShutdownDX(true);
                    MessageBox.Show("Problem in DirectX Meter Renderer !" + System.Environment.NewLine + System.Environment.NewLine + "[ " + e.ToString() + " ]", "DirectX", MessageBoxButtons.OK, MessageBoxIcon.Exclamation, MessageBoxDefaultButton.Button1, Common.MB_TOPMOST);
                }
            }
            private void setupAliasing()
            {
                if (!_bDXSetup) return;

                if (_bAntiAlias)
                    _renderTarget.AntialiasMode = AntialiasMode.PerPrimitive; // this will antialias even if multisampling is off
                else
                    _renderTarget.AntialiasMode = AntialiasMode.Aliased; // this will result in non antialiased lines only if multisampling = 1

                _renderTarget.TextAntialiasMode = TextAntialiasMode.Default;
            }
            public int VerticalBlanks
            {
                get { return _nVBlanks; }
                set
                {
                    int v = value;
                    if (v < 0) v = 0;
                    if (v > 4) v = 4;
                    _nVBlanks = v;
                }
            }
            private int targetWidth
            {
                get
                {
                    //return _displayTarget.Width; 
                    return _targetWidth;
                }
                set
                {
                    int tmp = value;
                    if (tmp < 100) tmp = 100; //see resize drag in ucmeter control

                    _targetWidth = tmp;
                    
                }
            }
            private int targetHeight
            {
                get
                {
                    //return _displayTarget.Height; 
                    return _targetHeight;
                }
                set
                {
                    int tmp = value;
                    if (tmp < 32) tmp = 32; //see resize drag in ucmeter control

                    _targetHeight = tmp;
                }
            }
            private void releaseDXResources()
            {
                if (!_bDXSetup) return;

                clearAllDynamicBrushes();
                clearAllDynamicTextFormats();
            }
            private void buildDXFonts()
            {
                if (!_bDXSetup) return;

                _fontFactory = new SharpDX.DirectWrite.Factory();
            }
            private void releaseDXFonts()
            {
                if (!_bDXSetup) return;

                if (_fontFactory != null) Utilities.Dispose(ref _fontFactory);

                _fontFactory = null;
            }
            private void clearAllDynamicBrushes()
            {
                if (!_bDXSetup || _DXBrushes == null) return;

                foreach (KeyValuePair<System.Drawing.Color, SharpDX.Direct2D1.Brush> kvp in _DXBrushes)
                {
                    SharpDX.Direct2D1.Brush b = kvp.Value;
                    Utilities.Dispose(ref b);
                    b = null;
                }

                _DXBrushes.Clear();
                _DXBrushes = null;
            }
            private void clearAllDynamicTextFormats()
            {
                if (!_bDXSetup || _textFormats == null) return;

                foreach (KeyValuePair<string, SharpDX.DirectWrite.TextFormat> kvp in _textFormats)
                {
                    SharpDX.DirectWrite.TextFormat tf = kvp.Value;
                    Utilities.Dispose(ref tf);
                    tf = null;
                }

                _textFormats.Clear();
                _textFormats = null;

                if (_fontFactory != null) Utilities.Dispose(ref _fontFactory);
                _fontFactory = null;
            }
            private SharpDX.Direct2D1.Brush getDXBrushForColour(System.Drawing.Color c, int replaceAlpha = -1)
            {
                if (!_bDXSetup) return null;

                System.Drawing.Color newC;
                if (replaceAlpha >= 0 && replaceAlpha <= 255)
                    newC = System.Drawing.Color.FromArgb(replaceAlpha, c.R, c.G, c.B); // override the alpha
                else
                    newC = c;

                if (_DXBrushes.ContainsKey(newC)) return _DXBrushes[newC];

                SolidBrush sb = new SolidBrush(newC);
                SharpDX.Direct2D1.Brush b = convertBrush(sb);
                sb.Dispose();

                _DXBrushes.Add(newC, b);

                return b;
            }
            private SharpDX.Direct2D1.SolidColorBrush convertBrush(SolidBrush b)
            {
                return new SharpDX.Direct2D1.SolidColorBrush(_renderTarget, convertColour(b.Color));
            }
            private SharpDX.Color4 convertColour(System.Drawing.Color c)
            {
                return new SharpDX.Color4(c.R / 255f, c.G / 255f, c.B / 255f, c.A / 255f);
            }
            private SharpDX.DirectWrite.TextFormat getDXTextFormatForFont(string sFontFamily, float emSize, FontStyle style, bool bAlignRight = false)
            {
                if (!_bDXSetup) return null;

                string sKey = sFontFamily + emSize.ToString("0.0") + style.ToString();

                if (_textFormats.ContainsKey(sKey)) return _textFormats[sKey];

                try
                {
                    SharpDX.DirectWrite.FontWeight fontWeight = SharpDX.DirectWrite.FontWeight.Regular;
                    SharpDX.DirectWrite.FontStyle fontStyle = SharpDX.DirectWrite.FontStyle.Normal;
                    if (((int)style & (int)FontStyle.Bold) == (int)FontStyle.Bold) fontWeight = SharpDX.DirectWrite.FontWeight.Bold;
                    if (((int)style & (int)FontStyle.Italic) == (int)FontStyle.Italic) fontStyle = SharpDX.DirectWrite.FontStyle.Italic;

                    SharpDX.DirectWrite.TextFormat tf = new SharpDX.DirectWrite.TextFormat(_fontFactory, sFontFamily, fontWeight, fontStyle, (emSize / 72) * _renderTarget.DotsPerInch.Width);
                    tf.WordWrapping = SharpDX.DirectWrite.WordWrapping.NoWrap;
                    if(bAlignRight)
                        tf.TextAlignment = SharpDX.DirectWrite.TextAlignment.Trailing;

                    _textFormats.Add(sKey, tf);

                    return tf;                    
                }
                catch { }

                return null;
            }
            private bool resizeDX()
            {
                lock (_DXlock)
                {
                    if (!_bDXSetup) return false;

                    try
                    {
                        Utilities.Dispose(ref _renderTarget);
                        Utilities.Dispose(ref _surface);

                        _renderTarget = null;
                        _surface = null;
                        
                        _device.ImmediateContext.ClearState();
                        _device.ImmediateContext.Flush();

                        _swapChain1.ResizeBuffers(_nBufferCount, targetWidth, targetHeight, _swapChain.Description.ModeDescription.Format, SwapChainFlags.None);
                        _surface = _swapChain1.GetBackBuffer<Surface>(0);

                        RenderTargetProperties rtp = new RenderTargetProperties(new SharpDX.Direct2D1.PixelFormat(_swapChain.Description.ModeDescription.Format, _ALPHA_MODE));
                        _renderTarget = new RenderTarget(_factory, _surface, rtp);

                        setupAliasing();
                        return true;
                    }
                    catch (Exception e)
                    {
                        ShutdownDX();
                        MessageBox.Show("DirectX resizeDX() Meter failure\n\nThis can sometimes be caused by other programs 'hooking' into directX," +
                            "such as GFX card control software (eg, EVGA Precision Xoc). Close down Thetis, quit as many 'system tray' and other\n" +
                            "things as possible and try again.\n\n" + e.Message, "DirectX", MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1, Common.MB_TOPMOST);
                        return false;
                    }
                }
            }
            //
            private void target_Resize(object sender, System.EventArgs e)
            {
                //Debug.Print(">> target resizing <<");

                _newTargetWidth = _displayTarget.Width;
                _newTargetHeight = _displayTarget.Height;
            }
            private void target_VisibleChanged(object sender, System.EventArgs e)
            {
                //Debug.Print(">> target visible changed <<");

                _targetVisible = _displayTarget.Visible;
            }
            private void OnMouseEnter(object sender, System.EventArgs e)
            {
                PictureBox pb = sender as PictureBox;
                if (pb == null) return;
                string sId = pb.Tag.ToString();
                if (!_meters.ContainsKey(sId)) return;

                clsMeter m = _meters[sId];

                lock (m._meterItemsLock)
                {
                    if (m.SortedMeterItemsForZOrder != null)
                    {
                        foreach (clsMeterItem mi in m.SortedMeterItemsForZOrder)
                        {
                            mi.MouseEntered = true;
                        }
                    }
                }
            }
            private void OnMouseLeave(object sender, System.EventArgs e)
            {
                PictureBox pb = sender as PictureBox;
                if (pb == null) return;
                string sId = pb.Tag.ToString();
                if (!_meters.ContainsKey(sId)) return;

                clsMeter m = _meters[sId];

                lock (m._meterItemsLock)
                {
                    if (m.SortedMeterItemsForZOrder != null)
                    {
                        foreach (clsMeterItem mi in m.SortedMeterItemsForZOrder)
                        {
                            mi.MouseEntered = false;
                        }
                    }
                }
            }
            private void OnMouseMove(object sender, System.Windows.Forms.MouseEventArgs e)
            {
                lock (_metersLock)
                {
                    PictureBox pb = sender as PictureBox;
                    if (pb == null) return;
                    string sId = pb.Tag.ToString();
                    if (!_meters.ContainsKey(sId)) return;

                    clsMeter m = _meters[sId];

                    lock (m._meterItemsLock)
                    {
                        if (m.SortedMeterItemsForZOrder != null)
                        {
                            float tw = targetWidth - 1f;// 0.5f;
                            float rw = m.XRatio;
                            float rh = m.YRatio;

                            SharpDX.RectangleF rect = new SharpDX.RectangleF(0, 0, tw * rw, tw * rh);

                            foreach (clsMeterItem mi in m.SortedMeterItemsForZOrder)
                            {
                                float x = (mi.DisplayTopLeft.X / m.XRatio) * rect.Width;
                                float y = (mi.DisplayTopLeft.Y / m.YRatio) * rect.Height;
                                float w = rect.Width * (mi.Size.Width / m.XRatio);
                                float h = rect.Height * (mi.Size.Height / m.YRatio);

                                SharpDX.RectangleF clickRect = new SharpDX.RectangleF(x, y, w, h);

                                if (clickRect.Contains(new SharpDX.Point(e.X, e.Y)))
                                {
                                    mi.MouseMovePoint = new PointF(e.X, e.Y);
                                    mi.MouseEntered = true;
                                }
                                else
                                {
                                    mi.MouseEntered = false;
                                }
                            }
                        }
                    }
                    HandledMouseEventArgs handledEventArgs = e as HandledMouseEventArgs;
                    if (handledEventArgs != null)
                    {
                        handledEventArgs.Handled = true;
                    }
                }
            }
            private void OnMouseWheel(object sender, System.Windows.Forms.MouseEventArgs e)
            {
                lock (_metersLock)
                {
                    PictureBox pb = sender as PictureBox;
                    if (pb == null) return;
                    string sId = pb.Tag.ToString();
                    if (!_meters.ContainsKey(sId)) return;

                    clsMeter m = _meters[sId];

                    lock (m._meterItemsLock)
                    {
                        if (m.SortedMeterItemsForZOrder != null)
                        {
                            float tw = targetWidth - 1f;// 0.5f;
                            float rw = m.XRatio;
                            float rh = m.YRatio;

                            SharpDX.RectangleF rect = new SharpDX.RectangleF(0, 0, tw * rw, tw * rh);

                            foreach (clsMeterItem mi in m.SortedMeterItemsForZOrder)
                            {
                                float x = (mi.DisplayTopLeft.X / m.XRatio) * rect.Width;
                                float y = (mi.DisplayTopLeft.Y / m.YRatio) * rect.Height;
                                float w = rect.Width * (mi.Size.Width / m.XRatio);
                                float h = rect.Height * (mi.Size.Height / m.YRatio);

                                SharpDX.RectangleF clickRect = new SharpDX.RectangleF(x, y, w, h);

                                if (clickRect.Contains(new SharpDX.Point(e.X, e.Y)))
                                {
                                    mi.MouseEntered = true;
                                    int number_of_moves = e.Delta * SystemInformation.MouseWheelScrollLines / 120;
                                    mi.MouseWheel(number_of_moves);
                                }
                                else
                                {
                                    mi.MouseEntered = false;
                                }
                            }
                        }
                    }
                    HandledMouseEventArgs handledEventArgs = e as HandledMouseEventArgs;
                    if (handledEventArgs != null)
                    {
                        handledEventArgs.Handled = true;
                    }
                }
            }
            private void OnMouseClick(object sender, MouseEventArgs e)
            {
                lock (_metersLock)
                {
                    PictureBox pb = sender as PictureBox;
                    if (pb == null) return;
                    string sId = pb.Tag.ToString();
                    if (!_meters.ContainsKey(sId)) return;

                    clsMeter m = _meters[sId];

                    lock (m._meterItemsLock)
                    {
                        if (m.SortedMeterItemsForZOrder != null)
                        {
                            float tw = targetWidth - 1f;// 0.5f;
                            float rw = m.XRatio;
                            float rh = m.YRatio;

                            SharpDX.RectangleF rect = new SharpDX.RectangleF(0, 0, tw * rw, tw * rh);

                            foreach (clsMeterItem mi in m.SortedMeterItemsForZOrder)
                            {
                                float x = (mi.DisplayTopLeft.X / m.XRatio) * rect.Width;
                                float y = (mi.DisplayTopLeft.Y / m.YRatio) * rect.Height;
                                float w = rect.Width * (mi.Size.Width / m.XRatio);
                                float h = rect.Height * (mi.Size.Height / m.YRatio);

                                SharpDX.RectangleF clickRect = new SharpDX.RectangleF(x, y, w, h);

                                if (clickRect.Contains(new SharpDX.Point(e.X, e.Y)))
                                {
                                    mi.MouseEntered = true;
                                    mi.MouseButton = e.Button;
                                    mi.MouseDownPoint = new PointF(e.X, e.Y);
                                    mi.MouseButtonDown = true;
                                    mi.MouseClick(e);
                                }
                                else
                                {
                                    mi.MouseEntered = false;
                                    mi.MouseButtonDown = false;
                                    mi.MouseClick(e);
                                }
                            }
                        }
                    }
                    HandledMouseEventArgs handledEventArgs = e as HandledMouseEventArgs;
                    if (handledEventArgs != null)
                    {
                        handledEventArgs.Handled = true;
                    }
                }
            }
            private void OnClick(object sender, System.EventArgs e)
            {
                
            }
            private void OnMouseDown(object sender, System.Windows.Forms.MouseEventArgs e)
            {
                lock (_metersLock)
                {
                    PictureBox pb = sender as PictureBox;
                    if (pb == null) return;
                    string sId = pb.Tag.ToString();
                    if (!_meters.ContainsKey(sId)) return;

                    clsMeter m = _meters[sId];

                    lock (m._meterItemsLock)
                    {
                        if (m.SortedMeterItemsForZOrder != null)
                        {
                            float tw = targetWidth - 1f;// 0.5f;
                            float rw = m.XRatio;
                            float rh = m.YRatio;

                            SharpDX.RectangleF rect = new SharpDX.RectangleF(0, 0, tw * rw, tw * rh);

                            foreach (clsMeterItem mi in m.SortedMeterItemsForZOrder)
                            {
                                float x = (mi.DisplayTopLeft.X / m.XRatio) * rect.Width;
                                float y = (mi.DisplayTopLeft.Y / m.YRatio) * rect.Height;
                                float w = rect.Width * (mi.Size.Width / m.XRatio);
                                float h = rect.Height * (mi.Size.Height / m.YRatio);

                                SharpDX.RectangleF clickRect = new SharpDX.RectangleF(x, y, w, h);

                                if (clickRect.Contains(new SharpDX.Point(e.X, e.Y)))
                                {
                                    mi.MouseEntered = true;
                                    mi.MouseButton = e.Button;
                                    mi.MouseDownPoint = new PointF(e.X, e.Y);
                                    mi.MouseButtonDown = true;
                                    mi.MouseDown(e);
                                }
                                else
                                { 
                                    mi.MouseEntered = false;
                                    mi.MouseButtonDown = false;
                                    mi.MouseDown(e);
                                }
                            }
                        }
                    }
                    HandledMouseEventArgs handledEventArgs = e as HandledMouseEventArgs;
                    if (handledEventArgs != null)
                    {
                        handledEventArgs.Handled = true;
                    }
                }
            }
            private void OnKeyPressed(object sender, RawInputEventArg e)
            {
                if (e.KeyPressEvent.KeyPressState == "BREAK") return;

                Debug.Print(e.KeyPressEvent.VKey.ToString() + " -- " + e.KeyPressEvent.ID);

                lock (_metersLock)
                {
                    string sId = e.KeyPressEvent.ID;
                    if (!_meters.ContainsKey(sId)) return;

                    clsMeter m = _meters[sId];

                    lock (m._meterItemsLock)
                    {
                        if (m.SortedMeterItemsForZOrder != null)
                        {
                            foreach (clsMeterItem mi in m.SortedMeterItemsForZOrder)
                            {
                                mi.KeyDown((Keys)e.KeyPressEvent.VKey);
                            }
                        }
                    }
                }
            }
            private void OnMouseUp(object sender, System.Windows.Forms.MouseEventArgs e)
            {
                lock (_metersLock)
                {
                    PictureBox pb = sender as PictureBox;
                    if (pb == null) return;
                    string sId = pb.Tag.ToString();
                    if (!_meters.ContainsKey(sId)) return;

                    clsMeter m = _meters[sId];

                    lock (m._meterItemsLock)
                    {
                        if (m.SortedMeterItemsForZOrder != null)
                        {
                            float tw = targetWidth - 1f;// 0.5f;
                            float rw = m.XRatio;
                            float rh = m.YRatio;

                            SharpDX.RectangleF rect = new SharpDX.RectangleF(0, 0, tw * rw, tw * rh);

                            foreach (clsMeterItem mi in m.SortedMeterItemsForZOrder)
                            {
                                if (mi.ItemType == clsMeterItem.MeterItemType.CLICKBOX)
                                {
                                    clsClickBox cb = (clsClickBox)mi;

                                    if ((!cb.OnlyWhenRX && !cb.OnlyWhenTX) || ((m.MOX && cb.OnlyWhenTX) || (!m.MOX && cb.OnlyWhenRX)))
                                    {
                                        float x = (mi.DisplayTopLeft.X / m.XRatio) * rect.Width;
                                        float y = (mi.DisplayTopLeft.Y / m.YRatio) * rect.Height;
                                        float w = rect.Width * (mi.Size.Width / m.XRatio);
                                        float h = rect.Height * (mi.Size.Height / m.YRatio);

                                        SharpDX.RectangleF clickRect = new SharpDX.RectangleF(x, y, w, h);

                                        if (clickRect.Contains(new SharpDX.Point(e.X, e.Y)))
                                        {
                                            m.MouseUp(e, m, cb);
                                        }
                                    }
                                }
                                else
                                {                                  
                                    float x = (mi.DisplayTopLeft.X / m.XRatio) * rect.Width;
                                    float y = (mi.DisplayTopLeft.Y / m.YRatio) * rect.Height;
                                    float w = rect.Width * (mi.Size.Width / m.XRatio);
                                    float h = rect.Height * (mi.Size.Height / m.YRatio);

                                    SharpDX.RectangleF clickRect = new SharpDX.RectangleF(x, y, w, h);

                                    if (clickRect.Contains(new SharpDX.Point(e.X, e.Y)))
                                    {
                                        mi.MouseButton = e.Button;
                                        mi.MouseUpPoint = new PointF(e.X, e.Y);
                                        mi.MouseButtonDown = false;
                                        mi.MouseUp(e);
                                    }
                                    else
                                    {
                                        mi.MouseButtonDown = false;
                                        mi.MouseUp(e);
                                    }
                                }
                            }
                        }
                    }
                    HandledMouseEventArgs handledEventArgs = e as HandledMouseEventArgs;
                    if (handledEventArgs != null)
                    {
                        handledEventArgs.Handled = true;
                    }
                }
            }

            //            
            private int drawMeters()
            {
                int nRedrawDelay = int.MaxValue;
                clsMeter m = _meter;

                lock (m._meterItemsLock)
                {
                    if (m.RX == _rx && m.SortedMeterItemsForZOrder != null && m.SortedMeterItemsForZOrder.Count > 0)
                    {
                        float tw = targetWidth - 1f;

                        int nTmp = m.MOX ? m.QuickestTXUpdate : m.QuickestRXUpdate;
                        if (nTmp < nRedrawDelay) nRedrawDelay = nTmp;

                        List<clsMeterItem> additionalDraws = new List<clsMeterItem>();
                        //foreach (KeyValuePair<string, clsMeterItem> mikvp in m.SortedMeterItemsForZOrder)
                        foreach (clsMeterItem mi in m.SortedMeterItemsForZOrder)
                        {
                            //clsMeterItem mi = mikvp.Value;

                            bool bRender = ((m.MOX && mi.OnlyWhenTX) || (!m.MOX && mi.OnlyWhenRX)) || (!mi.OnlyWhenTX && !mi.OnlyWhenRX);

                            if (bRender && ((m.DisplayGroup == 0 || mi.DisplayGroup == 0) || (mi.DisplayGroup == m.DisplayGroup)))
                            {
                                float rw = m.XRatio;
                                float rh = m.YRatio;

                                SharpDX.RectangleF rect = new SharpDX.RectangleF(0, 0, tw * rw, tw * rh);

                                switch (mi.ItemType)
                                {
                                    //case clsMeterItem.MeterItemType.V_BAR:
                                    //    renderVBar(rect, mi, m);
                                    //    break;
                                    case clsMeterItem.MeterItemType.H_BAR:
                                        clsMeterItem postDrawItem = renderHBar(rect, mi, m);
                                        if (postDrawItem != null)
                                            renderHBarMarkersOnly(rect, postDrawItem, m); // only draw the marker, this is so primary marker over the top of everything
                                                                                          // reason for this is that zorder for bar_pk and bar_avg causes the avg marker
                                                                                          // top be on top of the pk marker, which we dont want
                                        break;
                                    case clsMeterItem.MeterItemType.SOLID_COLOUR:
                                        renderSolidColour(rect, mi, m);
                                        break;
                                    case clsMeterItem.MeterItemType.NEEDLE:
                                        renderNeedle(rect, mi, m);
                                        break;
                                    case clsMeterItem.MeterItemType.IMAGE:
                                        renderImage(rect, mi, m);
                                        break;
                                    case clsMeterItem.MeterItemType.TEXT:
                                        renderText(rect, mi, m);
                                        break;
                                    case clsMeterItem.MeterItemType.H_SCALE:
                                    case clsMeterItem.MeterItemType.V_SCALE:
                                        renderScale(rect, mi, m);
                                        break;
                                    case clsMeterItem.MeterItemType.NEEDLE_SCALE_PWR:
                                        renderNeedleScale(rect, mi, m);
                                        break;
                                    case clsMeterItem.MeterItemType.MAGIC_EYE:
                                        renderEye(rect, mi, m);
                                        break;
                                    case clsMeterItem.MeterItemType.SPACER:
                                        renderSpacer(rect, mi, m);
                                        break;
                                    case clsMeterItem.MeterItemType.WEB_IMAGE:
                                        renderWebImage(rect, mi, m);
                                        break;
                                    case clsMeterItem.MeterItemType.TEXT_OVERLAY:
                                        renderTextOverlay(rect, mi, m, false);
                                        additionalDraws.Add(mi);
                                        break;
                                    case clsMeterItem.MeterItemType.LED:
                                        renderLed(rect, mi, m, false);
                                        additionalDraws.Add(mi);
                                        break;
                                    case clsMeterItem.MeterItemType.DATA_OUT:
                                        break;
                                    case clsMeterItem.MeterItemType.ROTATOR:
                                        renderRotator(rect, mi, m);
                                        break;
                                    //case clsMeterItem.MeterItemType.HISTORY:
                                    //    renderHistory(rect, mi, m);
                                    //    break;
                                    //case clsMeterItem.MeterItemType.ITEM_GROUP:
                                    //    renderGroup(rect, mi, m);
                                    //    break;
                                    case clsMeterItem.MeterItemType.VFO_DISPLAY:
                                        renderVfoDisplay(rect, mi, m);
                                        break;
                                    case clsMeterItem.MeterItemType.CLOCK:
                                        renderClock(rect, mi, m);
                                        break;
                                    case clsMeterItem.MeterItemType.SIGNAL_TEXT_DISPLAY:
                                        renderSignalTextDisplay(rect, mi, m);
                                        break;
                                    //case clsMeterItem.MeterItemType.SPECTRUM:
                                    //    renderSpectrum(rect, mi, m);
                                    //    break;
                                    case clsMeterItem.MeterItemType.FADE_COVER:
                                        renderFadeCover(rect, mi, m);
                                        break;
                                }
                            }
                        }
                        //foreach (KeyValuePair<string, clsMeterItem> mikvp in additionalDraws)
                        foreach(clsMeterItem mi in additionalDraws)
                        {
                            //clsMeterItem mi = mikvp.Value;

                            float rw = m.XRatio;
                            float rh = m.YRatio;

                            SharpDX.RectangleF rect = new SharpDX.RectangleF(0, 0, tw * rw, tw * rh);

                            switch (mi.ItemType)
                            {
                                case clsMeterItem.MeterItemType.TEXT_OVERLAY:
                                    renderTextOverlay(rect, mi, m, true);
                                    break;
                                case clsMeterItem.MeterItemType.LED:
                                    renderLed(rect, mi, m, true);
                                    break;
                            }
                        }
                    }
                }
                return nRedrawDelay;
            }
            private SizeF measureString(string sText, string sFontFamily, FontStyle style, float emSize, bool ignore_caching = false)
            {
                if (!_bDXSetup) return SizeF.Empty;

                if (emSize == 0) return SizeF.Empty; // zero size text is zero measurement

                emSize = (float)Math.Round(emSize, 2);                

                string sKey = sFontFamily + "_" + style + "_" + sText.Length + "_" + emSize.ToString("0.00");

                if (!ignore_caching && _stringMeasure.ContainsKey(sKey)) return _stringMeasure[sKey];

                SharpDX.DirectWrite.FontWeight fontWeight = SharpDX.DirectWrite.FontWeight.Regular;
                SharpDX.DirectWrite.FontStyle fontStyle = SharpDX.DirectWrite.FontStyle.Normal;
                if (((int)style & (int)FontStyle.Bold) == (int)FontStyle.Bold) fontWeight = SharpDX.DirectWrite.FontWeight.Bold;
                if (((int)style & (int)FontStyle.Italic) == (int)FontStyle.Italic) fontStyle = SharpDX.DirectWrite.FontStyle.Italic;
                
                // calculate how big the string would be @ emSize pt
                SharpDX.DirectWrite.TextFormat tf = new SharpDX.DirectWrite.TextFormat(_fontFactory, sFontFamily, fontWeight, fontStyle, emSize);
                tf.WordWrapping = SharpDX.DirectWrite.WordWrapping.NoWrap;
                
                SharpDX.DirectWrite.TextLayout layout = new SharpDX.DirectWrite.TextLayout(_fontFactory, sText, tf, float.MaxValue, float.MaxValue);
                float width = layout.Metrics.Width;
                float height = layout.Metrics.Height;
                Utilities.Dispose(ref layout);
                layout = null;
                Utilities.Dispose(ref tf);
                tf = null;

                SizeF size = new SizeF(width, height);

                //why these fudge factors? not sure, is it a font proportion issue?
                size.Width *= 1.333333333f;//1.338f
                size.Height *= 1.333333333f;//1.1f;

                bool bAdd = true;
                if (ignore_caching)
                {
                    // try to update existing
                    if(_stringMeasure.ContainsKey(sKey))
                    {
                        SizeF f = _stringMeasure[sKey];
                        f.Width = size.Width;
                        f.Height = size.Height;
                        _stringMeasure[sKey] = f;
                        bAdd = false;
                    }
                }
                if (bAdd)
                {
                    _stringMeasure.Add(sKey, size);
                    _stringMeasureKeys.Enqueue(sKey);
                    if (_stringMeasure.Count > 500)
                    {
                        string oldKey = _stringMeasureKeys.Dequeue();
                        _stringMeasure.Remove(oldKey);
                    }
                }

                return size;
            }
            // [2.10.1.0] MW0LGE
            private int fade(clsMeterItem mi, clsMeter m)
            {
                int dimmed = 48; // the level to dim to
                if (!mi.Disabled && !mi.FadeOnTx && m.MOX && mi.FadeValue == 255) return 255;
                if (!mi.Disabled && !mi.FadeOnRx && !m.MOX && mi.FadeValue == 255) return 255;

                int updateInterval = m.QuickestUpdateInterval(m.MOX);
                updateInterval = Math.Min(updateInterval, 500);
                // fade to take half a second
                int steps_needed = (int)Math.Ceiling(500 / (float)updateInterval);
                int stepSize = (int)Math.Ceiling(207 / (float)steps_needed); // 255-48 = 207

                if (mi.Disabled || (m.MOX && mi.FadeOnTx) || (!m.MOX && mi.FadeOnRx))
                {
                    mi.FadeValue -= stepSize;
                    if (mi.FadeValue < dimmed) mi.FadeValue = dimmed;
                }
                else
                {
                    mi.FadeValue += stepSize;
                    if (mi.FadeValue > 255) mi.FadeValue = 255;
                }

                return mi.FadeValue;
            }
            //
            private void renderNeedleScale(SharpDX.RectangleF rect, clsMeterItem mi, clsMeter m)
            {
                clsNeedleScalePwrItem scale = (clsNeedleScalePwrItem)mi;

                if (scale.ScaleCalibration == null || scale.ScaleCalibration.Count == 0) return;

                float x = (mi.DisplayTopLeft.X / m.XRatio) * rect.Width;
                float y = (mi.DisplayTopLeft.Y / m.YRatio) * rect.Height;
                float w = rect.Width * (mi.Size.Width / m.XRatio);
                float h = rect.Height * (mi.Size.Height / m.YRatio);

                int nFade = 255;

                SharpDX.RectangleF mirect = new SharpDX.RectangleF(x, y, w, h);
                //_renderTarget.DrawRectangle(mirect, getDXBrushForColour(System.Drawing.Color.CornflowerBlue));

                if (scale.ItemType == clsMeterItem.MeterItemType.NEEDLE_SCALE_PWR && (scale.ReadingSource == Reading.PWR || scale.ReadingSource == Reading.REVERSE_PWR))
                {
                    // go through subset of scalecalibration items to get positions for text       (10 for ananmm, 15 for crosspwr, 19 for crosspwrref )  7 8 8          
                    float fontSizeEmScaled = (scale.FontSize / 16f) * (rect.Width / 52f);
                    SizeF szTextSize;
                    int nMaxIndex = -1;

                    for (int n = 0; n < scale.Marks; n++)
                    {
                        int index = 0;
                        float fChangeAngle = 0;

                        if (scale.ScaleCalibration.Count == 10) // the ananmm
                        {
                            switch (n)
                            {
                                case 0: //0
                                    index = 0;
                                    fChangeAngle = (float)degToRad(-11);
                                    break;
                                case 1: //5
                                    index = 1;
                                    fChangeAngle = (float)degToRad(8);
                                    break;
                                case 2: //10
                                    index = 2;
                                    fChangeAngle = (float)degToRad(10);
                                    break;
                                case 3: //25
                                    index = 3;
                                    fChangeAngle = (float)degToRad(5);
                                    break;
                                case 4: //50
                                    index = 6;
                                    break;
                                case 5: //100
                                    index = 8;
                                    fChangeAngle = (float)degToRad(-8);
                                    break;
                                case 6: //150
                                    index = 9;
                                    fChangeAngle = (float)degToRad(-10);
                                    nMaxIndex = index;
                                    break;
                            }
                        }
                        else if (scale.ScaleCalibration.Count == 15) // the cross pwr
                        {
                            switch (n)
                            {
                                case 0: //0
                                    index = 0;
                                    break;
                                case 1: //10
                                    index = 2;
                                    break;
                                case 2: //20
                                    index = 4;
                                    break;
                                case 3: //30
                                    index = 6;
                                    break;
                                case 4: //40
                                    index = 8;
                                    break;
                                case 5: //70
                                    index = 11;
                                    break;
                                case 6: //80
                                    index = 12;
                                    break;
                                case 7: //100
                                    index = 14;
                                    nMaxIndex = index;
                                    break;
                            }
                        }
                        else if (scale.ScaleCalibration.Count == 19) // the cross ref
                        {
                            switch (n)
                            {
                                case 0: //0
                                    index = 0;
                                    break;
                                case 1: //1
                                    index = 4;
                                    break;
                                case 2: //2
                                    index = 5;
                                    break;
                                case 3: //4
                                    index = 7;
                                    break;
                                case 4: //6
                                    index = 9;
                                    break;
                                case 5: //8
                                    index = 11;
                                    break;
                                case 6: //16
                                    index = 16;
                                    break;
                                case 7: //20
                                    index = 18;
                                    nMaxIndex = index;
                                    break;
                            }
                        }

                        Dictionary<float, PointF>.KeyCollection kc = mi.ScaleCalibration.Keys;
                        int key = 0;
                        foreach (float k in kc)
                        {
                            if (index == key)
                            {
                                getPerc(mi, k, out float percX, out float percY, out PointF min, out PointF max);

                                //offset from centre
                                float cX = x + (w / 2);
                                float cY = y + (h / 2);
                                float startX = cX + (w * scale.NeedleOffset.X);
                                float startY = cY + (h * scale.NeedleOffset.Y);

                                float rotation = 180f;

                                float radiusX = (w / 2) * (scale.LengthFactor * scale.RadiusRatio.X);
                                float radiusY = (w / 2) * (scale.LengthFactor * scale.RadiusRatio.Y);

                                //todo
                                switch (scale.Placement)
                                {
                                    case clsNeedleItem.NeedlePlacement.Bottom:
                                        rotation = 180f;
                                        break;
                                    case clsNeedleItem.NeedlePlacement.Left:
                                        //rotation = 90f;
                                        break;
                                    case clsNeedleItem.NeedlePlacement.Top:
                                        //rotation = 0f;
                                        break;
                                    case clsNeedleItem.NeedlePlacement.Right:
                                        //rotation = 270f;
                                        break;
                                }

                                float eX, eY, dX, dY;

                                // map the meter scales to pixels
                                eX = x + (min.X * w) + (percX * ((max.X - min.X) * w));
                                eY = y + (min.Y * h) + (percY * ((max.Y - min.Y) * h));

                                // calc angle required
                                dX = startX - eX;
                                dY = startY - eY;
                                // expand
                                dX /= scale.RadiusRatio.X;
                                dY /= scale.RadiusRatio.Y;
                                float ang = (float)Math.Atan2(dY, dX);

                                float endX = startX + (float)(Math.Cos(ang + degToRad(rotation)) * radiusX);
                                float endY = startY + (float)(Math.Sin(ang + degToRad(rotation)) * radiusY);

                                float fPower = k * (scale.MaxPower / 100f);
                                bool bmW = scale.MaxPower <= 1f; // switch to mW if sub 1 watt
                                if (bmW) fPower *= 1000f;

                                string sText = tidyPower(fPower);
                                if (index == nMaxIndex) sText += bmW ? "mW" : "W"; // last index, add W or mW

                                szTextSize = measureString(sText, scale.FontFamily, scale.FntStyle, fontSizeEmScaled);

                                float fontEndX = endX - (szTextSize.Width / 2f);
                                float fontEndY = endY - (szTextSize.Height / 2f);

                                Matrix3x2 currentTransform = _renderTarget.Transform;

                                Matrix3x2 t = Matrix3x2.Rotation((ang + fChangeAngle) + (float)(degToRad(90f + rotation))/*(float)radToDeg(ang) + rotation*/, new Vector2(endX, endY));
                                t.TranslationVector += _pixelShift;
                                _renderTarget.Transform = t;

                                SharpDX.RectangleF txtrect = new SharpDX.RectangleF(fontEndX, fontEndY, szTextSize.Width, szTextSize.Height);
                                _renderTarget.DrawText(sText, getDXTextFormatForFont(scale.FontFamily, fontSizeEmScaled, scale.FntStyle), txtrect, getDXBrushForColour(scale.LowColour, nFade));

                                _renderTarget.Transform = currentTransform;

                                break;
                            }
                            key++;
                        }
                    }
                }
            }
            private string tidyPower(float fPower)
            {
                float fRemainder = fPower - (int)Math.Floor(fPower);
                string sFormat = "f0";
                if (fRemainder >= 0.1f && fPower <= 8f) sFormat = "f1";

                return fPower.ToString(sFormat);
            }
            private void renderScale(SharpDX.RectangleF rect, clsMeterItem mi, clsMeter m)
            {
                clsScaleItem scale = (clsScaleItem)mi;

                float x = (mi.DisplayTopLeft.X / m.XRatio) * rect.Width;
                float y = (mi.DisplayTopLeft.Y / m.YRatio) * rect.Height;
                float w = rect.Width * (mi.Size.Width / m.XRatio);
                float h = rect.Height * (mi.Size.Height / m.YRatio);

                int nFade = 255;

                SharpDX.RectangleF mirect = new SharpDX.RectangleF(x, y, w, h);
                //_renderTarget.DrawRectangle(mirect, getDXBrushForColour(System.Drawing.Color.CornflowerBlue));

                RawVector2 startPoint = new RawVector2();
                RawVector2 endPoint = new RawVector2();

                if (scale.ItemType == clsMeterItem.MeterItemType.H_SCALE)
                {
                    float fontSizeEmScaled = (scale.FontSize / 16f) * (rect.Width / 52f);
                    SizeF szTextSize;

                    if (scale.ShowType)
                    {
                        // meter type text
                        string sText = scale.ReadingName;//MeterManager.ReadingName(scale.ReadingSource);
                        szTextSize = measureString(sText, scale.FontFamily, scale.FntStyle, fontSizeEmScaled);
                        SharpDX.RectangleF txtrect = new SharpDX.RectangleF(x + (w * 0.5f) - (szTextSize.Width / 2f), y - szTextSize.Height - (h * 0.1f), szTextSize.Width, szTextSize.Height);
                        _renderTarget.DrawText(sText, getDXTextFormatForFont(scale.FontFamily, fontSizeEmScaled, scale.FntStyle), txtrect, getDXBrushForColour(scale.FontColourType, nFade));
                    }

                    //szTextSize = measureString("0", scale.FontFamily, scale.FntStyle, fontSizeEmScaled);

                    float fLineBaseY = y + (h * 0.85f);

                    switch (scale.ReadingSource)
                    {
                        case Reading.AGC_GAIN:
                            {
                                generalScale(x, y, w, h, scale, 7, 1, -50, 125, 25, 25, fLineBaseY, fontSizeEmScaled, nFade);
                            }
                            break;
                        case Reading.AGC_AV:
                        case Reading.AGC_PK:
                            {
                                generalScale(x, y, w, h, scale, 6, 5, -125, 125, 25, 25, fLineBaseY, fontSizeEmScaled, nFade);
                            }
                            break;
                        case Reading.SIGNAL_STRENGTH:
                        case Reading.AVG_SIGNAL_STRENGTH:
                            {
                                generalScale(x,y,w,h, scale, 6, 3, -1, 60, 2, 20, fLineBaseY, fontSizeEmScaled, nFade, 0.5f, true, true);
                            }
                            break;
                        case Reading.ADC_PK:
                        case Reading.ADC_AV:
                            {
                                generalScale(x, y, w, h, scale, 6, 1, -120, 0, 20, 20, fLineBaseY, fontSizeEmScaled, nFade);
                            }
                            break;
                        case Reading.ESTIMATED_PBSNR:
                            {
                                generalScale(x, y, w, h, scale, 6, 1, 0, 60, 10, 10, fLineBaseY, fontSizeEmScaled, nFade);
                            }
                            break;
                        // -30 to 12
                        case Reading.MIC:
                        case Reading.MIC_PK:
                        case Reading.EQ:
                        case Reading.EQ_PK:
                        case Reading.LEVELER:
                        case Reading.LEVELER_PK:
                        case Reading.CFC_AV:
                        case Reading.CFC_PK:
                        case Reading.COMP:
                        case Reading.COMP_PK:
                        //case Reading.CPDR: //CPDR is the same as comp
                        //case Reading.CPDR_PK: //CPDR is the same as comp
                        case Reading.ALC:
                        case Reading.ALC_PK:
                            {
                                generalScale(x, y, w, h, scale, 4, 3, -30, 12, 10, 4, fLineBaseY, fontSizeEmScaled, nFade, 0.665f);
                            }
                            break;
                        case Reading.ALC_GROUP:
                            {
                                generalScale(x, y, w, h, scale, 4, 5, -30, 25, 10, 5, fLineBaseY, fontSizeEmScaled, nFade, 0.5f, true);
                            }
                            break;
                        case Reading.PWR:
                        case Reading.REVERSE_PWR:
                            {
                                //string[] list500 = { "50", "100", "250", "500", "600+" };
                                //string[] list200 = { "10", "20", "100", "200", "240+" };
                                //string[] list100 = { "5", "10", "50", "100", "120+" };
                                //string[] list30 = { "5", "10", "20", "30", "50+" };
                                //string[] list15 = { "1", "5", "10", "15", "25+" };
                                //string[] list1 = { "100", "250", "500", "800", "1000+" };

                                //int nPower = MeterManager.CurrentPowerRating;
                                //string[] powerList;
                                //switch (nPower)
                                //{
                                //    case 500:
                                //        powerList = list500;
                                //        break;
                                //    case 200:
                                //        powerList = list200;
                                //        break;
                                //    case 100:
                                //        powerList = list100;
                                //        break;
                                //    case 30:
                                //        powerList = list30;
                                //        break;
                                //    case 15:
                                //        powerList = list15;
                                //        break;
                                //    case 1:
                                //        powerList = list1;
                                //        break;
                                //    default:
                                //        powerList = list500;
                                //        break;
                                //}

                                SharpDX.Direct2D1.Brush lowColour = getDXBrushForColour(scale.LowColour, nFade);
                                SharpDX.Direct2D1.Brush highColour = getDXBrushForColour(scale.HighColour, nFade);

                                string[] powerList = new string[5];
                                float fMaxPower = scale.MaxPower <= 1f ? scale.MaxPower * 1000f : scale.MaxPower;

                                // scaled to 100w
                                powerList[0] = tidyPower(fMaxPower * (5 / 100f));
                                powerList[1] = tidyPower(fMaxPower * (10 / 100f));
                                powerList[2] = tidyPower(fMaxPower * (50 / 100f));
                                powerList[3] = tidyPower(fMaxPower * (100 / 100f));
                                powerList[4] = tidyPower(fMaxPower * (120 / 100f)) + "+";

                                float spacing = (w * 0.75f) / 4.0f;

                                // horiz line
                                startPoint.X = x;
                                startPoint.Y = fLineBaseY;
                                endPoint.X = x + (w * 0.75f);
                                endPoint.Y = startPoint.Y;
                                _renderTarget.DrawLine(startPoint, endPoint, lowColour, 2f);

                                startPoint.X = endPoint.X;
                                endPoint.X = x + (w * 0.99f);
                                _renderTarget.DrawLine(startPoint, endPoint, highColour, 2f);

                                //low markers
                                for (int i = 1; i < 5; i++)
                                {
                                    // short ticks
                                    startPoint.X = x + (i * spacing) - (spacing * 0.5f); // - half a space to shift left between longer ticks
                                    endPoint.X = startPoint.X;
                                    endPoint.Y = fLineBaseY - (h * 0.15f);

                                    _renderTarget.DrawLine(startPoint, endPoint, lowColour, 2f);

                                    // long ticks
                                    startPoint.X = x + (i * spacing);
                                    endPoint.X = startPoint.X;
                                    endPoint.Y = fLineBaseY - (h * 0.3f);

                                    _renderTarget.DrawLine(startPoint, endPoint, lowColour, 2f);

                                    // text
                                    string sText = powerList[i - 1];
                                    szTextSize = measureString(sText, scale.FontFamily, scale.FntStyle, fontSizeEmScaled);
                                    SharpDX.RectangleF txtrect = new SharpDX.RectangleF(startPoint.X - (szTextSize.Width * 0.5f), endPoint.Y - szTextSize.Height - (h * 0.1f), szTextSize.Width, szTextSize.Height);
                                    _renderTarget.DrawText(sText, getDXTextFormatForFont(scale.FontFamily, fontSizeEmScaled, scale.FntStyle), txtrect, lowColour);
                                }

                                //high markers
                                spacing = ((w * 0.99f) - (w * 0.75f));

                                // short ticks
                                startPoint.X = x + (w * 0.75f) + (1 * spacing) - (spacing * 0.5f); // - half a space to shift left between longer ticks
                                endPoint.X = startPoint.X;
                                endPoint.Y = fLineBaseY - (h * 0.15f);

                                _renderTarget.DrawLine(startPoint, endPoint, highColour, 2f);

                                // long ticks
                                startPoint.X = x + (w * 0.75f) + (1 * spacing);
                                endPoint.X = startPoint.X;
                                endPoint.Y = fLineBaseY - (h * 0.3f);

                                _renderTarget.DrawLine(startPoint, endPoint, highColour, 2f);

                                // text
                                string sText2 = powerList[4];
                                szTextSize = measureString(sText2, scale.FontFamily, scale.FntStyle, fontSizeEmScaled);
                                SharpDX.RectangleF txtrect2 = new SharpDX.RectangleF(startPoint.X - szTextSize.Width, endPoint.Y - szTextSize.Height - (h * 0.1f), szTextSize.Width, szTextSize.Height);
                                _renderTarget.DrawText(sText2, getDXTextFormatForFont(scale.FontFamily, fontSizeEmScaled, scale.FntStyle), txtrect2, highColour);
                            }
                            break;
                        //case MeterTXMode.SWR_POWER:
                        //    {

                        //    }
                        //    break;
                        case Reading.SWR:
                            {
                                SharpDX.Direct2D1.Brush lowColour = getDXBrushForColour(scale.LowColour, nFade);
                                SharpDX.Direct2D1.Brush highColour = getDXBrushForColour(scale.HighColour, nFade);

                                float spacing = (w * 0.75f) / 15.0f;
                                string[] swr_list = { "1.5", "2" };
                                string[] swr_hi_list = { "3", "4", "5" };

                                //g.FillRectangle(low_brush, 0, H - 4, (int)(W * 0.75), 2);
                                //g.FillRectangle(high_brush, (int)(W * 0.75), H - 4, (int)(W * 0.25) - 4, 2);

                                // horiz line
                                startPoint.X = x;
                                startPoint.Y = fLineBaseY;
                                endPoint.X = x + (w * 0.75f);
                                endPoint.Y = startPoint.Y;
                                _renderTarget.DrawLine(startPoint, endPoint, lowColour, 2f);

                                startPoint.X = endPoint.X;
                                endPoint.X = x + (w * 0.99f);
                                _renderTarget.DrawLine(startPoint, endPoint, highColour, 2f);

                                //low markers
                                for (int i = 1; i < 15; i++)
                                {
                                    // short ticks
                                    startPoint.X = x + (i * spacing);
                                    endPoint.X = startPoint.X;
                                    endPoint.Y = fLineBaseY - (h * 0.15f);

                                    _renderTarget.DrawLine(startPoint, endPoint, lowColour, 2f);
                                }

                                //low markers
                                for (int i = 1; i < 15; i++)
                                {
                                    // short ticks
                                    startPoint.X = x + (i * spacing);
                                    endPoint.X = startPoint.X;
                                    endPoint.Y = fLineBaseY - (h * 0.15f);

                                    _renderTarget.DrawLine(startPoint, endPoint, lowColour, 2f);
                                }
                                for (int i = 1; i < 3; i++)
                                {
                                    spacing = (w * 0.5f) / 2.0f;

                                    // long ticks 
                                    startPoint.X = x + (i * spacing);
                                    endPoint.X = startPoint.X;
                                    endPoint.Y = fLineBaseY - (h * 0.3f);

                                    _renderTarget.DrawLine(startPoint, endPoint, lowColour, 2f);

                                    // text
                                    string sText = swr_list[i - 1];
                                    szTextSize = measureString(sText, scale.FontFamily, scale.FntStyle, fontSizeEmScaled);
                                    SharpDX.RectangleF txtrect = new SharpDX.RectangleF(startPoint.X - (szTextSize.Width / 2f), endPoint.Y - szTextSize.Height - (h * 0.1f), szTextSize.Width, szTextSize.Height);
                                    _renderTarget.DrawText(sText, getDXTextFormatForFont(scale.FontFamily, fontSizeEmScaled, scale.FntStyle), txtrect, lowColour);
                                }

                                //high markers
                                spacing = ((w * 0.99f) - (w * 0.75f)) / 2f;

                                for (int i = 1; i < 4; i++)
                                {
                                    if (i < 3) // as small tick will go off then end when i=3
                                    {
                                        // short ticks
                                        startPoint.X = x + (w * 0.75f) + (i * spacing) - (spacing * 0.5f); // - half a space to shift left between longer ticks
                                        endPoint.X = startPoint.X;
                                        endPoint.Y = fLineBaseY - (h * 0.15f);

                                        _renderTarget.DrawLine(startPoint, endPoint, highColour, 2f);
                                    }

                                    // long ticks 
                                    startPoint.X = x + (w * 0.75f) + (i * spacing) - spacing; // - one full space to the left
                                    endPoint.X = startPoint.X;
                                    endPoint.Y = fLineBaseY - (h * 0.3f);

                                    _renderTarget.DrawLine(startPoint, endPoint, highColour, 2f);

                                    // text
                                    string sText = swr_hi_list[i - 1];
                                    szTextSize = measureString(sText, scale.FontFamily, scale.FntStyle, fontSizeEmScaled);
                                    SharpDX.RectangleF txtrect = new SharpDX.RectangleF(startPoint.X - (szTextSize.Width / 2f), endPoint.Y - szTextSize.Height - (h * 0.1f), szTextSize.Width, szTextSize.Height);
                                    _renderTarget.DrawText(sText, getDXTextFormatForFont(scale.FontFamily, fontSizeEmScaled, scale.FntStyle), txtrect, highColour);
                                }
                            }
                            break;
                        case Reading.ALC_G:
                        case Reading.LVL_G:
                        case Reading.CFC_G:
                            {
                                generalScale(x, y, w, h, scale, 5, 1, 0, 25, 5, 5, fLineBaseY, fontSizeEmScaled, nFade, -1, true);                                
                            }
                            break;
                        default:
                            break;
                    }
                }
                else if (scale.ItemType == clsMeterItem.MeterItemType.V_SCALE)
                {
                    float fontSize = 8f;
                    SizeF adjustedFontSize = measureString("0", scale.FontFamily, scale.FntStyle, fontSize);
                    float ratio = w / adjustedFontSize.Width;
                    float newSize = (float)Math.Round((fontSize * ratio) * (fontSize / _renderTarget.DotsPerInch.Width), 1);

                    if (scale.ShowType)
                    {
                        string sText = scale.ReadingName;//MeterManager.ReadingName(scale.ReadingSource);
                        adjustedFontSize = measureString(sText, scale.FontFamily, scale.FntStyle, newSize);
                        SharpDX.RectangleF txtrect = new SharpDX.RectangleF(x, y - (adjustedFontSize.Height * 1.1f), adjustedFontSize.Width, adjustedFontSize.Height);
                        _renderTarget.DrawText(sText, getDXTextFormatForFont(scale.FontFamily, newSize, scale.FntStyle), txtrect, getDXBrushForColour(scale.FontColourType, nFade));
                    }

                    fontSize = 10f;
                    adjustedFontSize = measureString("0", scale.FontFamily, scale.FntStyle, fontSize);
                    ratio = w / adjustedFontSize.Width;
                    newSize = (float)Math.Round((fontSize * ratio) * (fontSize / _renderTarget.DotsPerInch.Width), 1);

                    float fBottomY = y + h;
                    float xCentreLine = x + (w * 0.5f);                    

                    switch (scale.ReadingSource)
                    {
                        case Reading.SIGNAL_STRENGTH:
                        case Reading.AVG_SIGNAL_STRENGTH:
                            {
                                float spacing = (h * 0.5f) / 5.0f;
                                if (scale.ShowMarkers)
                                {
                                    // vert line
                                    startPoint.X = xCentreLine;
                                    startPoint.Y = fBottomY;
                                    endPoint.X = xCentreLine;
                                    endPoint.Y = fBottomY - (h * 0.5f);
                                    _renderTarget.DrawLine(startPoint, endPoint, getDXBrushForColour(scale.LowColour, nFade), 2f);

                                    startPoint.Y = endPoint.Y;
                                    endPoint.Y = fBottomY - (h * 0.99f);
                                    _renderTarget.DrawLine(startPoint, endPoint, getDXBrushForColour(scale.HighColour, nFade), 2f);
                                }

                                // markers low                                
                                for (int i = 1; i < 6; i++)
                                {
                                    if (scale.ShowMarkers)
                                    {
                                        // short ticks
                                        startPoint.X = xCentreLine - (w * 0.1f);
                                        startPoint.Y = fBottomY - ((i * spacing) - (spacing * 0.5f)); // - half a space to shift left between longer ticks
                                        endPoint.X = xCentreLine + (w * 0.1f);
                                        endPoint.Y = startPoint.Y;

                                        _renderTarget.DrawLine(startPoint, endPoint, getDXBrushForColour(scale.LowColour, nFade), 2f);
                                    }

                                    // long ticks
                                    startPoint.X = xCentreLine - (w * 0.3f);
                                    startPoint.Y = fBottomY - (i * spacing);
                                    endPoint.X = xCentreLine + (w * 0.3f);
                                    endPoint.Y = startPoint.Y;

                                    if (scale.ShowMarkers) _renderTarget.DrawLine(startPoint, endPoint, getDXBrushForColour(scale.LowColour, nFade), 2f);

                                    // text
                                    string sText = (-1 + i * 2).ToString();
                                    adjustedFontSize = measureString(sText, scale.FontFamily, scale.FntStyle, newSize);
                                    SharpDX.RectangleF txtrect = new SharpDX.RectangleF(endPoint.X + (w * 0.08f), endPoint.Y - (adjustedFontSize.Height / 2f), adjustedFontSize.Width, adjustedFontSize.Height);
                                    _renderTarget.DrawText(sText, getDXTextFormatForFont(scale.FontFamily, newSize, scale.FntStyle), txtrect, getDXBrushForColour(scale.FontColourLow, nFade));
                                }

                                // markers high
                                spacing = ((h * 0.5f) - (h * 0.01f)) / 3.0f; // - w*0.01f as we only draw the line up to w*0.99f
                                for (int i = 1; i < 4; i++)
                                {

                                    //short ticks
                                    if (scale.ShowMarkers)
                                    {
                                        startPoint.X = xCentreLine - (w * 0.1f);
                                        startPoint.Y = fBottomY -  ((h * 0.5f) + (i * spacing) - (spacing * 0.5f)); // - half a space to shift left between longer ticks
                                        endPoint.X = xCentreLine + (w * 0.1f);
                                        endPoint.Y = startPoint.Y;

                                        _renderTarget.DrawLine(startPoint, endPoint, getDXBrushForColour(scale.HighColour, nFade), 2f);
                                    }

                                    // long ticks
                                    startPoint.X = xCentreLine - (w * 0.3f);
                                    startPoint.Y = fBottomY - ((h * 0.5f) + (i * spacing));
                                    endPoint.X = xCentreLine + (w * 0.3f);
                                    endPoint.Y = startPoint.Y;

                                    if (scale.ShowMarkers) _renderTarget.DrawLine(startPoint, endPoint, getDXBrushForColour(scale.HighColour, nFade), 2f);

                                    // text
                                    string sText = "+" + (i * 20).ToString();
                                    adjustedFontSize = measureString(sText, scale.FontFamily, scale.FntStyle, newSize);
                                    SharpDX.RectangleF txtrect = new SharpDX.RectangleF(endPoint.X - (w * 0.2f), endPoint.Y - (adjustedFontSize.Height / 2f) + (h * 0.01f), adjustedFontSize.Width, adjustedFontSize.Height);
                                    _renderTarget.DrawText(sText, getDXTextFormatForFont(scale.FontFamily, newSize, scale.FntStyle), txtrect, getDXBrushForColour(scale.FontColourHigh, nFade));
                                }
                            }
                            break;
                    }
                }
            }
            private void generalScale(float x,float y,float w,float h,clsScaleItem scale, int lowLongTicks, int highLongTicks, int lowStartNumner, int highEndNumber, int lowIncrement, int highIngrement, float fLineBaseY, float newSize, int nFade, float centrePerc = -1, bool addTrailingPlus = false, bool addAllTrailingPlus = false)
            {
                float lowToHighPoint = (float)(lowLongTicks - 1) / (float)(lowLongTicks + highLongTicks - 1);

                if (centrePerc >= 0) lowToHighPoint = centrePerc;

                float spacing = (w * lowToHighPoint) / (float)(lowLongTicks - 1);//(w * 0.6666f) / 5.0f;
                RawVector2 startPoint = new RawVector2();
                RawVector2 endPoint = new RawVector2();

                SharpDX.Direct2D1.Brush lowColour = getDXBrushForColour(scale.LowColour, nFade);
                SharpDX.Direct2D1.Brush fontLowColour = getDXBrushForColour(scale.FontColourLow, nFade);
                SharpDX.Direct2D1.Brush highColour = getDXBrushForColour(scale.HighColour, nFade);
                SharpDX.Direct2D1.Brush fontHighColour = getDXBrushForColour(scale.FontColourHigh, nFade);

                if (scale.ShowMarkers)
                {
                    // horiz line
                    startPoint.X = x;
                    startPoint.Y = fLineBaseY;
                    endPoint.X = x + (spacing * (float)(lowLongTicks - 1));//(w * 0.6666f);
                    endPoint.Y = startPoint.Y;
                    _renderTarget.DrawLine(startPoint, endPoint, lowColour, 2f);

                    startPoint.X = endPoint.X;
                    endPoint.X = x + (w * 0.99f);
                    _renderTarget.DrawLine(startPoint, endPoint, highColour, 2f);
                }

                // markers low                                
                for (int i = 1; i < lowLongTicks; i++)
                {
                    if (scale.ShowMarkers)
                    {
                        // short ticks
                        startPoint.X = x + (i * spacing) - (spacing * 0.5f); // - half a space to shift left between longer ticks
                        endPoint.X = startPoint.X;
                        endPoint.Y = fLineBaseY - (h * 0.15f);

                        _renderTarget.DrawLine(startPoint, endPoint, lowColour, 2f);
                    }

                    // long ticks
                    startPoint.X = x + (i * spacing);
                    endPoint.X = startPoint.X;
                    endPoint.Y = fLineBaseY - (h * 0.3f);

                    if (scale.ShowMarkers) _renderTarget.DrawLine(startPoint, endPoint, lowColour, 2f);

                    // text
                    string sText = (lowStartNumner + i * lowIncrement).ToString();
                    SizeF szTextSize = measureString(sText, scale.FontFamily, scale.FntStyle, newSize);
                    SharpDX.RectangleF txtrect = new SharpDX.RectangleF(startPoint.X - (szTextSize.Width / 2f), endPoint.Y - szTextSize.Height, szTextSize.Width, szTextSize.Height);
                    _renderTarget.DrawText(sText, getDXTextFormatForFont(scale.FontFamily, newSize, scale.FntStyle), txtrect, fontLowColour);
                }
                
                // markers high
                float lowSpacing = spacing;
                spacing = ((w - (lowSpacing * (float)(lowLongTicks - 1))) - (w * 0.01f)) / (float)highLongTicks; // - w*0.01f as we only draw the line up to w*0.99f
                for (int i = 1; i < highLongTicks + 1; i++)
                {

                    //short ticks
                    if (scale.ShowMarkers)
                    {
                        startPoint.X = x + (lowSpacing * (float)(lowLongTicks-1)) + (i * spacing) - (spacing * 0.5f); // - half a space to shift left between longer ticks
                        endPoint.X = startPoint.X;
                        endPoint.Y = fLineBaseY - (h * 0.15f);

                        _renderTarget.DrawLine(startPoint, endPoint, highColour, 2f);
                    }

                    // long ticks
                    startPoint.X = x + ((lowSpacing * (float)(lowLongTicks - 1))) + (i * spacing);
                    endPoint.X = startPoint.X;
                    endPoint.Y = fLineBaseY - (h * 0.3f);

                    if (scale.ShowMarkers) _renderTarget.DrawLine(startPoint, endPoint, highColour, 2f);

                    // text
                    string sText = ((highEndNumber - (highLongTicks * highIngrement)) + i * highIngrement).ToString();
                    if (addAllTrailingPlus || (i == highLongTicks && addTrailingPlus)) sText += "+";
                    SizeF szTextSize = measureString(sText, scale.FontFamily, scale.FntStyle, newSize);
                    SharpDX.RectangleF txtrect = new SharpDX.RectangleF(i == highLongTicks ? x + w - szTextSize.Width : startPoint.X - szTextSize.Width / 2f, endPoint.Y - szTextSize.Height, szTextSize.Width, szTextSize.Height);
                    _renderTarget.DrawText(sText, getDXTextFormatForFont(scale.FontFamily, newSize, scale.FntStyle), txtrect, fontHighColour);
                }
            }
            private void renderGroup(SharpDX.RectangleF rect, clsMeterItem mi, clsMeter m)
            {
                clsItemGroup ig = (clsItemGroup)mi;

                float x = (mi.DisplayTopLeft.X / m.XRatio) * rect.Width;
                float y = (mi.DisplayTopLeft.Y / m.YRatio) * rect.Height;
                float w = rect.Width * (mi.Size.Width / m.XRatio);
                float h = rect.Height * (mi.Size.Height / m.YRatio);

                SharpDX.RectangleF igrect = new SharpDX.RectangleF(x, y, w, h);
                _renderTarget.DrawRectangle(igrect, getDXBrushForColour(System.Drawing.Color.Green));

                float newSize = 16f;
                string sText = ig.Order.ToString();
                SizeF szTextSize = measureString(sText, "Trebuchet MS", FontStyle.Regular, newSize);
                SharpDX.RectangleF txtrect = new SharpDX.RectangleF(x, y, szTextSize.Width, szTextSize.Height);
                _renderTarget.DrawText(sText, getDXTextFormatForFont("Trebuchet MS", newSize, FontStyle.Regular), txtrect, getDXBrushForColour(System.Drawing.Color.White));
            }
            private void slits(Vector2 centre, float radiusX, float radiusY, float w, float h, SharpDX.Direct2D1.Brush closedSectionBrush)
            {
                const double rads90 = Math.PI / 2f;

                // do the slits slits either side
                PathGeometry sharpGeometry = new PathGeometry(_renderTarget.Factory);
                GeometrySink geo = sharpGeometry.Open();
                //start bottom
                geo.BeginFigure(new SharpDX.Vector2(centre.X, centre.Y + (h * 0.03f)), FigureBegin.Filled);

                // calc radius point right side
                float rX = centre.X + (float)Math.Sin(rads90) * radiusX;
                float rY = centre.Y + (float)Math.Cos(rads90) * radiusY;
                geo.AddLine(new SharpDX.Vector2(rX, rY));

                //to top
                geo.AddLine(new SharpDX.Vector2(centre.X, centre.Y - (h * 0.03f)));

                // calc radius point left side
                rX = centre.X + (float)Math.Sin(-rads90) * radiusX;
                rY = centre.Y + (float)Math.Cos(-rads90) * radiusY;
                geo.AddLine(new SharpDX.Vector2(rX, rY));

                geo.EndFigure(FigureEnd.Closed); // adds the closing line
                geo.Close();

                _renderTarget.FillGeometry(sharpGeometry, closedSectionBrush);

                Utilities.Dispose(ref geo);
                geo = null;
                Utilities.Dispose(ref sharpGeometry);
                sharpGeometry = null;
            }
            private void renderLed(SharpDX.RectangleF rect, clsMeterItem mi, clsMeter m, bool draw_led)
            {
                clsLed led = (clsLed)mi;

                float x = (mi.DisplayTopLeft.X / m.XRatio) * rect.Width;
                float y = (mi.DisplayTopLeft.Y / m.YRatio) * rect.Height;
                float w = rect.Width * (mi.Size.Width / m.XRatio);
                float h = rect.Height * (mi.Size.Height / m.YRatio);

                bool do_cover_fade = (mi.FadeOnRx && !m.MOX) || (mi.FadeOnTx && m.MOX);
                if (!do_cover_fade && (led.PanelBackColour1 != led.PanelBackColour2))
                {
                    if (m.MOX != mi.MOX)
                    {
                        mi.FadeValue = 48;
                        mi.MOX = m.MOX;
                    }
                    else
                    {
                        int updateInterval = m.QuickestUpdateInterval(m.MOX);
                        updateInterval = Math.Min(updateInterval, 500);
                        // fade to take half a second
                        int steps_needed = (int)Math.Ceiling(500 / (float)updateInterval);
                        int stepSize = (int)Math.Ceiling(207 / (float)steps_needed); // 255-48 = 207

                        mi.FadeValue += stepSize;
                        if (mi.FadeValue > 255) mi.FadeValue = 255;
                    }
                }

                if (!draw_led)
                {
                    if (led.ShowBackPanel)
                    {
                        SharpDX.RectangleF rectSC = new SharpDX.RectangleF(x, y, w, h);
                        _renderTarget.FillRectangle(rectSC, getDXBrushForColour(m.MOX ? led.PanelBackColour2 : led.PanelBackColour1, mi.FadeValue));
                    }
                }
                else
                {
                    int intervalSpeed = 250;

                    if (led.ConditionResult != led.OldConditionResult)
                    {
                        if (led.ConditionResult && led.Blink)
                        {
                            led.BlinkCount = 0;
                            led.PulsateUp = true;
                            intervalSpeed = 100;
                            led.ColorFade = 0;
                        }
                    }

                    if (led.Blink || led.Pulsate)
                    {
                        if (led.Blink)
                        {
                            if (led.ColorFade == 1)
                            {
                                if (led.BlinkCount < 2)
                                {
                                    led.PulsateUp = false;
                                    led.BlinkCount++;
                                    intervalSpeed = 100;
                                }
                                else
                                {
                                    led.ColorFade = 0;
                                    led.PulsateUp = false;
                                }
                            }
                            if (led.ColorFade == 0 && led.ConditionResult && led.BlinkCount < 2)
                            {
                                led.PulsateUp = true;
                            }
                        }
                        else
                        {
                            if (led.ColorFade == 1)
                                led.PulsateUp = false;
                            if (led.ColorFade == 0 && led.ConditionResult)
                                led.PulsateUp = true;
                        }
                    }
                    else
                    {
                        if (led.ConditionResult)
                        {
                            led.PulsateUp = true;
                        }
                        else
                        {
                            led.PulsateUp = false;
                        }
                    }

                    int updateInterval = m.QuickestUpdateInterval(m.MOX);
                    updateInterval = Math.Min(updateInterval, intervalSpeed);
                    int steps_needed = (int)Math.Ceiling(intervalSpeed / (float)updateInterval);
                    float stepSize = 1 / (float)steps_needed;

                    System.Drawing.Color c;
                    if (led.PulsateUp)
                    {
                        led.ColorFade += stepSize;
                        c = ColorInterpolator.InterpolateBetween(led.FalseColour, led.TrueColour, led.ColorFade);
                    }
                    else
                    {
                        led.ColorFade -= stepSize;
                        c = ColorInterpolator.InterpolateBetween(led.FalseColour, led.TrueColour, led.ColorFade);
                    }

                    if ((led.ShowTrue && led.ConditionResult) || (led.ShowFalse && !led.ConditionResult))
                    { 
                        float xSize = targetWidth * led.SizeX;
                        float ySize = targetWidth * led.SizeY;
                        float posX = x + led.OffsetX * (targetWidth * m.XRatio);
                        float posY = y + led.OffsetY * (targetWidth * m.YRatio);
                        SharpDX.RectangleF igrect = new SharpDX.RectangleF(posX - (xSize / 2f), posY - (ySize / 2f), xSize, ySize);

                        _renderTarget.FillRectangle(igrect, getDXBrushForColour(c, 255));
                    }
                }
            }
            private void renderTextOverlay(SharpDX.RectangleF rect, clsMeterItem mi, clsMeter m, bool text)
            {
                clsTextOverlay text_overlay = (clsTextOverlay)mi;

                float x = (mi.DisplayTopLeft.X / m.XRatio) * rect.Width;
                float y = (mi.DisplayTopLeft.Y / m.YRatio) * rect.Height;
                float w = rect.Width * (mi.Size.Width / m.XRatio);
                float h = rect.Height * (mi.Size.Height / m.YRatio);

                bool do_cover_fade = (mi.FadeOnRx && !m.MOX) || (mi.FadeOnTx && m.MOX);
                if (!do_cover_fade && (text_overlay.PanelBackColour1 != text_overlay.PanelBackColour2))
                {
                    if (m.MOX != mi.MOX)
                    {
                        mi.FadeValue = 48;
                        mi.MOX = m.MOX;
                    }
                    else
                    {
                        int updateInterval = m.QuickestUpdateInterval(m.MOX);
                        updateInterval = Math.Min(updateInterval, 500);
                        // fade to take half a second
                        int steps_needed = (int)Math.Ceiling(500 / (float)updateInterval);
                        int stepSize = (int)Math.Ceiling(207 / (float)steps_needed); // 255-48 = 207

                        mi.FadeValue += stepSize;
                        if (mi.FadeValue > 255) mi.FadeValue = 255;
                    }
                }

                if (!text)
                {
                    if (text_overlay.ShowBackPanel)
                    {
                        SharpDX.RectangleF rectSC = new SharpDX.RectangleF(x, y, w, h);
                        _renderTarget.FillRectangle(rectSC, getDXBrushForColour(m.MOX ? text_overlay.PanelBackColour2 : text_overlay.PanelBackColour1, mi.FadeValue));
                    }
                }
                else
                {
                    // Determine the text to measure and display
                    string displayText = m.MOX ? text_overlay.ParsedText2(_rx) : text_overlay.ParsedText1(_rx);
                    string fontFamily = m.MOX ? text_overlay.FontFamily2 : text_overlay.FontFamily1;
                    float fontSize = m.MOX ? text_overlay.FontSize2 : text_overlay.FontSize1;
                    fontSize *= 2.1f; // when a container is added, and not resized, 72 point font needs this fudge to get it to be 72 point
                    FontStyle fontStyle = m.MOX ? text_overlay.Style2 : text_overlay.Style1;

                    // Calculate the text position
                    float textX = x + (m.MOX ? text_overlay.TextXOffset2 : text_overlay.TextXOffset1) * (targetWidth * m.XRatio);
                    float textY = y + (m.MOX ? text_overlay.TextYOffset2 : text_overlay.TextYOffset1) * (targetWidth * m.YRatio);

                    if ((text_overlay.ShowTextBackColour1 && !m.MOX) || (text_overlay.ShowTextBackColour2 && m.MOX))
                    {
                        // Measure the text dimensions
                        float fontSizeEmScaled = (fontSize / 16f) * (rect.Width / 52f);
                        SizeF textSize = measureString(displayText, fontFamily, fontStyle, fontSizeEmScaled, m.MOX ? text_overlay.IgnoreMeasureCache2 : text_overlay.IgnoreMeasureCache1);
                        if (!m.MOX && text_overlay.IgnoreMeasureCache1) text_overlay.IgnoreMeasureCache1 = false;
                        if (m.MOX && text_overlay.IgnoreMeasureCache2) text_overlay.IgnoreMeasureCache2 = false;

                        // Draw the background rectangle for the text
                        SharpDX.RectangleF textBackgroundRect = new SharpDX.RectangleF(textX, textY, textSize.Width, textSize.Height);
                        _renderTarget.FillRectangle(textBackgroundRect, getDXBrushForColour(m.MOX ? text_overlay.TextBackColour2 : text_overlay.TextBackColour1, mi.FadeValue));
                    }

                    // Render the text
                    plotText(displayText, textX, textY, h, rect.Width, fontSize, m.MOX ? text_overlay.TextColour2 : text_overlay.TextColour1, 255, fontFamily, fontStyle, false);
                }
            }
            private void renderSpacer(SharpDX.RectangleF rect, clsMeterItem mi, clsMeter m)
            {
                clsSpacerItem spacer = (clsSpacerItem)mi;

                float x = (mi.DisplayTopLeft.X / m.XRatio) * rect.Width;
                float y = (mi.DisplayTopLeft.Y / m.YRatio) * rect.Height;
                float w = rect.Width * (mi.Size.Width / m.XRatio);
                float h = rect.Height * (mi.Size.Height / m.YRatio);

                bool do_cover_fade = (mi.FadeOnRx && !m.MOX) || (mi.FadeOnTx && m.MOX);
                if (!do_cover_fade && (spacer.Colour1 != spacer.Colour2))
                {
                    if (m.MOX != mi.MOX)
                    {
                        mi.FadeValue = 48;
                        mi.MOX = m.MOX;
                    }
                    else
                    {
                        int updateInterval = m.QuickestUpdateInterval(m.MOX);
                        updateInterval = Math.Min(updateInterval, 500);
                        // fade to take half a second
                        int steps_needed = (int)Math.Ceiling(500 / (float)updateInterval);
                        int stepSize = (int)Math.Ceiling(207 / (float)steps_needed); // 255-48 = 207

                        mi.FadeValue += stepSize;
                        if (mi.FadeValue > 255) mi.FadeValue = 255;
                    }
                }

                SharpDX.RectangleF rectSC = new SharpDX.RectangleF(x, y, w, h);

                _renderTarget.FillRectangle(rectSC, getDXBrushForColour(m.MOX ? spacer.Colour2 : spacer.Colour1, mi.FadeValue));
            }
            private void renderWebImage(SharpDX.RectangleF rect, clsMeterItem mi, clsMeter m)
            {
                clsWebImage webimg = (clsWebImage)mi;

                float x = (mi.DisplayTopLeft.X / m.XRatio) * rect.Width;
                float y = (mi.DisplayTopLeft.Y / m.YRatio) * rect.Height;
                float w = rect.Width * (mi.Size.Width / m.XRatio);
                float h = rect.Height * (mi.Size.Height / m.YRatio);

                bool do_cover_fade = (mi.FadeOnRx && !m.MOX) || (mi.FadeOnTx && m.MOX);
                if (!do_cover_fade)
                {
                    if (m.MOX != mi.MOX)
                    {
                        mi.FadeValue = 48;
                        mi.MOX = m.MOX;
                    }
                    else
                    {
                        int updateInterval = m.QuickestUpdateInterval(m.MOX);
                        updateInterval = Math.Min(updateInterval, 500);
                        // fade to take half a second
                        int steps_needed = (int)Math.Ceiling(500 / (float)updateInterval);
                        int stepSize = (int)Math.Ceiling(207 / (float)steps_needed); // 255-48 = 207

                        mi.FadeValue += stepSize;
                        if (mi.FadeValue > 255) mi.FadeValue = 255;
                    }
                }

                SharpDX.RectangleF rectSC = new SharpDX.RectangleF(x, y, w, h);

                if(webimg.Bitmap != null)
                {
                    string key = "webimg_" + webimg.FetcherGuid.ToString();

                    if (!_images.ContainsKey(key))
                    {
                        // convert + add
                        try
                        {
                            SharpDX.Direct2D1.Bitmap img = bitmapFromSystemBitmap(_renderTarget, webimg.Bitmap, key);
                            img.Tag = webimg.BitmapGuid; // guid for web image, we also use this as a bool for skin image
                            _images.Add(key, img);
                        }
                        catch { }
                    }
                    else
                    {
                        // has image changed from the one we put in _images
                        SharpDX.Direct2D1.Bitmap b = _images[key];
                        if((Guid)b.Tag != webimg.BitmapGuid)
                        {
                            // new image, need to remove _image, and the stream cache, and re-add
                            _images[key].Dispose();
                            _images.Remove(key);
                            //

                            //remove from stream cache
                            RemoveStreamData(key);
                            //

                            // convert + add
                            try
                            {
                                SharpDX.Direct2D1.Bitmap img = bitmapFromSystemBitmap(_renderTarget, webimg.Bitmap, key);
                                img.Tag = webimg.BitmapGuid; // guid for web image, we also use this as a bool for skin image
                                _images.Add(key, img);
                            }
                            catch { }
                        }
                    }

                    if (_images.ContainsKey(key))
                    {
                        SharpDX.RectangleF imgRect = new SharpDX.RectangleF(x, y, w, h);

                        SharpDX.Direct2D1.Bitmap b = _images[key];

                        // maintain aspect ratio, the clip removes anything outside the rect
                        float im_w = b.Size.Width;
                        float im_h = b.Size.Height;

                        if (w > h)
                            imgRect.Height = imgRect.Width * (im_h / im_w);
                        else
                            imgRect.Width = imgRect.Height * (im_w / im_h);

                        _renderTarget.DrawBitmap(b, imgRect, 1f, BitmapInterpolationMode.Linear);
                    }
                }
            }
            private string convertDegreesToCardinal(float degrees)
            {
                string[] cardinals = new string[]
                {
                    "N", "NNE", "NE", "ENE", "E", "ESE", "SE", "SSE",
                    "S", "SSW", "SW", "WSW", "W", "WNW", "NW", "NNW", "N"
                };

                float[] boundaries = new float[]
                {
                    0f, 11.25f, 33.75f, 56.25f, 78.75f, 101.25f, 123.75f, 146.25f,
                    168.75f, 191.25f, 213.75f, 236.25f, 258.75f, 281.25f, 303.75f, 326.25f, 348.75f
                };

                degrees = degrees % 360;

                for (int i = 0; i < boundaries.Length - 1; i++)
                {
                    if (degrees >= boundaries[i] && degrees < boundaries[i + 1])
                    {
                        return cardinals[i];
                    }
                }

                return cardinals[0];
            }
            private bool _showing_rotator_ele_drag = false;
            private bool _rotator_was_dragging = false;
            private float _dragging_rotator_degrees = -1;
            private bool _dragging_rotator_ele = false;
            private int _dragging_old_update_rate = -1;
            private float _rotator_az_angle_deg = -999;
            private float _rotator_ele_angle_deg = -999;
            private void renderRotator(SharpDX.RectangleF rect, clsMeterItem mi, clsMeter m)
            {
                clsRotatorItem rotator = (clsRotatorItem)mi;

                float x = (mi.DisplayTopLeft.X / m.XRatio) * rect.Width;
                float y = (mi.DisplayTopLeft.Y / m.YRatio) * rect.Height;
                float w = rect.Width * (mi.Size.Width / m.XRatio);
                float h = rect.Height * (mi.Size.Height / m.YRatio);

                //SharpDX.RectangleF mirect = new SharpDX.RectangleF(x, y, w, h);
                //_renderTarget.DrawRectangle(mirect, getDXBrushForColour(System.Drawing.Color.Green));

                SharpDX.Direct2D1.Brush line_br = getDXBrushForColour(rotator.ArrowColour, 255);
                SharpDX.Direct2D1.Brush big_dot_br = getDXBrushForColour(rotator.BigBlobColour, 255);
                SharpDX.Direct2D1.Brush small_dot_br = getDXBrushForColour(rotator.SmallBlobColour, 255);
                SharpDX.Direct2D1.Brush beam_widh_br = getDXBrushForColour(rotator.BeamWidthColour, 192);

                float xShift = rotator.ViewMode == clsRotatorItem.RotatorMode.BOTH ? 2f * (w * 0.0125f) : 0;

                if (rotator.Primary)
                {
                    if (rotator.ViewMode == clsRotatorItem.RotatorMode.ELE) return; // az is not drawn

                    Vector2 centre = new Vector2(xShift + x + h / 2f, y + h / 2f);
                    Vector2 pos = new Vector2(0, 0);
                    Vector2 pointer_tip = new Vector2(0, 0);
                    Ellipse elipse = new Ellipse(pos, h * 0.01f, h * 0.01f);

                    float radius = (h * 0.8f) / 2f;
                    float radius_text = (h * 0.92f) / 2f;
                    float radius_inner_arrow = (h * 0.75f) / 2f;
                    float radius_tip_arrow = (h * 0.78f) / 2f;
                    float radius_tip_arrow_extra = (h * 0.98f) / 2f; // to include the numbers when clicking to move the rotator
                    float cx;
                    float cy;
                    float rad;
                    float text_scale = rotator.ViewMode == clsRotatorItem.RotatorMode.AZ ? 1.5f : 1f;
                    text_scale *= rotator.ViewMode == clsRotatorItem.RotatorMode.BOTH ? 1f : rotator.Padding;

                    float degrees_az = Math.Abs(rotator.Value) % 360f;
                    bool cardinals = rotator.ShowCardinals;
                    bool show_beam_wdith = rotator.ShowBeamWidth;
                    float beam_width = rotator.BeamWidth;
                    if (cardinals)
                    {
                        // small dots
                        for (int deg = 0; deg <= 350; deg += 10)
                        {
                            rad = (deg - 90) * (float)Math.PI / 180.0f; // Convert degrees to radians, and -90 to top
                            cx = centre.X + radius * (float)Math.Cos(rad);
                            cy = centre.Y + radius * (float)Math.Sin(rad);
                            pos.X = cx; pos.Y = cy;
                            elipse.Point.X = pos.X; elipse.Point.Y = pos.Y;
                            if (deg % 45 != 0)
                            {
                                elipse.RadiusX = h * 0.005f; elipse.RadiusY = h * 0.005f;
                                _renderTarget.FillEllipse(elipse, small_dot_br);
                            }
                        }
                        // big dots and text
                        for (int deg = 0; deg <= 315; deg += 45)
                        {
                            rad = (deg - 90) * (float)Math.PI / 180.0f; // Convert degrees to radians, and -90 to top
                            cx = centre.X + radius * (float)Math.Cos(rad);
                            cy = centre.Y + radius * (float)Math.Sin(rad);
                            pos.X = cx; pos.Y = cy;
                            elipse.Point.X = pos.X; elipse.Point.Y = pos.Y;
                            if (deg % 45 == 0)
                            {
                                elipse.RadiusX = h * 0.015f; elipse.RadiusY = h * 0.015f;
                                _renderTarget.FillEllipse(elipse, big_dot_br);

                                string card = "";
                                switch (deg)
                                {
                                    case 0:
                                        card = "N";
                                        break;
                                    case 45:
                                        card = "NE";
                                        break;
                                    case 90:
                                        card = "E";
                                        break;
                                    case 135:
                                        card = "SE";
                                        break;
                                    case 180:
                                        card = "S";
                                        break;
                                    case 225:
                                        card = "SW";
                                        break;
                                    case 270:
                                        card = "W";
                                        break;
                                    case 315:
                                        card = "NW";
                                        break;
                                }

                                cx = centre.X + radius_text * (float)Math.Cos(rad);
                                cy = centre.Y + radius_text * (float)Math.Sin(rad);
                                plotText(card, cx, cy, h, rect.Width, rotator.FontSize * text_scale, rotator.OuterTextColour, 255, rotator.FontFamily, rotator.Style, false, true, 0, true);
                            }
                        }

                    }
                    else
                    {
                        for (int deg = 0; deg <= 350; deg += 10)
                        {
                            rad = (deg - 90) * (float)Math.PI / 180.0f; // Convert degrees to radians, and -90 to top
                            cx = centre.X + radius * (float)Math.Cos(rad);
                            cy = centre.Y + radius * (float)Math.Sin(rad);
                            pos.X = cx; pos.Y = cy;
                            elipse.Point.X = pos.X; elipse.Point.Y = pos.Y;
                            if (deg % 30 == 0)
                            {
                                elipse.RadiusX = h * 0.015f; elipse.RadiusY = h * 0.015f;
                                _renderTarget.FillEllipse(elipse, big_dot_br);

                                cx = centre.X + radius_text * (float)Math.Cos(rad);
                                cy = centre.Y + radius_text * (float)Math.Sin(rad);
                                plotText(deg.ToString(), cx, cy, h, rect.Width, rotator.FontSize * text_scale, rotator.OuterTextColour, 255, rotator.FontFamily, rotator.Style, false, true, 0, true);
                            }
                            else
                            {
                                elipse.RadiusX = h * 0.005f; elipse.RadiusY = h * 0.005f;
                                _renderTarget.FillEllipse(elipse, small_dot_br);
                            }
                        }
                    }

                    // beam wdith
                    if (show_beam_wdith)
                    {
                        Vector2 arc_edge_1 = new Vector2(0, 0);
                        Vector2 arc_edge_2 = new Vector2(0, 0);
                        beam_width /= 2f;
                        rad = (degrees_az - 90 - beam_width) * (float)Math.PI / 180.0f; // Convert degrees to radians, and -90 to top
                        cx = centre.X + radius_tip_arrow * (float)Math.Cos(rad);
                        cy = centre.Y + radius_tip_arrow * (float)Math.Sin(rad);
                        arc_edge_1.X = cx; arc_edge_1.Y = cy;
                        rad = (degrees_az - 90 + beam_width) * (float)Math.PI / 180.0f; // Convert degrees to radians, and -90 to top
                        cx = centre.X + radius_tip_arrow * (float)Math.Cos(rad);
                        cy = centre.Y + radius_tip_arrow * (float)Math.Sin(rad);
                        arc_edge_2.X = cx; arc_edge_2.Y = cy;

                        PathGeometry sharpGeometry = new PathGeometry(_renderTarget.Factory);

                        GeometrySink geo = sharpGeometry.Open();
                        geo.BeginFigure(new SharpDX.Vector2(centre.X, centre.Y), FigureBegin.Filled);

                        geo.AddLine(new SharpDX.Vector2(arc_edge_1.X, arc_edge_1.Y));

                        ArcSegment arcSegment = new ArcSegment();
                        arcSegment.Point = new SharpDX.Vector2(arc_edge_2.X, arc_edge_2.Y);
                        arcSegment.SweepDirection = SweepDirection.Clockwise;
                        arcSegment.ArcSize = beam_width <= 90f ? ArcSize.Small : ArcSize.Large;
                        arcSegment.Size = new Size2F(radius_tip_arrow, radius_tip_arrow);
                        geo.AddArc(arcSegment);

                        geo.EndFigure(FigureEnd.Closed); // adds the closing line
                        geo.Close();

                        _renderTarget.FillGeometry(sharpGeometry, beam_widh_br);
                    }

                    // arrow tip
                    rad = (degrees_az - 90) * (float)Math.PI / 180.0f; // Convert degrees to radians, and -90 to top
                    cx = centre.X + radius_tip_arrow * (float)Math.Cos(rad);
                    cy = centre.Y + radius_tip_arrow * (float)Math.Sin(rad);
                    pointer_tip.X = cx; pointer_tip.Y = cy;
                    _renderTarget.DrawLine(centre, pointer_tip, line_br, h * 0.01f);

                    // arrow side, offset 3 degrees, and inset
                    rad = (degrees_az - 90 - 3) * (float)Math.PI / 180.0f; // Convert degrees to radians, and -90 to top
                    cx = centre.X + radius_inner_arrow * (float)Math.Cos(rad);
                    cy = centre.Y + radius_inner_arrow * (float)Math.Sin(rad);
                    pos.X = cx; pos.Y = cy;
                    _renderTarget.DrawLine(pointer_tip, pos, line_br, h * 0.01f);

                    rad = (degrees_az - 90 + 3) * (float)Math.PI / 180.0f; // Convert degrees to radians, and -90 to top
                    cx = centre.X + radius_inner_arrow * (float)Math.Cos(rad);
                    cy = centre.Y + radius_inner_arrow * (float)Math.Sin(rad);
                    pos.X = cx; pos.Y = cy;
                    _renderTarget.DrawLine(pointer_tip, pos, line_br, h * 0.01f);
                    //

                    // az text
                    if (rotator.ViewMode == clsRotatorItem.RotatorMode.BOTH)
                    {
                        cx = x + w * 0.75f;
                        cy = y + h * 0.575f;
                        plotText(convertDegreesToCardinal(degrees_az), cx, cy, h, rect.Width, rotator.FontSize * 2f * text_scale, rotator.OuterTextColour, 255, rotator.FontFamily, rotator.Style, false, false, 0, true);
                        plotText(" cardinal", cx - (w * 0.01f), cy + (h * 0.035f), h, rect.Width, rotator.FontSize * text_scale, rotator.OuterTextColour, 255, rotator.FontFamily, rotator.Style, true, false, 0, true);
                        cy = y + h * 0.7f;
                        plotText(degrees_az.ToString("f1") + "°", cx, cy, h, rect.Width, rotator.FontSize * 2f * text_scale, rotator.OuterTextColour, 255, rotator.FontFamily, rotator.Style, false, false, 0, true);
                        plotText("  azimuth", cx - (w * 0.01f), cy + (h * 0.035f), h, rect.Width, rotator.FontSize * text_scale, rotator.OuterTextColour, 255, rotator.FontFamily, rotator.Style, true, false, 0, true);
                    }
                    else
                    {
                        cx = x + w * 0.5f;
                        cy = y + h * 0.525f;
                        plotText(convertDegreesToCardinal(degrees_az), cx, cy, h, rect.Width, rotator.FontSize * 2f * text_scale, rotator.OuterTextColour, 255, rotator.FontFamily, rotator.Style, false, false, 0, true);
                        plotText(" cardinal", cx - (w * 0.01f), cy + (h * 0.035f), h, rect.Width, rotator.FontSize * text_scale, rotator.OuterTextColour, 255, rotator.FontFamily, rotator.Style, true, false, 0, true);
                        cy = y + h * 0.65f;
                        plotText(degrees_az.ToString("f1") + "°", cx, cy, h, rect.Width, rotator.FontSize * 2f * text_scale, rotator.OuterTextColour, 255, rotator.FontFamily, rotator.Style, false, false, 0, true);
                        plotText("  azimuth", cx - (w * 0.01f), cy + (h * 0.035f), h, rect.Width, rotator.FontSize * text_scale, rotator.OuterTextColour, 255, rotator.FontFamily, rotator.Style, true, false, 0, true);
                    }

                    // check if arrow at desination
                    if (degrees_az >= _rotator_az_angle_deg - 3 && degrees_az <= _rotator_az_angle_deg + 3)
                        _rotator_az_angle_deg = -999;

                    if (rotator.AllowControl)
                    {
                        if (rotator.MouseButtonDown && rotator.MouseEntered)
                        {
                            float temp_degrees = 0;
                            SharpDX.Direct2D1.Brush rotator_control_br = getDXBrushForColour(rotator.ControlColour, 255);

                            if (!_rotator_was_dragging && _dragging_old_update_rate == -1)
                            {
                                _dragging_old_update_rate = rotator.UpdateInterval;
                                rotator.UpdateInterval = 16;
                                m.UpdateIntervals();
                            }

                            // find outer edge
                            rad = (temp_degrees - 90) * (float)Math.PI / 180.0f; // Convert degrees to radians, and -90 to top
                            cx = centre.X + radius_tip_arrow_extra * (float)Math.Cos(rad);
                            cy = centre.Y + radius_tip_arrow_extra * (float)Math.Sin(rad);
                            float dist = calculateDistance(new PointF(centre.X, centre.Y), new PointF(cx, cy));
                            float distToMouse = calculateDistance(new PointF(centre.X, centre.Y), rotator.MouseDownPoint);

                            if (distToMouse <= dist)
                            {
                                elipse.Point.X = centre.X;
                                elipse.Point.Y = centre.Y;
                                elipse.RadiusX = h * 0.015f; elipse.RadiusY = h * 0.015f;
                                _renderTarget.FillEllipse(elipse, big_dot_br);

                                //get the angle through the mouse using atan2, radians
                                float deltaX = rotator.MouseMovePoint.X - centre.X;
                                float deltaY = rotator.MouseMovePoint.Y - centre.Y;
                                rad = (float)Math.Atan2(deltaY, deltaX);
                                cx = centre.X + radius_tip_arrow * (float)Math.Cos(rad);
                                cy = centre.Y + radius_tip_arrow * (float)Math.Sin(rad);

                                _renderTarget.DrawLine(centre, new Vector2(cx, cy), rotator_control_br, h * 0.01f);

                                temp_degrees = rad * (180.0f / (float)Math.PI); // the angle we need to send
                                temp_degrees = (temp_degrees + 90) % 360;
                                if (temp_degrees < 0)
                                {
                                    temp_degrees += 360;
                                }
                                _dragging_rotator_degrees = temp_degrees;
                                _dragging_rotator_ele = false;
                                _rotator_was_dragging = true;
                            }
                        }

                        if (_rotator_az_angle_deg != -999)
                        {
                            SharpDX.Direct2D1.Brush rotator_control_br = getDXBrushForColour(rotator.ControlColour, 255);
                            //draw to angle
                            //set to -999 when pointer gets close

                            rad = (_rotator_az_angle_deg - 90) * (float)Math.PI / 180.0f; // Convert degrees to radians, and -90 to top
                            cx = centre.X + radius_tip_arrow * (float)Math.Cos(rad);
                            cy = centre.Y + radius_tip_arrow * (float)Math.Sin(rad);

                            _renderTarget.DrawLine(centre, new Vector2(cx, cy), rotator_control_br, h * 0.01f);
                        }
                    }
                }
                else
                {
                    if (rotator.ViewMode == clsRotatorItem.RotatorMode.AZ) return; // ele is not drawn

                    Vector2 centre = new Vector2(0, 0);// = new Vector2(xShift + x + h / 2f, y + h / 2f);
                    Vector2 pos = new Vector2(0, 0);
                    Vector2 pointer_tip = new Vector2(0, 0);
                    Ellipse elipse = new Ellipse(pos, h * 0.01f, h * 0.01f);

                    float radius = (h * 0.8f) / 2f;
                    float radius_text = (h * 0.92f) / 2f;
                    float radius_inner_arrow = (h * 0.75f) / 2f;
                    float radius_tip_arrow = (h * 0.78f) / 2f;
                    float radius_tip_arrow_extra = (h * 0.98f) / 2f; // to include the numbers when clicking to move the rotator
                    float cx;
                    float cy;
                    float rad;
                    float text_scale = rotator.ViewMode == clsRotatorItem.RotatorMode.ELE ? 1.5f : 1f;
                    text_scale *= rotator.ViewMode == clsRotatorItem.RotatorMode.BOTH ? 1f : rotator.Padding;

                    if (rotator.ViewMode == clsRotatorItem.RotatorMode.ELE)
                    {
                        radius = h * 0.84f;
                        radius_text = (h * 0.90f);
                        radius_inner_arrow = (h * 0.75f);
                        radius_tip_arrow = (h * 0.78f);
                        radius_tip_arrow_extra = (h * 0.98f);
                        centre.X = x + (h / 2f) - (radius / 2f);
                        centre.Y = y + h - (4f * (w * 0.0125f));
                    }
                    else
                    {
                        centre.X = w - xShift - h / 2f;
                        centre.Y = y + h / 2f;
                    }

                    for (int deg = 0; deg <= 90; deg += 5)
                    {
                        rad = (deg - 90) * (float)Math.PI / 180.0f; // Convert degrees to radians, and -90 to top
                        cx = centre.X + radius * (float)Math.Cos(rad);
                        cy = centre.Y + radius * (float)Math.Sin(rad);
                        pos.X = cx; pos.Y = cy;
                        elipse.Point.X = pos.X; elipse.Point.Y = pos.Y;
                        if (deg % 15 == 0)
                        {
                            elipse.RadiusX = h * 0.015f; elipse.RadiusY = h * 0.015f;
                            _renderTarget.FillEllipse(elipse, big_dot_br);

                            cx = centre.X + radius_text * (float)Math.Cos(rad);
                            cy = centre.Y + radius_text * (float)Math.Sin(rad);
                            plotText((90 - deg).ToString(), cx, cy, h, rect.Width, rotator.FontSize * text_scale, rotator.OuterTextColour, 255, rotator.FontFamily, rotator.Style, false, true, 0, false);
                        }
                        else
                        {
                            elipse.RadiusX = h * 0.005f; elipse.RadiusY = h * 0.005f;
                            _renderTarget.FillEllipse(elipse, small_dot_br);
                        }
                    }

                    float degrees_ele = Math.Abs(rotator.Value) % 90f;
                    // arrow tip
                    rad = (-degrees_ele) * (float)Math.PI / 180.0f; // Convert degrees to radians, and -90 to top
                    cx = centre.X + radius_tip_arrow * (float)Math.Cos(rad);
                    cy = centre.Y + radius_tip_arrow * (float)Math.Sin(rad);
                    pointer_tip.X = cx; pointer_tip.Y = cy;
                    _renderTarget.DrawLine(centre, pointer_tip, line_br, h * 0.01f);

                    // arrow side, offset 3 degrees, and inset
                    rad = (-degrees_ele - 3) * (float)Math.PI / 180.0f; // Convert degrees to radians, and -90 to top
                    cx = centre.X + radius_inner_arrow * (float)Math.Cos(rad);
                    cy = centre.Y + radius_inner_arrow * (float)Math.Sin(rad);
                    pos.X = cx; pos.Y = cy;
                    _renderTarget.DrawLine(pointer_tip, pos, line_br, h * 0.01f);

                    rad = (-degrees_ele + 3) * (float)Math.PI / 180.0f; // Convert degrees to radians, and -90 to top
                    cx = centre.X + radius_inner_arrow * (float)Math.Cos(rad);
                    cy = centre.Y + radius_inner_arrow * (float)Math.Sin(rad);
                    pos.X = cx; pos.Y = cy;
                    _renderTarget.DrawLine(pointer_tip, pos, line_br, h * 0.01f);
                    //

                    // ele text
                    if (rotator.ViewMode == clsRotatorItem.RotatorMode.ELE)
                    {
                        cx = x + w * 0.4f;
                        cy = y + h * 0.75f;
                        plotText(degrees_ele.ToString("f1") + "°", cx, cy, h, rect.Width, rotator.FontSize * 2f * text_scale, rotator.OuterTextColour, 255, rotator.FontFamily, rotator.Style, false, false, 0, true);
                        plotText("elevation", cx - (w * 0.01f), cy + (h * 0.035f), h, rect.Width, rotator.FontSize * text_scale, rotator.OuterTextColour, 255, rotator.FontFamily, rotator.Style, true, false, 0, true);
                    }
                    else
                    {
                        cx = x + w * 0.75f;
                        cy = y + h * 0.825f;
                        plotText(degrees_ele.ToString("f1") + "°", cx, cy, h, rect.Width, rotator.FontSize * 2f * text_scale, rotator.OuterTextColour, 255, rotator.FontFamily, rotator.Style, false, false, 0, true);
                        plotText("elevation", cx - (w * 0.01f), cy + (h * 0.035f), h, rect.Width, rotator.FontSize * text_scale, rotator.OuterTextColour, 255, rotator.FontFamily, rotator.Style, true, false, 0, true);
                    }

                    // check if arrow at desination
                    if (degrees_ele >= _rotator_ele_angle_deg - 3 && degrees_ele <= _rotator_ele_angle_deg + 3)
                        _rotator_ele_angle_deg = -999;

                    if (rotator.AllowControl)
                    {
                        if (rotator.MouseButtonDown && rotator.MouseEntered)
                        {
                            float temp_degrees = 0;
                            SharpDX.Direct2D1.Brush rotator_control_br = getDXBrushForColour(rotator.ControlColour, 255);
                            if (!_rotator_was_dragging && _dragging_old_update_rate == -1)
                            {
                                _dragging_old_update_rate = rotator.UpdateInterval;
                                rotator.UpdateInterval = 16;
                                m.UpdateIntervals();
                            }
                            //find outer edge
                            temp_degrees = 0;
                            rad = (temp_degrees - 90) * (float)Math.PI / 180.0f; // Convert degrees to radians, and -90 to top
                            cx = centre.X + radius_tip_arrow_extra * (float)Math.Cos(rad);
                            cy = centre.Y + radius_tip_arrow_extra * (float)Math.Sin(rad);
                            float dist = calculateDistance(new PointF(centre.X, centre.Y), new PointF(cx, cy));
                            float distToMouse = calculateDistance(new PointF(centre.X, centre.Y), rotator.MouseDownPoint);

                            float deltaX = rotator.MouseMovePoint.X - centre.X;
                            float deltaY = rotator.MouseMovePoint.Y - centre.Y;
                            rad = (float)Math.Atan2(deltaY, deltaX);
                            temp_degrees = -rad * (180.0f / (float)Math.PI);

                            if (distToMouse <= dist && ((temp_degrees >= 0 && temp_degrees <= 90) || _showing_rotator_ele_drag))
                            {
                                //get the angle through the mouse using atan2, radians
                                if (temp_degrees > 90) temp_degrees = 90;
                                if (temp_degrees < 0) temp_degrees = 0;

                                rad = rad = temp_degrees * -((float)Math.PI / 180.0f);
                                cx = centre.X + radius_tip_arrow * (float)Math.Cos(rad);
                                cy = centre.Y + radius_tip_arrow * (float)Math.Sin(rad);

                                elipse.Point.X = centre.X;
                                elipse.Point.Y = centre.Y;
                                elipse.RadiusX = h * 0.015f; elipse.RadiusY = h * 0.015f;
                                _renderTarget.FillEllipse(elipse, big_dot_br);

                                _renderTarget.DrawLine(centre, new Vector2(cx, cy), rotator_control_br, h * 0.01f);

                                _showing_rotator_ele_drag = true;
                                _dragging_rotator_degrees = temp_degrees;
                                _dragging_rotator_ele = true;
                                _rotator_was_dragging = true;
                            }
                            else
                                _showing_rotator_ele_drag = false;
                        }
                        else if (_showing_rotator_ele_drag) _showing_rotator_ele_drag = false;

                        if (_rotator_ele_angle_deg != -999)
                        {
                            SharpDX.Direct2D1.Brush rotator_control_br = getDXBrushForColour(rotator.ControlColour, 255);
                            //draw to angle
                            //set to -999 when pointer gets close

                            rad = (-_rotator_ele_angle_deg) * (float)Math.PI / 180.0f; // Convert degrees to radians, and -90 to top
                            cx = centre.X + radius_tip_arrow * (float)Math.Cos(rad);
                            cy = centre.Y + radius_tip_arrow * (float)Math.Sin(rad);

                            _renderTarget.DrawLine(centre, new Vector2(cx, cy), rotator_control_br, h * 0.01f);
                        }
                    }
                }

                //send rotator message
                if (rotator.AllowControl && !rotator.MouseButtonDown && _rotator_was_dragging && _dragging_rotator_degrees >= 0)
                {
                    _rotator_was_dragging = false;

                    if (_dragging_rotator_ele)
                        _rotator_ele_angle_deg = _dragging_rotator_degrees;
                    else
                        _rotator_az_angle_deg = _dragging_rotator_degrees;

                    rotator.SendRotatorMessage(_dragging_rotator_ele, _dragging_rotator_degrees);

                    _dragging_rotator_ele = false;
                    _dragging_rotator_degrees = -1;
                    rotator.UpdateInterval = _dragging_old_update_rate;
                    _dragging_old_update_rate = -1;
                    m.UpdateIntervals();
                }
            }
            private float calculateDistance(PointF point1, PointF point2)
            {
                float deltaX = point2.X - point1.X;
                float deltaY = point2.Y - point1.Y;
                return (float)Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
            }
            private void renderEye(SharpDX.RectangleF rect, clsMeterItem mi, clsMeter m)
            {
                clsMagicEyeItem magicEye = (clsMagicEyeItem)mi;

                float x = (mi.DisplayTopLeft.X / m.XRatio) * rect.Width;
                float y = (mi.DisplayTopLeft.Y / m.YRatio) * rect.Height;
                float w = rect.Width * (mi.Size.Width / m.XRatio);
                float h = rect.Height * (mi.Size.Height / m.YRatio);

                int nFade = 255;

                //SharpDX.RectangleF mirect = new SharpDX.RectangleF(x, y, w, h);
                //_renderTarget.DrawRectangle(mirect, getDXBrushForColour(System.Drawing.Color.Green));

                Vector2 centre = new Vector2(x + w / 2f, y + h / 2f);

                Ellipse eyeElipse = new Ellipse(centre, w / 2f, h / 2f);

                System.Drawing.Color overlapColour = magicEye.Colour;
                SharpDX.Direct2D1.Brush closedSectionBrush = getDXBrushForColour(System.Drawing.Color.FromArgb(255, (int)(overlapColour.R * 0.35f), (int)(overlapColour.G * 0.35f), (int)(overlapColour.B * 0.35f)), nFade);
                System.Drawing.Color dimmedEye = System.Drawing.Color.FromArgb((int)(overlapColour.R * 0.75f), (int)(overlapColour.G * 0.75f), (int)(overlapColour.B * 0.75f));

                getPerc(magicEye, magicEye.Value, out float percX, out float percY, out PointF min, out PointF max);

                // scale percX for overlap hard coded for now
                percX *= 1f / 0.85f;

                float radiusX = w / 2f;
                float radiusY = h / 2f;

                if (percX <= 0.01f)
                {
                    // closed
                    _renderTarget.FillEllipse(eyeElipse, closedSectionBrush);
                }
                else if (percX >= 1f)// 0.99f)
                {
                    // fully open
                    _renderTarget.FillEllipse(eyeElipse, getDXBrushForColour(dimmedEye, nFade));

                    if (percX >= 1f) 
                    {
                        // handle overlap by drawing a segment
                        float fRemaining = percX - 1f;

                        float fDeg = ((int)(360 * fRemaining)) / 2f;
                        float fRad = (float)degToRad(fDeg);

                        float endMaxX = centre.X + (float)Math.Sin(-fRad) * radiusX;
                        float endMaxY = centre.Y + (float)Math.Cos(-fRad) * radiusY;
                        float endMinX = centre.X + (float)Math.Sin(fRad) * radiusX;
                        float endMinY = centre.Y + (float)Math.Cos(fRad) * radiusY;

                        PathGeometry sharpGeometry = new PathGeometry(_renderTarget.Factory);

                        GeometrySink geo = sharpGeometry.Open();
                        geo.BeginFigure(new SharpDX.Vector2(centre.X, centre.Y), FigureBegin.Filled);

                        geo.AddLine(new SharpDX.Vector2(endMinX, endMinY));

                        ArcSegment arcSegment = new ArcSegment();
                        arcSegment.Point = new SharpDX.Vector2(endMaxX, endMaxY);
                        arcSegment.SweepDirection = SweepDirection.Clockwise;
                        arcSegment.ArcSize = fDeg <= 90f ? ArcSize.Small : ArcSize.Large;
                        arcSegment.Size = new Size2F(radiusX, radiusY);
                        geo.AddArc(arcSegment);

                        geo.EndFigure(FigureEnd.Closed); // adds the closing line
                        geo.Close();

                        _renderTarget.FillGeometry(sharpGeometry, getDXBrushForColour(overlapColour, nFade)); // c being brightest

                        Utilities.Dispose(ref geo);
                        geo = null;
                        Utilities.Dispose(ref sharpGeometry);
                        sharpGeometry = null;
                    }

                    slits(centre, radiusX - (w * 0.1f), radiusY - (h * 0.1f), w, h, closedSectionBrush);
                }
                else
                {
                    _renderTarget.FillEllipse(eyeElipse, getDXBrushForColour(dimmedEye, nFade));

                    // draw closed arc to cover
                    float fDeg = (360f - (int)(360 * percX)) / 2f;
                    float fRad = (float)degToRad(fDeg);

                    //yes sin is normally Y axis, but to save angle rotation swap them
                    float endMaxX = centre.X + (float)Math.Sin(-fRad) * radiusX;
                    float endMaxY = centre.Y + (float)Math.Cos(-fRad) * radiusY;
                    float endMinX = centre.X + (float)Math.Sin(fRad) * radiusX;
                    float endMinY = centre.Y + (float)Math.Cos(fRad) * radiusY;

                    PathGeometry sharpGeometry = new PathGeometry(_renderTarget.Factory);

                    GeometrySink geo = sharpGeometry.Open();
                    geo.BeginFigure(new SharpDX.Vector2(centre.X, centre.Y), FigureBegin.Filled);

                    geo.AddLine(new SharpDX.Vector2(endMinX, endMinY));

                    ArcSegment arcSegment = new ArcSegment();
                    arcSegment.Point = new SharpDX.Vector2(endMaxX, endMaxY);
                    arcSegment.SweepDirection = SweepDirection.Clockwise;
                    arcSegment.ArcSize = fDeg <= 90f ? ArcSize.Small : ArcSize.Large;
                    arcSegment.Size = new Size2F(radiusX, radiusY);
                    geo.AddArc(arcSegment);

                    geo.EndFigure(FigureEnd.Closed); // adds the closing line
                    geo.Close();

                    _renderTarget.FillGeometry(sharpGeometry, closedSectionBrush);

                    Utilities.Dispose(ref geo);
                    geo = null;
                    Utilities.Dispose(ref sharpGeometry);
                    sharpGeometry = null;

                    slits(centre, radiusX - (w * 0.1f), radiusY - (h * 0.1f), w, h, closedSectionBrush);
                }

                // centre cap/cover
                eyeElipse.RadiusX = w / 6f;
                eyeElipse.RadiusY = w / 6f;
                _renderTarget.FillEllipse(eyeElipse, getDXBrushForColour(System.Drawing.Color.FromArgb(32, 32, 32)));
            }
            //private void renderHistory(SharpDX.RectangleF rect, clsMeterItem mi, clsMeter m)
            //{
            //    clsHistoryItem hi = (clsHistoryItem)mi;

            //    float x = (mi.DisplayTopLeft.X / m.XRatio) * rect.Width;
            //    float y = (mi.DisplayTopLeft.Y / m.YRatio) * rect.Height;
            //    float w = rect.Width * (mi.Size.Width / m.XRatio);
            //    float h = rect.Height * (mi.Size.Height / m.YRatio);

            //    int nFade = 255;

            //    SharpDX.RectangleF mirect = new SharpDX.RectangleF(x, y, w, h);
            //    _renderTarget.DrawRectangle(mirect, getDXBrushForColour(System.Drawing.Color.Green));

            //    List<float> history = hi.GetHistory();
            //    if (history.Count == 0) return;

            //    float fMax = history.Max();
            //    float fMin = history.Min();
            //    float fSpan = fMax - fMin;
            //    //mi.UpdateInterval

            //    Vector2 start = new Vector2();
            //    Vector2 end = new Vector2();

            //    if (history.Count > 1) 
            //    {
            //        float fScale = 1f;
            //        if (true)//hi.ScaleStrokeWidth)
            //        {
            //            //float fSmallestDimension = w < h ? w : h;
            //            //fScale = fSmallestDimension / 200f;
            //            float fDiag = (float)Math.Sqrt((w * w) + (h * h));
            //            fScale = fDiag / 450f;
            //        }
            //        float fStrokeWidth = hi.StrokeWidth * fScale;

            //        int i = history.Count - 1;
            //        getPerc(hi, history[i], out float percX, out float percY, out PointF min, out PointF max);

            //        start.X = x;
            //        start.Y = y + (h - (h * percY));
            //        float fStep = w / (float)history.Count;

            //        for (int n = 1; n < i; n++)
            //        {
            //            float val = history[i - n];
            //            getPerc(hi, val, out percX, out percY, out min, out max);

            //            end.X = x + (n * fStep);
            //            end.Y = y + (h - (h * percY));

            //            System.Drawing.Color c = val >= -73 ? System.Drawing.Color.Red : System.Drawing.Color.White;
            //            _renderTarget.DrawLine(start, end, getDXBrushForColour(c), fStrokeWidth);

            //            start.X = end.X;
            //            start.Y = end.Y;
            //        }
            //    }
            //}
            private void renderText(SharpDX.RectangleF rect, clsMeterItem mi, clsMeter m)
            {
                clsText txt = (clsText)mi;

                float x = (mi.DisplayTopLeft.X / m.XRatio) * rect.Width;
                float y = (mi.DisplayTopLeft.Y / m.YRatio) * rect.Height;
                float w = rect.Width * (mi.Size.Width / m.XRatio);
                float h = rect.Height * (mi.Size.Height / m.YRatio);

                int nFade = 255;

                string sText;
                switch (txt.Text.ToLower())
                {
                    case "%group%":
                        sText = m.DisplayGroupText;
                        break;
                    //case "%fps%":
                    //    sText = _nFps.ToString();
                    //    break;
                    default:
                        sText = txt.Text;
                        break;
                }

                float fontSize;
                SizeF size;
                if (txt.FixedSize)
                {
                    fontSize = txt.FontSize;
                    size = measureString("0", txt.FontFamily, txt.Style, fontSize);
                }
                else
                {
                    fontSize = 72f;
                    size = measureString(sText, txt.FontFamily, txt.Style, fontSize);
                }

                float ratio = w / size.Width;
                float newSize = (float)Math.Round((fontSize * ratio) * (fontSize / _renderTarget.DotsPerInch.Width), 1);

                SharpDX.RectangleF txtrect = new SharpDX.RectangleF(x, y, w, h);
                _renderTarget.DrawText(sText, getDXTextFormatForFont(txt.FontFamily, newSize, txt.Style), txtrect, getDXBrushForColour(txt.Colour, nFade));
            }
            private void renderHBarMarkersOnly(SharpDX.RectangleF rect, clsMeterItem mi, clsMeter m)
            {
                // similar to renderHBar, but only renders the marker
                clsBarItem cbi = (clsBarItem)mi;

                float x = (mi.DisplayTopLeft.X / m.XRatio) * rect.Width;
                float y = (mi.DisplayTopLeft.Y / m.YRatio) * rect.Height;
                float w = rect.Width * (mi.Size.Width / m.XRatio);
                float h = rect.Height * (mi.Size.Height / m.YRatio);

                int nFade = 255;

                PointF min, max;
                float percX, percY;

                getPerc(cbi, cbi.Value, out percX, out percY, out min, out max);

                float xPos = x + (min.X * w) + (percX * ((max.X - min.X) * w));

                SharpDX.Direct2D1.Brush markerColour = getDXBrushForColour(cbi.MarkerColour, nFade);
                SharpDX.Direct2D1.Brush peakHoldMarkerColour = getDXBrushForColour(cbi.PeakHoldMarkerColour, nFade);

                float maxHistory_x = x;

                if (cbi.PeakHold) // max is used for peak hold
                {
                    getPerc(cbi, cbi.MaxHistory, out percX, out percY, out min, out max);
                    maxHistory_x = x + (min.X * w) + (percX * ((max.X - min.X) * w));
                }

                //peak hold marker
                if (cbi.PeakHold)
                    _renderTarget.DrawLine(new SharpDX.Vector2(maxHistory_x, y), new SharpDX.Vector2(maxHistory_x, y + h), peakHoldMarkerColour, cbi.StrokeWidth);

                //value marker
                if (cbi.ShowMarker)
                    _renderTarget.DrawLine(new SharpDX.Vector2(xPos, y), new SharpDX.Vector2(xPos, y + h), markerColour, cbi.StrokeWidth);
            }
            private clsMeterItem renderHBar(SharpDX.RectangleF rect, clsMeterItem mi, clsMeter m)
            {
                clsBarItem cbi = (clsBarItem)mi;

                float x = (mi.DisplayTopLeft.X / m.XRatio) * rect.Width;
                float y = (mi.DisplayTopLeft.Y / m.YRatio) * rect.Height;
                float w = rect.Width * (mi.Size.Width / m.XRatio);
                float h = rect.Height * (mi.Size.Height / m.YRatio);

                int nFade = 255;

                //SharpDX.RectangleF mirect = new SharpDX.RectangleF(x, y, w, h);
                //_renderTarget.DrawRectangle(mirect, getDXBrushForColour(System.Drawing.Color.Red));

                PointF min, max;
                float percX, percY;

                getPerc(cbi, cbi.Value, out percX, out percY, out min, out max);

                float xPos = x + (min.X * w) + (percX * ((max.X - min.X) * w));

                float minHistory_x = x;
                float maxHistory_x = x;
                if (cbi.ShowHistory)
                {
                    getPerc(cbi, cbi.MinHistory, out percX, out percY, out min, out max);
                    minHistory_x = x + (min.X * w) + (percX * ((max.X - min.X) * w));
                    getPerc(cbi, cbi.MaxHistory, out percX, out percY, out min, out max);
                    maxHistory_x = x + (min.X * w) + (percX * ((max.X - min.X) * w));
                }
                else if (cbi.PeakHold) // max is used for peak hold
                {
                    getPerc(cbi, cbi.MaxHistory, out percX, out percY, out min, out max);
                    maxHistory_x = x + (min.X * w) + (percX * ((max.X - min.X) * w));
                }

                int segmentBlockSize = 7;
                int segmentStep = 9;
                float fHighXPosTransition = float.MaxValue;
                float fBarHeight = h * 0.85f; // same as base line % in render scale

                if (cbi.Style == clsBarItem.BarStyle.Segments || cbi.Style == clsBarItem.BarStyle.SolidFilled)
                {
                    segmentBlockSize = (int)(w * 0.02f);
                    if (segmentBlockSize < 7) segmentBlockSize = 7; // minimum 7 pixels
                    int segmentGapSize = (int)(segmentBlockSize * 0.2f);
                    if (segmentBlockSize < 2) segmentBlockSize = 2; // minimum 2 pixels

                    segmentStep = segmentBlockSize + segmentGapSize; // step, to include block + gap                  

                    if (!cbi.HighPoint.IsEmpty)
                    {
                        fHighXPosTransition = x + (w * cbi.HighPoint.X);
                    }
                }
                SharpDX.Direct2D1.Brush markerColour = getDXBrushForColour(cbi.MarkerColour, nFade);
                SharpDX.Direct2D1.Brush peakValueColour = getDXBrushForColour(cbi.PeakValueColour, nFade);
                SharpDX.Direct2D1.Brush historyColour = getDXBrushForColour(cbi.HistoryColour, nFade < cbi.HistoryColour.A ? nFade : cbi.HistoryColour.A);
                SharpDX.Direct2D1.Brush colour = getDXBrushForColour(cbi.Colour, nFade);
                SharpDX.Direct2D1.Brush colourHigh = getDXBrushForColour(cbi.ColourHigh, nFade);
                SharpDX.Direct2D1.Brush peakHoldMarkerColour = getDXBrushForColour(cbi.PeakHoldMarkerColour, nFade);

                if (cbi.ShowHistory)
                {
                    switch (cbi.Style)
                    {
                        case clsBarItem.BarStyle.None:
                        case clsBarItem.BarStyle.SolidFilled:
                        case clsBarItem.BarStyle.Line:
                            {
                                SharpDX.RectangleF history = new SharpDX.RectangleF(minHistory_x, y, maxHistory_x - minHistory_x, fBarHeight);
                                _renderTarget.FillRectangle(history, historyColour);
                            }
                            break;
                        case clsBarItem.BarStyle.Segments:
                            {
                                int numValueBlocks = (int)((xPos - x) / (float)segmentStep);

                                float i;
                                float startX = (numValueBlocks * segmentStep) + x;
                                SharpDX.RectangleF barRect;

                                for (i = startX; i < maxHistory_x - segmentStep; i += segmentStep)
                                {
                                    barRect = new SharpDX.RectangleF(i, y, segmentBlockSize, fBarHeight);
                                    _renderTarget.FillRectangle(barRect, historyColour);
                                }

                                // complete the end sliver
                                if (i < maxHistory_x)
                                {
                                    barRect = new SharpDX.RectangleF(i, y, maxHistory_x - i, fBarHeight);
                                    _renderTarget.FillRectangle(barRect, historyColour);
                                }
                            }
                            break;
                    }
                }

                switch (cbi.Style)
                {
                    case clsBarItem.BarStyle.SolidFilled:
                        {
                            float fEnd = xPos < fHighXPosTransition ? xPos : fHighXPosTransition;

                            SharpDX.RectangleF barRect = new SharpDX.RectangleF(x, y, fEnd - x, fBarHeight);

                            _renderTarget.FillRectangle(barRect, colour);

                            if (fEnd < xPos) // the area in the high range
                            {
                                // complete the bar
                                barRect = new SharpDX.RectangleF(fEnd, y, xPos - fEnd, fBarHeight);
                                _renderTarget.FillRectangle(barRect, colourHigh);
                            }
                        }
                        break;
                    case clsBarItem.BarStyle.Line:
                        {
                            // marker is drawn below
                        }
                        break;
                    case clsBarItem.BarStyle.Segments:
                        {
                            float i;
                            SharpDX.RectangleF barRect;

                            float fEnd = xPos < fHighXPosTransition ? xPos : fHighXPosTransition;

                            for (i = x; i < fEnd - segmentStep; i += segmentStep)
                            {
                                barRect = new SharpDX.RectangleF(i, y, segmentBlockSize, fBarHeight);
                                _renderTarget.FillRectangle(barRect, colour);
                            }

                            // complete the end sliver up to the high transition
                            if (i < fEnd)
                            {
                                barRect = new SharpDX.RectangleF(i, y, fEnd - i, fBarHeight);
                                _renderTarget.FillRectangle(barRect, colour);
                            }

                            if (xPos > fHighXPosTransition) // the area in the high range
                            {
                                // sliver to complete block at high transition
                                if (i < fEnd)
                                {
                                    barRect = new SharpDX.RectangleF(fEnd, y, i + segmentBlockSize - fEnd, fBarHeight);
                                    _renderTarget.FillRectangle(barRect, colourHigh);
                                }

                                float j;
                                for (j = i + segmentStep; j < xPos - segmentStep; j += segmentStep)
                                {
                                    barRect = new SharpDX.RectangleF(j, y, segmentBlockSize, fBarHeight);
                                    _renderTarget.FillRectangle(barRect, colourHigh);
                                }

                                // complete the end sliver
                                if (j < xPos)
                                {
                                    barRect = new SharpDX.RectangleF(j, y, xPos - j, fBarHeight);
                                    _renderTarget.FillRectangle(barRect, colourHigh);
                                }
                            }
                        }
                        break;
                }

                //peak hold marker
                if (cbi.PeakHold)
                    _renderTarget.DrawLine(new SharpDX.Vector2(maxHistory_x, y), new SharpDX.Vector2(maxHistory_x, y + h), peakHoldMarkerColour, cbi.StrokeWidth);

                //value marker
                if (cbi.ShowMarker)
                    _renderTarget.DrawLine(new SharpDX.Vector2(xPos, y), new SharpDX.Vector2(xPos, y + h), markerColour, cbi.StrokeWidth);

                //value text
                if (cbi.ShowValue)
                {
                    float fontSizeEmScaled = (cbi.FontSize / 16f) * (rect.Width / 52f);

                    string sText;
                    switch (cbi.Unit)
                    {
                        case clsBarItem.Units.S_UNTS:
                            if (cbi.ReadingSource != Reading.ESTIMATED_PBSNR)
                                sText = Common.SMeterFromDBM(cbi.Value, MeterManager.IsAboveS9Frequency(_rx)).Replace(" ", "");
                            else
                                sText = (cbi.Value / 6f).ToString("f1") + "su";
                            break;
                        case clsBarItem.Units.U_V:
                            sText = Common.UVfromDBM(cbi.Value).ToString("f2") + "uV";
                            break;
                        default:
                            sText = cbi.Value.ToString("f1") + MeterManager.ReadingUnits(cbi.ReadingSource);
                            break;
                    }

                    SizeF szTextSize = measureString(sText, cbi.FontFamily, cbi.FntStyle, fontSizeEmScaled);
                    SharpDX.RectangleF txtrect = new SharpDX.RectangleF(x, y - szTextSize.Height - (h * 0.1f), szTextSize.Width, szTextSize.Height);
                    _renderTarget.DrawText(sText, getDXTextFormatForFont(cbi.FontFamily, fontSizeEmScaled, cbi.FntStyle), txtrect, markerColour);
                }
                if (cbi.ShowPeakValue)
                {
                    float fontSizeEmScaled = (cbi.FontSize / 16f) * (rect.Width / 52f);

                    string sText;
                    switch (cbi.Unit)
                    {
                        case clsBarItem.Units.S_UNTS:
                            if (cbi.ReadingSource != Reading.ESTIMATED_PBSNR)
                                sText = Common.SMeterFromDBM(cbi.MaxHistory, MeterManager.IsAboveS9Frequency(_rx)).Replace(" ", "");
                            else
                                sText = (cbi.Value / 6f).ToString("f1") + "su";
                            break;
                        case clsBarItem.Units.U_V:
                            sText = Common.UVfromDBM(cbi.MaxHistory).ToString("f2") + "uV";
                            break;
                        default:
                            sText = cbi.MaxHistory.ToString("f1") + MeterManager.ReadingUnits(cbi.ReadingSource);
                            break;
                    }
                    SizeF szTextSize = measureString(sText, cbi.FontFamily, cbi.FntStyle, fontSizeEmScaled);
                    SharpDX.RectangleF txtrect = new SharpDX.RectangleF(x + w - szTextSize.Width, y - szTextSize.Height - (h * 0.1f), szTextSize.Width, szTextSize.Height);
                    _renderTarget.DrawText(sText, getDXTextFormatForFont(cbi.FontFamily, fontSizeEmScaled, cbi.FntStyle, true), txtrect, peakValueColour);
                }

                return cbi.PostDrawItem;
            }
            //private void renderVBar(SharpDX.RectangleF rect, clsMeterItem mi, clsMeter m)
            //{
            //    clsBarItem cbi = (clsBarItem)mi;

            //    float x = (mi.DisplayTopLeft.X / m.XRatio) * rect.Width;
            //    float y = (mi.DisplayTopLeft.Y / m.YRatio) * rect.Height;
            //    float w = rect.Width * (mi.Size.Width / m.XRatio);
            //    float h = rect.Height * (mi.Size.Height / m.YRatio);

            //    //SharpDX.RectangleF mirect = new SharpDX.RectangleF(x, y, w, h);
            //    //_renderTarget.DrawRectangle(mirect, getDXBrushForColour(System.Drawing.Color.Green));

            //    PointF min, max;
            //    float percX, percY;

            //    getPerc(cbi, cbi.Value, out percX, out percY, out min, out max);

            //    //top left is 0,0
            //    float yBottom = y + h;
            //    //float xPos = x + (min.X * w) + (percX * ((max.X - min.X) * w));
            //    float yPos = yBottom - ((min.Y * h) + (percY * ((max.Y - min.Y) * h)));

            //    float minHistory_y = y;
            //    float maxHistory_y = y;
            //    if (cbi.ShowHistory)
            //    {
            //        getPerc(cbi, cbi.MinHistory, out percX, out percY, out min, out max);
            //        minHistory_y = yBottom - ((min.Y * h) + (percY * ((max.Y - min.Y) * h)));
            //        getPerc(cbi, cbi.MaxHistory, out percX, out percY, out min, out max);
            //        maxHistory_y = yBottom - ((min.Y * h) + (percY * ((max.Y - min.Y) * h)));
            //    }
            //    else if (cbi.PeakHold) // max is used for peak hold
            //    {
            //        getPerc(cbi, cbi.MaxHistory, out percX, out percY, out min, out max);
            //        maxHistory_y = yBottom - ((min.Y * h) + (percY * ((max.Y - min.Y) * h)));
            //    }

            //    int segmentBlockSize = (int)(w * 0.02f);
            //    if (segmentBlockSize < 7) segmentBlockSize = 7;
            //    int segmentGapSize = (int)(segmentBlockSize * 0.2f);
            //    if (segmentBlockSize < 2) segmentBlockSize = 2;
            //    int segmentStep = segmentBlockSize + segmentGapSize;

            //    if (cbi.ShowHistory)
            //    {
            //        switch (cbi.Style)
            //        {
            //            case clsBarItem.BarStyle.None:
            //            case clsBarItem.BarStyle.SolidFilled:
            //            case clsBarItem.BarStyle.Line:
            //                {
            //                    SharpDX.RectangleF history = new SharpDX.RectangleF(x, maxHistory_y, w, minHistory_y - maxHistory_y); // drawn TL to BR
            //                    _renderTarget.FillRectangle(history, getDXBrushForColour(cbi.HistoryColour));
            //                }
            //                break;
            //            //case clsBarItem.BarStyle.Segments:
            //            //    {
            //            //        int numValueBlocks = (int)((xPos - x) / (float)segmentStep);

            //            //        float i;
            //            //        float startX = (numValueBlocks * segmentStep) + x;
            //            //        SharpDX.RectangleF barrect;
            //            //        for (i = startX; i < maxHistory_x - segmentStep; i += segmentStep)
            //            //        {
            //            //            barrect = new SharpDX.RectangleF(i, y, segmentBlockSize, h);
            //            //            _renderTarget.FillRectangle(barrect, getDXBrushForColour(cbi.HistoryColour));
            //            //        }

            //            //        // complete the sliver
            //            //        if (i < maxHistory_x)
            //            //        {
            //            //            barrect = new SharpDX.RectangleF(i, y, maxHistory_x - i, h);
            //            //            _renderTarget.FillRectangle(barrect, getDXBrushForColour(cbi.HistoryColour));
            //            //        }
            //            //    }
            //            //    break;
            //        }
            //    }

            //    switch (cbi.Style)
            //    {
            //        case clsBarItem.BarStyle.SolidFilled:
            //            {
            //                SharpDX.RectangleF barrect = new SharpDX.RectangleF(x, yPos, w, yBottom - yPos);

            //                if (cbi.PeakHold)
            //                    _renderTarget.DrawLine(new SharpDX.Vector2(x, maxHistory_y), new SharpDX.Vector2(x + w, maxHistory_y), getDXBrushForColour(cbi.PeakHoldMarkerColour), cbi.StrokeWidth);

            //                _renderTarget.FillRectangle(barrect, getDXBrushForColour(cbi.Colour));
            //            }
            //            break;
            //        case clsBarItem.BarStyle.Line:
            //            {
            //                if (cbi.PeakHold)
            //                    _renderTarget.DrawLine(new SharpDX.Vector2(x, maxHistory_y), new SharpDX.Vector2(x + w, maxHistory_y), getDXBrushForColour(cbi.PeakHoldMarkerColour), cbi.StrokeWidth);

            //                _renderTarget.DrawLine(new SharpDX.Vector2(x, yPos), new SharpDX.Vector2(x + w, yPos), getDXBrushForColour(cbi.MarkerColour), cbi.StrokeWidth);
            //            }
            //            break;
            //        //case clsBarItem.BarStyle.Segments:
            //        //    {
            //        //        float i;
            //        //        SharpDX.RectangleF barrect;
            //        //        for (i = x; i < xPos - segmentStep; i += segmentStep)
            //        //        {
            //        //            barrect = new SharpDX.RectangleF(i, y, segmentBlockSize, h);
            //        //            _renderTarget.FillRectangle(barrect, getDXBrushForColour(cbi.Colour));
            //        //        }

            //        //        // complete the sliver
            //        //        if (i < xPos)
            //        //        {
            //        //            barrect = new SharpDX.RectangleF(i, y, xPos - i, h);
            //        //            _renderTarget.FillRectangle(barrect, getDXBrushForColour(cbi.Colour));
            //        //        }

            //        //        if (cbi.PeakHold)
            //        //            _renderTarget.DrawLine(new SharpDX.Vector2(maxHistory_x, y), new SharpDX.Vector2(maxHistory_x, y + h), getDXBrushForColour(cbi.PeakHoldMarkerColour), cbi.StrokeWidth);

            //        //        if (cbi.ShowMarker)
            //        //            _renderTarget.DrawLine(new SharpDX.Vector2(xPos, y), new SharpDX.Vector2(xPos, y + h), getDXBrushForColour(cbi.MarkerColour), cbi.StrokeWidth);
            //        //    }
            //        //    break;
            //    }

            //    if (cbi.ShowValue)
            //    {
            //        string sText = cbi.Value.ToString("0.0") + MeterManager.ReadingUnits(cbi.ReadingSource);

            //        float fontSize = 38f;//cbi.FontSize; //38f;
            //        SizeF adjustedFontSize = measureString("0", cbi.FontFamily, cbi.FntStyle, fontSize);
            //        float ratio = h / adjustedFontSize.Height;
            //        float newSize = (float)Math.Round((fontSize * ratio) * (fontSize / _renderTarget.DotsPerInch.Width), 1);

            //        SharpDX.RectangleF txtrect = new SharpDX.RectangleF(x, y + (h * 0.2f), w, h);
            //        _renderTarget.DrawText(sText, getDXTextFormatForFont(cbi.FontFamily, newSize, cbi.FntStyle), txtrect, getDXBrushForColour(cbi.FontColour));
            //    }
            //}
            private void renderSolidColour(SharpDX.RectangleF rect, clsMeterItem mi, clsMeter m)
            {
                clsSolidColour sc = (clsSolidColour)mi;

                float x = (mi.DisplayTopLeft.X / m.XRatio) * rect.Width;
                float y = (mi.DisplayTopLeft.Y / m.YRatio) * rect.Height;
                float w = rect.Width * (mi.Size.Width / m.XRatio);
                float h = rect.Height * (mi.Size.Height / m.YRatio);

                int nFade = 255;

                SharpDX.RectangleF rectSC = new SharpDX.RectangleF(x, y, w, h);
                _renderTarget.FillRectangle(rectSC, getDXBrushForColour(sc.Colour, nFade < sc.Colour.A ? nFade : sc.Colour.A));
            }
            private void renderFadeCover(SharpDX.RectangleF rect, clsMeterItem mi, clsMeter m)
            {
                //clsFadeCover fc = (clsFadeCover)mi;

                float x = (mi.DisplayTopLeft.X / m.XRatio) * rect.Width;
                float y = (mi.DisplayTopLeft.Y / m.YRatio) * rect.Height;
                float w = rect.Width * (mi.Size.Width / m.XRatio);
                float h = rect.Height * (mi.Size.Height / m.YRatio);

                int nFade = 255 - fade(mi, m);

                float wi = w * 0.01f;
                float hi = h * 0.01f;
                wi = Math.Max(2f, wi);
                hi = Math.Max(2f, hi);

                SharpDX.RectangleF rectFC = new SharpDX.RectangleF(x, y, w, h);
                rectFC.Inflate(wi, hi); // increase size slightly as indictator lines stroke width of 3 will be outside bounds
                _renderTarget.FillRectangle(rectFC, getDXBrushForColour(this.BackgroundColour, nFade));
                //_renderTarget.DrawRectangle(rectFC, getDXBrushForColour(System.Drawing.Color.YellowGreen, nFade));
            }
            private void getParts(double vfoFreq, out string MHz, out string kHz, out string hz)
            {
                string vfo = vfoFreq.ToString("0.000000");
                int index = vfo.IndexOf(".");
                MHz = vfo.Substring(0, index);
                kHz = vfo.Substring(index + 1, 3);
                hz = vfo.Substring(index + 4, 3);
            }
            private void plotText(string sText, float x, float y, float h, float containerWidth, float fTextSize, System.Drawing.Color c, int nFade, string sFontFamily, FontStyle style, bool bAlignRight = false, bool bAlignCentre = false, float fit_size = 0, bool ignore_cache = false)
            {
                float fontSizeEmScaled = (fTextSize / 16f) * (containerWidth / 52f);
                SizeF szTextSize;

                szTextSize = measureString(sText, sFontFamily, style, fontSizeEmScaled, ignore_cache);
                if(fit_size > 0 && szTextSize.Width > fit_size)
                {
                    // need to shrink the size by some ratio?
                    float ratio = fit_size / szTextSize.Width;
                    fTextSize *= ratio;
                    fontSizeEmScaled = (fTextSize / 16f) * (containerWidth / 52f);
                    szTextSize = measureString(sText, sFontFamily, style, fontSizeEmScaled, ignore_cache);
                }
                SharpDX.RectangleF txtrect;
                if (!bAlignRight)
                {
                    if (bAlignCentre)
                    {
                        txtrect = new SharpDX.RectangleF(x - szTextSize.Width / 2f, y - szTextSize.Height / 2f, szTextSize.Width, szTextSize.Height);
                    }
                    else
                    {
                        txtrect = new SharpDX.RectangleF(x, y, szTextSize.Width, szTextSize.Height);
                    }
                }
                else
                {
                    // use x is now right edge
                    txtrect = new SharpDX.RectangleF(x - szTextSize.Width, y, szTextSize.Width, szTextSize.Height);
                }
                _renderTarget.DrawText(sText, getDXTextFormatForFont(sFontFamily, fontSizeEmScaled, style), txtrect, getDXBrushForColour(c, nFade));
                //_renderTarget.DrawRectangle(txtrect, getDXBrushForColour(System.Drawing.Color.Red));
            }
            private void highlightBox(float x, float y, float w, float h, SharpDX.RectangleF rect, clsVfoDisplay vfo, int bx, int by, float gap, clsMeter m, float shift)
            {
                SharpDX.RectangleF rct;
                float wB = gap;
                float hB = h / 2f;
                float xB = x + (w * shift) + (bx * gap);
                float yB = y + (by * hB);

                rct = new SharpDX.RectangleF(xB, yB, wB, hB);
                _renderTarget.FillRectangle(rct, getDXBrushForColour(vfo.DigitHighlightColour));
            }
            private clsVfoDisplay.buttonState drawBand(float x, float y, float w, float h, SharpDX.RectangleF rect, clsVfoDisplay vfo, clsMeter m, float shift)
            {
                // draw grid
                SharpDX.Direct2D1.Brush lineBrush = getDXBrushForColour(System.Drawing.Color.White, 255);
                SharpDX.RectangleF rct;
                float xB = 0;
                float yB = y;
                float wB = x + w * 0.47f;
                float hB = h;
                float gap = wB / 8f;
                int nx = 0;
                int ny = 0;
                float mx = 0;
                float my = 0;
                clsVfoDisplay.buttonState button_state = clsVfoDisplay.buttonState.NONE;
                int button_grid_index = -1;

                if (vfo.MouseEntered)
                {
                    mx = (vfo.MouseMovePoint.X - x) / w;
                    my = (vfo.MouseMovePoint.Y - y) / h;
                    mx = (mx - shift) / (wB / w);
                    if (mx >= 0 && mx <= 1 && my >= 0 && my <= 1)
                    {
                        nx = (int)(8 * mx);
                        ny = (int)(2 * my);
                        highlightBox(x, y, w, h, rect, vfo, nx, ny, gap, m, shift);
                        button_grid_index = (ny * 8) + nx;
                        if (shift != 0)
                            button_state = clsVfoDisplay.buttonState.BAND;
                        else
                            button_state = clsVfoDisplay.buttonState.BAND;
                    }
                }

                xB = x + (w * shift);
                rct = new SharpDX.RectangleF(xB, yB, wB, hB);
                _renderTarget.DrawRectangle(rct, lineBrush);
                _renderTarget.DrawLine(new RawVector2(xB, yB + hB / 2f), new RawVector2(xB + wB, yB + hB / 2f), lineBrush);
                for (int i = 1; i < 8; i++)
                {
                    _renderTarget.DrawLine(new RawVector2(xB + (gap * i), y), new RawVector2(xB + (gap * i), y + h), lineBrush);
                }

                nx = 0;
                ny = 0;
                float t_xB;
                float t_yB;
                Band start = Band.FIRST;
                Band end = Band.LAST;
                bool vhf = false;
                if (_console.BandHFSelected)
                {
                    start = Band.B160M;
                    end = Band.B6M;
                }
                else if (_console.BandVHFSelected)
                {
                    start = Band.VHF0;
                    end = Band.VHF13;
                    vhf = true;
                }
                else if (_console.BandGENSelected)
                {
                    start = Band.B120M;
                    end = Band.B11M;
                }
                System.Drawing.Color band_colour;
                string band;
                for (int i = (int)start; i <= (int)end; i++)
                {
                    Band b = (Band)i;

                    if (shift < 0.5f)
                    {
                        if (m.BandVfoA == (Band)i) highlightBox(x, y, w, h, rect, vfo, nx, ny, gap, m, shift);
                    }
                    else
                    {
                        if (m.BandVfoB == (Band)i) highlightBox(x, y, w, h, rect, vfo, nx, ny, gap, m, shift);
                    }

                    if (vhf)
                    {
                        int idx = (ny * 8) + nx;
                        if (_console.GetVHFEnabled(idx))
                            band_colour = BandStackManager.BandToColour(b);
                        else
                            band_colour = System.Drawing.Color.FromArgb(64, 64, 64);
                        
                        band = _console.GetVHFText(idx).Left(3);
                        if (string.IsNullOrEmpty(band))
                        {
                            band = BandStackManager.BandToString(b);
                            band = band.Substring(3);
                        }
                    }
                    else
                    {                        
                        band = BandStackManager.BandToString(b);
                        band = band.Left(band.Length - 1);
                        band_colour = BandStackManager.BandToColour(b);
                    }

                    t_xB = xB + (gap / 2f) + (nx * gap);
                    t_yB = yB + (hB / 4f) + (ny * (hB / 2f));
                    plotText(band, t_xB, t_yB, h, rect.Width, 20f, band_colour, 255, vfo.FontFamily, vfo.Style, false, true, 0, true);
                    nx++;
                    if (nx > 7)
                    {
                        nx = 0;
                        ny += 1;
                    }
                }

                t_xB = xB + (gap / 2f) + (nx * gap);
                t_yB = yB + (hB / 4f) + (ny * (hB / 2f));
                if (_console.BandHFSelected)
                {
                    plotText("VHF", t_xB, t_yB, h, rect.Width, 16f, System.Drawing.Color.Yellow, 255, vfo.FontFamily, vfo.Style, false, true, 0, true);
                    nx++;
                    t_xB = xB + (gap / 2f) + (nx * gap);
                    plotText("WWV", t_xB, t_yB, h, rect.Width, 14f, BandStackManager.BandToColour(Band.WWV), 255, vfo.FontFamily, vfo.Style, false, true, 0, true);
                    nx++;
                    t_xB = xB + (gap / 2f) + (nx * gap);
                    plotText("SWL", t_xB, t_yB, h, rect.Width, 16f, System.Drawing.Color.Orange, 255, vfo.FontFamily, vfo.Style, false, true, 0, true);
                }
                else if (_console.BandGENSelected || _console.BandVHFSelected)
                {
                    plotText("HF", t_xB, t_yB, h, rect.Width, 16f, System.Drawing.Color.Red, 255, vfo.FontFamily, vfo.Style, false, true, 0, true);
                }

                //plotText($"{mx.ToString("f3")},{my.ToString("f3")}", x, y, h, rect.Width, 12, System.Drawing.Color.White, 255, vfo.FontFamily, vfo.Style);

                if (shift != 0)
                    vfo.ButtonGridIndexVFOb = button_grid_index;
                else
                    vfo.ButtonGridIndexVFOa = button_grid_index;

                return button_state;
            }
            private clsVfoDisplay.buttonState drawMode(float x, float y, float w, float h, SharpDX.RectangleF rect, clsVfoDisplay vfo, clsMeter m, float shift)
            {
                // draw grid
                SharpDX.Direct2D1.Brush lineBrush = getDXBrushForColour(System.Drawing.Color.White, 255);
                SharpDX.RectangleF rct;
                float xB = 0;
                float yB = y;
                float wB = x + w * 0.47f;
                float hB = h;
                float gap = wB / 6f;
                int nx = 0;
                int ny = 0;
                float mx = 0;
                float my = 0;
                clsVfoDisplay.buttonState button_state = clsVfoDisplay.buttonState.NONE;
                int button_grid_index = -1;

                if (vfo.MouseEntered)
                {
                    mx = (vfo.MouseMovePoint.X - x) / w;
                    my = (vfo.MouseMovePoint.Y - y) / h;
                    mx = (mx - shift) / (wB / w);
                    if (mx >= 0 && mx <= 1 && my >= 0 && my <= 1)
                    {
                        nx = (int)(6 * mx);
                        ny = (int)(2 * my);
                        highlightBox(x, y, w, h, rect, vfo, nx, ny, gap, m, shift);
                        button_grid_index = (ny * 6) + nx;
                        if (shift != 0)
                            button_state = clsVfoDisplay.buttonState.MODE;
                        else
                            button_state = clsVfoDisplay.buttonState.MODE;
                    }
                }

                xB = x + (w * shift);
                rct = new SharpDX.RectangleF(xB, yB, wB, hB);
                _renderTarget.DrawRectangle(rct, lineBrush);
                _renderTarget.DrawLine(new RawVector2(xB, yB + hB / 2f), new RawVector2(xB + wB, yB + hB / 2f), lineBrush);
                for (int i = 1; i < 6; i++)
                {
                    _renderTarget.DrawLine(new RawVector2(xB + (gap * i), y), new RawVector2(xB + (gap * i), y + h), lineBrush);
                }

                nx = 0;
                ny = 0;
                float t_xB;
                float t_yB;
                clsVfoDisplay.DSPModeForModeDisplay start = clsVfoDisplay.DSPModeForModeDisplay.LSB;
                clsVfoDisplay.DSPModeForModeDisplay end = clsVfoDisplay.DSPModeForModeDisplay.DRM;
                System.Drawing.Color mode_colour;
                string mode;
                for (int i = (int)start; i <= (int)end; i++)
                {
                    clsVfoDisplay.DSPModeForModeDisplay mde = (clsVfoDisplay.DSPModeForModeDisplay)i;
                    string modeString = mde.ToString();
                    Enum.TryParse<DSPMode>(modeString, out DSPMode dsp_mode);                    
                    if (shift < 0.5f)
                    {
                        if (m.ModeVfoA == dsp_mode) highlightBox(x, y, w, h, rect, vfo, nx, ny, gap, m, shift);
                    }
                    else
                    {
                        if (m.ModeVfoB == dsp_mode) highlightBox(x, y, w, h, rect, vfo, nx, ny, gap, m, shift);
                    }

                    mode = mde.ToString();
                    mode_colour = System.Drawing.Color.White;

                    t_xB = xB + (gap / 2f) + (nx * gap);
                    t_yB = yB + (hB / 4f) + (ny * (hB / 2f));
                    plotText(mode, t_xB, t_yB, h, rect.Width, 18f, mode_colour, 255, vfo.FontFamily, vfo.Style, false, true, 0, true);
                    nx++;
                    if (nx > 5)
                    {
                        nx = 0;
                        ny += 1;
                    }
                }

                //plotText($"{mx.ToString("f3")},{my.ToString("f3")}", x, y, h, rect.Width, 12, System.Drawing.Color.White, 255, vfo.FontFamily, vfo.Style);

                if (shift != 0)
                    vfo.ButtonGridIndexVFOb = button_grid_index;
                else
                    vfo.ButtonGridIndexVFOa = button_grid_index;

                return button_state;
            }
            private clsVfoDisplay.buttonState drawFilter(float x, float y, float w, float h, SharpDX.RectangleF rect, clsVfoDisplay vfo, clsMeter m, float shift)
            {
                // draw grid
                SharpDX.Direct2D1.Brush lineBrush = getDXBrushForColour(System.Drawing.Color.White, 255);
                SharpDX.RectangleF rct;
                float xB = 0;
                float yB = y;
                float wB = x + w * 0.47f;
                float hB = h;
                float gap = wB / 6f;
                int nx = 0;
                int ny = 0;
                float mx = 0;
                float my = 0;
                clsVfoDisplay.buttonState button_state = clsVfoDisplay.buttonState.NONE;
                int button_grid_index = -1;

                if (vfo.MouseEntered)
                {
                    mx = (vfo.MouseMovePoint.X - x) / w;
                    my = (vfo.MouseMovePoint.Y - y) / h;
                    mx = (mx - shift) / (wB / w);
                    if (mx >= 0 && mx <= 1 && my >= 0 && my <= 1)
                    {
                        nx = (int)(6 * mx);
                        ny = (int)(2 * my);
                        highlightBox(x, y, w, h, rect, vfo, nx, ny, gap, m, shift);
                        button_grid_index = (ny * 6) + nx;
                        if (shift != 0)
                            button_state = clsVfoDisplay.buttonState.FILTER;
                        else
                            button_state = clsVfoDisplay.buttonState.FILTER;
                    }
                }

                xB = x + (w * shift);
                rct = new SharpDX.RectangleF(xB, yB, wB, hB);
                _renderTarget.DrawRectangle(rct, lineBrush);
                _renderTarget.DrawLine(new RawVector2(xB, yB + hB / 2f), new RawVector2(xB + wB, yB + hB / 2f), lineBrush);
                for (int i = 1; i < 6; i++)
                {
                    _renderTarget.DrawLine(new RawVector2(xB + (gap * i), y), new RawVector2(xB + (gap * i), y + h), lineBrush);
                }

                nx = 0;
                ny = 0;
                float t_xB;
                float t_yB;
                Filter start = Filter.F1;
                Filter end;
                if (shift < 0.5f)
                    end = Filter.VAR2;
                else
                {
                    if (m.RX2Enabled)
                        end = Filter.F7;
                    else
                        end = Filter.VAR2;
                }
                System.Drawing.Color filter_colour;
                string filter;
                for (int i = (int)start; i <= (int)end; i++)
                {
                    Filter fltr = (Filter)i;
                    if (shift < 0.5f)
                    {
                        if (m.FilterVfoA == fltr) highlightBox(x, y, w, h, rect, vfo, nx, ny, gap, m, shift);
                        filter = _console.rx1_filters[(int)m.ModeVfoA].GetName(fltr);
                    }
                    else
                    {
                        if (m.FilterVfoB == fltr) highlightBox(x, y, w, h, rect, vfo, nx, ny, gap, m, shift);
                        if(m.RX2Enabled)
                            filter = _console.rx2_filters[(int)m.ModeVfoB].GetName(fltr);
                        else
                            filter = _console.rx1_filters[(int)m.ModeVfoA].GetName(fltr); // uses RX1
                    }
                    if (string.IsNullOrEmpty(filter)) filter = fltr.ToString();

                    filter_colour = System.Drawing.Color.White;

                    t_xB = xB + (gap / 2f) + (nx * gap);
                    t_yB = yB + (hB / 4f) + (ny * (hB / 2f));
                    plotText(filter, t_xB, t_yB, h, rect.Width, 16f, filter_colour, 255, vfo.FontFamily, vfo.Style, false, true, gap, true);
                    nx++;
                    if (nx > 5)
                    {
                        nx = 0;
                        ny += 1;
                    }
                    // to add the VAR1/2 to rx2
                    if(m.RX2Enabled && shift >= 0.5f && i == (int)Filter.F7)
                    {
                        start = Filter.VAR1;
                        end = Filter.VAR2;
                        i = (int)Filter.F10; // as i gets incremented and moved to VAR1
                    }
                }

                //plotText($"{mx.ToString("f3")},{my.ToString("f3")}", x, y, h, rect.Width, 12, System.Drawing.Color.White, 255, vfo.FontFamily, vfo.Style);

                if (shift != 0)
                    vfo.ButtonGridIndexVFOb = button_grid_index;
                else
                    vfo.ButtonGridIndexVFOa = button_grid_index;

                return button_state;
            }
            private void renderVfoDisplay(SharpDX.RectangleF rect, clsMeterItem mi, clsMeter m)
            {
                clsVfoDisplay vfo = (clsVfoDisplay)mi;

                float x = (mi.DisplayTopLeft.X / m.XRatio) * rect.Width;
                float y = (mi.DisplayTopLeft.Y / m.YRatio) * rect.Height;
                float w = rect.Width * (mi.Size.Width / m.XRatio);
                float h = rect.Height * (mi.Size.Height / m.YRatio);

                int nFade = 255;

                int nVfoAFade = nFade;
                int nVfoBFade = nFade;
                if (m.RX == 1)
                {
                    if (m.RX2Enabled && !(m.MultiRxEnabled || m.Split)) nVfoBFade = 24; // vfoB is 'disabled' on rx1 if rx2 in use, but not if multirxenabled/splt
                }
                else
                {
                    if (m.RX2Enabled) nVfoAFade = 24; // vfoA is 'disabled' on rx2 if rx2 in use always
                }

                //mouse wheel boxes
                bool draw_box = false;
                bool button_back_box = false;
                double step = 0;
                float shift = 0;
                float xB = 0;
                float yB = 0;
                float wB = 0;
                float hB = 0;
                float mx = 0;
                float my = 0;
                SharpDX.RectangleF rct;
                bool mouse_over_good = false;

                clsVfoDisplay.buttonState button_state_vfoA = clsVfoDisplay.buttonState.NONE;
                clsVfoDisplay.buttonState button_state_vfoB = clsVfoDisplay.buttonState.NONE;

                if (vfo.MouseEntered)
                {
                    mx = (vfo.MouseMovePoint.X - x) / w;
                    my = (vfo.MouseMovePoint.Y - y) / h;
                    float box_size = 0;
                    int boxes = -1;
                    int box = -1;
                    float tx = -1;
                    shift = mx >= 0.5f ? 0.510f : 0; //vfoA to B shift
                    mouse_over_good = true;
                    bool left = vfo.VFOARenderState == clsVfoDisplay.renderState.VFO && (shift == 0) && (m.RX == 1);
                    bool right = vfo.VFOBRenderState == clsVfoDisplay.renderState.VFO && (shift != 0) && ((m.RX == 1 && (!m.RX2Enabled || (m.Split || m.MultiRxEnabled))) || (m.RX == 2));

                    // band
                    if (my >= 0.555 && my <= 0.870 && (((mx >= 0.333 && mx <= 0.480) & left) || ((mx >= 0.333 + shift && mx <= 0.480 + shift) & right)))
                    {
                        xB = 0.333f + shift;
                        yB = 0.555f;
                        wB = 0.480f - 0.333f;
                        hB = 0.870f - 0.555f;

                        button_back_box = true;

                        if (left)
                        {
                            button_state_vfoA = clsVfoDisplay.buttonState.BAND_SCREEN;
                            left = false;
                        }
                        if (right)
                        {
                            button_state_vfoB = clsVfoDisplay.buttonState.BAND_SCREEN;
                            right = false;
                        }
                    }
                    // mode
                    if (my >= 0.530 && my <= 0.860 && (((mx >= 0.008 && mx <= 0.078) & left) || ((mx >= 0.008 + shift && mx <= 0.078 + shift) & right)))
                    {
                        xB = 0.008f + shift;
                        yB = 0.530f;
                        wB = 0.078f - 0.008f;
                        hB = 0.860f - 0.530f;

                        button_back_box = true;

                        if (left)
                        {
                            button_state_vfoA = clsVfoDisplay.buttonState.MODE_SCREEN;
                            left = false;
                        }
                        if (right)
                        {
                            button_state_vfoB = clsVfoDisplay.buttonState.MODE_SCREEN;
                            right = false;
                        }
                    }
                    // filter
                    if (my >= 0.555 && my <= 0.870 && (((mx >= 0.250 && mx <= 0.332) & left) || ((mx >= 0.250 + shift && mx <= 0.332 + shift) & right)))
                    {
                        xB = 0.250f + shift;
                        yB = 0.555f;
                        wB = 0.332f - 0.250f;
                        hB = 0.870f - 0.555f;

                        button_back_box = true;

                        if (left)
                        {
                            button_state_vfoA = clsVfoDisplay.buttonState.FILTER_SCREEN;
                            left = false;
                        }
                        if (right)
                        {
                            button_state_vfoB = clsVfoDisplay.buttonState.FILTER_SCREEN;
                            right = false;
                        }
                    }

                    // large numbers
                    if (my >= 0.100 && my <= 0.500)
                    {
                        //mhz
                        if (((mx >= 0.206 && mx <= 0.328) && left) || ((mx >= 0.206 + shift && mx <= 0.328 + shift) && right))
                        {
                            yB = 0.100f;
                            hB = 0.500f - 0.100f;
                            boxes = 5;
                            box_size = (0.328f - 0.206f) / (float)boxes;
                            tx = mx - shift - 0.206f;
                            box = (int)(tx / box_size);
                            xB = 0.206f + (box_size * box) + shift;
                            wB = box_size;
                            step = 1000000 * (int)Math.Pow(10, ((boxes - 1) - box));

                            string t = (shift >= 0.5f ? m.VfoB : m.VfoA).ToString("F6", CultureInfo.InvariantCulture);
                            string[] parts = t.Split('.');
                            draw_box = ((boxes - 1) - box) <= parts[0].Length - 1;
                        }
                        //khz
                        if (((mx >= 0.343 && mx <= 0.414) && left) || ((mx >= 0.343 + shift && mx <= 0.414 + shift) && right))
                        {
                            yB = 0.100f;
                            hB = 0.500f - 0.100f;
                            boxes = 3;
                            box_size = (0.414f - 0.343f) / (float)boxes;
                            tx = mx - shift - 0.343f;
                            box = (int)(tx / box_size);
                            xB = 0.343f + (box_size * box) + shift;
                            wB = box_size;
                            step = 1000 * (int)Math.Pow(10, ((boxes - 1) - box));
                            draw_box = true;
                        }
                    }
                    //small numbers
                    if (my >= 0.170 && my <= 0.500)
                    {
                        //hz
                        if (((mx >= 0.421 && mx <= 0.480) && left) || ((mx >= 0.421 + shift && mx <= 0.480 + shift) && right))
                        {
                            yB = 0.170f;
                            hB = 0.500f - 0.170f;

                            boxes = 3;
                            box_size = (0.480f - 0.421f) / (float)boxes;
                            tx = mx - shift - 0.421f;
                            box = (int)(tx / box_size);
                            xB = 0.421f + (box_size * box) + shift;
                            wB = box_size;
                            step = (int)Math.Pow(10, ((boxes - 1) - box));
                            draw_box = true;
                        }
                    }

                    vfo.MouseOverVfoB = shift != 0;

                    //plotText($"{mx.ToString("f3")},{my.ToString("f3")} -- boxes:{boxes} box:{box}", x, y, h, rect.Width, 12, System.Drawing.Color.White, 255, vfo.FontFamily, vfo.Style);
                }                

                if (vfo.VFOARenderState == clsVfoDisplay.renderState.BAND)
                {
                    button_state_vfoA = drawBand(x, y, w, h, rect, vfo, m, 0);
                }
                if (vfo.VFOBRenderState == clsVfoDisplay.renderState.BAND)
                {
                    button_state_vfoB = drawBand(x, y, w, h, rect, vfo, m, 0.510f);
                }
                if (vfo.VFOARenderState == clsVfoDisplay.renderState.MODE)
                {
                    button_state_vfoA = drawMode(x, y, w, h, rect, vfo, m, 0);
                }
                if (vfo.VFOBRenderState == clsVfoDisplay.renderState.MODE)
                {
                    button_state_vfoB = drawMode(x, y, w, h, rect, vfo, m, 0.510f);
                }
                if (vfo.VFOARenderState == clsVfoDisplay.renderState.FILTER)
                {
                    button_state_vfoA = drawFilter(x, y, w, h, rect, vfo, m, 0);
                }
                if (vfo.VFOBRenderState == clsVfoDisplay.renderState.FILTER)
                {
                    button_state_vfoB = drawFilter(x, y, w, h, rect, vfo, m, 0.510f);
                }

                if (draw_box && mouse_over_good)
                {
                    vfo.AdjustStep = step;
                    //vfo.VfoAdjustEnabled = true;
                    if (vfo.MouseOverVfoB)
                        button_state_vfoB = clsVfoDisplay.buttonState.VFO;
                    else
                        button_state_vfoA = clsVfoDisplay.buttonState.VFO;
                }
                else
                {
                    //vfo.VfoAdjustEnabled = false;
                    vfo.AdjustStep = 0;
                }

                if (draw_box || button_back_box)
                {
                    SharpDX.RectangleF rctB = new SharpDX.RectangleF(x + (w * xB), y + (h * yB), w * wB, h * hB);
                    _renderTarget.FillRectangle(rctB, getDXBrushForColour(vfo.DigitHighlightColour));
                }

                // set the button state
                if (mouse_over_good)
                {
                    if (vfo.MouseOverVfoB)
                        vfo.VFOBButtonState = button_state_vfoB;
                    else
                        vfo.VFOAButtonState = button_state_vfoA;
                }

                // frequency
                string MHz;
                string kHz;
                string hz;
                if (vfo.VFOARenderState == clsVfoDisplay.renderState.VFO)
                {
                    plotText("VFO A", x + (w * 0.01f), y + (h * 0.03f), h, rect.Width, vfo.FontSize, vfo.TypeColour, nVfoAFade, vfo.FontFamily, vfo.Style);
                }

                if (vfo.VFOBRenderState == clsVfoDisplay.renderState.VFO)
                {
                    if (m.RX == 1 && m.RX2Enabled && (m.MultiRxEnabled || m.Split) && m.VfoSub >= 0) //[2.10.3.6]MW0LGE added m.vfosub >= 0
                    {
                        // vfoa sub
                        plotText("VFO Sub", x + (w * 0.52f), y + (h * 0.03f), h, rect.Width, vfo.FontSize, vfo.TypeColour, nVfoBFade, vfo.FontFamily, vfo.Style);
                    }
                    else
                        plotText("VFO B", x + (w * 0.52f), y + (h * 0.03f), h, rect.Width, vfo.FontSize, vfo.TypeColour, nVfoBFade, vfo.FontFamily, vfo.Style);
                }

                if (vfo.VFOARenderState == clsVfoDisplay.renderState.VFO) 
                { 
                    getParts(m.VfoA, out MHz, out kHz, out hz);
                    string sVfoA = MHz + "." + kHz;// + "." + hz;
                    plotText(sVfoA, x + (w * 0.415f), y + (h * 0.02f), h, rect.Width, vfo.FontSize * 1.5f, vfo.FrequencyColour, nVfoAFade, vfo.FontFamily, vfo.Style, true);
                    plotText(hz, x + (w * 0.48f), y + (h * 0.11f), h, rect.Width, vfo.FontSize * 1.2f, vfo.FrequencyColour, nVfoAFade, vfo.FontFamily, vfo.Style, true);
                }

                double tmpVfoB;
                Band tmpVfoBBand;
                DSPMode tmpVfoBMode;
                string tmpVfoBFilterName;

                if (m.RX == 1 && m.RX2Enabled && (m.MultiRxEnabled || m.Split))
                {
                    tmpVfoB = m.VfoSub;
                    tmpVfoBBand = m.BandVfoASub;
                    tmpVfoBMode = m.ModeVfoA;
                    tmpVfoBFilterName = m.FilterVfoAName;
                }
                else
                {
                    tmpVfoB = m.VfoB;
                    tmpVfoBBand = m.BandVfoB;
                    tmpVfoBMode = m.ModeVfoB;
                    tmpVfoBFilterName = m.FilterVfoBName;
                }

                if (vfo.VFOBRenderState == clsVfoDisplay.renderState.VFO)
                {
                    getParts(tmpVfoB, out MHz, out kHz, out hz);
                    string sVfoB = MHz + "." + kHz;// + "." + hz;
                    plotText(sVfoB, x + (w * 0.925f), y + (h * 0.02f), h, rect.Width, vfo.FontSize * 1.5f, vfo.FrequencyColour, nVfoBFade, vfo.FontFamily, vfo.Style, true);
                    plotText(hz, x + (w * 0.99f), y + (h * 0.11f), h, rect.Width, vfo.FontSize * 1.2f, vfo.FrequencyColour, nVfoBFade, vfo.FontFamily, vfo.Style, true);
                }

                if (vfo.VFOARenderState == clsVfoDisplay.renderState.VFO)
                {
                    // mode VFOA
                    plotText(m.ModeVfoA.ToString(), x + (w * 0.01f), y + (h * 0.52f), h, rect.Width, vfo.FontSize * 1f, vfo.ModeColour, nVfoAFade, vfo.FontFamily, vfo.Style);
                }

                if (vfo.VFOBRenderState == clsVfoDisplay.renderState.VFO)
                {
                    // mode VFOB
                    plotText(tmpVfoBMode.ToString(), x + (w * 0.52f), y + (h * 0.52f), h, rect.Width, vfo.FontSize * 1f, vfo.ModeColour, nVfoBFade, vfo.FontFamily, vfo.Style);
                }

                string sBand;
                if (vfo.VFOARenderState == clsVfoDisplay.renderState.VFO)
                {
                    //band VFOA
                    //System.Drawing.Color bandColor = BandStackManager.BandToColour(m.BandVfoA);
                    sBand = BandStackManager.BandToString(m.BandVfoA);
                    if (sBand.EndsWith("M")) sBand = sBand.ToLower();
                    plotText(sBand + " band", x + (w * 0.48f), y + (h * 0.54f), h, rect.Width, vfo.FontSize * 1f, vfo.BandColour, nVfoAFade, vfo.FontFamily, vfo.Style, true);
                }

                if (vfo.VFOBRenderState == clsVfoDisplay.renderState.VFO)
                {
                    //band VFOB
                    sBand = BandStackManager.BandToString(tmpVfoBBand);
                    if (sBand.EndsWith("M")) sBand = sBand.ToLower();
                    plotText(sBand + " band", x + (w * 0.99f), y + (h * 0.54f), h, rect.Width, vfo.FontSize * 1f, vfo.BandColour, nVfoBFade, vfo.FontFamily, vfo.Style, true);
                }

                if (vfo.VFOARenderState == clsVfoDisplay.renderState.VFO)
                {
                    //split only on VFOA
                    SharpDX.RectangleF rectSplit = new SharpDX.RectangleF(x + (w * 0.1f), y + (h * 0.03f), w * 0.1f, h * 0.4f);
                    _renderTarget.FillRectangle(rectSplit, getDXBrushForColour(vfo.SplitBackColour, nVfoAFade));
                    System.Drawing.Color splitColor = m.Split ? vfo.SplitColour : System.Drawing.Color.Black;
                    plotText(m.QuickSplitEnabled ? "QSPLT" : "SPLIT", rectSplit.X + (w * (m.QuickSplitEnabled ? 0.01f : 0.015f)), rectSplit.Y, h, rect.Width, vfo.FontSize * 1f, splitColor, nVfoAFade, vfo.FontFamily, vfo.Style);
                }

                //-- tx/rx state
                bool bCanVfoATx = m.RX == 1 && !m.TXVFOb && !m.Split;
                bool bCanVfoBTx = ((m.TXVFOb && m.RX == 1 && !m.RX2Enabled) || (m.TXVFOb && m.RX == 2 && m.RX2Enabled) || (m.Split && m.RX == 1));

                bool bRxVFOA = !m.MOX && m.RX == 1;
                bool bTxVFOA = m.MOX && bCanVfoATx;

                bool bRxVFOB = !m.MOX && (m.MultiRxEnabled || (m.RX2Enabled && m.RX == 2));
                bool bTxVFOB = m.MOX && bCanVfoBTx;

               
                // tx/rx box colours
                System.Drawing.Color cRx = vfo.RxColour;
                System.Drawing.Color cDimRx = System.Drawing.Color.FromArgb((int)(cRx.R * 0.6f), (int)(cRx.G * 0.6f), (int)(cRx.B * 0.6f));
                System.Drawing.Color cDimerRx = System.Drawing.Color.FromArgb((int)(cRx.R * 0.3f), (int)(cRx.G * 0.3f), (int)(cRx.B * 0.3f));
                System.Drawing.Color cTx = vfo.TxColour;
                System.Drawing.Color cDimTx = System.Drawing.Color.FromArgb((int)(cTx.R * 0.6f), (int)(cTx.G * 0.6f), (int)(cTx.B * 0.6f));
                System.Drawing.Color cDimerTx = System.Drawing.Color.FromArgb((int)(cTx.R * 0.3f), (int)(cTx.G * 0.3f), (int)(cTx.B * 0.3f));                
                System.Drawing.Color boxColour;
                System.Drawing.Color txtColour;

                if (vfo.VFOARenderState == clsVfoDisplay.renderState.VFO)
                {
                    //rx box VFOA
                    rct = new SharpDX.RectangleF(x + (w * 0.1f), y + (h * 0.52f), w * 0.048f, h * 0.4f);
                    boxColour = bRxVFOA ? cDimRx : cDimerRx;
                    _renderTarget.FillRectangle(rct, getDXBrushForColour(boxColour, nVfoAFade));
                    txtColour = bRxVFOA ? cRx : System.Drawing.Color.Black;
                    plotText("RX", rct.X + (w * 0.005f), rct.Y, h, rect.Width, vfo.FontSize * 1f, txtColour, nVfoAFade, vfo.FontFamily, vfo.Style);

                    //tx box VFOA
                    rct = new SharpDX.RectangleF(x + (w * 0.152f), y + (h * 0.52f), w * 0.048f, h * 0.4f);
                    boxColour = bCanVfoATx ? cDimTx : cDimerTx;
                    _renderTarget.FillRectangle(rct, getDXBrushForColour(boxColour, nVfoAFade));
                    txtColour = bTxVFOA ? cTx : System.Drawing.Color.Black;
                    plotText("TX", rct.X + (w * 0.005f), rct.Y, h, rect.Width, vfo.FontSize * 1f, txtColour, nVfoAFade, vfo.FontFamily, vfo.Style);
                }

                if (vfo.VFOBRenderState == clsVfoDisplay.renderState.VFO)
                {
                    //rx box VFOB
                    rct = new SharpDX.RectangleF(x + (w * 0.1f) + (w * 0.52f), y + (h * 0.52f), w * 0.048f, h * 0.4f);
                    boxColour = bRxVFOB ? cDimRx : cDimerRx;
                    _renderTarget.FillRectangle(rct, getDXBrushForColour(boxColour, nVfoBFade));
                    txtColour = bRxVFOB ? cRx : System.Drawing.Color.Black;
                    plotText("RX", rct.X + (w * 0.005f), rct.Y, h, rect.Width, vfo.FontSize * 1f, txtColour, nVfoBFade, vfo.FontFamily, vfo.Style);

                    //tx box VFOB
                    rct = new SharpDX.RectangleF(x + (w * 0.152f) + (w * 0.52f), y + (h * 0.52f), w * 0.048f, h * 0.4f);
                    boxColour = bCanVfoBTx ? cDimTx : cDimerTx;
                    _renderTarget.FillRectangle(rct, getDXBrushForColour(boxColour, nVfoBFade));
                    txtColour = bTxVFOB ? cTx : System.Drawing.Color.Black;
                    plotText("TX", rct.X + (w * 0.005f), rct.Y, h, rect.Width, vfo.FontSize * 1f, txtColour, nVfoBFade, vfo.FontFamily, vfo.Style);
                }

                if (vfo.VFOARenderState == clsVfoDisplay.renderState.VFO)
                {
                    //filter VFOA
                    rct = new SharpDX.RectangleF(x + (w * 0.25f), y + (h * 0.54f), w * 0.048f, h * 0.4f);
                    plotText(m.FilterVfoAName, rct.X + (w * 0.005f), rct.Y, h, rect.Width, vfo.FontSize * 1f, vfo.FilterColour, nVfoAFade, vfo.FontFamily, vfo.Style);
                }

                if (vfo.VFOBRenderState == clsVfoDisplay.renderState.VFO)
                {
                    //filter VFOB
                    rct = new SharpDX.RectangleF(x + (w * 0.25f) + (w * 0.52f), y + (h * 0.54f), w * 0.048f, h * 0.4f);
                    plotText(tmpVfoBFilterName, rct.X + (w * 0.005f), rct.Y, h, rect.Width, vfo.FontSize * 1f, vfo.FilterColour, nVfoBFade, vfo.FontFamily, vfo.Style);
                }

                //if (vfo.MouseEntered)
                //{
                //    plotText($"{mx.ToString("f3")},{my.ToString("f3")}", x, y, h, rect.Width, 12, System.Drawing.Color.White, 255, vfo.FontFamily, vfo.Style);
                //}

                //plotText($"a render state = {vfo.VFOARenderState}", x, y, h, rect.Width, 12, System.Drawing.Color.White, 255, vfo.FontFamily, vfo.Style);
                //plotText($"b render state = {vfo.VFOBRenderState}", x, y + 20, h, rect.Width, 12, System.Drawing.Color.White, 255, vfo.FontFamily, vfo.Style);

                //plotText($"a button state = {vfo.VFOAButtonState}", x, y + 40, h, rect.Width, 12, System.Drawing.Color.White, 255, vfo.FontFamily, vfo.Style);
                //plotText($"b button state = {vfo.VFOBButtonState}", x, y + 60, h, rect.Width, 12, System.Drawing.Color.White, 255, vfo.FontFamily, vfo.Style);
            }
            private void renderClock(SharpDX.RectangleF rect, clsMeterItem mi, clsMeter m)
            {
                clsClock clk = (clsClock)mi;

                float x = (mi.DisplayTopLeft.X / m.XRatio) * rect.Width;
                float y = (mi.DisplayTopLeft.Y / m.YRatio) * rect.Height;
                float w = rect.Width * (mi.Size.Width / m.XRatio);
                float h = rect.Height * (mi.Size.Height / m.YRatio);

                int nFade = 255;

                DateTime now = DateTime.Now;
                DateTime UTCnow = DateTime.UtcNow;

                string sUtc;
                string sUtcAmPm = "";
                float fPadUtc = 0;
                if (clk.Show24HourCLock)
                    sUtc = UTCnow.ToString("HH:mm:ss");
                else
                {
                    sUtc = UTCnow.ToString("h:mm:ss");
                    sUtcAmPm = UTCnow.Hour >= 12 ? "pm" : "am";
                    if (sUtc.Length == 7) fPadUtc = w * 0.03f;
                }

                string sLoc;
                string sLocAmPm = "";
                float fPadLoc = 0;
                if (clk.Show24HourCLock)
                    sLoc = now.ToString("HH:mm:ss");
                else
                {
                    sLoc = now.ToString("h:mm:ss");
                    sLocAmPm = now.Hour >= 12 ? "pm" : "am";
                    if (sLoc.Length == 7) fPadLoc = w * 0.03f;
                }

                string sUtcDate = UTCnow.ToString("ddd d MMM yyyy");
                string sLocDate = now.ToString("ddd d MMM yyyy");

                if (clk.ShowType)
                {
                    plotText("local", x + (w * 0.01f), y + (h * 0.03f), h, rect.Width, clk.FontSize, clk.TypeTitleColour, nFade, clk.FontFamily, clk.Style);
                    plotText("utc", x + (w * 0.52f), y + (h * 0.03f), h, rect.Width, clk.FontSize, clk.TypeTitleColour, nFade, clk.FontFamily, clk.Style);
                }

                SharpDX.RectangleF rct;

                //time
                rct = new SharpDX.RectangleF(x + (w * 0.12f), y + (h * 0.02f), w, h);
                plotText(sLoc, rct.X + fPadLoc, rct.Y, h, rect.Width, clk.FontSize * 1.9f, clk.TimeColour, nFade, clk.FontFamily, clk.Style);
                if (!clk.Show24HourCLock)
                    plotText(sLocAmPm, rct.X + (w * 0.228f), rct.Y + (h * 0.285f), h, rect.Width, clk.FontSize * 0.8f, clk.TimeColour, nFade, clk.FontFamily, clk.Style);

                rct = new SharpDX.RectangleF(x + (w * 0.12f) + (w * 0.52f), y + (h * 0.02f), w, h);
                plotText(sUtc, rct.X + fPadUtc, rct.Y, h, rect.Width, clk.FontSize * 1.9f, clk.TimeColour, nFade, clk.FontFamily, clk.Style);
                if (!clk.Show24HourCLock)
                    plotText(sUtcAmPm, rct.X + (w * 0.228f), rct.Y + (h * 0.285f), h, rect.Width, clk.FontSize * 0.8f, clk.TimeColour, nFade, clk.FontFamily, clk.Style);

                //date
                rct = new SharpDX.RectangleF(x + (w * 0.132f), y + (h * 0.6f), w, h);
                plotText(sLocDate, rct.X, rct.Y, h, rect.Width, clk.FontSize * 0.9f, clk.DateColour, nFade, clk.FontFamily, clk.Style);
                rct = new SharpDX.RectangleF(x + (w * 0.132f) + (w * 0.52f), y + (h * 0.6f), w, h);
                plotText(sUtcDate, rct.X, rct.Y, h, rect.Width, clk.FontSize * 0.9f, clk.DateColour, nFade, clk.FontFamily, clk.Style);
            }
            private void renderSignalTextDisplay(SharpDX.RectangleF rect, clsMeterItem mi, clsMeter m)
            {
                clsSignalText st = (clsSignalText)mi;

                float x = (mi.DisplayTopLeft.X / m.XRatio) * rect.Width;
                float y = (mi.DisplayTopLeft.Y / m.YRatio) * rect.Height;
                float w = rect.Width * (mi.Size.Width / m.XRatio);
                float h = rect.Height * (mi.Size.Height / m.YRatio);

                int nFade = 255;

                float fontSizeEmScaled;
                SizeF szTextSize;

                // s-reading
                fontSizeEmScaled = (st.FontSize / 16f) * (w / 52f);
                Common.SMeterFromDBM2(st.Value, MeterManager.IsAboveS9Frequency(_rx), out int S, out int dBmOver);
                string sText = "S " + S.ToString();
                szTextSize = measureString(sText, st.FontFamily, st.FntStyle, fontSizeEmScaled);
                SharpDX.RectangleF txtrect = new SharpDX.RectangleF(x + (w * 0.5f) - (szTextSize.Width * 0.5f), y, szTextSize.Width, szTextSize.Height);
                _renderTarget.DrawText(sText, getDXTextFormatForFont(st.FontFamily, fontSizeEmScaled, st.FntStyle), txtrect, getDXBrushForColour(st.FontColour, nFade));
                if (dBmOver > 0)
                {
                    plotText("+" + dBmOver.ToString(), txtrect.X + txtrect.Width + (w * 0.005f), txtrect.Y + (h * 0.24f), h, rect.Width, st.FontSize * 0.6f, st.FontColour, nFade, st.FontFamily, st.FntStyle);
                }

                if (st.ShowSubMarker)
                {
                    //uv reading
                    sText = Common.UVfromDBM(st.Value).ToString("f2") + "uV";
                    plotText(sText, x + (w * 0.98f), y + (h * 0.22f), h, rect.Width, st.FontSize * 0.55f, st.HistoryColour, nFade, st.FontFamily, st.FntStyle, true);

                    //dbm reading
                    sText = st.Value.ToString("f1") + MeterManager.ReadingUnits(st.ReadingSource);
                    plotText(sText, x + (w * 0.02f), y + (h * 0.22f), h, rect.Width, st.FontSize * 0.55f, st.HistoryColour, nFade, st.FontFamily, st.FntStyle);
                }

                if (st.ShowPeakValue)
                {
                    //peaks
                    fontSizeEmScaled = ((st.FontSize * 0.5f) / 16f) * (w / 52f);
                    Common.SMeterFromDBM2(st.MaxHistory, MeterManager.IsAboveS9Frequency(_rx), out S, out dBmOver);
                    sText = "S " + S.ToString();
                    szTextSize = measureString(sText, st.FontFamily, st.FntStyle, fontSizeEmScaled);
                    txtrect = new SharpDX.RectangleF(x + (w * 0.5f) - (szTextSize.Width * 0.5f), y + (h * 0.62f), szTextSize.Width, szTextSize.Height);
                    _renderTarget.DrawText(sText, getDXTextFormatForFont(st.FontFamily, fontSizeEmScaled, st.FntStyle), txtrect, getDXBrushForColour(st.PeakValueColour, nFade));
                    if (dBmOver > 0)
                    {
                        plotText("+" + dBmOver.ToString(), txtrect.X + txtrect.Width + (w * 0.005f), txtrect.Y + (h * 0.12f), h, rect.Width, st.FontSize * 0.3f, st.PeakValueColour, nFade, st.FontFamily, st.FntStyle);
                    }

                    if (st.ShowSubMarker)
                    {
                        //uv peak
                        sText = Common.UVfromDBM(st.MaxHistory).ToString("f2") + "uV";
                        plotText(sText, x + (w * 0.98f), y + (h * 0.65f), h, rect.Width, st.FontSize * 0.35f, st.PeakValueColour, nFade, st.FontFamily, st.FntStyle, true);

                        //dbm peak
                        sText = st.MaxHistory.ToString("f1") + MeterManager.ReadingUnits(st.ReadingSource);
                        plotText(sText, x + (w * 0.02f), y + (h * 0.65f), h, rect.Width, st.FontSize * 0.35f, st.PeakValueColour, nFade, st.FontFamily, st.FntStyle);
                    }
                }
            }
            //private void renderSpectrum(SharpDX.RectangleF rect, clsMeterItem mi, clsMeter m)
            //{
            //    lock (_spectrumArrayLock)
            //    {
            //        if (_newSpectrumPassband == null || _currentSpectrumPassband == null) return;

            //        clsSpectrum spect = (clsSpectrum)mi;

            //        float x = (mi.DisplayTopLeft.X / m.XRatio) * rect.Width;
            //        float y = (mi.DisplayTopLeft.Y / m.YRatio) * rect.Height;
            //        float w = rect.Width * (mi.Size.Width / m.XRatio);
            //        float h = rect.Height * (mi.Size.Height / m.YRatio);

            //        int nFade = 255;

            //        float top = Display.SpectrumGridMax; //dbm
            //        float bottom = Display.SpectrumGridMin; //-150; //dbm

            //        float yRange = top - bottom;
            //        float dbmToPixel = h / yRange;

            //        if (_spectrumReady)
            //        {
            //            unsafe
            //            {
            //                // copy to current
            //                fixed (void* srcptr = &_newSpectrumPassband[0])
            //                fixed (void* destptr = &_currentSpectrumPassband[0])
            //                    Win32.memcpy(destptr, srcptr, _currentSpectrumPassband.Length * sizeof(float));

            //                //for (int i = 0; i < _newSpectrumPassband.Length; i++)
            //                //{
            //                //    _currentSpectrumPassband[i] = (_newSpectrumPassband[i] * 0.3f) + (_currentSpectrumPassband[i] * 0.7f);
            //                //}
            //            }
            //            _spectrumReady = false;
            //        }

            //        if (_currentSpectrumPassband != null)
            //        {
            //            SharpDX.RectangleF rct = new SharpDX.RectangleF(x, y, w, h);
            //            _renderTarget.PushAxisAlignedClip(rct, AntialiasMode.Aliased);

            //            float xPos = x;
            //            float increment = w / _currentSpectrumPassband.Length;
            //            Vector2 oldPoint = new Vector2();
            //            Vector2 newPoint = new Vector2();

            //            oldPoint.X = x;
            //            oldPoint.Y = y + ((top - (float)_currentSpectrumPassband[0]) * dbmToPixel);

            //            SharpDX.Direct2D1.Brush b = getDXBrushForColour(System.Drawing.Color.White, nFade);

            //            for (int i = 0; i < _currentSpectrumPassband.Length; i++)
            //            {
            //                float yPos = y + ((top - (float)_currentSpectrumPassband[i]) * dbmToPixel);

            //                newPoint.X = xPos;
            //                newPoint.Y = yPos;

            //                _renderTarget.DrawLine(oldPoint, newPoint, b);

            //                oldPoint.X = newPoint.X;
            //                oldPoint.Y = newPoint.Y;

            //                xPos += increment;
            //            }

            //            _renderTarget.PopAxisAlignedClip();
            //        }
            //    }
            //}
            private void renderImage(SharpDX.RectangleF rect, clsMeterItem mi, clsMeter m)
            {
                clsImage img = (clsImage)mi;

                float x = (mi.DisplayTopLeft.X / m.XRatio) * rect.Width;
                float y = (mi.DisplayTopLeft.Y / m.YRatio) * rect.Height;
                float w = rect.Width * (mi.Size.Width / m.XRatio);
                float h = rect.Height * (mi.Size.Height / m.YRatio);

                int nFade = 255;

                string sImage = img.ImageName;

                //string sKey = sImage;// + "-" + MeterManager.CurrentPowerRating.ToString();
                //if (MeterManager.ContainsBitmap(sKey)) sImage = sKey; // with power rating
                string sKey = sImage + (img.DarkMode ? "-dark" : "");
                if (MeterManager.ContainsBitmap(sKey)) sImage = sKey;

                float fDiag = (float)Math.Sqrt((w * w) + (h * h));
                if(fDiag <= 450)
                    sKey = sImage + "-small";
                else if(fDiag >= 1200)
                    sKey = sImage + "-large";

                if (MeterManager.ContainsBitmap(sKey)) sImage = sKey; // with size

                if (MeterManager.ContainsBitmap(sImage))
                {
                    SharpDX.RectangleF imgRect = new SharpDX.RectangleF(x, y, w, h);

                    if (!img.Clipped)
                    {
                        _renderTarget.PushAxisAlignedClip(imgRect, AntialiasMode.Aliased); // prevent anything drawing from outside the rectangle, no need to cut the image
                    }
                    else
                    {
                        float cx = (img.ClipTopLeft.X / m.XRatio) * rect.Width;
                        float cy = (img.ClipTopLeft.Y / m.YRatio) * rect.Height;
                        float cw = rect.Width * (img.ClipSize.Width / m.XRatio);
                        float ch = rect.Height * (img.ClipSize.Height / m.YRatio);

                        SharpDX.RectangleF clipRect = new SharpDX.RectangleF(cx, cy, cw, ch);
                        _renderTarget.PushAxisAlignedClip(clipRect, AntialiasMode.Aliased); // prevent anything drawing from outside the rectangle, no nee to cut the image
                    }

                    if (_images.ContainsKey(sImage))
                    {
                        SharpDX.Direct2D1.Bitmap b = _images[sImage];

                        // maintain aspect ratio, the clip removes anything outside the rect
                        float im_w = b.Size.Width;
                        float im_h = b.Size.Height;

                        if (w > h)
                            imgRect.Height = imgRect.Width * (im_h / im_w);
                        else
                            imgRect.Width = imgRect.Height * (im_w / im_h);

                        _renderTarget.DrawBitmap(b, imgRect, nFade / 255f, BitmapInterpolationMode.Linear);//, sourceRect);
                    }

                    _renderTarget.PopAxisAlignedClip();
                }
            }
            //private Vector _p;
            //private Vector _p2;
            //private Vector _q;
            //private Vector _q2;

            //private bool _bGotPWR = false;
            //private bool _bGotRefPWR = false;

            //private Dictionary<string, SharpDX.Vector2> _fanSWR = new Dictionary<string, Vector2>();

            private void renderNeedle(SharpDX.RectangleF rect, clsMeterItem mi, clsMeter m)
            {
                clsNeedleItem ni = (clsNeedleItem)mi;

                float x = (mi.DisplayTopLeft.X / m.XRatio) * rect.Width;
                float y = (mi.DisplayTopLeft.Y / m.YRatio) * rect.Height;
                float w = rect.Width * (mi.Size.Width / m.XRatio);
                float h = rect.Height * (mi.Size.Height / m.YRatio);

                int nFade = 255;

                SharpDX.RectangleF nirect = new SharpDX.RectangleF(x, y, w, h);
                //_renderTarget.DrawRectangle(nirect, getDXBrushForColour(System.Drawing.Color.Red));

                _renderTarget.PushAxisAlignedClip(nirect, AntialiasMode.Aliased); // prevent anything drawing from outside the rectangle

                // needle offset from centre
                float cX = x + (w / 2);
                float cY = y + (h / 2);
                float startX = cX + (w * ni.NeedleOffset.X);
                float startY = cY + (h * ni.NeedleOffset.Y);

                float rotation = 180f;

                float radiusX = (w / 2) * (ni.LengthFactor * ni.RadiusRatio.X);
                float radiusY = (w / 2) * (ni.LengthFactor * ni.RadiusRatio.Y);

                //todo
                switch (ni.Placement)
                {
                    case clsNeedleItem.NeedlePlacement.Bottom:
                        rotation = 180f;
                        break;
                    case clsNeedleItem.NeedlePlacement.Left:
                        //rotation = 90f;
                        break;
                    case clsNeedleItem.NeedlePlacement.Top:
                        //rotation = 0f;
                        break;
                    case clsNeedleItem.NeedlePlacement.Right:
                        //rotation = 270f;
                        break;
                }

                float eX, eY, dX, dY;
                float endMaxX = 0 , endMaxY = 0;

                if (ni.ShowHistory || ni.Setup || ni.PeakHold)
                {
                    float valueMin;
                    float valueMax;

                    if (ni.Setup) 
                    {
                        KeyValuePair<float, PointF> kvp;
                        kvp = ni.ScaleCalibration.OrderBy(p => p.Key).First();
                        valueMin = kvp.Key;
                        kvp = ni.ScaleCalibration.OrderByDescending(p => p.Key).First();
                        valueMax = kvp.Key;
                    }
                    else
                    {
                        valueMin = ni.MinHistory;
                        valueMax = ni.MaxHistory;
                    }

                    getPerc(ni, valueMin, out float minPercX, out float minPercY, out PointF minHistoryMin, out PointF minHistoryMax);
                    getPerc(ni, valueMax, out float maxPercX, out float maxPercY, out PointF maxHistoryMin, out PointF maxHistoryMax);

                    // map the meter scales to pixels
                    float eXmin = x + (minHistoryMin.X * w) + (minPercX * ((minHistoryMax.X - minHistoryMin.X) * w));
                    float eYmin = y + (minHistoryMin.Y * h) + (minPercY * ((minHistoryMax.Y - minHistoryMin.Y) * h));
                    float eXmax = x + (maxHistoryMin.X * w) + (maxPercX * ((maxHistoryMax.X - maxHistoryMin.X) * w));
                    float eYmax = y + (maxHistoryMin.Y * h) + (maxPercY * ((maxHistoryMax.Y - maxHistoryMin.Y) * h));

                    // calc angle required
                    float dXmin = startX - eXmin;
                    float dYmin = startY - eYmin;
                    float dXmax = startX - eXmax;
                    float dYmax = startY - eYmax;
                    // expand for ellipse
                    dXmin /= ni.RadiusRatio.X;
                    dYmin /= ni.RadiusRatio.Y;
                    dXmax /= ni.RadiusRatio.X;
                    dYmax /= ni.RadiusRatio.Y;
                    //
                    float angMin = (float)Math.Atan2(dYmin, dXmin);
                    float angMax = (float)Math.Atan2(dYmax, dXmax);

                    float endMinX = startX + (float)(Math.Cos(angMin + degToRad(rotation)) * radiusX);
                    float endMinY = startY + (float)(Math.Sin(angMin + degToRad(rotation)) * radiusY);
                    endMaxX = startX + (float)(Math.Cos(angMax + degToRad(rotation)) * radiusX);
                    endMaxY = startY + (float)(Math.Sin(angMax + degToRad(rotation)) * radiusY);

                    if (ni.ShowHistory || ni.Setup)
                    {
                        PathGeometry sharpGeometry = new PathGeometry(_renderTarget.Factory);

                        GeometrySink geo = sharpGeometry.Open();
                        geo.BeginFigure(new SharpDX.Vector2(startX, startY), FigureBegin.Filled);

                        geo.AddLine(new SharpDX.Vector2(endMinX, endMinY));

                        ArcSegment arcSegment = new ArcSegment();
                        arcSegment.Point = new SharpDX.Vector2(endMaxX, endMaxY);
                        arcSegment.SweepDirection = ni.Direction == clsNeedleItem.NeedleDirection.Clockwise ? SweepDirection.Clockwise : SweepDirection.CounterClockwise;
                        arcSegment.ArcSize = Math.Abs(radToDeg(angMax) - radToDeg(angMin)) <= 180f ? ArcSize.Small : ArcSize.Large;
                        arcSegment.Size = new Size2F(radiusX, radiusY);
                        geo.AddArc(arcSegment);

                        geo.EndFigure(FigureEnd.Closed); // adds the closing line
                        geo.Close();

                        _renderTarget.FillGeometry(sharpGeometry, getDXBrushForColour(ni.HistoryColour, nFade < ni.HistoryColour.A ? nFade : ni.HistoryColour.A));

                        Utilities.Dispose(ref geo);
                        geo = null;
                        Utilities.Dispose(ref sharpGeometry);
                        sharpGeometry = null;
                    }
                }

                getPerc(ni, ni.Value, out float percX, out float percY, out PointF min, out PointF max);

                // map the meter scales to pixels
                eX = x + (min.X * w) + (percX * ((max.X - min.X) * w));
                eY = y + (min.Y * h) + (percY * ((max.Y - min.Y) * h));

                // calc angle required
                dX = startX - eX;
                dY = startY - eY;
                // expand
                dX /= ni.RadiusRatio.X;
                dY /= ni.RadiusRatio.Y;
                float ang = (float)Math.Atan2(dY, dX);

                float endX = startX + (float)(Math.Cos(ang + degToRad(rotation)) * radiusX);
                float endY = startY + (float)(Math.Sin(ang + degToRad(rotation)) * radiusY);

                float fScale = 1f;
                if (ni.ScaleStrokeWidth)
                {
                    float fDiag = (float)Math.Sqrt((w*w) + (h*h));
                    fScale = fDiag / 450f;
                }
                float fStrokeWidth = ni.StrokeWidth * fScale;

                if (ni.PeakHold)
                {
                    if (ni.Shadow)
                    {
                        float fDeltaFromCentre = -((w / 2f) - (endMaxX - x));
                        float fR = fDeltaFromCentre / (w / 2f);
                        float fShift = (fStrokeWidth * 1.5f) * fR;
                        float fTotalWidth = fStrokeWidth * 3f;
                        float tmpStartX = startX + fShift;
                        float tmpStartY = startY + (fStrokeWidth * 1.5f);
                        float tmpEndX = endMaxX + fShift;
                        float tmpEndY = endMaxY + (fStrokeWidth * 1.5f);

                        for (int n = 0; n < 8; n++)
                        {
                            float fReduce = (n / 7f) * fTotalWidth;
                            _renderTarget.DrawLine(new SharpDX.Vector2(tmpStartX, tmpStartY), new SharpDX.Vector2(tmpEndX, tmpEndY), getDXBrushForColour(System.Drawing.Color.Black, (int)ni.PeakNeedleShadowFade), fTotalWidth - fReduce);
                        }

                        if (Math.Abs(endX - endMaxX) > fStrokeWidth) 
                            ni.PeakNeedleShadowFade += 1;
                        else
                            ni.PeakNeedleShadowFade -= 1;
                    }
                    _renderTarget.DrawLine(new SharpDX.Vector2(startX, startY), new SharpDX.Vector2(endMaxX, endMaxY), getDXBrushForColour(ni.PeakHoldMarkerColour, nFade), fStrokeWidth);
                }

                //shadow?
                if (ni.Shadow)
                {
                    float fDeltaFromCentre = -((w / 2f) - (endX - x));
                    float fR = fDeltaFromCentre / (w / 2f);
                    float fShift = (fStrokeWidth * 1.5f) * fR;
                    float fTotalWidth = fStrokeWidth * 3f;
                    float tmpStartX = startX + fShift;
                    float tmpStartY = startY + (fStrokeWidth * 1.5f);
                    float tmpEndX = endX + fShift;
                    float tmpEndY = endY + (fStrokeWidth * 1.5f);

                    for (int n = 0; n<8; n++) 
                    {
                        float fReduce = (n / 7f) * fTotalWidth;
                        _renderTarget.DrawLine(new SharpDX.Vector2(tmpStartX, tmpStartY), new SharpDX.Vector2(tmpEndX, tmpEndY), getDXBrushForColour(System.Drawing.Color.Black, 12), fTotalWidth - fReduce);
                    }
                }

                _renderTarget.DrawLine(new SharpDX.Vector2(startX, startY), new SharpDX.Vector2(endX, endY), getDXBrushForColour(ni.Colour, nFade), fStrokeWidth);

                if (ni.Setup)
                {
                    foreach (KeyValuePair<float, PointF> kvp in ni.ScaleCalibration)
                    {
                        PointF p = kvp.Value;
                        _renderTarget.FillEllipse(new Ellipse(new SharpDX.Vector2(x + (p.X * w), y + (p.Y * h)), 2f, 2f), getDXBrushForColour(System.Drawing.Color.Red, nFade));
                    }
                }
                //if (ni.ReadingSource == Reading.PWR)
                //{
                //    _p = new Vector(startX, startY);
                //    _p2 = new Vector(endX, endY);
                //    _bGotPWR = true;
                //}
                //if(ni.ReadingSource == Reading.REVERSE_PWR)
                //{
                //    _q = new Vector(startX, startY);
                //    _q2 = new Vector(endX, endY);
                //    _bGotRefPWR = true;

                //}
                //if(_bGotPWR && _bGotRefPWR)
                //{
                //    bool b = lineSegementsIntersect(_p, _p2, _q, _q2, out Vector intersect);

                //    if (b)
                //    {
                //        float xx = (float)(_p.X + (_q.X - _p.X)/2f);
                //        float yy = (float)(_p.Y + (_q.Y - _p.Y)/2f);

                //        string sKey = MeterManager.getReading(_rx, Reading.REVERSE_PWR).ToString("0.000");
                //        if (!_fanSWR.ContainsKey(sKey))
                //            _fanSWR.Add(sKey, new SharpDX.Vector2((float)intersect.X, (float)intersect.Y));

                //        foreach(KeyValuePair<string, SharpDX.Vector2> pair in _fanSWR)
                //        {
                //            _renderTarget.DrawLine(new SharpDX.Vector2(xx, yy), new SharpDX.Vector2(pair.Value.X, pair.Value.Y), getDXBrushForColour(System.Drawing.Color.Red, nFade), ni.StrokeWidth);
                //        }
                //    }

                //    _bGotPWR = false;
                //    _bGotRefPWR = false;
                //}

                _renderTarget.PopAxisAlignedClip();
            }
            private double degToRad(float deg)
            {
                return (deg * Math.PI) / 180f;
            }
            private double radToDeg(float rad)
            {
                return rad * (180f / Math.PI);
            }
            private void getPerc(clsMeterItem mi, float value, out float percX, out float percY, out PointF min, out PointF max)
            {
                percX = 0;
                percY = 0;
                min = new PointF(0f, 0f);
                max = new PointF(0f, 0f);

                value = (float)Math.Round(value, 2); // NOTE: this will limit the finding of keys in the scale calibration below

                // adjust for >= 30mhz
                if (mi.ReadingSource == Reading.SIGNAL_STRENGTH || mi.ReadingSource == Reading.AVG_SIGNAL_STRENGTH)
                    value += MeterManager.dbmOffsetForAboveS9Frequency(_rx);

                // normalise to 100w
                else if (mi.NormaliseTo100W)
                {
                    //value *= MeterManager.normalisePower();
                    value *= (100 / mi.MaxPower);
                }

                clsMeterItem.clsPercCache pc = mi.GetPerc(value);
                if (pc != null)
                {                    
                    percX = pc._percX;
                    percY = pc._percY;
                    min = pc._min;
                    max = pc._max;
                    return;
                }

                if (mi.ScaleCalibration.Count > 0)
                {
                    // todo, only implemented for clockwise needles atm
                    PointF p_low = PointF.Empty;
                    PointF p_high = PointF.Empty;
                    float value_low = -200f;
                    float value_high = -200f;
                    bool bEqual = false;

                    if (mi.ScaleCalibration.ContainsKey(value))
                    {
                        p_low = mi.ScaleCalibration[value];
                        p_high = mi.ScaleCalibration[value];
                        value_low = value;
                        value_high = value;
                        bEqual = true;
                    }
                    else if (value < mi.ScaleCalibration.OrderBy(p => p.Key).First().Key) // below lowest
                    {
                        p_low = p_high = mi.ScaleCalibration.OrderBy(p => p.Key).First().Value;
                        value_low = value_high = mi.ScaleCalibration.OrderBy(p => p.Key).First().Key;
                        bEqual = true;
                    }
                    else if (value > mi.ScaleCalibration.OrderBy(p => p.Key).Last().Key) // above highest
                    {
                        p_low = p_high = mi.ScaleCalibration.OrderBy(p => p.Key).Last().Value;
                        value_low = value_high = mi.ScaleCalibration.OrderBy(p => p.Key).Last().Key;
                        bEqual = true;
                    }
                    else
                    {
                        //low side
                        foreach (KeyValuePair<float, PointF> kvp in mi.ScaleCalibration.OrderByDescending(p => p.Key))
                        {
                            if (kvp.Key <= value)
                            {
                                p_low = kvp.Value;
                                value_low = kvp.Key;
                                break;
                            }
                        }

                        // high
                        foreach (KeyValuePair<float, PointF> kvp in mi.ScaleCalibration.OrderBy(p => p.Key))
                        {
                            if (kvp.Key >= value)
                            {
                                p_high = kvp.Value;
                                value_high = kvp.Key;
                                break;
                            }
                        }
                    }
                    //get mins/maxs
                    float minX = (float)Math.Round(mi.ScaleCalibration.OrderBy(p => p.Value.X).First().Value.X, 3);
                    float maxX = (float)Math.Round(mi.ScaleCalibration.OrderByDescending(p => p.Value.X).First().Value.X, 3);

                    float minY = (float)Math.Round(mi.ScaleCalibration.OrderBy(p => p.Value.Y).First().Value.Y, 3);
                    float maxY = (float)Math.Round(mi.ScaleCalibration.OrderByDescending(p => p.Value.Y).First().Value.Y, 3);

                    float rangeX = maxX - minX;
                    float rangeY = maxY - minY;
                    percX = 0;
                    percY = 0;
                    if (bEqual)
                    {
                        if (rangeX > 0) percX = (p_low.X - minX) / rangeX;
                        if (rangeY > 0) percY = (p_low.Y - minY) / rangeY;
                    }
                    else
                    {
                        float value_range = value_high - value_low;
                        float ratio = (value - value_low) / value_range;
                        float newRangeX = p_high.X - p_low.X;
                        float newRangeY = p_high.Y - p_low.Y;

                        if (rangeX > 0) percX = ((p_low.X - minX) + (newRangeX * ratio)) / rangeX;
                        if (rangeY > 0) percY = ((p_low.Y - minY) + (newRangeY * ratio)) / rangeY;
                    }

                    percX = (float)Math.Round(percX, 3);
                    percY = (float)Math.Round(percY, 3);
                    min = new PointF(minX, minY);
                    max = new PointF(maxX, maxY);

                    pc = new clsMeterItem.clsPercCache() { _value = value,
                                                            _percX = percX,
                                                            _percY = percY,
                                                            _min = min,
                                                            _max = max };
                    mi.AddPerc(pc);
                }
            }            
            private SharpDX.Direct2D1.Bitmap bitmapFromSystemBitmap(RenderTarget rt, System.Drawing.Bitmap bitmap, string sId)
            {
                SharpDX.Direct2D1.Bitmap dxBitmap;
                Size2 size = new Size2(bitmap.Width, bitmap.Height);
                int stride = bitmap.Width * sizeof(int);
                BitmapProperties bitmapProperties = new BitmapProperties(new SharpDX.Direct2D1.PixelFormat(Format.B8G8R8A8_UNorm, _ALPHA_MODE)); //was R8G8B8A8_UNorm  //MW0LGE_21k9

                if (MeterManager.ContainsStreamData(sId))
                {
                    DataStream tempStream = MeterManager.GetStreamData(sId);
                    tempStream.Position = 0;

                    // bitmaps need to be built per render target, which use their own factory
                    // these can not be shared
                    dxBitmap = new SharpDX.Direct2D1.Bitmap(rt, size, tempStream, stride, bitmapProperties);
                }
                else
                {
                    System.Drawing.Rectangle sourceArea = new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height);

                    // Transform pixels from ARGB to RGBA
                    DataStream tempStream = new DataStream(bitmap.Height * stride, true, true);

                    // Lock System.Drawing.Bitmap
                    System.Drawing.Imaging.BitmapData bitmapData = bitmap.LockBits(sourceArea, System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);

                    // Convert all pixels 
                    IntPtr first_pixel = bitmapData.Scan0;
                    int bitmapData_stride = bitmapData.Stride;

                    //for (int y = 0; y < bitmap.Height; y++)
                    //{
                    //    int offset = bitmapData_stride * y;
                    //    for (int x = 0; x < bitmap.Width; x++)
                    //    {
                    //        byte B = Marshal.ReadByte(first_pixel, offset++);
                    //        byte G = Marshal.ReadByte(first_pixel, offset++);
                    //        byte R = Marshal.ReadByte(first_pixel, offset++);
                    //        byte A = Marshal.ReadByte(first_pixel, offset++);
                    //        //int rgba = R | (G << 8) | (B << 16) | (A << 24); //MW0LGE_21k9
                    //        //tempStream.Write(rgba);
                    //        int bgra = B | (G << 8) | (R << 16) | (A << 24);
                    //        tempStream.Write<int>(bgra);
                    //    }
                    //}

                    // --
                    // Parallel conversion of rows
                    int h = bitmap.Height;
                    int w = bitmap.Width;
                    int[] data = new int[h * w];

                    Parallel.For(0, h, y =>
                    {
                        int offset = bitmapData_stride * y;
                        for (int x = 0; x < w; x++)
                        {
                            byte B = Marshal.ReadByte(first_pixel, offset++);
                            byte G = Marshal.ReadByte(first_pixel, offset++);
                            byte R = Marshal.ReadByte(first_pixel, offset++);
                            byte A = Marshal.ReadByte(first_pixel, offset++);
                            data[(y * w) + x] = B | (G << 8) | (R << 16) | (A << 24);
                        }
                    });

                    tempStream.WriteRange<int>(data);
                    // --

                    bitmap.UnlockBits(bitmapData);

                    tempStream.Position = 0;

                    dxBitmap = new SharpDX.Direct2D1.Bitmap(rt, size, tempStream, stride, bitmapProperties);

                    //Utilities.Dispose(ref tempStream);
                    //tempStream = null;

                    MeterManager.AddStreamData(sId, tempStream);
                }
                return dxBitmap;
            }
            //private class Vector
            //{
            //    public double X;
            //    public double Y;

            //    // Constructors.
            //    public Vector(double x, double y) { X = x; Y = y; }
            //    public Vector() : this(double.NaN, double.NaN) { }

            //    public static Vector operator -(Vector v, Vector w)
            //    {
            //        return new Vector(v.X - w.X, v.Y - w.Y);
            //    }

            //    public static Vector operator +(Vector v, Vector w)
            //    {
            //        return new Vector(v.X + w.X, v.Y + w.Y);
            //    }

            //    public static double operator *(Vector v, Vector w)
            //    {
            //        return v.X * w.X + v.Y * w.Y;
            //    }

            //    public static Vector operator *(Vector v, double mult)
            //    {
            //        return new Vector(v.X * mult, v.Y * mult);
            //    }

            //    public static Vector operator *(double mult, Vector v)
            //    {
            //        return new Vector(v.X * mult, v.Y * mult);
            //    }

            //    public double Cross(Vector v)
            //    {
            //        return X * v.Y - Y * v.X;
            //    }

            //    public override bool Equals(object obj)
            //    {
            //        var v = (Vector)obj;
            //        return (X - v.X).IsZero() && (Y - v.Y).IsZero();
            //    }
            //}
            ////https://www.codeproject.com/Tips/862988/Find-the-Intersection-Point-of-Two-Line-Segments
            //private bool lineSegementsIntersect(Vector p, Vector p2, Vector q, Vector q2, out Vector intersection, bool considerCollinearOverlapAsIntersect = false)
            //{
            //    intersection = new Vector();

            //    Vector r = p2 - p;
            //    Vector s = q2 - q;
            //    double rxs = r.Cross(s);
            //    double qpxr = (q - p).Cross(r);

            //    // If r x s = 0 and (q - p) x r = 0, then the two lines are collinear.
            //    if (rxs.IsZero() && qpxr.IsZero())
            //    {
            //        // 1. If either  0 <= (q - p) * r <= r * r or 0 <= (p - q) * s <= * s
            //        // then the two lines are overlapping,
            //        if (considerCollinearOverlapAsIntersect)
            //            if ((0 <= (q - p) * r && (q - p) * r <= r * r) || (0 <= (p - q) * s && (p - q) * s <= s * s))
            //                return true;

            //        // 2. If neither 0 <= (q - p) * r = r * r nor 0 <= (p - q) * s <= s * s
            //        // then the two lines are collinear but disjoint.
            //        // No need to implement this expression, as it follows from the expression above.
            //        return false;
            //    }

            //    // 3. If r x s = 0 and (q - p) x r != 0, then the two lines are parallel and non-intersecting.
            //    if (rxs.IsZero() && !qpxr.IsZero())
            //        return false;

            //    // t = (q - p) x s / (r x s)
            //    double t = (q - p).Cross(s) / rxs;

            //    // u = (q - p) x r / (r x s)

            //    double u = (q - p).Cross(r) / rxs;

            //    // 4. If r x s != 0 and 0 <= t <= 1 and 0 <= u <= 1
            //    // the two line segments meet at the point p + t r = q + u s.
            //    if (!rxs.IsZero() && (0 <= t && t <= 1) && (0 <= u && u <= 1))
            //    {
            //        // We can calculate the intersection point using either t or u.
            //        intersection = p + t * r;

            //        // An intersection was found.
            //        return true;
            //    }

            //    // 5. Otherwise, the two line segments are not parallel but do not intersect.
            //    return false;
            //}
        }        
        #endregion
    }
    public static class MultiMeterIO
    {
        private const int DELAY = 100; // msec
        [Serializable]
        public enum MMIODirection
        {
            IN = 0,
            OUT = 1,
            BOTH = 2
        }
        [Serializable]
        public enum MMIOFormat
        {
            JSON = 0,
            XML = 1,
            RAW = 2,
            LAST = 3
        }
        [Serializable]
        public enum MMIOType
        {
            UDP_LISTENER = 0,
            TCPIP_LISTENER = 1,
            SERIAL = 2,
            TCPIP_CLIENT = 3,
            REQUESTER = 4
        }
        [Serializable]
        public enum MMIOTerminator
        {
            NONE = 0,
            CR = 1,
            LF = 2,
            CRLF = 3,
            CUSTOM = 4,
            LAST = 5
        }
        [Serializable]
        public class clsMMIO
        {
            private Guid _guid;
            private MMIODirection _direction;
            private string _ip;
            private int _port;
            private string _udp_endpoint_ip;
            private int _udp_endpoint_port;
            private MMIOFormat _format_in;
            private MMIOFormat _format_out;
            private MMIOType _type;
            private bool _listener_active;
            private string _four_char;
            private bool _enabled;
            private bool _connector_running;
            private MMIOTerminator _terminator_in;
            private MMIOTerminator _terminator_out;
            private string _custom_terminator_in;
            private string _custom_terminator_out;
            private string _custom_terminator_parsed_in;
            private string _custom_terminator_parsed_out;
            private IPEndPoint _udp_endpoint;

            //
            private string _com_port;
            private int _baud_rate;
            private int _data_bits;
            private StopBits _stop_bits;
            private Parity _parity;
            //

            private ConcurrentDictionary<string, object> _io_variables;
            private ConcurrentQueue<string> _outbound_queue;
            public clsMMIO()
            {
                init();

                _four_char = FourChar(_ip, _port, _guid);
            }
            private void init()
            {
                _enabled = true;
                _guid = Guid.NewGuid();
                _type = MMIOType.UDP_LISTENER;
                _ip = "127.0.0.1";
                _port = 9000;
                _udp_endpoint_ip = "127.0.0.1";
                _udp_endpoint_port = 10000;
                _listener_active = false;
                _format_in = MMIOFormat.JSON;
                _format_out = MMIOFormat.JSON;
                _direction = MMIODirection.IN;
                _terminator_in = MMIOTerminator.NONE;
                _terminator_out = MMIOTerminator.NONE;
                _custom_terminator_in = "";
                _custom_terminator_out = "";
                _custom_terminator_parsed_in = "";
                _custom_terminator_parsed_out = "";
                _udp_endpoint = null;

                _com_port = "";
                _baud_rate = 9600;
                _data_bits = 8;
                _stop_bits = StopBits.One;
                _parity = Parity.None;

                _io_variables = new ConcurrentDictionary<string, object>();
                _outbound_queue = new ConcurrentQueue<string>();                
            }
            //public clsMMIO(Guid guid, MMIOType type, string ip, int port, bool enabled)
            //{
            //    init();

            //    _enabled = enabled;
            //    if (guid == Guid.Empty)
            //        _guid = Guid.NewGuid();
            //    else
            //        _guid = guid;
            //    _type = type;
            //    _ip = ip;
            //    _port = port;

            //    _four_char = FourChar(_ip, _port, _guid);
            //}
            public clsMMIO(MMIOType type, string ip, int port, bool enabled)
            {
                init();

                _enabled = enabled;
                _type = type;
                _ip = ip;
                _port = port;

                _four_char = FourChar(_ip, _port, _guid);
            }
            public clsMMIO(MMIOType type, string com_port, int baud_rate, int data_bits, StopBits stop_bits, Parity parity, bool enabled)
            {
                init();

                _enabled = enabled;
                _type = type;
                _ip = "";
                _port = 0;

                _com_port = com_port;
                _baud_rate = baud_rate;
                _data_bits = data_bits;
                _stop_bits = stop_bits; ;
                _parity = parity;

                _four_char = FourChar(com_port, baud_rate + data_bits, _guid);
            }
            public Guid Guid
            {
                get { return _guid; }
                set { 
                    _guid = value;
                    _four_char = FourChar(_ip, _port, _guid);
                }
            }
            public MMIODirection Direction
            {
                get { return _direction; }
                set { 
                    _direction = value;
                    refreshUdpEndpoint();
                }
            }
            public string IP
            {
                get { return _ip; }
                set { 
                    _ip = value;
                    _four_char = FourChar(_ip, _port, _guid);
                }
            }
            public int Port
            {
                get { return _port; }
                set { 
                    _port = value;
                    _four_char = FourChar(_ip, _port, _guid);
                }
            }
            public string UdpEndpointIP
            {
                get { return _udp_endpoint_ip; }
                set
                {
                    _udp_endpoint_ip = value;
                    refreshUdpEndpoint();
                }
            }
            public int UdpEndpointPort
            {
                get { return _udp_endpoint_port; }
                set
                {
                    _udp_endpoint_port = value;
                    refreshUdpEndpoint();
                }
            }
            public string ComPort
            {
                get { return _com_port; }
                set { _com_port = value; }
            }
            public int BaudRate
            {
                get { return _baud_rate; }
                set { _baud_rate = value; }
            }
            public int DataBits
            {
                get { return _data_bits; }
                set { _data_bits = value; }
            }
            public StopBits StopBits
            {
                get { return _stop_bits; }
                set { _stop_bits = value; }
            }
            public Parity Parity
            {
                get { return _parity; }
                set { _parity = value; }
            }
            private void refreshUdpEndpoint()
            {
                if (_direction == MMIODirection.OUT || _direction == MMIODirection.BOTH)
                    _udp_endpoint = new IPEndPoint(IPAddress.Parse(_udp_endpoint_ip), _udp_endpoint_port);
                else
                {
                    _udp_endpoint = null;
                }
            }
            public IPEndPoint UDPEndPoint
            {
                get { return _udp_endpoint; }
            }
            public MMIOFormat FormatIn
            {
                get { return _format_in; }
                set { _format_in = value; }
            }
            public MMIOFormat FormatOut
            {
                get { return _format_out; }
                set { _format_out = value; }
            }
            public MMIOTerminator TerminatorIn
            {
                get { return _terminator_in; }
                set { _terminator_in = value; }
            }
            public MMIOTerminator TerminatorOut
            {
                get { return _terminator_out; }
                set { _terminator_out = value; }
            }
            public MMIOType Type
            {
                get { return _type; }
                set { _type = value; }
            }
            public bool Active
            {
                get { return _listener_active; }
                set { _listener_active = value; }
            }
            public string FourChar
            {
                get { return _four_char; }
            }
            public bool Enabled
            {
                get { return _enabled; }
                set { _enabled = value; }
            }
            public string CustomTerminatorParsedIn
            {
                get { return string.IsNullOrEmpty(_custom_terminator_parsed_in) ? "\0" : _custom_terminator_parsed_in; }
            }
            public string CustomTerminatorIn
            {
                get { return _custom_terminator_in; }
                set
                {
                    _custom_terminator_in = value;

                    _custom_terminator_parsed_in = _custom_terminator_in.Replace(@"\n", "\n"); // use @ so that it is a literal verbatim string
                    _custom_terminator_parsed_in = _custom_terminator_parsed_in.Replace(@"\r", "\r");
                    _custom_terminator_parsed_in = _custom_terminator_parsed_in.Replace(@"\0", "\0");
                }
            }
            public void EnqueueOutbound(string data)
            {
                if (_direction == MMIODirection.IN) return;
                _outbound_queue.Enqueue(data);
            }
            public string DequeueOutbound()
            {
                if (_direction == MMIODirection.IN) return "";

                bool ok = _outbound_queue.TryDequeue(out string data);
                if (ok) return data;
                return "";
            }
            public bool OutboundQueueEmpty
            {
                get { return _outbound_queue.Count == 0; }
            }
            public string CustomTerminatorParsedOut
            {
                get { return string.IsNullOrEmpty(_custom_terminator_parsed_out) ? "\0" : _custom_terminator_parsed_out; }
            }
            public string CustomTerminatorOut
            {
                get { return _custom_terminator_out; }
                set
                {
                    _custom_terminator_out = value;

                    _custom_terminator_parsed_out = _custom_terminator_out.Replace(@"\n", "\n"); // use @ so that it is a literal verbatim string
                    _custom_terminator_parsed_out = _custom_terminator_parsed_out.Replace(@"\r", "\r");
                    _custom_terminator_parsed_out = _custom_terminator_parsed_out.Replace(@"\0", "\0");
                }
            }
            public bool StartConnection()
            {
                bool ok = false;
                switch(_type)
                {
                    case MMIOType.UDP_LISTENER:
                        refreshUdpEndpoint();
                        ok = MultiMeterIO.StartListeningUDP(this);
                        break;
                    case MMIOType.TCPIP_LISTENER:
                        ok = MultiMeterIO.StartListeningTCPIP(this);
                        break;
                    case MMIOType.TCPIP_CLIENT:
                        ok = MultiMeterIO.StartTcpClient(this);
                        break;
                    case MMIOType.SERIAL:
                        ok = MultiMeterIO.StartSerialPort(this);
                        break;
                }
                return ok;
            }
            public void StopConnection()
            {
                MultiMeterIO.StopConnection(_guid);
            }
            public bool SetVariable(string key, object value)
            {
                bool ok;
                if (_io_variables.ContainsKey(key))
                {
                    _io_variables[key] = value;
                    ok = true;
                }
                else
                {
                    ok = _io_variables.TryAdd(key, value);
                }
                return ok;
            }
            public object GetVariable(string key)
            {
                if (_io_variables.ContainsKey(key))
                {
                    object val = _io_variables[key];
                    string sTmp;
                    if (val is int)
                        sTmp = val.ToString();
                    else if (val is float)
                        sTmp = ((float)val).ToString("0.0#####");
                    else if (val is double)
                        sTmp = ((double)val).ToString("0.0#####");
                    else if (val is bool)
                        sTmp = ((bool)val).ToString().ToLower();
                    else
                        sTmp = val.ToString();

                    Type valueType = DetermineType(sTmp);
                    object covertedVal = ConvertToType(val.ToString(), valueType);
                    return covertedVal;
                }
                return false;
            }
            public Type DetermineType(string value)
            {
                // Create culture info for European style numbers
                CultureInfo europeanCulture = new CultureInfo("fr-FR");
                NumberStyles numberStyle = NumberStyles.Float | NumberStyles.AllowThousands;

                // Try parsing as an integer
                if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _) ||
                    int.TryParse(value, NumberStyles.Integer, europeanCulture, out _))
                {
                    return typeof(int);
                }
                // Try parsing as a float
                else if (float.TryParse(value, numberStyle, CultureInfo.InvariantCulture, out _) ||
                         float.TryParse(value, numberStyle, europeanCulture, out _))
                {
                    return typeof(float);
                }
                // Try parsing as a double
                else if (double.TryParse(value, numberStyle, CultureInfo.InvariantCulture, out _) ||
                         double.TryParse(value, numberStyle, europeanCulture, out _))
                {
                    return typeof(double);
                }
                // Try parsing as a boolean
                else if (bool.TryParse(value, out _))
                {
                    return typeof(bool);
                }
                // Default to string
                else
                {
                    return typeof(string);
                }
            }
            [DebuggerHidden]
            public object ConvertToType(string value, Type type)
            {
                CultureInfo europeanCulture = new CultureInfo("fr-FR");
                CultureInfo invariantCulture = CultureInfo.InvariantCulture;

                try
                {
                    TypeConverter converter = TypeDescriptor.GetConverter(type);
                    try
                    {
                        return converter.ConvertFromString(null, invariantCulture, value);
                    }
                    catch
                    {
                        try
                        {
                            return converter.ConvertFromString(null, CultureInfo.CurrentCulture, value);
                        }
                        catch
                        {
                            return converter.ConvertFromString(null, europeanCulture, value);
                        }
                    }
                }
                catch (Exception)
                {
                    if (type.IsEnum)
                    {
                        return Enum.Parse(type, value);
                    }

                    throw new ArgumentException("Cannot convert the string to the specified type.", nameof(value));
                }
            }
            public ConcurrentDictionary<string, object> Variables()
            {
                return _io_variables;
            }
            public string VariableValueType(object obj)
            {
                string tmp;
                if (obj is int)
                    tmp = ((int)obj).ToString();
                else if (obj is float)
                    tmp = ((float)obj).ToString("0.0#####");
                else if (obj is double)
                    tmp = ((double)obj).ToString("0.0#####");
                else if (obj is bool)
                    tmp = ((bool)obj).ToString().ToLower();
                else
                    tmp = obj.ToString();
                return tmp;
            }
            //private static T deepCopy<T>(T obj)
            //{
            //    if (ReferenceEquals(obj, null) || !typeof(T).IsSerializable)
            //    {
            //        return default(T);
            //    }

            //    IFormatter formatter = new BinaryFormatter();
            //    using (Stream stream = new MemoryStream())
            //    {
            //        formatter.Serialize(stream, obj);
            //        stream.Seek(0, SeekOrigin.Begin);
            //        return (T)formatter.Deserialize(stream);
            //    }
            //}
            public void RemoveVariable(string key)
            {
                _io_variables.TryRemove(key, out _);
            }
        }
        private class TcpListener
        {
            private Guid _guid;
            private MMIOType _type;
            private string _ip;
            private int _port;
            private System.Net.Sockets.TcpListener _tcpListener;
            private TcpClient _tcpClient;
            private Thread _listenerThread;
            private volatile bool _isRunning;

            public event Action<Guid, string> ReceivedDataString;
            public event Action<Guid> TransmittedData;
            public event Action<Guid, MMIOType, bool> ConnectorRunning;

            public TcpListener(Guid guid, MMIOType type, string ip, int port)
            {
                _guid = guid;
                _type = type;
                _ip = ip;
                _port = port;
                _tcpListener = new System.Net.Sockets.TcpListener(IPAddress.Parse(ip), port);
            }

            public void Start()
            {
                _isRunning = true;
                _listenerThread = new Thread(new ThreadStart(listen));
                _listenerThread.IsBackground = true;
                _listenerThread.Start();                
                ConnectorRunning?.Invoke(_guid, _type, true);
            }

            public void Stop()
            {
                _isRunning = false;
                if (_tcpClient != null)
                {
                    _tcpClient.Close();
                }
                _tcpListener.Stop();
                _listenerThread.Join();
                ConnectorRunning?.Invoke(_guid, _type, false);
            }

            private void listen()
            {                
                try
                {
                    bool started = true;
                    DateTime lastTimeActive = DateTime.Now;
                    _tcpListener.Start();
                    while (_isRunning)
                    {
                        if (!_tcpListener.Pending())
                        {
                            Thread.Sleep(50);
                            continue;
                        }

                        try
                        {
                            _tcpClient = _tcpListener.AcceptTcpClient();
                            _tcpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.NoDelay, true);
                            // stop listening for new
                            if (started)
                            {
                                _tcpListener.Stop();
                                started = false;
                            }

                            NetworkStream stream = _tcpClient.GetStream();
                            string bufferConcat = "";
                            while (_tcpClient.Connected && _isRunning)
                            {
                                bool sleep = true;
                                bool inbound = _mmio_data[_guid].Direction == MMIODirection.IN || _mmio_data[_guid].Direction == MMIODirection.BOTH;
                                bool outbound = _mmio_data[_guid].Direction == MMIODirection.OUT || _mmio_data[_guid].Direction == MMIODirection.BOTH;

                                if (inbound)
                                {
                                    try
                                    {
                                        if (stream.DataAvailable)
                                        {
                                            byte[] buffer = new byte[1024];
                                            int bytesRead = stream.Read(buffer, 0, buffer.Length);
                                            if (bytesRead > 0)
                                            {
                                                lastTimeActive = DateTime.Now;
                                                string receivedData = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                                                sleep = false;
                                                string term = "";
                                                switch (_mmio_data[_guid].TerminatorIn)
                                                {
                                                    case MMIOTerminator.NONE:
                                                        term = "";
                                                        break;
                                                    case MMIOTerminator.CR:
                                                        term = "\r";
                                                        break;
                                                    case MMIOTerminator.LF:
                                                        term = "\n";
                                                        break;
                                                    case MMIOTerminator.CRLF:
                                                        term = "\r\n";
                                                        break;
                                                    case MMIOTerminator.CUSTOM:
                                                        term = _mmio_data[_guid].CustomTerminatorParsedIn;
                                                        break;
                                                }
                                                bufferConcat += receivedData;                                                
                                                if (string.IsNullOrEmpty(term))
                                                {
                                                    ReceivedDataString?.Invoke(_guid, bufferConcat);
                                                    bufferConcat = "";
                                                }
                                                else
                                                {
                                                    //int pos = receivedData.IndexOf(term);
                                                    int pos = bufferConcat.IndexOf(term);
                                                    while (pos > -1)
                                                    {
                                                        ReceivedDataString?.Invoke(_guid, bufferConcat.Substring(0, pos));
                                                        bufferConcat = bufferConcat.Substring(pos + term.Length);
                                                        pos = bufferConcat.IndexOf(term);
                                                    }
                                                }

                                                if (bufferConcat.Length >= 4096 * 8) bufferConcat = bufferConcat.Substring(4096 * 4); // some limiter incase it is not being processed
                                            }
                                        }
                                    }
                                    catch (Exception ex) when (ex is SocketException || ex is IOException || ex is ObjectDisposedException)
                                    {
                                        // Handle disconnection or stream issues
                                        break;
                                    }
                                }
                                if (outbound)
                                {
                                    if (!_mmio_data[_guid].OutboundQueueEmpty)
                                    {
                                        string outData = "";
                                        int n = 0;
                                        string termSend = "";
                                        switch (_mmio_data[_guid].TerminatorOut)
                                        {
                                            case MMIOTerminator.NONE:
                                                termSend = "";
                                                break;
                                            case MMIOTerminator.CR:
                                                termSend = "\r";
                                                break;
                                            case MMIOTerminator.LF:
                                                termSend = "\n";
                                                break;
                                            case MMIOTerminator.CRLF:
                                                termSend = "\r\n";
                                                break;
                                            case MMIOTerminator.CUSTOM:
                                                termSend = _mmio_data[_guid].CustomTerminatorParsedOut;
                                                break;
                                        }
                                        while (!_mmio_data[_guid].OutboundQueueEmpty)
                                        {
                                            outData += _mmio_data[_guid].DequeueOutbound() + termSend;
                                            n++;
                                            if (n == 20) break; // some limit
                                        }
                                        if (outData.Length > 0)
                                        {
                                            lastTimeActive = DateTime.Now;
                                            sleep = false;

                                            Byte[] sendBytes = Encoding.ASCII.GetBytes(outData);
                                            try
                                            {
                                                stream.Write(sendBytes, 0, sendBytes.Length);
                                                TransmittedData?.Invoke(_guid);
                                            }
                                            catch (Exception ex) when (ex is SocketException || ex is IOException || ex is ObjectDisposedException)
                                            {
                                                // Handle disconnection or stream issues
                                                break;
                                            }
                                        }
                                    }
                                }

                                // heatbeat connection connected checker
                                if ((DateTime.Now - lastTimeActive).TotalMilliseconds > 5000 && _tcpClient.Connected)
                                {
                                    // at least 5 seconds since an rx or a tx, we should tx a byte, just to check connection state
                                    Byte[] sendBytes = Encoding.ASCII.GetBytes("\0");
                                    try
                                    {
                                        stream.Write(sendBytes, 0, sendBytes.Length);
                                        lastTimeActive = DateTime.Now;
                                    }
                                    catch (Exception ex) when (ex is SocketException || ex is IOException || ex is ObjectDisposedException)
                                    {
                                        // Handle disconnection or stream issues
                                        break;
                                    }
                                }

                                if (sleep)
                                    Thread.Sleep(50);
                                else
                                    Thread.Sleep(1);

                            }
                            if (_isRunning && !started)
                            {
                                _tcpListener.Start();
                            }
                        }
                        catch (Exception ex) when (ex is SocketException || ex is IOException || ex is ObjectDisposedException)
                        {
                            // Handle client connection acceptance issues
                            if (_tcpClient != null)
                            {
                                _tcpClient.Close();
                            }
                        }
                    }
                }
                catch (Exception ex) when (ex is SocketException || ex is ObjectDisposedException)
                {
                    // Handle listener stopping issues
                }
                catch (Exception ex)
                {
                    Debug.Print($">>>>>>   Exception in listener {_guid}: {ex.Message}");
                }
                finally
                {
                    if (_tcpClient != null)
                    {
                        _tcpClient.Close();
                    }
                    _tcpListener.Stop();
                }
            }
        }
        public class TcpClientHandler
        {
            private Guid _guid;
            private MMIOType _type;
            private string _ip;
            private int _port;
            private TcpClient _tcpClient;
            private Thread _clientThread;
            private volatile bool _isRunning;
            private NetworkStream _networkStream;

            public event Action<Guid, string> ReceivedDataString;
            public event Action<Guid> TransmittedData;
            public event Action<Guid, MMIOType, bool> ConnectorRunning;

            public TcpClientHandler(Guid guid, MMIOType type, string ip, int port)
            {
                _guid = guid;
                _type = type;
                _ip = ip;
                _port = port;
                //_tcpClient = new TcpClient();
            }

            public void Start()
            {
                _isRunning = true;
                _clientThread = new Thread(new ThreadStart(Connect));
                _clientThread.IsBackground = true;
                _clientThread.Start();
                ConnectorRunning?.Invoke(_guid, _type, true);
            }

            public void Stop()
            {
                _isRunning = false;
                if (_tcpClient != null)
                {
                    _tcpClient.Close();
                }
                _clientThread.Join();
                ConnectorRunning?.Invoke(_guid, _type, false);
            }

            private void Connect()
            {
                bool reconnect = true;
                while (_isRunning && reconnect)
                {
                    try
                    {
                        reconnect = false;
                        _tcpClient = new TcpClient();
                        _tcpClient.Connect(_ip, _port);
                        _networkStream = _tcpClient.GetStream();

                        DateTime lastTimeActive = DateTime.Now;
                        string bufferConcat = "";

                        while (_isRunning)
                        {
                            if (!_tcpClient.Connected)
                            {
                                break;
                            }

                            bool sleep = true;
                            bool inbound = _mmio_data[_guid].Direction == MMIODirection.IN || _mmio_data[_guid].Direction == MMIODirection.BOTH;
                            bool outbound = _mmio_data[_guid].Direction == MMIODirection.OUT || _mmio_data[_guid].Direction == MMIODirection.BOTH;

                            if (inbound)
                            {
                                try
                                {
                                    if (_networkStream.DataAvailable)
                                    {
                                        byte[] buffer = new byte[1024];
                                        int bytesRead = _networkStream.Read(buffer, 0, buffer.Length);
                                        if (bytesRead > 0)
                                        {
                                            lastTimeActive = DateTime.Now;
                                            string receivedData = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                                            sleep = false;
                                            string term = "";
                                            switch (_mmio_data[_guid].TerminatorIn)
                                            {
                                                case MMIOTerminator.NONE:
                                                    term = "";
                                                    break;
                                                case MMIOTerminator.CR:
                                                    term = "\r";
                                                    break;
                                                case MMIOTerminator.LF:
                                                    term = "\n";
                                                    break;
                                                case MMIOTerminator.CRLF:
                                                    term = "\r\n";
                                                    break;
                                                case MMIOTerminator.CUSTOM:
                                                    term = _mmio_data[_guid].CustomTerminatorParsedIn;
                                                    break;
                                            }
                                            bufferConcat += receivedData;
                                            if (string.IsNullOrEmpty(term))
                                            {
                                                ReceivedDataString?.Invoke(_guid, bufferConcat);
                                                bufferConcat = "";
                                            }
                                            else
                                            {
                                                //int pos = receivedData.IndexOf(term);
                                                int pos = bufferConcat.IndexOf(term);
                                                while (pos > -1)
                                                {
                                                    ReceivedDataString?.Invoke(_guid, bufferConcat.Substring(0, pos));
                                                    bufferConcat = bufferConcat.Substring(pos + term.Length);
                                                    pos = bufferConcat.IndexOf(term);
                                                }
                                            }

                                            if (bufferConcat.Length >= 4096 * 8) bufferConcat = bufferConcat.Substring(4096 * 4);
                                        }
                                    }
                                }
                                catch (Exception ex) when (ex is SocketException || ex is IOException || ex is ObjectDisposedException)
                                {
                                    if (_tcpClient != null && !_tcpClient.Connected)
                                        reconnect = true;

                                    break;
                                }
                            }
                            if (outbound)
                            {
                                if (!_mmio_data[_guid].OutboundQueueEmpty)
                                {
                                    string outData = "";
                                    int n = 0;
                                    string termSend = "";
                                    switch (_mmio_data[_guid].TerminatorOut)
                                    {
                                        case MMIOTerminator.NONE:
                                            termSend = "";
                                            break;
                                        case MMIOTerminator.CR:
                                            termSend = "\r";
                                            break;
                                        case MMIOTerminator.LF:
                                            termSend = "\n";
                                            break;
                                        case MMIOTerminator.CRLF:
                                            termSend = "\r\n";
                                            break;
                                        case MMIOTerminator.CUSTOM:
                                            termSend = _mmio_data[_guid].CustomTerminatorParsedOut;
                                            break;
                                    }
                                    while (!_mmio_data[_guid].OutboundQueueEmpty)
                                    {
                                        outData += _mmio_data[_guid].DequeueOutbound() + termSend;
                                        n++;
                                        if (n == 20) break;
                                    }
                                    if (outData.Length > 0)
                                    {
                                        lastTimeActive = DateTime.Now;
                                        sleep = false;

                                        Byte[] sendBytes = Encoding.ASCII.GetBytes(outData);
                                        try
                                        {
                                            _networkStream.Write(sendBytes, 0, sendBytes.Length);
                                            TransmittedData?.Invoke(_guid);
                                        }
                                        catch (Exception ex) when (ex is SocketException || ex is IOException || ex is ObjectDisposedException)
                                        {
                                            if (_tcpClient != null && !_tcpClient.Connected)
                                                reconnect = true;

                                            break;
                                        }
                                    }
                                }
                            }

                            if ((DateTime.Now - lastTimeActive).TotalMilliseconds > 5000 && _tcpClient.Connected)
                            {
                                Byte[] sendBytes = Encoding.ASCII.GetBytes("\0");
                                try
                                {
                                    _networkStream.Write(sendBytes, 0, sendBytes.Length);
                                    lastTimeActive = DateTime.Now;
                                }
                                catch (Exception ex) when (ex is SocketException || ex is IOException || ex is ObjectDisposedException)
                                {
                                    if (_tcpClient != null && !_tcpClient.Connected)
                                        reconnect = true;

                                    break;
                                }
                            }

                            if (sleep)
                                Thread.Sleep(50);
                            else
                                Thread.Sleep(1);
                        }
                    }
                    catch (Exception ex) when (ex is SocketException || ex is ObjectDisposedException)
                    {
                        if (_tcpClient != null && !_tcpClient.Connected)
                            reconnect = true;
                    }
                    catch (Exception ex)
                    {
                        if (_tcpClient != null && !_tcpClient.Connected)
                            reconnect = true;
                    }
                    finally
                    {
                        if (_tcpClient != null)
                        {
                            _tcpClient.Close();
                        }
                    }

                    if (reconnect)
                        Thread.Sleep(100);
                }
            }
        }
        private class UdpListener
        {
            private Guid _guid;
            private string _ip;
            private int _port;
            private MMIOType _type;
            private UdpClient _udpClient;
            private Thread _listenerThread;
            private volatile bool _isRunning;

            public event Action<Guid, string> ReceivedDataString;
            public event Action<Guid> TransmittedData;
            public event Action<Guid, MMIOType, bool> ConnectorRunning;

            public UdpListener(Guid guid, MMIOType type, string ip, int port)
            {
                _guid = guid;
                _type = type;
                _ip = ip;
                _port = port;
                _udpClient = new UdpClient(new IPEndPoint(IPAddress.Parse(ip), port));
            }

            public void Start()
            {
                _isRunning = true;
                _listenerThread = new Thread(new ThreadStart(listen));
                _listenerThread.IsBackground = true;
                _listenerThread.Start();
                ConnectorRunning?.Invoke(_guid, _type, true);
            }

            public void Stop()
            {
                _isRunning = false;
                _udpClient.Close();
                _listenerThread.Join();
                ConnectorRunning?.Invoke(_guid, _type, false);
            }

            private void listen()
            {
                string bufferConcat = "";
                UdpClient udpSenderClient = null;

                try
                {
                    while (_isRunning)
                    {
                        bool inbound = _mmio_data[_guid].Direction == MMIODirection.IN || _mmio_data[_guid].Direction == MMIODirection.BOTH;
                        bool outbound = _mmio_data[_guid].Direction == MMIODirection.OUT || _mmio_data[_guid].Direction == MMIODirection.BOTH;
                        bool sleep = true;

                        if (inbound)
                        {
                            try
                            {
                                if (_udpClient.Available > 0)
                                {
                                    IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
                                    byte[] data = _udpClient.Receive(ref remoteEndPoint);
                                    string receivedData = Encoding.UTF8.GetString(data);                                    
                                    sleep = false;
                                    string term = "";
                                    switch (_mmio_data[_guid].TerminatorIn)
                                    {
                                        case MMIOTerminator.NONE:
                                            term = "";
                                            break;
                                        case MMIOTerminator.CR:
                                            term = "\r";
                                            break;
                                        case MMIOTerminator.LF:
                                            term = "\n";
                                            break;
                                        case MMIOTerminator.CRLF:
                                            term = "\r\n";
                                            break;
                                        case MMIOTerminator.CUSTOM:
                                            term = _mmio_data[_guid].CustomTerminatorParsedIn;
                                            break;
                                    }
                                    bufferConcat += receivedData;                                    
                                    if (string.IsNullOrEmpty(term))
                                    {
                                        ReceivedDataString?.Invoke(_guid, bufferConcat);
                                        bufferConcat = "";
                                    }
                                    else
                                    {
                                        //int pos = receivedData.IndexOf(term);
                                        int pos = bufferConcat.IndexOf(term);
                                        while (pos > -1)
                                        {
                                            ReceivedDataString?.Invoke(_guid, bufferConcat.Substring(0, pos));
                                            bufferConcat = bufferConcat.Substring(pos + term.Length);
                                            pos = bufferConcat.IndexOf(term);
                                        }
                                    }

                                    if (bufferConcat.Length >= 4096 * 8) bufferConcat = bufferConcat.Substring(4096 * 4); // some limiter incase it is not being processed
                                }
                            }
                            catch (Exception ex) when (ex is SocketException || ex is IOException || ex is ObjectDisposedException)
                            {
                                if (!_isRunning) break;
                                //Debug.Print($">>>>>>   Socket Exception in listener {_guid}: {ex.Message}");
                            }
                        }
                        if (outbound && _mmio_data[_guid].UDPEndPoint != null)
                        {
                            if (udpSenderClient == null)
                            {
                                udpSenderClient = new UdpClient();
                                udpSenderClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.NoDelay, true);
                            }
                            if (!_mmio_data[_guid].OutboundQueueEmpty)
                            {
                                string outData = "";
                                int n = 0;
                                string termSend = "";
                                switch (_mmio_data[_guid].TerminatorOut)
                                {
                                    case MMIOTerminator.NONE:
                                        termSend = "";
                                        break;
                                    case MMIOTerminator.CR:
                                        termSend = "\r";
                                        break;
                                    case MMIOTerminator.LF:
                                        termSend = "\n";
                                        break;
                                    case MMIOTerminator.CRLF:
                                        termSend = "\r\n";
                                        break;
                                    case MMIOTerminator.CUSTOM:
                                        termSend = _mmio_data[_guid].CustomTerminatorParsedOut;
                                        break;
                                }
                                while (!_mmio_data[_guid].OutboundQueueEmpty)
                                {
                                    outData += _mmio_data[_guid].DequeueOutbound() + termSend;
                                    n++;
                                    if (n == 20) break; // some limit
                                }
                                if (outData.Length > 0)
                                {
                                    sleep = false;

                                    Byte[] sendBytes = Encoding.ASCII.GetBytes(outData);
                                    try
                                    {
                                        udpSenderClient.Send(sendBytes, sendBytes.Length, _mmio_data[_guid].UDPEndPoint);
                                        TransmittedData?.Invoke(_guid);
                                    }
                                    catch (Exception ex) when (ex is SocketException || ex is IOException || ex is ObjectDisposedException)
                                    {
                                        if (!_isRunning) break;
                                    }
                                }
                            }
                        }
                        else if (udpSenderClient != null)
                        {
                            try
                            {
                                udpSenderClient.Close();
                            }
                            catch (Exception ex) when (ex is SocketException || ex is IOException || ex is ObjectDisposedException)
                            {
                            }
                            udpSenderClient = null;
                        }

                        if (sleep)
                            Thread.Sleep(50);
                        else
                            Thread.Sleep(1);
                    }
                }
                catch (Exception ex)
                {
                    Debug.Print($">>>>>>   Exception in listener {_guid}: {ex.Message}");
                }
            }
        }
        public class SerialPortHandler
        {
            private Guid _guid;
            private MMIOType _type;
            private string _comPort;
            private int _baudRate;
            private int _dataBits;
            private StopBits _stopBits;
            private Parity _parity;
            private SerialPort _serialPort;
            private Thread _serialThread;
            private volatile bool _isRunning;

            public event Action<Guid, string> ReceivedDataString;
            public event Action<Guid> TransmittedData;
            public event Action<Guid, MMIOType, bool> ConnectorRunning;

            public SerialPortHandler(Guid guid, MMIOType type, string comPort, int baudRate, int dataBits, StopBits stopBits, Parity parity)
            {
                _guid = guid;
                _type = type;
                _comPort = comPort;
                _baudRate = baudRate;
                _dataBits = dataBits;
                _stopBits = stopBits;
                _parity = parity;
                _serialPort = new SerialPort(comPort, baudRate, parity, dataBits, stopBits);
                _serialPort.ReadTimeout = 1000;
                _serialPort.WriteTimeout = 1000;
            }

            public void Start()
            {
                _isRunning = true;
                _serialThread = new Thread(new ThreadStart(Connect));
                _serialThread.IsBackground = true;
                _serialThread.Start();
                ConnectorRunning?.Invoke(_guid, _type, true);
            }

            public void Stop()
            {
                _isRunning = false;
                if (_serialPort != null && _serialPort.IsOpen)
                {
                    _serialPort.Close();
                }
                _serialThread.Join();
                ConnectorRunning?.Invoke(_guid, _type, false);
            }

            private void Connect()
            {
                try
                {
                    _serialPort.Open();

                    DateTime lastTimeActive = DateTime.Now;
                    string bufferConcat = "";

                    while (_isRunning)
                    {
                        if (!_serialPort.IsOpen)
                        {
                            break;
                        }

                        bool sleep = true;
                        bool inbound = _mmio_data[_guid].Direction == MMIODirection.IN || _mmio_data[_guid].Direction == MMIODirection.BOTH;
                        bool outbound = _mmio_data[_guid].Direction == MMIODirection.OUT || _mmio_data[_guid].Direction == MMIODirection.BOTH;

                        if (inbound)
                        {
                            try
                            {
                                if (_serialPort.BytesToRead > 0)
                                {
                                    byte[] buffer = new byte[1024];
                                    int bytesRead = _serialPort.Read(buffer, 0, buffer.Length);
                                    if (bytesRead > 0)
                                    {
                                        lastTimeActive = DateTime.Now;
                                        string receivedData = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                                        sleep = false;
                                        string term = "";
                                        switch (_mmio_data[_guid].TerminatorIn)
                                        {
                                            case MMIOTerminator.NONE:
                                                term = "";
                                                break;
                                            case MMIOTerminator.CR:
                                                term = "\r";
                                                break;
                                            case MMIOTerminator.LF:
                                                term = "\n";
                                                break;
                                            case MMIOTerminator.CRLF:
                                                term = "\r\n";
                                                break;
                                            case MMIOTerminator.CUSTOM:
                                                term = _mmio_data[_guid].CustomTerminatorParsedIn;
                                                break;
                                        }
                                        bufferConcat += receivedData;
                                        if (string.IsNullOrEmpty(term))
                                        {
                                            ReceivedDataString?.Invoke(_guid, bufferConcat);
                                            bufferConcat = "";
                                        }
                                        else
                                        {
                                            //int pos = receivedData.IndexOf(term);
                                            int pos = bufferConcat.IndexOf(term);
                                            while (pos > -1)
                                            {
                                                ReceivedDataString?.Invoke(_guid, bufferConcat.Substring(0, pos));
                                                bufferConcat = bufferConcat.Substring(pos + term.Length);
                                                pos = bufferConcat.IndexOf(term);
                                            }
                                        }

                                        if (bufferConcat.Length >= 4096 * 8) bufferConcat = bufferConcat.Substring(4096 * 4);
                                    }
                                }
                            }
                            catch (Exception ex) when (ex is IOException || ex is InvalidOperationException)
                            {
                                break;
                            }
                        }
                        if (outbound)
                        {
                            if (!_mmio_data[_guid].OutboundQueueEmpty)
                            {
                                string outData = "";
                                int n = 0;
                                string termSend = "";
                                switch (_mmio_data[_guid].TerminatorOut)
                                {
                                    case MMIOTerminator.NONE:
                                        termSend = "";
                                        break;
                                    case MMIOTerminator.CR:
                                        termSend = "\r";
                                        break;
                                    case MMIOTerminator.LF:
                                        termSend = "\n";
                                        break;
                                    case MMIOTerminator.CRLF:
                                        termSend = "\r\n";
                                        break;
                                    case MMIOTerminator.CUSTOM:
                                        termSend = _mmio_data[_guid].CustomTerminatorParsedOut;
                                        break;
                                }
                                while (!_mmio_data[_guid].OutboundQueueEmpty)
                                {
                                    outData += _mmio_data[_guid].DequeueOutbound() + termSend;
                                    n++;
                                    if (n == 20) break;
                                }
                                if (outData.Length > 0)
                                {
                                    lastTimeActive = DateTime.Now;
                                    sleep = false;

                                    Byte[] sendBytes = Encoding.ASCII.GetBytes(outData);
                                    try
                                    {
                                        _serialPort.Write(sendBytes, 0, sendBytes.Length);
                                        TransmittedData?.Invoke(_guid);
                                    }
                                    catch (Exception ex) when (ex is IOException || ex is InvalidOperationException)
                                    {
                                        break;
                                    }
                                }
                            }
                        }

                        if ((DateTime.Now - lastTimeActive).TotalMilliseconds > 5000 && _serialPort.IsOpen)
                        {
                            Byte[] sendBytes = Encoding.ASCII.GetBytes("\0");
                            try
                            {
                                _serialPort.Write(sendBytes, 0, sendBytes.Length);
                                lastTimeActive = DateTime.Now;
                            }
                            catch (Exception ex) when (ex is IOException || ex is InvalidOperationException)
                            {
                                break;
                            }
                        }

                        if (sleep)
                            Thread.Sleep(50);
                        else
                            Thread.Sleep(1);
                    }
                }
                catch (Exception ex) when (ex is IOException || ex is InvalidOperationException)
                {
                    // Handle connection issues
                }
                catch (Exception ex)
                {
                    Debug.Print($">>>>>>   Exception in serial port {_guid}: {ex.Message}");
                }
                finally
                {
                    if (_serialPort != null && _serialPort.IsOpen)
                    {
                        _serialPort.Close();
                    }
                }
            }

            public static List<string> GetAvailableComPorts()
            {
                return new List<string>(SerialPort.GetPortNames());
            }
        }

        // Each startlisten will only accept a single TCP client for that IP/port combo.
        // UDP startlisteners can have messages from multiple UDP clients, but single endpoint for output
        public static event Action<Guid> ClientConnected;
        public static event Action<Guid> ClientDisconnected;
        public static event Action<Guid, string> ReceivedDataString;
        public static event Action<Guid> TransmittedData;
        public static event Action<Guid, MMIOType, bool> ConnectorRunning;

        private static ConcurrentDictionary<Guid, UdpListener> _udp_listeners;
        private static ConcurrentDictionary<Guid, TcpListener> _tcpip_listeners;
        private static ConcurrentDictionary<Guid, TcpClientHandler> _tcpip_clients;
        private static ConcurrentDictionary<Guid, SerialPortHandler> _serial_ports;
        private static ConcurrentDictionary<Guid, clsMMIO> _mmio_data;

        private static readonly object _connectionLock = new object();

        static MultiMeterIO()
        {
            Debug.Print("MultiMeterIO Init");
            _udp_listeners = new ConcurrentDictionary<Guid, UdpListener>();
            _tcpip_listeners = new ConcurrentDictionary<Guid, TcpListener>();
            _tcpip_clients = new ConcurrentDictionary<Guid, TcpClientHandler>();
            _serial_ports = new ConcurrentDictionary<Guid, SerialPortHandler>();
            _mmio_data = new ConcurrentDictionary<Guid, clsMMIO>();

            ReceivedDataString += MultiMeterIO_ReceivedDataString;
            TransmittedData += MultiMeterIO_TransmittedData;
            ConnectorRunning += MultiMeterIO_ListenerRunning;
        }
        public static ConcurrentDictionary<Guid, clsMMIO> Data
        {
            get { return _mmio_data; }
        }
        public static bool StartListeningUDP(clsMMIO mmio)
        {
            if (mmio == null) return false;
            if (mmio.Type != MMIOType.UDP_LISTENER) return false;
            if (!Common.IsIpv4Valid(mmio.IP, mmio.Port)) return false;

            UdpListener listener;
            try
            {
                listener = new UdpListener(mmio.Guid, mmio.Type, mmio.IP, mmio.Port);
            }
            catch
            {
                return false;
            }
            listener.ReceivedDataString += (listenerGuid, data) => ReceivedDataString?.Invoke(listenerGuid, data);
            listener.ConnectorRunning += (listenerGuid, type, running) => ConnectorRunning?.Invoke(listenerGuid, type, running);
            listener.TransmittedData += (guid) => TransmittedData?.Invoke(guid);

            if (_udp_listeners.TryAdd(mmio.Guid, listener))
            {
                listener.Start();
                return true;
            }

            return false;
        }

        public static bool StartListeningTCPIP(clsMMIO mmio)
        {
            if (mmio == null) return false;
            if (mmio.Type != MMIOType.TCPIP_LISTENER) return false;
            if (!Common.IsIpv4Valid(mmio.IP, mmio.Port)) return false;

            TcpListener listener;
            try
            {
                listener = new TcpListener(mmio.Guid, mmio.Type, mmio.IP, mmio.Port);
            }
            catch
            {
                return false;
            }
            listener.ReceivedDataString += (listenerGuid, data) => ReceivedDataString?.Invoke(listenerGuid, data);
            listener.ConnectorRunning += (listenerGuid, type, running) => ConnectorRunning?.Invoke(listenerGuid, type, running);
            listener.TransmittedData += (guid) => TransmittedData?.Invoke(guid);

            if (_tcpip_listeners.TryAdd(mmio.Guid, listener))
            {
                listener.Start();
                return true;
            }
            return false;
        }
        public static bool StartTcpClient(clsMMIO mmio)
        {
            if (mmio == null) return false;
            if (mmio.Type != MMIOType.TCPIP_CLIENT) return false;
            if (!Common.IsIpv4Valid(mmio.IP, mmio.Port)) return false;

            TcpClientHandler clientHandler;
            try
            {
                clientHandler = new TcpClientHandler(mmio.Guid, mmio.Type, mmio.IP, mmio.Port);
            }
            catch
            {
                return false;
            }

            clientHandler.ReceivedDataString += (clientGuid, data) => ReceivedDataString?.Invoke(clientGuid, data);
            clientHandler.ConnectorRunning += (clientGuid, type, running) => ConnectorRunning?.Invoke(clientGuid, type, running);
            clientHandler.TransmittedData += (guid) => TransmittedData?.Invoke(guid);

            if (_tcpip_clients.TryAdd(mmio.Guid, clientHandler))
            {
                clientHandler.Start();
                return true;
            }

            return false;
        }
        public static bool StartSerialPort(clsMMIO mmio)
        {
            if (mmio == null) return false;
            if (mmio.Type != MMIOType.SERIAL) return false;
            if (string.IsNullOrEmpty(mmio.ComPort) || mmio.BaudRate <= 0 || mmio.DataBits <= 0) return false;

            SerialPortHandler serialPortHandler;
            try
            {
                serialPortHandler = new SerialPortHandler(mmio.Guid, mmio.Type, mmio.ComPort, mmio.BaudRate, mmio.DataBits, mmio.StopBits, mmio.Parity);
            }
            catch
            {
                return false;
            }

            serialPortHandler.ReceivedDataString += (portGuid, data) => ReceivedDataString?.Invoke(portGuid, data);
            serialPortHandler.ConnectorRunning += (portGuid, type, running) => ConnectorRunning?.Invoke(portGuid, type, running);
            serialPortHandler.TransmittedData += (guid) => TransmittedData?.Invoke(guid);

            if (_serial_ports.TryAdd(mmio.Guid, serialPortHandler))
            {
                serialPortHandler.Start();
                return true;
            }

            return false;
        }
        public static void StopConnection(Guid guid)
        {
            UdpListener listener;
            if (_udp_listeners.TryRemove(guid, out listener))
            {
                listener.Stop();
            }
            TcpListener tcpipListener;
            if (_tcpip_listeners.TryRemove(guid, out tcpipListener))
            {
                tcpipListener.Stop();
            }
            TcpClientHandler tcpipClient;
            if (_tcpip_clients.TryRemove(guid, out tcpipClient))
            {
                tcpipClient.Stop();
            }
            SerialPortHandler serialPort;
            if (_serial_ports.TryRemove(guid, out serialPort))
            {
                serialPort.Stop();
            }

            Debug.Print("MultiMeterIO Stopped listening : " + guid.ToString());        
        }
        public static void StopConnections()
        {
            foreach (KeyValuePair<Guid, UdpListener> kvp in _udp_listeners)
            {
                kvp.Value.Stop();
            }
            _udp_listeners.Clear();

            foreach (KeyValuePair<Guid, TcpListener> kvp in _tcpip_listeners)
            {
                kvp.Value.Stop();
            }
            _tcpip_listeners.Clear();

            foreach (KeyValuePair<Guid, TcpClientHandler> kvp in _tcpip_clients)
            {
                kvp.Value.Stop();
            }
            _tcpip_clients.Clear();

            foreach (KeyValuePair<Guid, SerialPortHandler> kvp in _serial_ports)
            {
                kvp.Value.Stop();
            }
            _serial_ports.Clear();
        }
        public static bool AlreadyConfigured(string ip, int port, MMIOType type)
        {
            foreach (KeyValuePair<Guid, clsMMIO> kvp in _mmio_data)
            {
                clsMMIO mmio = kvp.Value;
                if (mmio.IP.Equals(ip) &&
                    mmio.Port == port && mmio.Type == type)
                {
                    return true;
                }
            }
            return false;
        }
        public static string FourChar(string data1, int data2, Guid guid)
        {
            string input = $"{data1}:{data2}:{guid}";
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
                string base64Hash = Convert.ToBase64String(hashBytes);
                return convertToFourChar(base64Hash);
            }
        }

        private static string convertToFourChar(string base64Hash)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            char[] result = new char[4];
            int[] indices = new int[4];
            for (int i = 0; i < base64Hash.Length; i++)
            {
                indices[i % 4] = (indices[i % 4] + base64Hash[i]) % chars.Length;
            }
            for (int i = 0; i < 4; i++)
            {
                result[i] = chars[indices[i]];
            }
            return new string(result);
        }

        public static string GetSaveData()
        {
            //1 for version 1
            return "1|" + Common.SerializeToBase64<ConcurrentDictionary<Guid, clsMMIO>>(_mmio_data);

            //string data = "";

            //data += _mmio_data.Count.ToString() + "|";
            //foreach (KeyValuePair<Guid, clsMMIO> kvp in _mmio_data)
            //{
            //    clsMMIO mmio = kvp.Value;
            //    data += mmio.Guid.ToString() + "|"; //1
            //    data += mmio.Direction.ToString() + "|"; //2
            //    data += mmio.IP + "|"; //3
            //    data += mmio.Port.ToString() + "|"; //4
            //    data += mmio.FormatIn.ToString() + "|"; //5
            //    data += mmio.Type.ToString() + "|"; //6
            //    data += mmio.Enabled.ToString() + "|"; //7
            //    data += mmio.TerminatorIn.ToString() + "|"; //8
            //    data += mmio.FormatOut.ToString() + "|"; //9
            //    data += mmio.TerminatorOut.ToString() + "|"; //10
            //    data += mmio.CustomTerminatorIn.Replace("|", "++><++") + "|"; //11
            //    data += mmio.CustomTerminatorOut.Replace("|", "++><++") + "|"; //12
            //    data += mmio.UdpEndpointIP + "|"; //13
            //    data += mmio.UdpEndpointPort.ToString() + "|"; //14

            //    // always last
            //    data += mmio.Variables().Count.ToString() + "|"; //15
            //    foreach(KeyValuePair<string, object> kvpvar in mmio.Variables())
            //    {
            //        data += kvpvar.Key + "|";

            //        string val;
            //        object valobj = kvpvar.Value;

            //        if (valobj is int)
            //            val = valobj.ToString();
            //        else if (valobj is float)
            //            val = ((float)valobj).ToString("0.0#####");
            //        else if (valobj is double)
            //            val = ((double)valobj).ToString("0.0#####");
            //        else if (valobj is bool)
            //            val = valobj.ToString().ToLower();
            //        else
            //            val = valobj.ToString().Replace("|", "++><++");

            //        data += val + "|";
            //    }
            //}
            //data = data.Substring(0, data.Length - 1); // scrap trailing |

            //return data;
        }
        public static bool RestoreSaveData2(string data)
        {
            if (string.IsNullOrEmpty(data)) return true;
            try
            {
                StopConnections(); //start new                

                string[] parts = data.Split('|');
                if (parts.Length != 2) return false;

                if (parts[0] == "1") // 1 signifies version 1 of the serialize for future proofing
                    _mmio_data = Common.DeserializeFromBase64<ConcurrentDictionary<Guid, clsMMIO>>(parts[1]);

                foreach(KeyValuePair<Guid, clsMMIO> kvp in _mmio_data)
                {
                    clsMMIO mmio = kvp.Value;
                    if (mmio.Enabled)
                        mmio.StartConnection();
                }

                return true;
            }
            catch
            {
                return false;
            }
        }
        public static bool RestoreSaveData(string data)
        {
            if (string.IsNullOrEmpty(data)) return true;
            StopConnections(); //start new           

            string[] parts = data.Split('|');
            if (parts.Length < 1) return true;

            bool ok;
            int listeners;

            ok = int.TryParse(parts[0], out listeners);
            if (ok)
            {
                Guid guid = Guid.Empty;
                MMIODirection direction = MMIODirection.BOTH;
                MMIOFormat format_in = MMIOFormat.JSON;
                MMIOFormat format_out = MMIOFormat.JSON;
                MMIOType type = MMIOType.UDP_LISTENER;
                MMIOTerminator terminator_in = MMIOTerminator.NONE;
                MMIOTerminator terminator_out = MMIOTerminator.NONE;
                int port = 0;
                bool enabled = false;
                int variables = 0;
                int variable_count_index = 0;
                int udp_endpoint_port = 0;

                int idx = 0;

                for (int i = 0; i < listeners; i++)
                {
                    clsMMIO mmio = new clsMMIO();
                    if (ok)
                    {
                        ok = Guid.TryParse(parts[idx + 1], out guid);
                    }
                    if (ok)
                    {
                        mmio.Guid = guid;
                        ok = Enum.TryParse<MMIODirection>(parts[idx + 2], out direction);
                    }
                    if (ok)
                    {
                        mmio.Direction = direction;
                        mmio.IP = parts[idx + 3];
                        ok = int.TryParse(parts[idx + 4], out port);
                    }
                    if (ok)
                    {
                        mmio.Port = port;
                        ok = Enum.TryParse<MMIOFormat>(parts[idx + 5], out format_in);
                    }
                    if (ok)
                    {
                        mmio.FormatIn = format_in;
                        ok = Enum.TryParse<MMIOType>(parts[idx + 6], out type);
                    }
                    if (ok)
                    {
                        mmio.Type = type;
                        ok = bool.TryParse(parts[idx + 7], out enabled);
                    }
                    if (ok)
                    {
                        mmio.Enabled = enabled;
                        ok = Enum.TryParse<MMIOTerminator>(parts[idx + 8], out terminator_in);
                    }
                    if (ok)
                    {
                        mmio.TerminatorIn = terminator_in;
                        ok = Enum.TryParse<MMIOFormat>(parts[idx + 9], out format_out);
                    }
                    if (ok)
                    {
                        mmio.FormatOut = format_out;
                        ok = Enum.TryParse<MMIOTerminator>(parts[idx + 10], out terminator_out);
                    }
                    if (ok)
                    {
                        mmio.TerminatorOut = terminator_out;
                        mmio.CustomTerminatorIn = parts[idx + 11].Replace("++><++", "|");
                        mmio.CustomTerminatorOut = parts[idx + 12].Replace("++><++", "|");
                        mmio.UdpEndpointIP = parts[idx + 13];
                        ok = int.TryParse(parts[idx + 14], out udp_endpoint_port);
                    }
                    if (ok)
                    {
                        mmio.UdpEndpointPort = udp_endpoint_port;
                    }
                    if (ok)
                    {
                        // restore variables
                        variable_count_index = 15; // this needs to be one larger than N in the [idx + N] above
                        ok = int.TryParse(parts[idx + variable_count_index], out variables);
                        if (ok)
                        {
                            for(int n = 0; n < variables; n++)
                            {
                                string val = parts[idx + (variable_count_index + 2) + (n * 2)].Replace("++><++", "|");
                                Type tpe = mmio.DetermineType(val);
                                object typedValue = mmio.ConvertToType(val, tpe);
                                ok = mmio.Variables().TryAdd(parts[idx + (variable_count_index + 1) + (n * 2)], typedValue);
                                if (!ok) break;
                            }
                        }
                    }
                    if (ok)
                    {
                        ok = _mmio_data.TryAdd(guid, mmio);
                        if (!ok)
                        {
                            StopConnection(guid);
                            return false;
                        }
                    }
                    if (ok && enabled)
                    {
                        mmio.StartConnection();
                    }
                    if (ok)
                    {
                        idx += variable_count_index + (variables * 2);
                    }
                }
            }
            return ok;
        }
        public static bool AddMMIO(clsMMIO mmio)
        {
            if (_mmio_data.ContainsKey(mmio.Guid)) return false;
            if (mmio == null) return false;

            bool ok = _mmio_data.TryAdd(mmio.Guid, mmio);
            if (!ok)
            {
                StopConnection(mmio.Guid);
                return false;
            }
            return ok;
        }
        public static bool RemoveMMIO(Guid guid)
        {
            bool ok = false;
            if (_mmio_data.ContainsKey(guid))
            {
                ok = _mmio_data.TryRemove(guid, out clsMMIO mmio);
                if (ok) StopConnection(guid);
            }
            return ok;
        }
        public static void SendDataMMIO(Guid guid, string data)
        {
            if(_mmio_data.ContainsKey(guid))
            {
                clsMMIO mmio = _mmio_data[guid];
                mmio.EnqueueOutbound(data);
            }
        }
        public static Guid GuidfromFourChar(string fourChar)
        {
            foreach(KeyValuePair<Guid, clsMMIO> kvp in _mmio_data)
            {
                clsMMIO mmio = kvp.Value;
                if(mmio.FourChar == fourChar) return mmio.Guid;
            }
            return Guid.Empty;
        }
        private static void MultiMeterIO_ListenerRunning(Guid guid, MMIOType type, bool running)
        {
            if (!MultiMeterIO.Data.ContainsKey(guid)) return;
            MultiMeterIO.clsMMIO mmio = MultiMeterIO.Data[guid];
            mmio.Active = running;
        }
        private static void MultiMeterIO_TransmittedData(Guid guid)
        {
        }
        [DebuggerHidden]
        public static bool IsValidXml(string xmlString)
        {
            try
            {
                XmlReaderSettings settings = new XmlReaderSettings();
                settings.DtdProcessing = DtdProcessing.Parse;
                settings.XmlResolver = null;
                settings.ValidationType = ValidationType.None;

                using (XmlReader reader = XmlReader.Create(new System.IO.StringReader(xmlString), settings))
                {
                    while (reader.Read())
                    {
                        // Simply reading the XML, if it's invalid, an exception will be thrown
                    }
                }

                return true;
            }
            catch (XmlException)
            {
                return false;
            }
        }
        //[DebuggerHidden]
        private static void MultiMeterIO_ReceivedDataString(Guid guid, string dataString)
        {
            char[] charsToTrim = { ' ', '\n', '\r', '\t', '\0' };
            dataString = dataString.Trim(charsToTrim);
            if (string.IsNullOrWhiteSpace(dataString)) return;

            //Debug.Print("Data : " + guid + " : [" + dataString + "]");
            if (!MultiMeterIO.Data.ContainsKey(guid)) return;

            MultiMeterIO.clsMMIO mmio = MultiMeterIO.Data[guid];
            MultiMeterIO.MMIOFormat format = mmio.FormatIn;
            string fourChar = mmio.FourChar;

            Dictionary<string, string> keyValuePairs = new Dictionary<string, string>();
            switch (format)
            {
                case MultiMeterIO.MMIOFormat.JSON:
                    try
                    {
                        JObject parsedJson = JObject.Parse(dataString);
                        parseJsonToken(parsedJson, fourChar, keyValuePairs);
                    }
                    catch { }
                    break;
                case MultiMeterIO.MMIOFormat.XML:
                    try
                    {
                        if (IsValidXml(dataString))
                        {
                            XElement parsedXml = XElement.Parse(dataString);
                            parseXMLElement(parsedXml, fourChar, keyValuePairs);
                        }
                    }
                    catch { }
                    break;
                case MultiMeterIO.MMIOFormat.RAW:
                    try
                    {
                        string[] split = dataString.Split(':');
                        int pairs = split.Length / 2;
                        for(int n = 0;n < pairs; n++)
                        {
                            keyValuePairs.Add(fourChar + "." + split[n * 2], split[(n * 2) + 1]);
                        }
                    }
                    catch { }
                    break;
            }

            //foreach (KeyValuePair<string, string> kvp in keyValuePairs)
            //{
            //    //Debug.Print($"key = {kvp.Key}    value = {kvp.Value}    type = {determineType(kvp.Value)}");
            //    Type tpe = DetermineType(kvp.Value);
            //    object typedValue = ConvertToType(kvp.Value, tpe);
            //    mmio.SetVariable(kvp.Key, typedValue);
            //}
            Parallel.ForEach(keyValuePairs, kvp =>
            {
                Type tpe = mmio.DetermineType(kvp.Value);
                object typedValue = mmio.ConvertToType(kvp.Value, tpe);
                mmio.SetVariable(kvp.Key, typedValue);
            });
        }
        public static void parseJsonToken(JToken token, string currentPath, Dictionary<string, string> keyValuePairs)
        {
            if (token is JValue)
            {
                JValue valueToken = (JValue)token;
                keyValuePairs[currentPath] = valueToken.ToString();
            }
            else if (token is JObject)
            {
                JObject obj = (JObject)token;
                foreach (JProperty property in obj.Properties())
                {
                    parseJsonToken(property.Value, $"{currentPath}.{property.Name}", keyValuePairs);
                }
            }
            else if (token is JArray)
            {
                JArray array = (JArray)token;
                for (int i = 0; i < array.Count; i++)
                {
                    parseJsonToken(array[i], $"{currentPath}[{i}]", keyValuePairs);
                }
            }
        }
        private static void parseXMLElement(XElement element, string currentPath, Dictionary<string, string> keyValuePairs)
        {
            foreach (XAttribute attribute in element.Attributes())
            {
                keyValuePairs[$"{currentPath}.{attribute.Name}"] = attribute.Value;
            }

            bool hasElements = false;
            foreach (XElement childElement in element.Elements())
            {
                hasElements = true;
                parseXMLElement(childElement, $"{currentPath}.{childElement.Name}", keyValuePairs);
            }

            if (!hasElements)
            {
                keyValuePairs[currentPath] = element.Value;
            }
        }
    }
}
