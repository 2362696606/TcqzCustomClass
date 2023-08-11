// See https://aka.ms/new-console-template for more information

using System.Reflection;
using Microsoft.Data.Sqlite;
using Tcqz.Configuration;

var path = @"C:\Users\23626\Documents\Code\Git\tangqinzheng\Com.Skonda\AppSetting.db";
SqliteDataReader reader;
using (var connection = new SqliteConnection($"Data Source={path}"))
{
    connection.Open();
    var command = connection.CreateCommand();
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
    reader = command.ExecuteReader();

    while (reader.Read())
    {
        string writeLine = string.Empty;
        for (int i = 0; i < reader.FieldCount; i++)
        {
            writeLine += reader[i] + "\t";
        }

        Console.WriteLine(writeLine);
    }
    command.Dispose();
    var getGroupIdCommand = connection.CreateCommand();
    getGroupIdCommand.CommandText = "SELECT\n" +
                                    "	Id \n" +
                                    "FROM\n" +
                                    "	tb_SettingsGroup \n" +
                                    "WHERE\n" +
                                    "	GroupName = 'TestGroup_2'";
    var groupIdObj = getGroupIdCommand.ExecuteScalar();
    var groupId = Convert.ToInt32(groupIdObj);
    Console.WriteLine(groupId.ToString() ?? "null");
}
Console.ReadLine();
