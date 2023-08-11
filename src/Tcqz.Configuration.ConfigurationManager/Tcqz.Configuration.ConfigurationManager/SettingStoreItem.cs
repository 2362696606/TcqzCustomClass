namespace Tcqz.Configuration;


public class SettingStoreItem
{
    public string ValueName { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public SettingsSerializeAs SerializeAs { get; set; }
    public string GroupName { get; set; } = string.Empty;
    
}