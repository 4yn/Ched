﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;

namespace Ched.Configuration
{
    internal sealed class ApplicationSettings : SettingsBase
    {
        public static ApplicationSettings Default { get; } = (ApplicationSettings)Synchronized(new ApplicationSettings());

        private ApplicationSettings()
        {
        }

        [UserScopedSetting]
        [DefaultSettingValue("12")]
        public int UnitLaneWidth
        {
            get { return ((int)(this["UnitLaneWidth"])); }
            set { this["UnitLaneWidth"] = value; }
        }

        [UserScopedSetting]
        [DefaultSettingValue("120")]
        public int UnitBeatHeight
        {
            get { return ((int)(this["UnitBeatHeight"])); }
            set { this["UnitBeatHeight"] = value; }
        }

        [UserScopedSetting]
        [DefaultSettingValue("True")]
        public bool InsertAirWithAirAction
        {
            get { return ((bool)(this["InsertAirWithAirAction"])); }
            set { this["InsertAirWithAirAction"] = value; }
        }

        [UserScopedSetting]
        [DefaultSettingValue("False")]
        public bool IsPreviewAbortAtLastNote
        {
            get { return ((bool)(this["IsPreviewAbortAtLastNote"])); }
            set { this["IsPreviewAbortAtLastNote"] = value; }
        }

        [UserScopedSetting]
        [DefaultSettingValue("False")]
        public bool IsFollowWhenPlaying
        {
            get { return ((bool)(this["IsFollowWhenPlaying"])); }
            set { this["IsFollowWhenPlaying"] = value; }
        }

        [UserScopedSetting]
        [DefaultSettingValue("False")]
        public bool IsPlayAtHalfSpeed
        {
            get { return ((bool)(this["IsPlayAtHalfSpeed"])); }
            set { this["IsPlayAtHalfSpeed"] = value; }
        }

        [UserScopedSetting]
        [DefaultSettingValue("0")]
        public int RecorderInputDevice
        {
            get { return ((int)(this["RecorderInputDevice"])); }
            set { this["RecorderInputDevice"] = value; }
        }
    }
}
