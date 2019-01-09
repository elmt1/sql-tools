using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Configuration;

namespace AddSchemaName
{
    class AddSchemaName
    {
        static void Main(string[] args)
        {
            try
            {
                var builder = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("appsettings.json");

                IConfiguration configuration = builder.Build();

                var sqlProjectPath = configuration["SqlProjectPath"];
                if (!sqlProjectPath.EndsWith("\\"))
                {
                    sqlProjectPath += "\\";
                }

                var connectionString = configuration.GetConnectionString("SqlDB");

                var schemas = GetSchemas(connectionString);

                var count = configuration.GetSection("Criteria").GetChildren().Count();

                for (int i = 0; i < count; i++)
                {
                    RenameFiles(schemas, sqlProjectPath + configuration["Criteria:" + i + ":Folder"], configuration["Criteria:" + i + ":Search"]);
                }
            }
            catch (UnauthorizedAccessException UAEx)
            {
                Debug.WriteLine(UAEx.Message);
            }
            catch (PathTooLongException PathEx)
            {
                Debug.WriteLine(PathEx.Message);
            }
        }

        private static IList<string> GetSchemas(string connectionString)
        {
            IList<string> schemas = new List<string>();

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                var sql = "select distinct SCHEMA_NAME(schema_id) as schemaName from sys.tables";
                connection.Open();
                using (SqlCommand command = new SqlCommand(sql, connection))
                {
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            schemas.Add(reader.GetString(0));
                        }
                    }
                }
            }

            return schemas;
        }

        private static void RenameFiles(IList<string> schemas, string path, string search)
        {
            try
            {
                var files = new List<string>(Directory.EnumerateFiles(path));
                Console.WriteLine("rem Processing folder: " + path);

                foreach (var file in files)
                {

                    string text = System.IO.File.ReadAllText(file);
                    var start = text.ToLower().LastIndexOf(search.ToLower()) + search.ToLower().Length;

                    if (!start.Equals(search.Length - 1))
                    {
                        var schema = text.Substring(start, text.Substring(start).IndexOf(".")).Trim().Replace("[", String.Empty).Replace("]", String.Empty);
                        schema = schemas.Where(s => s.ToLower().Equals(schema.ToLower())).FirstOrDefault();

                        if (schema != null && !file.ToLower().Contains(schema.ToLower() + "."))
                        {
                            var newName = path + "\\" + Path.GetFileName(file.Substring(0, file.LastIndexOf(@"\") + 1) + schema + "." + file.Substring(file.LastIndexOf(@"\") + 1));
                            File.Move(file, newName);
                            Console.WriteLine("ren \"" + file + "\" " + newName);
                        }
                    }
                }
            }

            catch (System.IO.DirectoryNotFoundException e)
            {
                Console.WriteLine("rem Skipped folder: " + path);
            }
        }
    }
}
