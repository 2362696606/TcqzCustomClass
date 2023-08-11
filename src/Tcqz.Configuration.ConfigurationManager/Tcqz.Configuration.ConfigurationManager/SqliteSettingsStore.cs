// ReSharper disable once CheckNamespace

using System.Collections;
using System.Data;
using Microsoft.Data.Sqlite;

// ReSharper disable once CheckNamespace
namespace Tcqz.Configuration;

public static class SqliteSettingsStore
{
    /// <summary>
    /// 读取设置
    /// </summary>
    /// <param name="sectionName">SectionName</param>
    /// <param name="isUserScoped">IsUserScoped</param>
    /// <returns>读取结果</returns>
    public static IDictionary GetSettings(string sectionName, bool isUserScoped)
    {
        IDictionary settings = new Hashtable();
        if (isUserScoped)
        {
            if (File.Exists(ConfigPaths.Instance.ApplicationConfigUri))
            {
                return GetSettingsFromPath(ConfigPaths.Instance.ApplicationConfigUri, sectionName);
            }
        }
        else
        {
            if (File.Exists(ConfigPaths.Instance.RoamingConfigFilename))
            {
                var roaming = GetSettingsFromPath(ConfigPaths.Instance.RoamingConfigFilename, sectionName);
                foreach (DictionaryEntry item in roaming)
                {
                    settings.Add(item.Key, item);
                }
            }
            if (File.Exists(ConfigPaths.Instance.LocalConfigFilename))
            {
                var local = GetSettingsFromPath(ConfigPaths.Instance.LocalConfigFilename, sectionName);
                foreach (DictionaryEntry item in local)
                {
                    settings.Add(item.Key, item);
                }
            }
        }

        return settings;
    }
    /// <summary>
    /// 通过Path获取SettingStoreItem列表
    /// </summary>
    /// <param name="path">路径</param>
    /// <param name="sectionName">配置名</param>
    /// <returns>SettingStoreItem字典</returns>
    private static IDictionary GetSettingsFromPath(string path, string sectionName)
    {
        IDictionary settings = new Hashtable();
        if (!File.Exists(path)) return settings;
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Password = "Skonda"
        }.ToString();
        using var connection = new SqliteConnection(connectionString);
        connection.Open();
        SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT\n" +
                              "	A.Id,\n" +
                              "	A.ValueName,\n" +
                              "	A.Value,\n" +
                              "	A.SerializeAs,\n" +
                              "	B.Id,\n" +
                              "	B.GroupName \n" +
                              "FROM\n" +
                              "	tb_SettingsValue AS A\n" +
                              "	JOIN tb_SettingsGroup AS B ON A.GroupId = B.Id \n" +
                              "WHERE\n" +
                              "	B.GroupName = 'TestGroup_1';";
        command.Parameters.AddWithValue("$sectionName", sectionName);
        var reader = command.ExecuteReader();
        while (reader.Read())
        {
            SettingStoreItem tempItem = new SettingStoreItem
            {
                ValueName = reader["ValueName"].ToString() ?? string.Empty,
                Value = reader["Value"].ToString() ?? string.Empty,
                GroupName = reader["GroupName"].ToString() ?? string.Empty,
            };
            if (Enum.TryParse<SettingsSerializeAs>(reader["SerializeAs"].ToString(), out var tempSerializeAs))
            {
                tempItem.SerializeAs = tempSerializeAs;
            }

            var key = tempItem.GroupName + "." + tempItem.ValueName;
            settings.Add(key, tempItem);
        }

