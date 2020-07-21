using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.OleDb;
using System.Data.SQLite;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace AppTime
{
    class DB
    {

        public readonly static DB Instance = new DB();

        DbConnection conn;
        DbProviderFactory factory = SQLiteFactory.Instance;

        public DB()
        { 
            var connectionString = new SQLiteConnectionStringBuilder()
            {
                 DataSource= Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "data.db")
                 
            }.ToString();
            conn =  new SQLiteConnection
            {
                ConnectionString = connectionString
            }; 
        }

        T execute<T>(Func<DbCommand, T> handler, string sql, params object[] args)
        {
            lock (this)
            {
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = sql;
                for (var i = 0; i < args.Length; i++)
                {
                    var arg = args[i];
                    if (!(arg is DbParameter param))
                    {
                        param = factory.CreateParameter();
                        param.ParameterName = $"@v{i}";
                        param.Value = arg;
                    } 
                    cmd.Parameters.Add(param);
                }

                var result = handler(cmd);
                conn.Close();
                return result;
            }
        }

        public int Execute(string sql, params object[] args)
        {
            return execute(cmd => cmd.ExecuteNonQuery(), sql, args) ;
        }

        public List<object[]> ExecuteData(string sql, params object[] args)
        {
            return execute(cmd =>
            {
                var result = new List<object[]>();
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    var row = new object[reader.FieldCount];
                    reader.GetValues(row);
                    result.Add(row);
                }
                return result;
            }, sql, args);

        }

        public T ExecuteValue<T>(string sql, params object[] args)
        {
            return (T)Convert.ChangeType(ExecuteData(sql, args)[0][0], typeof(T));
        }


        public T[] ExecuteColumn<T>(string sql, params object[] args)
        {
            var data = ExecuteData(sql, args);
            var result = new T[data.Count];
            for(var i = 0;i<data.Count;i++)
            {
                result[i] = (T)Convert.ChangeType(data[i][0], typeof(T)); 
            }
            return result;
        }

        public DataTable ExecuteTable(string sql, params object[] args)
        {
            return execute(cmd =>
            {
                using var adapter = factory.CreateDataAdapter();
                adapter.SelectCommand = cmd;
                var result = new DataTable();
                adapter.Fill(result);
                return result;
            }, sql, args);
        }

        public IEnumerable<dynamic> ExecuteDynamic(string sql, params object[] args)
        {
            return from r in ExecuteTable(sql, args).AsEnumerable() select new DynamicDataRow(r); 
        }


    }
 

    class DynamicDataRow : DynamicObject
    {
        DataRow row;
        public DynamicDataRow(DataRow row)
        {
            this.row = row;
        }

        public override bool TryGetMember(GetMemberBinder binder, out object result)
        {
            result = row[binder.Name];
            return true;
        }
    }
}
