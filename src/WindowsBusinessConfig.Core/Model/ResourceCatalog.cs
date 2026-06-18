using System;
using System.Collections.Generic;

namespace WindowsBusinessConfig.Core.Model;

/// <summary>
/// Maps DSC resource type + name to a friendly category and one-line
/// description for the editor and CLI listings.
/// </summary>
public static class ResourceCatalog
{
    private static readonly Dictionary<string, ResourceCategory> KnownByName = new(StringComparer.OrdinalIgnoreCase)
    {
        // Appearance
        ["darkTheme"]                    = ResourceCategory.Appearance,
        // Explorer
        ["ShowFileExtensions"]           = ResourceCategory.Explorer,
        ["FullPathTitlebar"]             = ResourceCategory.Explorer,
        ["OpenThisPC"]                   = ResourceCategory.Explorer,
        ["FrequentFolders"]              = ResourceCategory.Explorer,
        ["FrequentFiles"]                = ResourceCategory.Explorer,
        ["RecommendedFiles"]             = ResourceCategory.Explorer,
        // BloatRemoval / notifications
        ["TipsOff"]                      = ResourceCategory.BloatRemoval,
        ["DoNotDisturb"]                 = ResourceCategory.BloatRemoval,
        ["DisableSoftLanding"]           = ResourceCategory.BloatRemoval,
        ["CdmSettingsSuggestions"]       = ResourceCategory.BloatRemoval,
        ["CdmLockscreenSuggestions"]     = ResourceCategory.BloatRemoval,
        ["CdmSpotlightTips"]             = ResourceCategory.BloatRemoval,
        ["CdmRotatingLockScreen"]        = ResourceCategory.BloatRemoval,
        ["CdmSilentApps"]                = ResourceCategory.BloatRemoval,
        ["CdmOemPreinstall"]             = ResourceCategory.BloatRemoval,
        ["CdmPreInstall"]                = ResourceCategory.BloatRemoval,
        ["DisableConsumerFeatures"]      = ResourceCategory.BloatRemoval,
        ["DisableConsumerAccountStateContent"] = ResourceCategory.BloatRemoval,
        ["RemoveConsumerAppx"]           = ResourceCategory.BloatRemoval,
        // Privacy
        ["GameDvrOff"]                   = ResourceCategory.Privacy,
        ["GameBarPolicyOff"]             = ResourceCategory.Privacy,
    };

    public static ResourceCategory CategorizeByType(string? type, string? name)
    {
        if (type is null && name is null)
        {
            return ResourceCategory.Other;
        }

        if (type is not null && type.StartsWith("Microsoft.WinGet/Package", StringComparison.OrdinalIgnoreCase))
        {
            return ResourceCategory.Apps;
        }

        if (name is not null && KnownByName.TryGetValue(name, out var known))
        {
            return known;
        }

        if (name is not null && name.Equals("InstallMarker", StringComparison.OrdinalIgnoreCase))
        {
            return ResourceCategory.InstallMarker;
        }

        if (name is null)
        {
            return ResourceCategory.Other;
        }

        var n = name.ToLowerInvariant();

        if (n.Contains("dark") || n.Contains("theme") || n.Contains("accent"))
        {
            return ResourceCategory.Appearance;
        }

        if (n.Contains("explorer") || n.Contains("hidden"))
        {
            return ResourceCategory.Explorer;
        }

        if (n.Contains("taskbar") || n.Contains("widget"))
        {
            return ResourceCategory.Taskbar;
        }

        if (n.Contains("start") || n.Contains("search"))
        {
            return ResourceCategory.StartAndSearch;
        }

        if (n.Contains("edge"))
        {
            return ResourceCategory.Edge;
        }

        if (n.Contains("cloudcontent") || n.Contains("contentdelivery") || n.Contains("consumer") || n.Contains("telemetry") || n.Contains("ads") || n.StartsWith("cdm"))
        {
            return ResourceCategory.BloatRemoval;
        }

        if (n.Contains("appx") || n.Contains("debloat"))
        {
            return ResourceCategory.BloatRemoval;
        }

        if (n.Contains("power") || n.Contains("standby") || n.Contains("hiber"))
        {
            return ResourceCategory.Power;
        }

        if (n.Contains("bitlocker") || n.Contains("defender") || n.Contains("smartscreen"))
        {
            return ResourceCategory.Security;
        }

        return ResourceCategory.Other;
    }

    public static string? DescribeBy(string? type, string? name)
    {
        if (type is null)
        {
            return null;
        }

        if (type.StartsWith("Microsoft.WinGet/Package", StringComparison.OrdinalIgnoreCase))
        {
            return "Installs or pins a winget package.";
        }

        if (type.StartsWith("Microsoft.Windows/Registry", StringComparison.OrdinalIgnoreCase))
        {
            return "Sets a Windows registry value.";
        }

        if (type.StartsWith("Microsoft.DSC.Transitional/PowerShellScript", StringComparison.OrdinalIgnoreCase))
        {
            return "Runs a PowerShell get/test/set triple.";
        }

        return null;
    }
}