        return settings;
    }
    /// <summary>
    /// 写入设置
    /// </summary>
    /// <param name="sectionName">SectionName</param>
    /// <param name="isRoaming">IsRoaming</param>
    /// <param name="newSettings">New Settings</param>
    public static void WriteSettings(string sectionName, bool isRoaming, IDictionary newSettings)
    {
        WriteSettingsFromPath(
            isRoaming ? ConfigPaths.Instance.RoamingConfigFilename : ConfigPaths.Instance.LocalConfigFilename,
            sectionName, newSettings);
    }

    private static void WriteSettingsFromPath(string path, string sectionName, IDictionary newSettings)
    {
        var connection = GetConnection(path);
        if (connection.State < ConnectionState.Open)
        {
            connection.Open();
        }
        var getSettingsGroupCommand = connection.CreateCommand();
        getSettingsGroupCommand.CommandText = "SELECT\n" +
                                              "	Id \n" +
                                              "FROM\n" +
                                              "	tb_SettingsGroup \n" +
                                              "WHERE\n" +
                                              "	GroupName = 'TestGroup_1'";
        var groupId = Convert.ToInt32(getSettingsGroupCommand.ExecuteScalar());
        getSettingsGroupCommand.Dispose();
        IDictionary readResult = new Hashtable();
        if (groupId != 0)
        {
            var getExistValuesCommand = connection.CreateCommand();
            getExistValuesCommand.CommandText = "SELECT\n" +
                                                "	A.Id,\n" +
                                                "	A.ValueName,\n" +
                                                "	A.Value,\n" +
                                                "	A.SerializeAs,\n" +
                                                "	B.Id,\n" +
                                                "	B.GroupName \n" +
                                                "FROM\n" +
                                                "	tb_SettingsValue AS A\n" +
                                                "	JOIN tb_SettingsGroup AS B ON A.GroupId = B.Id \n" +
                                                "WHERE\n" +
                                                $"	B.GroupName = '{sectionName}';";
            var getExistValuesReader = getExistValuesCommand.ExecuteReader();
            while (getExistValuesReader.Read())
            {
                var temp = new
                {
                    ValueId = (int)getExistValuesReader[0],
                    ValueName = getExistValuesReader[1].ToString(),
                    Value = getExistValuesReader[2].ToString(),
                    SerializeAs = getExistValuesReader[3].ToString(),
                    GroupId = (int)getExistValuesReader[4],
                    GroupName = getExistValuesReader[5].ToString()
                };
                string key = temp.GroupName + "." + temp.ValueName;
                readResult.Add(key, temp);
            }
            getExistValuesReader.Dispose();
        }
        else
        {
            var insertGroupCommand = connection.CreateCommand();
            insertGroupCommand.CommandText = "INSERT INTO \"tb_SettingsGroup\" ( GroupName )\n" +
                                             "VALUES\n" +
                                             $"	( '{sectionName}' );\n" +
                                             "SELECT\n" +
                                             "	Id \n" +
                                             "FROM\n" +
                                             "	\"tb_SettingsGroup\" \n" +
                                             "WHERE\n" +
                                             $"	GroupName = '{sectionName}';";
            groupId = Convert.ToInt32(insertGroupCommand.ExecuteScalar());
        }
        IDictionary updateDictionary = new Hashtable();
        IDictionary addDictionary = new Hashtable();
        foreach (DictionaryEntry dictionaryEntry in newSettings)
        {
            if (readResult.Contains(dictionaryEntry.Key))
            {
                updateDictionary.Add(dictionaryEntry.Key, dictionaryEntry);
            }
            else
            {
                addDictionary.Add(dictionaryEntry.Key, dictionaryEntry);
            }
        }

        if (updateDictionary.Count > 0)
        {
            using var updateCommand = connection.CreateCommand();
            foreach (DictionaryEntry dictionaryEntry in updateDictionary)
            {
                var valueId = (int?)((readResult[dictionaryEntry.Key] as dynamic)?.ValueId);
                if(valueId == null) continue;
                updateCommand.CommandText += "UPDATE tb_SettingsValue \n" +
                                             $"SET Value = '{dictionaryEntry.Value}' \n" +
                                             "WHERE\n" +
                                             $"	Id = {valueId}; \n";
            }
            updateCommand.ExecuteNonQuery();
        }
        if (addDictionary.Count > 0)
        {
            using var addCommand = connection.CreateCommand();
            foreach (DictionaryEntry dictionaryEntry in addDictionary)
            {
                SettingStoreItem item = (SettingStoreItem)dictionaryEntry.Value!;
                addCommand.CommandText +=
                    "INSERT INTO \"tb_SettingsValue\" ( ValueName, Value, SerializeAs, GroupId )\n" +
                    "VALUES\n" +
                    $"	( '{item.ValueName}', '{item.Value}', '{item.SerializeAs}', {groupId} );";
            }
            addCommand.ExecuteNonQuery();
        }
    }

    private static SqliteConnection GetConnection(string path)
    {
        var connectStringBuilder = new SqliteConnectionStringBuilder()
        {
            DataSource = path,
            Password = "Skonda"
        };
        var connection = new SqliteConnection(connectStringBuilder.ToString());
        if (File.Exists(path)) return connection;
        var folderPath = Path.GetDirectoryName(path) ?? string.Empty;
        if (!string.IsNullOrEmpty(folderPath) && !Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }
        var createTablesCommand = connection.CreateCommand();
        createTablesCommand.CommandText = "CREATE TABLE\n" +
                                          "IF\n" +
                                          "	NOT EXISTS 'tb_SettingsGroup' ( 'Id' INTEGER NOT NULL, 'GroupName' TEXT NOT NULL, PRIMARY KEY ( 'Id' ) );\n" +
                                          "CREATE UNIQUE INDEX\n" +
                                          "IF\n" +
                                          "	NOT EXISTS 'index_GroupName' ON 'tb_SettingsGroup' ( 'GroupName' ASC );\n" +
                                          "CREATE TABLE\n" +
                                          "IF\n" +
                                          "	NOT EXISTS 'tb_SettingsValue' (\n" +
                                          "		'Id' INTEGER NOT NULL,\n" +
                                          "		'ValueName' TEXT NOT NULL,\n" +
                                          "		'Value' TEXT,\n" +
                                          "		'SerializeAs' TEXT,\n" +
                                          "		'GroupId' INTEGER,\n" +
                                          "		PRIMARY KEY ( 'Id' ),\n" +
                                          "		CONSTRAINT 'GroupIdForeignKey' FOREIGN KEY ( 'GroupId' ) REFERENCES 'tb_SettingsGroup' ( 'Id' ) ON DELETE CASCADE ON UPDATE CASCADE \n" +
                                          "	);\n" +
                                          "CREATE INDEX\n" +
                                          "IF\n" +
                                          "	NOT EXISTS 'index_ValueName' ON 'tb_SettingsValue' ( 'ValueName' ASC );\n" +
                                          "CREATE UNIQUE INDEX\n" +
                                          "IF\n" +
                                          "	NOT EXISTS 'index_ValueNameWithGroupId' ON 'tb_SettingsValue' ( 'ValueName' ASC, 'GroupId' ASC );";
        connection.Open();
        createTablesCommand.ExecuteNonQuery();
        connection.Close();
        return connection;
    }
}