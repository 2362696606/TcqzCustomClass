using System.Collections;
using System.Configuration;
using System.Diagnostics;

// ReSharper disable once CheckNamespace
namespace Tcqz.Configuration
{
    public class SqliteSettingsProvider: SettingsProvider, IApplicationSettingsProvider
    {
        public override SettingsPropertyValueCollection GetPropertyValues(SettingsContext context, SettingsPropertyCollection collection)
        {
            string sectionName = GetSectionName(context);
            SettingsPropertyValueCollection values = new SettingsPropertyValueCollection();
            IDictionary appSettings = SqliteSettingsStore.GetSettings(sectionName, true);
            IDictionary userSettings = SqliteSettingsStore.GetSettings(sectionName, false);
            foreach (SettingsProperty setting in collection)
            {
                string settingName = setting.Name;
                SettingsPropertyValue value = new SettingsPropertyValue(setting);

                //First look for and handle "special" settings
                SpecialSettingAttribute? attr = setting.Attributes[typeof(SpecialSettingAttribute)] as SpecialSettingAttribute;
                bool isConnString = attr is { SpecialSetting: SpecialSetting.ConnectionString };
                if (isConnString)
                {
                    string connStringName = sectionName + "." + settingName;
                    if (appSettings[connStringName] is SettingStoreItem tempSettingItem)
                    {
                        value.PropertyValue = tempSettingItem.Value;
                    }
                    else if (setting.DefaultValue is string)
                    {
                        value.PropertyValue = setting.DefaultValue;
                    }
                    else
                    {
                        //No value found and no default specified
                        value.PropertyValue = string.Empty;
                    }

                    value.IsDirty = false; //reset IsDirty so that it is correct when SetPropertyValues is called
                    values.Add(value);
                    continue;
                }

                bool isUserSetting = IsUserSetting(setting);

                IDictionary settings = isUserSetting ? userSettings : appSettings;

                if (settings.Contains(settingName))
                {
                    SettingStoreItem? ss = (SettingStoreItem?)settings[settingName];
                    string valueString = ss?.Value ?? string.Empty;

                    // We need to un-escape string serialized values
                    if (ss?.SerializeAs == SettingsSerializeAs.String)
                    {
                        value.SerializedValue = valueString;
                    }

                }
                else if (setting.DefaultValue != null)
                {
                    value.SerializedValue = setting.DefaultValue;
                }
                else
                {
                    //No value found and no default specified
                    value.PropertyValue = null;
                }

                value.IsDirty = false; //reset IsDirty so that it is correct when SetPropertyValues is called
                values.Add(value);
            }
            return values;
        }
        /// <summary>
        /// 获取设置组名
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        private string GetSectionName(SettingsContext context)
        {
            string groupName = context["GroupName"]?.ToString() ?? string.Empty;
            string key = context["SettingsKey"]?.ToString()??string.Empty;

            Debug.Assert(string.IsNullOrEmpty(groupName), "SettingsContext did not have a GroupName!");

            string sectionName = groupName;

            if (!string.IsNullOrEmpty(key))
            {
                sectionName = sectionName + "." + key;
            }

            return sectionName;
        }
        /// <summary>
        /// This provider needs settings to be marked with either the UserScopedSettingAttribute or the
        /// ApplicationScopedSettingAttribute. This method determines whether this setting is user-scoped
        /// or not. It will throw if none or both of the attributes are present. 
        /// </summary>
        /// <param name="setting"></param>
        /// <returns></returns>
        /// <exception cref="ConfigurationErrorsException"></exception>
        private static bool IsUserSetting(SettingsProperty setting)
        {
            bool isUser = setting.Attributes[typeof(UserScopedSettingAttribute)] is UserScopedSettingAttribute;
            bool isApp = setting.Attributes[typeof(ApplicationScopedSettingAttribute)] is ApplicationScopedSettingAttribute;

            if (isUser && isApp)
            {
                throw new ConfigurationErrorsException($"{setting.Name}同时包含ApplicationScope与UserScope");
            }
            else if (!(isUser || isApp))
            {
                throw new ConfigurationErrorsException($"{setting.Name}未定义ApplicationScope或UserScope");
            }

            return isUser;
        }
        
        public override void SetPropertyValues(SettingsContext context, SettingsPropertyValueCollection collection)
        {
            throw new NotImplementedException();
        }

        public override string ApplicationName { get; set; } = string.Empty;
        public SettingsPropertyValue GetPreviousVersion(SettingsContext context, SettingsProperty property)
        {
            throw new NotImplementedException();
        }

        public void Reset(SettingsContext context)
        {
            throw new NotImplementedException();
        }

        public void Upgrade(SettingsContext context, SettingsPropertyCollection properties)
        {
            throw new NotImplementedException();
        }
    }
}