// ReSharper disable once CheckNamespace

using System.Collections;
using System.Data;
using Microsoft.Data.Sqlite;

// ReSharper disable once CheckNamespace
namespace Tcqz.Configuration;

public sealed class SqliteSettingsStore
{
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
                    settings.Add(item.Key,item);
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

    public static IDictionary GetSettingsFromPath(string path, string sectionName)
    {
        IDictionary settings = new Hashtable();
        if (File.Exists(path))
        {
            var connectionString = new SqliteConnectionStringBuilder
            {
                ConnectionString = $@"Data Source={path}",
                //Password = "Skonda"
            }.ToString();
            using SqliteConnection connection = new SqliteConnection(connectionString);
            connection.Open();
            SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT\n" +
                                  "	B.GroupName,\n" +
                                  "	A.ValueName,\n" +
                                  "	A.Value,\n" +
                                  "	A.SerializeAs \n" +
                                  "FROM\n" +
                                  "	SettingValues AS A\n" +
                                  "	JOIN SettingGroups AS B ON A.GroupId == B.Id \n" +
                                  "WHERE\n" +
                                  "	B.GroupName = $sectionName";
            command.Parameters.AddWithValue("$sectionName", sectionName);
            var reader = command.ExecuteReader();
            while (reader.Read())
            {
                SettingStoreItem tempItem = new SettingStoreItem    
                {
                    ValueName = reader["ValueName"].ToString() ?? string.Empty,
                    GroupName = reader["GroupName"].ToString() ?? string.Empty,
                    Value = reader["Value"].ToString() ?? string.Empty
                };
                if (Enum.TryParse<SettingsSerializeAs>(reader["SerializeAs"].ToString(), out var tempSerializeAs))
                {
                    tempItem.SerializeAs = tempSerializeAs;
                }

                var key = tempItem.GroupName + "." + tempItem.ValueName;
                settings.Add(key, tempItem);
            }
        }

        return settings;
    }
    
    public static void WriteSettings(string sectionName, bool isRoaming, IDictionary newSettings)
    {

    }

    private static void WriteSettingsFromFile(string path, string sectionName,IDictionary newSettings)
    {
        //var connectStringBuilder = new SqliteConnectionStringBuilder()
        //{
        //    DataSource = path,
        //    Password = "Skonda"
        //};
        //using (SqliteConnection connection = new SqliteConnection(connectStringBuilder.ToString()))
        //{
        //    if (!File.Exists(path))
        //    {
        //        connectStringBuilder.Mode = SqliteOpenMode.ReadWriteCreate;
        //        var folderPath = Path.GetDirectoryName(path) ?? string.Empty;
        //        if (!string.IsNullOrEmpty(folderPath) && !Directory.Exists(folderPath))
        //        {
        //            Directory.CreateDirectory(folderPath);
        //        }

        //        connection.ConnectionString = connectStringBuilder.ToString();
        //        connection.Open();
        //        CreateSettingsTable(connection);

        //    }
        //    else
        //    {
        //        connection.Open();
        //    }
            
        //}
        var connectStringBuilder = new SqliteConnectionStringBuilder()
        {
            DataSource = path,
            Password = "Skonda"
        };
        
        using (var connection = new SqliteConnection())
        {
            var command = connection.CreateCommand();
            if (!File.Exists(path))
            {
                connectStringBuilder.Mode = SqliteOpenMode.ReadWriteCreate;
                var folderPath = Path.GetDirectoryName(path) ?? string.Empty;
                if (!string.IsNullOrEmpty(folderPath) && !Directory.Exists(folderPath))
                {
                    Directory.CreateDirectory(folderPath);
                }
                AddCreateSettingsTableCommand(command);
            }
            connection.ConnectionString = connectStringBuilder.ToString();
            connection.Open();
            
        }
    }
    private static void AddCreateSettingsTableCommand(SqliteCommand command)
    {

        command.CommandText += "-- ----------------------------\n" +
                              "-- Table structure for tb_SettingsGroup\n" +
                              "-- ----------------------------\n" +
                              "CREATE TABLE IF NOT EXISTS \"tb_SettingsGroup\" ( \"Id\" INTEGER NOT NULL, \"GroupName\" text NOT NULL, PRIMARY KEY ( \"Id\" ) );\n" +
                              "-- ----------------------------\n" +
                              "-- Indexes structure for table SettingGroups\n" +
                              "-- ----------------------------\n" +
                              "CREATE UNIQUE INDEX \"index_GroupName\" ON \"tb_SettingsGroup\" ( \"GroupName\" ASC );\n" +
                              "-- ----------------------------\n" +
                              "-- Table structure for tb_SettingsValue\n" +
                              "-- ----------------------------\n" +
                              "CREATE TABLE IF NOT EXISTS \"tb_SettingsValue\" (\n" +
                              "	\"Id\" INTEGER NOT NULL,\n" +
                              "	\"ValueName\" TEXT NOT NULL,\n" +
                              "	\"Value\" TEXT,\n" +
                              "	\"SerializeAs\" TEXT,\n" +
                              "	\"GroupId\" INTEGER,\n" +
                              "	PRIMARY KEY ( \"Id\" ),\n" +
                              "	CONSTRAINT \"GroupIdForeignKey\" FOREIGN KEY ( \"GroupId\" ) REFERENCES \"tb_SettingsGroup\" ( \"Id\" ) ON DELETE CASCADE ON UPDATE CASCADE \n" +
                              ");\n" +
                              "-- ----------------------------\n" +
                              "-- Indexes structure for table tb_SettingsValue\n" +
                              "-- ----------------------------\n" +
                              "CREATE INDEX \"index_ValueName\" ON \"tb_SettingsValue\" ( \"ValueName\" ASC );\n" +
                              "CREATE UNIQUE INDEX \"index_ValueNameWithGroupId\" ON \"tb_SettingsValue\" ( \"ValueName\" ASC, \"GroupId\" ASC );";

    }
}

public class SettingStoreItem
{
    public string ValueName { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public SettingsSerializeAs SerializeAs { get; set; }
    public string GroupName { get; set; } = string.Empty;
}

public enum SettingsSerializeAs
{
    String = 0,
    Xml = 1,
    Binary = 2,
    ProviderSpecific = 3
}