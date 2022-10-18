using System;
using System.Collections.Generic;
using System.Data.Common;
using Microsoft.Data.SqlClient;
using System.Text.RegularExpressions;
using MSDF.DataChecker.Persistence.RuleExecutionLogDetails;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using MSDF.DataChecker.Persistence.EntityFramework;
using MSDF.DataChecker.Persistence.Settings;
using Microsoft.Extensions.Options;

namespace MSDF.DataChecker.Persistence
{
    public static class Utility
    {
        public static string ParseConnectionString(string connectionString, string Engine)
        {           
            connectionString = connectionString.Replace("Host=", "Server=");
            var RUNNING_IN_CONTAINER = Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER");
            if (!string.IsNullOrEmpty(RUNNING_IN_CONTAINER))
            {
                if (connectionString.ToLower().Contains("localhost"))
                    connectionString = connectionString.Replace("localhost", "host.docker.internal");
            }
            else
            {
                if (connectionString.ToLower().Contains("host.docker.internal"))
                    connectionString = connectionString.Replace("host.docker.internal", "localhost");
            }
            if (Engine== "SqlServer" || Engine =="sqlconnection")
            {
                var port = Regex.Match(connectionString, $@"Port(.+?)(?=;)").Groups[1].Value;
                  connectionString.Replace($"Port{port};", "");
            }
            return connectionString;
        }

        public static DbCommand AddDbCommandParameters(this DbCommand dBCommand, string connectionType, Dictionary<string, string> parameters = null)
        {
            foreach (KeyValuePair<string, string> parameter in parameters)
            {
                if (connectionType.ToLower().Contains("postgres"))
                {
                    int value = 0;
                    if (int.TryParse(parameter.Value, out value))
                    {
                        var sqlParameter = new Npgsql.NpgsqlParameter( parameter.Key, value);
                        dBCommand.Parameters.Add(sqlParameter);
                    }
                    else
                    {
                        var sqlParameter = new Npgsql.NpgsqlParameter(parameter.Key , parameter.Value);
                        dBCommand.Parameters.Add(sqlParameter);
                    }

                }
                else
                {
                    var sqlParameter = new SqlParameter( parameter.Key , parameter.Value);
                    dBCommand.Parameters.Add(sqlParameter);
                }
            }
            return dBCommand;
        }

        public static string GetNewTableScript(string connectionType, List<DestinationTableColumn> destinationTableInDbColumns, string destinationTable)
        {
            var sqlColumns = new List<string>();
            var sqlCreate = "";
            foreach (var column in destinationTableInDbColumns)
            {
                if (connectionType.ToLower().Contains("postgres"))
                {
                    string isNull = column.IsNullable ? " " : "NOT NULL";
                    if (column.Name == "id" && (column.Type == "int" || column.Type == "integer"))
                        sqlColumns.Add("\"Id\" integer NOT NULL GENERATED BY DEFAULT AS IDENTITY ( INCREMENT 1 START 1 MINVALUE 1 MAXVALUE 2147483647 CACHE 1 )");
                    else if (column.Type.Contains("varchar") || column.Type.Contains("text"))
                        sqlColumns.Add($"\"{column.Name}\"  text {isNull}");
                    else if (column.Type.Contains("date"))
                        sqlColumns.Add($"\"{column.Name}\" timestamp without time zone {isNull}");
                    else if (column.Type.Contains("uuid"))
                        sqlColumns.Add($"\"{column.Name}\" uuid {isNull}");
                    else
                        sqlColumns.Add($"\"{column.Name}\" {column.Type} {isNull}");
                    sqlCreate = $"CREATE TABLE destination.\"{destinationTable}\"({string.Join(",", sqlColumns)}) ";
                }
                else
                {
                    string isNull = column.IsNullable ? "NULL" : "NOT NULL";
                    if (column.Name == "id" && column.Type == "int")
                        sqlColumns.Add("[Id] [int] IDENTITY(1,1) NOT NULL");
                    else if (column.Type.Contains("varchar"))
                        sqlColumns.Add($"[{column.Name}] [{column.Type}](max) {isNull}");
                    else if (column.Type.Contains("datetime"))
                        sqlColumns.Add($"[{column.Name}] [{column.Type}](7) {isNull}");
                    else
                        sqlColumns.Add($"[{column.Name}] [{column.Type}] {isNull}");
                    sqlCreate = $"CREATE TABLE [destination].[{destinationTable}]({string.Join(",", sqlColumns)}) ";
                }

            }
            return sqlCreate;
        }


        public static List<DestinationTableColumn> ParseColumns(string connectionType, List<DestinationTableColumn> sourceTableInDbColumns)
        {
            foreach (var column in sourceTableInDbColumns)
            {   // from SQL to Postgres
                if (connectionType.ToLower().Contains("postgres"))
                {
                    switch (column.Type)
                    {
                        case "int":
                            column.Type = "integer";
                            break;
                        case "nvarchar":
                            column.Type = "text";
                            break;
                        default:
                            // Default stuff
                            break;
                    }
                }
                else
                {// from Postgres to SQL
                    switch (column.Type)
                    {
                        case "integer":
                            column.Type = "int";
                            break;
                        case "text":
                            column.Type = "nvarchar";
                            break;
                        default:
                            // Default stuff
                            break;
                    }
                }

            }
            return sourceTableInDbColumns;
        }

        public static string GetConnectionString(this RuleExecutionContext Source)
        {
            try
            {
                if (Source != null)
                    return Source.Database.GetDbConnection().ConnectionString;
                else
                    return "";
            }
            catch (Exception )
            {
                return "";
            }
        }

        public static RuleExecutionContext Changedatabase(this RuleExecutionContext Source, string engine, string configconnectionstringname = "")
        {
            try
            {
                if (engine.ToLower().Contains("postgres"))
                {
                    configconnectionstringname = Utility.ParseConnectionString(configconnectionstringname, engine);
                }
            }
            catch (Exception)
            {
                // set log item if required
            }

            return new RuleExecutionContext(configconnectionstringname, engine, new DbContextOptions<RuleExecutionContext>());
        }
    }
}
