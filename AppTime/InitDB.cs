using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppTime
{
    /**
     * 初始化数据库
     */
    class InitDB
    {
        private DB db;

        public InitDB()
        {
            db = DB.Instance;
        }

        public void Start()
        {
            // 创建数据表

            db.Execute("CREATE TABLE IF NOT EXISTS \"app\" (\"id\" INTEGER NOT NULL, \"process\" text NOT NULL," +
                "\"text\" TEXT NOT NULL DEFAULT process, " +
                "\"tagId\" INTEGER NOT NULL DEFAULT(0), " +
                "PRIMARY KEY(\"id\") ) WITHOUT ROWID");

            db.Execute("CREATE TABLE IF NOT EXISTS \"period\" ( \"timeStart\" DATETIME NOT NULL, \"timeEnd\" DATETIME NOT NULL, " +
                "\"winId\" INTEGER NOT NULL," +
                "PRIMARY KEY(\"timeStart\")," +
                "UNIQUE(\"timeStart\" ASC) )WITHOUT ROWID");

            db.Execute("CREATE TABLE IF NOT EXISTS \"tag\" ( \"id\" INTEGER NOT NULL, \"text\" TEXT NOT NULL," +
                "PRIMARY KEY(\"id\"), UNIQUE(\"id\" ASC), UNIQUE(\"text\" ASC) ) WITHOUT ROWID");

            db.Execute("CREATE TABLE IF NOT EXISTS \"win\" (\"id\" INTEGER NOT NULL," +
                "\"appId\" INTEGER NOT NULL, \"text\" TEXT NOT NULL, " +
                "\"tagId\" INTEGER NOT NULL DEFAULT(0), " +
                "PRIMARY KEY(\"id\") ) WITHOUT ROWID");


            // 创建索引

            long existIndex = (long)db.ExecuteData("SELECT count(*) FROM sqlite_master WHERE type=\"table\" AND name =\'is_index\'")[0][0];
            // 判断是否已经存在索引
            if (existIndex == 0)
            {
                db.Execute("CREATE UNIQUE INDEX \"ix_app\" ON \"app\"( \"process\" ASC )");
                db.Execute("CREATE INDEX \"ix_app_tagId\" ON \"app\" ( \"tagId\" ASC )");

                db.Execute("CREATE UNIQUE INDEX \"ix_period\" ON \"period\" ( \"timeStart\" ASC, \"timeEnd\" ASC )");

                db.Execute("CREATE UNIQUE INDEX \"ix_win\" ON \"win\" ( \"appId\" ASC, \"text\" ASC )");
                db.Execute("CREATE INDEX \"ix_win_tagId\" ON \"win\" (\"tagId\" ASC )");

                // 此表仅用于标识索引已经创建完成
                db.Execute("CREATE TABLE IF NOT EXISTS \"is_index\"( \"id\" INTEGER NOT NULL, PRIMARY KEY(\"id\"))");
            }
        }
    }
}
