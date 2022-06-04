﻿using System;
using System.Collections.Generic;
using System.Linq;
using Octokit;

namespace LenovoLegionToolkit.Lib
{
    public struct CPUBoostMode
    {
        public int Value { get; }
        public string Name { get; }

        public CPUBoostMode(int value, string name)
        {
            Value = value;
            Name = name;
        }
    }

    public struct CPUBoostModeSettings
    {
        public PowerPlan PowerPlan { get; }
        public List<CPUBoostMode> CPUBoostModes { get; }
        public int ACSettingValue { get; }
        public int DCSettingValue { get; }

        public CPUBoostModeSettings(PowerPlan powerPlan, List<CPUBoostMode> cpuBoostModes, int acSettingValue, int dcSettingValue)
        {
            PowerPlan = powerPlan;
            CPUBoostModes = cpuBoostModes;
            ACSettingValue = acSettingValue;
            DCSettingValue = dcSettingValue;
        }
    }
    public struct MachineInformation
    {
        public string Vendor { get; }
        public string Model { get; }

        public MachineInformation(string vendor, string model)
        {
            Vendor = vendor;
            Model = model;
        }
    }

    public struct PowerPlan
    {
        public string InstanceID { get; }
        public string Name { get; }
        public bool IsActive { get; }
        public string Guid => InstanceID.Split("\\").Last().Replace("{", "").Replace("}", "");

        public PowerPlan(string instanceID, string name, bool isActive)
        {
            InstanceID = instanceID;
            Name = name;
            IsActive = isActive;
        }

        public override string ToString() => Name;
    }

    public struct RefreshRate : IDisplayName
    {
        public int Frequency { get; }

        public string DisplayName => $"{Frequency} Hz";

        public RefreshRate(int frequency)
        {
            Frequency = frequency;
        }
    }
    public struct Update
    {
        public Version Version { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string? Url { get; set; }

        public Update(Release release)
        {
            Version = Version.Parse(release.TagName);
            Title = release.Name;
            Description = release.Body;
            Url = release.Assets.Where(ra => ra.Name.EndsWith("setup.exe", StringComparison.InvariantCultureIgnoreCase)).Select(ra => ra.BrowserDownloadUrl).FirstOrDefault();
        }
    }

    public struct WindowSize
    {
        public double Width { get; set; }
        public double Height { get; set; }

        public WindowSize(double width, double height)
        {
            Width = width;
            Height = height;
        }
    }
}
