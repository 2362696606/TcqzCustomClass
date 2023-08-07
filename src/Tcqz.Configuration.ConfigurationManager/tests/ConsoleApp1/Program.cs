// See https://aka.ms/new-console-template for more information

using System.Reflection;
using Microsoft.Data.Sqlite;
using Tcqz.Configuration;

var path = @"D:\Code\Git\Com.Skonda\AppSetting.db";
using (var connection = new SqliteConnection($"Data Source={path}"))
{
    connection.Open();
    var command = connection.CreateCommand();
    command.CommandText = "SELECT\n" +
                          "	A.Id,\n" +
                          "	A.ValueName,\n" +
                          "	A.Value,\n" +
                          "	A.SerializeAs,\n" +
                          "	A.GroupId,\n" +
                          "	B.Id,\n" +
                          "	B.GroupName\n" +
                          "FROM\n" +
                          "	tb_SettingsValue AS A\n" +
                          "	JOIN tb_SettingsGroup AS B ON A.GroupId = B.Id \n" +
                          "WHERE\n" +
                          "	B.Id =2";
    var reader = command.ExecuteReader();
    while (reader.Read())
    {
        Console.WriteLine(reader["A.Id"] + "\t" 
            + reader["ValueName"] + "\t" 
            + reader["Value"] + "\t" 
            + reader["SerializeAs"] + "\t" 
            + reader["GroupId"] + "\t" 
            + reader["Id"] + "\t" 
            + reader["GroupName"]);
    }
}
Console.ReadLine();
